"""Publish and release only raw recordings created by a fresh owned experiment.

Existing captures cannot be adopted. Each release requires complete verified
archives in the run and backup, an unchanged raw inventory, and a durable intent
record. Failures retain all unpublished/remaining raw files; there is no resume
or recursive-purge API. No model, protocol or scientific bound is changed.
"""
import argparse
from contextlib import contextmanager, ExitStack
from datetime import datetime, timezone
import json
import os
from pathlib import Path
import shutil
import stat
import uuid
from native_evidence_archive import EvidenceArchive, digest_file, inventory, pack, physical
from native_evidence_view import EvidenceView

VERSION = 'owned-new-native-capture-publication-v1'
CONDITIONS = ('quiet', 'unit-left', 'strong-left', 'strong-right', 'strong-cut', 'strong-fast', 'strong-retry')


def read(path):
    return json.loads(physical(path).read_text(encoding='utf-8-sig'))


def write_new(path, value):
    with physical(path).open('x', encoding='utf-8') as stream:
        json.dump(value, stream, indent=2, allow_nan=False); stream.write('\n')
        stream.flush(); os.fsync(stream.fileno())


def checked(path, *, exists=True):
    path = Path(path)
    if not path.is_absolute() or path != Path(os.path.abspath(path)):
        raise ValueError('Canonical absolute capture path required.')
    # Inspect every lexical ancestor before resolving: resolving first could
    # silently turn a junction or symlink into an unrelated deletion target.
    for item in reversed([path, *path.parents]):
        try:
            info = physical(item).lstat()
        except FileNotFoundError:
            continue
        if stat.S_ISLNK(info.st_mode) or getattr(info, 'st_file_attributes', 0) & 0x400:
            raise ValueError('Capture paths cannot traverse reparse points: ' + str(item))
    resolved = path.resolve(strict=exists)
    if resolved != path:
        raise ValueError('Capture path changed during resolution.')
    return path


def key_name(key):
    if key not in CONDITIONS:
        raise ValueError('Only a declared motor condition may be published.')
    return key


def initialize(package, backup):
    package = checked(package); backup = checked(backup)
    if package == backup or package in backup.parents or backup in package.parents:
        raise ValueError('Run and backup must have separate non-overlapping roots.')
    raw = package / 'native-bridge'; local = package / 'capture-storage'; mirror = backup / 'capture-storage'
    for path in (raw, local, mirror):
        checked(path, exists=False)
        if physical(path).exists():
            raise FileExistsError('Cannot adopt earlier raw records or publication state: ' + str(path))
    physical(local).mkdir(); physical(mirror).mkdir()
    owner = dict(version=VERSION, owner=str(uuid.uuid4()), package=str(package), backup=str(backup),
        rawRoot=str(raw), conditions=list(CONDITIONS), createdUtc=datetime.now(timezone.utc).isoformat(),
        rawRootAbsentAtCreation=True, existingCapturesMayBeAdopted=False)
    write_new(local / 'ownership.json', owner)
    # Both identities precede the native capture. Failed initialization is
    # preserved and cannot be mistaken for a valid resumable store.
    with physical(local / 'ownership.json').open('rb') as source, physical(mirror / 'ownership.json').open('xb') as target:
        shutil.copyfileobj(source, target); target.flush(); os.fsync(target.fileno())
    if digest_file(local / 'ownership.json') != digest_file(mirror / 'ownership.json'):
        raise ValueError('Publication ownership copy differs.')
    return owner


class CaptureStore:
    def __init__(self, package):
        self.package = checked(package); self.local = checked(self.package / 'capture-storage')
        self.owner = read(self.local / 'ownership.json'); self.owner_hash = digest_file(self.local / 'ownership.json')
        self.backup = checked(self.owner['backup']); self.mirror = checked(self.backup / 'capture-storage')
        if (self.owner['version'] != VERSION or self.owner['package'] != str(self.package)
                or self.owner['rawRoot'] != str(self.package / 'native-bridge')
                or self.owner['conditions'] != list(CONDITIONS) or not self.owner['rawRootAbsentAtCreation']
                or self.owner['existingCapturesMayBeAdopted'] or str(uuid.UUID(self.owner['owner'])) != self.owner['owner']
                or self.package == self.backup or self.package in self.backup.parents or self.backup in self.package.parents):
            raise ValueError('Unexpected new-capture ownership.')
        self._ownership()

    def _ownership(self):
        for root in (self.local, self.mirror):
            checked(root)
            if digest_file(root / 'ownership.json') != self.owner_hash:
                raise ValueError('Capture ownership changed.')

    def _paths(self, key):
        key_name(key); self._ownership()
        source = checked(self.package / 'native-bridge' / key)
        raw_root = checked(self.package / 'native-bridge')
        if source.parent != raw_root or raw_root.parent != self.package:
            raise ValueError('Raw condition escaped its owned root.')
        for root in (self.local, self.mirror):
            checked(root / (key + '.zip'), exists=False)
        return source, self.local / (key + '.zip'), self.mirror / (key + '.zip')

    def seal(self, key, completion_hash, *, progress=None):
        source, archive, mirror = self._paths(key)
        if physical(source / 'failure.json').exists() or not physical(source / 'final.json').is_file():
            raise ValueError('Failed or incomplete conditions must retain their raw prefixes.')
        if digest_file(source / 'final.json') != completion_hash.upper():
            raise ValueError('Closed native completion record changed.')
        for suffix in ('.started.json', '.published.json', '.release-intent.json', '.released.json', '.failed.json'):
            if physical(self.local / (key + suffix)).exists():
                raise FileExistsError('Publication attempts cannot overwrite or silently resume earlier work.')
        if physical(archive).exists() or physical(mirror).exists():
            raise FileExistsError('Archive destination already exists.')
        write_new(self.local / (key + '.started.json'), dict(owner=self.owner['owner'], key=key,
            source=str(source), completionSha256=completion_hash.upper(), rawFilesRemoved=0))
        try:
            packed = pack(source, archive, progress=progress)
            # Copy without replacement, flush, and verify both archive bytes and
            # every member before any raw-file release can be admitted.
            with physical(archive).open('rb') as original, physical(mirror).open('xb') as target:
                shutil.copyfileobj(original, target, length=1024*1024); target.flush(); os.fsync(target.fileno())
            with EvidenceArchive(archive, packed['archiveSha256']) as first, EvidenceArchive(mirror, packed['archiveSha256']) as second:
                first.verify(progress=progress); second.verify(first.manifest['files'], progress)
                if first.entries['final.json']['sha256'] != completion_hash.upper():
                    raise ValueError('Published completion record differs.')
            result = dict(version=VERSION, owner=self.owner['owner'], key=key, source=str(source),
                archive=str(archive), backupArchive=str(mirror), archiveSha256=packed['archiveSha256'],
                files=packed['files'], uncompressedBytes=packed['uncompressedBytes'], archiveBytes=packed['archiveBytes'],
                completionSha256=completion_hash.upper(), bothArchivesAndMembersVerified=True,
                rawFilesRemoved=0, copiesShareDrive=self.package.drive.casefold()==self.backup.drive.casefold())
            write_new(self.local / (key + '.published.json'), result)
            write_new(self.mirror / (key + '.published.json'), result)
            return result
        except BaseException as error:
            write_new(self.local / (key + '.failed.json'), dict(stage='publication', owner=self.owner['owner'],
                key=key, error=repr(error), rawFilesRemoved=0))
            raise

    def release(self, key, *, progress=None):
        source, archive, mirror = self._paths(key)
        record = read(self.local / (key + '.published.json'))
        if record != read(self.mirror / (key + '.published.json')):
            raise ValueError('Publication receipts differ.')
        if (record['version'] != VERSION or record['owner'] != self.owner['owner'] or record['key'] != key
                or record['source'] != str(source) or record['archive'] != str(archive) or record['backupArchive'] != str(mirror)
                or not record['bothArchivesAndMembersVerified'] or record['rawFilesRemoved'] != 0):
            raise ValueError('Unowned or unverified release request.')
        if physical(self.local / (key + '.failed.json')).exists():
            raise ValueError('An interrupted capture publication requires review, not automatic release.')
        for suffix in ('.release-intent.json', '.released.json'):
            if physical(self.local / (key + suffix)).exists():
                raise FileExistsError('Do not resume or repeat raw release.')
        with EvidenceArchive(archive, record['archiveSha256']) as first, EvidenceArchive(mirror, record['archiveSha256']) as second:
            first.verify(progress=progress); second.verify(first.manifest['files'], progress)
            entries = first.manifest['files']
            if len(entries) != record['files'] or first.manifest['totalBytes'] != record['uncompressedBytes']:
                raise ValueError('Published inventory changed.')
            actual = inventory(source)
            if set(actual) != set(first.entries):
                raise ValueError('Raw inventory changed after publication.')
            for row in entries:
                target = checked(source / row['path'])
                if source not in target.parents or actual[row['path']][0] != row['bytes'] or digest_file(target) != row['sha256']:
                    raise ValueError('Raw bytes changed after publication: ' + row['path'])
            if inventory(source) != actual:
                raise ValueError('Raw inventory changed during release verification.')
        self._ownership()
        directories = set(); pending = [source]
        while pending:
            directory = checked(pending.pop()); directories.add(directory)
            for child in physical(directory).iterdir():
                info = child.lstat()
                if stat.S_ISLNK(info.st_mode) or getattr(info, 'st_file_attributes', 0) & 0x400:
                    raise ValueError('Raw directory tree changed before removal.')
                if stat.S_ISDIR(info.st_mode):
                    pending.append(directory / child.name)
        if inventory(source) != actual:
            raise ValueError('Raw inventory changed before recording release intent.')
        intent = dict(owner=self.owner['owner'], key=key, source=str(source), archiveSha256=record['archiveSha256'],
            files=record['files'], bothArchivesAndMembersVerified=True, rawInventoryExactlyVerified=True,
            recordedDirectories=sorted(p.relative_to(source).as_posix() for p in directories))
        write_new(self.local / (key + '.release-intent.json'), intent)
        write_new(self.mirror / (key + '.release-intent.json'), intent)
        removed = 0
        try:
            # Only enumerated, hash-verified files inside this fresh condition
            # are removed. Never use recursive deletion, wildcards or a shell.
            for row in entries:
                if removed % 1000 == 0:
                    self._ownership()
                if source.parent != self.package / 'native-bridge':
                    raise ValueError('Owned raw root changed before removal.')
                target = checked(source / row['path'])
                if source not in target.parents or digest_file(target) != row['sha256']:
                    raise ValueError('Raw file changed immediately before removal.')
                physical(target).unlink(); removed += 1
                if progress and (removed % 10000 == 0 or removed == len(entries)):
                    progress('release-owned-raw', removed, len(entries))
            for directory in sorted(directories, key=lambda p: len(p.parts), reverse=True):
                checked(directory)
                if directory != source and source not in directory.parents:
                    raise ValueError('Directory escaped its condition.')
                physical(directory).rmdir()  # Refuses any unexpected remaining file.
            result = dict(intent, completed=True, rawFilesRemoved=removed, originalFileBytesRetainedInBothArchives=True,
                existingCapturesTouched=False, modelsExecuted=False, testsExecuted=False)
            write_new(self.local / (key + '.released.json'), result)
            write_new(self.mirror / (key + '.released.json'), result)
            return result
        except BaseException as error:
            write_new(self.local / (key + '.failed.json'), dict(stage='release', owner=self.owner['owner'], key=key,
                error=repr(error), rawFilesRemoved=removed, remainingRawPreserved=True, completeArchivesRetained=True))
            raise


def publish_closed(package, key):
    store = CaptureStore(package); key_name(key)
    closed = read(store.local / (key + '.closed.json'))
    if (closed['owner'] != store.owner['owner'] or closed['key'] != key or not closed['nativeProcessExitedSuccessfully']
            or not closed['recordingScopesDisposed'] or closed['physicsSteps'] != 31996):
        raise ValueError('Require the new runner\'s completed and disposed native condition.')
    progress = lambda phase, done, total: print(f'{key} {phase} {done}/{total}', flush=True)
    store.seal(key, closed['completionSha256'], progress=progress)
    store.release(key, progress=progress)
    print('Published exact original records in both archives; released only this new raw condition: ' + key, flush=True)


def snapshot_view(package):
    store = CaptureStore(package); root = checked(store.package / 'native-bridge')
    protocol = read(root / 'protocol.json'); outcome = read(root / 'results.json')
    if (protocol['version'] != 'rubral-neuronal-native-hinge-v1' or protocol['physicsResolution'] != 3
            or protocol['muscleSubsteps'] != 1 or protocol['steps'] != 31996 or protocol['strongLeftOnly']
            or [row['key'] for row in protocol['conditions']] != list(CONDITIONS)
            or not outcome['completed'] or not outcome['sourceBodyAccountingPassed']
            or not outcome['observedNeuronalMuscleMovement'] or outcome['actualPhysicsSteps'] != 223972
            or [row['condition'] for row in outcome['cases']] != list(CONDITIONS)
            or not all(row['completed'] and row['physicsSteps'] == 31996 for row in outcome['cases'])):
        raise ValueError('Require the complete unchanged full motor matrix at h/4 body and muscle.')
    archives = []
    for key in CONDITIONS:
        released = read(store.local / (key + '.released.json')); published = read(store.local / (key + '.published.json'))
        if (released != read(store.mirror / (key + '.released.json')) or not released['completed']
                or published != read(store.mirror / (key + '.published.json')) or published['owner'] != store.owner['owner']
                or released['owner'] != store.owner['owner'] or released['archiveSha256'] != published['archiveSha256']
                or released['rawFilesRemoved'] != published['files'] or physical(root / key).exists()):
            raise ValueError('A condition has not completed exact publication and owned release.')
        for location in (store.local, store.mirror):
            if digest_file(location / (key + '.zip')) != published['archiveSha256']:
                raise ValueError('A released archive changed before complete-capture accounting.')
        archives.append(dict(key=key, prefix='native-bridge/' + key, path=key + '.zip', sha256=published['archiveSha256']))
    headers = store.local / 'headers'; physical(headers).mkdir()
    files = [('native-bridge/' + name, root / name) for name in inventory(root)]
    files += [('reference/' + name, store.package / 'reference' / name) for name in ('failed-request-000033.json','last-reply-000032.json')]
    rows = []
    for relative, source in files:
        source = checked(source); target = headers / relative; physical(target.parent).mkdir(parents=True, exist_ok=True)
        with physical(source).open('rb') as original, physical(target).open('xb') as copy:
            shutil.copyfileobj(original, copy); copy.flush(); os.fsync(copy.fileno())
        digest = digest_file(source)
        if digest_file(target) != digest:
            raise ValueError('Header snapshot differs from its original.')
        rows.append(dict(path=relative,bytes=physical(source).stat().st_size,sha256=digest))
    result = dict(version=VERSION, owner=store.owner['owner'], archives=archives, headers=sorted(rows,key=lambda r:r['path']),
        completed=True, allSevenConditionsArchived=True, actualPhysicsSteps=223972,
        sourcePacketNativeAccountingPending=True, numericalAccuracyPending=True, physiologyQualified=False, avatarRecoveryAssessed=False)
    write_new(store.local / 'view.json', result)
    return result


@contextmanager
def open_view(package):
    store = CaptureStore(package); spec = read(store.local / 'view.json')
    if (spec['version'] != VERSION or spec['owner'] != store.owner['owner'] or not spec['completed']
            or [row['key'] for row in spec['archives']] != list(CONDITIONS)):
        raise ValueError('Require a complete owned archive view.')
    with ExitStack() as resources:
        mounts = {}
        for row in spec['archives']:
            key = key_name(row['key'])
            if row['path'] != key + '.zip' or row['prefix'] != 'native-bridge/' + key:
                raise ValueError('Archive mount escaped its declared condition.')
            if digest_file(store.mirror / row['path']) != row['sha256']:
                raise ValueError('Backup archive changed after publication.')
            archive = resources.enter_context(EvidenceArchive(store.local / row['path'],row['sha256']))
            mounts[row['prefix']] = archive
        view = resources.enter_context(EvidenceView(store.local / 'headers',spec['headers'],mounts))
        yield view.root


if __name__ == '__main__':
    parser = argparse.ArgumentParser(); parser.add_argument('command', choices=['initialize', 'publish-closed', 'snapshot-view'])
    parser.add_argument('package', type=Path); parser.add_argument('target', nargs='?'); args = parser.parse_args()
    if args.command == 'initialize':
        initialize(args.package, Path(args.target))
    elif args.command == 'publish-closed':
        publish_closed(args.package, args.target)
    else:
        snapshot_view(args.package)

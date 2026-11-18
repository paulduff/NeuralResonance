"""Visible-window lifecycle controls; all files belong to new synthetic fixtures."""
import argparse
import json
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import patch
from native_capture_store import CaptureStore, CONDITIONS, checked, initialize, publish_closed, write_new, snapshot_view, open_view
from native_evidence_archive import EvidenceArchive, digest_file, inventory, physical

EXPECTED = 22


def reject(action):
    try:
        action()
    except (ValueError, FileExistsError, FileNotFoundError, PermissionError):
        return
    raise AssertionError('Invalid lifecycle transition was accepted.')


def run(output):
    output = checked(output, exists=False)
    if output.exists():
        raise FileExistsError('Never overwrite earlier controls.')
    output.mkdir(); results = []

    def check(key, action):
        folder=output/key;folder.mkdir()
        try:
            action(folder);row=dict(key=key,passed=True)
        except Exception as error:
            row=dict(key=key,passed=False,error=repr(error))
        results.append(row);print(key,row['passed'],flush=True)

    def fixture(folder):
        package=folder/'run';backup=folder/'backup';package.mkdir();backup.mkdir()
        (package/'old-save.bin').write_bytes(b'pre-existing save must remain unchanged')
        owner=initialize(package,backup);raw=package/'native-bridge/quiet';(raw/'nested').mkdir(parents=True)
        (raw/'empty').mkdir()
        (raw/'alpha.bin').write_bytes(bytes(range(256)))
        (raw/'nested/records.jsonl').write_bytes(b'{"record":1}\r\n{"record":2}\n')
        (raw/'final.json').write_bytes(b'{"completed":true}\n')
        return CaptureStore(package),raw,owner

    def snapshot(raw):
        return {key:(physical(raw)/key).read_bytes() for key in inventory(raw)}

    def check_archives(store,expected):
        for root in (store.local,store.mirror):
            with EvidenceArchive(root/'quiet.zip') as archive:
                archive.verify()
                assert {key:archive.read_bytes(key) for key in archive.entries}==expected
        assert (store.package/'old-save.bin').read_bytes()==b'pre-existing save must remain unchanged'

    def happy(folder):
        store,raw,_=fixture(folder);before=snapshot(raw)
        published=store.seal('quiet',digest_file(raw/'final.json'))
        assert snapshot(raw)==before and published['rawFilesRemoved']==0
        result=store.release('quiet')
        assert result['completed'] and result['rawFilesRemoved']==3 and not raw.exists()
        assert store.package.exists() and (store.package/'native-bridge').exists()
        check_archives(store,before)
    check('verified-publication-releases-only-new-owned-files',happy)

    def existing_raw(folder):
        package=folder/'run';backup=folder/'backup';package.mkdir();backup.mkdir()
        (package/'native-bridge').mkdir();sentinel=package/'native-bridge/old.bin';sentinel.write_bytes(b'old')
        reject(lambda:initialize(package,backup));assert sentinel.read_bytes()==b'old'
    check('existing-raw-capture-cannot-be-adopted',existing_raw)

    def existing_backup(folder):
        package=folder/'run';backup=folder/'backup';package.mkdir();backup.mkdir()
        (backup/'capture-storage').mkdir();sentinel=backup/'capture-storage/prior.bin';sentinel.write_bytes(b'prior')
        reject(lambda:initialize(package,backup));assert sentinel.read_bytes()==b'prior' and not (package/'capture-storage').exists()
    check('existing-publication-copy-cannot-be-adopted',existing_backup)

    def overlap(folder):
        inner=folder/'inner';inner.mkdir()
        reject(lambda:initialize(folder,folder));reject(lambda:initialize(folder,inner));reject(lambda:initialize(inner,folder))
    check('overlapping-and-identical-roots-rejected',overlap)

    def undeclared(folder):
        store,raw,_=fixture(folder);before=snapshot(raw)
        for key in ('../old-save.bin','quiet/../strong-left','unknown','C:/outside','QUIET'):
            reject(lambda:store.seal(key,digest_file(raw/'final.json')))
        assert snapshot(raw)==before
    check('undeclared-and-escaping-condition-paths-rejected',undeclared)

    def incomplete(folder):
        store,raw,_=fixture(folder);(raw/'failure.json').write_bytes(b'{"error":"injected"}')
        before=snapshot(raw);reject(lambda:store.seal('quiet',digest_file(raw/'final.json')))
        assert snapshot(raw)==before and not (store.local/'quiet.zip').exists()
    check('failed-condition-retains-all-raw-evidence',incomplete)

    def wrong_completion(folder):
        store,raw,_=fixture(folder);before=snapshot(raw)
        reject(lambda:store.seal('quiet','0'*64));assert snapshot(raw)==before
    check('changed-completion-record-rejected',wrong_completion)

    def missing_completion(folder):
        store,raw,_=fixture(folder)
        # This fixture-only rename does not delete any evidence.
        (raw/'final.json').rename(raw/'unfinished.json')
        before=snapshot(raw);reject(lambda:store.seal('quiet','0'*64));assert snapshot(raw)==before
    check('unfinished-condition-cannot-be-published',missing_completion)

    def duplicate(folder):
        store,raw,_=fixture(folder);digest=digest_file(raw/'final.json');store.seal('quiet',digest);before=snapshot(raw)
        reject(lambda:store.seal('quiet',digest));assert snapshot(raw)==before
    check('publication-cannot-overwrite-or-restart',duplicate)

    def modified_raw(folder):
        store,raw,_=fixture(folder);store.seal('quiet',digest_file(raw/'final.json'))
        (raw/'alpha.bin').write_bytes(b'changed');before=snapshot(raw)
        reject(lambda:store.release('quiet'));assert snapshot(raw)==before
    check('changed-raw-file-stops-before-release',modified_raw)

    def extra_raw(folder):
        store,raw,_=fixture(folder);store.seal('quiet',digest_file(raw/'final.json'))
        (raw/'unexpected.bin').write_bytes(b'new evidence');before=snapshot(raw)
        reject(lambda:store.release('quiet'));assert snapshot(raw)==before
    check('unaccounted-raw-file-stops-before-release',extra_raw)

    def corrupted_backup(folder):
        store,raw,_=fixture(folder);store.seal('quiet',digest_file(raw/'final.json'));before=snapshot(raw)
        with (store.mirror/'quiet.zip').open('ab') as stream:stream.write(b'corrupted')
        reject(lambda:store.release('quiet'));assert snapshot(raw)==before
    check('changed-backup-stops-before-release',corrupted_backup)

    def changed_owner(folder):
        store,raw,_=fixture(folder);store.seal('quiet',digest_file(raw/'final.json'));before=snapshot(raw)
        with (store.local/'ownership.json').open('ab') as stream:stream.write(b' ')
        reject(lambda:store.release('quiet'));assert snapshot(raw)==before
    check('changed-ownership-stops-before-release',changed_owner)

    def copy_failure(folder):
        store,raw,_=fixture(folder);before=snapshot(raw)
        with patch('native_capture_store.shutil.copyfileobj',side_effect=PermissionError('injected backup write failure')):
            reject(lambda:store.seal('quiet',digest_file(raw/'final.json')))
        assert snapshot(raw)==before and (store.local/'quiet.failed.json').exists()
        reject(lambda:store.release('quiet'));assert snapshot(raw)==before
    check('failed-backup-publication-retains-raw-prefix',copy_failure)

    def interrupted_intent(folder):
        store,raw,_=fixture(folder);store.seal('quiet',digest_file(raw/'final.json'));before=snapshot(raw)
        write_new(store.local/'quiet.release-intent.json',dict(interrupted=True))
        reject(lambda:store.release('quiet'));assert snapshot(raw)==before
    check('interrupted-release-never-auto-resumes',interrupted_intent)

    def interrupted_removal(folder):
        store,raw,_=fixture(folder);before=snapshot(raw);store.seal('quiet',digest_file(raw/'final.json'))
        unlink=Path.unlink
        def fail_on_second_file(path,*args,**kwargs):
            if path.name=='final.json':raise PermissionError('injected raw file lock')
            return unlink(path,*args,**kwargs)
        with patch.object(Path,'unlink',fail_on_second_file):reject(lambda:store.release('quiet'))
        assert not (raw/'alpha.bin').exists() and (raw/'final.json').read_bytes()==before['final.json']
        assert (raw/'nested/records.jsonl').read_bytes()==before['nested/records.jsonl']
        failure=json.loads((store.local/'quiet.failed.json').read_text(encoding='utf-8'))
        assert failure['rawFilesRemoved']==1 and failure['completeArchivesRetained']
        check_archives(store,before);reject(lambda:store.release('quiet'))
    check('interrupted-removal-retains-archives-and-remaining-raw',interrupted_removal)

    def wrong_closed_owner(folder):
        store,raw,owner=fixture(folder);before=snapshot(raw)
        write_new(store.local/'quiet.closed.json',dict(owner='wrong-owner',key='quiet',physicsSteps=31996,
            completionSha256=digest_file(raw/'final.json'),nativeProcessExitedSuccessfully=True,recordingScopesDisposed=True))
        reject(lambda:publish_closed(store.package,'quiet'));assert snapshot(raw)==before
    check('closed-condition-receipt-must-belong-to-this-run',wrong_closed_owner)

    def open_stream_scope(folder):
        store,raw,owner=fixture(folder);before=snapshot(raw)
        write_new(store.local/'quiet.closed.json',dict(owner=owner['owner'],key='quiet',physicsSteps=31996,
            completionSha256=digest_file(raw/'final.json'),nativeProcessExitedSuccessfully=True,recordingScopesDisposed=False))
        reject(lambda:publish_closed(store.package,'quiet'));assert snapshot(raw)==before
    check('unfinished-recording-scope-cannot-be-published',open_stream_scope)

    def reparse_ancestor(folder):
        store,raw,_=fixture(folder);before=snapshot(raw);original=Path.lstat
        def marked(path,*args,**kwargs):
            result=original(path,*args,**kwargs)
            if path.name=='native-bridge':
                return SimpleNamespace(st_mode=result.st_mode,st_file_attributes=getattr(result,'st_file_attributes',0)|0x400)
            return result
        with patch.object(Path,'lstat',marked):
            reject(lambda:store.seal('quiet',digest_file(raw/'final.json')))
        assert snapshot(raw)==before and not (store.local/'quiet.zip').exists()
    check('reported-reparse-ancestor-rejected-before-publication',reparse_ancestor)

    def reparse_before_release(folder):
        store,raw,_=fixture(folder);store.seal('quiet',digest_file(raw/'final.json'));before=snapshot(raw);original=Path.lstat
        def marked(path,*args,**kwargs):
            result=original(path,*args,**kwargs)
            if path.name=='nested':
                return SimpleNamespace(st_mode=result.st_mode,st_file_attributes=getattr(result,'st_file_attributes',0)|0x400)
            return result
        with patch.object(Path,'lstat',marked):reject(lambda:store.release('quiet'))
        assert snapshot(raw)==before
    check('reported-reparse-descendant-stops-before-release',reparse_before_release)

    def complete_view(folder):
        store,raw,owner=fixture(folder);expected={}
        for key in CONDITIONS:
            case=store.package/'native-bridge'/key
            if key!='quiet':
                case.mkdir();(case/'final.json').write_bytes(b'{"synthetic":"'+key.encode()+b'"}')
            expected[key]=snapshot(case);store.seal(key,digest_file(case/'final.json'));store.release(key)
        root=store.package/'native-bridge'
        write_new(root/'protocol.json',dict(version='rubral-neuronal-native-hinge-v1',physicsResolution=3,muscleSubsteps=1,
            steps=31996,strongLeftOnly=False,conditions=[dict(key=key) for key in CONDITIONS]))
        write_new(root/'results.json',dict(completed=True,sourceBodyAccountingPassed=True,observedNeuronalMuscleMovement=True,
            actualPhysicsSteps=223972,cases=[dict(condition=key,completed=True,physicsSteps=31996) for key in CONDITIONS]))
        reference=store.package/'reference';reference.mkdir()
        for name in ('failed-request-000033.json','last-reply-000032.json'):(reference/name).write_bytes(b'synthetic wire header')
        result=snapshot_view(store.package);assert result['allSevenConditionsArchived'] and result['sourcePacketNativeAccountingPending']
        with open_view(store.package) as view:
            for key,files in expected.items():
                for name,value in files.items():assert (view/('native-bridge/'+key+'/'+name)).read_bytes()==value
            assert (view/'native-bridge/protocol.json').read_bytes()==(root/'protocol.json').read_bytes()
        return store
    check('complete-seven-condition-view-preserves-all-original-bytes',complete_view)

    def changed_view_backup(folder):
        store=complete_view(folder)
        with (store.mirror/'strong-right.zip').open('ab') as stream:stream.write(b'changed after snapshot')
        def access():
            with open_view(store.package):pass
        reject(access)
    check('released-capture-view-requires-unchanged-backup',changed_view_backup)

    assert len(results)==EXPECTED
    write_new(output/'controls.json',dict(completed=True,expected=EXPECTED,passed=sum(r['passed'] for r in results),
        cases=results,syntheticStorageFixtures=True,neuronalModelsExecuted=False,nativePhysicsExecuted=False,
        existingCaptureFilesRemoved=0,physiologyQualified=False,avatarRecoveryAssessed=False))
    if not all(row['passed'] for row in results):
        raise RuntimeError('New-capture lifecycle controls failed; do not start native capture.')


if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    parser=argparse.ArgumentParser();parser.add_argument('output',type=Path);args=parser.parse_args();run(args.output)

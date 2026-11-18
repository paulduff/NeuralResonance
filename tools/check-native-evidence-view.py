"""Visible-only archive-mount controls, with no neuronal/native execution."""
import argparse
import gzip
import hashlib
import json
from pathlib import Path
from native_evidence_archive import EvidenceArchive, pack
from native_evidence_view import EvidenceView

EXPECTED = 12


def index(folder):
    return [dict(path=p.relative_to(folder).as_posix(), bytes=p.stat().st_size,
                 sha256=hashlib.sha256(p.read_bytes()).hexdigest().upper())
            for p in sorted(folder.rglob('*')) if p.is_file()]


def equal(a, b):
    assert a == b, (a, b)


def reject(action):
    try:
        action()
    except (ValueError, FileNotFoundError, KeyError, TypeError):
        return
    raise AssertionError('Invalid evidence access was accepted.')


def run(output):
    assert not output.exists(); output.mkdir()
    headers = output / 'headers'; headers.mkdir()
    (headers / 'protocol.json').write_bytes(b'{"step":1}\r\n')
    expected = index(headers)
    raw = output / 'payload'; (raw / 'bridge').mkdir(parents=True)
    (raw / 'initial.json').write_bytes(b'{"session":"fixture"}\n')
    (raw / 'bridge/request-000001.json').write_bytes(b'{"bits":"3FB999999999999A"}')
    (raw / 'bridge/reply-000001.json').write_bytes(b'{"step":1}')
    (raw / 'frames.gz').write_bytes(gzip.compress(b'{"frame":1}\n{"frame":2}\n', mtime=0))
    archive_path = output / 'payload.zip'
    packed = pack(raw, archive_path, index(raw))
    cases = []

    def check(key, action):
        try:
            action(); row = dict(key=key, passed=True)
        except Exception as error:
            row = dict(key=key, passed=False, error=repr(error))
        cases.append(row); print(key, row['passed'], flush=True)

    with EvidenceArchive(archive_path, packed['archiveSha256']) as archive:
        mounts = {'native-bridge/strong-left': archive}
        with EvidenceView(headers, expected, mounts) as view:
            root = view.root
            check('headers-and-members-retain-original-bytes', lambda: equal(
                [(root / 'protocol.json').read_bytes(), (root / 'native-bridge/strong-left/initial.json').read_bytes()],
                [(headers / 'protocol.json').read_bytes(), (raw / 'initial.json').read_bytes()]))

            def compressed_stream():
                with (root / 'native-bridge/strong-left/frames.gz').open('rb') as stream, gzip.open(stream, 'rt', encoding='utf-8') as records:
                    equal([json.loads(line) for line in records], [dict(frame=1), dict(frame=2)])
            check('gzip-records-read-through-checked-member', compressed_stream)
            check('glob-yields-only-request-members', lambda: equal(
                [p.name for p in sorted((root / 'native-bridge/strong-left/bridge').glob('request-*.json'))], ['request-000001.json']))
            check('logical-directories-and-files-have-distinct-kinds', lambda: equal(
                [(root / 'native-bridge').is_dir(), (root / 'native-bridge').is_file(),
                 (root / 'native-bridge/strong-left/initial.json').is_file(), (root / 'absent').exists()], [True, False, True, False]))
            check('write-and-traversal-are-rejected', lambda: (
                reject(lambda: (root / 'protocol.json').open('w')), reject(lambda: root / '../outside')))

            def no_fallback():
                shadow = headers / 'native-bridge/strong-left/missing.json'; shadow.parent.mkdir(parents=True)
                shadow.write_bytes(b'filesystem fallback must never be read')
                reject(lambda: (root / 'native-bridge/strong-left/missing.json').read_bytes())
                assert shadow.read_bytes() == b'filesystem fallback must never be read'
            check('missing-mounted-member-never-falls-back-to-filesystem', no_fallback)

            def changed_header():
                (headers / 'protocol.json').write_bytes(b'{"step":2}\r\n')
                reject(lambda: (root / 'protocol.json').read_bytes())
            check('changed-header-hash-is-rejected', changed_header)
        check('closed-view-cannot-read', lambda: reject(lambda: (root / 'protocol.json').read_bytes()))
        # A separate clean header tree avoids changing any previous fixture.
        clean = output / 'clean'; clean.mkdir(); (clean / 'protocol.json').write_bytes(b'header')
        clean_index = index(clean)
        check('overlapping-mounts-are-rejected', lambda: reject(lambda: EvidenceView(clean, clean_index,
            {'native-bridge/strong-left': archive, 'native-bridge': archive})))
        check('case-ambiguous-mounts-are-rejected', lambda: reject(lambda: EvidenceView(clean, clean_index,
            {'native-bridge/strong-left': archive, 'Native-Bridge/strong-right': archive})))
        check('header-archive-namespace-collision-is-rejected', lambda: reject(lambda: EvidenceView(clean, clean_index,
            {'protocol.json/child': archive})))
        with EvidenceView(clean, clean_index, mounts) as closing:
            retained = closing.root
            archive.close()
            check('closed-external-archive-invalidates-view', lambda: reject(lambda: (retained / 'protocol.json').read_bytes()))
    assert len(cases) == EXPECTED
    result = dict(completed=True, expected=EXPECTED, passed=sum(row['passed'] for row in cases), cases=cases,
                  modelsExecuted=False, nativePhysicsExecuted=False, originalCapturesModified=False)
    (output / 'controls.json').write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    if result['passed'] != EXPECTED:
        raise RuntimeError('Archive view controls failed; retain fixtures and original recordings.')


if __name__ == '__main__':
    if not __debug__: raise RuntimeError('Assertions must remain enabled.')
    parser = argparse.ArgumentParser(); parser.add_argument('output', type=Path)
    run(parser.parse_args().output.resolve())

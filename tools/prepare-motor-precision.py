"""Package fixed-state motor-cell diagnosis from the reported, preserved failure."""
from datetime import datetime, timezone
from hashlib import sha256
import json
from pathlib import Path
import shutil
import subprocess
import uuid
from zipfile import ZipFile

ROOT = Path(__file__).resolve().parents[1]
SCENARIOS = ['quiet', 'soma-1000', 'without-fast-sodium', 'soma-6000']
read = lambda p: json.loads(p.read_text(encoding='utf-8-sig'))
digest = lambda p: sha256(p.read_bytes()).hexdigest().upper()
git = lambda *a: subprocess.check_output(['git', *a], cwd=ROOT, encoding='utf-8').strip()


def main():
    assert not git('status', '--porcelain'), 'Commit before freezing.'
    revision = git('rev-parse', 'HEAD')
    assert revision == git('rev-parse', 'origin/main'), 'Push before freezing.'
    review = ROOT / 'artifacts/compute-cell-review-20261109'
    reviewed = read(review / 'motor-review.json')
    assert reviewed['originalStatus'] == 'FAILED' and reviewed['passedScenarios'] == 8
    original = Path(reviewed['capture'])
    backup = Path(reviewed['originalBackup'])
    manifest = read(original / 'backup-manifest.json')
    assert digest(original / 'backup-manifest.json') == digest(backup / 'backup-manifest.json')
    entries = {e['Path'].replace('\\', '/'): e for e in manifest['Files']}
    def verify(name):
        expected = entries[name]['Sha256']
        for folder in (original, backup):
            assert digest(folder / name) == expected, name
        return original / name
    config = read(verify('configuration.json'))
    assert config['Revision'] == reviewed['sourceRevision'] == '22d295951135b2b04992884ddfd309607cc40f6c'
    assert read(verify('window-status.json'))['Status'] == 'FAILED'
    review_manifest = read(review / 'backup-manifest.json')
    review_entry = next(e for e in review_manifest['Files'] if e['Path'] == 'motor-review.json')
    assert digest(review / 'motor-review.json') == review_entry['Sha256'] == digest(Path(review_manifest['Destination']) / 'motor-review.json')
    selected = {}
    for name in entries:
        if name.startswith('motoneuron-input/'):
            selected[name] = name
    for name in ('motoneuron-active-input.json', 'motoneuron-active-study/protocol.json',
                 'motoneuron-active-study/results.json', 'motoneuron-active-reference/comparison.json'):
        selected[name] = 'retained/' + name
    comparison = read(verify('motoneuron-active-reference/comparison.json'))
    for row in comparison['cases']:
        if row['scenario'] not in SCENARIOS:
            continue
        assert len(row['references']) == len(row['candidates']) == 3
        for candidate in row['candidates']:
            name = 'motoneuron-active-study/' + candidate['key'] + '.csv'
            selected[name] = 'retained/' + name
        for method in row['references']:
            name = 'motoneuron-active-reference/' + row['scenario'] + '-' + method['method'] + '.npz'
            selected[name] = 'retained/' + name
    for name in ('motoneuron-active-reference.py', 'motoneuron-reference-check.py',
                 'motoneuron_active_protocol.py', 'motoneuron_bounded_jacobian.py'):
        selected['source/tools/' + name] = 'reference-source/' + name
    for name in selected:
        verify(name)
    assert sum(n.endswith('.csv') for n in selected) == 12 and sum(n.endswith('.npz') for n in selected) == 12

    stamp = datetime.now(timezone.utc).strftime('%Y%m%d')
    package = ROOT / 'artifacts' / ('motor-precision-' + stamp + '-' + uuid.uuid4().hex[:6])
    destination = Path('D:/DNNE-desktop-runs/UnityWorld') / package.name
    assert not package.exists() and not destination.exists()
    package.mkdir()
    subprocess.run(['git', 'archive', '--format=zip', '--output=' + str(package / 'source.zip'), revision], cwd=ROOT, check=True)
    with ZipFile(package / 'source.zip') as archive:
        assert all(not Path(n).is_absolute() and '..' not in Path(n).parts for n in archive.namelist())
        archive.extractall(package / 'source')
    # Only the new read-only diagnostic entry point may differ from the failed
    # candidate's cell source. Newline decoding does not modify either capture.
    name = 'source/Structures/_SharedRuntime/MotoneuronReferenceCell.cs'
    previous = verify(name).read_text(encoding='utf-8-sig')
    current = (package / name).read_text(encoding='utf-8-sig')
    begin = current.index('    // Read-only diagnostic at a supplied physical state.')
    end = current.index('    public MotoneuronReferenceCell(', begin)
    assert current[:begin] + current[end:] == previous, 'Candidate equations changed.'
    name = 'source/Structures/_SharedRuntime/MotoneuronChannelKinetics.cs'
    assert digest(package / name) == digest(verify(name)), 'Channel source changed.'
    provenance = []
    for name, target_name in selected.items():
        target = package / target_name
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(original / name, target)
        assert digest(target) == entries[name]['Sha256']
        provenance.append(dict(OriginalPath=name, Path=target_name, Sha256=digest(target)))
    (package / 'provenance.json').write_text(json.dumps(dict(originalCapture=str(original), originalBackup=str(backup),
        originalManifestSha256=digest(original / 'backup-manifest.json'), review=reviewed,
        candidateEquationsUnchanged=True, files=provenance), indent=2) + '\n', encoding='utf-8')
    shutil.copy2(package / 'source/tools/backup-unity-run.ps1', package / 'backup-run.ps1')
    python = ROOT / 'artifacts/numerical-reference-env-20261102/Scripts/python.exe'
    dotnet = shutil.which('dotnet')
    assert python.is_file() and dotnet
    files = [dict(Path=p.relative_to(package).as_posix(), Sha256=digest(p)) for p in sorted(package.rglob('*')) if p.is_file()]
    configuration = dict(Revision=revision, OriginalRevision=config['Revision'], Scenarios=SCENARIOS,
        PreparedUtc=datetime.now(timezone.utc).isoformat(), PythonExecutable=str(python), DotnetExecutable=dotnet,
        BackupDestination=str(destination), PackageFiles=files, NoLiveMonitoring=True, LiveBrainAccessed=False,
        OldSavesLoaded=False, OriginalNumericalStatus='FAILED')
    (package / 'configuration.json').write_text(json.dumps(configuration, indent=2) + '\n', encoding='utf-8')
    pointer = dict(Package=str(package), Backup=str(destination), Revision=revision, NoLiveMonitoring=True, PreparedOnly=True,
        Launcher=str(package / 'source/tools/start-motor-precision-window.ps1'))
    (ROOT / 'artifacts' / ('motor-precision-prepared-' + stamp + '.json')).write_text(json.dumps(pointer, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(pointer, indent=2))


if __name__ == '__main__':
    main()

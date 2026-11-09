"""Freeze reviewed Golgi inputs and eleven completed solves without executing them."""
from datetime import datetime, timezone
from hashlib import sha256
import json
from pathlib import Path
import shutil
import subprocess
import uuid
from zipfile import ZipFile

ROOT = Path(__file__).resolve().parents[1]
read = lambda p: json.loads(p.read_text(encoding='utf-8-sig'))
digest = lambda p: sha256(p.read_bytes()).hexdigest().upper()
git = lambda *a: subprocess.check_output(['git', *a], cwd=ROOT, encoding='utf-8').strip()


def main():
    assert not git('status', '--porcelain'), 'Commit before freezing.'
    revision = git('rev-parse', 'HEAD')
    assert revision == git('rev-parse', 'origin/main'), 'Push before freezing.'
    review = ROOT / 'artifacts/compute-cell-review-20261109'
    manifest = read(review / 'backup-manifest.json')
    backup = Path(manifest['Destination'])
    assert digest(review / 'backup-manifest.json') == digest(backup / 'backup-manifest.json')
    evidence = {e['Path'].replace('\\', '/'): e for e in manifest['Files']}

    def verify_review(name):
        entry = evidence[name]
        for folder in (review, backup):
            assert digest(folder / name) == entry['Sha256'], name
        return review / name

    report = read(verify_review('golgi-review.json'))
    sealed_index = read(verify_review('golgi-sealed-files.json'))
    sealed = review / 'golgi-stopped-capture'
    assert report['reviewedOutcome'] == 'INTERRUPTED_INCOMPLETE'
    assert report['completedReferenceSolves'] == 11 and report['recomputedReferenceContrasts'] == 10
    assert report['sourceRevision'] == '73db8166d81e0f88c46e6368aee196bf5b647070'
    original = read(verify_review('golgi-stopped-capture/configuration.json'))
    assert original['Revision'] == report['sourceRevision']
    original_files = {e['Path']: e['Sha256'] for e in original['PackageFiles']}
    solver_files = ['golgi-active-reference.py', 'analyse-golgi-cell.py', 'golgi-reference-refinement.py']
    selected = []
    for entry in sealed_index:
        name = entry['path']
        if name.startswith('golgi-input/'):
            destination = name
        elif name.startswith('reference-refinement/'):
            destination = 'retained-reference/' + name.split('/', 1)[1]
        elif name in ['source/tools/' + f for f in solver_files]:
            destination = 'reference-source/' + Path(name).name
        else:
            continue
        assert '..' not in Path(name).parts and not Path(name).is_absolute()
        assert verify_review('golgi-stopped-capture/' + name) == sealed / name
        assert digest(sealed / name) == entry['sha256']
        if name.startswith(('source/', 'golgi-input/')):
            assert original_files[name] == entry['sha256'], name
        selected.append(dict(OriginalPath=name, Path=destination, Sha256=entry['sha256'], Bytes=entry['bytes']))
    assert len([r for r in selected if r['Path'].startswith('golgi-input/')]) == 78
    assert len([r for r in selected if r['Path'].startswith('reference-source/')]) == 3
    assert len([r for r in selected if r['Path'].startswith('retained-reference/')]) == 14
    assert len([r for r in selected if r['Path'].startswith('retained-reference/') and r['Path'].endswith('.npz')]) == 11
    assert not (sealed / 'reference-refinement/no-sk-radau-strict.npz').exists()

    stamp = datetime.now(timezone.utc).strftime('%Y%m%d')
    package = ROOT / 'artifacts' / ('golgi-resume-' + stamp + '-' + uuid.uuid4().hex[:6])
    destination = Path('D:/DNNE-desktop-runs/UnityWorld') / package.name
    assert not package.exists() and not destination.exists()
    package.mkdir()
    subprocess.run(['git', 'archive', '--format=zip', '--output=' + str(package / 'source.zip'), revision], cwd=ROOT, check=True)
    with ZipFile(package / 'source.zip') as archive:
        assert all(not Path(n).is_absolute() and '..' not in Path(n).parts for n in archive.namelist())
        archive.extractall(package / 'source')
    for entry in selected:
        target = package / entry['Path']
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(sealed / entry['OriginalPath'], target)
        assert digest(target) == entry['Sha256']
    provenance = package / 'provenance'
    provenance.mkdir()
    for name in ('golgi-review.json', 'golgi-sealed-files.json', 'golgi-terminal-observation.json'):
        shutil.copy2(verify_review(name), provenance / name)
    for name in ('configuration.json', 'window-status.json'):
        shutil.copy2(verify_review('golgi-stopped-capture/' + name), provenance / ('original-' + name))
    (provenance / 'selected-files.json').write_text(json.dumps(dict(reviewManifestSha256=digest(review / 'backup-manifest.json'),
        review=str(review), verifiedBackup=str(backup), files=selected), indent=2) + '\n', encoding='utf-8')
    shutil.copy2(package / 'source/tools/backup-unity-run.ps1', package / 'backup-run.ps1')
    python = ROOT / 'artifacts/numerical-reference-env-20261102/Scripts/python.exe'
    assert python.is_file()
    files = [dict(Path=p.relative_to(package).as_posix(), Sha256=digest(p)) for p in sorted(package.rglob('*')) if p.is_file()]
    config = dict(Revision=revision, OriginalRevision=report['sourceRevision'], PreparedUtc=datetime.now(timezone.utc).isoformat(),
        PythonExecutable=str(python), BackupDestination=str(destination), PackageFiles=files,
        ExpectedReusedSolves=11, ExpectedNewSolves=1, ExpectedReferenceComparisons=12, ExpectedCandidateComparisons=36,
        NoLiveMonitoring=True, OldSavesLoaded=False, LiveBrainAccessed=False, PhysiologyQualified=False, AvatarRecoveryAssessed=False)
    (package / 'configuration.json').write_text(json.dumps(config, indent=2) + '\n', encoding='utf-8')
    pointer = dict(Package=str(package), Backup=str(destination), Revision=revision, NoLiveMonitoring=True,
        Launcher=str(package / 'source/tools/start-golgi-resume-window.ps1'), PreparedOnly=True)
    (ROOT / 'artifacts' / ('golgi-resume-prepared-' + stamp + '.json')).write_text(json.dumps(pointer, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(pointer, indent=2))


if __name__ == '__main__':
    main()

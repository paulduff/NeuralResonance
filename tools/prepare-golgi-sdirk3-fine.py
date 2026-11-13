"""Freeze the finer comparison after timing finishes; this performs no model run."""
import argparse
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path
import shutil
import subprocess
import uuid
from zipfile import ZipFile
from golgi_sdirk3_fine_protocol import protocol
from golgi_environment_transition import validate_analyzer_change, validate_transition

ROOT = Path(__file__).resolve().parents[1]
git = lambda *a: subprocess.check_output(['git', *a], cwd=ROOT, encoding='utf-8').strip()


def main(review_folder, environment_review_folder):
    assert not git('status', '--porcelain'), 'Commit before freezing.'
    revision = git('rev-parse', 'HEAD'); assert revision == git('rev-parse', 'origin/main'), 'Push before freezing.'
    spec = importlib.util.spec_from_file_location('capture_io', ROOT/'tools/review-olive-and-motor-20261109.py')
    common = importlib.util.module_from_spec(spec); spec.loader.exec_module(common)
    read, digest, io = common.read, common.digest, common.io
    review_manifest = read(review_folder/'backup-manifest.json')
    review_backup = Path(review_manifest['Destination'])
    assert digest(review_folder/'backup-manifest.json') == digest(review_backup/'backup-manifest.json')
    assert len(review_manifest['Files']) == 1 and review_manifest['Files'][0]['Path'] == 'review.json'
    for folder in (review_folder, review_backup):
        assert digest(folder/'review.json') == review_manifest['Files'][0]['Sha256']
    review = read(review_folder/'review.json')
    assert review['operatorReportedCompletion'] and review['originalAnalysisReproduced'] and review['completedConditions'] == 38
    assert review['passedFocusedTests'] == 43 and review['allPhysicalAccountingPassed'] and not review['originalBoundsChanged']
    assert review['originalStatus'] == 'FAILED' and not review['numericalCriteriaPassed']
    baseline = Path(review['capture'])
    verified = common.verify(baseline, ['test-binaries.json', 'probe-binaries.json'],
                            'faac10d04c87d48155e33946edf6a6cdb1ded763', expected_status='FAILED', expected_exit=1)
    assert all(review[k] == v for k,v in verified.items())
    transition_manifest = read(environment_review_folder/'backup-manifest.json')
    transition_backup = Path(transition_manifest['Destination'])
    assert digest(environment_review_folder/'backup-manifest.json') == digest(transition_backup/'backup-manifest.json')
    assert {e['Path'] for e in transition_manifest['Files']} == {'review.json', 'current-environment.json'}
    for entry in transition_manifest['Files']:
        for folder in (environment_review_folder, transition_backup):
            assert digest(folder/entry['Path']) == entry['Sha256']
    transition_review = read(environment_review_folder/'review.json')
    failed = Path(transition_review['capture'])
    failed_verification = common.verify(failed, ['test-binaries.json'], '69bc40f1593ec5cab71883be705cdbe2a8cfbec4',
                                        expected_status='FAILED', expected_exit=1)
    assert all(transition_review[k] == v for k,v in failed_verification.items())
    declared_environment = read(environment_review_folder/'current-environment.json')
    validate_transition(read(baseline/'reference-preflight.json')['environment'], declared_environment, transition_review)
    unchanged = ['Structures/_SharedRuntime/'+name for name in
                 ('GolgiSdirkCell.cs', 'GolgiReferenceCell.cs', 'GolgiChannelKinetics.cs', 'GolgiCable.cs')]
    unchanged += ['tools/golgi_sdirk_protocol.py', 'tools/golgi-reference-resume.py']
    for path in unchanged:
        assert (ROOT/path).read_text(encoding='utf-8-sig') == (baseline/'source'/path).read_text(encoding='utf-8-sig'), path
    validate_analyzer_change((baseline/'source/tools/analyse-golgi-sdirk.py').read_text(encoding='utf-8-sig'),
                             (ROOT/'tools/analyse-golgi-sdirk.py').read_text(encoding='utf-8-sig'))
    marker = '        Directory.CreateDirectory(output);'
    old = (baseline/'source/tools/GolgiCellProbe/SdirkStudy.cs').read_text(encoding='utf-8-sig')
    new = (ROOT/'tools/GolgiCellProbe/SdirkStudy.cs').read_text(encoding='utf-8-sig')
    assert old.count(marker) == new.count(marker) == 1 and old.split(marker)[1] == new.split(marker)[1]

    original_manifest = read(baseline/'backup-manifest.json')
    entries = {e['Path'].replace('\\', '/'): e for e in original_manifest['Files']}
    selected = {e['Path'].replace('\\', '/') for e in read(baseline/'configuration.json')['PackageFiles']}
    selected.update(name for name in entries if name.startswith('study/'))
    selected.update(['configuration.json', 'sdirk-analysis.json', 'reference-preflight.json', 'window-status.json',
                     'test-results/focused.trx', 'test-binaries.json', 'probe-binaries.json', 'analysis.log', 'candidate.log'])
    assert selected <= entries.keys()
    stamp = datetime.now(timezone.utc).strftime('%Y%m%d')
    package = ROOT/'artifacts'/('golgi-sdirk3-fine-'+stamp+'-'+uuid.uuid4().hex[:6])
    backup = Path('D:/DNNE-desktop-runs/UnityWorld')/package.name
    assert not package.exists() and not backup.exists(); package.mkdir()
    subprocess.run(['git', 'archive', '--format=zip', '--output='+str(package/'source.zip'), revision], cwd=ROOT, check=True)
    with ZipFile(package/'source.zip') as archive:
        assert all(not Path(n).is_absolute() and '..' not in Path(n).parts for n in archive.namelist())
        archive.extractall(package/'source')
    copied = []
    for name in sorted(selected | {'backup-manifest.json'}):
        target = package/'baseline'/name; io(target.parent).mkdir(parents=True, exist_ok=True)
        shutil.copy2(io(baseline/name), io(target))
        expected = review['manifestSha256'] if name == 'backup-manifest.json' else entries[name]['Sha256']
        assert digest(target) == expected
        copied.append(dict(path='baseline/'+name, sha256=expected))
    request = protocol(); assert request['criteria'] == review['criteria']
    (package/'sdirk-input.json').write_text(json.dumps(request, indent=2)+'\n', encoding='utf-8')
    (package/'provenance.json').write_text(json.dumps(dict(review=review, unchangedSources=unchanged,
        reviewManifestSha256=digest(review_folder/'backup-manifest.json'), copiedFiles=copied,
        retainedAnalyzerChange='explicit-replay-environment-only',
        environmentReviewManifestSha256=digest(environment_review_folder/'backup-manifest.json'),
        baselineIsVerifiedInputSubset=True, originalCapturesUnchanged=True), indent=2)+'\n', encoding='utf-8')
    shutil.copy2(environment_review_folder/'review.json', package/'environment-review.json')
    shutil.copy2(environment_review_folder/'current-environment.json', package/'declared-environment.json')
    shutil.copy2(package/'source/tools/backup-unity-run.ps1', package/'backup-run.ps1')
    files = [dict(Path=p.relative_to(io(package)).as_posix(), Sha256=digest(p))
             for p in sorted(io(package).rglob('*')) if p.is_file()]
    classes = dict(GolgiSdirk3Tests=11, GolgiSdirkTests=7, GolgiReferenceCellTests=8,
                   GolgiChannelKineticsTests=16, SolutionManifestTests=1)
    python = ROOT/'artifacts/numerical-reference-env-20261102/Scripts/python.exe'
    dotnet = shutil.which('dotnet'); assert python.is_file() and dotnet
    config = dict(Revision=revision, PreparedUtc=datetime.now(timezone.utc).isoformat(), PackageFiles=files,
                  DotnetExecutable=dotnet, PythonExecutable=str(python), BackupDestination=str(backup), NoLiveMonitoring=True,
                  SolverOrder=3, BaselineReviewed=True, ReusePassiveReferences=True, EnvironmentTransitionReviewed=True, ExpectedTestCases=43,
                  TestClasses=list(classes), TestClassCounts=classes, TestFilter='|'.join('FullyQualifiedName~'+n for n in classes),
                  ExpectedConditions=14, ExpectedBaselineConditions=38, LiveBrainAccessed=False, OldSavesLoaded=False)
    (package/'configuration.json').write_text(json.dumps(config, indent=2)+'\n', encoding='utf-8')
    pointer = dict(Package=str(package), Backup=str(backup), Revision=revision, PreparedOnly=True, NoLiveMonitoring=True,
                   Launcher=str(package/'source/tools/start-golgi-sdirk3-fine-window.ps1'))
    (ROOT/'artifacts'/('golgi-sdirk3-fine-prepared-'+stamp+'.json')).write_text(json.dumps(pointer, indent=2)+'\n', encoding='utf-8')
    print(json.dumps(pointer, indent=2))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--review', type=Path, required=True)
    parser.add_argument('--environment-review', type=Path, required=True)
    parser.add_argument('--cost-study-finished', action='store_true', required=True,
                        help='Operator completion report required; never infer it by inspecting the live benchmark.')
    args = parser.parse_args()
    main(args.review.resolve(), args.environment_review.resolve())

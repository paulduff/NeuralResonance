"""Freeze an active spindle force diagnostic without running queries or kinetics."""
from datetime import datetime, timezone
import importlib.util
import json
import os
from pathlib import Path, PurePosixPath
import shutil
import subprocess
import sys
import uuid
from zipfile import ZipFile

for name in ('OMP_NUM_THREADS', 'OPENBLAS_NUM_THREADS', 'MKL_NUM_THREADS', 'NUMEXPR_NUM_THREADS'):
    os.environ[name] = '1'
ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'tools'))
from spindle_slack_roots import FIBERS, PROTOCOLS, STEPS_US, protocol


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    value = importlib.util.module_from_spec(spec); spec.loader.exec_module(value)
    return value


def main():
    helper = module('packaging', ROOT / 'tools/prepare-causal-motor.py')
    io, read, digest, verify, git = helper.io, helper.read, helper.digest, helper.verify, helper.git
    assert not git('status', '--porcelain'), 'Commit clean source first.'
    revision = git('rev-parse', 'HEAD'); assert revision == git('rev-parse', 'origin/main'), 'Push first.'
    reviewed = ROOT / 'artifacts/spindle-state-review-20261113'
    verify(reviewed, ['review.json']); review = read(reviewed / 'review.json')
    assert review['operatorReportedCompletion'] and review['canonicalStateChecksPassed']
    assert review['currentAnalysisExactlyMatchesRepairedFrozenAnalysis']
    assert review['runtimePreparationBeforeAfterMatch'] and review['completedTrajectories'] == 30
    capture = Path(review['capture'])
    assert digest(capture / 'backup-manifest.json') == review['manifestSha256']
    labels = [f'{f}-{p}-{s}us' for f in FIBERS for p in PROTOCOLS for s in STEPS_US]
    captures = [f'mechanics/{label}.{extension}' for label in labels for extension in ('mat', 'json')]
    # Source and licenses remain alongside the independent implementation.
    frozen = ['canonical-state.json', 'octave-compatibility.json', 'runtime-before.json', 'runtime-after.json',
              'canonical-state/LICENSE', 'upstream/LICENSE', 'source/tools/run_spindle_mechanics_reference.m']
    for tree in ('upstream', 'canonical-state'):
        folder = tree + '/vendor/matlabMuscleSpindleModellingTools/'
        frozen += [folder + f'spindle_model/@halfSarcWithCoop{f}/halfSarcWithCoop{f}.m' for f in ('Bag', 'Chain')]
        frozen += [folder + f'spindle_model/getDefaultSarc{f}.m' for f in ('B', 'C')]
        frozen += [folder + 'extrafusal_model/getDefaultSarcE.m']
    frozen += ['upstream/vendor/matlabMuscleSpindleModellingTools/spindle_model/' + name for name in
        ('sarcSimDriverIntrafusal20250627.m', 'find_hsl_from_force_spindle20250627.m')]
    frozen += ['source/tools/spindle_trial_stress.m']
    verify(capture, captures + frozen)
    assert digest(ROOT / 'tools/spindle-reference-LICENSE.txt') == digest(capture / 'upstream/LICENSE')
    fixed_review = ROOT / 'artifacts/spindle-fixed-review-20261116'
    verify(fixed_review, ['review.json']); fixed = read(fixed_review / 'review.json')
    assert fixed['operatorReportedCompletion'] and fixed['savedAnalysisExactlyReproduced']
    assert fixed['capturedFixedLengthAccuracyPassed'] and fixed['candidateComparisons'] == 54
    fixed_capture = Path(fixed['capture'])
    assert digest(fixed_capture / 'backup-manifest.json') == fixed['manifestSha256']
    verify(fixed_capture, ['spindle-fixed-analysis.json', 'source/tools/spindle_fixed_reference.py'])
    assert read(fixed_capture / 'spindle-fixed-analysis.json') == fixed['analysis']
    assert (ROOT / 'tools/spindle_fixed_reference.py').read_text(encoding='utf-8-sig') == io(fixed_capture / 'source/tools/spindle_fixed_reference.py').read_text(encoding='utf-8-sig')
    qualified_review = ROOT / 'artifacts/causal-motor-comparison-review-20261113'
    verify(qualified_review, ['review.json']); qualified = read(qualified_review / 'review.json')
    assert qualified['operatorReportedCompletion'] and qualified['savedAnalysisExactlyReproduced']
    assert qualified['analysis']['numericalCriteriaPassed']
    qualified_capture = Path(qualified['capture'])
    assert digest(qualified_capture / 'backup-manifest.json') == qualified['manifestSha256']
    verify(qualified_capture, ['reference-preflight.json', 'source/tools/golgi-reference-resume.py'])
    assert (ROOT / 'tools/golgi-reference-resume.py').read_text(encoding='utf-8-sig') == io(qualified_capture / 'source/tools/golgi-reference-resume.py').read_text(encoding='utf-8-sig')
    environment = module('dependencies', ROOT / 'tools/golgi-reference-resume.py').environment()
    assert environment == read(qualified_capture / 'reference-preflight.json')['environment']
    package = ROOT / 'artifacts' / ('spindle-slack-roots-' + datetime.now(timezone.utc).strftime('%Y%m%d') + '-' + uuid.uuid4().hex[:6])
    backup = Path('D:/DNNE-desktop-runs/UnityWorld') / package.name
    assert not package.exists() and not backup.exists(); package.mkdir()
    subprocess.run(['git', 'archive', '--format=zip', '--output=' + str(package / 'source.zip'), revision], cwd=ROOT, check=True)
    with ZipFile(package / 'source.zip') as archive:
        for entry in archive.infolist():
            path = PurePosixPath(entry.filename)
            assert not path.is_absolute() and '..' not in path.parts and ':' not in entry.filename and '\\' not in entry.filename
            assert ((entry.external_attr >> 16) & 0o170000) != 0o120000
            target = io(package / 'source' / path)
            if entry.is_dir(): target.mkdir(parents=True, exist_ok=True)
            else:
                target.parent.mkdir(parents=True, exist_ok=True)
                with target.open('xb') as stream: stream.write(archive.read(entry))
    copied = []
    def copy(original, relative):
        target = io(package / relative); target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(io(original), target); assert digest(original) == digest(target)
        copied.append(dict(original=str(original), path=relative, sha256=digest(target)))
    for name in captures: copy(capture / name, 'native/' + Path(name).name)
    for name in frozen: copy(capture / name, 'reference-source/' + name)
    copy(reviewed / 'review.json', 'reference/state-review.json')
    copy(fixed_review / 'review.json', 'reference/fixed-review.json')
    copy(fixed_capture / 'backup-manifest.json', 'reference/fixed-manifest.json')
    copy(capture / 'backup-manifest.json', 'reference/state-manifest.json')
    copy(qualified_review / 'review.json', 'reference/qualified-numerical-review.json')
    copy(qualified_capture / 'reference-preflight.json', 'reference/qualified-numerical-preflight.json')
    for name, value in (('spindle-slack-input.json', protocol()), ('preparation-environment.json', environment),
                       ('provenance.json', dict(copied=copied, completedSourceCapture=str(capture),
                        sourceLicense='AGPL-3.0-only', nativeRerun=False, newModelExecutedDuringPreparation=False, rootSearchExecutedDuringPreparation=False))):
        with (package / name).open('x', encoding='utf-8') as stream: json.dump(value, stream, indent=2, allow_nan=False)
    # Parse native MAT captures and verify their constants, clocks and recorded
    # forces. This reads completed observations; no solver/control is invoked.
    audit = module('candidate_parser', ROOT / 'tools/analyse-spindle-slack-roots.py')
    parsed = audit.validate(package)
    with (package / 'preparation-capture-check.json').open('x', encoding='utf-8') as stream:
        json.dump(dict(modelExecuted=False, captures=[dict(label=c['label'], samples=len(c['states']),
            forceReconstructionErrorPa=c['reconstructionErrorPa']) for c in parsed]), stream, indent=2)
    shutil.copyfile(package / 'source/tools/backup-unity-run.ps1', package / 'backup-run.ps1')
    files = [dict(Path=p.relative_to(io(package)).as_posix(), Sha256=digest(p)) for p in sorted(io(package).rglob('*')) if p.is_file()]
    configuration = dict(Revision=revision, PreparedUtc=datetime.now(timezone.utc).isoformat(), PackageFiles=files,
        PythonExecutable=sys.executable, BackupDestination=str(backup), NoLiveMonitoring=True,
        IsolatedSpindleSlackRoots=True, ExpectedNativeTrajectories=12, ExpectedStateQueries=2812,
        ExpectedAnalyticControls=6, KineticsExecuted=False,
        NativeRerun=False, LiveBodySelected=False, MovingTrajectoryAccuracyAssessed=False, SlackTrajectoryAssessed=False,
        AfferentFiringAssessed=False, PhysiologyQualified=False, AvatarRecoveryAssessed=False)
    (package / 'configuration.json').write_text(json.dumps(configuration, indent=2) + '\n', encoding='utf-8')
    pointer = dict(Package=str(package), Backup=str(backup), Revision=revision, PreparedOnly=True, NoLiveMonitoring=True,
        Launcher=str(package / 'source/tools/start-spindle-slack-roots-window.ps1'))
    (package.parent / (package.name + '-prepared.json')).write_text(json.dumps(pointer, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(pointer, indent=2), flush=True)


if __name__ == '__main__': main()

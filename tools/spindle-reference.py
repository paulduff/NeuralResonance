"""Freeze/verify the isolated native spindle reference, or audit its saved data.

Preparation and audit never execute Octave or solve an ODE. Execution belongs to
the separate visible PowerShell launcher. No physiological acceptance bounds are
inferred from finite traces or differences between timesteps.
"""
import argparse
from datetime import datetime, timezone
from hashlib import sha256
import importlib.util
import json
import math
import os
from pathlib import Path, PurePosixPath
import shutil
import subprocess
import sys
import uuid
from zipfile import ZipFile
from types import SimpleNamespace

ROOT = Path(__file__).resolve().parents[1]
UPSTREAM = '721ba03cb775261548766ed1349c4bf2e0e35385'
UPSTREAM_HASH = '050F71D846360410E4E917B74FB7A2F3E497C16E2107F0F53552C069D8869444'
RUNTIME_HASH = 'B0E1BC72A87ABE4E2A0CF7E76B7727D8FDE5C99EC049C1576F095280C3E48E5A'
FIBERS = ('bag', 'chain')
PROTOCOLS = ('quiet-hold', 'active-hold', 'drive-on-off', 'stretch-shorten', 'imposed-below-slack')
STEPS_US = (1000, 500, 250)
COMPATIBILITY = 'octave-ode23-empty-options-struct-v1'


def state_candidate():
    spec = importlib.util.spec_from_file_location('spindle_state_candidate', Path(__file__).with_name('spindle-state-candidate.py'))
    candidate = importlib.util.module_from_spec(spec); spec.loader.exec_module(candidate)
    api = SimpleNamespace(io=io, read=read, write=write, digest=digest, analyse=analyse,
        vector=vector, one=one, FIBERS=FIBERS, PROTOCOLS=PROTOCOLS, STEPS_US=STEPS_US)
    return candidate, api


def io(path):
    value = str(Path(path).absolute())
    return Path('\\\\?\\' + value) if os.name == 'nt' and not value.startswith('\\\\?\\') else Path(value)


def read(path):
    return json.loads(io(path).read_text(encoding='utf-8-sig'))


def write(path, value):
    with io(path).open('x', encoding='utf-8') as stream:
        stream.write(json.dumps(value, indent=2, allow_nan=False) + '\n')


def digest(path):
    h = sha256()
    with io(path).open('rb') as stream:
        for block in iter(lambda: stream.read(4 * 1024 * 1024), b''):
            h.update(block)
    return h.hexdigest().upper()


def extract(archive_path, destination, strip_root=False):
    names = set()
    with ZipFile(archive_path) as archive:
        for entry in archive.infolist():
            p = PurePosixPath(entry.filename)
            assert not p.is_absolute() and '..' not in p.parts and ':' not in entry.filename and '\\' not in entry.filename
            assert ((entry.external_attr >> 16) & 0o170000) != 0o120000, 'Archive link'
            if strip_root:
                assert p.parts[0] == 'spindleModelTutorial-' + UPSTREAM
                p = PurePosixPath(*p.parts[1:])
            if entry.is_dir():
                continue
            assert str(p).casefold() not in names
            names.add(str(p).casefold())
            target = io(destination / p)
            target.parent.mkdir(parents=True, exist_ok=True)
            with target.open('xb') as stream:
                stream.write(archive.read(entry))


def verify_runtime(package):
    config = read(package / 'configuration.json')
    assert config['UpstreamRevision'] == UPSTREAM and config['ExpectedTrajectories'] == 30
    assert config['ExpectedBoundaryRecords'] == 2 and config['NoLiveMonitoring'] and config['NoTimingMeasured']
    for e in config['PackageFiles']:
        assert digest(package / e['Path']) == e['Sha256'], e['Path']
    for e in config['ExternalFiles']:
        assert digest(e['Path']) == e['Sha256'], e['Path']
    manifest = read(package / 'runtime-files.json')
    assert manifest['sourceSha256'].upper() == RUNTIME_HASH
    runtime = io(config['RuntimeDirectory'])
    expected = {e['path']: e for e in manifest['files']}
    assert len(expected) == len(manifest['files']) == 59532
    paths = {p.relative_to(runtime).as_posix(): p for p in runtime.rglob('*') if p.is_file()}
    assert paths.keys() == expected.keys(), 'Runtime file inventory changed'
    for name, path in paths.items():
        e = expected[name]
        assert not path.is_symlink() and path.stat().st_size == e['bytes'] and digest(path) == e['sha256'].upper(), name
    adapter_files = verify_compatibility(package)
    result = dict(verifiedRuntimeFiles=len(paths), runtimeManifestSha256=digest(package / 'runtime-files.json'),
        verifiedPackageFiles=len(config['PackageFiles']), verifiedExternalFiles=len(config['ExternalFiles']),
        verifiedCompatibilityFiles=adapter_files,
        octaveExecutableSha256=digest(config['OctaveExecutable']), pythonVersion=sys.version,
        modelExecutedDuringVerification=False)
    if config.get('CanonicalStateCandidate', False):
        candidate, api = state_candidate()
        result['verifiedCanonicalStateFiles'] = candidate.verify(package, api)
    return result


def verify_compatibility(package):
    declaration = read(package / 'octave-compatibility.json')
    assert declaration['Compatibility'] == COMPATIBILITY and declaration['OriginalSourcePreserved']
    assert not declaration['EquationsChanged'] and not declaration['SolverDefaultsChanged']
    originals = io(package / 'upstream/vendor')
    adapter = io(package / 'octave-compatible/vendor')
    original_paths = {p.relative_to(originals).as_posix(): p for p in originals.rglob('*') if p.is_file()}
    adapted_paths = {p.relative_to(adapter).as_posix(): p for p in adapter.rglob('*') if p.is_file()}
    assert original_paths.keys() == adapted_paths.keys()
    expected_edits = {'vendor/matlabMuscleSpindleModellingTools/spindle_model/@halfSarcWithCoop' + f + '/halfSarcWithCoop' + f + '.m'
                      for f in ('Bag', 'Chain')}
    edits = {e['Path']: e for e in declaration['Edits']}
    assert len(declaration['Edits']) == 2 and edits.keys() == expected_edits
    for name, path in original_paths.items():
        original = path.read_bytes()
        changed = adapted_paths[name].read_bytes()
        if 'vendor/' + name in edits:
            e = edits['vendor/' + name]
            assert e['Before'] == 'ode23(@derivs,time_step*[0 1],y,[])'
            assert e['After'] == 'ode23(@derivs,time_step*[0 1],y,odeset())'
            assert original.count(e['Before'].encode()) == 1
            assert original.replace(e['Before'].encode(), e['After'].encode()) == changed
            assert sha256(original).hexdigest().upper() == e['OriginalSha256']
            assert sha256(changed).hexdigest().upper() == e['ExecutionSha256']
        else:
            assert changed == original, name
    return len(original_paths)


def prepare(canonical=False):
    def git(*args):
        return subprocess.check_output(['git', *args], cwd=ROOT, encoding='utf-8').strip()
    assert not git('status', '--porcelain'), 'Commit clean source before freezing'
    revision = git('rev-parse', 'HEAD')
    assert revision == git('rev-parse', 'origin/main'), 'Push before freezing'
    upstream_capture = ROOT / 'artifacts/spindle-2026-source-20261113'
    runtime_capture = ROOT / 'artifacts/octave-runtime-11.3.0-20261113'
    runtime = ROOT / 'artifacts/portable-octave-11.3.0'
    dependencies = []
    for capture, archive, expected_hash in ((upstream_capture, 'spindleModelTutorial.zip', UPSTREAM_HASH),
            (runtime_capture, 'octave-11.3.0-w64.zip', RUNTIME_HASH)):
        for folder in (capture, Path('D:/DNNE-desktop-runs/UnityWorld') / capture.name):
            path = folder / archive
            assert digest(path) == expected_hash, path
            dependencies.append(dict(Path=str(path), Sha256=expected_hash))
    executable = runtime / 'mingw64/bin/octave-cli.exe'
    assert executable.is_file()
    dependencies.append(dict(Path=sys.executable, Sha256=digest(sys.executable)))
    for dll in sorted(Path(sys.executable).parent.glob('python*.dll')):
        dependencies.append(dict(Path=str(dll), Sha256=digest(dll)))
    name = ('spindle-state-' if canonical else 'spindle-reference-') + datetime.now(timezone.utc).strftime('%Y%m%d') + '-' + uuid.uuid4().hex[:6]
    package = ROOT / 'artifacts' / name
    backup = Path('D:/DNNE-desktop-runs/UnityWorld') / name
    assert not package.exists() and not backup.exists()
    package.mkdir()
    subprocess.run(['git', 'archive', '--format=zip', '--output=' + str(package / 'source.zip'), revision], cwd=ROOT, check=True)
    extract(package / 'source.zip', package / 'source')
    extract(upstream_capture / 'spindleModelTutorial.zip', package / 'upstream', strip_root=True)
    # Preserve every original byte. Only the separately identified execution
    # copy translates the MATLAB empty-options argument into an options struct.
    shutil.copytree(io(package / 'upstream/vendor'), io(package / 'octave-compatible/vendor'))
    edits = []
    before = b'ode23(@derivs,time_step*[0 1],y,[])'
    after = b'ode23(@derivs,time_step*[0 1],y,odeset())'
    for fiber in ('Bag', 'Chain'):
        relative = Path('vendor/matlabMuscleSpindleModellingTools/spindle_model') / ('@halfSarcWithCoop' + fiber) / ('halfSarcWithCoop' + fiber + '.m')
        original = io(package / 'upstream' / relative).read_bytes()
        assert original.count(before) == 1
        changed = original.replace(before, after)
        io(package / 'octave-compatible' / relative).write_bytes(changed)
        edits.append(dict(Path=relative.as_posix(), OriginalSha256=sha256(original).hexdigest().upper(),
                          ExecutionSha256=sha256(changed).hexdigest().upper(), Before=before.decode(), After=after.decode()))
    write(package / 'octave-compatibility.json', dict(Compatibility=COMPATIBILITY, Edits=edits,
        OriginalSourcePreserved=True, EquationsChanged=False, SolverDefaultsChanged=False,
        MatlabAgreementAssessed=False))
    for source, target in ((upstream_capture / 'source.json', 'upstream-source.json'),
            (runtime_capture / 'runtime-files.json', 'runtime-files.json'),
            (runtime_capture / 'download.json', 'runtime-download.json'),
            (package / 'source/tools/backup-unity-run.ps1', 'backup-run.ps1')):
        shutil.copyfile(io(source), io(package / target))
    if canonical:
        candidate, api = state_candidate()
        candidate.prepare(package, api)
    entries = [dict(Path=p.relative_to(io(package)).as_posix(), Sha256=digest(p))
               for p in sorted(io(package).rglob('*')) if p.is_file()]
    config = dict(Revision=revision, UpstreamRevision=UPSTREAM, PreparedUtc=datetime.now(timezone.utc).isoformat(),
        PackageFiles=entries, ExternalFiles=dependencies, PythonExecutable=sys.executable,
        RuntimeDirectory=str(runtime), OctaveHome=str(runtime / 'mingw64'), OctaveExecutable=str(executable),
        BackupDestination=str(backup), NoLiveMonitoring=True, NoTimingMeasured=True,
        ExpectedTrajectories=30, ExpectedBoundaryRecords=2, LiveBodySelected=False,
        UpstreamEquationsModified=canonical, CanonicalStateCandidate=canonical, RuntimeCompatibility=COMPATIBILITY,
        ExecutionSourceAdapted=True, PhysiologyQualified=False)
    write(package / 'configuration.json', config)
    write(package / 'preparation-verification.json', verify_runtime(package))
    pointer = dict(Package=str(package), Backup=str(backup), Revision=revision, PreparedOnly=True,
        NoLiveMonitoring=True, Launcher=str(package / 'source/tools/start-spindle-reference-window.ps1'))
    write(package.parent / (name + '-prepared.json'), pointer)
    print(json.dumps(pointer, indent=2), flush=True)


def vector(value):
    if isinstance(value, list):
        return [n for item in value for n in vector(item)]
    assert isinstance(value, (int, float)) and not isinstance(value, bool) and math.isfinite(value), 'Nonfinite or nonnumeric saved state'
    return [value]


def one(value):
    values = vector(value)
    assert len(values) == 1, 'Expected scalar observation'
    return values[0]


def inputs(protocol, step, dt_us):
    end_us = step * dt_us
    start_us = max(0, step - 1) * dt_us
    pca = 9 if protocol == 'quiet-hold' or (protocol == 'drive-on-off' and not 20000 <= start_us < 80000) else 6.5
    excursion = 20 if protocol == 'stretch-shorten' else -250 if protocol == 'imposed-below-slack' else 0
    fraction = 0
    if 20000 < end_us <= 40000:
        fraction = (end_us - 20000) / 20000
    elif 40000 < end_us <= 60000:
        fraction = 1
    elif 60000 < end_us <= 80000:
        fraction = (80000 - end_us) / 20000
    return pca, 1250 + excursion * fraction


def snapshot(s):
    y, bins = vector(s['y']), vector(s['reportedBinPopulations'])
    assert len(y) == 84 and len(bins) == 81
    assert math.prod(vector(s['yShape'])) == 84 and math.prod(vector(s['reportedBinShape'])) == 81
    stress = vector(s['stressPa'])
    assert len(stress) == math.prod(vector(s['stressShape']))
    # Empty original cache fields are retained as empty, never replaced by zero.
    for field in ('reportedDetached', 'reportedBound', 'crossBridgeStressPa', 'passiveStressPa',
                  'overlap', 'pCa', 'hsLengthNm', 'commandLengthNm'):
        vector(s[field])
    assert s['finite']
    return dict(myosinError=abs(y[0] + sum(y[1:-2]) - 1), actinError=abs(y[-2] + y[-1] - 1),
        cacheDifference=max(abs(a - b) for a, b in zip(y[1:-2], bins)),
        minimumState=min(y), forceElements=len(stress), minimumStressPa=min(stress), maximumStressPa=max(stress))


def analyse(package):
    config = read(package / 'configuration.json')
    assert config['UpstreamRevision'] == UPSTREAM
    verified_adapter_files = verify_compatibility(package)
    folder = package / 'mechanics'
    execution = read(folder / 'execution.json')
    labels = [f'{f}-{p}-{dt}us' for f in FIBERS for p in PROTOCOLS for dt in STEPS_US]
    boundaries = [f + '-state-boundaries' for f in FIBERS]
    expected = set(labels + boundaries)
    outcomes = execution['outcomes']
    assert len(outcomes) == 32 and {o['label'] for o in outcomes} == expected
    assert execution['expectedTrajectories'] == 30 and execution['expectedBoundaryRecords'] == 2
    canonical = config.get('CanonicalStateCandidate', False)
    assert execution['upstreamEquationsModified'] == canonical
    assert not any(execution[k] for k in ('fullSlackDriverExecuted',
        'matlabAgreementAssessed', 'receptorPotentialComputed', 'afferentMembraneExecuted',
        'physiologyQualified', 'avatarRecoveryAssessed'))
    assert {p.name for p in folder.iterdir()} == {'execution.json', 'resolution.json'} | {
        label + ext for label in expected for ext in ('.json', '.mat')}
    for label in expected:
        assert (folder / (label + '.mat')).stat().st_size > 0
    resolution = read(folder / 'resolution.json')
    assert resolution['runtime'] == '11.3.0'
    assert config['RuntimeCompatibility'] == resolution['runtimeCompatibility'] == COMPATIBILITY
    controls = resolution['emptyOptionsCompatibility']
    assert controls['emptyArgumentRejected'] and controls['threeAndStructExactlyEqual']
    assert controls['maximumExponentialError'] <= 1e-8
    for key in ('ode23', 'interp1', 'fzero', 'odeset'):
        Path(resolution[key]).resolve().relative_to(Path(config['OctaveHome']).resolve())
    trajectories, states, failed = [], {}, []
    for outcome in outcomes:
        label = outcome['label']
        if not outcome['completed'] or not outcome['finite']:
            failed.append(outcome)
            continue
        result = read(folder / (label + '.json'))
        if label in boundaries:
            continue
        dt = round(result['dtSeconds'] * 1e6)
        assert dt in STEPS_US and label == f"{result['fiber']}-{result['protocol']}-{dt}us"
        count = 100000 // dt
        assert result['completed'] and result['completedSteps'] == count and result['finite']
        assert math.isclose(result['durationSeconds'], .1, rel_tol=0, abs_tol=1e-12)
        assert not result['slackControllerUsed'] and not result['upstreamODEOptionsChanged']
        rows = result['snapshots']
        assert len(rows) == count + 1
        metrics = []
        for index, s in enumerate(rows):
            assert s['step'] == index and math.isclose(s['timeSeconds'], index * dt / 1e6, rel_tol=0, abs_tol=1e-12)
            pca, length = inputs(result['protocol'], index, dt)
            assert one(s['pCa']) == pca
            assert math.isclose(one(s['hsLengthNm']), length, rel_tol=0, abs_tol=1e-8)
            assert math.isclose(one(s['commandLengthNm']), length, rel_tol=0, abs_tol=1e-8)
            metrics.append(snapshot(s))
        assert vector(rows[0]['y']) == [1] + [0] * 81 + [1, 0], 'Missing unevolved initial state'
        trajectories.append(dict(label=label, samples=len(rows),
            maximumMyosinTotalError=max(m['myosinError'] for m in metrics),
            maximumActinTotalError=max(m['actinError'] for m in metrics),
            maximumPopulationCacheDifference=max(m['cacheDifference'] for m in metrics),
            minimumState=min(m['minimumState'] for m in metrics),
            nonScalarForceSteps=[s['step'] for s, m in zip(rows, metrics) if m['forceElements'] != 1],
            minimumStressPa=min(m['minimumStressPa'] for m in metrics),
            maximumStressPa=max(m['maximumStressPa'] for m in metrics)))
        states[label] = rows
    comparisons = []
    for f in FIBERS:
        for p in PROTOCOLS:
            for coarse, fine in zip(STEPS_US, STEPS_US[1:]):
                a, b = f'{f}-{p}-{coarse}us', f'{f}-{p}-{fine}us'
                if a not in states or b not in states:
                    continue
                differences, force_differences, non_scalar = [], [], []
                for i, x in enumerate(states[a]):
                    y = states[b][i * (coarse // fine)]
                    assert math.isclose(x['timeSeconds'], y['timeSeconds'], rel_tol=0, abs_tol=1e-12)
                    differences.append(max(abs(u-v) for u, v in zip(vector(x['y']), vector(y['y']))))
                    xf, yf = vector(x['stressPa']), vector(y['stressPa'])
                    if len(xf) == len(yf) == 1:
                        force_differences.append(abs(xf[0] - yf[0]))
                    else:
                        non_scalar.append(i)
                comparisons.append(dict(coarse=a, fine=b, alignedSamples=len(differences),
                    maximumStateDifference=max(differences), maximumScalarStressDifferencePa=max(force_differences, default=None),
                    scalarStressPairs=len(force_differences), nonScalarForceCoarseSteps=non_scalar,
                    numericalAcceptanceAssessed=False))
    boundary_reports = []
    for label in boundaries:
        if any(o['label'] == label for o in failed):
            continue
        r = read(folder / (label + '.json'))
        assert r['diagnosticInstantaneousShifts'] and not r['zeroOverlapODEExecuted']
        boundary_reports.append(dict(label=label, states={k: snapshot(r[k]) for k in ('original', 'shifted', 'returned', 'outOfBins')},
            exactBoundaryFromPositive=one(r['exactBoundaryFromPositive']), exactBoundaryFromZero=one(r['exactBoundaryFromZero']),
            boundaryHistoryDifference=abs(one(r['exactBoundaryFromPositive']) - one(r['exactBoundaryFromZero']))))
    return dict(complete=len(trajectories) == 30 and len(boundary_reports) == 2 and not failed,
        trajectories=trajectories, refinementComparisons=comparisons, boundaries=boundary_reports, failedExecutions=failed,
        interpretation=('Canonical-state candidate with explicit unsupported-overlap rejection' if canonical else
            'Original mechanics equations with a declared Octave options-binding adapter; finite execution and descriptive differences only'),
        runtimeCompatibility=COMPATIBILITY, verifiedCompatibilityFiles=verified_adapter_files,
        modelExecutedDuringAnalysis=False, numericalAcceptanceAssessed=False, matlabAgreementAssessed=False,
        receptorPotentialComputed=False, afferentMembraneExecuted=False, physiologyQualified=False, avatarRecoveryAssessed=False)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('mode', choices=('prepare', 'prepare-state', 'verify', 'analyse', 'analyse-state'))
    parser.add_argument('--package', type=Path)
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    if args.mode in ('prepare', 'prepare-state'):
        prepare(args.mode == 'prepare-state')
    else:
        assert args.package and args.output
        if args.mode == 'analyse-state':
            candidate, api = state_candidate()
            report = candidate.analyse(args.package.resolve(), api)
        else:
            report = (verify_runtime if args.mode == 'verify' else analyse)(args.package.resolve())
        write(args.output.resolve(), report)
        print(json.dumps({k: v for k, v in report.items() if k not in ('trajectories', 'refinementComparisons', 'boundaries', 'sourceComparison', 'boundaryControls')}, indent=2))
        if args.mode in ('analyse', 'analyse-state') and (not report['complete'] or not report.get('canonicalStateChecksPassed', True)):
            raise RuntimeError('Native execution incomplete/nonfinite; retained evidence requires review')

"""Prepare/review frozen component gates; never run a neuron or inspect a live job.

Review is permitted only after the operator reports completion. The explicit
CLI acknowledgement records that human workflow; it is not a completion probe.
Preparation requires a successful, D:-verified component review. All actual
tests and source integrations belong to the separate visible PowerShell window.
"""
import argparse
from collections import Counter
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path, PurePosixPath
import re
import shutil
import subprocess
import uuid
import xml.etree.ElementTree as ET
from zipfile import ZipFile

ROOT = Path(__file__).resolve().parents[1]
COMPONENT = dict(MotorReceptorContactTests=14, MotoneuronReferenceTests=11,
    MotorAxonTransportTests=32, MotorAxonReplayBufferTests=16, SolutionManifestTests=1)
COUPLED = dict(ReceptorDrivenMotoneuronTests=15, MotorReceptorContactTests=14,
    MotoneuronReferenceTests=11, MotorAxonTransportTests=32, MotorAxonReplayBufferTests=16,
    ObservedMotorSourceTests=8, ReceptorMotorSourceTests=14, SolutionManifestTests=1)
REFERENCES = ['boundary-analysis.json', 'boundary-input.json', 'source-results.json',
    'source-metadata.json', 'quiet-h0.003125.csv', 'soma-6000-h0.003125.csv']
PRESERVED = ['Structures/_SharedRuntime/MotoneuronChannelKinetics.cs',
    'tools/MotoneuronReferenceProbe/ClockAudit.cs', 'Protocol/MotorAxonProtocol.cs',
    'Protocol/MotorAxonPublisher.cs', 'Protocol/MotorAxonReplayBuffer.cs',
    'src/NRE.SimAvatar/AvatarMotorAxonReceiver.cs',
    'tools/NativeMotorBridgeProbe/ObservedMotorSourceStudy.cs', 'tools/analyse-observed-motor-source.py']
SOURCE_ADAPTER = 'tools/NativeMotorBridgeProbe/ObservedMotorSource.cs'
SOURCE_EXTENSION_REVISION = '5ce3ec7841d90c1d41f488baf7644f0fa1136aca'


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec); spec.loader.exec_module(result)
    return result


def helper():
    return module('receptor_package_helpers', ROOT / 'tools/prepare-causal-motor.py')


def inventory(source, counts):
    h = helper(); result = Counter()
    for cls, count in counts.items():
        code = h.io(source / 'tests/NeuralResonanceEngine.DNNE.Tests' / (cls + '.cs')).read_text(encoding='utf-8-sig')
        found = re.findall(r'((?:\s*\[[^\n]+\])+\s*)public\s+(?:async\s+)?(?:void|Task)\s+(\w+)\(', code)
        methods = {name: 1 if '[Fact]' in attrs else attrs.count('[InlineData(')
                   for attrs, name in found if '[Fact]' in attrs or '[Theory]' in attrs}
        assert sum(methods.values()) == count and all(methods.values()), cls
        result.update({'NeuralResonanceEngine.DNNE.Tests.' + cls + '.' + name: n for name, n in methods.items()})
    return result


def check_tests(package, counts):
    h = helper(); config = h.read(package / 'configuration.json')
    expected = inventory(package / 'source', counts)
    assert config['ExpectedClassCounts'] == counts and config['ExpectedTestCases'] == sum(counts.values())
    assert expected == Counter(config['ExpectedTestMethods'])
    ns = {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
    trx = ET.parse(h.io(package / 'test-results/focused.trx'))
    results = trx.findall('.//t:UnitTestResult', ns)
    assert len(results) == len({r.attrib['testName'] for r in results}) == sum(counts.values())
    assert all(r.attrib['outcome'] == 'Passed' for r in results)
    definitions = {}
    for unit in trx.findall('.//t:TestDefinitions/t:UnitTest', ns):
        method = unit.find('t:TestMethod', ns); assert method is not None
        assert unit.attrib['id'] not in definitions
        definitions[unit.attrib['id']] = method.attrib['className'] + '.' + method.attrib['name']
    assert Counter(definitions[r.attrib['testId']] for r in results) == expected
    return dict(passedCases=len(results), passedClasses=counts, exactMethodMultiplicity=True)


def review_component(package, output, reported):
    if not reported:
        raise ValueError('Wait for the operator completion report before reading this capture.')
    assert not output.exists(), 'Use a new review directory; preserve earlier evidence.'
    common = module('receptor_capture_audit', ROOT / 'tools/review-olive-and-motor-20261109.py')
    status = common.read(package / 'window-status.json')
    # A failed/interrupted capture needs diagnosis, not a success review.
    assert status['Status'] == 'DIAGNOSTICS_COMPLETE' and status['ExitCode'] == 0
    revision = '74e1c62a20d78a88e8a672140a1816f75c6099b1'
    reviewed = common.verify(package, ['test-binaries.json'], revision)
    archive = module('receptor_archive_audit', ROOT / 'tools/review-spindle-root-and-causal-motor-20261113.py')
    reviewed['verifiedGitFiles'] = archive.source_archive(common, package, revision)
    tests = check_tests(package, COMPONENT); config = common.read(package / 'configuration.json')
    assert config['IsolatedMotorReceptor'] and config['QualifiedMotorCorePreserved'] and config['NoLiveMonitoring']
    assert status['MotorReceptorAndRegressionChecksPassed'] and status['PassedFocusedTests'] == 74
    assert status['ModelExecutionAttempted'] and status['IncludesNeuronalRegressionChecks']
    for key in ('LiveBrainAccessed', 'OldSavesLoaded', 'LiveUnityIntegrated', 'TimingMeasured',
                'IndependentReceptorReferenceQualified', 'PhysiologyQualified', 'AvatarRecoveryAssessed', 'FullUnitSuitePassed'):
        assert status[key] is False, key
    preservation = common.read(package / 'reference/core-preservation.json')
    h = helper(); baseline = Path(preservation['capture'])
    assert h.digest(baseline / 'backup-manifest.json') == preservation['sourceManifestSha256']
    h.verify(baseline, ['source/' + row['path'] for row in preservation['preservedFiles']])
    for row in preservation['preservedFiles']:
        assert common.digest(baseline / 'source' / row['path']) == row['qualifiedSha256']
        assert common.io(package / 'source' / row['path']).read_text(encoding='utf-8-sig') == \
            common.io(baseline / 'source' / row['path']).read_text(encoding='utf-8-sig')
    reviewed.update(reviewedUtc=datetime.now(timezone.utc).isoformat(), operatorReportedCompletion=True,
        componentCriteriaPassed=True, testInventory=tests, passedTests=74,
        testsExecutedDuringReview=False, modelsSolvedDuringReview=False, coupledNeuronalAccuracyQualified=False,
        liveMotorAuthorityQualified=False, physiologyQualified=False, avatarRecoveryAssessed=False)
    output.mkdir(parents=True)
    (output / 'review.json').write_text(json.dumps(reviewed, indent=2) + '\n')
    print(json.dumps(reviewed, indent=2), flush=True)


def review_gate(package, output, reported):
    if not reported:
        raise ValueError('Wait for the operator completion report before reading this gate.')
    assert not output.exists(), 'Use a new review directory.'
    common = module('membrane_gate_capture', ROOT / 'tools/review-olive-and-motor-20261109.py')
    config = common.read(package / 'configuration.json'); status = common.read(package / 'window-status.json')
    assert config['IsolatedReceptorMembraneGate'] and config['NoLiveMonitoring']
    reviewed = common.verify(package, ['test-binaries.json', 'probe-binaries.json'], config['Revision'])
    archive = module('membrane_gate_archive', ROOT / 'tools/review-spindle-root-and-causal-motor-20261113.py')
    reviewed['verifiedGitFiles'] = archive.source_archive(common, package, config['Revision'])
    tests = check_tests(package, COUPLED)
    for key in ('SourceAdapterCriteriaPassed', 'ReceptorMembraneComponentChecksPassed', 'NeuronalModelExecutionConfirmed',
                'NeuronalModelExecutionAttempted', 'OriginalSourceExecutionAttempted', 'ManagedRuntimePreserved'):
        assert status[key] is True, key
    for key in ('LiveBrainAccessed', 'OldSavesLoaded', 'NativePhysicsExecuted', 'MuscleDynamicsExecuted',
                'IndependentCoupledReferenceQualified', 'PhysiologyQualified', 'AvatarRecoveryAssessed', 'FullUnitSuitePassed'):
        assert status[key] is False, key
    assert status['PassedFocusedTests'] == 111 and status['SuccessfulConditions'] == 5
    audit_module = module('frozen_membrane_source_audit', package / 'source/tools/analyse-observed-motor-source.py')
    analysis = audit_module.audit(package)
    assert analysis == common.read(package / 'source-analysis.json') and analysis['sourceAdapterCriteriaPassed']
    assert analysis['activeCadenceEventIdentityEqual'] and len(analysis['cases']) == 5
    provenance = common.read(package / 'provenance.json')
    assert provenance['sourceReview']['operatorReportedCompletion'] and provenance['sourceReview']['sourceAdapterCriteriaPassed']
    assert provenance['receptorReview']['operatorReportedCompletion'] and provenance['receptorReview']['componentCriteriaPassed']
    h = helper(); baseline = Path(provenance['sourceReview']['capture'])
    assert h.digest(baseline / 'backup-manifest.json') == provenance['sourceReview']['manifestSha256']
    h.verify(baseline, ['reference/' + name for name in REFERENCES])
    for row in provenance['copied']:
        assert common.digest(package / row['path']) == common.digest(row['original']) == row['sha256']
    before = common.read(package / 'managed-runtime-before.json'); after = common.read(package / 'managed-runtime-after.json')
    assert before == after and common.digest(package / 'managed-runtime-before.json') == common.digest(package / 'managed-runtime-after.json')
    assert before['version'] == 'motor-probe-runtime-dependencies-v1' and before['modelsExecuted'] is False and before['testsExecuted'] is False
    assert before['appLocalDependenciesExcluded'] and before['appLocalFilesRequireSeparateBinaryManifest']
    probe = package / 'source/artifacts/receptor-membrane-source-probe/bin/NativeMotorBridgeProbe/release'
    binaries = common.read(package / 'probe-binaries.json')
    assert binaries and (probe / 'NeuralResonanceEngine.DNNE.Tests.dll').is_file()
    for entry in binaries: Path(entry['Path']).relative_to(probe)
    reviewed.update(reviewedUtc=datetime.now(timezone.utc).isoformat(), operatorReportedCompletion=True,
        gateCriteriaPassed=True, passedTests=111, originalSourceConditions=5, savedAnalysisExactlyReproduced=True,
        testInventory=tests, analysis=analysis, managedRuntimeSha256=common.digest(package / 'managed-runtime-before.json'),
        qualifiedProbeDirectory=str(probe), qualifiedProbeFiles=len(binaries),
        testsExecutedDuringReview=False, modelsSolvedDuringReview=False, independentCoupledAccuracyQualified=False,
        physiologyQualified=False, avatarRecoveryAssessed=False)
    output.mkdir(parents=True)
    (output / 'review.json').write_text(json.dumps(reviewed, indent=2) + '\n')
    shutil.copyfile(package / 'managed-runtime-before.json', output / 'managed-runtime.json')
    print(json.dumps({key: reviewed[key] for key in ('capture', 'sourceRevision', 'passedTests', 'originalSourceConditions',
        'verifiedFilesPerCopy', 'verifiedGitFiles', 'qualifiedProbeFiles', 'gateCriteriaPassed')}, indent=2), flush=True)


def prepare(review_folder):
    h = helper()
    assert Path(h.git('rev-parse', '--show-toplevel')).resolve() == ROOT.resolve(), 'Prepare only from the owning checkout.'
    assert not h.git('status', '--porcelain'), 'Commit clean source first.'
    revision = h.git('rev-parse', 'HEAD'); assert revision == h.git('rev-parse', 'origin/main'), 'Push first.'
    h.verify(review_folder, ['review.json']); receptor = h.read(review_folder / 'review.json')
    assert receptor['operatorReportedCompletion'] and receptor['componentCriteriaPassed'] and receptor['passedTests'] == 74
    assert receptor['sourceRevision'] == '74e1c62a20d78a88e8a672140a1816f75c6099b1'
    receptor_capture = Path(receptor['capture'])
    assert h.digest(receptor_capture / 'backup-manifest.json') == receptor['manifestSha256']
    h.verify(receptor_capture, ['window-status.json', 'configuration.json', 'test-results/focused.trx'])
    check_tests(receptor_capture, COMPONENT)
    source_review_folder = ROOT / 'artifacts/observed-motor-source-review-20261116'
    h.verify(source_review_folder, ['review.json']); source_review = h.read(source_review_folder / 'review.json')
    assert source_review['operatorReportedCompletion'] and source_review['savedAnalysisExactlyReproduced']
    assert source_review['sourceAdapterCriteriaPassed'] and source_review['conditions'] == 5 and source_review['sourceDurationMs'] == 6000
    capture = Path(source_review['capture'])
    assert h.digest(capture / 'backup-manifest.json') == source_review['manifestSha256']
    membrane = 'Structures/_SharedRuntime/MotoneuronReferenceCell.cs'
    h.verify(capture, ['reference/' + n for n in REFERENCES] + ['source/' + n for n in PRESERVED + [SOURCE_ADAPTER, membrane]])
    for name in PRESERVED:
        assert (ROOT / name).read_text(encoding='utf-8-sig') == h.io(capture / 'source' / name).read_text(encoding='utf-8-sig'), name
    expected_adapter = subprocess.check_output(['git', 'show', SOURCE_EXTENSION_REVISION + ':' + SOURCE_ADAPTER], cwd=ROOT).decode('utf-8-sig')
    assert (ROOT / SOURCE_ADAPTER).read_text(encoding='utf-8-sig') == expected_adapter.replace('\r\n', '\n')
    authoring = ROOT / 'artifacts/receptor-membrane-authoring-20261116-r3'
    authoring_names = [row['Path'].replace('\\', '/') for row in h.read(authoring / 'backup-manifest.json')['Files']]
    h.verify(authoring, authoring_names)
    author = h.read(authoring / 'authoring-review.json')
    assert author['prospectiveTotalCases'] == 97 and author['buildSucceeded'] and not author['testsExecuted']
    for entry in author['candidateFiles']:
        if entry['path'].endswith('.cs'):
            assert h.digest(authoring / 'candidate' / entry['path']) == entry['sha256']
            assert (ROOT / entry['path']).read_text(encoding='utf-8-sig') == h.io(authoring / 'candidate' / entry['path']).read_text(encoding='utf-8-sig'), entry['path']
    source_authoring = ROOT / 'artifacts/receptor-motor-source-authoring-20261116'
    source_authoring_names = [e['Path'].replace('\\', '/') for e in h.read(source_authoring / 'backup-manifest.json')['Files']]
    h.verify(source_authoring, source_authoring_names)
    source_author = h.read(source_authoring / 'authoring-review.json')
    assert source_author['prospectiveNewCases'] == 14 and source_author['prospectiveTotalCases'] == 111
    assert source_author['buildSucceeded'] and not source_author['testsExecuted'] and not source_author['modelsExecuted']
    source_candidates = [e for e in source_author['candidateFiles'] if e['path'].endswith('.cs')]
    assert {e['path'] for e in source_candidates} == {'Structures/_SharedRuntime/ReceptorMotorSource.cs',
        'tests/NeuralResonanceEngine.DNNE.Tests/ReceptorMotorSourceTests.cs'}
    for entry in source_candidates:
        saved = source_authoring / 'candidate' / entry['path']
        assert h.digest(saved) == entry['sha256']
        assert (ROOT / entry['path']).read_text(encoding='utf-8-sig') == h.io(saved).read_text(encoding='utf-8-sig'), entry['path']
    methods = inventory(ROOT, COUPLED)
    package = ROOT / 'artifacts' / ('receptor-membrane-' + datetime.now(timezone.utc).strftime('%Y%m%d') + '-' + uuid.uuid4().hex[:6])
    backup = Path('D:/DNNE-desktop-runs/UnityWorld') / package.name
    assert not package.exists() and not backup.exists(); package.mkdir()
    subprocess.run(['git', 'archive', '--format=zip', '--output=' + str(package / 'source.zip'), revision], cwd=ROOT, check=True)
    with ZipFile(package / 'source.zip') as archive:
        for entry in archive.infolist():
            name = PurePosixPath(entry.filename)
            assert not name.is_absolute() and '..' not in name.parts and ':' not in entry.filename and '\\' not in entry.filename
            assert ((entry.external_attr >> 16) & 0o170000) != 0o120000
            target = h.io(package / 'source' / name)
            if entry.is_dir(): target.mkdir(parents=True, exist_ok=True)
            else:
                target.parent.mkdir(parents=True, exist_ok=True)
                with target.open('xb') as stream: stream.write(archive.read(entry))
    reference = package / 'reference'; reference.mkdir()
    copied = []
    for name in REFERENCES:
        original = capture / 'reference' / name; target = reference / name
        shutil.copyfile(h.io(original), target)
        assert h.digest(original) == h.digest(target)
        copied.append(dict(original=str(original), path='reference/' + name, sha256=h.digest(target)))
    changed = []
    for name in (SOURCE_ADAPTER, membrane):
        target = reference / 'baseline' / name; target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(h.io(capture / 'source' / name), target)
        changed.append(dict(path=name, originalSha256=h.digest(target), candidateSha256=h.digest(ROOT / name),
            qualifiedFileUnchanged=False, exactTraceReproductionRequired=True))
    for folder, destination in [(review_folder, 'receptor-review.json'), (source_review_folder, 'source-review.json'), (authoring, 'authoring-review.json')]:
        name = 'authoring-review.json' if folder == authoring else 'review.json'
        shutil.copyfile(folder / name, reference / destination)
    for name in authoring_names:
        target = h.io(reference / 'authoring' / name); target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(h.io(authoring / name), target)
    for name in source_authoring_names:
        target = h.io(reference / 'motor-source-authoring' / name); target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(h.io(source_authoring / name), target)
    provenance = dict(receptorReview=receptor, sourceReview=source_review, authoringReview=author,
        motorSourceAuthoringReview=source_author,
        preservedSourceFiles=PRESERVED, copied=copied, changedSourceFiles=changed, membraneFileChanged=True,
        sourceOwnershipExtensionPresent=True, sourceOwnershipExtensionRevision=SOURCE_EXTENSION_REVISION,
        newReceptorInputPathQualified=False, originalTraceReproductionRequired=True,
        testsExecutedDuringPreparation=False, modelsExecutedDuringPreparation=False)
    (package / 'provenance.json').write_text(json.dumps(provenance, indent=2) + '\n')
    shutil.copyfile(package / 'source/tools/backup-unity-run.ps1', package / 'backup-run.ps1')
    files = [dict(Path=p.relative_to(h.io(package)).as_posix(), Sha256=h.digest(p)) for p in sorted(h.io(package).rglob('*')) if p.is_file()]
    dotnet = shutil.which('dotnet'); python = Path('C:/Users/duff_/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe')
    assert dotnet and python.is_file()
    config = dict(Revision=revision, PreparedUtc=datetime.now(timezone.utc).isoformat(), PackageFiles=files,
        DotnetExecutable=dotnet, DotnetExecutableSha256=h.digest(dotnet), PythonExecutable=str(python), PythonExecutableSha256=h.digest(python),
        BackupDestination=str(backup), NoLiveMonitoring=True, IsolatedReceptorMembraneGate=True,
        ExpectedConditions=5, ExpectedTestCases=sum(COUPLED.values()), ExpectedClassCounts=COUPLED,
        ExpectedTestMethods=methods, TestFilter='|'.join('FullyQualifiedName~' + c for c in COUPLED),
        NativePhysicsRequested=False, IndependentCoupledReferenceQualified=False, PhysiologyQualified=False, AvatarRecoveryAssessed=False)
    (package / 'configuration.json').write_text(json.dumps(config, indent=2) + '\n')
    pointer = dict(Package=str(package), Backup=str(backup), Revision=revision, PreparedOnly=True, NoLiveMonitoring=True,
        Launcher=str(package / 'source/tools/start-receptor-membrane-window.ps1'))
    (package.parent / (package.name + '-prepared.json')).write_text(json.dumps(pointer, indent=2) + '\n')
    print(json.dumps(pointer, indent=2), flush=True)


def main():
    if not __debug__: raise RuntimeError('Gate preparation and review assertions must remain enabled.')
    parser = argparse.ArgumentParser(description=__doc__); commands = parser.add_subparsers(dest='command', required=True)
    review = commands.add_parser('review-component')
    review.add_argument('package', type=Path); review.add_argument('output', type=Path)
    review.add_argument('--operator-reported-complete', action='store_true', required=True)
    gate_review = commands.add_parser('review-gate')
    gate_review.add_argument('package', type=Path); gate_review.add_argument('output', type=Path)
    gate_review.add_argument('--operator-reported-complete', action='store_true', required=True)
    prepare_command = commands.add_parser('prepare'); prepare_command.add_argument('--receptor-review', type=Path, required=True)
    check = commands.add_parser('check-tests'); check.add_argument('package', type=Path)
    args = parser.parse_args()
    if args.command == 'review-component':
        review_component(args.package.resolve(), args.output.resolve(), args.operator_reported_complete)
    elif args.command == 'review-gate':
        review_gate(args.package.resolve(), args.output.resolve(), args.operator_reported_complete)
    elif args.command == 'prepare': prepare(args.receptor_review.resolve())
    else: print(json.dumps(check_tests(args.package.resolve(), COUPLED), indent=2), flush=True)


if __name__ == '__main__': main()

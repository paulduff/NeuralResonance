"""Review operator-reported completed sensory studies; no tests/ODE reruns."""
import argparse
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path, PurePosixPath
import shutil
import sys

ROOT = Path(__file__).resolve().parents[1]


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    value = importlib.util.module_from_spec(spec); spec.loader.exec_module(value)
    return value


def review(package, reported):
    assert reported, 'Wait for the operator final completion report.'
    canal = package.name.startswith('coupled-canal-')
    assert canal or package.name.startswith('afferent-endpoint-')
    assert package.parent == (Path('D:/DNNE-build-work') if canal else ROOT / 'artifacts')
    output = ROOT / 'artifacts' / (package.name + '-review'); assert not output.exists()
    files = module('completed_sensory_files', ROOT / 'tools/review-olive-and-motor-20261109.py')
    archive = module('completed_sensory_source', ROOT / 'tools/review-spindle-root-and-causal-motor-20261113.py')
    cfg = files.read(package / 'configuration.json'); status = files.read(package / 'window-status.json')
    result = files.verify(package, ['test-binaries.json', 'probe-binaries.json'], cfg['Revision'], progress_every=1000)
    result['verifiedGitFiles'] = archive.source_archive(files, package, cfg['Revision'])
    sys.path.insert(0, str(package / 'source/tools'))
    try:
        gate = module('completed_sensory_gate', package / 'source/tools' / ('coupled_canal_gate.py' if canal else 'afferent_endpoint_gate.py'))
        gate.runtime(package)
        tests = gate.inventory.check_tests(package, gate.COUNTS)
        if canal:
            analysis = dict(completed=True, componentChecksPassed=True, tests=tests, coupledCanalChecksPassed=True,
                independentPublishedTrajectoryQualified=False, sourceToSkullFrameQualified=False, localBundleMapQualified=False,
                mechanicalCalibrationQualified=False, independentCoupledTrajectoryQualified=False,
                stationaryInitializationQualified=False, spatialGeometryCalibrated=False, afferentFiringQualified=False,
                physiologyQualified=False, avatarRecoveryAssessed=False)
            assert analysis == files.read(package / 'analysis.json')
            assert status['CoupledCanalChecksPassed'] and status['PassedFocusedTests'] == tests['passedCases'] == 76
            result.update(componentCriteriaPassed=True, coupledCanalChecksPassed=True, passedTests=76,
                independentPublishedTrajectoryQualified=False, sourceToSkullFrameQualified=False, localBundleMapQualified=False,
                archiveCopiesShareDrive=True, analysisSha256=files.digest(package / 'analysis.json'))
        else:
            assert status['NumericalTrajectoryCriteriaPassed'] and status['OriginalNativeReplayByteIdentical'] and status['SpectrumControlsPassed']
            assert status['PassedFocusedTests'] == tests['passedCases'] == 333
            assert files.read(package / 'spectrum-controls.json') == dict(completed=True, passed=8, expected=8, modelsExecuted=False)
            replay = files.read(package / 'original-replay.json'); expected = files.read(package / 'provenance.json')['originalReplayFiles']
            assert replay['completed'] and replay['originalNativeReplayByteIdentical']
            reconstructed = []
            for row in expected:
                original = package / 'original-study' / PurePosixPath(row['path']).relative_to('study')
                assert files.digest(package / 'baseline' / row['path']) == row['sha256']
                digest = files.digest(original); assert digest == row['sha256']
                reconstructed.append(dict(path=row['path'], expectedSha256=row['sha256'], actualSha256=digest))
            assert reconstructed == replay['files']
            assert {p.relative_to(package / 'original-study').as_posix() for p in (package / 'original-study').rglob('*') if p.is_file()} == {
                str(PurePosixPath(row['path']).relative_to('study')) for row in expected}
            protocol = gate.prerequisites(package)
            original_reference = files.read(package / 'original-reference-replay.json')
            assert original_reference == [dict(key=case['key'], exactOriginalDerivatives=True) for case in protocol['cases']]
            assert files.read(package / 'study/activation-continuation.json') == dict(version=gate.MODEL, policy='EndpointHold', originalDefaultChanged=False, physiologicalCalibrationQualified=False)
            for case in protocol['cases']:
                for ticks in protocol['stepTicks']:
                    relative = f"{case['key']}-ticks{ticks}/initial.json"
                    before = files.read(package / 'original-study' / relative); after = files.read(package / 'study' / relative)
                    assert before['fingerprint'] != after['fingerprint']
                    for key in ('values', 'ledgers', 'stateLabels', 'ledgerLabels', 'settings', 'sessionId', 'cellId'):
                        assert before[key] == after[key], key
            analyser = module('completed_endpoint_saved_analysis', package / 'source/tools/afferent_trajectory_analysis.py')
            analysis = analyser.analyse(package, protocol, files.read(package / 'source-decisions.json'))
            assert analysis == files.read(package / 'trajectory-analysis.json')
            assert analysis['numericalTrajectoryCriteriaPassed'] and len(analysis['cases']) == 7
            assert all(row['passed'] for row in analysis['cases'])
            result.update(passedTests=333, spectrumControlsPassed=8, numericalTrajectoryCriteriaPassed=True,
                analysisSha256=files.digest(package / 'trajectory-analysis.json'), nativeRuns=21, references=21,
                originalNativeReplayRuns=21, originalNativeReplayByteIdentical=True, originalReplayFilesVerified=len(expected),
                originalReferenceWitnessReplayReported=True, originalBoundsChanged=False, originalDefaultsChanged=False,
                newKineticAssumption=True, fastTransientOnly=True, allSevenCasesPass=True,
                cases=[dict(key=row['key'], errorFractions=[r['error']['maximumAllowedFraction'] for r in row['candidates']],
                    referenceCriteriaPassed=row['referenceCriteriaPassed'], sampledRefinementPassed=row['sampledRefinementPassed'],
                    ledgerCriteriaPassed=row['ledgerCriteriaPassed']) for row in analysis['cases']])
    finally:
        sys.path.pop(0)
    assert files.digest(package / 'managed-runtime-before.json') == files.digest(package / 'managed-runtime-after.json')
    for key in ('ComponentChecksPassed', 'TestExecutionAttempted', 'MatchesQualifiedManagedRuntime',
                'QualifiedDependenciesPreserved', 'ManagedRuntimePreserved', 'FrozenPackageUnchanged'):
        assert status[key] is True, key
    for key in ('IndependentCoupledTrajectoryQualified', 'StationaryInitializationQualified', 'SpatialGeometryCalibrated',
                'AfferentFiringQualified', 'LiveBrainAccessed', 'OldSavesLoaded', 'NativeBodyExecuted', 'PhysiologyQualified',
                'AvatarRecoveryAssessed', 'FullUnitSuitePassed'):
        assert status[key] is False, key
    result.update(reviewedUtc=datetime.now(timezone.utc).isoformat(), operatorReportedCompletion=True,
        savedAnalysisExactlyReproduced=True, modelsExecutedDuringReview=False, testsExecutedDuringReview=False,
        independentCoupledTrajectoryQualified=False, stationaryInitializationQualified=False, afferentFiringQualified=False,
        physiologyQualified=False, avatarRecoveryAssessed=False)
    output.mkdir()
    (output / 'review.json').write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    (output / 'analysis.json').write_text(json.dumps(analysis, indent=2) + '\n', encoding='utf-8')
    shutil.copyfile(__file__, output / Path(__file__).name)
    print(json.dumps(dict(output=str(output), verifiedFiles=result['verifiedFilesPerCopy'], passedTests=result['passedTests'],
        savedAnalysisExactlyReproduced=True, cases=result.get('cases', [])), indent=2), flush=True)


if __name__ == '__main__':
    if not __debug__: raise RuntimeError('Assertions must remain enabled.')
    parser = argparse.ArgumentParser(); parser.add_argument('package', type=Path)
    parser.add_argument('--operator-reported-complete', action='store_true'); args = parser.parse_args()
    review(args.package.resolve(), args.operator_reported_complete)

"""Review operator-reported captures using saved data only; never rerun models."""
import argparse
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    value = importlib.util.module_from_spec(spec); spec.loader.exec_module(value)
    return value


def forbid_solve(*args, **kwargs):
    raise RuntimeError('This is a saved-evidence review; model solving is forbidden.')


def start(kind, capture, revision, binaries):
    common = module('completed_capture_integrity', ROOT / 'tools/review-olive-and-motor-20261109.py')
    output = ROOT / 'artifacts' / (kind + '-review-20261116')
    assert not output.exists(), 'Preserve earlier reviews.'
    print('Verifying all reported-complete C:/D: files, frozen source and binaries: ' + capture.name, flush=True)
    result = common.verify(capture, binaries, revision)
    archive = common.module('completed_source_archive', ROOT / 'tools/review-spindle-root-and-causal-motor-20261113.py')
    result['verifiedGitFiles'] = archive.source_archive(common, capture, revision)
    sys.path.insert(0, str(capture / 'source/tools'))
    # Imported numerical helpers can evaluate saved arrays and formulae only.
    import scipy.integrate
    scipy.integrate.solve_ivp = forbid_solve
    result.update(reviewedUtc=datetime.now(timezone.utc).isoformat(), operatorReportedCompletion=True,
        testsExecutedDuringReview=False, modelsSolvedDuringReview=False, originalBoundsChanged=False,
        physiologyQualified=False, avatarRecoveryAssessed=False)
    return common, result, output


def finish(output, report):
    output.mkdir()
    (output / 'review.json').write_text(json.dumps(report, indent=2, allow_nan=False) + '\n')
    print(json.dumps({k: v for k, v in report.items() if not isinstance(v, (dict, list))}, indent=2), flush=True)


def body():
    capture = ROOT / 'artifacts/observed-native-body-20261116-bb10dc'
    common, report, output = start('observed-native-body', capture, 'f9322f1c222186084b156bd34306e58582761cd8',
        ['test-binaries.json', 'probe-binaries.json'])
    config = common.read(capture / 'configuration.json'); status = common.read(capture / 'window-status.json')
    assert config['IsolatedNeuronalNative'] and config['NoLiveMonitoring']
    for flag in ('MusclePathAndRegressionChecksPassed', 'NeuronalModelExecuted', 'NativeBridgeChecksPassed', 'UnityBuildSucceeded',
                 'SourceAdapterCriteriaPassed', 'NativeDriverSucceeded', 'NeuronalNativeCriteriaPassed', 'ReferenceEnvironmentUnchanged'):
        assert status[flag] is True, flag
    for flag in ('LiveBrainAccessed', 'OldSavesLoaded', 'IncludesFixtureMotorEvents', 'LiveUnityIntegrated', 'TimingMeasured',
                 'PhysiologyQualified', 'AvatarRecoveryAssessed', 'FullUnitSuitePassed'):
        assert status[flag] is False, flag
    assert status['PassedFocusedTests'] == 160 and status['PassedNativeCases'] == 7 and status['ActualPhysicsSteps'] == 14336
    counts = dict(MusclePathGeometryTests=31, CausalMotorPlantTests=27, DynamicMuscleTests=16,
        DiagnosticBridgeFileTests=6, MuscleHingeClockTests=12, SolutionManifestTests=1,
        ObservedMotorSourceTests=8, MotorAxonTransportTests=32, MotorAxonReplayBufferTests=16, MotoneuronReferenceTests=11)
    gate = common.module('test_inventory', ROOT / 'tools/receptor_membrane_gate.py')
    report['testInventory'] = gate.check_tests(capture, counts)
    for entry in common.read(capture / 'player-files.json'):
        Path(entry['Path']).relative_to(capture / 'player'); assert common.digest(entry['Path']) == entry['Sha256']
    compiled = 0; unity_source = common.io(capture / 'source/UnityWorld')
    for path in unity_source.rglob('*.cs'):
        assert path.read_bytes() == common.io(capture / 'built-project' / path.relative_to(unity_source)).read_bytes(); compiled += 1
    build = common.read(capture / 'player/build-result.json'); assert build['result'] == 'Succeeded' and build['errors'] == 0
    metadata = common.module('frozen_unity_metadata', capture / 'source/tools/unity_metadata.py')
    assert metadata.validate(capture / 'source/UnityWorld') == common.read(capture / 'unity-metadata.json')
    source_audit = common.module('frozen_original_source', capture / 'source/tools/analyse-observed-motor-source.py')
    source = source_audit.audit(capture); assert source == common.read(capture / 'source-analysis.json')
    assert source['sourceAdapterCriteriaPassed'] and len(source['cases']) == 5
    print('Reproducing saved neuronal, motor, native-command and independent-reference analyses...', flush=True)
    audit = common.module('frozen_neuronal_body', capture / 'source/tools/analyse-observed-native-body.py')
    audit.solve = forbid_solve; audit.HingeCoupledReference.solve = forbid_solve
    analysis = audit.audit(capture)
    assert analysis == common.read(capture / 'neuronal-body-analysis.json')
    assert analysis['completed'] and analysis['numericalCriteriaPassed'] and analysis['independentReferences'] == 12
    assert analysis['nativeSourceEvidence'] == common.read(capture / 'native-source-analysis.json')
    evidence = analysis['nativeSourceEvidence']
    assert evidence['sourceBodyCriteriaPassed'] and evidence['packetAndRetryStateBitwiseEqual'] and evidence['sourceTracePrefixesExactlyReproduced']
    maxima = {key: max(c['errors'][key] for r in analysis['cases'] for c in r['comparisons'])
        for key in analysis['cases'][0]['comparisons'][0]['errors']}
    spec = common.read(capture / 'neuronal-hinge-input.json')
    body_duration = 2048 * spec['nativeGeometry']['stepSeconds'] * 1000
    assert body_duration == 128
    membrane = 'Structures/_SharedRuntime/MotoneuronReferenceCell.cs'
    current_membrane_same = (ROOT / membrane).read_text(encoding='utf-8-sig') == common.io(capture / 'source' / membrane).read_text(encoding='utf-8-sig')
    report.update(passedTests=160, compiledUnitySourceFilesVerified=compiled, sourceConditions=5,
        savedAnalysisExactlyReproduced=True, savedSourceAnalysisExactlyReproduced=True,
        numericalCriteriaPassed=True, actualPhysicsSteps=14336, nativeConditions=7, independentReferences=12,
        observedMotorEventsDelivered=sum(c['observedCrossingsDelivered'] for c in evidence['cases']),
        maximumCandidateErrors=maxima, candidateBounds=spec['candidateBounds'], analysis=analysis,
        fullSourceTraceDurationMs=6000, sourcePrimingMs=1000, coupledBodyDurationMs=body_duration,
        currentMembraneFileUnchanged=current_membrane_same, currentReceptorCandidateSeparatelyGated=True,
        physicalStepRefinementForThisInputAssessed=False, liveMotorAuthorityQualified=False)
    finish(output, report)


def spindle():
    import numpy as np
    capture = ROOT / 'artifacts/spindle-fixed-20261116-18ec5e'
    common, report, output = start('spindle-fixed', capture, '7cce1244227399f598ff4e90e9621d10e7de2414', [])
    status = common.read(capture / 'window-status.json'); config = common.read(capture / 'configuration.json')
    assert config['IsolatedSpindleFixedComparison'] and config['NoLiveMonitoring']
    assert status['CapturedFixedLengthAccuracyPassed'] and status['ReferenceControlsPassed'] and status['ReferenceEnvironmentUnchanged']
    for key in ('LiveBrainAccessed', 'OldSavesLoaded', 'NativeRerun', 'MovingLengthAssessed', 'SlackAssessed',
                'AfferentFiringAssessed', 'PhysiologyQualified', 'AvatarRecoveryAssessed'):
        assert status[key] is False, key
    audit = common.module('frozen_fixed_spindle', capture / 'source/tools/analyse-spindle-fixed.py')
    audit.solve = audit.integrate = audit.analytic_controls = forbid_solve
    environment = audit.environment(capture)
    assert environment == common.read(capture / 'preparation-environment.json')
    assert environment == common.read(capture / 'reference-preflight.json')['environment']
    assert common.read(capture / 'reference-postflight.json') == dict(environment=environment, unchanged=True, modelExecuted=False)
    controls = common.read(capture / 'reference-controls/controls.json')
    assert controls['completed'] and controls['passed'] and not controls['physiologicalControls'] and len(controls['analytic']) == 6
    model = audit.native(capture, 'bag', 'quiet-hold', 1000)['model']
    for row in controls['analytic']:
        with np.load(capture / 'reference-controls' / (row['condition'] + '-' + row['setting']['label'] + '.npz'), allow_pickle=False) as saved:
            times = saved['times']; actual = saved['states']; assert np.array_equal(times, np.linspace(0, .01, 101))
            expected = np.tile(model.initial(), (101, 1))
            if row['condition'] == 'detachment-only':
                expected[:, 1:-2] = (.2 / 81) * np.exp(-times[:, None] * model.detach)
                expected[:, 0] = 1 - expected[:, 1:-2].sum(axis=1); expected[:, -1] = .4; expected[:, -2] = .6
            else:
                assert row['condition'] == 'actin-only'
                expected[:, -1] = model.overlap * np.tanh(model.on * 10**-6.5 * times); expected[:, -2] = 1 - expected[:, -1]
            assert np.array_equal(expected, saved['expected']) and actual.shape == expected.shape
            error = float(np.max(np.abs(actual - expected)))
            assert error == row['maximumStateError'] and error <= audit.CRITERIA['analyticStateAbsolute']
            assert np.isfinite(saved['internal_states']).all() and saved['internal_times'][0] == 0 and saved['internal_times'][-1] == .01
    with np.load(capture / 'reference-controls/jacobian.npz', allow_pickle=False) as saved:
        assert saved['approximate'].shape == saved['analytic'].shape == (84, 84)
        jacobian_error = float(np.max(np.abs(saved['approximate'] - saved['analytic'])))
        assert jacobian_error == controls['jacobianMaximumError'] and jacobian_error <= audit.CRITERIA['jacobianAbsolute']
    generated = {}
    def retain(path, value):
        assert path == capture / 'spindle-fixed-analysis.json' and not generated
        generated['analysis'] = value
    audit.save = retain  # Preserve the completed source capture byte for byte.
    print('Reproducing fixed-length comparisons from saved native/reference arrays...', flush=True)
    audit.audit(capture); analysis = generated['analysis']
    assert analysis == common.read(capture / 'spindle-fixed-analysis.json') and analysis['capturedFixedLengthAccuracyPassed']
    maxima = {category: {key: max(row[key] for row in analysis[category])
        for key in ('maximumStateDifference', 'maximumStressDifferencePa')}
        for category in ('referenceComparisons', 'candidateComparisons')}
    report.update(savedAnalysisExactlyReproduced=True, capturedFixedLengthAccuracyPassed=True,
        analyticControlsVerified=6, savedJacobianDiscrepancyVerified=jacobian_error,
        nativeTrajectories=18, independentReferences=18, referenceComparisons=18, candidateComparisons=54,
        maximumErrors=maxima, analysis=analysis, nativeRerun=False, movingLengthAssessed=False, slackAssessed=False,
        afferentFiringAssessed=False, liveBodyIntegrated=False)
    finish(output, report)


if __name__ == '__main__':
    if not __debug__: raise RuntimeError('Review assertions must remain enabled.')
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('capture', choices=('body', 'spindle'))
    parser.add_argument('--operator-reported-complete', required=True, action='store_true')
    args = parser.parse_args()
    (body if args.capture == 'body' else spindle)()

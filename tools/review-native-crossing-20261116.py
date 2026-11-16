"""Review the operator-reported native crossing gate; never execute models/tests."""
import argparse
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path
import shutil

ROOT = Path(__file__).resolve().parents[1]


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    value = importlib.util.module_from_spec(spec); spec.loader.exec_module(value)
    return value


def main():
    capture = ROOT / 'artifacts/native-crossing-20261116-0b35ae'
    output = ROOT / 'artifacts/native-crossing-review-20261116'
    assert not output.exists(), 'Preserve previous reviews.'
    common = module('native_crossing_integrity', ROOT / 'tools/review-olive-and-motor-20261109.py')
    review = common.verify(capture, ['test-binaries.json', 'probe-binaries.json'],
        '492c2a491340a7b1b7b3c29ce0464a5d095e850b')
    archive = module('native_crossing_archive', ROOT / 'tools/review-spindle-root-and-causal-motor-20261113.py')
    review['verifiedGitFiles'] = archive.source_archive(common, capture, review['sourceRevision'])
    gate = module('native_crossing_inventory', capture / 'source/tools/native_crossing_gate.py')
    tests = gate.gate.check_tests(capture, gate.COUNTS)
    assert tests['passedCases'] == 124
    status = common.read(capture / 'window-status.json'); config = common.read(capture / 'configuration.json')
    assert config['NoLiveMonitoring'] and config['IsolatedNativeCrossingGate']
    assert status['PassedFocusedTests'] == 124 and status['SuccessfulConditions'] == 5
    for key in ('NativeCrossingComponentChecksPassed', 'SourceAdapterCriteriaPassed', 'NeuronalModelExecutionConfirmed',
                'NeuronalModelExecutionAttempted', 'OriginalSourceExecutionAttempted', 'ManagedRuntimePreserved'):
        assert status[key] is True, key
    for key in ('LiveBrainAccessed', 'OldSavesLoaded', 'NativePhysicsExecuted', 'MuscleDynamicsExecuted',
                'IndependentCoupledReferenceQualified', 'PhysiologyQualified', 'AvatarRecoveryAssessed', 'FullUnitSuitePassed'):
        assert status[key] is False, key
    analysis = module('native_crossing_saved_source', capture / 'source/tools/analyse-observed-motor-source.py').audit(capture)
    assert analysis == common.read(capture / 'source-analysis.json')
    assert analysis['sourceAdapterCriteriaPassed'] and len(analysis['cases']) == 5
    before = common.read(capture / 'managed-runtime-before.json')
    assert before == common.read(capture / 'managed-runtime-after.json')
    assert common.digest(capture / 'managed-runtime-before.json') == common.digest(capture / 'managed-runtime-after.json')
    assert not before['modelsExecuted'] and not before['testsExecuted']
    provenance = common.read(capture / 'provenance.json'); h = gate.gate.helper()
    for prior in provenance['reviews'].values():
        assert prior['operatorReportedCompletion']
        assert h.digest(Path(prior['capture']) / 'backup-manifest.json') == prior['manifestSha256']
    baseline = Path(provenance['reviews']['membrane']['capture'])
    h.verify(baseline, ['source/' + p for p in provenance['preservedSourceFiles']])
    for name in provenance['preservedSourceFiles']:
        assert common.io(capture / 'source' / name).read_text(encoding='utf-8-sig') == common.io(baseline / 'source' / name).read_text(encoding='utf-8-sig')
    for copied in provenance['copied']:
        assert common.digest(capture / copied['path']) == common.digest(copied['original']) == copied['sha256']
    assert provenance['newModeIsOptional'] and not provenance['liveSelectionChanged']
    probe = capture / 'source/artifacts/native-crossing-source-probe/bin/NativeMotorBridgeProbe/release'
    binaries = common.read(capture / 'probe-binaries.json')
    assert binaries and (probe / 'NeuralResonanceEngine.DNNE.Tests.dll').is_file()
    for entry in binaries: Path(entry['Path']).relative_to(probe)
    review.update(reviewedUtc=datetime.now(timezone.utc).isoformat(), operatorReportedCompletion=True,
        componentGatePassed=True, passedTests=124, testInventory=tests, originalSourceConditions=5,
        savedSourceAnalysisExactlyReproduced=True, sourceAnalysis=analysis,
        qualifiedProbeDirectory=str(probe), qualifiedProbeFiles=len(binaries),
        managedRuntimeSha256=common.digest(capture / 'managed-runtime-before.json'),
        testsExecutedDuringReview=False, modelsExecutedDuringReview=False, originalBoundsChanged=False,
        fullReceptorNumericalMatrixQualified=False, presynapticNeuronalAuthorityQualified=False,
        physiologyQualified=False, avatarRecoveryAssessed=False)
    output.mkdir()
    (output / 'review.json').write_text(json.dumps(review, indent=2) + '\n', encoding='utf-8')
    shutil.copyfile(capture / 'managed-runtime-before.json', output / 'managed-runtime.json')
    print(json.dumps({k: review[k] for k in ('passedTests', 'originalSourceConditions', 'verifiedFilesPerCopy',
        'verifiedGitFiles', 'componentGatePassed', 'fullReceptorNumericalMatrixQualified')}, indent=2), flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--operator-reported-complete', action='store_true', required=True)
    parser.parse_args()
    if not __debug__: raise RuntimeError('Assertions must remain enabled.')
    main()

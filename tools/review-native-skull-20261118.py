"""Audit the operator-completed v4 skull capture without rerunning physics/tests."""
import argparse
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec); spec.loader.exec_module(result); return result


def main(reported):
    assert reported, 'Operator completion report required.'
    output = ROOT/'artifacts/native-skull-review-20261118'
    assert not output.exists()
    capture = ROOT/'artifacts/unity-skull-20261118-0ada47'
    common = module('skull_integrity', ROOT/'tools/review-olive-and-motor-20261109.py')
    archive = module('skull_source_integrity', ROOT/'tools/review-spindle-root-and-causal-motor-20261113.py')
    result = common.verify(capture, ['test-binaries.json', 'probe-binaries.json'],
                           'f1ee1845b236afcce8171473be0e6cd6c5108424')
    result['verifiedGitFiles'] = archive.source_archive(common, capture, result['sourceRevision'])
    assert common.digest(capture/'managed-runtime-before.json') == common.digest(capture/'managed-runtime-after.json')
    for row in common.read(capture/'player-files.json'):
        Path(row['Path']).relative_to(capture/'player')
        assert common.digest(row['Path']) == row['Sha256']
    source = common.io(capture/'source/UnityWorld')
    for path in source.rglob('*.cs'):
        assert common.digest(path) == common.digest(capture/'built-project'/path.relative_to(source))
    frozen = module('completed_native_skull_gate', capture/'source/tools/native_skull_gate.py')
    analysis = frozen.compare(capture)
    assert analysis == common.read(capture/'skull-analysis.json'), 'Saved analysis differs.'
    status = common.read(capture/'window-status.json')
    for key in ('ProducerAndTransportChecksPassed','UnityBuildSucceeded','PhysicsStudyAttempted',
                'UnityPhysicsChecksPassed','NativeHeadSamplingChecksPassed','ManagedRuntimePreserved','FrozenPackageUnchanged'):
        assert status[key] is True, key
    for key in ('LiveBrainAccessed','OldSavesLoaded','NeuronalModelExecuted','LiveUnityIntegrated','TimingMeasured',
                'PhysiologyQualified','AvatarRecoveryAssessed','FullUnitSuitePassed'):
        assert status[key] is False, key
    assert status['PassedFocusedTests'] == analysis['tests']['passedCases'] == 80
    assert status['ActualPhysicsSteps'] == status['DeliveredBodySamples'] == 201
    assert status['BodySampleCompleteThroughStep'] == analysis['completeThroughStep'] == 200
    result.update(reviewedUtc=datetime.now(timezone.utc).isoformat(), operatorReportedCompletion=True,
                  savedAnalysisExactlyReproduced=True, passedTests=80, analysis=analysis,
                  actualPhysicsSteps=201, deliveredBodySamples=201,
                  modelsExecutedDuringReview=False, testsExecutedDuringReview=False,
                  continuousMotionAccuracyQualified=False, physiologyQualified=False, avatarRecoveryAssessed=False)
    output.mkdir()
    (output/'review.json').write_text(json.dumps(result, indent=2)+'\n', encoding='utf-8')
    print(f"Verified {result['verifiedFilesPerCopy']} files per C:/D: copy; 80 tests, 201 steps, 603 receipts, "
          '33 scale controls and both real file-lock controls. Saved analysis reproduced exactly.', flush=True)


if __name__ == '__main__':
    if not __debug__: raise RuntimeError('Assertions must remain enabled.')
    parser = argparse.ArgumentParser(); parser.add_argument('--operator-reported-complete', action='store_true')
    main(parser.parse_args().operator_reported_complete)

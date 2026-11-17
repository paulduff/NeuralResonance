"""Review the operator-reported failed native head capture; never rerun physics."""
import argparse
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec); spec.loader.exec_module(result); return result


def main():
    capture = ROOT / 'artifacts/unity-skull-20261117-733e7c'
    output = ROOT / 'artifacts/native-skull-failure-review-20261117'; assert not output.exists()
    common = module('native_skull_review_integrity', ROOT / 'tools/review-olive-and-motor-20261109.py')
    print('Verifying failed native capture, source, binaries and both backup copies...', flush=True)
    review = common.verify(capture, ['test-binaries.json', 'probe-binaries.json'],
                           '8734689b82aee169dd346911e3bb354b0e8dcda8', expected_status='FAILED', expected_exit=1)
    archive = module('native_skull_review_archive', ROOT / 'tools/review-spindle-root-and-causal-motor-20261113.py')
    review['verifiedGitFiles'] = archive.source_archive(common, capture, review['sourceRevision'])
    player = common.read(capture / 'player-files.json')
    for entry in player: assert common.digest(entry['Path']) == entry['Sha256']
    for path in common.io(capture / 'source/UnityWorld').rglob('*.cs'):
        relative = path.relative_to(common.io(capture / 'source/UnityWorld'))
        assert common.digest(path) == common.digest(capture / 'built-project' / relative)
    gate = module('native_skull_frozen_gate', capture / 'source/tools/native_skull_gate.py')
    tests = gate.gate.check_tests(capture, gate.COUNTS); managed = gate.runtime(capture)
    assert common.digest(capture / 'managed-runtime-before.json') == common.digest(capture / 'managed-runtime-after.json')
    status = common.read(capture / 'window-status.json'); result = common.read(capture / 'study/study-result.json')
    assert status['UnityBuildSucceeded'] and status['FrozenPackageUnchanged'] and status['ManagedRuntimePreserved']
    assert status['PassedFocusedTests'] == 74 and not status['UnityPhysicsChecksPassed']
    assert not result['completed'] and result['exitCode'] == 1 and result['dedicatedPlayerStoppedOnFailure']
    directory = capture / 'study/bridge'; failure = common.read(directory / 'failure.json')
    assert failure['physicsStep'] == 178 and failure['expectedSequence'] == 181
    assert 'Head point capture requires an unscaled physical frame.' in failure['error']
    request = common.read(directory / 'request-000181.json')
    assert request['operation'] == 'step' and request['expectedStep'] == 177
    assert not (directory / 'reply-000181.json').exists() and not (directory / 'skull-000181.json').exists()
    assert len(list(directory.glob('request-*.json'))) == 181
    maximum_scale_error = 0.; worst = None; steps = []; maximum_velocity_error = maximum_position_error = maximum_basis_error = 0.
    for index, path in enumerate(sorted(directory.glob('skull-*.json'))):
        assert path.name == f'skull-{index:06}.json'
        source = common.read(path); body = common.read(directory / ('hello.json' if index == 0 else f'reply-{index:06}.json'))
        assert source['sequence'] == index and source['physicsStep'] == body['physicsStep'] and source['operation'] == body['operation']
        assert source['sessionId'] == body['sessionId'] == failure['sessionId']
        assert body['paused'] and body['isolated'] and not body['connected'] and body['finite']
        if source['operation'] in ('hello', 'step'): steps.append(source['physicsStep'])
        for snapshot in source['samples']:
            v = {key: gate.vector(snapshot[key]) for key in gate.VECTORS}
            error = sum((x-1)**2 for x in v['scale']); assert error <= 1e-12
            if error > maximum_scale_error: maximum_scale_error = error; worst = dict(step=source['physicsStep'], scale=snapshot['scale'])
            omega = v['angularVelocityWorldRadiansPerSecond']
            rotational = gate.cross(omega, gate.subtract(v['pointWorldMeters'], v['centerOfMassWorldMeters']))
            expected = [a+b for a,b in zip(v['centerOfMassVelocityWorldMetersPerSecond'], rotational, strict=True)]
            velocity_error = gate.maximum(gate.subtract(expected, v['pointVelocityWorldMetersPerSecond']))
            maximum_velocity_error = max(maximum_velocity_error, velocity_error)
            x,y,z,w = gate.vector(snapshot['rotation'], ('x','y','z','w'))
            axes = [[1-2*(y*y+z*z),2*(x*y+z*w),2*(x*z-y*w)],
                    [2*(x*y-z*w),1-2*(x*x+z*z),2*(y*z+x*w)],
                    [2*(x*z+y*w),2*(y*z-x*w),1-2*(x*x+y*y)]]
            position = [v['originWorldMeters'][i]+sum(axes[j][i]*v['localPointMeters'][j] for j in range(3)) for i in range(3)]
            maximum_position_error = max(maximum_position_error, gate.maximum(gate.subtract(position,v['pointWorldMeters'])))
            maximum_basis_error = max(maximum_basis_error, *(gate.maximum(gate.subtract(a,v[k])) for a,k in zip(axes,('rightWorld','upWorld','forwardWorld'),strict=True)))
    assert index+1 == 181 and steps == list(range(178))
    receipts = sorted((capture / 'study').glob('skull-receipts-*.json')); assert len(receipts) == 178
    for step, path in enumerate(receipts):
        rows = common.read(path); assert len(rows) == 3
        for row in rows:
            receipt = row['receipt']; assert receipt['physicsStep'] == step
            raw = common.io(capture / 'study' / row['sourceFile']).read_text(encoding='utf-8-sig')
            assert row['sourceSha256'] == gate.digest(raw)
            assert receipt['rawObservationSha256'] == gate.digest(receipt['rawObservationJson'])
    review.update(reviewedUtc=datetime.now(timezone.utc).isoformat(), operatorReportedCompletion=True,
                  passedTests=74, tests=tests, managedRuntime=managed, verifiedPlayerFiles=len(player),
                  capturedEndpoints=178, pointReceipts=534, sidecars=181, failedPhysicsStep=178,
                  lastAcknowledgedPhysicsStep=177, failingScaleWasSaved=False,
                  maximumRetainedScaleSquaredDeviation=maximum_scale_error, worstRetainedScale=worst,
                  maximumRetainedPointVelocityError=maximum_velocity_error, maximumRetainedPointGeometryError=maximum_position_error,
                  maximumRetainedBasisError=maximum_basis_error, originalBoundsChanged=False,
                  nativeSamplingQualified=False, physiologicalCauseEstablished=False,
                  modelsExecutedDuringReview=False, testsExecutedDuringReview=False, avatarRecoveryAssessed=False)
    output.mkdir(); (output / 'review.json').write_text(json.dumps(review,indent=2)+'\n',encoding='utf-8')
    print(json.dumps({k:review[k] for k in ('verifiedFilesPerCopy','passedTests','capturedEndpoints','failedPhysicsStep',
                     'maximumRetainedScaleSquaredDeviation','worstRetainedScale','maximumRetainedPointVelocityError',
                     'maximumRetainedPointGeometryError','maximumRetainedBasisError')},indent=2),flush=True)


if __name__ == '__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--operator-reported-complete',action='store_true');args=parser.parse_args()
    if not args.operator_reported_complete: raise RuntimeError('Wait for the operator report.')
    if not __debug__: raise RuntimeError('Assertions must remain enabled.')
    main()

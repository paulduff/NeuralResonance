"""Review completed native evidence; no model, test or force analysis rerun."""
import argparse
from datetime import datetime, timezone
import hashlib
import importlib.util
import json
import math
from pathlib import Path
import shutil
import subprocess

ROOT = Path(__file__).resolve().parents[1]
CAPTURE = Path('D:/DNNE-build-work/avatar-tendons-20261123-bb320d')
REVISION = '218054afac3165edbb1cac7fdd0a98629d786f95'


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec); spec.loader.exec_module(result)
    return result


def review(package):
    assert package == CAPTURE
    output = ROOT/'artifacts'/(package.name+'-review')
    assert not output.exists(), 'Preserve previous reviews.'
    common = module('tendon_saved_files', ROOT/'tools/review-olive-and-motor-20261109.py')
    archive = module('tendon_saved_git', ROOT/'tools/review-spindle-root-and-causal-motor-20261113.py')
    gate = module('tendon_saved_gate', package/'source/tools/native_avatar_tendon_study.py')
    result = common.verify(package, ['test-binaries.json'], REVISION, progress_every=1000)
    result['verifiedGitFiles'] = archive.source_archive(common, package, REVISION)
    cfg = common.read(package/'configuration.json'); status = common.read(package/'window-status.json')
    assert not subprocess.check_output(['git', 'diff', '--name-only', gate.BASE, REVISION, '--', 'src', 'tests', 'Structures'], cwd=ROOT).strip()
    gate.preflight(package)  # Frozen input/executable/stdlib hashes only.
    gate.check_tests(package)  # Read exact completed TRX inventory; no execution.
    assert gate.inventory() == cfg['ExpectedTestMethods']
    assert status['PassedFocusedTests'] == 112
    for key in ('NoLiveMonitoring', 'NativePhysicsAttempted', 'MechanicalChecksPassed', 'UnityBuildSucceeded'):
        assert status[key] is True, key
    for key in ('NeuronalSourceExecuted', 'AnatomicalCalibrationQualified', 'AvatarBalanceQualified'):
        assert status[key] is False, key

    # Do not rescan today's external .NET installation: the frozen runner checks
    # its captured runtime list before successful completion and in finally.
    # The full saved manifest itself is protected by the verified package bytes.
    runtime = common.read(package/'managed-runtime-files.json')
    assert len(runtime) == len({x['Path'] for x in runtime}) == 3099
    player = common.read(package/'player-files.json')
    player_names = set()
    for entry in player:
        path = Path(entry['Path']); relative = path.relative_to(package/'player')
        assert relative not in player_names
        player_names.add(relative)
        assert common.digest(path) == entry['Sha256']
    assert player_names == {p.relative_to(package/'player') for p in (package/'player').rglob('*') if p.is_file()}

    analysis = common.read(package/'analysis.json')
    assert analysis['completed'] and analysis['mechanicalCriteriaPassed'] and analysis['syntheticAttachments']
    for key in ('independentCoupledTrajectoryQualified', 'neuronalSourceExecuted', 'physiologyQualified', 'avatarBalanceQualified'):
        assert analysis[key] is False, key
    assert 0 <= analysis['maximumForceErrorNewtons'] <= cfg['Limits']['forceAbsoluteNewtons']
    summaries = []; baselines = {}; endpoints = {}; guards = []; endpoint_count = 0
    for case in cfg['Cases']:
        folder = package/'native'/case['name']; saved = common.read(folder/'result.json')
        assert saved['completed'] and saved['passed'] and not saved['error']
        assert (saved['condition'], saved['muscle']) == (case['condition'], case['muscle'])
        assert saved['unity'] == '6000.3.19f1' and saved['links'] == 15 and saved['paths'] == 38
        assert not any(saved[k] for k in ('neuronalSourceExecuted', 'anatomicalCalibrationQualified', 'avatarBalanceQualified'))
        expected = 100 if case['condition'] == 'freefall' else 1 if case['condition'] == 'reject-repeat' else 0 if case['condition'].startswith('reject-') else 8
        records = [json.loads(line) for line in (folder/'endpoints.jsonl').read_text(encoding='utf-8-sig').splitlines()]
        assert saved['finalStep'] == expected and len(records) == expected+1
        dt = gate.real(saved['stepMsBits'])
        for index, record in enumerate(records):
            assert record['step'] == index and gate.real(record['physicalTimeMsBits']) == index*dt
            assert hashlib.sha256(record['stateJson'].encode()).hexdigest().upper() == record['stateHash']
            state = json.loads(record['stateJson'])
            assert state['physicsStep'] == index and len(state['links']) == 15 and len(record['paths']) == 38
            if index: assert len(json.loads(record['applicationsJson'])['points']) == 76
        if case['condition'].startswith('reject-'):
            assert saved['rejectedWithoutStep'] and saved['faultLatchPassed'] and saved['rejectedError']
            assert saved['beforeRejectedState'] == saved['afterRejectedState']
            guards.append(dict(name=case['name'], unchangedBody=True, faultLatched=True, completedSteps=expected))
        baselines[case['name']] = records[0]['stateHash']
        endpoints[case['name']] = json.loads(records[-1]['stateJson'])['links']
        endpoint_count += len(records)
        summaries.append(dict(name=case['name'], steps=expected, passed=True))
    assert analysis['cases'] == summaries and len(summaries) == 51
    assert sum(r['steps'] for r in summaries) == analysis['actualPhysicsSteps'] == 413 and endpoint_count == 464
    assert len(guards) == 11
    responses = analysis['comparisonToQuiet']
    pulses = [c for c in cfg['Cases'] if c['condition'] == 'pulse']
    assert len(responses) == len(pulses) == 38
    # Reconcile only the saved endpoint comparison, not force envelopes or any
    # trajectory solver. Retain the original 1e-8 m observation threshold.
    for response, case in zip(responses, pulses, strict=True):
        assert response['muscle'] == case['muscle'] and baselines[case['name']] == baselines['quiet']
        difference = max(abs(a-b) for left, right in zip(endpoints[case['name']], endpoints['quiet'], strict=True)
                         for a,b in zip(gate.vec(left['positionBits']), gate.vec(right['positionBits']), strict=True))
        assert math.isfinite(difference) and response['maximumPositionDifferenceMeters'] == difference
        assert response['observed'] == (difference > cfg['Limits']['responseObservationMeters'])
    freefall = common.read(package/'native/freefall/result.json')
    fall = {k: freefall[k] for k in ('initialPelvisHeight', 'finalPelvisHeight', 'peakFootForce', 'finalStep')}
    assert fall['initialPelvisHeight']-fall['finalPelvisHeight'] > .02 and fall['peakFootForce'] > 0
    result.update(reviewedUtc=datetime.now(timezone.utc).isoformat(), operatorReportedCompletion=True,
        passedTests=112, exactTestInventory=True, verifiedPlayerFiles=len(player), savedRuntimeFiles=len(runtime),
        runtimePreservationVerifiedByFrozenRunner=True, externalManagedRuntimeRehashedDuringReview=False,
        originalCanonicalSourceUnchanged=True, nativeCases=51, physicalSteps=413, endpointRecords=464,
        rejectedCommands=guards, forceAccountingPassed=True, fullForceAnalysisRecomputedDuringReview=False,
        maximumReportedForceErrorNewtons=analysis['maximumForceErrorNewtons'],
        positionResponsesAboveOriginalThreshold=sum(r['observed'] for r in responses), positionResponses=responses,
        maximumPositionDifferenceMeters=max(r['maximumPositionDifferenceMeters'] for r in responses), freefall=fall,
        analysisSha256=common.digest(package/'analysis.json'), syntheticAttachments=True,
        testsExecutedDuringReview=False, modelsExecutedDuringReview=False,
        usefulAllAxisControlQualified=False, independentCoupledTrajectoryQualified=False,
        anatomicalCalibrationQualified=False, neuronalAuthorityQualified=False, avatarBalanceQualified=False)
    output.mkdir()
    (output/'review.json').write_text(json.dumps(result, indent=2, allow_nan=False)+'\n', encoding='utf-8')
    shutil.copyfile(package/'analysis.json', output/'analysis.json')
    shutil.copyfile(package/'window-status.json', output/'window-status.json')
    shutil.copyfile(__file__, output/Path(__file__).name)
    print(json.dumps(dict(output=str(output), verifiedFiles=result['verifiedFilesPerCopy'], tests=112, cases=51,
        steps=413, positionResponses=result['positionResponsesAboveOriginalThreshold'],
        maximumPositionDifferenceMeters=result['maximumPositionDifferenceMeters']), indent=2), flush=True)


if __name__ == '__main__':
    if not __debug__: raise RuntimeError('Assertions must remain enabled.')
    parser = argparse.ArgumentParser(); parser.add_argument('package', type=Path)
    parser.add_argument('--operator-reported-complete', action='store_true'); args = parser.parse_args()
    assert args.operator_reported_complete
    review(args.package.resolve())

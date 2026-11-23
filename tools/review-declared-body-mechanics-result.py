"""Audit an operator-completed mechanics run; no test, physics or solver rerun."""
import argparse
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path
import shutil

ROOT = Path(__file__).resolve().parents[1]
CAPTURE = Path('D:/DNNE-build-work/declared-body-mechanics-20261123-203560')
REVISION = 'abec8aab441815fed37a800fd94f98739737f572'


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    value = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(value)
    return value


def review(package):
    assert package == CAPTURE
    output = ROOT/'artifacts'/(package.name+'-review')
    assert not output.exists()
    common = module('declared_saved_files', ROOT/'tools/review-olive-and-motor-20261109.py')
    archive = module('declared_saved_archive', ROOT/'tools/review-spindle-root-and-causal-motor-20261113.py')
    gate = module('declared_saved_gate', package/'source/tools/native_body_mechanics_study.py')
    result = common.verify(package, ['test-binaries.json', 'probe-binaries.json'], REVISION, progress_every=1000)
    result['verifiedGitFiles'] = archive.source_archive(common, package, REVISION)
    gate.preflight(package)
    gate.check_tests(package)  # Read the completed TRX inventory, not a test invocation.
    gate.h.runtime(package)
    status = common.read(package/'window-status.json')
    analysis = common.read(package/'analysis.json')
    for key in ('NoLiveMonitoring', 'NativeExecutionAttempted', 'NativeHeadDeliveryChecksPassed',
                'TestExecutionAttempted', 'UnityBuildSucceeded', 'DeclaredMechanicsChecksPassed',
                'FrozenInputsPreserved', 'ManagedRuntimePreserved'):
        assert status[key] is True, key
    assert status['PassedFocusedTests'] == status['RequiredComponentCases'] == 341
    for key in ('LiveBrainAccessed', 'OldSavesLoaded', 'NeuronalSourceExecuted', 'MuscleCellExecuted',
                'PhysiologicalFiringQualified', 'AvatarBalanceQualified'):
        assert status[key] is False, key
    assert common.digest(package/'managed-runtime-before.json') == common.digest(package/'managed-runtime-after.json')
    runtime = common.read(package/'managed-runtime-before.json')
    assert len(runtime['directories']) == 2 and sum(len(d['files']) for d in runtime['directories']) == 335
    for entry in common.read(package/'player-files.json'):
        path = Path(entry['Path']); path.relative_to(package)
        assert common.digest(path) == entry['Sha256']
    assert analysis['modes'] == list(gate.MODES)
    for key in ('completed', 'declaredMechanicsChecksPassed', 'existingJointLimitGuardPassed',
                'originalAndNoFrictionQuietReplaysIdentical', 'inspectionPreserved'):
        assert analysis[key] is True, key
    for key in ('neuronalSourceExecuted', 'muscleCellExecuted', 'physiologicalFiringQualified', 'avatarBalanceQualified'):
        assert analysis[key] is False, key
    assert [analysis[k] for k in ('passedComponentCases', 'nativeCases', 'actualPhysicsSteps', 'physicalEndpoints',
            'headReceipts', 'extraInspections', 'mechanicalSettingReceipts', 'derivedChecks', 'jointLimitObservations')] == [341, 12, 96, 108, 324, 108, 240, 2592, 2052]
    angles = common.read(package/'joint-limit-observations.json')
    assert len(angles) == len({(r['mode'], r['case'], r['step'], r['joint'], r['axis']) for r in angles}) == 2052
    assert all(r['passed'] and 0 <= r['violationRadians'] < .05000000074505806 for r in angles)
    assert max(r['violationRadians'] for r in angles) == analysis['maximumJointLimitViolationRadians']
    settings = head_count = inspections = checks = 0
    fractions = []
    for mode in gate.MODES:
        folder = package/'conditions'/mode
        saved = common.read(folder/'analysis.json')
        for key in ('completed', 'nativeHeadDeliveryChecksPassed', 'originalQuietEndpointsIdentical', 'nonmovingInspectionPreserved', 'derivedChecksPassed'):
            assert saved[key] is True, key
        ledger = common.read(folder/'derived-observations.json')
        assert len(ledger) == len({r['label'] for r in ledger}) == 1296
        assert all(r['passed'] and 0 <= r['error'] <= r['allowance'] for r in ledger)
        fraction = max(r['error']/r['allowance'] for r in ledger)
        assert fraction == saved['maximumDerivedAllowanceFraction']
        fractions.append(fraction); checks += len(ledger)
        acknowledged = {}
        for name, _, repeats in gate.h.CASES:
            case = folder/'native'/name
            config = common.read(case/'input.json')
            assert config['protocol'] == 'full-avatar-tendon-wire-v3' and config['mechanicsMode'] == mode
            ack = gate.h.rows(case/'acknowledged.jsonl')
            heads = gate.h.rows(case/'heads.jsonl')
            inspected = gate.h.rows(case/'inspections.jsonl')
            assert len(ack) == len(heads) == 9 and len(inspected) == repeats*9
            wire = case/'tendon-bridge'
            replies = [common.read(wire/'hello.json')] + [common.read(p) for p in sorted(wire.glob('reply-*.json'))]
            assert len(replies) == 11+repeats*9 and [r['sequence'] for r in replies] == list(range(len(replies)))
            assert len(list(wire.glob('request-*.json'))) == len(replies)-1
            assert replies[-1]['operation'] == 'stop' and replies[-1]['physicsStep'] == 8
            provenance = replies[0]['mechanicsDiagnosticJson']
            gate.mechanical.settings(json.loads(provenance), mode)
            assert all(r['mechanicsDiagnosticJson'] == provenance for r in replies)
            settings += len(replies)
            for step, (a, frame) in enumerate(zip(ack, heads, strict=True)):
                assert a == replies[a['sequence']] and a['physicsStep'] == frame['physicsStep'] == step
                assert gate.h.sha(a['stateJson']) == a['stateHash'] == frame['bodyStateSha256']
                assert gate.h.sha(a['observationsJson']) == a['observationsHash'] == frame['observationsSha256']
                assert a['observationsJson'] == frame['originalObservationsJson']
                native = json.loads(a['observationsJson'])
                assert len(native['contactsJson']) == 15 and len(native['skullJson']) == len(frame['samples']) == 3
                for sample, raw in zip(frame['samples'], native['skullJson'], strict=True):
                    assert sample['originalNativeJson'] == raw and sample['nativeSha256'] == gate.h.sha(raw)
                    receipt = sample['kinematics']
                    assert receipt['rawObservationSha256'] == gate.h.sha(receipt['rawObservationJson'])
                    assert receipt['physicsStep'] == step and (receipt['vestibularInput'] is None) == (step == 0)
                    head_count += 1
            for a in inspected:
                assert a == replies[a['sequence']] and a['operation'] == 'inspect'
                assert all(a[k] == ack[a['physicsStep']][k] for k in ('stateJson', 'observationsJson', 'applicationsJson', 'paths'))
                inspections += 1
            acknowledged[name] = ack
        original = gate.h.rows(package/'friction-evidence/native'/(mode+'__quiet')/'endpoints.jsonl')
        assert all(a['stateJson'] == b['stateJson'] for a, b in zip(acknowledged['quiet'], original, strict=True))
        for name in ('quiet', 'hip', 'shoulder'):
            for a, b in zip(acknowledged[name], acknowledged[name+'-inspected'], strict=True):
                assert all(a[k] == b[k] for k in ('geometryJson', 'stateJson', 'paths', 'applicationsJson'))
                assert json.loads(a['observationsJson'])['skullJson'] == json.loads(b['observationsJson'])['skullJson']
    assert (settings, head_count, inspections, checks) == (240, 324, 108, 2592)
    result.update(reviewedUtc=datetime.now(timezone.utc).isoformat(), operatorReportedCompletion=True,
        passedComponentCases=341, nativeCases=12, physicalSteps=96, physicalEndpoints=108, headReceipts=324,
        extraInspections=108, mechanicalSettingReceipts=240, derivedChecks=2592, jointLimitObservations=2052,
        maximumDerivedAllowanceFraction=max(fractions), maximumJointLimitViolationRadians=analysis['maximumJointLimitViolationRadians'],
        exactOriginalQuietReplays=True, inspectionPreserved=True, selectedManagedFiles=335, runtimeBeforeAfterIdentical=True,
        fullDerivedAnalysisRecomputedDuringReview=False, testsExecutedDuringReview=False, modelsExecutedDuringReview=False,
        physiologicalFiringQualified=False, neuronalAuthorityQualified=False, avatarBalanceQualified=False)
    output.mkdir()
    (output/'review.json').write_text(json.dumps(result, indent=2, allow_nan=False)+'\n', encoding='utf-8')
    for name in ('analysis.json', 'window-status.json'):
        shutil.copyfile(package/name, output/name)
    shutil.copyfile(__file__, output/Path(__file__).name)
    print(json.dumps(dict(output=str(output), verifiedFiles=result['verifiedFilesPerCopy'], passedCases=341,
                         nativeCases=12, headReceipts=324, maximumDerivedAllowanceFraction=max(fractions)), indent=2), flush=True)


if __name__ == '__main__':
    if not __debug__:
        raise RuntimeError('Assertions must remain enabled.')
    parser = argparse.ArgumentParser()
    parser.add_argument('package', type=Path)
    parser.add_argument('--operator-reported-complete', action='store_true')
    args = parser.parse_args()
    assert args.operator_reported_complete
    review(args.package.resolve())

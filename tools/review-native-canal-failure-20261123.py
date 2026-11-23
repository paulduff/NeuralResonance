"""Read-only audit of Paul's completed native/sensory failure; no trajectory replay."""
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path
import shutil
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT/'tools'))
PACKAGE = Path('D:/DNNE-build-work/native-canal-20261123-184323')
REVISION = 'f851ee1be3ac0e0c708c9e8db5f82ff4d08f22d0'


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec); spec.loader.exec_module(result); return result


def review():
    output = ROOT/'artifacts'/(PACKAGE.name+'-review'); assert not output.exists()
    common = module('native_canal_failure_files', ROOT/'tools/review-olive-and-motor-20261109.py')
    archives = module('native_canal_failure_source', ROOT/'tools/review-spindle-root-and-causal-motor-20261113.py')
    result = common.verify(PACKAGE, ['probe-binaries.json'], REVISION, 'FAILED', 1, progress_every=1000)
    result['verifiedGitFiles'] = archives.source_archive(common, PACKAGE, REVISION)
    status = common.read(PACKAGE/'window-status.json')
    for key in ('NoLiveMonitoring', 'NativeExecutionAttempted', 'SensoryExecutionAttempted', 'FrozenInputsPreserved', 'ManagedRuntimePreserved'):
        assert status[key] is True
    for key in ('LiveBrainAccessed', 'OldSavesLoaded', 'NeuronalMotorAuthority', 'MuscleCellExecuted', 'PhysiologicalFiringQualified',
                'AvatarStandingQualified', 'IndependentConnectedAccuracyQualified', 'ConnectedAccountingPassed'):
        assert status[key] is False
    assert common.digest(PACKAGE/'managed-runtime-before.json') == common.digest(PACKAGE/'managed-runtime-after.json')
    error = (PACKAGE/'compare.log').read_text(encoding='utf-8-sig')
    assert "all(a[k] == old[k] for k in ('geometryJson', 'stateJson', 'paths', 'applicationsJson'))" in error
    assert 'AssertionError' in error and not (PACKAGE/'analysis.json').exists()
    # Exact saved values only; leave the corrected geometry/trajectory checks
    # for the independent visible PowerShell study, including negative controls.
    cases = []; operands = None
    def rows(path):
        return [json.loads(line) for line in path.read_text(encoding='utf-8-sig').splitlines()]
    for condition in ('quiet', 'hip', 'shoulder'):
        original = rows(PACKAGE/'native-evidence/conditions/no-joint-friction/native'/condition/'acknowledged.jsonl')
        for count in (8192, 16384):
            folder = PACKAGE/'native'/f'{condition}-n{count}'
            final = common.read(folder/'result.json'); current = rows(folder/'acknowledged.jsonl')
            assert final['completed'] and len(current) == len(original) == 9
            assert final['sensorySteps'] == 7*count and final['packets'] == 7*count//512
            fields = [{key: a[key] == b[key] for key in ('geometryJson', 'stateJson', 'paths', 'applicationsJson')}
                      for a, b in zip(current, original, strict=True)]
            assert all(r == dict(geometryJson=False, stateJson=True, paths=False, applicationsJson=True) for r in fields)
            cases.append(dict(case=folder.name, completed=True, sourceSteps=final['sensorySteps'], packets=final['packets'],
                              recordedEvents=final['events'], rawEndpointFieldEquality=fields))
            if operands is None: operands = dict(current=current[0], original=original[0])
    result.update(reviewedUtc=datetime.now(timezone.utc).isoformat(), operatorReportedCompletion=True,
        originalFailurePreserved=True, originalLimitsChanged=False, completedCases=6, sourceSteps=516096,
        packets=1008, cases=cases, recordedEvents=sum(c['recordedEvents'] for c in cases),
        failure='Cross-session geometry and derived path identity comparison; body states and point forces exact.',
        correctedComparisonExecuted=False, testsExecutedDuringReview=False, modelsExecutedDuringReview=False,
        connectedAccountingQualified=False, physiologicalFiringQualified=False, avatarStandingQualified=False)
    output.mkdir()
    for name, value in [('review.json', result), ('failure-operands.json', operands)]:
        (output/name).write_text(json.dumps(value, indent=2, allow_nan=False)+'\n', encoding='utf-8')
    for name in ('compare.log', 'window-status.json'): shutil.copyfile(PACKAGE/name, output/name)
    shutil.copyfile(__file__, output/Path(__file__).name)
    print(json.dumps(dict(output=str(output), verifiedFiles=result['verifiedFilesPerCopy'], completedCases=6,
                         recordedEvents=result['recordedEvents'], correctedComparisonExecuted=False)), flush=True)


if __name__ == '__main__':
    assert '--operator-reported-complete' in sys.argv
    if not __debug__: raise RuntimeError('Assertions must remain enabled.')
    review()

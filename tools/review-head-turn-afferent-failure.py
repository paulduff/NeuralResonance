"""Review the reported capture failure without replaying numerical analysis."""
import argparse
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path
import shutil
import sys

ROOT = Path(__file__).resolve().parents[1]
CAPTURE = Path('D:/DNNE-build-work/head-turn-afferent-20261120-2e0ed3')
REVISION = '5f3887d489c36b67012700bc96012b195f9fc686'


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec); spec.loader.exec_module(result)
    return result


def review(package):
    assert package == CAPTURE
    output = ROOT/'artifacts'/(package.name+'-review')
    assert not output.exists(), 'Preserve earlier reviews.'
    common = module('head_files', ROOT/'tools/review-olive-and-motor-20261109.py')
    archive = module('head_git', ROOT/'tools/review-spindle-root-and-causal-motor-20261113.py')
    result = common.verify(package, ['test-binaries.json','probe-binaries.json'], REVISION,
        expected_status='FAILED', expected_exit=1, progress_every=1000)
    result['verifiedGitFiles'] = archive.source_archive(common, package, REVISION)
    status = common.read(package/'window-status.json')
    for key in ('ComponentChecksPassed','AnalyticControlsPassed','NativeCaptureAttempted','QualifiedDependenciesPreserved',
                'ManagedRuntimePreserved','FrozenPackageUnchanged','PythonRuntimeChanged','NumericalLibrariesByteUnchanged'):
        assert status[key] is True, key
    for key in ('ConnectedAccountingPassed','IndependentCoupledTrajectoryQualified','AfferentFiringQualified',
                'LiveBrainAccessed','OldSavesLoaded','NativeBodyExecuted','PhysiologyQualified','AvatarRecoveryAssessed'):
        assert status[key] is False, key
    assert common.digest(package/'managed-runtime-before.json') == common.digest(package/'managed-runtime-after.json')
    sys.path.insert(0,str(package/'source/tools'))
    try:
        gate = module('head_original_gate',package/'source/tools/head_turn_afferent_gate.py')
        gate.runtime(package)
        tests = gate.test_evidence(package)
        assert tests['passedCases'] == status['PassedFocusedTests'] == 212
        assert tests == common.read(package/'test-analysis.json')['tests']
        analytic = common.read(package/'analytic-controls.json')
        assert analytic == dict(completed=True,passed=True,cases=32,methods=gate.control_methods(package/'source'),
            failures=[],errors=[],skipped=[])
    finally:
        sys.path.pop(0)
    log = (package/'reference-analyse.log').read_text(encoding='utf-8-sig')
    assert "released['heldInput']['skull']['rawObservationJson']" in log and 'AssertionError' in log
    assert not (package/'analysis.json').exists()
    saved = common.read(package/'study/results.json')
    assert saved['completed'] and len(saved['runs']) == 6
    summaries = []
    for case in saved['runs']:
        assert case['completed'] and case['failure'] is None
        folder = package/'study'/f"{case['condition']}-n{case['count']}"
        assert common.read(folder/'final.json') == case
        summaries.append({k:case[k] for k in ('condition','count','steps','intervals','packets','events')})
    assert sum(r['steps'] for r in summaries) == 2654208
    # Save the precise first failing operands. No full accounting or new model.
    folder = package/'study/quiet-n16384'
    mechanical = common.read(folder/'mechanical-02.json')['mechanics']['completedInterval']
    head = common.read(folder/'head-input.json')
    operands = dict(originalRawJson=mechanical['heldInput']['skull']['rawObservationJson'],
        expected=head['observations'][1])
    assert json.loads(operands['originalRawJson']) != operands['expected']
    result.update(reviewedUtc=datetime.now(timezone.utc).isoformat(),operatorReportedCompletion=True,
        originalFailurePreserved=True,passedTests=212,passedAnalyticControls=32,exactTestInventory=True,
        nativeSummaries=summaries,allNativeCapturesReportedComplete=True,fullAccountingRecomputed=False,
        modelsExecutedDuringReview=False,testsExecutedDuringReview=False,independentTrajectoryAccuracyQualified=False,
        afferentFiringQualified=False,physiologicalCalibrationQualified=False,avatarBalanceAssessed=False)
    output.mkdir()
    for name,value in [('review.json',result),('failure-operands.json',operands)]:
        (output/name).write_text(json.dumps(value,indent=2,allow_nan=False)+'\n',encoding='utf-8')
    shutil.copyfile(package/'window-status.json',output/'failure.json')
    shutil.copyfile(__file__,output/Path(__file__).name)
    print(json.dumps(dict(output=str(output),verifiedFiles=result['verifiedFilesPerCopy'],nativeSummaries=summaries),indent=2),flush=True)


if __name__ == '__main__':
    if not __debug__: raise RuntimeError('Assertions must remain enabled.')
    parser=argparse.ArgumentParser();parser.add_argument('package',type=Path)
    parser.add_argument('--operator-reported-complete',action='store_true');args=parser.parse_args()
    if not args.operator_reported_complete: parser.error('Require the final operator report.')
    review(args.package.resolve())

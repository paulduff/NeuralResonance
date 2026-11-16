"""Review the reported partial force-landscape capture without rerunning searches."""
import argparse
from collections import Counter
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]


def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path); value=importlib.util.module_from_spec(spec)
    spec.loader.exec_module(value); return value


def main():
    common=module('reported_spindle_integrity',ROOT/'tools/review-olive-and-motor-20261109.py')
    capture=ROOT/'artifacts/spindle-slack-roots-20261116-527839'
    output=ROOT/'artifacts/spindle-slack-review-20261116'; assert not output.exists()
    result=common.verify(capture,[],'cd5b775a4090f18ced1697ae025c7b71fd1f51ea','FAILED',1)
    archive=module('spindle_source_archive',ROOT/'tools/review-spindle-root-and-causal-motor-20261113.py')
    result['verifiedGitFiles']=archive.source_archive(common,capture,result['sourceRevision'])
    environment=module('spindle_dependencies',capture/'source/tools/golgi-reference-resume.py').environment()
    assert environment==common.read(capture/'preparation-environment.json')
    assert environment==common.read(capture/'reference-preflight.json')['environment']
    assert common.read(capture/'reference-postflight.json')==dict(environment=environment,unchanged=True,modelExecuted=False)
    controls=common.read(capture/'controls/controls.json')
    expected={'bag-passive','chain-passive','bag-passive-load','bag-passive-knot',
              'bag-central-bin-three-roots','bag-edge-bin-discontinuous-roots'}
    assert controls['completed'] and controls['passed'] and {r['label'] for r in controls['cases']}==expected
    for control in controls['cases']:
        row=common.read(capture/'controls'/(control['label']+'.json'))
        assert control['passed'] and row['passed'] and row['result']['passed']
        assert len(row['expectedRootsNm'])==len(row['result']['roots'])
        assert all(abs(a-b['lengthNm'])<=1e-9 for a,b in zip(row['expectedRootsNm'],row['result']['roots']))
    analysis=common.read(capture/'spindle-slack-analysis.json')
    assert not analysis['completed'] and not analysis['forceLandscapeNumericsPassed'] and analysis['statesChecked']==1556
    assert len(analysis['trajectories'])==8 and sum(int(r['passed']) for r in analysis['trajectories'])==7
    total=0; last=None; passing_multi=0; passing_zero=0
    for summary in analysis['trajectories']:
        rows=[json.loads(line) for line in (capture/'landscapes'/(summary['label']+'.jsonl')).read_text().splitlines()]
        assert len(rows)==summary['checkedStates']
        counts=Counter(len(r['roots']) for r in rows)
        admissible=Counter(sum(int(x['admissibleState']) for x in r['roots']) for r in rows)
        assert {str(k):v for k,v in counts.items()}==summary['rootCountHistogram']
        assert {str(k):v for k,v in admissible.items()}==summary['admissibleRootCountHistogram']
        assert sum(int(r['originalStressPa']<0) for r in rows)==summary['negativeOriginalStressStates']
        assert all(r['step']==i and r['statePreserved'] for i,r in enumerate(rows))
        assert all(r['passed'] for r in rows)==summary['passed']
        assert all(r['passed'] for r in rows[:-1])
        assert max(r['maximumRootResidualPa'] for r in rows)==summary['maximumRootResidualPa']
        assert max(r['maximumForceDifferencePa'] for r in rows)==summary['maximumForceDifferencePa']
        assert max((r['maximumRootDifferenceNm'] for r in rows if r['maximumRootDifferenceNm'] is not None),default=0.)==summary['maximumRootDifferenceNm']
        for r in rows:
            if r['passed']:
                assert len(r['roots'])==len(r['independentRootsNm'])
                assert all(abs(a['lengthNm']-b)<=1e-9 for a,b in zip(r['roots'],r['independentRootsNm']))
                passing_multi+=int(len(r['roots'])>1); passing_zero+=int(not r['roots'])
        total+=len(rows); last=rows[-1]
    assert total==1556 and last['step']==48 and not last['passed']
    failure=common.read(capture/'landscapes/chain-stretch-shorten-500us-first-failure.json')
    assert failure['snapshotStep']==48
    for key,value in failure['result'].items():
        if key not in ('segments','boundaries'): assert last[key]==value,key
    roots=failure['result']['roots']; reference=failure['result']['independentRootsNm']
    assert len(roots)==4 and len(reference)==3
    assert roots[0]['kind']=='knot' and roots[0]['lengthNm']==1200. and reference[0]==1200.
    assert roots[1]['kind']=='open-interval' and 0<roots[1]['lengthNm']-roots[0]['lengthNm']<1e-9
    assert roots[0]['residualPa']!=0 and abs(roots[0]['residualPa'])<1e-7
    assert roots[1]['residualPa']!=0 and abs(roots[1]['residualPa'])<1e-7
    assert [r['lengthNm'] for r in roots[2:]]==reference[1:]
    assert failure['result']['maximumRootResidualPa']<1e-7 and failure['result']['maximumForceDifferencePa']<1e-7
    source=(capture/'source/tools/spindle_slack_roots.py').read_text()
    assert "if abs(actual - target) <= CRITERIA['forceAbsolutePa']:" in source
    assert "abs(root-r) <= CRITERIA['rootAbsoluteNm'] and (r == a or r == b)" in source
    result.update(reviewedUtc=datetime.now(timezone.utc).isoformat(),operatorReportedCompletion=True,
        originalStatus='FAILED',analyticControlsPassed=6,savedStateQueries=1556,completedTrajectories=7,
        partialTrajectory='chain-stretch-shorten-500us',failedStep=48,duplicateKnotRootDiagnosed=True,
        nearKnotRoots=roots[:2],savedIndependentRootsNm=reference,
        passingStatesWithMultipleRoots=passing_multi,passingStatesWithNoRoot=passing_zero,
        fullMatrixQualified=False,originalBoundsChanged=False,rootSearchRerun=False,kineticsExecuted=False,
        physiologicalValidityQualified=False,avatarRecoveryAssessed=False,analysis=analysis)
    output.mkdir(); (output/'review.json').write_text(json.dumps(result,indent=2,allow_nan=False)+'\n')
    print(json.dumps({k:v for k,v in result.items() if not isinstance(v,(dict,list))},indent=2))


if __name__=='__main__':
    if not __debug__: raise RuntimeError('Assertions must remain enabled.')
    parser=argparse.ArgumentParser(); parser.add_argument('--operator-reported-complete',required=True,action='store_true')
    parser.parse_args(); main()

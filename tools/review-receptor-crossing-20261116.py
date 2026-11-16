"""Review reported saved-endpoint crossing evidence; no RHS, root search or test."""
import argparse
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]


def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path)
    value=importlib.util.module_from_spec(spec);spec.loader.exec_module(value);return value


def main():
    output=ROOT/'artifacts/receptor-crossing-review-20261116';assert not output.exists()
    capture=ROOT/'artifacts/receptor-crossing-20261116-c099e3'
    common=module('crossing_complete_integrity',ROOT/'tools/review-olive-and-motor-20261109.py')
    review=common.verify(capture,[],'8a6b4c4431d62d2715258ef46bd7bbece1b3d3ad')
    archive=module('crossing_complete_archive',ROOT/'tools/review-spindle-root-and-causal-motor-20261113.py')
    review['verifiedGitFiles']=archive.source_archive(common,capture,review['sourceRevision'])
    env=module('crossing_complete_environment',capture/'source/tools/golgi-reference-resume.py').environment()
    assert env==common.read(capture/'preparation-environment.json')
    status=common.read(capture/'window-status.json');report=common.read(capture/'crossing-analysis.json')
    spec=common.read(capture/'crossing-input.json');config=common.read(capture/'configuration.json')
    assert status['DiagnosticCriteriaPassed'] and status['DerivativeEvaluationAttempted'] and status['FrozenPackageUnchanged']
    assert report['completed'] and report['diagnosticCriteriaPassed'] and report['frozenInputsAndEnvironmentPreserved']
    for flag in ('kineticsIntegrated','productionImplementationChanged','presynapticNeuronalAuthorityQualified','physiologyQualified','avatarRecoveryAssessed'):
        assert report[flag] is False
    helper=module('crossing_review_provenance',ROOT/'tools/prepare-causal-motor.py')
    prior=ROOT/'artifacts/receptor-comparison-review-20261116'
    helper.verify(prior,['review.json'])
    assert helper.digest(prior/'review.json')==helper.digest(capture/'reference/review.json')
    original=helper.read(prior/'review.json');provenance=helper.read(capture/'provenance.json')
    assert provenance['capture']==original['capture'] and provenance['manifestSha256']==original['manifestSha256']
    original_package=Path(original['capture'])
    assert helper.digest(original_package/'backup-manifest.json')==original['manifestSha256']
    helper.verify(original_package,['receptor-membrane-input.json'])
    assert spec['criteria']==helper.read(original_package/'receptor-membrane-input.json')['criteria']
    assert not spec['kineticsIntegrated'] and not spec['productionImplementationChanged']
    assert config['ExpectedEndpointPairs']==provenance['endpointPairs']==len(spec['rows'])==27
    assert len(report['controls'])==spec['expectedAnalyticControls']==5
    expected_labels=['known-cubic-0.2','known-cubic-0.5','known-cubic-0.8','linear-limit','three-crossings-rejected']
    assert [r['label'] for r in report['controls']]==expected_labels and all(r['passed'] for r in report['controls'])
    for row,expected in zip(report['controls'][:4],[.2,.5,.8,4.5]):
        assert abs(row['timeMs']-expected)<=1e-12 and abs(row['residualMv'])<=spec['polynomialResidualMv']
        assert row['minimumScaledSlope']>0
    def identity(row):return row['scenario'],row['maximumStepMs'],row['ordinal']
    old={identity(r):r for r in original['crossingDecomposition']}
    assert len(old)==27 and [identity(r) for r in report['rows']]==[identity(r) for r in spec['rows']]
    for data,record in zip(spec['rows'],report['rows']):
        assert all(data[k]==v for k,v in old[identity(data)].items())
        assert record['legacyErrorMs']==data['totalErrorMs']
        h=data['bracketEndMs']-data['bracketStartMs']
        assert h>0
        for label,v0,v1 in (('candidate',data['startState'][0],data['endState'][0]),
                            ('reference',data['referenceStartState'][0],data['referenceEndState'][0])):
            result=record['estimates'][label];a,b,c,d=result['coefficients'];s=result['fraction']
            assert v0<-20<=v1 and 0<s<=1 and d==v0+20
            assert abs((a+b+c+d)-(v1+20))<=1e-12
            assert result['timeMs']==data['bracketStartMs']+h*s
            assert data['bracketStartMs']<=result['timeMs']<=data['bracketEndMs']
            assert result['residualMv']==((a*s+b)*s+c)*s+d
            assert abs(result['residualMv'])<=spec['polynomialResidualMv']
            positions=[0.,1.]
            if a and 0<-b/(3*a)<1:positions.append(-b/(3*a))
            minimum=min(3*a*x*x+2*b*x+c for x in positions)
            assert minimum==result['minimumScaledSlope'] and minimum>0
        assert record['cubicErrorMs']==record['estimates']['candidate']['timeMs']-data['referenceCrossingMs']
        assert record['referenceCubicErrorMs']==record['estimates']['reference']['timeMs']-data['referenceCrossingMs']
        passed=abs(record['cubicErrorMs'])<=spec['criteria']['candidateCrossingMs'] and abs(record['referenceCubicErrorMs'])<=spec['criteria']['referenceCrossingMs']
        assert record['passed']==passed and passed
    assert len(report['conditions'])==len(spec['conditions'])==18
    for condition,stored in zip(spec['conditions'],report['conditions']):
        rows=[r for r in report['rows'] if (r['scenario'],r['maximumStepMs'])==(condition['scenario'],condition['maximumStepMs'])]
        expected=dict(**condition,maximumCubicErrorMs=max((abs(r['cubicErrorMs']) for r in rows),default=0.),
            maximumLegacyErrorMs=max((abs(r['legacyErrorMs']) for r in rows),default=0.),passed=all(r['passed'] for r in rows))
        assert stored==expected and len(rows)==condition['crossings']
    rebuilt=[]
    for name in dict.fromkeys(c['scenario'] for c in spec['conditions']):
        rows=[c for c in report['conditions'] if c['scenario']==name]
        for coarse,fine in zip(rows,rows[1:]):
            bound=max(spec['criteria']['refinementFloor'],spec['criteria']['refinementRatio']*coarse['maximumCubicErrorMs'])
            rebuilt.append(dict(scenario=name,coarseStepMs=coarse['maximumStepMs'],fineStepMs=fine['maximumStepMs'],
                allowedMs=bound,fineErrorMs=fine['maximumCubicErrorMs'],passed=fine['maximumCubicErrorMs']<=bound))
    assert len(rebuilt)==12 and rebuilt==report['refinement'] and all(r['passed'] for r in rebuilt)
    changed=[r for r in report['conditions'] if r['maximumCubicErrorMs']>r['maximumLegacyErrorMs']]
    review.update(reviewedUtc=datetime.now(timezone.utc).isoformat(),operatorReportedCompletion=True,
        recordedAnalyticControlsPassed=5,recordedEndpointPairs=27,polynomialsChecked=54,conditionsChecked=18,
        refinementComparisonsPassed=12,savedArithmeticReconciled=True,originalBoundsChanged=False,
        maximumCubicErrorMs=max(abs(r['cubicErrorMs']) for r in report['rows']),
        maximumReferenceCubicErrorMs=max(abs(r['referenceCubicErrorMs']) for r in report['rows']),
        conditionsWithLargerAbsoluteError=changed,conditions=report['conditions'],
        noDerivativeEvaluationDuringReview=True,noRootSearchDuringReview=True,noTestExecutionDuringReview=True,
        originalComparisonRemainsFailed=True,nativeImplementationQualified=False,causalPublicationQualified=False,
        presynapticNeuronalAuthorityQualified=False,physiologyQualified=False,avatarRecoveryAssessed=False)
    output.mkdir();(output/'review.json').write_text(json.dumps(review,indent=2,allow_nan=False)+'\n',encoding='utf-8')
    print(json.dumps({k:v for k,v in review.items() if not isinstance(v,(dict,list))},indent=2))


if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    parser=argparse.ArgumentParser();parser.add_argument('--operator-reported-complete',required=True,action='store_true')
    parser.parse_args();main()

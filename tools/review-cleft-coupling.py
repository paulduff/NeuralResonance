"""Review operator-completed closed-cleft evidence using only saved comparisons."""
import argparse
from datetime import datetime,timezone
from decimal import Decimal,localcontext
import importlib.util
import json
from pathlib import Path
import sys

ROOT=Path(__file__).resolve().parents[1]


def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m


def numbers(value,key=''):
    if isinstance(value,dict):return {k:numbers(v,k) for k,v in value.items()}
    if isinstance(value,list):return [numbers(v,key) for v in value]
    return value if key in ('id','leftId','rightId') else Decimal(value)


def review(capture,output,reported):
    assert reported and not output.exists(),'Operator completion report and NEW review required.'
    common=module('cleft_saved_integrity',ROOT/'tools/review-olive-and-motor-20261109.py')
    revision='c53783a17e9ab26ab5968bca5d267d0c1a277fa5'
    result=common.verify(capture,['test-binaries.json','probe-binaries.json'],revision)
    result['verifiedGitFiles']=module('cleft_saved_archive',ROOT/'tools/review-spindle-root-and-causal-motor-20261113.py').source_archive(common,capture,revision)
    sys.path.insert(0,str(capture/'source/tools'))
    try:base=module('cleft_saved_comparison',capture/'source/tools/analyse-cleft-coupling.py')
    finally:sys.path.pop(0)
    spec=base.validate(common.read(capture/'cleft-input.json'));cfg=common.read(capture/'configuration.json');status=common.read(capture/'window-status.json')
    assert cfg['IsolatedCleftConstitutive'] and cfg['NoLiveMonitoring'] and cfg['ExpectedTestCases']==40
    tests=module('cleft_saved_inventory',ROOT/'tools/receptor_membrane_gate.py').check_tests(capture,base.COUNTS)
    assert common.digest(capture/'managed-runtime-before.json')==common.digest(capture/'managed-runtime-after.json')
    before=common.read(capture/'reference-preflight.json')['environment'];after=common.read(capture/'reference-postflight.json')
    assert after['unchanged'] and before==after['environment']==common.read(capture/'preparation-environment.json')
    for key in ('FocusedTestsPassed','NumericalCriteriaPassed','ComponentStudyExecutionAttempted','ConstitutiveArithmeticEvaluated',
                'NumericalEnvironmentUnchanged','ManagedRuntimePreserved','FrozenPackageUnchanged'):assert status[key] is True,key
    for key in ('TimeIntegrated','SpatialGeometryCalibrated','AfferentFiringQualified','NativeBodyExecuted','LiveBrainAccessed',
                'OldSavesLoaded','PhysiologyQualified','AvatarRecoveryAssessed','FullUnitSuitePassed'):assert status[key] is False,key
    expected={k:spec[k] for k in ('version','edgeVersion','chargeVersion','spatialVersion','edgeCases','chargeCases','spatialCases','expectedEdges','expectedCharges','expectedSpatial')}
    expected['chargeCases']=[base.captured_charge_input(row) for row in spec['chargeCases']]
    assert common.read(capture/'study/input.json')==expected
    assert common.read(capture/'study/results.json')==dict(completed=True,edgeRows=468,chargeRows=60,spatialRows=96,
        timeIntegrated=False,spatialGeometryCalibrated=False,physiologyQualified=False,avatarRecoveryAssessed=False)
    maximum=Decimal(0);counts={};fingerprints={}
    for label,key in [('edges','edgeCases'),('charges','chargeCases'),('spatial','spatialCases')]:
        count=0
        with (capture/'study'/(label+'.jsonl')).open() as native,(capture/('independent-'+label+'.jsonl')).open() as reference:
            for expected,a,b in zip(spec[key],native,reference,strict=True):
                row=json.loads(a);saved=json.loads(b)
                assert set(row)=={'input','fingerprint','evaluation'}
                assert row['input']==(base.captured_charge_input(expected) if label=='charges' else expected)
                assert saved['index']==count and saved['input']==expected and saved['precision']==[60,90]
                identity_keys=dict(edges=('material','geometry'),charges=('capacitances',),
                    spatial=('hairCellId','hairApicalPf','hairPerilymphPf','material','compartments','connections'))[label]
                identity=json.dumps([expected[k] for k in identity_keys],sort_keys=True)
                assert len(row['fingerprint'])==64 and fingerprints.setdefault((label,identity),row['fingerprint'])==row['fingerprint']
                references=[numbers(r) for r in saved['expected']];assert len(references)==2
                with localcontext() as context:
                    context.prec=90
                    base.compare_values(references[0],references[1],Decimal(spec['referenceAbsoluteTolerance']),Decimal(spec['referenceRelativeTolerance']))
                    ratio=base.compare_values(row['evaluation'],references[1],Decimal(spec['candidateAbsoluteTolerance']),Decimal(spec['candidateRelativeTolerance']))
                    assert str(ratio)==saved['maximumAllowedFraction'];maximum=max(maximum,ratio)
                count+=1
        counts[label]=count
    assert counts==dict(edges=468,charges=60,spatial=96) and len(set(fingerprints.values()))==len(fingerprints)
    analysis=dict(completed=True,numericalComponentCriteriaPassed=True,tests=tests,counts=counts,maximumAllowedFraction=str(maximum),
        decimalPrecisions=[60,90],originalBoundsChanged=False,timeIntegrated=False,spatialGeometryCalibrated=False,physiologyQualified=False,avatarRecoveryAssessed=False)
    assert analysis==common.read(capture/'analysis.json')
    result.update(reviewedUtc=datetime.now(timezone.utc).isoformat(),operatorReportedCompletion=True,
        savedAnalysisExactlyReproduced=True,independentSavedComparisonsRechecked=True,numericalCriteriaPassed=True,passedTests=40,
        analysis=analysis,modelsEvaluatedDuringReview=False,testsExecutedDuringReview=False,
        timeIntegrated=False,physiologyQualified=False,avatarRecoveryAssessed=False)
    output.mkdir(parents=True)
    with (output/'review.json').open('x',encoding='utf-8') as stream:json.dump(result,stream,indent=2,allow_nan=False);stream.write('\n')
    print(json.dumps({k:v for k,v in result.items() if k!='analysis'},indent=2),flush=True)


if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('capture',type=Path);parser.add_argument('output',type=Path)
    parser.add_argument('--operator-reported-complete',action='store_true',required=True);args=parser.parse_args()
    review(args.capture.resolve(),args.output.resolve(),args.operator_reported_complete)

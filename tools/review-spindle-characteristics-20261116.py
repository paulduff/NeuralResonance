"""Audit reported retained-state arrays and ledgers; no transport/kinetics rerun."""
import argparse
from datetime import datetime, timezone
import importlib.util
import json
import math
import os
from pathlib import Path

for key in ('OMP_NUM_THREADS','OPENBLAS_NUM_THREADS','MKL_NUM_THREADS','NUMEXPR_NUM_THREADS'):
    os.environ[key]='1'
import numpy as np

ROOT=Path(__file__).resolve().parents[1]


def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path)
    value=importlib.util.module_from_spec(spec);spec.loader.exec_module(value);return value


def main():
    output=ROOT/'artifacts/spindle-characteristics-review-20261116';assert not output.exists()
    capture=ROOT/'artifacts/spindle-characteristics-20261116-7c6db6'
    common=module('retained_integrity',ROOT/'tools/review-olive-and-motor-20261109.py')
    review=common.verify(capture,[],'683b0cf0eb4774b935558b557438c78b60add02c')
    archive=module('retained_archive',ROOT/'tools/review-spindle-root-and-causal-motor-20261113.py')
    review['verifiedGitFiles']=archive.source_archive(common,capture,review['sourceRevision'])
    env=module('retained_environment',capture/'source/tools/golgi-reference-resume.py').environment()
    assert env==common.read(capture/'preparation-environment.json')==common.read(capture/'reference-preflight.json')['environment']
    assert common.read(capture/'reference-postflight.json')==dict(environment=env,unchanged=True,modelExecuted=False)
    status=common.read(capture/'window-status.json');config=common.read(capture/'configuration.json')
    assert status['TransportNumericsPassed'] and status['FrozenPackageUnchanged'] and status['NumericalEnvironmentUnchanged']
    assert config['NoLiveMonitoring'] and config['IsolatedSpindleCharacteristics']
    spec=common.read(capture/'characteristic-input.json');report=common.read(capture/'characteristic-analysis.json')
    bounds=spec['criteria']
    assert bounds==dict(populationAbsolute=1e-10,conservationAbsolute=1e-10,forceAbsolutePa=1e-7,
        domainAbsolute=1e-8,secondMomentAbsoluteNm2=1e-8)==report['criteria']
    assert report['completed'] and report['transportNumericsPassed']
    for key in ('originalBoundsChanged','kineticsExecuted','rootSelected','slackTrajectoryAssessed',
                'sensoryFiringAssessed','physiologyQualified','avatarRecoveryAssessed'):assert report[key] is False
    helper=module('retained_provenance',ROOT/'tools/prepare-causal-motor.py')
    prior=ROOT/'artifacts/spindle-transport-review-20261116';helper.verify(prior,['review.json'])
    assert helper.digest(prior/'review.json')==helper.digest(capture/'reference/transport-review.json')
    baseline=common.read(prior/'review.json')
    assert baseline['operatorReportedCompletion'] and baseline['savedRecordsReconciled']
    assert helper.digest(Path(baseline['capture'])/'backup-manifest.json')==baseline['manifestSha256']
    controls=common.read(capture/'controls/controls.json')
    labels=['stationary','half-cell','right-partial','left-partial','right-full','left-full','multicell',
        'whole-domain','empty','readout-and-return-preserve-shape','escaped-cohorts-never-reappear','invalid-input-preserves-state']
    assert controls['completed'] and controls['passed'] and [r['label'] for r in controls['cases']]==labels
    assert all(r['passed'] for r in controls['cases'])
    for label in labels[:9]:assert common.read(capture/'controls'/(label+'.json'))['passed']
    returned=controls['cases'][9]['state'];lost=controls['cases'][10]['state']
    assert abs(returned['retained']-.2)<=bounds['populationAbsolute'] and abs(returned['secondMomentNm2']-.2*.5**2/12)<=bounds['secondMomentAbsoluteNm2']
    assert abs(lost['retained'])<=bounds['populationAbsolute'] and abs(lost['rightOutflow']-.2)<=bounds['populationAbsolute']
    matrix=common.read(capture/'matrix-analysis.json')
    assert matrix['completed'] and matrix['passed'] and matrix['queries']==25308 and len(matrix['cases'])==12
    shifts=common.read(capture/'transport-input.json')['shiftsNm'];total=0;maximum_conservation=0.
    expected_labels=[f'{fiber}-{path}-{step}us' for fiber in ('bag','chain') for path in ('stretch-shorten','imposed-below-slack') for step in (1000,500,250)]
    assert [r['label'] for r in matrix['cases']]==expected_labels
    for summary in matrix['cases']:
        rows=[json.loads(line) for line in (capture/'matrix'/(summary['label']+'.jsonl')).read_text().splitlines()]
        with np.load(capture/'matrix'/(summary['label']+'.npz')) as stored:
            candidate=stored['candidateMasses'];reference=stored['referenceMasses']
        assert candidate.shape==reference.shape==(len(rows),81) and len(rows)==summary['queries'] and len(rows)%9==0
        assert summary['passed'] and np.isfinite(candidate).all() and np.isfinite(reference).all()
        for index,row in enumerate(rows):
            c,r,e=row['candidate'],row['reference'],row['errors']
            assert row['stateIndex']==index//9 and row['shiftNm']==shifts[index%9] and row['passed']
            assert float(np.sum(abs(candidate[index]-reference[index])))==e['populationL1']<=bounds['populationAbsolute']
            assert max(abs(c[k]-r[k]) for k in ('leftOutflow','rightOutflow'))==e['fluxError']<=bounds['populationAbsolute']
            assert abs(c['forcePa']-r['continuumForcePa'])==e['forceErrorPa']<=bounds['forceAbsolutePa']
            assert 69000*(c['firstMomentNm']+2.5*c['retained'])==c['forcePa']
            assert abs(float(np.sum(candidate[index]))-c['retained'])<=bounds['populationAbsolute']
            assert abs(c['conservationResidual'])==e['conservationResidual']<=bounds['conservationAbsolute']
            assert e['domainViolation']<=bounds['domainAbsolute']
            maximum_conservation=max(maximum_conservation,e['conservationResidual'])
        assert summary['maximumPopulationL1']==max(r['errors']['populationL1'] for r in rows)
        assert summary['maximumForceErrorPa']==max(r['errors']['forceErrorPa'] for r in rows)
        total+=len(rows)
    assert total==25308
    continuity=common.read(capture/'continuity-analysis.json')
    assert continuity['completed'] and continuity['passed'] and not continuity['rootsRecomputed']
    assert len(continuity['cases'])==continuity['probes']==config['ExpectedContinuityProbes']==31
    for row in continuity['cases']:
        assert row['passed'] and all(p['passed'] for p in row['points'])
        assert abs(row['points'][-1]['candidate']['forcePa']-row['points'][0]['candidate']['forcePa'])==row['forceSpanPa']<=row['boundPa']
    histories=common.read(capture/'history-analysis.json');groups={};endpoint_count=0
    assert histories['completed'] and histories['passed'] and len(histories['cases'])==144 and len(histories['cadence'])==96
    assert len({r['label'] for r in histories['cases']})==144
    for summary in histories['cases']:
        stem=capture/'histories'/summary['label'];rows=common.read(Path(str(stem)+'.json'))
        with np.load(Path(str(stem)+'.npz')) as stored:
            actual=stored['candidateHistoryMasses'];exact=stored['exactHistoryMasses'];initial=stored['initialMasses']
            left=stored['leftOriginHistoryNm'];right=stored['rightOriginHistoryNm'];edges=stored['edgesNm']
        expected=1+summary['steps']*(2 if summary['path']=='return' else 1)
        assert len(rows)==summary['endpoints']==expected
        assert actual.shape==exact.shape==left.shape==right.shape==(expected,len(initial))
        assert np.isfinite(actual).all() and np.isfinite(exact).all() and np.isfinite(left).all() and np.isfinite(right).all()
        assert (np.diff(left,axis=0)>=0).all() and (np.diff(right,axis=0)<=0).all() and (right>=left).all()
        density=initial/np.diff(edges)
        assert abs(math.fsum(initial)-.2)<=bounds['conservationAbsolute']
        for i,row in enumerate(rows):
            assert row['index']==i and row['passed']
            assert float(np.sum(abs(actual[i]-exact[i])))==row['populationL1']<=bounds['populationAbsolute']
            mass=density*(right[i]-left[i]);a=left[i]+row['displacementNm'];b=right[i]+row['displacementNm']
            assert math.fsum(mass)==row['retained']
            assert math.fsum(mass*(a+b)*.5)==row['firstMomentNm']
            assert math.fsum(mass*(a*a+a*b+b*b)/3)==row['secondMomentNm2']
            assert 69000*(row['firstMomentNm']+2.5*row['retained'])==row['forcePa']
            assert abs(float(np.sum(actual[i]))-row['retained'])<=bounds['populationAbsolute']
            assert abs(row['retained']+row['leftOutflow']+row['rightOutflow']-.2)<=bounds['conservationAbsolute']
            assert row['forceErrorPa']<=bounds['forceAbsolutePa'] and row['secondMomentErrorNm2']<=bounds['secondMomentAbsoluteNm2']
            assert row['fluxError']<=bounds['populationAbsolute'] and row['domainViolation']<=bounds['domainAbsolute']
            assert abs(row['conservationResidual'])<=bounds['conservationAbsolute']
        for saved,field in [('maximumPopulationL1','populationL1'),('maximumForceErrorPa','forceErrorPa'),('maximumSecondMomentErrorNm2','secondMomentErrorNm2')]:
            assert summary[saved]==max(r[field] for r in rows)
        groups[(summary['halfRangeNm'],summary['widthNm'],summary['profile'],summary['path'],summary['steps'])]=(actual,rows)
        endpoint_count+=len(rows)
    for pair in histories['cadence']:
        key=tuple(pair['case']);coarse,a=groups[key+(pair['coarseSteps'],)];fine,b=groups[key+(pair['fineSteps'],)]
        stride=pair['fineSteps']//pair['coarseSteps'];fine=fine[::stride];b=b[::stride]
        assert len(a)==len(b) and pair['passed']
        assert float(np.max(np.sum(abs(coarse-fine),axis=1)))==pair['populationL1']<=bounds['populationAbsolute']
        assert max(abs(x['forcePa']-y['forcePa']) for x,y in zip(a,b))==pair['forceDifferencePa']<=bounds['forceAbsolutePa']
        assert max(abs(x['secondMomentNm2']-y['secondMomentNm2']) for x,y in zip(a,b))==pair['secondMomentDifferenceNm2']<=bounds['secondMomentAbsoluteNm2']
        assert max(abs(x[k]-y[k]) for x,y in zip(a,b) for k in ('leftOutflow','rightOutflow'))==pair['outflowDifference']<=bounds['populationAbsolute']
    review.update(reviewedUtc=datetime.now(timezone.utc).isoformat(),operatorReportedCompletion=True,
        savedRecordsReconciled=True,analyticControlsPassed=12,savedArrayComparisons=total,continuityProbes=31,
        historyCases=144,historyEndpoints=endpoint_count,cadenceComparisons=96,
        maximumMatrixPopulationL1=max(r['maximumPopulationL1'] for r in matrix['cases']),
        maximumMatrixForceErrorPa=max(r['maximumForceErrorPa'] for r in matrix['cases']),
        maximumMatrixConservationResidual=maximum_conservation,
        maximumHistoryPopulationL1=max(r['maximumPopulationL1'] for r in histories['cases']),
        maximumHistoryForceErrorPa=max(r['maximumForceErrorPa'] for r in histories['cases']),
        maximumHistorySecondMomentErrorNm2=max(r['maximumSecondMomentErrorNm2'] for r in histories['cases']),
        maximumCadencePopulationL1=max(r['populationL1'] for r in histories['cadence']),
        maximumCadenceForceDifferencePa=max(r['forceDifferencePa'] for r in histories['cadence']),
        maximumCadenceOutflowDifference=max(r['outflowDifference'] for r in histories['cadence']),
        finestHistories=[r for r in histories['cases'] if r['widthNm']==.0625 and r['steps']==64],
        previousMaximumHistoryPopulationL1=baseline['maximumHistoryPopulationL1'],
        previousMaximumHistoryForceErrorPa=baseline['maximumHistoryForceErrorPa'],
        originalBoundsChanged=False,newTransportExecuted=False,newHistoryExecuted=False,kineticsExecuted=False,
        finiteDomainPhysiologyQualified=False,slackTrajectoryAssessed=False,sensoryFiringAssessed=False,
        physiologyQualified=False,avatarRecoveryAssessed=False)
    output.mkdir();(output/'review.json').write_text(json.dumps(review,indent=2)+'\n',encoding='utf-8')
    print(json.dumps({k:review[k] for k in ('verifiedFilesPerCopy','verifiedGitFiles','historyEndpoints',
        'maximumHistoryPopulationL1','maximumHistoryForceErrorPa','maximumCadencePopulationL1','maximumCadenceForceDifferencePa')},indent=2),flush=True)


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--operator-reported-complete',action='store_true',required=True);parser.parse_args()
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    main()

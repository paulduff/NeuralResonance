"""Audit reported transport arrays/ledgers; no new translation, history or kinetics."""
import argparse
from datetime import datetime, timezone
import importlib.util
import json
import os
from pathlib import Path

for name in ('OMP_NUM_THREADS','OPENBLAS_NUM_THREADS','MKL_NUM_THREADS','NUMEXPR_NUM_THREADS'):
    os.environ[name]='1'
import numpy as np

ROOT=Path(__file__).resolve().parents[1]


def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path)
    value=importlib.util.module_from_spec(spec);spec.loader.exec_module(value);return value


def main():
    output=ROOT/'artifacts/spindle-transport-review-20261116';assert not output.exists()
    capture=ROOT/'artifacts/spindle-transport-20261116-ba9648'
    common=module('transport_complete_integrity',ROOT/'tools/review-olive-and-motor-20261109.py')
    review=common.verify(capture,[],'6c7788c6f6f4e2734702069486bf0d2704c5b20a')
    archive=module('transport_complete_archive',ROOT/'tools/review-spindle-root-and-causal-motor-20261113.py')
    review['verifiedGitFiles']=archive.source_archive(common,capture,review['sourceRevision'])
    env=module('transport_complete_environment',capture/'source/tools/golgi-reference-resume.py').environment()
    assert env==common.read(capture/'preparation-environment.json')==common.read(capture/'reference-preflight.json')['environment']
    assert common.read(capture/'reference-postflight.json')==dict(environment=env,unchanged=True,modelExecuted=False)
    status=common.read(capture/'window-status.json');assert status['TransportNumericsPassed'] and status['FrozenPackageUnchanged']
    spec=common.read(capture/'transport-input.json');bounds=spec['criteria']
    report=common.read(capture/'transport-analysis.json')
    assert report['completed'] and report['transportNumericsPassed'] and report['criteria']==bounds
    for key in ('originalBoundsChanged','kineticsExecuted','rootSelected','slackTrajectoryAssessed',
                'sensoryFiringAssessed','physiologyQualified','avatarRecoveryAssessed'):assert report[key] is False
    controls=common.read(capture/'controls/controls.json')
    assert controls['completed'] and controls['passed'] and len(controls['cases'])==10
    assert len({r['label'] for r in controls['cases']})==10
    for c in controls['cases']:
        r=common.read(capture/'controls'/(c['label']+'.json'));assert c['passed'] and r['passed']
        if 'expectedMasses' in r:
            actual=np.asarray(r['result']['candidate']['masses']);expected=np.asarray(r['expectedMasses'])
            assert np.max(abs(actual-expected))<=bounds['populationAbsolute']
            assert abs(r['result']['candidate']['forcePa']-r['expectedForcePa'])<=bounds['forceAbsolutePa']
        else:assert r['legacyJumpPa']>100000 and r['candidateSpanPa']<=r['candidateSpanBoundPa']
    matrix=common.read(capture/'matrix-analysis.json');assert matrix['completed'] and matrix['passed'] and matrix['queries']==25308
    assert len(matrix['trajectories'])==12
    total=0;centres=np.arange(-20.,20.5,.5)
    for summary in matrix['trajectories']:
        rows=[json.loads(line) for line in (capture/'matrix'/(summary['label']+'.jsonl')).read_text().splitlines()]
        with np.load(capture/'matrix'/(summary['label']+'.npz')) as values:
            candidate=values['candidateMasses'];reference=values['referenceMasses']
        assert candidate.shape==reference.shape==(len(rows),81)
        assert np.isfinite(candidate).all() and np.isfinite(reference).all()
        assert len(rows)==summary['queries']==summary['expectedQueries'] and summary['passed']
        for index,row in enumerate(rows):
            assert row['step']==index//9 and row['shiftNm']==spec['shiftsNm'][index%9]
            assert row['passed'] and row['statePreserved']
            assert float(np.max(abs(candidate[index]-reference[index])))==row['maximumPopulationDifference']<=bounds['populationAbsolute']
            for side,array in (('candidate',candidate),('reference',reference)):
                assert float(np.sum(array[index]))==row[side]['retained']
                assert float(centres@array[index])==row[side]['firstMomentNm']
                assert 69000.*(row[side]['firstMomentNm']+2.5*row[side]['retained'])==row[side]['forcePa']
            assert abs(row['candidate']['forcePa']-row['reference']['forcePa'])==row['forceDifferencePa']<=bounds['forceAbsolutePa']
            assert row['candidate']['forcePa']-row['reference']['continuumForcePa']==row['gridVsContinuumForcePa']
            assert row['conservationResidual']<=bounds['conservationAbsolute'] and row['domainViolation']<=bounds['domainAbsolute']
        for saved,field in (('maximumPopulationDifference','maximumPopulationDifference'),
            ('maximumForceDifferencePa','forceDifferencePa'),('maximumConservationResidual','conservationResidual')):
            assert summary[saved]==max(r[field] for r in rows)
        assert summary['maximumGridVsContinuumForcePa']==max(abs(r['gridVsContinuumForcePa']) for r in rows)
        total+=len(rows)
    assert total==25308
    continuity=common.read(capture/'continuity-analysis.json')
    assert continuity['completed'] and continuity['passed'] and continuity['capturedNoRootStates']==31
    assert continuity['probes']==len(continuity['cases'])==31 and not continuity['rootsRecomputed']
    for i,summary in enumerate(continuity['cases']):
        row=common.read(capture/'continuity'/(str(i)+'.json'))
        assert {k:v for k,v in row.items() if k!='points'}==summary
        assert row['passed'] and all(r['passed'] for r in row['points'])
        assert abs(row['points'][-1]['candidate']['forcePa']-row['points'][0]['candidate']['forcePa'])==row['candidateForceSpanPa']<=row['candidateSpanBoundPa']
    histories=common.read(capture/'history-analysis.json')
    assert histories['completed'] and histories['passed'] and len(histories['cases'])==144
    for summary in histories['cases']:
        path=capture/'histories'/summary['label']
        rows=common.read(path.with_suffix('.json')) if '.' not in summary['label'] else common.read(Path(str(path)+'.json'))
        assert len(rows)==summary['endpoints']==1+summary['stepsPerLeg']*(2 if summary['path']=='return' else 1)
        assert summary['passed'] and all(r['passed'] for r in rows)
        assert all(r['index']==i and r['conservationResidual']<=bounds['conservationAbsolute'] and
            r['domainViolation']<=bounds['domainAbsolute'] and abs(r['retained']+r['leftOutflow']+r['rightOutflow']-.2)<=bounds['conservationAbsolute'] for i,r in enumerate(rows))
        for saved,field in (('maximumPopulationL1','populationL1'),('maximumForceErrorPa','forceErrorPa'),('maximumSecondMomentErrorNm2','secondMomentErrorNm2')):
            assert summary[saved]==max(abs(r[field]) for r in rows)
        with np.load(Path(str(path)+'.npz')) as final:
            actual=final['candidateFinalMasses'];exact=final['exactFinalMasses'];x=final['centresNm']
            assert np.isfinite(actual).all() and np.isfinite(exact).all()
            assert float(np.sum(abs(actual-exact)))==rows[-1]['populationL1']
            assert float(np.sum(actual))==summary['finalRetained']==rows[-1]['retained']
            assert abs(float(np.sum(exact))-summary['exactFinalRetained'])<=bounds['conservationAbsolute']
            assert abs(float(np.sum(final['initialMasses']))-.2)<=bounds['conservationAbsolute']
    assert len(histories['spatialRefinement'])==36
    for pair in histories['spatialRefinement']:
        selected=[r for r in histories['cases'] if all(r[k]==pair[k] for k in ('halfRangeNm','profile','path','stepsPerLeg'))]
        assert [r['widthNm'] for r in selected]==spec['refinementWidthsNm']
        assert pair['coarseError']==selected[0]['maximumPopulationL1'] and pair['fineError']==selected[-1]['maximumPopulationL1']
        bound=max(spec['spatialRefinementFloor'],spec['spatialRefinementRatio']*pair['coarseError'])
        assert pair['allowedFineError']==bound and pair['passed'] and pair['fineError']<=bound
    finest=[r for r in histories['cases'] if r['widthNm']==.0625 and r['stepsPerLeg']==64]
    assert len(finest)==12
    review.update(reviewedUtc=datetime.now(timezone.utc).isoformat(),operatorReportedCompletion=True,
        analyticControlsPassed=10,savedArrayComparisons=total,continuityProbes=31,historyCases=144,
        spatialRefinementComparisons=36,savedRecordsReconciled=True,
        maximumPopulationDifference=max(r['maximumPopulationDifference'] for r in matrix['trajectories']),
        maximumForceDifferencePa=max(r['maximumForceDifferencePa'] for r in matrix['trajectories']),
        maximumConservationResidual=max(r['maximumConservationResidual'] for r in matrix['trajectories']),
        maximumGridVsContinuumForcePa=max(r['maximumGridVsContinuumForcePa'] for r in matrix['trajectories']),
        maximumHistoryPopulationL1=max(r['maximumPopulationL1'] for r in histories['cases']),
        maximumHistoryForceErrorPa=max(r['maximumForceErrorPa'] for r in histories['cases']),
        finestHistories=finest,originalBoundsChanged=False,newTransportExecuted=False,newHistoryExecuted=False,
        kineticsExecuted=False,finiteDomainPhysiologyQualified=False,slackTrajectoryAssessed=False,
        sensoryFiringAssessed=False,physiologyQualified=False,avatarRecoveryAssessed=False)
    output.mkdir();(output/'review.json').write_text(json.dumps(review,indent=2,allow_nan=False)+'\n',encoding='utf-8')
    print(json.dumps({k:v for k,v in review.items() if not isinstance(v,(dict,list))},indent=2))


if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    parser=argparse.ArgumentParser();parser.add_argument('--operator-reported-complete',required=True,action='store_true')
    parser.parse_args();main()

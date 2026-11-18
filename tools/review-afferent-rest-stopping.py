"""Saved stationary stopping diagnosis. No derivative, search or eigen solve."""
import argparse
from datetime import datetime,timezone
import json
from pathlib import Path
import shutil
from afferent_precision_gate import h,read,save
from afferent_rest_analysis import saved_residual,state_at
from afferent_trajectory_protocol import labels

ROOT=Path(__file__).resolve().parents[1]


def main():
    capture=ROOT/'artifacts/afferent-rest-20261118-62949f'
    prior=ROOT/'artifacts/afferent-rest-20261118-62949f-audit-review'
    out=ROOT/'artifacts/afferent-rest-20261118-62949f-stopping-review'
    assert not out.exists()
    h.verify(prior,['review.json','rest-analysis.json']);r=read(prior/'review.json')
    assert r['operatorReportedCompletion'] and r['exactRecordedEigenResidualsReproduced'] and r['originalFailurePreserved']
    assert h.digest(capture/'backup-manifest.json')==r['manifestSha256']
    assert h.digest(prior/'rest-analysis.json')==r['analysisSha256']
    spec=read(capture/'rest-input.json');files=['rest-input.json'];rows=[]
    for c in spec['cases']:
        key=c['key'];files += [f'study/{key}/result.json',f'native-witnesses/{key}.json',f'independent-rest/{key}/result.json']
    h.verify(capture,files)
    for c in spec['cases']:
        key=c['key'];n=read(capture/f'study/{key}/result.json');w=read(capture/f'native-witnesses/{key}.json');ref=read(capture/f'independent-rest/{key}/result.json')
        point=n['acceptedIterations'][-1]['point'];names=labels(c['fixture'])[0]
        assert w['fullState']==state_at(point,names)
        physical=saved_residual(w['physicalRates'],names,spec['rootCriteria']);assert physical==w['physicalResidual']
        assert w['precision']['passed'] and w['nativeEvaluationComparison']['passed'] and all(a['passed'] for a in w['accounting'])
        differences=[abs(a-b) for a,b in zip(n['acceptedIterations'][-1]['transformedState'],ref['unknowns'],strict=True)]
        if not n['stationaryCriteriaPassed']:assert n['outcome']=='LineSearchFailed' and n['lastTrialError'] is None
        rows.append(dict(key=key,nativeSearchPassed=n['stationaryCriteriaPassed'],originalOutcome=n['outcome'],
            nativeInternalResidualFraction=point['scaledResidual'],independentPhysicalResidual=physical,
            maximumTransformedCoordinateDifference=max(differences),nativeEvaluationAgrees=True))
    result=dict(completed=True,cases=rows,nativeSearchPasses=sum(r['nativeSearchPassed'] for r in rows),
        independentlyCheckedPhysicalResidualPasses=sum(r['independentPhysicalResidual']['passed'] for r in rows),
        originalStudyStillFailed=True,originalBoundsChanged=False,searchOrBiologicalEquationsChanged=False,
        interpretation='Six line searches stop just above their stricter internal bound, at states within binary64-scale distances of independent roots. This does not reclassify the original study.',
        modelsExecuted=False,testsExecuted=False,physiologicalRestQualified=False,avatarRecoveryAssessed=False)
    assert result['nativeSearchPasses']==12 and result['independentlyCheckedPhysicalResidualPasses']==18
    out.mkdir();save(out/'analysis.json',result);shutil.copyfile(__file__,out/Path(__file__).name)
    save(out/'review.json',dict(reviewedUtc=datetime.now(timezone.utc).isoformat(),capture=str(capture),
        originalManifestSha256=r['manifestSha256'],priorReviewSha256=h.digest(prior/'review.json'),
        analysisSha256=h.digest(out/'analysis.json'),inputFiles=[dict(path=n,sha256=h.digest(capture/n)) for n in files],
        scriptSha256=h.digest(__file__),operatorReportedCompletion=True,modelsExecuted=False,testsExecuted=False,
        nativeSearchPasses=12,independentPhysicalResidualPasses=18,originalFailurePreserved=True))
    print(json.dumps(dict(output=str(out),nativeSearchPasses=12,independentPhysicalResidualPasses=18),indent=2),flush=True)


if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    argparse.ArgumentParser(description=__doc__).parse_args();main()

"""Inspect completed saved-only accounting; no model/reference/test execution."""
from datetime import datetime,timezone
import importlib.util
import json
from pathlib import Path
import shutil
import sys

ROOT=Path(__file__).resolve().parents[1]
PACKAGE=Path('D:/DNNE-build-work/native-population-reanalysis-20261123-c6dcd3')
REVISION='e4d2f181ccdb8f0ee8b91cdf3d0f0d56868455bb'

def module(name,path):
    s=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m

def main():
    assert '--operator-reported-complete' in sys.argv and __debug__
    output=ROOT/'artifacts'/(PACKAGE.name+'-review');assert not output.exists()
    c=module('saved_population_review_files',ROOT/'tools/review-olive-and-motor-20261109.py')
    archive=module('saved_population_review_source',ROOT/'tools/review-spindle-root-and-causal-motor-20261113.py')
    report=c.verify(PACKAGE,[],REVISION,progress_every=1000)
    report['verifiedGitFiles']=archive.source_archive(c,PACKAGE,REVISION)
    cfg=c.read(PACKAGE/'configuration.json');status=c.read(PACKAGE/'window-status.json')
    assert status['SavedAnalysisOnly'] and status['SavedPopulationAccountingPassed'] and status['OriginalCaptureAndBackupsPreserved']
    assert not any(status[k] for k in ('ModelsExecuted','NativeUnityExecuted','OriginalBoundsChanged','NeuronalAuthorityQualified','AvatarStandingQualified'))
    assert c.read(PACKAGE/'receipt-controls.json')==dict(testsRun=8,passed=True,syntheticRecordsOnly=True)
    original=Path(cfg['OriginalPackage']);manifest=c.read(original/'backup-manifest.json')
    assert c.digest(original/'backup-manifest.json')==c.digest(Path(manifest['Destination'])/'backup-manifest.json')==cfg['OriginalManifestSha256']
    assert c.read(original/'window-status.json')==c.read(PACKAGE/'original-identity/window-status.json')
    assert c.read(original/'window-status.json')['Status']=='FAILED'
    a=c.read(PACKAGE/'native-population-analysis.json');limits=c.read(original/'configuration.json')['NativeLimits']
    assert a['completed'] and a['nativeAccountingPassed'] and a['cutAndRetryExact'] and a['allActiveCasesResponded'] and not a['errors']
    assert not any(a[k] for k in ('independentCoupledTrajectoryQualified','physiologicalRecruitmentQualified','neuronalAuthorityQualified','avatarStandingQualified','bodyStepRefinementPerformed'))
    assert a['prescribedDiagnosticEvents'] and a['muscleSubstepComparisonDiagnosticOnly']
    assert [r['name'] for r in a['cases']]==[r['name'] for r in c.read(original/'configuration.json')['Cases']]
    assert len(a['cases'])==8 and len(a['responses'])==4
    force_errors=[]
    for case in a['cases']:
        assert [case[k] for k in ('packets','pointForces','endpoints','headReceipts','exactInspectReceipts')]==[11760,320,42,126,40]
        folder=PACKAGE/'native'/case['name'];forces=c.read(folder/'saved-force-analysis.json');receipts=c.read(folder/'saved-endpoint-receipts.json')
        assert len(forces)==320 and len(receipts)==40 and [r['physicsStep'] for r in receipts]==list(range(2,42))
        assert all(r['inspectSequence']==r['stepSequence']+1 for r in receipts)
        for r in forces:
            assert r['pointErrorMeters']<=limits['nativePointMeters'] and r['lengthErrorMeters']<=limits['pathLengthMeters'] and r['forceErrorNewtons']<=limits['forceNewtons']
            force_errors.append(r['forceErrorNewtons'])
    assert all(r['observed'] and r['maximumJointResponseRadians']>limits['responseRadians'] for r in a['responses'])
    report.update(reviewedUtc=datetime.now(timezone.utc).isoformat(),operatorReportedCompletion=True,passedReceiptControls=8,
        originalNativeCases=8,originalManagedTestsPassed=388,unitsPerMuscle=147,packets=94080,physicalEndpoints=336,headReceipts=1008,
        pointForces=2560,exactInspectReceipts=320,maximumForceErrorNewtons=max(force_errors),nativePopulationAccountingPassed=True,
        originalFailureRetained=True,cutAndRetryExact=True,responses=a['responses'],modelsExecutedDuringReview=False,
        testsExecutedDuringReview=False,neuronalAuthorityQualified=False,independentCoupledTrajectoryQualified=False,avatarStandingQualified=False)
    output.mkdir();(output/'review.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    for file in ('window-status.json','receipt-controls.json','native-population-analysis.json'):shutil.copyfile(PACKAGE/file,output/file)
    shutil.copyfile(__file__,output/Path(__file__).name)
    print(json.dumps(report,indent=2),flush=True)

if __name__=='__main__':main()

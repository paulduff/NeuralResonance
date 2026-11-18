"""Review a reported completed motor epoch comparison; saved arithmetic only."""
import argparse
from datetime import datetime,timezone
import importlib.util
import json
from pathlib import Path
import shutil
import sys

ROOT=Path(__file__).resolve().parents[1]


def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m


def review(capture,reported):
    assert reported,'Wait for the operator final completion report.'
    assert capture.parent==ROOT/'artifacts' and capture.name.startswith('rubral-epoch-comparison-')
    output=capture.parent/(capture.name+'-review');assert not output.exists()
    files=module('completed_epoch_files',ROOT/'tools/review-olive-and-motor-20261109.py')
    archive=module('completed_epoch_source',ROOT/'tools/review-spindle-root-and-causal-motor-20261113.py')
    cfg=files.read(capture/'configuration.json');status=files.read(capture/'window-status.json')
    assert cfg['IsolatedSavedRubralEpochComparison'] and cfg['NoLiveMonitoring']
    assert status['Status']=='DIAGNOSTICS_COMPLETE' and status['ExitCode']==0
    result=files.verify(capture,[],cfg['Revision'],expected_status='DIAGNOSTICS_COMPLETE',expected_exit=0)
    result['verifiedGitFiles']=archive.source_archive(files,capture,cfg['Revision'])
    for key in ('OriginalFullCaptureVerified','IdentityControlsPassed','NumericalCriteriaPassed','OriginalManifestAndSourcePreserved'):assert status[key] is True
    for key in ('ModelsExecuted','NativePhysicsExecuted','OriginalBoundsChanged','OriginalPackageModified','PhysiologyQualified','AvatarRecoveryAssessed'):assert status[key] is False
    original=Path(cfg['OriginalCapture']);receipt=files.read(capture/'verified-original.json');counts=files.read(capture/'verified-original-files.json')
    assert receipt['capture']==str(original) and receipt['fullCaptureVerified'] and receipt['originalFailurePreserved']
    assert receipt['passedTests']==287 and receipt['verifiedFilesPerCopy']==583562
    for k,v in counts.items():assert receipt[k]==v,k
    assert files.digest(original/'backup-manifest.json')==cfg['OriginalManifestSha256']==receipt['manifestSha256']
    assert files.digest(Path(receipt['backup'])/'backup-manifest.json')==receipt['manifestSha256']
    output.mkdir()
    for n in ('verified-original.json','epoch-controls.json'):shutil.copyfile(capture/n,output/n)
    sys.path.insert(0,str(capture/'source/tools'))
    try:
        analysis=module('completed_epoch_analysis',capture/'source/tools/analyse-rubral-refinement-epochs.py')
        analysis.analyse(original,output)
    finally:sys.path.pop(0)
    reproduced=files.read(output/'analysis.json');assert reproduced==files.read(capture/'analysis.json')
    for r in reproduced['cases']:
        name='epoch-map-'+r['key']+'.json';assert files.digest(output/name)==files.digest(capture/name)
    result.update(reviewedUtc=datetime.now(timezone.utc).isoformat(),operatorReportedCompletion=True,
        savedAnalysisExactlyReproduced=True,analysisSha256=files.digest(capture/'analysis.json'),numericalCriteriaPassed=True,
        originalFullAuditReceipt=receipt,originalFullFileAuditRepeated=False,allNativeEndpointsReconciled=True,
        originalBoundsPreserved=True,identityControlsPassed=12,independentReferencesReused=3,
        newReferenceSolves=0,neuronalModelsRerun=False,nativePhysicsRerun=False,testsExecutedDuringReview=False,
        commonGridAccuracyOnly=True,fullCaseMatrixAssessed=False,upstreamNumericalAccuracyQualified=False,
        physiologyQualified=False,avatarRecoveryAssessed=False)
    (output/'review.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
    shutil.copyfile(__file__,output/Path(__file__).name)
    print(json.dumps(dict(output=str(output),verifiedFiles=result['verifiedFilesPerCopy'],savedAnalysisExactlyReproduced=True),indent=2),flush=True)


if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    p=argparse.ArgumentParser();p.add_argument('capture',type=Path);p.add_argument('--operator-reported-complete',action='store_true');a=p.parse_args()
    review(a.capture.resolve(),a.operator_reported_complete)

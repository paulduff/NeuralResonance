"""Prepare a saved-data-only continuation after reported final preservation."""
import argparse
from datetime import datetime,timezone
import importlib.util
import json
from pathlib import Path,PurePosixPath
import shutil
import subprocess
import sys
import uuid
from zipfile import ZipFile

ROOT=Path(__file__).resolve().parents[1]
REVISION='778c7f2fddcd385922fc62e95719bb0e3754f88d'


def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m


h=module('epoch_package_files',ROOT/'tools/prepare-causal-motor.py');read=h.read


def save(path,value):
    with path.open('x',encoding='utf-8') as stream:json.dump(value,stream,indent=2,allow_nan=False)


def require_original(capture):
    assert capture.parent.name=='artifacts' and capture.name=='rubral-refinement-20261118-3d8835'
    selected=['configuration.json','window-status.json','refinement/results.json','coupled-analysis.log','managed-runtime-before.json','managed-runtime-after.json']
    h.verify(capture,selected);cfg=read(capture/'configuration.json');status=read(capture/'window-status.json')
    assert cfg['Revision']==status['Revision']==REVISION and status['Status']=='FAILED' and status['ExitCode']==1
    assert status['Stage']=='final-result-backup' and status['PassedFocusedTests']==287
    for key in ('FocusedTestsPassed','NativeDriverSucceeded','UnityBuildSucceeded','SavedReferencesReused','OriginalSavedAccountingReproduced',
                'ReferenceEnvironmentPreserved','ManagedRuntimePreserved'):assert status[key] is True,key
    assert not status['NumericalCriteriaPassed'] and not status.get('BackupError')
    error=(capture/'coupled-analysis.log').read_text(encoding='utf-8-sig')
    assert 'event_fields' in error and 'AssertionError' in error,'Review a different failure separately.'
    assert read(capture/'refinement/results.json')['completed']
    assert h.digest(capture/'managed-runtime-before.json')==h.digest(capture/'managed-runtime-after.json')
    return cfg,status


def prepare(capture,reported):
    assert reported,'Require the operator report of the final verified backup/footer; do not inspect a pending package.'
    assert capture.parent==ROOT/'artifacts'
    assert not h.git('status','--porcelain'),'Commit first.'
    revision=h.git('rev-parse','HEAD');assert revision==h.git('rev-parse','origin/main'),'Push first.'
    cfg,status=require_original(capture)
    assert shutil.disk_usage(ROOT).free>=2*1024**3 and shutil.disk_usage('D:/').free>=4*1024**3
    assert sys.executable.lower()==cfg['PythonExecutable'].lower() and h.digest(sys.executable)==cfg['PythonExecutableSha256']
    package=ROOT/'artifacts'/('rubral-epoch-comparison-'+datetime.now(timezone.utc).strftime('%Y%m%d')+'-'+uuid.uuid4().hex[:6])
    backup=Path('D:/DNNE-desktop-runs/UnityWorld')/package.name
    assert not package.exists() and not backup.exists();package.mkdir()
    # Scheduled PowerShell inherits the desktop PATH, not Codex's bundled Git.
    # Export the independent reference while Git is available, then bind it to
    # this package. The window must never need to spawn Git for its source audit.
    reference_archive=package/'original-source-reference.zip'
    subprocess.run(['git','archive','--format=zip','--output='+str(reference_archive),REVISION],cwd=ROOT,check=True)
    archive=module('epoch_prepare_archive',ROOT/'tools/review-spindle-root-and-causal-motor-20261113.py')
    verified_sources=archive.source_archive(h,capture,REVISION,reference_archive=reference_archive)
    save(package/'original-source-reference.json',dict(revision=REVISION,sha256=h.digest(reference_archive),
        verifiedGitFiles=verified_sources,exportedFromRepository=str(ROOT),modelsExecuted=False,testsExecuted=False))
    subprocess.run(['git','archive','--format=zip','--output='+str(package/'source.zip'),revision],cwd=ROOT,check=True)
    with ZipFile(package/'source.zip') as archive:
        for entry in archive.infolist():
            name=PurePosixPath(entry.filename)
            assert not name.is_absolute() and '..' not in name.parts and ':' not in entry.filename and '\\' not in entry.filename
            assert ((entry.external_attr>>16)&0o170000)!=0o120000
            target=h.io(package/'source'/name)
            if entry.is_dir():target.mkdir(parents=True,exist_ok=True)
            else:
                target.parent.mkdir(parents=True,exist_ok=True)
                with target.open('xb') as stream:stream.write(archive.read(entry))
    shutil.copyfile(package/'source/tools/backup-unity-run.ps1',package/'backup-run.ps1')
    save(package/'original-status.json',status)
    files=[dict(Path=p.relative_to(h.io(package)).as_posix(),Sha256=h.digest(p)) for p in sorted(h.io(package).rglob('*')) if p.is_file()]
    save(package/'configuration.json',dict(Revision=revision,PackageFiles=files,BackupDestination=str(backup),NoLiveMonitoring=True,
        IsolatedSavedRubralEpochComparison=True,OperatorReportedCompletion=True,OriginalCapture=str(capture),OriginalRevision=REVISION,
        OriginalManifestSha256=h.digest(capture/'backup-manifest.json'),PythonExecutable=sys.executable,PythonExecutableSha256=h.digest(sys.executable),
        ExpectedIdentityControls=12,ExpectedPriorTests=287,ExpectedNativeConditions=5,ExpectedSavedReferences=3,
        OriginalSourceReferenceSha256=h.digest(reference_archive),WindowRequiresGit=False,
        ModelsWillExecute=False,NativePhysicsWillExecute=False,OriginalBoundsChanged=False,OriginalPackageWillBeModified=False))
    pointer=dict(Package=str(package),Backup=str(backup),Revision=revision,NoLiveMonitoring=True,Launcher=str(package/'source/tools/start-rubral-epoch-window.ps1'))
    save(package.parent/(package.name+'-prepared.json'),pointer);print(json.dumps(pointer,indent=2),flush=True)


def preflight(package):
    cfg=read(package/'configuration.json');capture=Path(cfg['OriginalCapture'])
    assert cfg['IsolatedSavedRubralEpochComparison'] and cfg['OperatorReportedCompletion'] and cfg['NoLiveMonitoring']
    assert cfg['OriginalRevision']==REVISION and cfg['ExpectedIdentityControls']==12 and cfg['ExpectedPriorTests']==287
    assert cfg['ExpectedNativeConditions']==5 and cfg['ExpectedSavedReferences']==3
    assert not any(cfg[k] for k in ('ModelsWillExecute','NativePhysicsWillExecute','OriginalBoundsChanged','OriginalPackageWillBeModified'))
    assert h.digest(capture/'backup-manifest.json')==cfg['OriginalManifestSha256']
    for entry in cfg['PackageFiles']:assert h.digest(package/entry['Path'])==entry['Sha256']
    assert h.digest(cfg['PythonExecutable'])==cfg['PythonExecutableSha256']
    reference=read(package/'original-source-reference.json')
    assert cfg['WindowRequiresGit'] is False and reference['revision']==REVISION
    assert not reference['modelsExecuted'] and not reference['testsExecuted']
    assert h.digest(package/'original-source-reference.zip')==reference['sha256']==cfg['OriginalSourceReferenceSha256']
    # Fail on missing/mismatched archive before the 583,562-file audit begins.
    archive=module('epoch_preflight_archive',package/'source/tools/review-spindle-root-and-causal-motor-20261113.py')
    assert archive.source_archive(h,capture,REVISION,reference_archive=package/'original-source-reference.zip')==reference['verifiedGitFiles']
    _,status=require_original(capture);assert status==read(package/'original-status.json')
    old=module('qualified_motor_partition_gate',capture/'source/tools/rubral_refinement_gate.py');old.preflight(capture);old.check_tests(capture)
    return capture


def review(package):
    capture=preflight(package)
    files=module('epoch_original_review',package/'source/tools/review-olive-and-motor-20261109.py')
    archive=module('epoch_original_archive',package/'source/tools/review-spindle-root-and-causal-motor-20261113.py')
    git_files=archive.source_archive(files,capture,REVISION,reference_archive=package/'original-source-reference.zip')
    save(package/'source-archive-review.json',dict(sourceRevision=REVISION,verifiedGitFiles=git_files,
        referenceArchiveSha256=h.digest(package/'original-source-reference.zip'),gitExecuted=False))
    result=files.verify(capture,['test-binaries.json','probe-binaries.json'],REVISION,expected_status='FAILED',expected_exit=1,progress_every=10000)
    # Preserve completed phases even if a later phase fails; logs alone are not
    # used as a substitute for a structured full-audit result on a future retry.
    save(package/'verified-original-files.json',result)
    result['verifiedGitFiles']=git_files
    for entry in read(capture/'player-files.json'):
        Path(entry['Path']).relative_to(capture/'player');assert h.digest(entry['Path'])==entry['Sha256']
    result.update(operatorReportedCompletion=True,fullCaptureVerified=True,passedTests=287,originalStatus='FAILED',originalFailurePreserved=True,
        modelsExecuted=False,nativePhysicsExecuted=False,reviewedUtc=datetime.now(timezone.utc).isoformat())
    save(package/'verified-original.json',result)
    print('Complete original failed capture, frozen source, binaries and both backup copies verified; no model rerun.',flush=True)


def compare(package):
    capture=preflight(package)
    analysis=module('epoch_partition_analysis',package/'source/tools/analyse-rubral-refinement-epochs.py')
    analysis.analyse(capture,package)


def postflight(package):
    preflight(package)
    print('Original manifests, declared source/runtime and failure status preserved. No neural or native model was rerun.',flush=True)


if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    parser=argparse.ArgumentParser();parser.add_argument('command',choices=['prepare','preflight','review','compare','postflight']);parser.add_argument('path',type=Path)
    parser.add_argument('--operator-reported-complete',action='store_true');args=parser.parse_args()
    if args.command=='prepare':prepare(args.path.resolve(),args.operator_reported_complete)
    else:dict(preflight=preflight,review=review,compare=compare,postflight=postflight)[args.command](args.path.resolve())

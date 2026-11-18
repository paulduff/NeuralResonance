"""Visible recording-format qualification on reported, reviewed motor evidence."""
import argparse
from datetime import datetime,timezone
import hashlib
import importlib.util
import json
from pathlib import Path,PurePosixPath
import shutil
import subprocess
import sys
import uuid
from zipfile import ZipFile
import numpy as np
from afferent_precision_gate import h,read,save
from cleft_trajectory_reference import base
from native_evidence_archive import EvidenceArchive,pack,digest_file

ROOT=Path(__file__).resolve().parents[1]
KEYS=('repeat','body-finer')


def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m


def prepare(review_folder):
    assert not h.git('status','--porcelain'),'Commit first.'
    revision=h.git('rev-parse','HEAD');assert revision==h.git('rev-parse','origin/main'),'Push first.'
    assert shutil.disk_usage('D:/').free>=8*1024**3
    h.verify(review_folder,['review.json','analysis.json']);review=read(review_folder/'review.json')
    assert review['operatorReportedCompletion'] and review['savedAnalysisExactlyReproduced'] and review['numericalCriteriaPassed']
    original=review['originalFullAuditReceipt'];capture=Path(original['capture']);backup=Path(original['backup'])
    assert original['fullCaptureVerified'] and original['originalFailurePreserved'] and original['passedTests']==287
    assert h.digest(capture/'backup-manifest.json')==h.digest(backup/'backup-manifest.json')==original['manifestSha256']
    h.verify(capture,['preparation-environment.json'])
    assert base.environment()==read(capture/'preparation-environment.json')
    manifest=read(backup/'backup-manifest.json');analysis=read(review_folder/'analysis.json')
    assert h.digest(review_folder/'analysis.json')==review['analysisSha256']
    parent=Path('D:/DNNE-build-work');parent.mkdir(exist_ok=True)
    package=parent/('native-evidence-'+datetime.now(timezone.utc).strftime('%Y%m%d')+'-'+uuid.uuid4().hex[:6])
    destination=Path('D:/DNNE-desktop-runs/UnityWorld')/package.name
    assert not package.exists() and not destination.exists();package.mkdir()
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
    for source,target in [(review_folder/'review.json','reviewed-motor.json'),(review_folder/'analysis.json','reviewed-analysis.json'),
        (package/'source/tools/backup-unity-run.ps1','backup-run.ps1'),(capture/'preparation-environment.json','preparation-environment.json')]:
        shutil.copyfile(h.io(source),h.io(package/target));assert h.digest(source)==h.digest(package/target)
    # Decode-only reader from the reviewed source is frozen separately. No new
    # reference or equation implementation is used for this format comparison.
    reader_paths=['tools/analyse-native-hinge-comparison.py','tools/native_hinge_protocol.py']
    reader_sources=[]
    for n in reader_paths:
        h.verify(capture,['source/'+n]);target=package/'reviewed-reader'/Path(n).name;target.parent.mkdir(exist_ok=True)
        shutil.copyfile(h.io(capture/'source'/n),target)
        assert (ROOT/n).read_text(encoding='utf-8-sig')==target.read_text(encoding='utf-8-sig'),n
        reader_sources.append(dict(path=n,sha256=h.digest(target)))
    cases=[]
    for key in KEYS:
        prefix='refinement/'+key+'/native-bridge/strong-left/'
        records=sorted([dict(path=r['Path'].replace('\\','/')[len(prefix):],bytes=r['Bytes'],sha256=r['Sha256'])
            for r in manifest['Files'] if r['Path'].replace('\\','/').startswith(prefix)],key=lambda r:r['path'])
        assert records and len({r['path'] for r in records})==len(records)
        initial=read(backup/prefix/'initial.json');step=initial['clock']['physicsStepMs'];steps=initial['actualSteps']
        mapping=next(r['epochIdentityMapping'] for r in analysis['cases'] if r['key']==key)
        assert mapping['passed'] and mapping['current']['session']==initial['session']
        fixture=dict(key='strong-left',durationMs=steps*step,physicalStepMs=step,physicalSteps=steps,
            initialAngleRadians=0,initialSpeedRadiansPerSecond=0,events=mapping['current']['recordedEvents'])
        save(package/(key+'-expected.json'),records)
        cases.append(dict(key=key,source=str(backup/prefix),expected=key+'-expected.json',fixture=fixture))
    save(package/'protocol.json',dict(version='native-evidence-format-comparison-v1',cases=cases,expectedControls=24,
        originalManifestSha256=original['manifestSha256'],readerSources=reader_sources,
        sourceRecordsRemain=True,liveCaptureConsumerChanged=False,modelsExecuted=False,testsExecutedDuringPreparation=False))
    files=[dict(Path=p.relative_to(h.io(package)).as_posix(),Sha256=h.digest(p)) for p in sorted(h.io(package).rglob('*')) if p.is_file()]
    save(package/'configuration.json',dict(Revision=revision,PackageFiles=files,BackupDestination=str(destination),
        IsolatedNativeEvidence=True,OperatorReportedCompletion=True,NoLiveMonitoring=True,ExpectedControls=24,ExpectedCaptures=2,
        PythonExecutable=sys.executable,PythonExecutableSha256=h.digest(sys.executable),OriginalCapture=str(capture),
        ModelsWillExecute=False,NativePhysicsWillExecute=False,OriginalPackageWillBeModified=False,ArchiveCopiesShareDrive=True))
    pointer=dict(Package=str(package),Backup=str(destination),Revision=revision,NoLiveMonitoring=True,
        Launcher=str(package/'source/tools/start-native-evidence-window.ps1'))
    save(ROOT/'artifacts'/(package.name+'-prepared.json'),pointer);print(json.dumps(pointer,indent=2),flush=True)


def preflight(package):
    cfg=read(package/'configuration.json');p=read(package/'protocol.json');review=read(package/'reviewed-motor.json')
    assert cfg['IsolatedNativeEvidence'] and cfg['NoLiveMonitoring'] and cfg['OperatorReportedCompletion']
    assert cfg['ExpectedControls']==p['expectedControls']==24 and cfg['ExpectedCaptures']==len(p['cases'])==2
    assert tuple(r['key'] for r in p['cases'])==KEYS and p['sourceRecordsRemain'] and not p['liveCaptureConsumerChanged']
    for k in ('ModelsWillExecute','NativePhysicsWillExecute','OriginalPackageWillBeModified'):assert not cfg[k]
    for row in cfg['PackageFiles']:assert h.digest(package/row['Path'])==row['Sha256']
    assert h.digest(cfg['PythonExecutable'])==cfg['PythonExecutableSha256']
    assert review['operatorReportedCompletion'] and review['savedAnalysisExactlyReproduced'] and review['numericalCriteriaPassed']
    original=review['originalFullAuditReceipt']
    assert h.digest(Path(original['capture'])/'backup-manifest.json')==h.digest(Path(original['backup'])/'backup-manifest.json')==p['originalManifestSha256']==original['manifestSha256']
    assert h.digest(package/'reviewed-analysis.json')==review['analysisSha256']
    manifest=read(Path(original['backup'])/'backup-manifest.json')
    for case in p['cases']:
        prefix='refinement/'+case['key']+'/native-bridge/strong-left/'
        expected=sorted([dict(path=r['Path'].replace('\\','/')[len(prefix):],bytes=r['Bytes'],sha256=r['Sha256'])
            for r in manifest['Files'] if r['Path'].replace('\\','/').startswith(prefix)],key=lambda r:r['path'])
        assert expected==read(package/case['expected']) and Path(case['source'])==Path(original['backup'])/prefix
    for row in p['readerSources']:
        assert h.digest(package/'reviewed-reader'/Path(row['path']).name)==row['sha256']
        assert (package/'source'/row['path']).read_text(encoding='utf-8-sig')==(package/'reviewed-reader'/Path(row['path']).name).read_text(encoding='utf-8-sig')
    assert base.environment()==read(package/'preparation-environment.json')
    print('Reviewed motor outcome, original manifests, decode-only reader and numerical dependencies preserved; no model/test executed.',flush=True)
    return p


def controls(package):
    preflight(package);test=module('visible_native_evidence_controls',package/'source/tools/check-native-evidence-archive.py')
    test.run(package/'controls')


def captures(package):
    p=preflight(package);control=read(package/'controls/controls.json');assert control['completed'] and control['passed']==control['expected']==24
    assert all(r['passed'] for r in control['cases']) and not control['modelsExecuted']
    reader=module('reviewed_native_evidence_reader',package/'reviewed-reader/analyse-native-hinge-comparison.py')
    reports=[];folder=package/'captures';folder.mkdir()
    for case in p['cases']:
        key=case['key'];print('ARCHIVE COMPLETED MOTOR CAPTURE '+key,flush=True)
        assert shutil.disk_usage(package).free>=4*1024**3,'Preserve all sources and stop before recording if space is insufficient.'
        result=pack(Path(case['source']),folder/(key+'.zip'),read(package/case['expected']),
            lambda phase,index,total:print(key,phase,index,'/',total,flush=True))
        save(folder/(key+'-archive.json'),result)
        print('COMPARE ORIGINAL AND ARCHIVED DECODED ENDPOINTS '+key,flush=True)
        raw=reader.candidate_data(Path(case['source']),case['fixture'],allowed_steps=(case['fixture']['physicalSteps'],))
        with EvidenceArchive(folder/(key+'.zip'),result['archiveSha256']) as archive:
            decoded=reader.candidate_data(archive.root,case['fixture'],allowed_steps=(case['fixture']['physicalSteps'],))
        assert set(raw)==set(decoded);columns={}
        for name,value in raw.items():
            if isinstance(value,np.ndarray):
                other=decoded[name];assert isinstance(other,np.ndarray) and value.dtype==other.dtype and value.shape==other.shape
                assert value.tobytes()==other.tobytes(),name
                columns[name]=dict(shape=value.shape,dtype=str(value.dtype),sha256=hashlib.sha256(value.tobytes()).hexdigest().upper())
            else:assert value==decoded[name],name
        report=dict(key=key,sourceFiles=result['files'],sourceBytes=result['uncompressedBytes'],archiveBytes=result['archiveBytes'],
            archiveSha256=result['archiveSha256'],allFileBytesPreserved=True,decodedArraysBitwiseEqual=True,arrayDigests=columns,
            physicalSamples=case['fixture']['physicalSteps']+1,sourceFilesRemoved=0)
        save(folder/(key+'-comparison.json'),report);reports.append(report)
    save(package/'analysis.json',dict(completed=True,recordingCriteriaPassed=True,controlsPassed=24,cases=reports,
        originalSourcesModified=False,modelsExecuted=False,nativePhysicsExecuted=False,liveCaptureConsumerChanged=False,
        fullMotorMatrixQualified=False,physiologyQualified=False,avatarRecoveryAssessed=False))


if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    p=argparse.ArgumentParser();p.add_argument('command',choices=['prepare','preflight','postflight','controls','captures']);p.add_argument('path',type=Path);a=p.parse_args()
    dict(prepare=prepare,preflight=preflight,postflight=preflight,controls=controls,captures=captures)[a.command](a.path.resolve())

"""Freeze dimensional vestibular bundle input and causal physical-interval controls."""
import argparse
from datetime import datetime,timezone
from pathlib import Path,PurePosixPath
import json
import shutil
import subprocess
import sys
import uuid
from zipfile import ZipFile
import afferent_ingress_gate as prior
from afferent_trajectory_gate import inventory
from cleft_trajectory_reference import base

ROOT=Path(__file__).resolve().parents[1];h=prior.h;read=prior.read;save=prior.save
COUNTS=dict(prior.COUNTS,VestibularBundleInputTests=25,ClockedVestibularMechanicsTests=16,SkullKinematicsTests=12)
PRESERVED=sorted(set(prior.PRESERVED+[
    'src/NRE.SimAvatar/AvatarClockedVestibularMechanics.cs','src/NRE.SimAvatar/AvatarSkullKinematics.cs',
    'src/NRE.SimAvatar/AvatarAfferentCrossing.cs','src/NRE.SimAvatar/AvatarAfferentEventObserver.cs',
    'src/NRE.SimAvatar/AvatarAfferentPacketSource.cs','Protocol/ObservedAfferentPacket.cs',
    'Structures/_SharedRuntime/IsolatedAfferentIngress.cs','Structures/_SharedRuntime/StructureEngine.cs']))


def prepare(review_folder):
    assert not h.git('status','--porcelain'),'Commit first.'
    revision=h.git('rev-parse','HEAD');assert revision==h.git('rev-parse','origin/main'),'Push first.'
    assert shutil.disk_usage(ROOT).free>=4*1024**3 and shutil.disk_usage('D:/').free>=8*1024**3
    h.verify(review_folder,['review.json']);review=read(review_folder/'review.json')
    assert review['operatorReportedCompletion'] and review['savedAnalysisExactlyReproduced'] and review['componentCriteriaPassed'] and review['observedIngressChecksPassed'] and review['passedTests']==264
    capture=Path(review['capture']);assert h.digest(capture/'backup-manifest.json')==review['manifestSha256']
    evidence=['managed-runtime-before.json','preparation-environment.json','analysis.json','source-decisions.json']
    h.verify(capture,evidence+['source/'+name for name in PRESERVED])
    assert h.digest(capture/'analysis.json')==review['analysisSha256']
    for name in PRESERVED:assert (ROOT/name).read_text(encoding='utf-8-sig')==h.io(capture/'source'/name).read_text(encoding='utf-8-sig'),name
    assert base.environment()==read(capture/'preparation-environment.json')
    methods=inventory.inventory(ROOT,COUNTS);assert sum(methods.values())==317
    package=ROOT/'artifacts'/('vestibular-bundle-'+datetime.now(timezone.utc).strftime('%Y%m%d')+'-'+uuid.uuid4().hex[:6]);backup=Path('D:/DNNE-desktop-runs/UnityWorld')/package.name
    assert not package.exists() and not backup.exists();package.mkdir()
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
    def copy(source,name):
        target=h.io(package/name);target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(h.io(source),target);assert h.digest(source)==h.digest(target)
    copy(review_folder/'review.json','qualified-ingress-review.json')
    for name in evidence:copy(capture/name,{'managed-runtime-before.json':'qualified-managed-runtime.json','analysis.json':'qualified-ingress-analysis.json'}.get(name,name))
    copy(package/'source/tools/backup-unity-run.ps1','backup-run.ps1')
    save(package/'provenance.json',dict(originalRevision=review['sourceRevision'],originalManifestSha256=review['manifestSha256'],
        preservedSources=[dict(path=n,sha256=h.digest(capture/'source'/n)) for n in PRESERVED],
        pendingCapturesRead=False,originalBiologyChanged=False,modelsExecutedDuringPreparation=False,testsExecutedDuringPreparation=False))
    files=[dict(Path=p.relative_to(h.io(package)).as_posix(),Sha256=h.digest(p)) for p in sorted(h.io(package).rglob('*')) if p.is_file()]
    dotnet=shutil.which('dotnet');assert dotnet
    save(package/'configuration.json',dict(Revision=revision,PreparedUtc=datetime.now(timezone.utc).isoformat(),PackageFiles=files,
        DotnetExecutable=dotnet,DotnetExecutableSha256=h.digest(dotnet),PythonExecutable=sys.executable,PythonExecutableSha256=h.digest(sys.executable),
        BackupDestination=str(backup),IsolatedVestibularBundleInput=True,MechanicalCalibrationQualified=False,NoLiveMonitoring=True,ExpectedTestCases=317,ExpectedClassCounts=COUNTS,
        ExpectedTestMethods=methods,TestFilter='|'.join('FullyQualifiedName~'+c for c in COUNTS),IndependentCoupledTrajectoryQualified=False,
        StationaryInitializationQualified=False,SpatialGeometryCalibrated=False,AfferentFiringQualified=False,PhysiologyQualified=False,
        AvatarRecoveryAssessed=False,LiveSelectionChanged=False))
    pointer=dict(Package=str(package),Backup=str(backup),Revision=revision,NoLiveMonitoring=True,Launcher=str(package/'source/tools/start-vestibular-bundle-window.ps1'))
    save(package.parent/(package.name+'-prepared.json'),pointer);print(json.dumps(pointer,indent=2),flush=True)


def preflight(package):
    cfg=read(package/'configuration.json');review=read(package/'qualified-ingress-review.json');provenance=read(package/'provenance.json')
    assert not cfg['MechanicalCalibrationQualified']
    assert cfg['IsolatedVestibularBundleInput'] and cfg['NoLiveMonitoring'] and cfg['ExpectedTestCases']==317 and cfg['ExpectedClassCounts']==COUNTS
    assert review['sourceRevision']==provenance['originalRevision'] and review['manifestSha256']==provenance['originalManifestSha256']
    assert review['operatorReportedCompletion'] and review['savedAnalysisExactlyReproduced'] and review['componentCriteriaPassed'] and review['observedIngressChecksPassed'] and review['passedTests']==264
    assert h.digest(package/'qualified-ingress-analysis.json')==review['analysisSha256']
    assert not any(provenance[k] for k in ('pendingCapturesRead','originalBiologyChanged','modelsExecutedDuringPreparation','testsExecutedDuringPreparation'))
    assert {r['path'] for r in provenance['preservedSources']}==set(PRESERVED)
    for row in provenance['preservedSources']:assert h.digest(package/'source'/row['path'])==row['sha256']
    assert base.environment()==read(package/'preparation-environment.json')
    for directory in read(package/'qualified-managed-runtime.json')['directories']:
        root=h.io(directory['path']);assert {str(p) for p in root.rglob('*') if p.is_file()}=={str(h.io(e['path'])) for e in directory['files']}
        for row in directory['files']:assert h.digest(row['path'])==row['sha256']
    print('Reviewed skull/mechanical kernels, observed ingress, neuronal equations and numerical/runtime dependencies preserved; no model or test executed.',flush=True)


def runtime(package):
    preflight(package);assert read(package/'managed-runtime-before.json')==read(package/'qualified-managed-runtime.json')


def check_tests(package):
    preflight(package);result=inventory.check_tests(package,COUNTS)
    save(package/'analysis.json',dict(completed=True,componentChecksPassed=True,tests=result,bundleInputChecksPassed=True,mechanicalCalibrationQualified=False,
        independentCoupledTrajectoryQualified=False,stationaryInitializationQualified=False,spatialGeometryCalibrated=False,
        afferentFiringQualified=False,physiologyQualified=False,avatarRecoveryAssessed=False))
    print(json.dumps(result,indent=2),flush=True)


if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    p=argparse.ArgumentParser();p.add_argument('command',choices=['prepare','preflight','postflight','runtime','check-tests']);p.add_argument('path',type=Path)
    a=p.parse_args();dict(prepare=prepare,preflight=preflight,postflight=preflight,runtime=runtime,**{'check-tests':check_tests})[a.command](a.path.resolve())

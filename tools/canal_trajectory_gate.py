"""Freeze published canal comparison; authoring never executes a model/test."""
import argparse
from datetime import datetime, timezone
import decimal
import _decimal
import json
from pathlib import Path, PurePosixPath
import shutil
import subprocess
import sys
import uuid
from zipfile import ZipFile
from afferent_precision_gate import h, read, save
from cleft_trajectory_reference import base
from coupled_canal_protocol import protocol

ROOT=Path(__file__).resolve().parents[1]
PRESERVED=['src/NRE.SimAvatar/AvatarCoupledCanalParameters.cs','src/NRE.SimAvatar/AvatarCoupledCanalDynamics.cs',
    'src/NRE.SimAvatar/AvatarCleftTransport.cs','src/NRE.SimAvatar/NRE.SimAvatar.csproj',
    'docs/reference-data/vestibular-ifediba-2007.json',
    *['tests/NeuralResonanceEngine.DNNE.Tests/'+name+'.cs' for name in
      ('CoupledCanalMechanicsTests','ClockedVestibularMechanicsTests','SkullKinematicsTests','VestibularBundleInputTests')]]


def decimal_runtime():
    return dict(version=decimal.__version__,libmpdec=decimal.__libmpdec_version__,
        files=[dict(path=str(Path(module.__file__).resolve()),sha256=h.digest(module.__file__)) for module in (decimal,_decimal)])


def prepare(review_folder):
    assert not h.git('status','--porcelain'),'Commit first.'
    revision=h.git('rev-parse','HEAD');assert revision==h.git('rev-parse','origin/main'),'Push first.'
    assert shutil.disk_usage('D:/').free>=20*1024**3,'Require 20 GiB for complete stage/reference records, build and backup.'
    h.verify(review_folder,['review.json','analysis.json']);review=read(review_folder/'review.json');capture=Path(review['capture'])
    assert review['operatorReportedCompletion'] and review['componentCriteriaPassed'] and review['coupledCanalChecksPassed'] and review['passedTests']==76
    assert h.digest(capture/'backup-manifest.json')==review['manifestSha256']
    h.verify(capture,['source/'+name for name in PRESERVED]+['managed-runtime-before.json'])
    for name in PRESERVED:assert (ROOT/name).read_text(encoding='utf-8-sig')==(capture/'source'/name).read_text(encoding='utf-8-sig'),name
    package=Path('D:/DNNE-build-work')/('canal-trajectories-'+datetime.now(timezone.utc).strftime('%Y%m%d')+'-'+uuid.uuid4().hex[:6])
    backup=Path('D:/DNNE-desktop-runs/UnityWorld')/package.name
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
        shutil.copyfile(source,package/name);assert h.digest(source)==h.digest(package/name)
    copy(review_folder/'review.json','reviewed-components.json')
    copy(review_folder/'analysis.json','reviewed-component-analysis.json')
    copy(capture/'managed-runtime-before.json','qualified-managed-runtime.json')
    copy(package/'source/tools/backup-unity-run.ps1','backup-run.ps1')
    save(package/'trajectory-input.json',protocol());save(package/'preparation-environment.json',base.environment())
    save(package/'decimal-runtime.json',decimal_runtime())
    save(package/'provenance.json',dict(originalManifestSha256=review['manifestSha256'],originalRevision=review['sourceRevision'],
        preservedSources=[dict(path=name,sha256=h.digest(capture/'source'/name)) for name in PRESERVED],
        componentCasesReused=76,componentTestsWillRerun=False,biologicalParametersChanged=False,modelsExecutedDuringPreparation=False,
        testsExecutedDuringPreparation=False,referenceMethod='Decimal Taylor scaling/squaring at 60/90 digits; independent SciPy expm',
        nativeValuesSeedReferences=False,sourceToSkullFrameQualified=False,localBundleMapQualified=False))
    files=[dict(Path=p.relative_to(h.io(package)).as_posix(),Sha256=h.digest(p)) for p in sorted(h.io(package).rglob('*')) if p.is_file()]
    dotnet=shutil.which('dotnet');assert dotnet
    save(package/'configuration.json',dict(Revision=revision,PackageFiles=files,BackupDestination=str(backup),
        DotnetExecutable=dotnet,DotnetExecutableSha256=h.digest(dotnet),PythonExecutable=sys.executable,PythonExecutableSha256=h.digest(sys.executable),
        IsolatedCanalTrajectories=True,NoLiveMonitoring=True,ExpectedNativeRuns=45,ExpectedReferenceCases=15,ExpectedReferenceControls=6,
        ComponentTestsReused=76,FullUnitSuiteWillRun=False,LiveSelectionChanged=False,PhysiologyQualified=False,AvatarRecoveryAssessed=False,
        ArchiveCopiesShareDrive=True))
    pointer=dict(Package=str(package),Backup=str(backup),Revision=revision,NoLiveMonitoring=True,Launcher=str(package/'source/tools/start-canal-trajectory-window.ps1'))
    save(ROOT/'artifacts'/(package.name+'-prepared.json'),pointer);print(json.dumps(pointer,indent=2),flush=True)


def preflight(package):
    cfg=read(package/'configuration.json');p=read(package/'provenance.json');review=read(package/'reviewed-components.json')
    assert cfg['IsolatedCanalTrajectories'] and cfg['NoLiveMonitoring'] and cfg['ExpectedNativeRuns']==45 and cfg['ExpectedReferenceCases']==15
    assert cfg['ExpectedReferenceControls']==6 and cfg['ComponentTestsReused']==p['componentCasesReused']==review['passedTests']==76
    assert review['operatorReportedCompletion'] and review['componentCriteriaPassed'] and review['coupledCanalChecksPassed']
    assert h.digest(package/'reviewed-component-analysis.json')==review['analysisSha256']
    assert review['sourceRevision']==p['originalRevision'] and review['manifestSha256']==p['originalManifestSha256']
    assert {r['path'] for r in p['preservedSources']}==set(PRESERVED)
    for row in p['preservedSources']:assert h.digest(package/'source'/row['path'])==row['sha256']
    assert read(package/'trajectory-input.json')==protocol()
    for row in cfg['PackageFiles']:assert h.digest(package/row['Path'])==row['Sha256'],row['Path']
    for tool in ('Dotnet','Python'):assert h.digest(cfg[tool+'Executable'])==cfg[tool+'ExecutableSha256']
    assert base.environment()==read(package/'preparation-environment.json') and decimal_runtime()==read(package/'decimal-runtime.json')
    for directory in read(package/'qualified-managed-runtime.json')['directories']:
        root=h.io(directory['path']);assert {str(f) for f in root.rglob('*') if f.is_file()}=={str(h.io(r['path'])) for r in directory['files']}
        for row in directory['files']:assert h.digest(row['path'])==row['sha256']
    print('Reviewed mechanical kernels, 76-case component evidence, source catalog and numerical/runtime dependencies preserved; no model/test executed.',flush=True)


def runtime(package):
    preflight(package);assert read(package/'managed-runtime-before.json')==read(package/'qualified-managed-runtime.json')


def storage(package):
    required=10*1024**3;available=shutil.disk_usage(package).free
    save(package/('storage-'+str(len(list(package.glob('storage-*.json')))+1)+'.json'),dict(requiredBytes=required,availableBytes=available,passed=available>=required))
    assert available>=required,'Preserve current output and stop before a new phase; 10 GiB reserve required.'


def controls(package):
    preflight(package)
    from coupled_canal_reference import controls as run
    run(package/'reference-controls')


def reference(package):
    runtime(package)
    control=read(package/'reference-controls/controls.json')
    assert control['completed'] and control['expected']==control['passed']==6
    from coupled_canal_reference import run
    run(package)


def compare(package):
    runtime(package)
    from coupled_canal_analysis import analyse
    result=analyse(package,protocol());save(package/'trajectory-analysis.json',result)
    if not result['numericalCriteriaPassed']:raise RuntimeError('Published canal trajectory criteria failed; retain complete records and declared bounds.')


if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    parser=argparse.ArgumentParser();parser.add_argument('command',choices=['prepare','preflight','postflight','runtime','storage','controls','reference','compare'])
    parser.add_argument('path',type=Path);args=parser.parse_args()
    dict(prepare=prepare,preflight=preflight,postflight=preflight,runtime=runtime,storage=storage,controls=controls,reference=reference,compare=compare)[args.command](args.path.resolve())

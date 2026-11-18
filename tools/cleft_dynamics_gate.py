"""Freeze isolated joint sensory dynamics component tests; no inline execution."""
import argparse
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path, PurePosixPath
import shutil
import subprocess
import sys
import uuid
from zipfile import ZipFile
from cleft_membrane_protocol import COUNTS as MEMBRANE_COUNTS

ROOT=Path(__file__).resolve().parents[1]
COUNTS=dict(MEMBRANE_COUNTS,VestibularCleftDynamicsTests=12)
REVISION='274214bc6c44538fbf0312ef730dcac843d437e3'
PRESERVED=['src/NRE.SimAvatar/'+n+'.cs' for n in ('AvatarVestibularHairCell','AvatarCalyxMembrane',
    'AvatarCleftTransport','AvatarCleftChargeBalance','AvatarCleftSpatialCoupling','AvatarCleftOpenBoundary','AvatarVestibularCleftMembranes')]
PRESERVED+=['tests/NeuralResonanceEngine.DNNE.Tests/'+n+'.cs' for n in MEMBRANE_COUNTS if n!='SolutionManifestTests']


def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m


h=module('cleft_dynamic_files',ROOT/'tools/prepare-causal-motor.py')
inventory=module('cleft_dynamic_inventory',ROOT/'tools/receptor_membrane_gate.py')
read=h.read
def save(path,value):
    with path.open('x',encoding='utf-8') as stream:json.dump(value,stream,indent=2,allow_nan=False)


def prepare(review_folder):
    assert not h.git('status','--porcelain'),'Commit first.'
    revision=h.git('rev-parse','HEAD');assert revision==h.git('rev-parse','origin/main'),'Push first.'
    h.verify(review_folder,['review.json']);review=read(review_folder/'review.json')
    assert review['operatorReportedCompletion'] and review['savedAnalysisExactlyReproduced'] and review['independentSavedComparisonsRechecked']
    assert review['numericalCriteriaPassed'] and review['passedTests']==108 and review['sourceRevision']==REVISION
    assert review['analysis']['membraneRows']==216 and review['analysis']['regressionRecordsBitwisePreserved']
    capture=Path(review['capture']);assert h.digest(capture/'backup-manifest.json')==review['manifestSha256']
    h.verify(capture,['source/'+name for name in PRESERVED]+['managed-runtime-before.json'])
    for name in PRESERVED:assert (ROOT/name).read_text(encoding='utf-8-sig')==h.io(capture/'source'/name).read_text(encoding='utf-8-sig'),name
    methods=inventory.inventory(ROOT,COUNTS);assert sum(methods.values())==120
    package=ROOT/'artifacts'/('cleft-dynamics-'+datetime.now(timezone.utc).strftime('%Y%m%d')+'-'+uuid.uuid4().hex[:6])
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
    for name in PRESERVED:assert h.digest(package/'source'/name)==h.digest(capture/'source'/name),name
    shutil.copyfile(review_folder/'review.json',package/'qualified-membrane-review.json')
    shutil.copyfile(capture/'managed-runtime-before.json',package/'qualified-managed-runtime.json')
    shutil.copyfile(package/'source/tools/backup-unity-run.ps1',package/'backup-run.ps1')
    save(package/'provenance.json',dict(preservedSources=[dict(path=name,sha256=h.digest(capture/'source'/name)) for name in PRESERVED],
        originalManifestSha256=review['manifestSha256'],qualifiedManagedRuntimeSha256=h.digest(capture/'managed-runtime-before.json'),
        modelsExecutedDuringPreparation=False,testsExecutedDuringPreparation=False,pendingCapturesRead=False))
    files=[dict(Path=p.relative_to(h.io(package)).as_posix(),Sha256=h.digest(p)) for p in sorted(h.io(package).rglob('*')) if p.is_file()]
    dotnet=shutil.which('dotnet');assert dotnet
    save(package/'configuration.json',dict(Revision=revision,PreparedUtc=datetime.now(timezone.utc).isoformat(),PackageFiles=files,
        DotnetExecutable=dotnet,DotnetExecutableSha256=h.digest(dotnet),PythonExecutable=sys.executable,PythonExecutableSha256=h.digest(sys.executable),
        BackupDestination=str(backup),IsolatedCleftDynamics=True,NoLiveMonitoring=True,ExpectedTestCases=120,ExpectedClassCounts=COUNTS,
        ExpectedTestMethods=methods,TestFilter='|'.join('FullyQualifiedName~'+c for c in COUNTS),
        IndependentCoupledTrajectoryQualified=False,StationaryInitializationQualified=False,LiveSelectionChanged=False,
        SpatialGeometryCalibrated=False,AfferentFiringQualified=False,PhysiologyQualified=False,AvatarRecoveryAssessed=False))
    pointer=dict(Package=str(package),Backup=str(backup),Revision=revision,NoLiveMonitoring=True,
        Launcher=str(package/'source/tools/start-cleft-dynamics-window.ps1'))
    save(package.parent/(package.name+'-prepared.json'),pointer);print(json.dumps(pointer,indent=2),flush=True)


def preflight(package):
    cfg=read(package/'configuration.json');review=read(package/'qualified-membrane-review.json');p=read(package/'provenance.json')
    assert cfg['IsolatedCleftDynamics'] and cfg['NoLiveMonitoring'] and cfg['ExpectedTestCases']==120 and cfg['ExpectedClassCounts']==COUNTS
    assert review['sourceRevision']==REVISION and review['operatorReportedCompletion'] and review['numericalCriteriaPassed'] and review['passedTests']==108
    assert review['savedAnalysisExactlyReproduced'] and review['independentSavedComparisonsRechecked']
    assert p['originalManifestSha256']==review['manifestSha256'] and not p['modelsExecutedDuringPreparation'] and not p['testsExecutedDuringPreparation']
    assert {r['path'] for r in p['preservedSources']}==set(PRESERVED)
    for row in p['preservedSources']:assert h.digest(package/'source'/row['path'])==row['sha256']
    assert h.digest(package/'qualified-managed-runtime.json')==p['qualifiedManagedRuntimeSha256']
    for directory in read(package/'qualified-managed-runtime.json')['directories']:
        root=h.io(directory['path'])
        assert {str(p) for p in root.rglob('*') if p.is_file()}=={str(h.io(e['path'])) for e in directory['files']}
        for entry in directory['files']:assert h.digest(entry['path'])==entry['sha256']
    print('Reviewed instantaneous membranes and declared runtime preserved; no model or test executed.',flush=True)


def runtime(package):
    preflight(package)
    assert read(package/'managed-runtime-before.json')==read(package/'qualified-managed-runtime.json')
    print('Exact reviewed managed runtime retained.',flush=True)


def check_tests(package):
    result=inventory.check_tests(package,COUNTS)
    save(package/'analysis.json',dict(completed=True,componentChecksPassed=True,tests=result,
        independentCoupledTrajectoryQualified=False,stationaryInitializationQualified=False,
        spatialGeometryCalibrated=False,afferentFiringQualified=False,physiologyQualified=False,avatarRecoveryAssessed=False))
    print(json.dumps(result,indent=2),flush=True)


if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    parser=argparse.ArgumentParser();parser.add_argument('command',choices=['prepare','preflight','postflight','runtime','check-tests']);parser.add_argument('path',type=Path)
    a=parser.parse_args();dict(prepare=prepare,preflight=preflight,postflight=preflight,runtime=runtime,**{'check-tests':check_tests})[a.command](a.path.resolve())

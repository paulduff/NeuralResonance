"""Freeze source-frame controls and replay of completed native head measurements."""
import argparse
from datetime import datetime,timezone
from decimal import Decimal
import json
from pathlib import Path,PurePosixPath
import shutil
import subprocess
import sys
import uuid
from zipfile import ZipFile
from afferent_precision_gate import h,read,save
from cleft_trajectory_reference import base
from coupled_canal_gate import COUNTS as previous_counts
import receptor_membrane_gate as inventory

ROOT=Path(__file__).resolve().parents[1]
COUNTS=dict(previous_counts,CanalSkullInputTests=24)
PRESERVED=['src/NRE.SimAvatar/'+n for n in ('AvatarSkullKinematics.cs','AvatarCoupledCanalParameters.cs',
    'AvatarCoupledCanalDynamics.cs','AvatarCleftTransport.cs','NRE.SimAvatar.csproj')]+[
    'Shared.Contracts/PhysicalSampleProtocol.cs','UnityWorld/Assets/NRE/Runtime/SkullMotionCapture.cs',
    'docs/reference-data/vestibular-ifediba-2007.json']+[
    'tests/NeuralResonanceEngine.DNNE.Tests/'+n+'.cs' for n in previous_counts]


def source_interpretation(folder):
    catalog=read(ROOT/'docs/reference-data/vestibular-ifediba-2007.json')
    assert h.digest(folder/'PMC3005417.html').lower()==catalog['retainedHtmlSha256']
    text=read(folder/'PMC3005417-html-text.json');parts={key:t for tag,key,t in text if tag=='p' and key in ('P20','P45')}
    assert set(parts)=={'P20','P45'} and 'simple vector sum' in parts['P20']
    norms=[str(sum(Decimal(x)**2 for x in row)) for row in catalog['primeDirections']['vectors']]
    assert norms==['0.999869','0.999901','1.055307']
    return dict(sourceSha256=catalog['retainedHtmlSha256'],sourceParagraphs=parts,printedSquaredNorms=norms,
        reconstruction='alpha_source = sum(prime_direction_i * component_i); solve the printed column basis',
        polarMap='(-forward, right, up)',axialMap='(+roll, -pitch, -yaw)',determinant=-1,
        printedPosteriorNormIssueResolved=False,directionsNormalized=False,anatomicalCalibrationQualified=False)


def input_from(folder):
    rows=[]
    for step in range(201):
        group=read(folder/f'skull-receipts-{step:06}.json');assert [r['sensorId'] for r in group]==['centre','left','right']
        assert all(r['receipt']['physicsStep']==step for r in group);rows.extend(group)
    return dict(version='retained-native-skull-prime-input-v1',clock=read(folder/'clock.json'),expectedEndpoints=201,records=rows)


def core_subset(original,full):
    assert {k:v for k,v in original.items() if k!='directories'}=={k:v for k,v in full.items() if k!='directories'}
    indexed={d['path']:d for d in full['directories']}
    assert len(original['directories'])==1 and len(full['directories'])==2
    for directory in original['directories']:
        assert 'Microsoft.NETCore.App' in directory['path'] and indexed[directory['path']]==directory
    extras=[d['path'] for d in full['directories'] if d['path'] not in {n['path'] for n in original['directories']}]
    assert len(extras)==1 and 'Microsoft.AspNetCore.App' in extras[0]
    return dict(originalCoreRuntimePreserved=True,additionalReviewedRuntimeDirectories=extras,
        reason='NativeMotorBridgeProbe references the structures runtime; original UnityPhysicalSampleProbe did not.')


def prepare():
    assert not h.git('status','--porcelain'),'Commit first.'
    revision=h.git('rev-parse','HEAD');assert revision==h.git('rev-parse','origin/main'),'Push first.'
    assert shutil.disk_usage('D:/').free>=10*1024**3
    review_folder=ROOT/'artifacts/native-skull-review-20261118';h.verify(review_folder,['review.json']);review=read(review_folder/'review.json')
    assert review['operatorReportedCompletion'] and review['savedAnalysisExactlyReproduced'] and review['passedTests']==80
    assert review['actualPhysicsSteps']==review['deliveredBodySamples']==201
    capture=Path(review['capture']);assert h.digest(capture/'backup-manifest.json')==review['manifestSha256']
    frame_input=input_from(capture/'study')
    names=['study/clock.json','managed-runtime-before.json']+[f'study/skull-receipts-{i:06}.json' for i in range(201)]
    names+=sorted({'study/'+row['sourceFile'] for row in frame_input['records']})
    h.verify(capture,names)
    for row in frame_input['records']:assert h.digest(capture/'study'/row['sourceFile'])==row['sourceSha256']
    canal_folder=ROOT/'artifacts/canal-trajectories-20261118-debebb-review';h.verify(canal_folder,['review.json','analysis.json']);canal=read(canal_folder/'review.json')
    assert canal['operatorReportedCompletion'] and canal['savedAnalysisExactlyReproduced'] and canal['numericalCriteriaPassed']
    cc=Path(canal['capture']);assert h.digest(cc/'backup-manifest.json')==canal['manifestSha256']
    h.verify(cc,['source/'+n for n in PRESERVED]+['managed-runtime-before.json'])
    runtime_relation=core_subset(read(capture/'managed-runtime-before.json'),read(cc/'managed-runtime-before.json'))
    for name in PRESERVED:assert (ROOT/name).read_text(encoding='utf-8-sig')==h.io(cc/'source'/name).read_text(encoding='utf-8-sig'),name
    source=ROOT/'artifacts/vestibular-mechanics-source-20261118';source_names=['PMC3005417.html','PMC3005417-html-text.json']
    h.verify(source,source_names);interpretation=source_interpretation(source)
    methods=inventory.inventory(ROOT,COUNTS);assert sum(methods.values())==100
    package=Path('D:/DNNE-build-work')/('canal-frame-'+datetime.now(timezone.utc).strftime('%Y%m%d')+'-'+uuid.uuid4().hex[:6]);backup=Path('D:/DNNE-desktop-runs/UnityWorld')/package.name
    assert not package.exists() and not backup.exists();package.mkdir()
    subprocess.run(['git','archive','--format=zip','--output='+str(package/'source.zip'),revision],cwd=ROOT,check=True)
    with ZipFile(package/'source.zip') as z:
        for entry in z.infolist():
            name=PurePosixPath(entry.filename)
            assert not name.is_absolute() and '..' not in name.parts and ':' not in entry.filename and '\\' not in entry.filename
            assert ((entry.external_attr>>16)&0o170000)!=0o120000
            target=h.io(package/'source'/name)
            if entry.is_dir():target.mkdir(parents=True,exist_ok=True)
            else:
                target.parent.mkdir(parents=True,exist_ok=True)
                with target.open('xb') as f:f.write(z.read(entry))
    def copy(src,name):
        target=package/name;target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(h.io(src),h.io(target));assert h.digest(src)==h.digest(target)
    for name in names:copy(capture/name,'baseline/'+name)
    copy(review_folder/'review.json','reviewed-skull.json');copy(canal_folder/'review.json','reviewed-canal.json')
    copy(cc/'managed-runtime-before.json','qualified-managed-runtime.json')
    save(package/'runtime-relation.json',runtime_relation)
    for name in source_names:copy(source/name,'source-evidence/'+name)
    copy(package/'source/tools/backup-unity-run.ps1','backup-run.ps1')
    save(package/'source-interpretation.json',interpretation);save(package/'frame-input.json',frame_input);save(package/'preparation-environment.json',base.environment())
    save(package/'provenance.json',dict(skullManifestSha256=review['manifestSha256'],canalManifestSha256=canal['manifestSha256'],
        fullManagedRuntimeSha256=h.digest(cc/'managed-runtime-before.json'),
        baselineFiles=[dict(path=n,sha256=h.digest(capture/n)) for n in names],
        preservedSources=[dict(path=n,sha256=h.digest(cc/'source'/n)) for n in PRESERVED],
        newNativePhysicsRequested=False,originalSourceDirectionsChanged=False,originalBoundsChanged=False,
        originalDefaultsChanged=False,modelsExecutedDuringPreparation=False,testsExecutedDuringPreparation=False))
    files=[dict(Path=p.relative_to(h.io(package)).as_posix(),Sha256=h.digest(p)) for p in sorted(h.io(package).rglob('*')) if p.is_file()]
    dotnet=shutil.which('dotnet');assert dotnet
    save(package/'configuration.json',dict(Revision=revision,PackageFiles=files,BackupDestination=str(backup),
        DotnetExecutable=dotnet,DotnetExecutableSha256=h.digest(dotnet),PythonExecutable=sys.executable,PythonExecutableSha256=h.digest(sys.executable),
        IsolatedCanalFrame=True,NoLiveMonitoring=True,ExpectedTestCases=100,ExpectedClassCounts=COUNTS,ExpectedTestMethods=methods,
        TestFilter='|'.join('FullyQualifiedName~'+c for c in COUNTS),ExpectedNativeRecords=603,ExpectedAngularRecords=600,
        NewNativePhysicsRequested=False,LiveSelectionChanged=False,PhysiologyQualified=False,AvatarRecoveryAssessed=False))
    pointer=dict(Package=str(package),Backup=str(backup),Revision=revision,NoLiveMonitoring=True,Launcher=str(package/'source/tools/start-canal-frame-window.ps1'))
    save(ROOT/'artifacts'/(package.name+'-prepared.json'),pointer);print(json.dumps(pointer,indent=2),flush=True)


def preflight(package):
    cfg=read(package/'configuration.json');p=read(package/'provenance.json');review=read(package/'reviewed-skull.json')
    assert cfg['IsolatedCanalFrame'] and cfg['NoLiveMonitoring'] and cfg['ExpectedTestCases']==100 and cfg['ExpectedClassCounts']==COUNTS
    assert cfg['ExpectedNativeRecords']==603 and cfg['ExpectedAngularRecords']==600 and not cfg['NewNativePhysicsRequested']
    assert review['operatorReportedCompletion'] and review['savedAnalysisExactlyReproduced'] and review['passedTests']==80
    assert p['skullManifestSha256']==review['manifestSha256'] and p['canalManifestSha256']==read(package/'reviewed-canal.json')['manifestSha256']
    assert h.digest(package/'qualified-managed-runtime.json')==p['fullManagedRuntimeSha256']
    for row in p['baselineFiles']:assert h.digest(package/'baseline'/row['path'])==row['sha256']
    assert read(package/'frame-input.json')==input_from(package/'baseline/study')
    assert {r['path'] for r in p['preservedSources']}==set(PRESERVED)
    for row in p['preservedSources']:assert h.digest(package/'source'/row['path'])==row['sha256']
    assert read(package/'source-interpretation.json')==source_interpretation(package/'source-evidence')
    assert read(package/'runtime-relation.json')==core_subset(read(package/'baseline/managed-runtime-before.json'),read(package/'qualified-managed-runtime.json'))
    assert base.environment()==read(package/'preparation-environment.json')
    for row in cfg['PackageFiles']:assert h.digest(package/row['Path'])==row['Sha256'],row['Path']
    for tool in ('Dotnet','Python'):assert h.digest(cfg[tool+'Executable'])==cfg[tool+'ExecutableSha256']
    for directory in read(package/'qualified-managed-runtime.json')['directories']:
        root=h.io(directory['path']);assert {str(f) for f in root.rglob('*') if f.is_file()}=={str(h.io(r['path'])) for r in directory['files']}
        for row in directory['files']:assert h.digest(row['path'])==row['sha256']
    print('Original head samples, source vectors, earlier kernels and runtime preserved; no model/test executed.',flush=True)


def runtime(package):
    preflight(package);assert read(package/'managed-runtime-before.json')==read(package/'qualified-managed-runtime.json')


def check_tests(package):
    runtime(package);result=inventory.check_tests(package,COUNTS);save(package/'test-analysis.json',result)


def reference(package):
    runtime(package)
    from canal_frame_reference import run
    run(package)


def compare(package):
    runtime(package);tests=inventory.check_tests(package,COUNTS);assert tests['passedCases']==100
    from canal_frame_reference import compare as run
    result=run(package);assert result['angularInputChecksPassed']
    print('603 original receipts and 600 independent angular reconstructions agree. No mechanics, anatomical or balance qualification.',flush=True)


if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    parser=argparse.ArgumentParser();parser.add_argument('command',choices=['prepare','preflight','postflight','runtime','check-tests','reference','compare']);parser.add_argument('path',type=Path,nargs='?')
    a=parser.parse_args()
    if a.command=='prepare':prepare()
    else:dict(preflight=preflight,postflight=preflight,runtime=runtime,reference=reference,compare=compare,**{'check-tests':check_tests})[a.command](a.path.resolve())

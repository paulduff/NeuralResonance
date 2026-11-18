"""Freeze the reviewed motor partition diagnosis; no inline simulation."""
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
import rubral_native_gate as original
from rubral_refinement_protocol import VERSION,RESOLUTIONS,CRITERIA

ROOT=Path(__file__).resolve().parents[1]
h=original.h;save=original.save;read=h.read
COUNTS=dict(original.COUNTS,MuscleHingeClockTests=18)
REVISION='40a78fd9f2cf30d29c4d67452fc3b84f5491d935'
NATIVE_REVISION='827f04554fb61191ae2cf489b3aa54618f76c4c0'
def module(name,path):
    s=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m
inventory=module('partition_inventory',ROOT/'tools/receptor_membrane_gate.py')


def prepare(review_folder):
    assert not h.git('status','--porcelain'),'Commit first.'
    revision=h.git('rev-parse','HEAD');assert revision==h.git('rev-parse','origin/main'),'Push first.'
    h.verify(review_folder,['review.json']);review=read(review_folder/'review.json')
    assert review['operatorReportedCompletion'] and review['savedComparisonErrorsExactlyReproduced'] and review['sourceRevision']==REVISION
    assert not review['numericalCriteriaPassed'] and not review['originalBoundsChanged']
    capture=Path(review['capture']);assert h.digest(capture/'backup-manifest.json')==review['manifestSha256']
    prior=review['originalNativeFullAuditRetained'];native=Path(prior['capture'])
    assert prior['sourceRevision']==NATIVE_REVISION and prior['fullCaptureVerified'] and prior['passedTests']==281
    assert h.digest(native/'backup-manifest.json')==h.digest(Path(prior['backup'])/'backup-manifest.json')==prior['manifestSha256']
    h.verify(native,['provenance.json','configuration.json'])
    provenance=read(native/'provenance.json');native_config=read(native/'configuration.json')
    preserved=sorted({r['path'] for r in provenance['preservedSources']} - {'UnityWorld/Assets/NRE/Runtime/MuscleHingeWire.cs'})
    preserved+=['tools/NativeMotorBridgeProbe/RubralCircuitTrace.cs']
    preserved+=['tests/NeuralResonanceEngine.DNNE.Tests/'+c+'.cs' for c in COUNTS if c not in ('SolutionManifestTests','MuscleHingeClockTests')]
    h.verify(native,['source/'+n for n in preserved])
    for n in preserved:assert (ROOT/n).read_text(encoding='utf-8-sig')==h.io(native/'source'/n).read_text(encoding='utf-8-sig'),n
    copies=['preparation-environment.json','reference-controls-summary.json','rubral-hinge-input.json','analysis.json','native-source-analysis.json']
    for directory in ('reference','reference-controls','rubral-reference'):
        root=h.io(capture/directory);copies += [directory+'/'+p.relative_to(root).as_posix() for p in root.rglob('*') if p.is_file()]
    h.verify(capture,copies)
    environment=module('partition_environment',ROOT/'tools/golgi-reference-resume.py').environment()
    assert environment==read(capture/'preparation-environment.json')
    metadata=module('partition_unity_metadata',ROOT/'tools/unity_metadata.py');metadata.validate(ROOT/'UnityWorld')
    methods=inventory.inventory(ROOT,COUNTS);assert sum(methods.values())==287
    assert shutil.disk_usage(ROOT).free>=8*1024**3,'Require 8 GiB free before preparing native traces.'
    assert shutil.disk_usage('D:/').free>=20*1024**3,'Require room for build scratch and independent D: backup.'
    package=ROOT/'artifacts'/('rubral-refinement-'+datetime.now(timezone.utc).strftime('%Y%m%d')+'-'+uuid.uuid4().hex[:6])
    backup=Path('D:/DNNE-desktop-runs/UnityWorld')/package.name
    workspace=Path('D:/DNNE-build-work')/package.name
    assert not package.exists() and not backup.exists() and not workspace.exists();package.mkdir()
    subprocess.run(['git','archive','--format=zip','--output='+str(package/'source.zip'),revision],cwd=ROOT,check=True)
    with ZipFile(package/'source.zip') as archive:
        for e in archive.infolist():
            name=PurePosixPath(e.filename)
            assert not name.is_absolute() and '..' not in name.parts and ':' not in e.filename and '\\' not in e.filename
            assert ((e.external_attr>>16)&0o170000)!=0o120000
            target=h.io(package/'source'/name)
            if e.is_dir():target.mkdir(parents=True,exist_ok=True)
            else:
                target.parent.mkdir(parents=True,exist_ok=True)
                with target.open('xb') as stream:stream.write(archive.read(e))
    def copy(source,name):
        target=h.io(package/name);target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(h.io(source),target)
        assert h.digest(source)==h.digest(target)
    reused=[]
    for name in copies:
        destination='baseline/'+name if name in ('rubral-hinge-input.json','analysis.json','native-source-analysis.json') or name.startswith('rubral-reference/') else name
        copy(capture/name,destination);reused.append(dict(path=destination,sha256=h.digest(capture/name),originalPath=name))
    copy(review_folder/'review.json','reviewed-motor.json');copy(package/'source/tools/backup-unity-run.ps1','backup-run.ps1')
    save(package/'refinement-input.json',dict(version=VERSION,resolutions=RESOLUTIONS,criteria=CRITERIA))
    save(package/'provenance.json',dict(reviewedComparisonManifestSha256=review['manifestSha256'],originalNativeManifestSha256=prior['manifestSha256'],
        originalNativeRevision=NATIVE_REVISION,preservedSources=[dict(path=n,sha256=h.digest(native/'source'/n)) for n in preserved],reusedFiles=reused,
        explicitChanges=['isolated native clock resolutions 2/3','clock test range 18 cases','parameterized native capture and all-record audit'],
        originalBoundsChanged=False,modelsExecutedDuringPreparation=False,testsExecutedDuringPreparation=False))
    files=[dict(Path=p.relative_to(h.io(package)).as_posix(),Sha256=h.digest(p)) for p in sorted(h.io(package).rglob('*')) if p.is_file()]
    dotnet=shutil.which('dotnet');unity=native_config['UnityExecutable'];assert dotnet and h.digest(unity)==native_config['UnityExecutableSha256']
    save(package/'configuration.json',dict(Revision=revision,PackageFiles=files,BackupDestination=str(backup),NoLiveMonitoring=True,
        IsolatedRubralRefinement=True,QualifiedCouplingCorePreserved=True,ExpectedTestCases=287,ExpectedClassCounts=COUNTS,ExpectedTestMethods=methods,
        TestFilter='|'.join('FullyQualifiedName~'+c for c in COUNTS),ExpectedNativeCases=5,ExpectedNativePhysicsSteps=95988,ExpectedSavedReferences=3,
        EnvironmentTransitionDeclared=native_config['EnvironmentTransitionDeclared'],ExpectedEnvironmentControls=10,
        ExpectedReferenceControlCounts=native_config['ExpectedReferenceControlCounts'],ExpectedUnityVersion='6000.3.19f1',UnityBuildWorkspace=str(workspace),
        UnityExecutable=unity,UnityExecutableSha256=h.digest(unity),DotnetExecutable=dotnet,DotnetExecutableSha256=h.digest(dotnet),
        PythonExecutable=sys.executable,PythonExecutableSha256=h.digest(sys.executable),MinimumFreeBytesBeforeRun=8*1024**3,
        LiveBodySelected=False,NoTimingMeasured=True,FullCaseMatrixAssessed=False,PhysiologyQualified=False,AvatarRecoveryAssessed=False))
    pointer=dict(Package=str(package),Backup=str(backup),Revision=revision,NoLiveMonitoring=True,Launcher=str(package/'source/tools/start-rubral-refinement-window.ps1'))
    save(package.parent/(package.name+'-prepared.json'),pointer);print(json.dumps(pointer,indent=2),flush=True)


def preflight(package):
    cfg=read(package/'configuration.json');p=read(package/'provenance.json');review=read(package/'reviewed-motor.json')
    assert cfg['IsolatedRubralRefinement'] and cfg['NoLiveMonitoring'] and cfg['ExpectedClassCounts']==COUNTS and cfg['ExpectedTestCases']==287
    assert cfg['ExpectedNativeCases']==5 and cfg['ExpectedNativePhysicsSteps']==95988 and cfg['ExpectedSavedReferences']==3
    assert read(package/'refinement-input.json')==dict(version=VERSION,resolutions=RESOLUTIONS,criteria=CRITERIA)
    assert review['sourceRevision']==REVISION and review['operatorReportedCompletion'] and review['savedComparisonErrorsExactlyReproduced']
    assert review['manifestSha256']==p['reviewedComparisonManifestSha256'] and review['analysis']==read(package/'baseline/analysis.json')
    assert not any(p[k] for k in ('originalBoundsChanged','modelsExecutedDuringPreparation','testsExecutedDuringPreparation'))
    for row in cfg['PackageFiles']:assert h.digest(package/row['Path'])==row['Sha256'],row['Path']
    for row in p['preservedSources']:assert h.digest(package/'source'/row['path'])==row['sha256'],row['path']
    for row in p['reusedFiles']:assert h.digest(package/row['path'])==row['sha256'],row['path']
    circuit_review=read(package/'reference/circuit-review.json');circuit=read(package/'reference/circuit-gate/analysis.json')
    assert circuit_review['operatorReportedCompletion'] and circuit_review['savedAnalysisExactlyReproduced'] and circuit_review['analysis']==circuit
    assert circuit['bodyEpochSourceGatePassed'] and circuit['tests']['passedCases']==161
    environment=module('partition_actual_environment',package/'source/tools/golgi-reference-resume.py').environment()
    assert environment==read(package/'preparation-environment.json')
    previous=read(package/'reference/qualified-environment.json')
    if cfg['EnvironmentTransitionDeclared']:original.transition.validate(previous,environment,read(package/'reference/environment-transition.json'))
    else:assert environment==previous
    original.check_reference_controls(package)
    expected=read(package/'reference/qualified-managed-runtime.json')
    for directory in expected['directories']:
        root=h.io(directory['path']);assert {str(x) for x in root.rglob('*') if x.is_file()}=={str(h.io(e['path'])) for e in directory['files']}
        for entry in directory['files']:assert h.digest(entry['path'])==entry['sha256']
    print('Reviewed neuronal/muscle sources, runtime and saved references preserved; no model/test executed.',flush=True)


def check_tests(package):
    result=inventory.check_tests(package,COUNTS);assert result['passedCases']==287;return result


def baseline(package):
    preflight(package);review=read(package/'reviewed-motor.json');capture=Path(review['capture'])
    assert h.digest(capture/'backup-manifest.json')==review['manifestSha256']
    audit=module('partition_original_audit',package/'source/tools/analyse-rubral-native-body.py')
    _,spec,_,evidence=audit.native_audit(capture)
    assert spec==read(package/'baseline/rubral-hinge-input.json')
    assert evidence==read(package/'baseline/native-source-analysis.json')
    save(package/'baseline-audit.json',dict(completed=True,originalFullCaseAccountingExactlyReproduced=True,
        sourceBodyAccounting=evidence,modelsExecuted=False,testsExecuted=False))
    print('Generalized saved-record audit reproduces all original seven-case accounting exactly; no native/neuronal rerun.',flush=True)


if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    p=argparse.ArgumentParser();p.add_argument('command',choices=['prepare','preflight','check-tests','baseline']);p.add_argument('path',type=Path)
    a=p.parse_args();result=dict(prepare=prepare,preflight=preflight,baseline=baseline,**{'check-tests':check_tests})[a.command](a.path.resolve())
    if result is not None:print(json.dumps(result,indent=2),flush=True)

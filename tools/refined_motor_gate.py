"""Freeze the full refined motor matrix with qualified archived accounting."""
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
from afferent_precision_gate import h, read, save
from cleft_trajectory_reference import base
import rubral_native_gate as original
from native_capture_store import initialize, snapshot_view, CONDITIONS

ROOT=Path(__file__).resolve().parents[1]
ACCOUNTING_REVIEW=ROOT/'artifacts/native-accounting-20261118-13c9cf-review'
INGRESS_REVIEW=ROOT/'artifacts/afferent-ingress-20261118-d519cf-review'
ENGINE='Structures/_SharedRuntime/StructureEngine.cs'
INGRESS='Structures/_SharedRuntime/IsolatedAfferentIngress.cs'
DRIVER='tools/NativeMotorBridgeProbe/RubralNativeBodyStudy.cs'
ADAPTERS=['tools/native_evidence_archive.py','tools/native_evidence_view.py','tools/analyse-rubral-native-body.py',
    'tools/analyse-native-hinge-comparison.py','tools/analyse-native-motor-bridge.py']


def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path);value=importlib.util.module_from_spec(spec);spec.loader.exec_module(value);return value


def driver_change(before,after):
    changes=[('bool strongLeftOnly=false,Action<string> afterConditionClosed=null)','bool strongLeftOnly=false)'),
        ('        string closedCondition=null;\n',''),
        ('            // The previous foreach scope, including every using declaration,\n            // has exited. Publication must never race open recording streams.\n            if(closedCondition is not null)afterConditionClosed?.Invoke(closedCondition);\n',''),
        ('            closedCondition=condition.Key;\n',''),
        ('        if(closedCondition is not null)afterConditionClosed?.Invoke(closedCondition);\n','')]
    for current,prior in changes:
        assert after.count(current)==1,current
        after=after.replace(current,prior)
    assert after==before,'Only the closed-condition publication hook may change the existing native driver.'


def inactive_ingress_addition(before,after,source,ingress):
    changes=[('IsolatedRubralProjection? diagnosticRubralProjection = null,IsolatedAfferentIngress? diagnosticAfferentIngress = null)',
        'IsolatedRubralProjection? diagnosticRubralProjection = null)'),
        ('\t\tif(diagnosticAfferentIngress is not null)\n\t\t{\n\t\t\tif(isolatedNeuronCount is not null||diagnosticSynapticTargets is not null||diagnosticCorticalPrecision||\n\t\t\t\tdiagnosticCorticalWarmStart||diagnosticRubralProjection is not null)\n\t\t\t\tthrow new ArgumentException("Observed sensory ingress requires its own fresh full vestibular population.");\n\t\t\tdiagnosticAfferentIngress.Bind(profile,_instanceKey,_circuit.NeuronCount);\n\t\t\t_diagnosticAfferentIngress=diagnosticAfferentIngress;\n\t\t}\n',''),
        ('\t\tif (_diagnosticAfferentIngress is not null)\n\t\t\tfingerprint = StableNeuralIdentity.Create(fingerprint,IsolatedAfferentIngress.Version,_diagnosticAfferentIngress.Fingerprint).ToString("N");\n',''),
        ('\t\tif(_diagnosticAfferentIngress is not null)\n\t\t\tthrow new InvalidOperationException("This isolated recipient accepts only its contiguous observed sensory packets.");\n',''),
        ('\t\t\t_diagnosticAfferentIngress?.ValidateTick(tickSignal,_lastBiologicalTimestampMs);\n',''),
        ('\t\tif(_diagnosticAfferentIngress is not null)return _diagnosticAfferentIngress.Resolve(message);\n','')]
    for addition,original_text in changes:
        assert after.count(addition)==1,addition
        after=after.replace(addition,original_text)
    assert after==before,'Unexpected change to prior neuronal calculations.'
    assert 'diagnosticAfferentIngress' not in source
    assert 'null, diagnosticSynapticTargets: declaredTargets, diagnosticRubralProjection: declaredProjection);' in source
    assert 'private readonly IsolatedAfferentIngress? _diagnosticAfferentIngress;' in ingress


def prepare(review_folder):
    assert not h.git('status','--porcelain'),'Commit first.'
    revision=h.git('rev-parse','HEAD');assert revision==h.git('rev-parse','origin/main'),'Push first.'
    assert shutil.disk_usage('D:/').free>=30*1024**3,'Reserve build, complete condition, both archives and final backup space.'
    h.verify(review_folder,['review.json']);review=read(review_folder/'review.json');prior=review['originalFullAuditReceipt']
    assert review['operatorReportedCompletion'] and review['numericalCriteriaPassed'] and review['allNativeEndpointsReconciled']
    assert review['originalBoundsPreserved'] and prior['fullCaptureVerified'] and prior['passedTests']==287
    completed=Path(review['capture']);assert h.digest(completed/'backup-manifest.json')==review['manifestSha256']
    capture=Path(prior['backup']);assert h.digest(capture/'backup-manifest.json')==prior['manifestSha256']
    h.verify(capture,['provenance.json','configuration.json','baseline-audit.json','source/'+DRIVER])
    prior_config=read(capture/'configuration.json');provenance=read(capture/'provenance.json')
    preserved={row['path'] for row in provenance['preservedSources']}
    unity_root=h.io(capture/'source/UnityWorld')
    preserved.update('UnityWorld/'+p.relative_to(unity_root).as_posix() for p in unity_root.rglob('*.cs'))
    preserved.update('tools/NativeMotorBridgeProbe/'+name+'.cs' for name in ('NativeHingeBody','RubralMotorCircuit','RubralCircuitTrace','WirePreflight'))
    preserved.remove(ENGINE);preserved=sorted(preserved)
    h.verify(capture,['source/'+name for name in preserved])
    for name in preserved:
        assert (ROOT/name).read_text(encoding='utf-8-sig')==h.io(capture/'source'/name).read_text(encoding='utf-8-sig'),name
    driver_change((capture/'source'/DRIVER).read_text(encoding='utf-8-sig'),(ROOT/DRIVER).read_text(encoding='utf-8-sig'))
    h.verify(INGRESS_REVIEW,['review.json']);ingress_review=read(INGRESS_REVIEW/'review.json');ingress_capture=Path(ingress_review['capture'])
    assert ingress_review['operatorReportedCompletion'] and ingress_review['componentCriteriaPassed'] and ingress_review['observedIngressChecksPassed'] and ingress_review['passedTests']==264
    assert h.digest(ingress_capture/'backup-manifest.json')==ingress_review['manifestSha256']
    h.verify(capture,['source/'+ENGINE]);h.verify(ingress_capture,['source/'+name for name in (ENGINE,INGRESS)])
    for name in (ENGINE,INGRESS):assert (ROOT/name).read_text(encoding='utf-8-sig')==(ingress_capture/'source'/name).read_text(encoding='utf-8-sig'),name
    inactive_ingress_addition((capture/'source'/ENGINE).read_text(encoding='utf-8-sig'),(ROOT/ENGINE).read_text(encoding='utf-8-sig'),
        (ROOT/'Structures/_SharedRuntime/IsolatedRubralSource.cs').read_text(encoding='utf-8-sig'),(ROOT/INGRESS).read_text(encoding='utf-8-sig'))
    h.verify(ACCOUNTING_REVIEW,['review.json']);accounting=read(ACCOUNTING_REVIEW/'review.json');accounted=Path(accounting['capture'])
    assert accounting['operatorReportedCompletion'] and accounting['controlsPassed']==12 and accounting['fullSourcePacketNativeAccountingPreserved']
    assert h.digest(accounted/'backup-manifest.json')==accounting['manifestSha256']
    h.verify(accounted,['source/'+name for name in ADAPTERS])
    for name in ADAPTERS:assert (ROOT/name).read_text(encoding='utf-8-sig')==(accounted/'source'/name).read_text(encoding='utf-8-sig'),name
    copies=['preparation-environment.json','reference-controls-summary.json','baseline-audit.json',
        'baseline/rubral-hinge-input.json','baseline/analysis.json','baseline/native-source-analysis.json']
    for directory in ('reference','reference-controls','baseline/rubral-reference'):
        root=h.io(capture/directory);copies.extend(directory+'/'+p.relative_to(root).as_posix() for p in sorted(root.rglob('*')) if p.is_file())
    h.verify(capture,copies)
    assert base.environment()==read(capture/'preparation-environment.json')
    module('refined_unity_metadata',ROOT/'tools/unity_metadata.py').validate(ROOT/'UnityWorld')
    package=Path('D:/DNNE-build-work')/('refined-motor-'+datetime.now(timezone.utc).strftime('%Y%m%d')+'-'+uuid.uuid4().hex[:6])
    backup=Path('D:/DNNE-desktop-runs/UnityWorld')/package.name;workspace=Path(str(package)+'-unity-build')
    assert not package.exists() and not backup.exists() and not workspace.exists();package.mkdir()
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
        target=h.io(package/name);target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(h.io(source),target)
        assert h.digest(source)==h.digest(target)
    for name in copies:copy(capture/name,name)
    copy(review_folder/'review.json','reviewed-motor.json');copy(ACCOUNTING_REVIEW/'review.json','reviewed-accounting.json')
    copy(INGRESS_REVIEW/'review.json','reviewed-ingress.json');copy(capture/'source'/ENGINE,'original-structure-engine.cs')
    copy(capture/'source'/DRIVER,'original-native-driver.cs');copy(package/'source/tools/backup-unity-run.ps1','backup-run.ps1')
    save(package/'provenance.json',dict(originalMotorManifestSha256=prior['manifestSha256'],reviewedMotorManifestSha256=review['manifestSha256'],
        reviewedAccountingManifestSha256=accounting['manifestSha256'],
        inactiveIngressAdditionDeclared=True,reviewedIngressManifestSha256=ingress_review['manifestSha256'],
        ingressSources=[dict(path=name,sha256=h.digest(ingress_capture/'source'/name)) for name in (ENGINE,INGRESS)],
        preservedSources=[dict(path=name,sha256=h.digest(capture/'source'/name)) for name in preserved],
        qualifiedAdapters=[dict(path=name,sha256=h.digest(accounted/'source'/name)) for name in ADAPTERS],
        reusedFiles=[dict(path=name,sha256=h.digest(capture/name)) for name in copies],
        originalBoundsChanged=False,originalSourceNumericsChanged=False,modelsExecutedDuringPreparation=False,testsExecutedDuringPreparation=False))
    files=[dict(Path=p.relative_to(h.io(package)).as_posix(),Sha256=h.digest(p)) for p in sorted(h.io(package).rglob('*')) if p.is_file()]
    dotnet=shutil.which('dotnet');unity=prior_config['UnityExecutable'];assert dotnet and h.digest(unity)==prior_config['UnityExecutableSha256']
    save(package/'configuration.json',dict(Revision=revision,PackageFiles=files,BackupDestination=str(backup),NoLiveMonitoring=True,
        IsolatedRefinedMotorMatrix=True,ExpectedStorageControls=22,ExpectedNativeCases=7,ExpectedNativePhysicsSteps=223972,
        ExpectedSavedReferences=15,CommonSamplesPerCase=8000,PriorFocusedTestsReused=287,PriorArchiveViewControlsReused=12,
        EnvironmentTransitionDeclared=prior_config['EnvironmentTransitionDeclared'],ExpectedEnvironmentControls=10,
        ExpectedReferenceControlCounts=prior_config['ExpectedReferenceControlCounts'],ExpectedUnityVersion='6000.3.19f1',UnityBuildWorkspace=str(workspace),
        UnityExecutable=unity,UnityExecutableSha256=h.digest(unity),DotnetExecutable=dotnet,DotnetExecutableSha256=h.digest(dotnet),
        PythonExecutable=sys.executable,PythonExecutableSha256=h.digest(sys.executable),MinimumFreeBytesBeforeRun=20*1024**3,
        LiveBodySelected=False,NoTimingMeasured=True,PhysiologyQualified=False,AvatarRecoveryAssessed=False,ArchiveCopiesShareDrive=True))
    pointer=dict(Package=str(package),Backup=str(backup),Revision=revision,NoLiveMonitoring=True,Launcher=str(package/'source/tools/start-refined-motor-window.ps1'))
    save(ROOT/'artifacts'/(package.name+'-prepared.json'),pointer);print(json.dumps(pointer,indent=2),flush=True)


def preflight(package):
    cfg=read(package/'configuration.json');p=read(package/'provenance.json');review=read(package/'reviewed-motor.json');archive=read(package/'reviewed-accounting.json')
    ingress_review=read(package/'reviewed-ingress.json')
    assert cfg['IsolatedRefinedMotorMatrix'] and cfg['NoLiveMonitoring'] and cfg['ExpectedStorageControls']==22
    assert cfg['ExpectedNativeCases']==7 and cfg['ExpectedNativePhysicsSteps']==223972 and cfg['ExpectedSavedReferences']==15
    assert review['operatorReportedCompletion'] and review['numericalCriteriaPassed'] and review['manifestSha256']==p['reviewedMotorManifestSha256']
    assert review['originalFullAuditReceipt']['manifestSha256']==p['originalMotorManifestSha256']
    assert archive['operatorReportedCompletion'] and archive['controlsPassed']==12 and archive['fullSourcePacketNativeAccountingPreserved']
    assert archive['manifestSha256']==p['reviewedAccountingManifestSha256']
    assert p['inactiveIngressAdditionDeclared'] and ingress_review['manifestSha256']==p['reviewedIngressManifestSha256']
    assert ingress_review['operatorReportedCompletion'] and ingress_review['observedIngressChecksPassed'] and ingress_review['passedTests']==264
    for row in cfg['PackageFiles']:assert h.digest(package/row['Path'])==row['Sha256'],row['Path']
    for row in p['preservedSources']+p['qualifiedAdapters']+p['ingressSources']:assert h.digest(package/'source'/row['path'])==row['sha256'],row['path']
    for row in p['reusedFiles']:assert h.digest(package/row['path'])==row['sha256'],row['path']
    for tool in ('Python','Dotnet','Unity'):assert h.digest(cfg[tool+'Executable'])==cfg[tool+'ExecutableSha256']
    driver_change((package/'original-native-driver.cs').read_text(encoding='utf-8-sig'),(package/'source'/DRIVER).read_text(encoding='utf-8-sig'))
    inactive_ingress_addition((package/'original-structure-engine.cs').read_text(encoding='utf-8-sig'),(package/'source'/ENGINE).read_text(encoding='utf-8-sig'),
        (package/'source/Structures/_SharedRuntime/IsolatedRubralSource.cs').read_text(encoding='utf-8-sig'),(package/'source'/INGRESS).read_text(encoding='utf-8-sig'))
    assert base.environment()==read(package/'preparation-environment.json')
    original.check_reference_controls(package)
    audit=read(package/'baseline-audit.json')
    assert audit['completed'] and audit['originalFullCaseAccountingExactlyReproduced'] and audit['sourceBodyAccounting']==read(package/'baseline/native-source-analysis.json')
    assert [row['case'] for row in audit['sourceBodyAccounting']['cases']]==list(CONDITIONS)
    expected=read(package/'reference/qualified-managed-runtime.json')
    for directory in expected['directories']:
        root=h.io(directory['path']);assert {str(f) for f in root.rglob('*') if f.is_file()}=={str(h.io(r['path'])) for r in directory['files']}
        for row in directory['files']:assert h.digest(row['path'])==row['sha256']
    print('Original circuit/body calculations, archived accounting, saved references and numerical/runtime dependencies preserved; no model/test executed.',flush=True)


def controls(package):
    preflight(package)
    module('visible_new_capture_controls',package/'source/tools/check-native-capture-store.py').run(package/'storage-controls')


def start_store(package):
    preflight(package);cfg=read(package/'configuration.json');passed=read(package/'storage-controls/controls.json')
    assert passed['completed'] and passed['expected']==passed['passed']==22 and all(r['passed'] for r in passed['cases'])
    assert read(package/'managed-runtime-before.json')==read(package/'reference/qualified-managed-runtime.json')
    assert shutil.disk_usage(package).free>=cfg['MinimumFreeBytesBeforeRun'],'Preserve all outputs and stop before native capture.'
    initialize(package,Path(cfg['BackupDestination']))


if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    parser=argparse.ArgumentParser();parser.add_argument('command',choices=['prepare','preflight','controls','start-store','snapshot-view']);parser.add_argument('path',type=Path)
    args=parser.parse_args();dict(prepare=prepare,preflight=preflight,controls=controls,**{'start-store':start_store,'snapshot-view':snapshot_view})[args.command](args.path.resolve())

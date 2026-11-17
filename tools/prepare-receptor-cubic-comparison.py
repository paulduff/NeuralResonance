"""Freeze cubic-mode comparison; all tests and integrations belong to its window."""
import argparse
from datetime import datetime, timezone
import importlib.util
import json
import os
from pathlib import Path, PurePosixPath
import shutil
import subprocess
import sys
import uuid
from zipfile import ZipFile

for name in ('OMP_NUM_THREADS','OPENBLAS_NUM_THREADS','MKL_NUM_THREADS','NUMEXPR_NUM_THREADS'):
    os.environ[name]='1'
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'tools'))
from receptor_membrane_protocol import protocol, cell_version
from receptor_motor_output_protocol import protocol as output_protocol


def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path)
    value=importlib.util.module_from_spec(spec);spec.loader.exec_module(value);return value


def save(path,value):
    with path.open('x',encoding='utf-8') as stream:
        json.dump(value,stream,indent=2,allow_nan=False);stream.write('\n')


def helpers():return module('cubic_packaging',ROOT/'tools/prepare-causal-motor.py')


def check_environment(package):
    assert Path(__file__).resolve()==(package/'source/tools/prepare-receptor-cubic-comparison.py').resolve()
    h=helpers();config=h.read(package/'configuration.json')
    assert config['NoLiveMonitoring'] and config['IsolatedReceptorCubicComparison']
    assert config['CrossingInterpolation']=='CubicHermite'
    for entry in config['PackageFiles']:assert h.digest(package/entry['Path'])==entry['Sha256']
    environment=module('cubic_environment',ROOT/'tools/golgi-reference-resume.py').environment()
    assert environment==h.read(package/'reference-preflight.json')['environment']
    print(json.dumps(dict(numericalEnvironmentPreserved=True,numpy=environment['numpy'],scipy=environment['scipy'],
        modelsExecuted=False,testsExecuted=False)),flush=True)


def check_legacy(package):
    h=helpers();expected=h.read(package/'reference/legacy-trajectories.json')
    results=h.read(package/'membrane-study/results.json');meta=h.read(package/'membrane-study/protocol.json')
    spec=h.read(package/'receptor-membrane-input.json')
    assert spec==protocol('CubicHermite') and meta['candidateVersion']==cell_version(spec)
    assert results['completed'] and [r['key'] for r in results['cases']]==[r['key'] for r in expected]
    rows=[]
    for actual,prior in zip(results['cases'],expected):
        digest=h.digest(package/'membrane-study'/actual['key']/'trajectory.csv')
        assert digest==actual['trajectorySha256']
        rows.append(dict(key=actual['key'],originalSha256=prior['trajectorySha256'],candidateSha256=digest,
            exactAcceptedStateBytesPreserved=digest==prior['trajectorySha256']))
    passed=len(rows)==18 and all(r['exactAcceptedStateBytesPreserved'] for r in rows)
    save(package/'legacy-state-analysis.json',dict(completed=len(rows)==18,passed=passed,cases=rows,
        originalFailedStatusPreserved=True,modelsExecutedDuringAudit=False))
    if not passed:raise RuntimeError('Cubic readout changed a raw accepted-state trajectory; preserve evidence.')
    print('All 18 accepted-state trajectories exactly reproduce the original bytes; crossing timestamps are assessed separately.',flush=True)


def prepare():
    h=helpers();assert Path(h.git('rev-parse','--show-toplevel')).resolve()==ROOT.resolve()
    assert not h.git('status','--porcelain'),'Commit clean source first.'
    revision=h.git('rev-parse','HEAD');assert revision==h.git('rev-parse','origin/main'),'Push first.'
    gate_folder=ROOT/'artifacts/native-crossing-review-20261116'
    prior_folder=ROOT/'artifacts/receptor-comparison-review-20261116'
    h.verify(gate_folder,['review.json','managed-runtime.json']);h.verify(prior_folder,['review.json'])
    gate=h.read(gate_folder/'review.json');prior=h.read(prior_folder/'review.json')
    assert gate['operatorReportedCompletion'] and gate['componentGatePassed'] and gate['savedSourceAnalysisExactlyReproduced']
    assert gate['passedTests']==124 and gate['originalSourceConditions']==5
    assert gate['sourceRevision']=='492c2a491340a7b1b7b3c29ce0464a5d095e850b'
    assert prior['operatorReportedCompletion'] and prior['sourceRevision']=='ade8c0252749a204dee4715cb88ab850d1492fb5'
    assert prior['originalStatus']=='FAILED' and prior['savedCandidateAndOutputAuditsReproduced'] and prior['savedNumericalContrastsReproduced']
    crossing_folder=ROOT/'artifacts/receptor-crossing-review-20261116'
    h.verify(crossing_folder,['review.json']);crossing_review=h.read(crossing_folder/'review.json')
    assert crossing_review['operatorReportedCompletion'] and crossing_review['savedArithmeticReconciled']
    assert crossing_review['recordedEndpointPairs']==27 and crossing_review['refinementComparisonsPassed']==12
    crossing_capture=Path(crossing_review['capture'])
    assert h.digest(crossing_capture/'backup-manifest.json')==crossing_review['manifestSha256']
    h.verify(crossing_capture,['crossing-analysis.json','crossing-input.json'])
    gate_capture=Path(gate['capture']);original=Path(prior['capture'])
    for capture,review in ((gate_capture,gate),(original,prior)):
        assert h.digest(capture/'backup-manifest.json')==review['manifestSha256']
    changed=h.git('diff','--name-only',gate['sourceRevision'],revision).splitlines()
    compiled={n for n in changed if Path(n).suffix.lower() in ('.cs','.csproj','.props','.targets','.sln','.slnx','.resx') or Path(n).name.lower() in ('global.json','nuget.config')}
    allowed={'tools/NativeMotorBridgeProbe/ReceptorMembraneStudy.cs','tools/NativeMotorBridgeProbe/ReceptorMotorOutputStudy.cs'}
    assert compiled==allowed,'Only explicit recorder/source mode selection may differ from the component gate.'
    # The changed probe must rebuild and pass the original gate inside the window.
    # Do not reuse or relabel the previously qualified binary as this new probe.
    inventory=module('cubic_test_inventory',ROOT/'tools/native_crossing_gate.py')
    methods=inventory.gate.inventory(ROOT,inventory.COUNTS)
    legacy=h.read(original/'membrane-study/results.json')
    reference_names=['reference/'+p.relative_to(h.io(original/'reference')).as_posix()
        for p in sorted(h.io(original/'reference').rglob('*')) if p.is_file() and p.relative_to(h.io(original/'reference')).parts[0] in ('motor-source','receptor-source')]
    source_names=['tools/'+n for n in ('receptor_membrane_reference.py','motoneuron-active-reference.py','motoneuron-reference-check.py',
        'motoneuron_active_protocol.py','motoneuron_bounded_jacobian.py','golgi-reference-resume.py')]
    original_names=reference_names+['window-status.json','reference-preflight.json','receptor-membrane-input.json',
        'receptor-motor-output-input.json','membrane-study/results.json']+['source/'+n for n in source_names]
    original_names+=['membrane-study/'+r['key']+'/trajectory.csv' for r in legacy['cases']]
    h.verify(original,original_names)
    assert h.read(original/'window-status.json')['Status']=='FAILED'
    assert legacy['completed'] and len(legacy['cases'])==18
    assert h.read(original/'receptor-membrane-input.json')==protocol()
    assert h.read(original/'receptor-motor-output-input.json')==output_protocol()
    for name in source_names:
        current=(ROOT/name).read_text(encoding='utf-8-sig');old=h.io(original/'source'/name).read_text(encoding='utf-8-sig')
        if name=='tools/receptor_membrane_reference.py':
            extension="    from receptor_membrane_protocol import crossing_mode\n    spec = read(package / 'receptor-membrane-input.json'); crossing_mode(spec)"
            assert current.count(extension)==1
            current=current.replace(extension,"    spec = read(package / 'receptor-membrane-input.json'); assert spec == protocol()")
        assert current==old,'Independent reference equations/solver changed: '+name
    environment=module('cubic_preparation_environment',ROOT/'tools/golgi-reference-resume.py').environment()
    assert environment==h.read(original/'reference-preflight.json')['environment']
    for disk in (ROOT,Path('D:/DNNE-desktop-runs/UnityWorld')):
        assert shutil.disk_usage(disk).free>=20*1024**3,'Require 20 GiB free per capture/backup drive.'
    source_names_to_copy=['reference/'+n for n in inventory.gate.REFERENCES]
    h.verify(gate_capture,source_names_to_copy+['source.zip'])
    package=ROOT/'artifacts'/('receptor-cubic-'+datetime.now(timezone.utc).strftime('%Y%m%d')+'-'+uuid.uuid4().hex[:6])
    backup=Path('D:/DNNE-desktop-runs/UnityWorld')/package.name
    assert not package.exists() and not backup.exists();package.mkdir()
    subprocess.run(['git','archive','--format=zip','--output='+str(package/'source.zip'),revision],cwd=ROOT,check=True)
    with ZipFile(package/'source.zip') as archive:
        for entry in archive.infolist():
            path=PurePosixPath(entry.filename)
            assert not path.is_absolute() and '..' not in path.parts and ':' not in entry.filename and '\\' not in entry.filename
            assert ((entry.external_attr>>16)&0o170000)!=0o120000
            target=h.io(package/'source'/path)
            if entry.is_dir():target.mkdir(parents=True,exist_ok=True)
            else:
                target.parent.mkdir(parents=True,exist_ok=True)
                with target.open('xb') as stream:stream.write(archive.read(entry))
    copied=[]
    def copy(source,relative):
        target=h.io(package/relative);target.parent.mkdir(parents=True,exist_ok=True)
        shutil.copyfile(h.io(source),target);assert h.digest(source)==h.digest(target)
        copied.append(dict(original=str(source),path=relative,sha256=h.digest(target)))
    for name in reference_names+['reference-preflight.json']:copy(original/name,name)
    for name in source_names_to_copy:copy(gate_capture/name,name)
    copy(gate_folder/'managed-runtime.json','reference/qualified-managed-runtime.json')
    copy(gate_folder/'review.json','reference/component-gate-review.json')
    copy(gate_capture/'source.zip','reference/component-gate-source.zip')
    copy(prior_folder/'review.json','reference/original-comparison-review.json')
    copy(crossing_folder/'review.json','reference/saved-endpoint-review.json')
    copy(crossing_capture/'crossing-analysis.json','reference/saved-endpoint-crossings.json')
    copy(crossing_capture/'crossing-input.json','reference/saved-endpoint-input.json')
    save(package/'reference/legacy-trajectories.json',[dict(key=r['key'],trajectorySha256=r['trajectorySha256']) for r in legacy['cases']])
    spec=protocol('CubicHermite')
    save(package/'receptor-membrane-input.json',spec);save(package/'receptor-motor-output-input.json',output_protocol('CubicHermite'))
    save(package/'provenance.json',dict(componentGateReview=gate,originalComparisonReview=prior,copied=copied,
        changedCompilationInputs=sorted(compiled),newProbeBuildAndGateRequired=True,exactQualifiedProbeReused=False,
        independentEquationsAndSolverPreserved=True,originalBoundsChanged=False,testsExecutedDuringPreparation=False,modelsExecutedDuringPreparation=False))
    shutil.copyfile(package/'source/tools/backup-unity-run.ps1',package/'backup-run.ps1')
    files=[dict(Path=p.relative_to(h.io(package)).as_posix(),Sha256=h.digest(p)) for p in sorted(h.io(package).rglob('*')) if p.is_file()]
    runtime=h.read(gate_folder/'managed-runtime.json');dotnet=runtime['host']
    assert h.digest(dotnet)==runtime['hostSha256'] and h.digest(gate_folder/'managed-runtime.json')==gate['managedRuntimeSha256']
    config=dict(Revision=revision,PreparedUtc=datetime.now(timezone.utc).isoformat(),PackageFiles=files,
        DotnetExecutable=dotnet,DotnetExecutableSha256=h.digest(dotnet),PythonExecutable=sys.executable,PythonExecutableSha256=h.digest(sys.executable),
        BackupDestination=str(backup),NoLiveMonitoring=True,IsolatedReceptorMembraneComparison=True,IsolatedReceptorCubicComparison=True,
        CrossingInterpolation='CubicHermite',CrossingLocalizationToleranceMs=1e-9,
        QualifiedProbeRelativePath='source/artifacts/receptor-cubic-probe/bin/NativeMotorBridgeProbe/release/NeuralResonanceEngine.DNNE.Tests.dll',
        ExpectedClassCounts=inventory.COUNTS,ExpectedTestCases=124,ExpectedTestMethods=methods,
        TestFilter='|'.join('FullyQualifiedName~'+c for c in inventory.COUNTS),ExpectedOriginalSourceConditions=5,
        MotorSourceRevision=spec['motorSourceRevision'],ReceptorSourceRevision=spec['receptorSourceRevision'],
        ExpectedCandidateConditions=18,ExpectedReferenceConditions=18,ExpectedContactDeliveries=648,
        SourceOutputComparisonRequested=True,ExpectedSourceOutputConditions=36,ExpectedSourceObservations=540036,
        ExpectedAnalyticControls=6,ExpectedJacobianControls=36,ExpectedReferenceContrasts=18,ExpectedCandidateContrasts=18,ExpectedRefinementContrasts=12,
        MinimumFreeBytes=20*1024**3,PresynapticEventsAreFixtures=True,PresynapticNeuronsExecuted=False,NativePhysicsRequested=False,
        PhysiologyQualified=False,AvatarRecoveryAssessed=False)
    save(package/'configuration.json',config)
    pointer=dict(Package=str(package),Backup=str(backup),Revision=revision,PreparedOnly=True,NoLiveMonitoring=True,
        Launcher=str(package/'source/tools/start-receptor-cubic-comparison-window.ps1'))
    save(package.parent/(package.name+'-prepared.json'),pointer);print(json.dumps(pointer,indent=2),flush=True)


if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    parser=argparse.ArgumentParser(description=__doc__);actions=parser.add_mutually_exclusive_group(required=True)
    actions.add_argument('--prepare',action='store_true');actions.add_argument('--check-environment',type=Path);actions.add_argument('--check-legacy',type=Path)
    args=parser.parse_args()
    if args.prepare:prepare()
    elif args.check_environment:check_environment(args.check_environment.resolve())
    else:check_legacy(args.check_legacy.resolve())

"""Freeze and qualify coupled membrane derivatives in an independent visible window."""
import argparse
from datetime import datetime, timezone
from decimal import Decimal, localcontext
import importlib.util
import json
from pathlib import Path, PurePosixPath
import shutil
import subprocess
import sys
import uuid
from zipfile import ZipFile
from cleft_membrane_protocol import protocol, validate, COUNTS
from cleft_membrane_reference import evaluate

ROOT=Path(__file__).resolve().parents[1]
OPEN_REVISION='04c4fd61f4df36ba438dfff26b41db28d278d625'
REGRESSION=('input.json','results.json','open.jsonl','closed/input.json','closed/results.json',
            'closed/edges.jsonl','closed/charges.jsonl','closed/spatial.jsonl')
PRESERVED=['src/NRE.SimAvatar/'+name+'.cs' for name in ('AvatarVestibularHairCell','AvatarCalyxMembrane',
    'AvatarCleftTransport','AvatarCleftChargeBalance','AvatarCleftSpatialCoupling','AvatarCleftOpenBoundary','AvatarVestibularCleftMembranes')]
PRESERVED+=['tests/NeuralResonanceEngine.DNNE.Tests/'+name+'.cs' for name in COUNTS if name!='SolutionManifestTests']
PRESERVED+=['tools/'+name for name in ('vestibular_hair_cell_protocol.py','calyx_membrane_protocol.py',
    'cleft_coupling_protocol.py','cleft_spatial_protocol.py','cleft_spatial_reference.py','cleft_open_protocol.py',
    'cleft_open_reference.py','analyse-cleft-coupling.py','analyse-cleft-open.py','NativeMotorBridgeProbe/CleftCouplingStudy.cs',
    'NativeMotorBridgeProbe/CleftOpenStudy.cs','golgi-reference-resume.py')]


def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m


h=module('membrane_files',ROOT/'tools/prepare-causal-motor.py')
inventory=module('membrane_test_inventory',ROOT/'tools/receptor_membrane_gate.py')
base=module('membrane_constitutive_reference',ROOT/'tools/analyse-cleft-coupling.py')
read=base.read;save=base.save


def prepare(review_folder):
    assert not h.git('status','--porcelain'),'Commit clean source first.'
    revision=h.git('rev-parse','HEAD');assert revision==h.git('rev-parse','origin/main'),'Push first.'
    h.verify(review_folder,['review.json']);review=read(review_folder/'review.json')
    assert review['operatorReportedCompletion'] and review['savedAnalysisExactlyReproduced'] and review['independentSavedComparisonsRechecked']
    assert review['numericalCriteriaPassed'] and review['passedTests']==51 and review['sourceRevision']==OPEN_REVISION
    assert review['analysis']['closedRecordsBitwisePreserved'] and review['analysis']['openRows']==192 and review['analysis']['closedRows']==624
    assert not review['analysis']['originalBoundsChanged']
    capture=Path(review['capture']);assert h.digest(capture/'backup-manifest.json')==review['manifestSha256']
    names=['study/'+name for name in REGRESSION]+['source/'+name for name in PRESERVED]
    names+=['cleft-input.json','open-input.json','preparation-environment.json','managed-runtime-before.json',
            'qualified-cleft-review.json','qualified-calyx-review.json','qualified-hair-cell-review.json']
    evidence_root=h.io(capture/'source-evidence')
    evidence=['source-evidence/'+p.relative_to(evidence_root).as_posix() for p in evidence_root.rglob('*') if p.is_file()]
    h.verify(capture,names+evidence)
    for name in PRESERVED: assert (ROOT/name).read_text(encoding='utf-8-sig')==h.io(capture/'source'/name).read_text(encoding='utf-8-sig'),name
    environment=base.environment();assert environment==read(capture/'preparation-environment.json'),'Runtime changed; review explicitly.'
    methods=inventory.inventory(ROOT,COUNTS);assert sum(methods.values())==sum(COUNTS.values())==108
    package=ROOT/'artifacts'/('cleft-membranes-'+datetime.now(timezone.utc).strftime('%Y%m%d')+'-'+uuid.uuid4().hex[:6])
    backup=Path('D:/DNNE-desktop-runs/UnityWorld')/package.name
    assert not package.exists() and not backup.exists();package.mkdir()
    subprocess.run(['git','archive','--format=zip','--output='+str(package/'source.zip'),revision],cwd=ROOT,check=True)
    with ZipFile(package/'source.zip') as archive:
        for entry in archive.infolist():
            name=PurePosixPath(entry.filename)
            assert not name.is_absolute() and '..' not in name.parts and ':' not in entry.filename and '\\' not in entry.filename
            assert ((entry.external_attr>>16)&0o170000)!=0o120000
            target=h.io(package/'source'/name)
            if entry.is_dir(): target.mkdir(parents=True,exist_ok=True)
            else:
                target.parent.mkdir(parents=True,exist_ok=True)
                with target.open('xb') as stream:stream.write(archive.read(entry))
    def copy(source,destination):
        target=h.io(package/destination);target.parent.mkdir(parents=True,exist_ok=True)
        shutil.copyfile(h.io(source),target);assert h.digest(source)==h.digest(target)
    for name in PRESERVED: assert h.digest(package/'source'/name)==h.digest(capture/'source'/name),name
    for name in evidence+['cleft-input.json','open-input.json','preparation-environment.json',
                          'qualified-cleft-review.json','qualified-calyx-review.json','qualified-hair-cell-review.json']: copy(capture/name,name)
    for name in REGRESSION: copy(capture/'study'/name,'reference/regression/'+name)
    copy(capture/'managed-runtime-before.json','qualified-managed-runtime.json')
    copy(review_folder/'review.json','qualified-open-review.json')
    copy(package/'source/tools/backup-unity-run.ps1','backup-run.ps1')
    save(package/'membrane-input.json',protocol())
    save(package/'provenance.json',dict(qualifiedCapture=str(capture),qualifiedCaptureManifestSha256=review['manifestSha256'],
        qualifiedSourceRevision=OPEN_REVISION,regressionFiles=[dict(path='reference/regression/'+name,sha256=h.digest(capture/'study'/name)) for name in REGRESSION],
        preservedSources=[dict(path=name,sha256=h.digest(capture/'source'/name)) for name in PRESERVED],
        numericalRuntimeMatchesQualifiedOpen=True,qualifiedManagedRuntimeSha256=h.digest(capture/'managed-runtime-before.json'),
        modelsExecutedDuringPreparation=False,testsExecutedDuringPreparation=False,pendingCapturesRead=False))
    files=[dict(Path=p.relative_to(h.io(package)).as_posix(),Sha256=h.digest(p)) for p in sorted(h.io(package).rglob('*')) if p.is_file()]
    dotnet=shutil.which('dotnet');assert dotnet
    save(package/'configuration.json',dict(Revision=revision,PreparedUtc=datetime.now(timezone.utc).isoformat(),PackageFiles=files,
        DotnetExecutable=dotnet,DotnetExecutableSha256=h.digest(dotnet),PythonExecutable=sys.executable,PythonExecutableSha256=h.digest(sys.executable),
        BackupDestination=str(backup),IsolatedCleftMembranes=True,NoLiveMonitoring=True,ExpectedTestCases=108,ExpectedClassCounts=COUNTS,
        ExpectedTestMethods=methods,TestFilter='|'.join('FullyQualifiedName~'+c for c in COUNTS),ExpectedMembraneCases=216,
        ExpectedOpenCases=192,ExpectedClosedCases=624,ExpectedDecimalPrecisions=[60,90],LiveSelectionChanged=False,TimeIntegrated=False,
        SpatialGeometryCalibrated=False,AfferentFiringQualified=False,PhysiologyQualified=False,AvatarRecoveryAssessed=False))
    pointer=dict(Package=str(package),Backup=str(backup),Revision=revision,NoLiveMonitoring=True,
        Launcher=str(package/'source/tools/start-cleft-membranes-window.ps1'))
    save(package.parent/(package.name+'-prepared.json'),pointer);print(json.dumps(pointer,indent=2),flush=True)


def prerequisites(package):
    spec=validate(read(package/'membrane-input.json'));cfg=read(package/'configuration.json')
    review=read(package/'qualified-open-review.json');provenance=read(package/'provenance.json')
    assert cfg['IsolatedCleftMembranes'] and cfg['NoLiveMonitoring'] and cfg['ExpectedTestCases']==108
    assert cfg['ExpectedMembraneCases']==216 and cfg['ExpectedOpenCases']==192 and cfg['ExpectedClosedCases']==624 and cfg['ExpectedDecimalPrecisions']==[60,90]
    assert review['operatorReportedCompletion'] and review['savedAnalysisExactlyReproduced'] and review['independentSavedComparisonsRechecked']
    assert review['numericalCriteriaPassed'] and review['passedTests']==51 and review['sourceRevision']==provenance['qualifiedSourceRevision']==OPEN_REVISION
    assert review['capture']==provenance['qualifiedCapture'] and review['manifestSha256']==provenance['qualifiedCaptureManifestSha256']
    assert review['analysis']['closedRecordsBitwisePreserved'] and not review['analysis']['originalBoundsChanged']
    assert provenance['numericalRuntimeMatchesQualifiedOpen'] and not provenance['modelsExecutedDuringPreparation'] and not provenance['testsExecutedDuringPreparation']
    assert h.digest(package/'qualified-managed-runtime.json')==provenance['qualifiedManagedRuntimeSha256']
    assert {row['path'] for row in provenance['regressionFiles']}=={'reference/regression/'+name for name in REGRESSION}
    assert {row['path'] for row in provenance['preservedSources']}==set(PRESERVED)
    for row in provenance['regressionFiles']: assert h.digest(package/row['path'])==row['sha256']
    for row in provenance['preservedSources']: assert h.digest(package/'source'/row['path'])==row['sha256']
    assert base.environment()==read(package/'preparation-environment.json'),'Numerical runtime changed.'
    return spec


def preflight(package):
    prerequisites(package)
    save(package/'reference-preflight.json',dict(environment=base.environment(),modelsExecuted=False,testsExecuted=False))


def runtime(package):
    prerequisites(package)
    expected=read(package/'qualified-managed-runtime.json')
    assert read(package/'managed-runtime-before.json')==expected,'Managed runtime differs from reviewed open-cleft capture.'
    for directory in expected['directories']:
        root=h.io(directory['path'])
        assert {str(p) for p in root.rglob('*') if p.is_file()}=={str(h.io(entry['path'])) for entry in directory['files']}
        for entry in directory['files']:assert h.digest(entry['path'])==entry['sha256']
    result=dict(exactQualifiedManagedRuntime=True,modelsExecuted=False,testsExecuted=False)
    print(json.dumps(result,indent=2),flush=True)
    return result


def compare(package):
    spec=prerequisites(package);assert base.environment()==read(package/'reference-preflight.json')['environment']
    runtime(package)
    tests=inventory.check_tests(package,COUNTS)
    assert read(package/'study/input.json')=={key:spec[key] for key in ('version','kernelVersion','cases','expectedCases','expectedClosedRows','expectedOpenRows')}
    assert read(package/'study/results.json')==dict(completed=True,closedRows=624,openRows=192,membraneRows=216,
        timeIntegrated=False,spatialGeometryCalibrated=False,afferentFiringQualified=False,physiologyQualified=False,avatarRecoveryAssessed=False)
    for name in REGRESSION:
        assert (package/'study/regression'/name).read_bytes()==(package/'reference/regression'/name).read_bytes(),('Preserved regression differs',name)
    maximum=Decimal(0);count=0;fingerprints={}
    with (package/'study/membranes.jsonl').open() as source,(package/'independent-membranes.jsonl').open('x') as target:
        for expected,line in zip(spec['cases'],source,strict=True):
            row=json.loads(line);assert set(row)=={'input','fingerprint','evaluation'} and row['input']==expected
            identity=json.dumps([expected[key] for key in ('hairParameters','calyxParameters','topology')],sort_keys=True)
            assert len(row['fingerprint'])==64 and fingerprints.setdefault(identity,row['fingerprint'])==row['fingerprint']
            references=[]
            for precision in spec['decimalPrecisions']:
                with localcontext() as context:
                    context.prec=precision;references.append(evaluate(expected,base.edge,base.dense_solve))
            with localcontext() as context:
                context.prec=90
                base.compare_values(references[0],references[1],Decimal(spec['referenceAbsoluteTolerance']),Decimal(spec['referenceRelativeTolerance']),'membrane/reference/'+str(count))
                ratio=base.compare_values(row['evaluation'],references[1],Decimal(spec['candidateAbsoluteTolerance']),Decimal(spec['candidateRelativeTolerance']),'membrane/candidate/'+str(count))
                maximum=max(maximum,ratio)
            target.write(json.dumps(dict(index=count,input=expected,precision=spec['decimalPrecisions'],
                expected=[base.decimal_strings(r) for r in references],maximumAllowedFraction=str(ratio)),allow_nan=False)+'\n');target.flush()
            count+=1
            if count%36==0:print(f'{count}/216 complete local membrane and spatial derivatives compared.',flush=True)
    assert count==216 and len(set(fingerprints.values()))==len(fingerprints)
    save(package/'analysis.json',dict(completed=True,numericalComponentCriteriaPassed=True,tests=tests,
        closedRows=624,openRows=192,membraneRows=count,regressionRecordsBitwisePreserved=True,maximumAllowedFraction=str(maximum),
        decimalPrecisions=[60,90],originalBoundsChanged=False,timeIntegrated=False,spatialGeometryCalibrated=False,
        afferentFiringQualified=False,physiologyQualified=False,avatarRecoveryAssessed=False))
    print('624 closed and 192 open records unchanged; 216 coupled membrane derivatives reconciled with independent 60/90-digit references.',flush=True)


if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command',choices=['prepare','preflight','compare','postflight','check-tests','runtime']);parser.add_argument('path',type=Path)
    args=parser.parse_args();path=args.path.resolve()
    if args.command=='check-tests':print(inventory.check_tests(path,COUNTS))
    else:dict(prepare=prepare,preflight=preflight,compare=compare,postflight=base.postflight,runtime=runtime)[args.command](path)

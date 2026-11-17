"""Freeze open-cleft qualification only after a completed, D:-verified closed review."""
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
from cleft_open_protocol import protocol,COUNTS
from cleft_coupling_protocol import validate as closed_validate

ROOT=Path(__file__).resolve().parents[1]


def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m


def save(path,value):
    with path.open('x',encoding='utf-8') as stream:json.dump(value,stream,indent=2,allow_nan=False);stream.write('\n')


def prepare(review_folder):
    h=module('open_prepare_helpers',ROOT/'tools/prepare-causal-motor.py')
    gate=module('open_prepare_inventory',ROOT/'tools/receptor_membrane_gate.py')
    assert not h.git('status','--porcelain'),'Commit clean source first.'
    revision=h.git('rev-parse','HEAD');assert revision==h.git('rev-parse','origin/main'),'Push first.'
    h.verify(review_folder,['review.json']);review=h.read(review_folder/'review.json')
    assert review['operatorReportedCompletion'] and review['savedAnalysisExactlyReproduced'] and review['independentSavedComparisonsRechecked']
    assert review['numericalCriteriaPassed'] and review['passedTests']==40
    assert review['sourceRevision']=='c53783a17e9ab26ab5968bca5d267d0c1a277fa5'
    assert review['analysis']['counts']==dict(edges=468,charges=60,spatial=96) and not review['analysis']['originalBoundsChanged']
    capture=Path(review['capture']);assert h.digest(capture/'backup-manifest.json')==review['manifestSha256']
    preserved=['src/NRE.SimAvatar/'+name for name in ('AvatarVestibularHairCell.cs','AvatarCalyxMembrane.cs','AvatarCleftChargeBalance.cs',
        'AvatarClockedVestibularMechanics.cs','AvatarPhysicalObservationProducer.cs','AvatarPhysicalObservationTransport.cs')]
    preserved+=['ControlProgram/VestibularLabyrinthTransducer.cs','ControlProgram/PhysicalBodyTransducerRuntime.cs']
    preserved+=['tests/NeuralResonanceEngine.DNNE.Tests/'+name for name in ('CleftCouplingTests.cs','CleftSpatialCouplingTests.cs',
        'CalyxMembraneTests.cs','VestibularHairCellTests.cs')]
    preserved+=['tools/'+name for name in ('cleft_coupling_protocol.py','cleft_spatial_protocol.py','cleft_spatial_reference.py',
        'analyse-cleft-coupling.py','NativeMotorBridgeProbe/CleftCouplingStudy.cs','golgi-reference-resume.py')]
    reference_files=['study/'+name for name in ('input.json','results.json','edges.jsonl','charges.jsonl','spatial.jsonl')]
    h.verify(capture,['source/'+p for p in preserved]+reference_files+['cleft-input.json','analysis.json','preparation-environment.json',
        'qualified-calyx-review.json','qualified-hair-cell-review.json'])
    assert review['analysis']==h.read(capture/'analysis.json')
    for name in preserved:assert (ROOT/name).read_text(encoding='utf-8-sig')==h.io(capture/'source'/name).read_text(encoding='utf-8-sig'),name
    closed=closed_validate(h.read(capture/'cleft-input.json'))
    environment=module('open_prepare_environment',ROOT/'tools/golgi-reference-resume.py').environment()
    assert environment==h.read(capture/'preparation-environment.json'),'Numerical runtime changed; inspect explicitly.'
    methods=gate.inventory(ROOT,COUNTS);assert sum(methods.values())==sum(COUNTS.values())==51
    evidence_root=h.io(capture/'source-evidence')
    evidence=['source-evidence/'+p.relative_to(evidence_root).as_posix() for p in evidence_root.rglob('*') if p.is_file()]
    h.verify(capture,evidence)
    package=ROOT/'artifacts'/('cleft-open-'+datetime.now(timezone.utc).strftime('%Y%m%d')+'-'+uuid.uuid4().hex[:6])
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
    def copy(source,destination):
        target=h.io(package/destination);target.parent.mkdir(parents=True,exist_ok=True)
        shutil.copyfile(h.io(source),target);assert h.digest(source)==h.digest(target)
    for name in evidence+['qualified-calyx-review.json','qualified-hair-cell-review.json']:copy(capture/name,name)
    closed_files=[]
    for name in reference_files:
        target='reference/closed/'+name.removeprefix('study/');copy(capture/name,target)
        closed_files.append(dict(path=target,sha256=h.digest(package/target)))
    copy(review_folder/'review.json','qualified-cleft-review.json')
    save(package/'cleft-input.json',closed);save(package/'open-input.json',protocol());save(package/'preparation-environment.json',environment)
    save(package/'provenance.json',dict(qualifiedCapture=str(capture),qualifiedCaptureManifestSha256=review['manifestSha256'],
        qualifiedSourceRevision=review['sourceRevision'],closedFiles=closed_files,
        preservedFiles=[dict(path=n,sha256=h.digest(capture/'source'/n)) for n in preserved],
        runtimeMatchesQualifiedCleft=True,modelsExecutedDuringPreparation=False,testsExecutedDuringPreparation=False,
        pendingCapturesRead=False,liveSelectionChanged=False,spatialGeometryCalibrated=False))
    copy(package/'source/tools/backup-unity-run.ps1','backup-run.ps1')
    files=[dict(Path=p.relative_to(h.io(package)).as_posix(),Sha256=h.digest(p)) for p in sorted(h.io(package).rglob('*')) if p.is_file()]
    dotnet=shutil.which('dotnet');assert dotnet
    save(package/'configuration.json',dict(Revision=revision,PreparedUtc=datetime.now(timezone.utc).isoformat(),PackageFiles=files,
        DotnetExecutable=dotnet,DotnetExecutableSha256=h.digest(dotnet),PythonExecutable=sys.executable,PythonExecutableSha256=h.digest(sys.executable),
        BackupDestination=str(backup),IsolatedCleftOpen=True,NoLiveMonitoring=True,ExpectedTestCases=51,ExpectedClassCounts=COUNTS,
        ExpectedTestMethods=methods,TestFilter='|'.join('FullyQualifiedName~'+c for c in COUNTS),ExpectedOpenCases=192,ExpectedClosedCases=624,
        ExpectedDecimalPrecisions=[60,90],LiveSelectionChanged=False,TimeIntegrated=False,SpatialGeometryCalibrated=False,
        AfferentFiringQualified=False,PhysiologyQualified=False,AvatarRecoveryAssessed=False))
    pointer=dict(Package=str(package),Backup=str(backup),Revision=revision,NoLiveMonitoring=True,
        Launcher=str(package/'source/tools/start-cleft-open-window.ps1'))
    save(package.parent/(package.name+'-prepared.json'),pointer);print(json.dumps(pointer,indent=2),flush=True)


if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('cleft_review',type=Path);args=parser.parse_args()
    prepare(args.cleft_review.resolve())

"""Prepare metadata only, after an operator-reported and D:-verified calyx review."""
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
from cleft_coupling_protocol import protocol,COUNTS

ROOT=Path(__file__).resolve().parents[1]


def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m


def save(path,value):
    with path.open('x',encoding='utf-8') as stream: json.dump(value,stream,indent=2,allow_nan=False);stream.write('\n')


def prepare(review_folder):
    h=module('cleft_prepare_helpers',ROOT/'tools/prepare-causal-motor.py')
    gate=module('cleft_prepare_inventory',ROOT/'tools/receptor_membrane_gate.py')
    assert not h.git('status','--porcelain'),'Commit clean source first.'
    revision=h.git('rev-parse','HEAD');assert revision==h.git('rev-parse','origin/main'),'Push first.'
    h.verify(review_folder,['review.json']);review=h.read(review_folder/'review.json')
    assert review['operatorReportedCompletion'] and review['savedAnalysisExactlyReproduced'] and review['independentSavedComparisonsRechecked']
    assert review['numericalCriteriaPassed'] and review['passedTests']==23
    assert review['sourceRevision']=='635508a1d6d8dcaba156004d50d3e67e620b9acd'
    assert review['analysis']['arithmeticRows']==15984 and review['analysis']['clampRows']==224 and not review['analysis']['originalBoundsChanged']
    capture=Path(review['capture']);assert h.digest(capture/'backup-manifest.json')==review['manifestSha256']
    preserved=['src/NRE.SimAvatar/AvatarClockedVestibularMechanics.cs',
        'ControlProgram/VestibularLabyrinthTransducer.cs','ControlProgram/PhysicalBodyTransducerRuntime.cs',
        'src/NRE.SimAvatar/AvatarPhysicalObservationProducer.cs','src/NRE.SimAvatar/AvatarPhysicalObservationTransport.cs',
        'src/NRE.SimAvatar/AvatarVestibularHairCell.cs','src/NRE.SimAvatar/AvatarCalyxMembrane.cs',
        'tests/NeuralResonanceEngine.DNNE.Tests/ClockedVestibularMechanicsTests.cs',
        'tests/NeuralResonanceEngine.DNNE.Tests/VestibularLabyrinthTransducerTests.cs',
        'tests/NeuralResonanceEngine.DNNE.Tests/VestibularHairCellTests.cs',
        'tests/NeuralResonanceEngine.DNNE.Tests/CalyxMembraneTests.cs',
        'tools/NativeMotorBridgeProbe/CalyxMembraneStudy.cs','tools/golgi-reference-resume.py']
    h.verify(capture,['source/'+p for p in preserved]+['preparation-environment.json','analysis.json','qualified-hair-cell-review.json'])
    assert review['analysis']==h.read(capture/'analysis.json')
    for name in preserved:
        assert (ROOT/name).read_text(encoding='utf-8-sig')==h.io(capture/'source'/name).read_text(encoding='utf-8-sig'),name
    environment=module('cleft_prepare_environment',ROOT/'tools/golgi-reference-resume.py').environment()
    assert environment==h.read(capture/'preparation-environment.json'),'Qualified runtime changed; inspect explicitly before preparation.'
    methods=gate.inventory(ROOT,COUNTS);assert sum(methods.values())==sum(COUNTS.values())==40
    evidence_root=h.io(capture/'source-evidence')
    evidence=['source-evidence/'+p.relative_to(evidence_root).as_posix() for p in evidence_root.rglob('*') if p.is_file()]
    h.verify(capture,evidence)
    package=ROOT/'artifacts'/('cleft-coupling-'+datetime.now(timezone.utc).strftime('%Y%m%d')+'-'+uuid.uuid4().hex[:6])
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
    for name in evidence+['qualified-hair-cell-review.json']:
        target=package/name;target.parent.mkdir(parents=True,exist_ok=True)
        shutil.copyfile(h.io(capture/name),h.io(target));assert h.digest(target)==h.digest(capture/name)
    shutil.copyfile(review_folder/'review.json',package/'qualified-calyx-review.json')
    save(package/'cleft-input.json',protocol());save(package/'preparation-environment.json',environment)
    save(package/'provenance.json',dict(qualifiedCapture=str(capture),qualifiedCaptureManifestSha256=review['manifestSha256'],
        qualifiedSourceRevision=review['sourceRevision'],preservedFiles=[dict(path=n,sha256=h.digest(capture/'source'/n)) for n in preserved],
        runtimeMatchesQualifiedCalyx=True,testsExecutedDuringPreparation=False,modelsExecutedDuringPreparation=False,
        pendingCapturesRead=False,liveSelectionChanged=False,spatialGeometryCalibrated=False))
    shutil.copyfile(package/'source/tools/backup-unity-run.ps1',package/'backup-run.ps1')
    files=[dict(Path=p.relative_to(h.io(package)).as_posix(),Sha256=h.digest(p)) for p in sorted(h.io(package).rglob('*')) if p.is_file()]
    dotnet=shutil.which('dotnet');assert dotnet
    save(package/'configuration.json',dict(Revision=revision,PreparedUtc=datetime.now(timezone.utc).isoformat(),PackageFiles=files,
        DotnetExecutable=dotnet,DotnetExecutableSha256=h.digest(dotnet),PythonExecutable=sys.executable,PythonExecutableSha256=h.digest(sys.executable),
        BackupDestination=str(backup),IsolatedCleftConstitutive=True,NoLiveMonitoring=True,ExpectedTestCases=40,ExpectedClassCounts=COUNTS,
        ExpectedTestMethods=methods,TestFilter='|'.join('FullyQualifiedName~'+c for c in COUNTS),ExpectedEdgeRows=468,ExpectedChargeRows=60,ExpectedSpatialRows=96,
        ExpectedDecimalPrecisions=[60,90],LiveSelectionChanged=False,TimeIntegrated=False,SpatialGeometryCalibrated=False,
        AfferentFiringQualified=False,PhysiologyQualified=False,AvatarRecoveryAssessed=False))
    pointer=dict(Package=str(package),Backup=str(backup),Revision=revision,NoLiveMonitoring=True,
        Launcher=str(package/'source/tools/start-cleft-coupling-window.ps1'))
    save(package.parent/(package.name+'-prepared.json'),pointer);print(json.dumps(pointer,indent=2),flush=True)


if __name__=='__main__':
    if not __debug__: raise RuntimeError('Assertions must remain enabled.')
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('calyx_review',type=Path);args=parser.parse_args()
    prepare(args.calyx_review.resolve())

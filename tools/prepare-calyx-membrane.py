"""Freeze this new isolated candidate and current dependencies; no model/test."""
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

for key in ('OMP_NUM_THREADS','OPENBLAS_NUM_THREADS','MKL_NUM_THREADS','NUMEXPR_NUM_THREADS'): os.environ[key]='1'
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'tools'))
from calyx_membrane_protocol import protocol, COUNTS


def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path)
    result=importlib.util.module_from_spec(spec); spec.loader.exec_module(result); return result


def save(path,value):
    with path.open('x',encoding='utf-8') as stream:
        json.dump(value,stream,indent=2,allow_nan=False); stream.write('\n')


def main():
    assert (ROOT/'tools/start-calyx-membrane-window.ps1').is_file()
    h=module('hair_cell_package',ROOT/'tools/prepare-causal-motor.py')
    gate=module('hair_cell_inventory',ROOT/'tools/receptor_membrane_gate.py')
    assert not h.git('status','--porcelain'), 'Commit clean source first.'
    revision=h.git('rev-parse','HEAD'); assert revision==h.git('rev-parse','origin/main'), 'Push first.'
    old='59b586b'
    preserved=['src/NRE.SimAvatar/AvatarClockedVestibularMechanics.cs',
        'ControlProgram/VestibularLabyrinthTransducer.cs','ControlProgram/PhysicalBodyTransducerRuntime.cs',
        'src/NRE.SimAvatar/AvatarPhysicalObservationProducer.cs','src/NRE.SimAvatar/AvatarPhysicalObservationTransport.cs',
        'tests/NeuralResonanceEngine.DNNE.Tests/ClockedVestibularMechanicsTests.cs',
        'tests/NeuralResonanceEngine.DNNE.Tests/VestibularLabyrinthTransducerTests.cs',
        'src/NRE.SimAvatar/AvatarVestibularHairCell.cs','tests/NeuralResonanceEngine.DNNE.Tests/VestibularHairCellTests.cs']
    for name in preserved:
        before=subprocess.check_output(['git','show',old+':'+name],cwd=ROOT).decode('utf-8-sig').replace('\r\n','\n')
        assert before==(ROOT/name).read_text(encoding='utf-8-sig'), name
    methods=gate.inventory(ROOT,COUNTS); assert sum(COUNTS.values())==23
    environment=module('hair_cell_environment',ROOT/'tools/golgi-reference-resume.py').environment()
    assert environment['numpy']=='2.2.6' and environment['scipy']=='1.16.3'
    # A new reference starts with today's explicitly inventoried runtime; this
    # is not an assertion of equivalence to an earlier Python binary build.
    source=ROOT/'artifacts/vestibular-calyx-source-20261116'
    audit=ROOT/'artifacts/vestibular-calyx-equations-20261117'
    names=['2022-12-11 Supporting Information.pdf','LICENSE','README.md','usedlicenses.txt']
    h.verify(source,names); h.verify(audit,['review.json','equation-review.md','xml-inspection/template.json','xml-inspection/paper_roles.json'])
    assert h.digest(source/names[0])=='C9145508AD7E705DEEA6F3749809438D112DA2D4889F1051F2380BCBB716EFA5'
    review_folder=ROOT/'artifacts/vestibular-hair-cell-review-20261117'
    h.verify(review_folder,['review.json']); review=h.read(review_folder/'review.json')
    assert review['operatorReportedCompletion'] and review['savedAnalysisExactlyReproduced'] and review['numericalCriteriaPassed'] and review['passedTests']==47
    assert h.digest(Path(review['capture'])/'backup-manifest.json')==review['manifestSha256']
    package=ROOT/'artifacts'/('calyx-membrane-'+datetime.now(timezone.utc).strftime('%Y%m%d')+'-'+uuid.uuid4().hex[:6])
    backup=Path('D:/DNNE-desktop-runs/UnityWorld')/package.name
    assert not package.exists() and not backup.exists(); package.mkdir()
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
                with target.open('xb') as stream: stream.write(archive.read(entry))
    copied=[]; (package/'source-evidence').mkdir()
    pairs=[(source,n) for n in names]+[(audit,n) for n in ['review.json','equation-review.md','xml-inspection/template.json','xml-inspection/paper_roles.json']]
    for origin,name in pairs:
        destination=package/'source-evidence'/name; destination.parent.mkdir(parents=True,exist_ok=True)
        shutil.copyfile(origin/name,destination)
        copied.append(dict(original=str(origin/name),path=destination.relative_to(package).as_posix(),sha256=h.digest(destination)))
    shutil.copyfile(review_folder/'review.json',package/'qualified-hair-cell-review.json')
    save(package/'calyx-input.json',protocol())
    save(package/'preparation-environment.json',environment)
    save(package/'provenance.json',dict(preservedRevision=h.git('rev-parse',old),preservedFiles=preserved,sourceEvidence=copied,
        referenceEnvironmentIsNewBaseline=True,historicalRuntimeEquivalenceClaimed=False,
        testsExecutedDuringPreparation=False,modelsExecutedDuringPreparation=False,noPendingCaptureRead=True,liveSelectionChanged=False))
    shutil.copyfile(package/'source/tools/backup-unity-run.ps1',package/'backup-run.ps1')
    files=[dict(Path=p.relative_to(h.io(package)).as_posix(),Sha256=h.digest(p)) for p in sorted(h.io(package).rglob('*')) if p.is_file()]
    dotnet=shutil.which('dotnet'); assert dotnet
    save(package/'configuration.json',dict(Revision=revision,PreparedUtc=datetime.now(timezone.utc).isoformat(),PackageFiles=files,
        DotnetExecutable=dotnet,DotnetExecutableSha256=h.digest(dotnet),PythonExecutable=sys.executable,
        PythonExecutableSha256=h.digest(sys.executable),BackupDestination=str(backup),IsolatedCalyxMembrane=True,
        NoLiveMonitoring=True,ExpectedVariants=6,
        ExpectedArithmeticRows=15984,ExpectedClampRows=224,ExpectedTestCases=23,ExpectedClassCounts=COUNTS,ExpectedTestMethods=methods,
        TestFilter='|'.join('FullyQualifiedName~'+c for c in COUNTS),LiveSelectionChanged=False,
        FreeMembraneIntegrated=False,CleftDynamicsIntegrated=False,AfferentFiringQualified=False,PhysiologyQualified=False,AvatarRecoveryAssessed=False))
    pointer=dict(Package=str(package),Backup=str(backup),Revision=revision,NoLiveMonitoring=True,
        Launcher=str(package/'source/tools/start-calyx-membrane-window.ps1'))
    save(package.parent/(package.name+'-prepared.json'),pointer); print(json.dumps(pointer,indent=2),flush=True)


if __name__=='__main__':
    if not __debug__: raise RuntimeError('Assertions must remain enabled.')
    main()

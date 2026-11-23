"""Freeze complete routed-path checks using the reviewed component outcomes."""
import argparse
from collections import Counter
from datetime import datetime,timezone
import importlib.util
import hashlib
from pathlib import Path,PurePosixPath
import shutil
import subprocess
import sys
import uuid
import xml.etree.ElementTree as ET
from zipfile import ZipFile

ROOT=Path(__file__).resolve().parents[1]
BASE='12229b9'
COUNTS=dict(RoutedMusclePathTests=18,CylinderMusclePathTests=52,PatellarPathTests=24,MusclePathGeometryTests=31,CausalMotorPlantTests=27,DynamicMuscleTests=16,SolutionManifestTests=1)
CHANGES=sorted(['src/NRE.SimAvatar/AvatarRoutedMusclePath.cs','UnityWorld/Assets/NRE/Runtime/RoutedMusclePathGeometry.cs','UnityWorld/Assets/NRE/Runtime/RoutedMusclePathGeometry.cs.meta','tests/NeuralResonanceEngine.DNNE.Tests/RoutedMusclePathTests.cs','tests/NeuralResonanceEngine.DNNE.Tests/NeuralResonanceEngine.DNNE.Tests.csproj'])

def module(name,path):
    s=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m
h=module('patellar_package_helpers',ROOT/'tools/native_avatar_tendon_study.py');h.COUNTS=COUNTS
read,write,digest=h.read,h.write,h.digest
fresh=module('patellar_runtime_helpers',ROOT/'tools/fresh_motor_epoch_study.py')
def numeric_runtime():
    import mpmath
    assert mpmath.__version__=='1.3.0' and mpmath.libmp.BACKEND=='python','Pinned pure-Python reference backend required.'
    root=Path(mpmath.__file__).parent
    return dict(version=mpmath.__version__,backend=mpmath.libmp.BACKEND,
        files=[dict(Path=str(p),Sha256=digest(p)) for p in sorted(root.rglob('*.py'))])
def prepare():
    assert not h.git('status','--porcelain'),'Commit clean source first.'
    revision=h.git('rev-parse','HEAD');assert revision==h.git('rev-parse','origin/main'),'Push first.'
    changed=sorted(h.git('diff','--name-only',BASE,revision,'--','src','Protocol','Shared.Contracts','Structures','ControlProgram','UnityWorld','tests','tools/NativeMotorBridgeProbe').splitlines())
    assert changed==CHANGES,changed
    methods=h.inventory();assert sum(methods.values())==169
    name='routed-muscle-path-'+datetime.now(timezone.utc).strftime('%Y%m%d')+'-'+uuid.uuid4().hex[:6]
    package=Path('D:/DNNE-build-work')/name;backup=Path('D:/DNNE-desktop-runs/UnityWorld')/name
    assert not package.exists() and not backup.exists();package.mkdir()
    subprocess.run(['git','archive','--format=zip','--output='+str(package/'source.zip'),revision],cwd=ROOT,check=True)
    with ZipFile(package/'source.zip') as archive:
        for e in archive.infolist():
            p=PurePosixPath(e.filename);assert not p.is_absolute() and '..' not in p.parts and ':' not in e.filename and '\\' not in e.filename
            assert ((e.external_attr>>16)&0o170000)!=0o120000
            target=package/'source'/p
            if e.is_dir():target.mkdir(parents=True,exist_ok=True)
            else:
                target.parent.mkdir(parents=True,exist_ok=True)
                with target.open('xb') as f:f.write(archive.read(e))
    protect=module('patellar_source_preservation',ROOT/'tools/native_articulated_head_study.py').protected_copy
    source=ROOT/'artifacts/patellar-input-20261123'
    protect(source,package/'patellar-input',[e['Path'].replace('\\','/') for e in read(source/'backup-manifest.json')['Files']])
    protect(ROOT/'artifacts/cylinder-reference-dependency-20261123',package/'reference-dependency',['mpmath-1.3.0-py3-none-any.whl','pypi-1.3.0.json','provenance.json'])
    for folder in ('cylinder-muscle-path-20261123-0b9165-review','patellar-path-20261123-a9bac5-review'):
        source=ROOT/'artifacts'/folder
        protect(source,package/folder,[e['Path'].replace('\\','/') for e in read(source/'backup-manifest.json')['Files']])
    shutil.copyfile(package/'source/tools/backup-unity-run.ps1',package/'backup-run.ps1')
    python=Path(sys.executable);dotnet=Path(shutil.which('dotnet'))
    files=[dict(Path=p.relative_to(package).as_posix(),Sha256=digest(p)) for p in sorted(package.rglob('*')) if p.is_file()]
    write(package/'configuration.json',dict(Revision=revision,PackageFiles=files,BackupDestination=str(backup),NoLiveMonitoring=True,
        PythonExecutable=str(python),PythonExecutableSha256=digest(python),PythonRuntime=h.python_runtime(),NumericalRuntime=numeric_runtime(),
        DotnetExecutable=str(dotnet),DotnetExecutableSha256=digest(dotnet),ExpectedTestCases=169,ExpectedTestMethods=methods,
        TestFilter='|'.join('FullyQualifiedName~'+c for c in COUNTS),ReferenceCases=0,ReferenceDigits=[],
        SourceDeclaration=dict(baseRevision=h.git('rev-parse',BASE),changedFiles=changed,originalBiologicalKernelsUnchanged=True),
        RoutedComponentStudy=True,LiveBrainAccessed=False,NativeUnityExecuted=False,AnatomicalCalibrationQualified=False,AvatarStandingQualified=False))
    preflight(package);print(str(package),flush=True)
def preflight(package):
    cfg=read(package/'configuration.json');assert cfg['NoLiveMonitoring'] and cfg['RoutedComponentStudy'] and cfg['ExpectedTestCases']==169
    assert cfg['ReferenceCases']==0 and cfg['ReferenceDigits']==[] and cfg['ExpectedTestMethods']==h.inventory()
    assert cfg['SourceDeclaration']['changedFiles']==CHANGES and cfg['SourceDeclaration']['originalBiologicalKernelsUnchanged']
    assert not any(cfg[k] for k in ('LiveBrainAccessed','NativeUnityExecuted','AnatomicalCalibrationQualified','AvatarStandingQualified'))
    for e in cfg['PackageFiles']:assert digest(package/e['Path'])==e['Sha256'],e['Path']
    for key in ('Python','Dotnet'):assert digest(cfg[key+'Executable'])==cfg[key+'ExecutableSha256']
    assert cfg['PythonRuntime']==h.python_runtime() and cfg['NumericalRuntime']==numeric_runtime()
    wheel=package/'reference-dependency/mpmath-1.3.0-py3-none-any.whl'
    assert digest(wheel)=='A0B2B9FE80BBCD81A6647FF13108738CFB482D481D826CC0E02F5B35E5C88D2C'
    import mpmath
    parent=Path(mpmath.__file__).parent.parent
    with ZipFile(wheel) as archive:
        expected={e.filename:hashlib.sha256(archive.read(e)).hexdigest().upper() for e in archive.infolist() if e.filename.startswith('mpmath/') and e.filename.endswith('.py')}
    assert expected=={Path(e['Path']).relative_to(parent).as_posix():e['Sha256'] for e in cfg['NumericalRuntime']['files']}
    for script,target in [('prepare-unity-routed-path.py','RoutedMusclePathGeometry.cs'),('prepare-unity-cylinder-paths.py','CylinderMusclePathGeometry.cs'),('prepare-unity-patellar-path.py','PatellarPathGeometry.cs')]:
        assert module('generated_'+target,ROOT/'tools'/script).generate(ROOT)==(ROOT/'UnityWorld/Assets/NRE/Runtime'/target).read_text(encoding='utf-8-sig')
    for folder,count in [('cylinder-muscle-path-20261123-0b9165-review',24),('patellar-path-20261123-a9bac5-review',450)]:
        reviewed=read(package/folder/'review.json')
        assert reviewed['operatorReportedCompletion'] and reviewed['independentGeometryCriteriaPassed'] and reviewed['independentComparisonCases']==count
    decoded=module('patellar_source_decode',ROOT/'tools/capture_patellar_inputs.py').decode(package/'patellar-input/anatomy/Rajagopal2016.osim')
    assert digest(package/'patellar-input/anatomy/Rajagopal2016.osim')=='3F5C5F23E486073F2AD2AA4A4967FFE2FCDD582B1E355512BC54F70C36376BF4'
    assert digest(package/'patellar-input/function/SimmSpline.cpp')=='371A2D507FE96622B3F6B0FDEFD44032A490F5975034D76241CB5A10CE690B5C'
    assert decoded==read(package/'patellar-input/patellar-source.json')==read(ROOT/'tests/NeuralResonanceEngine.DNNE.Tests/Fixtures/PatellarSource.json')
    # Git archives normalize text line endings. Original protected bytes are
    # still hashed above; additionally require identical canonical UTF-8 text.
    assert (ROOT/'tests/NeuralResonanceEngine.DNNE.Tests/Fixtures/PatellarSource.json').read_text(encoding='utf-8-sig')==(package/'patellar-input/patellar-source.json').read_text(encoding='utf-8-sig')
    assert (package/'patellar-input/AvatarArticulatedBody.cs').read_text(encoding='utf-8-sig')==(ROOT/'src/NRE.SimAvatar/AvatarArticulatedBody.cs').read_text(encoding='utf-8-sig')
    print('Original biological kernels, routed candidate/parity, source curves/attachments and numerical libraries preserved; no model/test executed by preflight.',flush=True)

def check_tests(package):
    cfg=read(package/'configuration.json');ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
    tree=ET.parse(package/'test-results/focused.trx');defs={r.attrib['id']:r.find('t:TestMethod',ns).attrib for r in tree.findall('.//t:UnitTest',ns)}
    actual=Counter();rows=tree.findall('.//t:UnitTestResult',ns)
    for row in rows:
        assert row.attrib['outcome']=='Passed',row.attrib
        m=defs[row.attrib['testId']];actual[m['className'].split(',')[0]+'.'+m['name']]+=1
    assert len(rows)==169 and dict(actual)==cfg['ExpectedTestMethods']==h.inventory()
    write(package/'component-analysis.json',dict(completed=True,passedCases=169,newRoutedCases=18,exactInventory=True,
        unitySourceParityChecked=True,nativeUnityExecuted=False,anatomicalCalibrationQualified=False,avatarStandingQualified=False))
    print('169 component/regression cases passed; complete analytic routed force/work checks included; actual native constrained forces remain unqualified.',flush=True)
if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    p=argparse.ArgumentParser();p.add_argument('stage',choices=('prepare','preflight','runtime','check-tests'));p.add_argument('package',nargs='?',type=Path);a=p.parse_args()
    if a.stage=='prepare':prepare()
    else:
        assert a.package is not None
        if a.stage=='runtime':fresh.runtime(a.package.resolve())
        else:globals()[a.stage.replace('-','_')](a.package.resolve())

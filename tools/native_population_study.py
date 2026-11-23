"""Freeze the next native population comparison; prepare/preflight run no model."""
import argparse
from collections import Counter
from datetime import datetime,timezone
import importlib.util
from pathlib import Path,PurePosixPath
import shutil
import subprocess
import sys
import uuid
import xml.etree.ElementTree as ET
from zipfile import ZipFile

ROOT=Path(__file__).resolve().parents[1]
BASE='a137b9d'
PLAYER=Path('D:/DNNE-build-work/named-muscle-native-20261123-998900')
ACTIVE=Path('D:/DNNE-build-work/muscle-population-active-20261123-a212b5')
def module(name,path):
    s=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m
fresh=module('population_native_fresh',ROOT/'tools/fresh_motor_epoch_study.py')
h=fresh.helper
read,write,digest=h.read,h.write,h.digest
protect=module('population_native_protect',ROOT/'tools/native_articulated_head_study.py').protected_copy
CASES=[dict(name=n,left=l,right=r,cut=c,retry=t,substeps=s) for n,l,r,c,t,s in (
    ('quiet',False,False,False,False,160),('left',True,False,False,False,160),('right',False,True,False,False,160),
    ('both',True,True,False,False,160),('both-cut',True,True,True,False,160),('both-retry',True,True,False,True,160),
    ('quiet-finer',False,False,False,False,320),('both-finer',True,True,False,False,320))]
LIMITS=dict(nativePointMeters=2e-6,pathLengthMeters=2e-12,forceNewtons=1e-10,responseRadians=1e-6)
def configure():
    assert fresh.configure(population=True)==379
    h.COUNTS['SharedMotorAnatomyTests']=9
    methods=h.inventory();assert sum(methods.values())==388
    return methods

def prepare():
    assert not h.git('status','--porcelain'),'Commit clean source first.'
    revision=h.git('rev-parse','HEAD');assert revision==h.git('rev-parse','origin/main'),'Push first.'
    changed=sorted(h.git('diff','--name-only',BASE,revision,'--','src','Protocol','Shared.Contracts','Structures','ControlProgram','UnityWorld','tests','tools/NativeMotorBridgeProbe').splitlines())
    assert changed==['tools/NativeMotorBridgeProbe/NativePopulationStudy.cs','tools/NativeMotorBridgeProbe/Program.cs'],changed
    methods=configure()
    name='native-muscle-population-'+datetime.now(timezone.utc).strftime('%Y%m%d')+'-'+uuid.uuid4().hex[:6]
    package=Path('D:/DNNE-build-work')/name;backup=Path('D:/DNNE-desktop-runs/UnityWorld')/name
    assert not package.exists() and not backup.exists();package.mkdir()
    subprocess.run(['git','archive','--format=zip','--output='+str(package/'source.zip'),revision],cwd=ROOT,check=True)
    with ZipFile(package/'source.zip') as archive:
        for e in archive.infolist():
            p=PurePosixPath(e.filename)
            assert not p.is_absolute() and '..' not in p.parts and ':' not in e.filename and '\\' not in e.filename
            assert ((e.external_attr>>16)&0o170000)!=0o120000
            target=package/'source'/p
            if e.is_dir():target.mkdir(parents=True,exist_ok=True)
            else:
                target.parent.mkdir(parents=True,exist_ok=True)
                with target.open('xb') as f:f.write(archive.read(e))
    for key,source in [('active',ROOT/'artifacts/muscle-population-active-20261123-a212b5-review'),
                       ('shared',ROOT/'artifacts/shared-motor-anatomy-20261123-5001cd-review'),
                       ('named',ROOT/'artifacts/named-muscle-native-20261123-998900-review')]:
        protect(source,package/(key+'-review'),[e['Path'].replace('\\','/') for e in read(source/'backup-manifest.json')['Files']])
    names=[Path(e['Path']).relative_to(PLAYER).as_posix() for e in read(PLAYER/'player-files.json')]
    assert len(names)==144 and all(n.startswith('player/') for n in names)
    sources=[p.relative_to(ROOT).as_posix() for p in (ROOT/'UnityWorld/Assets/NRE').rglob('*.cs')]
    for n in sources:assert (ROOT/n).read_text(encoding='utf-8-sig')==(PLAYER/'source'/n).read_text(encoding='utf-8-sig'),n
    protect(PLAYER,package/'qualified-player',names+['player-files.json']+['source/'+n for n in sources])
    protect(PLAYER,package/'original-input',['frame-review/mapped-lower-leg-paths.json','anatomical-input/binding-input.json','native/quiet/acknowledged.jsonl'])
    protect(ACTIVE,package/'active-input',['population-dynamics-input.json','managed-runtime-before.json'])
    inputs=package/'inputs';inputs.mkdir()
    for source,target in [('original-input/frame-review/mapped-lower-leg-paths.json','mapping.json'),
                          ('original-input/anatomical-input/binding-input.json','binding.json'),
                          ('original-input/native/quiet/acknowledged.jsonl','quiet.jsonl'),
                          ('active-input/population-dynamics-input.json','population.json')]:
        shutil.copyfile(package/source,inputs/target)
    write(inputs/'protocol.json',dict(version='native-full-slow-population-v1',bodySteps=40,cases=CASES))
    shutil.copyfile(package/'source/tools/backup-unity-run.ps1',package/'backup-run.ps1')
    python=Path(sys.executable);dotnet=Path(shutil.which('dotnet'))
    files=[dict(Path=p.relative_to(package).as_posix(),Sha256=digest(p)) for p in sorted(package.rglob('*')) if p.is_file()]
    write(package/'configuration.json',dict(Revision=revision,PackageFiles=files,BackupDestination=str(backup),NoLiveMonitoring=True,
        PythonExecutable=str(python),PythonExecutableSha256=digest(python),PythonRuntime=h.python_runtime(),
        DotnetExecutable=str(dotnet),DotnetExecutableSha256=digest(dotnet),ExpectedTestCases=388,ExpectedTestMethods=methods,
        TestFilter='|'.join('FullyQualifiedName~'+c for c in h.COUNTS),PlayerRelativePath='qualified-player/player/DNNE Unity World.exe',
        Cases=CASES,NativeLimits=LIMITS,ExpectedNativeCases=8,ExpectedPhysicalSteps=328,ExpectedEndpoints=336,ExpectedHeadReceipts=1008,
        ExpectedPointForces=2560,UnitsPerMuscle=147,PrescribedDiagnosticEvents=True,SharedOwnerRegressionIncluded=True,
        SourceDeclaration=dict(baseRevision=h.git('rev-parse',BASE),changedFiles=changed,biologicalKernelsUnchanged=True,sharedOwnerFixRetained=True),
        NeuronalAuthorityQualified=False,IndependentCoupledTrajectoryQualified=False,AnatomicalFitQualified=False,AvatarStandingQualified=False))
    preflight(package)
    print(str(package),flush=True)

def preflight(package):
    cfg=read(package/'configuration.json');assert cfg['NoLiveMonitoring'] and cfg['ExpectedTestCases']==388
    assert cfg['Cases']==CASES and cfg['NativeLimits']==LIMITS
    assert [cfg[k] for k in ('ExpectedNativeCases','ExpectedPhysicalSteps','ExpectedEndpoints','ExpectedHeadReceipts','ExpectedPointForces','UnitsPerMuscle')]==[8,328,336,1008,2560,147]
    assert cfg['PrescribedDiagnosticEvents'] and cfg['SharedOwnerRegressionIncluded']
    assert not any(cfg[k] for k in ('NeuronalAuthorityQualified','IndependentCoupledTrajectoryQualified','AnatomicalFitQualified','AvatarStandingQualified'))
    for e in cfg['PackageFiles']:assert digest(package/e['Path'])==e['Sha256'],e['Path']
    for name in ('Python','Dotnet'):assert digest(cfg[name+'Executable'])==cfg[name+'ExecutableSha256']
    assert cfg['PythonRuntime']==h.python_runtime() and cfg['ExpectedTestMethods']==configure()
    a=read(package/'active-review/review.json');s=read(package/'shared-review/review.json');n=read(package/'named-review/review.json')
    assert a['operatorReportedCompletion'] and a['activePopulationNumericalCriteriaPassed'] and a['candidateTraces']==a['independentReferences']==24
    assert s['operatorReportedCompletion'] and s['sharedMotorAnatomyChecksPassed'] and s['passedManagedTests']==164 and s['fullPopulationPerSide']==147
    assert n['operatorReportedCompletion'] and n['namedNativeForceAccountingPassed'] and n['verifiedPlayerFiles']==144
    for source,target in [('original-input/frame-review/mapped-lower-leg-paths.json','mapping.json'),('original-input/anatomical-input/binding-input.json','binding.json'),
                          ('original-input/native/quiet/acknowledged.jsonl','quiet.jsonl'),('active-input/population-dynamics-input.json','population.json')]:
        assert digest(package/source)==digest(package/'inputs'/target)
    assert read(package/'inputs/protocol.json')==dict(version='native-full-slow-population-v1',bodySteps=40,cases=CASES)
    print('Completed population/shared-owner evidence, exact native player, original equations and 388-case inventory preserved; no model/test executed by preflight.',flush=True)

def runtime(package):
    fresh.runtime(package)
    assert read(package/'managed-runtime-before.json')==read(package/'active-input/managed-runtime-before.json'),'Selected runtime changed since active comparison.'

def check_tests(package):
    cfg=read(package/'configuration.json');ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
    tree=ET.parse(package/'test-results/focused.trx')
    defs={r.attrib['id']:r.find('t:TestMethod',ns).attrib for r in tree.findall('.//t:UnitTest',ns)}
    actual=Counter();rows=tree.findall('.//t:UnitTestResult',ns)
    for row in rows:
        assert row.attrib['outcome']=='Passed',row.attrib
        m=defs[row.attrib['testId']];actual[m['className'].split(',')[0]+'.'+m['name']]+=1
    assert len(rows)==388 and dict(actual)==cfg['ExpectedTestMethods']==configure()
    initial=fresh.initial_balance_reference().compare(package)
    write(package/'component-analysis.json',dict(completed=True,passedCases=388,exactInventory=True,sharedAnatomyCases=9,initialBalancePassed=initial['passed']))
    print('388 component/regression cases and eight independent initial equilibria passed. Native coupling remains a separate check.',flush=True)

if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    p=argparse.ArgumentParser();p.add_argument('stage',choices=('prepare','preflight','runtime','check-tests','compare'));p.add_argument('package',nargs='?',type=Path);a=p.parse_args()
    if a.stage=='prepare':prepare()
    else:
        assert a.package is not None
        if a.stage=='compare':module('native_population_analysis',ROOT/'tools/native_population_analysis.py').compare(a.package.resolve())
        else:globals()[a.stage.replace('-','_')](a.package.resolve())

"""Explicit original/no-friction bridge comparison; all execution stays in PowerShell."""
import argparse
from collections import Counter
from datetime import datetime, timezone
import json
import math
from pathlib import Path, PurePosixPath
import shutil
import subprocess
import sys
import uuid
import xml.etree.ElementTree as ET
from zipfile import ZipFile
import importlib.util

ROOT=Path(__file__).resolve().parents[1]
BASE='914c2e4'
FRICTION=Path('D:/DNNE-build-work/tendon-friction-20261123-9d34cc')
def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path);r=importlib.util.module_from_spec(spec);spec.loader.exec_module(r);return r
h=module('declared_mechanics_head',ROOT/'tools/native_articulated_head_study.py')
components=module('declared_mechanics_components',ROOT/'tools/fresh_motor_epoch_study.py')
mechanical=module('declared_mechanics_saved_settings',ROOT/'tools/native_tendon_mechanics_study.py')
read,write,digest=h.read,h.write,h.digest
MODES=('original','no-joint-friction')
ALLOWED={'UnityWorld/Assets/NRE/Runtime/'+n+'.cs' for n in ('ArticulatedTendonWire','ArticulatedTendonBridge','TendonMechanicsDiagnostic')}
ALLOWED|={'tools/NativeMotorBridgeProbe/'+n+'.cs' for n in ('NativeArticulatedBody','ArticulatedHeadNativeStudy','Program')}
ALLOWED|={'tests/NeuralResonanceEngine.DNNE.Tests/'+n+'.cs' for n in ('ArticulatedTendonBridgeTests','ArticulatedMechanicsSelectionTests')}

def inventory():
    components.configure(head=True);components.helper.COUNTS['ArticulatedMechanicsSelectionTests']=10
    result=components.helper.inventory();assert sum(result.values())==341;return result

def prepare():
    assert not h.h.git('status','--porcelain'),'Commit clean source first.'
    revision=h.h.git('rev-parse','HEAD');assert revision==h.h.git('rev-parse','origin/main')
    changed=set(h.h.git('diff','--name-only',BASE,revision,'--','src','Structures','ControlProgram','tests','Shared.Contracts','Protocol','UnityWorld','tools/NativeMotorBridgeProbe').splitlines())
    assert changed==ALLOWED,changed
    name='declared-body-mechanics-'+datetime.now(timezone.utc).strftime('%Y%m%d')+'-'+uuid.uuid4().hex[:6]
    package=Path('D:/DNNE-build-work')/name;backup=Path('D:/DNNE-desktop-runs/UnityWorld')/name
    assert not package.exists() and not backup.exists();package.mkdir()
    for kind,run in (('friction','tendon-friction-20261123-9d34cc'),('head','native-articulated-head-20261123-e19c4a')):
        review=ROOT/'artifacts'/(run+'-review');proof=read(review/'review.json');assert proof['operatorReportedCompletion']
        if kind=='friction':assert proof['fullFrictionMatrix'] and proof['accumulatorChecksPassed'] and proof['accumulatorChecks']==92640
        else:assert proof['nativeHeadDeliveryChecksPassed'] and proof['headReceipts']==162
        h.protected_copy(review,package/(kind+'-review'),[e['Path'].replace('\\','/') for e in read(review/'backup-manifest.json')['Files']])
    h.protected_copy(FRICTION,package/'friction-evidence',['configuration.json']+[f'native/{mode}__quiet/{file}' for mode in MODES for file in ('result.json','endpoints.jsonl')])
    h.protected_copy(h.HEAD,package/'component-evidence',['managed-runtime-before.json'])
    subprocess.run(['git','archive','--format=zip','--output='+str(package/'source.zip'),revision],cwd=ROOT,check=True)
    with ZipFile(package/'source.zip') as archive:
        for entry in archive.infolist():
            path=PurePosixPath(entry.filename)
            assert not path.is_absolute() and '..' not in path.parts and ':' not in entry.filename and '\\' not in entry.filename
            assert ((entry.external_attr>>16)&0o170000)!=0o120000
            target=package/'source'/path
            if entry.is_dir():target.mkdir(parents=True,exist_ok=True)
            else:
                target.parent.mkdir(parents=True,exist_ok=True)
                with target.open('xb') as stream:stream.write(archive.read(entry))
    for entry in read(ROOT/'UnityWorld/Assets/Avatar/provenance.json')['files']:
        source=ROOT/'UnityWorld/Assets/Avatar'/entry['file'];assert source.name in ('MikeAlger.fbx','MikeAlger_Texture.jpeg') and digest(source)==entry['sha256'].upper()
        shutil.copyfile(source,package/'source/UnityWorld/Assets/Avatar'/source.name)
    metadata=module('declared_mechanics_metadata',ROOT/'tools/unity_metadata.py');write(package/'unity-metadata.json',metadata.validate(package/'source/UnityWorld'))
    for mode in MODES:
        folder=package/'conditions'/mode;folder.mkdir(parents=True)
        write(folder/'configuration.json',dict(MechanicsMode=mode,OriginalRelativePath='../../friction-evidence/native/'+mode+'__quiet',DerivedAbsoluteBound=1e-8,DerivedRelativeBound=1e-10))
    shutil.copyfile(package/'source/tools/backup-unity-run.ps1',package/'backup-run.ps1')
    methods=inventory();old=read(FRICTION/'configuration.json')
    unity=Path(old['UnityExecutable']);python=Path(sys.executable);dotnet=Path(shutil.which('dotnet'))
    files=[dict(Path=p.relative_to(package).as_posix(),Sha256=digest(p)) for p in sorted(package.rglob('*')) if p.is_file()]
    write(package/'configuration.json',dict(Revision=revision,PackageFiles=files,BackupDestination=str(backup),NoLiveMonitoring=True,
        NativeArticulatedHeadStudy=True,DeclaredBodyMechanicsStudy=True,RequiredComponentCases=341,ExpectedTestMethods=methods,
        TestFilter='|'.join('FullyQualifiedName~'+c for c in components.helper.COUNTS),Modes=MODES,ExpectedCases=12,
        ExpectedPhysicsSteps=96,ExpectedEndpoints=108,ExpectedHeadReceipts=324,ExpectedInspections=108,
        MaximumLimitViolationRadians=0.05000000074505806,
        UnityExecutable=str(unity),UnityExecutableSha256=digest(unity),UnityBuildWorkspace=str(package.parent/(package.name+'-unity')),
        PythonExecutable=str(python),PythonExecutableSha256=digest(python),PythonRuntime=h.h.python_runtime(),
        DotnetExecutable=str(dotnet),DotnetExecutableSha256=digest(dotnet),
        NeuronalSourceExecuted=False,MuscleCellExecuted=False,PhysiologicalFiringQualified=False,AvatarBalanceQualified=False))
    preflight(package)
    print(json.dumps(dict(Package=str(package),Backup=str(backup),Revision=revision,
        Launcher=str(package/'source/tools/start-declared-body-mechanics-window.ps1')),indent=2),flush=True)

def preflight(package):
    cfg=read(package/'configuration.json');assert cfg['NoLiveMonitoring'] and cfg['DeclaredBodyMechanicsStudy'] and cfg['RequiredComponentCases']==341
    assert cfg['Modes']==list(MODES) and [cfg[k] for k in ('ExpectedCases','ExpectedPhysicsSteps','ExpectedEndpoints','ExpectedHeadReceipts','ExpectedInspections')]==[12,96,108,324,108]
    assert cfg['MaximumLimitViolationRadians']==0.05000000074505806
    assert cfg['ExpectedTestMethods']==inventory() and cfg['PythonRuntime']==h.h.python_runtime()
    for key in ('NeuronalSourceExecuted','MuscleCellExecuted','PhysiologicalFiringQualified','AvatarBalanceQualified'):assert cfg[key] is False
    for e in cfg['PackageFiles']:assert digest(package/e['Path'])==e['Sha256'],e['Path']
    for name in ('Dotnet','Python','Unity'):assert digest(cfg[name+'Executable'])==cfg[name+'ExecutableSha256']
    print('Original mechanical choices, biological kernels, completed evidence and runtime declarations preserved; no model/test executed by preflight.',flush=True)

def check_tests(package):
    cfg=read(package/'configuration.json');ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
    tree=ET.parse(package/'test-results/focused.trx')
    definitions={r.attrib['id']:r.find('t:TestMethod',ns).attrib for r in tree.findall('.//t:UnitTest',ns)}
    actual=Counter();rows=tree.findall('.//t:UnitTestResult',ns)
    for row in rows:
        assert row.attrib['outcome']=='Passed',row.attrib
        m=definitions[row.attrib['testId']];actual[m['className'].split(',')[0]+'.'+m['name']]+=1
    assert len(rows)==341 and dict(actual)==cfg['ExpectedTestMethods']==inventory()
    print('All 341 exact component cases passed, including 10 explicit mechanical-selection controls.',flush=True)

def compare(package):
    cfg=read(package/'configuration.json');analyses=[];settings=0;limits=[]
    for mode in MODES:
        folder=package/'conditions'/mode
        for name,_,_ in h.CASES:
            case=folder/'native'/name;configuration=read(case/'input.json')
            assert configuration['protocol']=='full-avatar-tendon-wire-v3' and configuration['mechanicsMode']==mode
            baseline=None
            for file in sorted((case/'tendon-bridge').glob('*.json')):
                if file.name!='hello.json' and not file.name.startswith('reply-'):continue
                reply=read(file);evidence=json.loads(reply['mechanicsDiagnosticJson']);mechanical.settings(evidence,mode)
                if baseline is None:baseline=reply['mechanicsDiagnosticJson']
                assert reply['mechanicsDiagnosticJson']==baseline
                geometry=json.loads(reply['geometryJson']);expected=h.h.struct.unpack('<f',h.h.struct.pack('<f',.05 if mode=='original' else 0.))[0]
                assert all(h.h.struct.unpack('<f',h.h.struct.pack('<f',b['jointFriction']))[0]==expected for b in geometry['links'])
                settings+=1
            f32=lambda value:h.h.struct.unpack('<f',h.h.struct.pack('<f',value))[0]
            for endpoint in h.rows(case/'acknowledged.jsonl'):
                geometry=json.loads(endpoint['geometryJson']);bodies={b['id']:b for b in json.loads(endpoint['stateJson'])['links']}
                assert sum(len(b['activeNativeIndices']) for b in geometry['links'])==19
                for link in geometry['links']:
                    for axis,(index,drive) in enumerate(zip(link['activeNativeIndices'],link['activeDrives'],strict=True)):
                        angle=h.h.real(bodies[link['id']]['jointPositionBits'][index])
                        lower=f32(h.h.real(drive['lowerBits'])*f32(math.pi/180));upper=f32(h.h.real(drive['upperBits'])*f32(math.pi/180))
                        violation=max(0.,f32(lower-angle),f32(angle-upper))
                        limits.append(dict(mode=mode,case=name,step=endpoint['physicsStep'],joint=link['id'],axis=axis,
                            angleRadians=angle,lowerRadians=lower,upperRadians=upper,violationRadians=violation,passed=violation<cfg['MaximumLimitViolationRadians']))
        h.compare(folder);analyses.append(read(folder/'analysis.json'))
    assert all(a['nativeHeadDeliveryChecksPassed'] for a in analyses) and settings==240 and len(limits)==2052
    limits_ok=all(r['passed'] for r in limits);write(package/'joint-limit-observations.json',limits)
    write(package/'analysis.json',dict(completed=True,declaredMechanicsChecksPassed=limits_ok,passedComponentCases=341,
        modes=list(MODES),nativeCases=12,actualPhysicsSteps=96,physicalEndpoints=108,headReceipts=324,extraInspections=108,
        mechanicalSettingReceipts=settings,derivedChecks=sum(a['derivedChecks'] for a in analyses),
        jointLimitObservations=len(limits),existingJointLimitGuardPassed=limits_ok,maximumJointLimitViolationRadians=max(r['violationRadians'] for r in limits),
        originalAndNoFrictionQuietReplaysIdentical=True,inspectionPreserved=True,
        physiologicalFiringQualified=False,neuronalSourceExecuted=False,muscleCellExecuted=False,avatarBalanceQualified=False))
    if not limits_ok:raise RuntimeError('Existing joint-limit guard failed; retain the original bounds and every endpoint.')
    print('Both declared mechanical modes preserve native delivery and their original quiet trajectories: 12 cases / 96 steps / 324 head receipts. Standing remains unqualified.',flush=True)

if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    p=argparse.ArgumentParser();p.add_argument('stage',choices=('prepare','preflight','runtime','check-tests','compare'));p.add_argument('package',nargs='?',type=Path);a=p.parse_args()
    if a.stage=='prepare':prepare()
    else:
        assert a.package is not None
        {'preflight':preflight,'runtime':h.runtime,'check-tests':check_tests,'compare':compare}[a.stage](a.package.resolve())

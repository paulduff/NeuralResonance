"""Frozen actual full-body/head bridge study; no model runs during preparation."""
import argparse
from datetime import datetime, timezone
import importlib.util
import json
import math
from pathlib import Path, PurePosixPath
import shutil
import subprocess
import sys
import uuid
from zipfile import ZipFile

ROOT=Path(__file__).resolve().parents[1]
BASE='712e3251e877ccb46073d6535dda60ca9617e1cc'
PLAYER=Path('D:/DNNE-build-work/tendon-mechanics-20261123-9d73a7')
ORIGINAL=Path('D:/DNNE-build-work/tendon-force-20261123-e764e3')
HEAD=Path('D:/DNNE-build-work/articulated-head-20261123-7bc04c')
CASES=[('quiet','',0),('hip','leftHip/flexor',0),('shoulder','leftShoulderAbduction/flexor',0),
       ('quiet-inspected','',2),('hip-inspected','leftHip/flexor',2),('shoulder-inspected','leftShoulderAbduction/flexor',2)]

def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path);result=importlib.util.module_from_spec(spec);spec.loader.exec_module(result);return result
h=module('articulated_native_helpers',ROOT/'tools/native_avatar_tendon_study.py')
read,write,digest=h.read,h.write,h.digest

def protected_copy(source,destination,names):
    manifest=read(source/'backup-manifest.json');backup=Path(manifest['Destination'])
    assert digest(source/'backup-manifest.json')==digest(backup/'backup-manifest.json')
    entries={e['Path'].replace('\\','/'):e for e in manifest['Files']}
    for name in names:
        assert digest(source/name)==digest(backup/name)==entries[name]['Sha256'],name
        target=destination/name;target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(source/name,target)
    shutil.copyfile(source/'backup-manifest.json',destination/'backup-manifest.json')

def prepare():
    assert not h.git('status','--porcelain'),'Commit clean source first.'
    revision=h.git('rev-parse','HEAD');assert revision==h.git('rev-parse','origin/main')
    changed=set(h.git('diff','--name-only',BASE,revision,'--','src','Structures','tests','Shared.Contracts','Protocol','UnityWorld','tools/NativeMotorBridgeProbe').splitlines())
    assert changed=={'tools/NativeMotorBridgeProbe/Program.cs','tools/NativeMotorBridgeProbe/ArticulatedHeadNativeStudy.cs'},changed
    head_review=ROOT/'artifacts/articulated-head-20261123-7bc04c-review'; reviewed=read(head_review/'review.json')
    assert reviewed['passedTests']==331 and reviewed['newHeadCases']==16 and reviewed['operatorReportedCompletion'] and reviewed['componentChecksPassed']
    mechanics_review=ROOT/'artifacts/tendon-mechanics-20261123-9d73a7-review'; mechanical=read(mechanics_review/'review.json')
    assert mechanical['verifiedPlayerFiles']==144 and mechanical['operatorReportedCompletion'] and mechanical['passedTests']==112
    name='native-articulated-head-'+datetime.now(timezone.utc).strftime('%Y%m%d')+'-'+uuid.uuid4().hex[:6]
    package=Path('D:/DNNE-build-work')/name;backup=Path('D:/DNNE-desktop-runs/UnityWorld')/name
    assert not package.exists() and not backup.exists();package.mkdir()
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
    for source,target in ((head_review,package/'component-review'),(mechanics_review,package/'mechanical-review')):
        protected_copy(source,target,[e['Path'].replace('\\','/') for e in read(source/'backup-manifest.json')['Files']])
    protected_copy(HEAD,package/'component-evidence',['configuration.json','analysis.json','window-status.json','managed-runtime-before.json','managed-runtime-after.json'])
    player_names=[Path(e['Path']).relative_to(PLAYER).as_posix() for e in read(PLAYER/'player-files.json')]
    assert len(player_names)==144 and all(n.startswith('player/') for n in player_names)
    source_names=[p.relative_to(ROOT).as_posix() for p in (ROOT/'UnityWorld/Assets/NRE').rglob('*.cs')]
    for name in source_names:assert (ROOT/name).read_text(encoding='utf-8-sig')==(PLAYER/'source'/name).read_text(encoding='utf-8-sig'),name
    protected_copy(PLAYER,package/'qualified-player',player_names+['player-files.json','configuration.json']+['source/'+n for n in source_names])
    protected_copy(ORIGINAL,package/'original-evidence',['native/quiet-observed/result.json','native/quiet-observed/endpoints.jsonl'])
    shutil.copyfile(package/'source/tools/backup-unity-run.ps1',package/'backup-run.ps1')
    python=Path(sys.executable);dotnet=Path(shutil.which('dotnet'))
    files=[dict(Path=p.relative_to(package).as_posix(),Sha256=digest(p)) for p in sorted(package.rglob('*')) if p.is_file()]
    write(package/'configuration.json',dict(Revision=revision,PackageFiles=files,BackupDestination=str(backup),NoLiveMonitoring=True,
        PythonExecutable=str(python),PythonExecutableSha256=digest(python),PythonRuntime=h.python_runtime(),
        DotnetExecutable=str(dotnet),DotnetExecutableSha256=digest(dotnet),NativeArticulatedHeadStudy=True,
        PlayerRelativePath='qualified-player/player/DNNE Unity World.exe',OriginalRelativePath='original-evidence/native/quiet-observed',
        ComponentCasesReused=331,PlayerBuildReused=True,ExpectedCases=6,ExpectedPhysicsSteps=48,ExpectedEndpoints=54,
        ExpectedHeadReceipts=162,ExpectedInspections=54,DerivedAbsoluteBound=1e-8,DerivedRelativeBound=1e-10,
        NeuronalSourceExecuted=False,MuscleCellExecuted=False,PhysiologicalFiringQualified=False,AvatarBalanceQualified=False))
    preflight(package)
    print(json.dumps(dict(Package=str(package),Backup=str(backup),Revision=revision,
        Launcher=str(package/'source/tools/start-native-articulated-head-window.ps1')),indent=2),flush=True)

def preflight(package):
    cfg=read(package/'configuration.json')
    assert cfg['NoLiveMonitoring'] and cfg['NativeArticulatedHeadStudy'] and cfg['ComponentCasesReused']==331 and cfg['PlayerBuildReused']
    assert [cfg[k] for k in ('ExpectedCases','ExpectedPhysicsSteps','ExpectedEndpoints','ExpectedHeadReceipts','ExpectedInspections')]==[6,48,54,162,54]
    assert cfg['DerivedAbsoluteBound']==1e-8 and cfg['DerivedRelativeBound']==1e-10
    assert not any(cfg[k] for k in ('NeuronalSourceExecuted','MuscleCellExecuted','PhysiologicalFiringQualified','AvatarBalanceQualified'))
    for e in cfg['PackageFiles']:assert digest(package/e['Path'])==e['Sha256'],e['Path']
    for name in ('Python','Dotnet'):assert digest(cfg[name+'Executable'])==cfg[name+'ExecutableSha256']
    assert cfg['PythonRuntime']==h.python_runtime()
    assert read(package/'component-evidence/analysis.json')['passedCases']==331
    print('Passed component evidence, original geometry, qualified player and unchanged biological kernels preserved; no model/test executed by preflight.',flush=True)

def runtime(package):
    original=read(package/'component-evidence/managed-runtime-before.json');actual=read(package/'managed-runtime-before.json')
    assert actual==original,'Selected runtime changed; preserve the evidence before another experiment.'
    print('Exact selected 335-file runtime matches the completed component run; no model/test executed.',flush=True)

def rows(path):return [json.loads(line) for line in path.read_text(encoding='utf-8-sig').splitlines()]
def sha(text):return h.hashlib.sha256(text.encode()).hexdigest().upper()
def vec(d):return [d[k] for k in ('x','y','z')]
def sub(a,b):return [x-y for x,y in zip(a,b,strict=True)]
def dot(a,b):return sum(x*y for x,y in zip(a,b,strict=True))

def compare(package):
    cfg=read(package/'configuration.json');capture=package/'native';protocol=read(capture/'protocol.json');result=read(capture/'result.json')
    assert [(c['name'],c['muscle'],c['inspectionsPerEndpoint']) for c in protocol['cases']]==CASES
    assert protocol['prescribedTensionNewtons']==40 and protocol['passivePreludeSteps']==1 and protocol['originalSettingsRetained']
    original_report=read(package/cfg['OriginalRelativePath']/'result.json')
    original=rows(package/cfg['OriginalRelativePath']/'endpoints.jsonl')
    original_declarations=json.loads(json.loads(original_report['geometryJson'])['declarationsJson'])['paths']
    assert protocol['originalDeclarationsJson']==json.loads(original_report['geometryJson'])['declarationsJson']
    assert protocol['originalReportSha256']==sha((package/cfg['OriginalRelativePath']/'result.json').read_text(encoding='utf-8-sig'))
    assert result['completed'] and [result[k] for k in ('nativeCases','actualPhysicsSteps','physicalEndpoints','headReceipts','extraInspections')]==[6,48,54,162,54]
    all_ack={};all_heads={};checks=[];summaries=[]
    def close(label,actual,expected):
        assert math.isfinite(actual) and math.isfinite(expected),label
        allowance=cfg['DerivedAbsoluteBound']+cfg['DerivedRelativeBound']*abs(expected)
        checks.append(dict(label=label,error=abs(actual-expected),allowance=allowance,passed=abs(actual-expected)<=allowance))
    for name,muscle,inspect in CASES:
        folder=capture/name;status=read(folder/'result.json');wire=folder/'tendon-bridge';configuration=read(folder/'input.json')
        assert status['completed'] and status['endpoints']==9 and status['inspections']==inspect*9 and status['physicsSteps']==8 and status['headReceipts']==27
        assert configuration==read(wire/'configuration.json') and configuration['passivePreludeSteps']==1
        expected=[dict(muscleId=p['id'],provenance=p['provenance'],anchors=[dict(id=a['id'],bodyId=a['bodyId'],
            x=dict(bits=a['xBits']),y=dict(bits=a['yBits']),z=dict(bits=a['zBits'])) for a in p['anchors']]) for p in original_declarations]
        assert configuration['declarations']==expected
        assert h.real(configuration['stepSeconds']['bits'])==h.struct.unpack('<f',h.struct.pack('<f',.005))[0]
        step_ms=h.real(configuration['stepSeconds']['bits'])*1000;clock=read(folder/'clock.json')['clock']
        assert clock['initialPhysicsStep']==0 and clock['physicalEpochMs']==0 and clock['neuralEpochMs']==1000 and clock['physicsStepMs']==step_ms
        ack=rows(folder/'acknowledged.jsonl');heads=rows(folder/'heads.jsonl');inspections=rows(folder/'inspections.jsonl')
        assert len(ack)==len(heads)==9 and len(inspections)==inspect*9
        all_ack[name]=ack;all_heads[name]=heads
        current=read(wire/'hello.json');moving=[current];inspected=[];operations=[]
        assert current['operation']=='hello' and current['sequence']==current['physicsStep']==0
        assert current['isolated'] and current['paused'] and not current['connected'] and current['unity']=='6000.3.19f1'
        assert current['stateHash']==sha(current['stateJson']) and current['observationsHash']==sha(current['observationsJson'])
        assert current['bodyFingerprint']==sha(current['geometryJson']) and current['plantFingerprint']==current['lastAppliedEffortId']==''
        initial_before=None;initial_request=None
        requests=sorted(wire.glob('request-*.json'));replies=sorted(wire.glob('reply-*.json'))
        assert len(requests)==len(replies)==10+inspect*9
        for sequence,(request_file,reply_file) in enumerate(zip(requests,replies,strict=True),1):
            request=read(request_file);reply=read(reply_file);op=request['operation'];operations.append(op)
            assert request['sequence']==reply['sequence']==sequence and request['expectedStep']==current['physicsStep'] and reply['operation']==op
            assert request['configurationHash']==reply['configurationHash']==sha((folder/'input.json').read_text(encoding='utf-8'))
            assert request['bodyFingerprint']==reply['bodyFingerprint']==current['bodyFingerprint'] and request['stateHash']==current['stateHash']
            assert request['sessionId']==reply['sessionId']==configuration['sessionId']
            assert reply['isolated'] and reply['paused'] and not reply['connected'] and reply['unity']=='6000.3.19f1'
            assert reply['stateHash']==sha(reply['stateJson']) and reply['observationsHash']==sha(reply['observationsJson'])
            assert h.real(request['startTimeMs']['bits'])==current['physicsStep']*step_ms
            assert h.real(reply['physicalTimeMs']['bits'])==h.real(request['endTimeMs']['bits'])==reply['physicsStep']*step_ms
            assert reply['geometryJson']==current['geometryJson'] and reply['physicsStep']==current['physicsStep']+(op in ('step','prelude'))
            if op in ('step','prelude'):moving.append(reply)
            else:
                assert all(reply[k]==current[k] for k in ('stateJson','stateHash','observationsJson','observationsHash','applicationsJson','paths'))
                if op=='inspect':inspected.append(reply)
            if op=='prelude':
                assert current['physicsStep']==0 and not request['tensions'] and request['effortId']=='' and reply['applicationsJson']==''
                initial_before=current;initial_request=request
            elif op=='step':
                assert [t['muscleId'] for t in request['tensions']]==[d['muscleId'] for d in expected]
                assert all(h.real(t['axialNewtons']['bits'])==(40. if t['muscleId']==muscle else 0.) for t in request['tensions'])
                assert reply['lastAppliedEffortId']==request['effortId'] and request['effortId']!=current['lastAppliedEffortId']
                assert len(json.loads(reply['applicationsJson'])['points'])==76
            current=reply
        assert operations.count('prelude')==1 and operations.count('step')==7 and operations.count('bind')==1 and operations[-1]=='stop'
        assert ack==moving and inspections==inspected and current['sequence']==status['finalSequence']
        attachment=read(folder/'attachment.json');initial=json.loads(attachment['originalEvidenceJson'])
        assert initial['before']==initial_before and initial['request']==initial_request
        assert initial['after']==ack[1] and attachment['attachment']['initializationEvidenceSha256']==sha(attachment['originalEvidenceJson'])
        assert attachment['attachment']['neuralEpochMs']==1000+step_ms
        sample_ids=set()
        for step,(reply,frame) in enumerate(zip(ack,heads,strict=True)):
            assert frame['physicsStep']==reply['physicsStep']==step and frame['physicalMs']==step*step_ms and frame['neuralMs']==1000+step*step_ms
            assert frame['bodyStateSha256']==reply['stateHash'] and frame['originalObservationsJson']==reply['observationsJson'] and frame['observationsSha256']==reply['observationsHash']
            samples=json.loads(reply['observationsJson']);assert samples['physicsStep']==step and len(samples['contactsJson'])==15
            assert [s['sensorId'] for s in frame['samples']]==['centre','left','right']
            assert len({s['observation']['sampleId'] for s in frame['samples']})==1
            sample_id=frame['samples'][0]['observation']['sampleId'];assert sample_id not in sample_ids;sample_ids.add(sample_id)
            for i,sample in enumerate(frame['samples']):
                raw=sample['originalNativeJson'];native=json.loads(raw);observation=sample['observation'];receipt=sample['kinematics']
                assert raw==samples['skullJson'][i] and sample['nativeSha256']==sha(raw)
                for key in observation:
                    if key!='sampleId':assert observation[key]==native[key],(name,step,key)
                decoded={key[0].lower()+key[1:]:value for key,value in json.loads(receipt['rawObservationJson']).items()}
                for key,value in decoded.items():
                    if isinstance(value,dict):decoded[key]={k.lower():v for k,v in value.items()}
                assert decoded==observation and receipt['rawObservationSha256']==sha(receipt['rawObservationJson'])
                assert receipt['physicsStep']==step and receipt['physicalTimeMs']==frame['physicalMs'] and receipt['neuralTimeMs']==frame['neuralMs']
                if step==0:
                    assert all(receipt[k] is None for k in ('previousObservationSha256','accelerationFromStep','averageAccelerationWorldMetersPerSecondSquared','vestibularInput'))
                else:
                    prior=heads[step-1]['samples'][i];assert receipt['previousObservationSha256']==prior['kinematics']['rawObservationSha256'] and receipt['accelerationFromStep']==step-1
                    acceleration=[v/(step_ms/1000) for v in sub(vec(observation['pointVelocityWorldMetersPerSecond']),vec(prior['observation']['pointVelocityWorldMetersPerSecond']))]
                    for axis,(a,b) in enumerate(zip(vec(receipt['averageAccelerationWorldMetersPerSecondSquared']),acceleration,strict=True)):close(f'{name}/{step}/{i}/acceleration/{axis}',a,b)
                    input=receipt['vestibularInput'];assert input['physicsStep']==step and input['physicalSampleId']==observation['sampleId']
                    basis=[vec(observation[k]) for k in ('rightWorld','upWorld','forwardWorld')]
                    omega=vec(observation['angularVelocityWorldRadiansPerSecond']);g=sub(vec(observation['gravityWorldMetersPerSecondSquared']),acceleration)
                    for key,value in zip(('pitchVelocityRadiansPerSecond','yawVelocityRadiansPerSecond','rollVelocityRadiansPerSecond',
                        'gravitoInertialRightMetersPerSecondSquared','gravitoInertialDownMetersPerSecondSquared','gravitoInertialForwardMetersPerSecondSquared'),
                        [dot(omega,b) for b in basis]+[dot(g,basis[0]),-dot(g,basis[1]),dot(g,basis[2])],strict=True):close(f'{name}/{step}/{i}/{key}',input[key],value)
        summaries.append(dict(name=name,physicalSteps=8,endpoints=9,headReceipts=27,inspections=inspect*9))
    for base in ('quiet','hip','shoulder'):
        for a,b in zip(all_ack[base],all_ack[base+'-inspected'],strict=True):
            assert all(a[k]==b[k] for k in ('geometryJson','stateJson','paths','applicationsJson')),'Inspection changed physical evolution.'
            assert json.loads(a['observationsJson'])['skullJson']==json.loads(b['observationsJson'])['skullJson']
    original_ok=all(a['stateJson']==b['stateJson'] for a,b in zip(all_ack['quiet'],original,strict=True))
    assert len(checks)==6*8*3*9
    accurate=bool(checks) and all(c['passed'] for c in checks)
    write(package/'derived-observations.json',checks)
    write(package/'analysis.json',dict(completed=True,nativeHeadDeliveryChecksPassed=original_ok and accurate,cases=summaries,
        originalQuietEndpointsIdentical=original_ok,nonmovingInspectionPreserved=True,actualPhysicsSteps=48,physicalEndpoints=54,
        headReceipts=162,extraInspections=54,derivedChecks=len(checks),derivedChecksPassed=accurate,
        maximumDerivedAllowanceFraction=max(c['error']/c['allowance'] for c in checks),
        neuronalSourceExecuted=False,muscleCellExecuted=False,physiologicalFiringQualified=False,avatarBalanceQualified=False))
    if not original_ok or not accurate:raise RuntimeError('Actual head-input criteria failed; preserve original samples and bounds.')
    print('Six real full-body bridge cases, 54 endpoints and 162 original head receipts reconciled; no neuronal control or standing claim.',flush=True)

if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    p=argparse.ArgumentParser();p.add_argument('stage',choices=('prepare','preflight','runtime','compare'));p.add_argument('package',nargs='?',type=Path);a=p.parse_args()
    if a.stage=='prepare':prepare()
    else:
        assert a.package is not None
        {'preflight':preflight,'runtime':runtime,'compare':compare}[a.stage](a.package.resolve())

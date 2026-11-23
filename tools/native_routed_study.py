"""Isolated native routed-load qualification; all execution belongs to its window."""
import argparse
from datetime import datetime, timezone
import importlib.util
import json
import math
from pathlib import Path, PurePosixPath
import shutil
import struct
import subprocess
import sys
import uuid
from zipfile import ZipFile

ROOT = Path(__file__).resolve().parents[1]
BASE = 'caa07de'
CASES = ['quiet','left','right','both','both-cut','invalid-last','both-replay']
CHANGES = sorted('UnityWorld/Assets/NRE/Runtime/'+s for s in (
    'NativeRoutedMuscleBatch.cs','NativeRoutedMuscleBatch.cs.meta',
    'RoutedMuscleNativeQualification.cs','RoutedMuscleNativeQualification.cs.meta','WorldController.cs'))
LIMITS = dict(forceAbsoluteNewtons=2e-4,torqueAbsoluteNewtonMeters=2e-5,relativeAccumulatorError=2e-5,
              lengthMeters=2e-6,velocityMetersPerSecond=2e-6,generalizedLoadNewtonMeters=1e-4,powerWatts=1e-4)

def module(name, path):
    s=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m
h=module('routed_native_helpers',ROOT/'tools/native_avatar_tendon_study.py')
read,write,digest=h.read,h.write,h.digest
metadata=module('routed_native_assets',ROOT/'tools/unity_metadata.py')

def prepare():
    assert not h.git('status','--porcelain'),'Commit clean source first.'
    revision=h.git('rev-parse','HEAD');assert revision==h.git('rev-parse','origin/main'),'Push first.'
    changed=sorted(h.git('diff','--name-only',BASE,revision,'--','src','Structures','Protocol','Shared.Contracts','ControlProgram','tests','UnityWorld','tools/NativeMotorBridgeProbe').splitlines())
    assert changed==CHANGES,changed
    name='native-routed-path-'+datetime.now(timezone.utc).strftime('%Y%m%d')+'-'+uuid.uuid4().hex[:6]
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
    write(package/'unity-avatar-inputs.json',metadata.freeze_avatar_assets(ROOT/'UnityWorld',package/'source/UnityWorld'))
    write(package/'unity-metadata.json',metadata.validate(package/'source/UnityWorld'))
    protect=module('routed_native_preservation',ROOT/'tools/native_articulated_head_study.py').protected_copy
    for folder in ['routed-muscle-path-20261123-61820b-review','native-population-reanalysis-20261123-c6dcd3-review']:
        src=ROOT/'artifacts'/folder
        protect(src,package/folder,[e['Path'].replace('\\','/') for e in read(src/'backup-manifest.json')['Files']])
    protect(Path('D:/DNNE-build-work/named-muscle-native-20261123-998900'),package/'original',
            ['window-status.json','configuration.json','native/quiet/acknowledged.jsonl'])
    shutil.copyfile(package/'source/tools/backup-unity-run.ps1',package/'backup-run.ps1')
    python=Path(sys.executable);unity=Path('C:/Program Files/Unity/Hub/Editor/6000.3.19f1/Editor/Unity.exe')
    files=[dict(Path=p.relative_to(package).as_posix(),Sha256=digest(p)) for p in sorted(package.rglob('*')) if p.is_file()]
    write(package/'configuration.json',dict(Revision=revision,PackageFiles=files,BackupDestination=str(backup),NoLiveMonitoring=True,
        PythonExecutable=str(python),PythonExecutableSha256=digest(python),PythonRuntime=h.python_runtime(),
        UnityExecutable=str(unity),UnityExecutableSha256=digest(unity),UnityVersion='6000.3.19f1',
        UnityBuildWorkspace=str(package.with_name(name+'-unity-build')),Cases=CASES,Limits=LIMITS,
        SourceDeclaration=dict(baseRevision=h.git('rev-parse',BASE),changedFiles=changed,originalBiologicalKernelsUnchanged=True),
        AvatarBuildInputChecks=8,ReusedManagedCases=169,NativeRoutedStudy=True,PrescribedForces=True,
        LiveBrainAccessed=False,OldSavesLoaded=False,AnatomicalCalibrationQualified=False,AvatarStandingQualified=False))
    preflight(package);print(str(package),flush=True)

def preflight(package):
    cfg=read(package/'configuration.json')
    assert cfg['NoLiveMonitoring'] and cfg['NativeRoutedStudy'] and cfg['PrescribedForces']
    assert cfg['Cases']==CASES and cfg['Limits']==LIMITS and cfg['ReusedManagedCases']==169 and cfg['AvatarBuildInputChecks']==8
    assert cfg['SourceDeclaration']['changedFiles']==CHANGES and cfg['SourceDeclaration']['originalBiologicalKernelsUnchanged']
    assert not any(cfg[k] for k in ('LiveBrainAccessed','OldSavesLoaded','AnatomicalCalibrationQualified','AvatarStandingQualified'))
    for e in cfg['PackageFiles']:assert digest(package/e['Path'])==e['Sha256'],e['Path']
    for key in ['Python','Unity']:assert digest(cfg[key+'Executable'])==cfg[key+'ExecutableSha256']
    assert cfg['PythonRuntime']==h.python_runtime()
    assert metadata.validate(package/'source/UnityWorld')==read(package/'unity-metadata.json')
    assert metadata.avatar_assets(package/'source/UnityWorld')==read(package/'unity-avatar-inputs.json')
    review=read(package/'routed-muscle-path-20261123-61820b-review/review.json')
    assert review['operatorReportedCompletion'] and review['routedAssemblyChecksPassed'] and review['passedManagedTests']==169
    for script,target in [('prepare-unity-routed-path.py','RoutedMusclePathGeometry.cs'),('prepare-unity-cylinder-paths.py','CylinderMusclePathGeometry.cs'),('prepare-unity-patellar-path.py','PatellarPathGeometry.cs')]:
        assert module('parity_'+target,ROOT/'tools'/script).generate(ROOT)==(ROOT/'UnityWorld/Assets/NRE/Runtime'/target).read_text(encoding='utf-8-sig')
    assert read(package/'original/window-status.json')['Status']=='DIAGNOSTICS_COMPLETE'
    print('Reviewed geometry, original quiet capture, unchanged biological kernels and frozen build inputs verified; no model/test executed by preflight.',flush=True)

def real(bits):
    assert len(bits)==16
    v=struct.unpack('>d',bytes.fromhex(bits))[0];assert math.isfinite(v);return v
def vec(bits):return [real(b) for b in bits]
def f32(v):return struct.unpack('f',struct.pack('f',v))[0]
def add(a,b):return [x+y for x,y in zip(a,b,strict=True)]
def sub(a,b):return [x-y for x,y in zip(a,b,strict=True)]
def dot(a,b):return sum(x*y for x,y in zip(a,b,strict=True))
def cross(a,b):return [a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]]
def clean_state(s):return [{k:v for k,v in b.items() if k!='instance'} for b in s['bodies']]

def analyse(package):
    cfg=read(package/'configuration.json');assert cfg['Limits']==LIMITS
    checks=[];states={};summaries=[];point_count=torque_count=0
    def check(label,error,allowance,**where):
        assert math.isfinite(error) and allowance>0
        checks.append(dict(label=label,error=error,allowance=allowance,passed=error<=allowance,**where))
    for name in CASES:
        folder=package/'native'/name;report=read(folder/'routed-native-result.json')
        assert report['version']=='native-routed-muscle-qualification-v1' and report['condition']==name and report['unity']==cfg['UnityVersion']
        assert report['completed'] and not report.get('error') and report['isolated'] and report['paused'] and not report['connected']
        assert not any(report[k] for k in ['neuronalSourceExecuted','anatomicalCalibrationQualified','avatarStandingQualified'])
        assert report['steps']==8 and report['endpoints']==9 and real(report['stepSecondsBits'])==f32(.005)
        assert report['lateRejections']==(7 if name=='invalid-last' else 0) and report['replayRejections']==(7 if name=='both-replay' else 0)
        mechanics=json.loads(report['mechanicsJson']);assert mechanics['mode']=='no-joint-friction' and not mechanics['liveConfigurationChanged']
        fixtures={f['muscleId']:f for f in report['fixtures']};assert set(fixtures)=={'left-analytic-knee','right-analytic-knee'}
        ends=[read(folder/f'endpoint-{i:02}.json') for i in range(9)];states[name]=[]
        for i,e in enumerate(ends):
            assert e['step']==i;s=json.loads(e['stateJson']);assert s['step']==i and len(s['bodies'])==15
            assert len({b['id'] for b in s['bodies']})==len({b['instance'] for b in s['bodies']})==15
            for b in s['bodies']:
                for k,v in b.items():
                    if k not in ('id','instance'):vec(v)
            o=json.loads(e['observationsJson']);assert o['physicsStep']==i and len(o['contactsJson'])==15 and len(o['skullJson'])==3
            skull=[json.loads(x) for x in o['skullJson']]
            assert {x['sensorId'] for x in skull}=={'centre','left','right'} and all(x['physicsStep']==i and x['headBodyId']=='head' for x in skull)
            states[name].append(s)
        for i in range(7):
            row=read(folder/f'step-{i:02}.json');prepared=read(folder/f'prepared-{i:02}.json')
            assert row['index']==i and row['beforeStep']==i+1 and row['afterStep']==i+2
            assert row['beforeState']==ends[i+1]['stateJson'] and row['afterState']==ends[i+2]['stateJson']
            for key in ('index','beforeStep','beforeState','loadJson','paths','rejectedPaths','lateRejectedWithoutMutation'):assert row.get(key)==prepared.get(key),key
            assert row['lateRejectedWithoutMutation']==(name=='invalid-last') and row['replayRejectedWithoutMutation']==(name=='both-replay')
            if name=='invalid-last':
                assert len(row['rejectedPaths'])==2 and {x['muscleId'] for x in row['rejectedPaths']}==set(fixtures)
                assert all(real(x['tensionBits'])==40 for x in row['rejectedPaths'])
            load=json.loads(row['loadJson']);assert load['stateBefore']==row['beforeState'] and load['physicsStep']==i+1
            points,torques=load['points'],load['torques'];assert len(points)==10 and len(torques)==4
            assert {p['muscleId'] for p in points}==set(fixtures) and len(row['paths'])==2
            point_count+=len(points);torque_count+=len(torques)
            bodies={b['id']:b for b in states[name][i+1]['bodies']};power=0
            for p in points:
                assert p['bodyId'] in bodies and p['pathFingerprint']==fixtures[p['muscleId']]['pathFingerprint']
                assert len(p['appliedForceBits'])==len(p['appliedPositionBits'])==3
                assert vec(p['appliedForceBits'])==list(map(f32,vec(p['requestedForceBits'])))
                assert vec(p['appliedPositionBits'])==list(map(f32,vec(p['requestedPositionBits'])))
                b=bodies[p['bodyId']];v=add(vec(b['velocity']),cross(vec(b['angular']),sub(vec(p['appliedPositionBits']),vec(b['center']))))
                check('material velocity',max(map(abs,sub(v,vec(p['materialVelocityBits'])))),LIMITS['velocityMetersPerSecond'],case=name,step=i)
                power+=dot(vec(p['appliedForceBits']),vec(p['materialVelocityBits']))
            for t in torques:
                assert t['bodyId'] in bodies and t['pathFingerprint']==fixtures[t['muscleId']]['pathFingerprint']
                assert vec(t['appliedTorqueBits'])==list(map(f32,vec(t['requestedTorqueBits'])))
                assert t['angularVelocityBits']==bodies[t['bodyId']]['angular']
                power+=dot(vec(t['appliedTorqueBits']),vec(t['angularVelocityBits']))
            check('recorded native power',abs(power-real(load['nativeBoundaryPowerBits'])),1e-10,case=name,step=i)
            expected_power=0
            for p in row['paths']:
                f=fixtures[p['muscleId']];side=f['parent'].split('_')[0];scale=real(f['scaleBits']);q=real(p['qBits']);qd=real(p['relativeSpeedBits']);offset=real(f['offsetBits'])
                tension=40 if i%2==0 and name in (side,'both','both-replay') else 0
                assert real(p['tensionBits'])==tension and p['pathFingerprint']==f['pathFingerprint'] and len(p['points'])==5
                assert real(f['offsetBits'])==1.9 and abs(scale-.021)<1e-12
                expected_length=scale*(2*math.sqrt(5)+2*(q+offset-2*math.acos(2/3))+math.sqrt(1.25))
                check('analytic length',abs(real(p['lengthBits'])-expected_length),LIMITS['lengthMeters'],case=name,step=i,muscle=p['muscleId'])
                check('analytic length rate',abs(real(p['velocityBits'])-2*scale*qd),LIMITS['velocityMetersPerSecond'],case=name,step=i,muscle=p['muscleId'])
                ps=[x for x in points if x['muscleId']==p['muscleId']];ts=[x for x in torques if x['muscleId']==p['muscleId']]
                assert len(ps)==5 and len(ts)==2 and {t['bodyId'] for t in ts}=={f['parent'],f['child']}
                assert len({x['attachmentId'] for x in ps})==5 and {x['bodyId'] for x in ps}=={f['parent'],f['child']}
                net=[0.,0.,0.];moment=[0.,0.,0.];child_moment=[0.,0.,0.]
                for x in ps:
                    force=vec(x['appliedForceBits']);m=cross(sub(vec(x['appliedPositionBits']),vec(p['pivotBits'])),force)
                    net=add(net,force);moment=add(moment,m)
                    if x['bodyId']==f['child']:child_moment=add(child_moment,m)
                for t in ts:
                    moment=add(moment,vec(t['appliedTorqueBits']))
                    if t['bodyId']==f['child']:child_moment=add(child_moment,vec(t['appliedTorqueBits']))
                check('closed force',max(map(abs,net)),LIMITS['forceAbsoluteNewtons'],case=name,step=i)
                check('closed moment',max(map(abs,moment)),LIMITS['torqueAbsoluteNewtonMeters'],case=name,step=i)
                check('paired constraint moments',max(map(abs,add(vec(ts[0]['appliedTorqueBits']),vec(ts[1]['appliedTorqueBits'])))),1e-12,case=name,step=i)
                check('analytic knee load',abs(dot(child_moment,vec(p['axisBits']))+2*scale*tension),LIMITS['generalizedLoadNewtonMeters'],case=name,step=i)
                expected_power-=2*scale*tension*qd
            check('analytic power',abs(power-expected_power),LIMITS['powerWatts'],case=name,step=i)
            check('requested versus native power',abs(power-real(load['requestedPowerBits'])),LIMITS['powerWatts'],case=name,step=i)
            trace=json.loads(row['forceTraceJson']);obs=trace['observations']
            assert trace['observationOnly'] and trace['startStep']==i+1 and trace['physicsStepSecondsBits']==report['stepSecondsBits']
            assert [o['phase'] for o in obs]==['before-applications']+['after-point']*10+['after-constraint-torque']*4+['after-all-applications','after-physics']
            assert [o['applicationCount'] for o in obs]==list(range(15))+[14,14]
            for stage in obs:
                assert len(stage['bodies'])==15 and {b['id'] for b in stage['bodies']}==set(bodies)
                assert len(stage['reducedCounteractingForceBits'])==stage['nativeDofCount'];vec(stage['reducedCounteractingForceBits'])
                count=stage['applicationCount']
                for b in stage['bodies']:
                    force=[0.,0.,0.];torque=[0.,0.,0.]
                    if stage['phase']!='after-physics':
                        assert b['centerBits']==bodies[b['id']]['center']
                        force=vec(bodies[b['id']]['force']);torque=vec(bodies[b['id']]['torque'])
                        for p in points[:count]:
                            if p['bodyId']==b['id']:
                                f=vec(p['appliedForceBits']);force=add(force,f);torque=add(torque,cross(sub(vec(p['appliedPositionBits']),vec(b['centerBits'])),f))
                        for t in torques[:max(0,count-10)]:
                            if t['bodyId']==b['id']:torque=add(torque,vec(t['appliedTorqueBits']))
                    for label,expected,field,limit in [('force',force,'accumulatedForceBits','forceAbsoluteNewtons'),('torque',torque,'accumulatedTorqueBits','torqueAbsoluteNewtonMeters')]:
                        check(label,max(map(abs,sub(vec(b[field]),expected))),LIMITS[limit]+LIMITS['relativeAccumulatorError']*max(map(abs,expected)),case=name,step=i,phase=stage['phase'],body=b['id'])
        print('NATIVE ROUTED LOAD ACCOUNTING '+name,flush=True)
        summaries.append(dict(case=name,physicalSteps=8,lateRejections=report['lateRejections'],replayRejections=report['replayRejections']))
    old=[json.loads(line) for line in (package/'original/native/quiet/acknowledged.jsonl').read_text(encoding='utf-8-sig').splitlines() if line.strip()]
    assert len(old)==9
    mapping=dict(position='positionBits',rotation='rotationBits',velocity='velocityBits',angular='angularVelocityBits',jointPosition='jointPositionBits',jointVelocity='jointVelocityBits')
    original_equal=True
    for i,row in enumerate(old):
        prior={b['id']:b for b in json.loads(row['stateJson'])['links']}
        assert row['physicsStep']==i and len(prior)==15
        original_equal &= all(b[k]==prior[b['id']][v] for b in states['quiet'][i]['bodies'] for k,v in mapping.items())
    pairs={name:all(clean_state(a)==clean_state(b) for a,b in zip(states[name],states[control],strict=True)) for name,control in [('both-cut','quiet'),('invalid-last','quiet'),('both-replay','both')]}
    response={name:max(abs(a-b) for s,c in zip(states[name],states['quiet'],strict=True) for x,y in zip(s['bodies'],c['bodies'],strict=True) for a,b in zip(vec(x['jointPosition']),vec(y['jointPosition']),strict=True)) for name in ['left','right','both']}
    assert point_count==490 and torque_count==196 and len(checks)>24000
    passed=original_equal and all(pairs.values()) and all(c['passed'] for c in checks)
    write(package/'load-checks.json',checks)
    write(package/'analysis.json',dict(completed=True,nativeLoadCriteriaPassed=passed,cases=summaries,physicalSteps=56,pointForces=point_count,constraintMoments=torque_count,
        originalQuietRetained=original_equal,exactControlPairs=pairs,diagnosticJointResponsesRadians=response,checks=len(checks),failedChecks=sum(not c['passed'] for c in checks),
        maximumAllowanceFraction=max(c['error']/c['allowance'] for c in checks),independentCoupledTrajectoryQualified=False,
        anatomicalCalibrationQualified=False,neuronalAuthorityQualified=False,avatarStandingQualified=False))
    print(f'Seven native cases, 56 physical steps, {point_count} point forces and {torque_count} constraint moments. Load criteria: {passed}. Movement remains diagnostic.',flush=True)
    if not passed:raise RuntimeError('Native routed criteria failed; preserve all observations and original bounds.')

if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    p=argparse.ArgumentParser();p.add_argument('stage',choices=['prepare','preflight','analyse']);p.add_argument('package',nargs='?',type=Path);a=p.parse_args()
    if a.stage=='prepare':prepare()
    else:globals()[a.stage](a.package.resolve())

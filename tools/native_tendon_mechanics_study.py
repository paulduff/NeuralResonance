"""Isolated one-factor full-body mechanical diagnosis; never changes live settings."""
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

ROOT = Path(__file__).resolve().parents[1]
BASE = '0c36f50'
ORIGINAL = Path('D:/DNNE-build-work/tendon-force-20261123-e764e3')
spec = importlib.util.spec_from_file_location('tendon_force_helpers', ROOT/'tools/native_avatar_tendon_study.py')
h = importlib.util.module_from_spec(spec); spec.loader.exec_module(h)
read, write, digest = h.read, h.write, h.digest


MODES = ('original','no-joint-friction','no-body-damping','joint-speed-floor','free-active-axes','no-gravity')
PROFILES = ('leftHip','leftKnee','leftElbow','leftShoulderAbduction')
ORIGINAL_NAMES = ('quiet',) + tuple(p+'-'+side for p in PROFILES for side in ('flexor','extensor'))


def cases(full_friction=False):
    profiles = tuple(h.re.findall(r'new MuscleProfile\("([^"]+)"', (ROOT/'UnityWorld/Assets/NRE/Runtime/MuscleCore.cs').read_text())) if full_friction else PROFILES
    assert len(profiles)==(19 if full_friction else 4) and len(set(profiles))==len(profiles)
    names=('quiet',)+tuple(p+'-'+side for p in profiles for side in ('flexor','extensor'))
    return [dict(name=mode+'__'+name, original=name+'-observed', mode=mode,
                 condition='quiet' if name=='quiet' else 'pulse',
                 muscle='' if name=='quiet' else name.rsplit('-',1)[0]+'/'+name.rsplit('-',1)[1], observe=True)
            for mode in (MODES[:2] if full_friction else MODES) for name in names]


def prepare(full_friction=False):
    assert not h.git('status','--porcelain'), 'Commit clean source first.'
    revision = h.git('rev-parse','HEAD'); assert revision == h.git('rev-parse','origin/main')
    assert not h.git('diff','--name-only',BASE,revision,'--','src','Structures','tests','Shared.Contracts','tools/NativeMotorBridgeProbe','UnityWorld','tools/native_avatar_tendon_study.py')
    old = read(ORIGINAL/'configuration.json'); old_analysis = read(ORIGINAL/'analysis.json')
    assert old_analysis['mechanicalCriteriaPassed'] and old['Revision'] == '740fa633dcc92a1c5c537c44ebd27e08c8ba20b2'
    manifest = read(ORIGINAL/'backup-manifest.json'); protected = Path(manifest['Destination'])
    assert digest(ORIGINAL/'backup-manifest.json') == digest(protected/'backup-manifest.json')
    entries = {e['Path'].replace('\\','/'):e for e in manifest['Files']}
    planned=cases(full_friction)
    selected = [c for c in old['Cases'] if c['name'] in {c['original'] for c in planned}]; assert len(selected) == (39 if full_friction else 9)
    name = ('tendon-friction-' if full_friction else 'tendon-mechanics-')+datetime.now(timezone.utc).strftime('%Y%m%d')+'-'+uuid.uuid4().hex[:6]
    package = Path('D:/DNNE-build-work')/name; backup = Path('D:/DNNE-desktop-runs/UnityWorld')/name
    assert not package.exists() and not backup.exists(); package.mkdir()
    subprocess.run(['git','archive','--format=zip','--output='+str(package/'source.zip'),revision],cwd=ROOT,check=True)
    with ZipFile(package/'source.zip') as archive:
        for entry in archive.infolist():
            p = PurePosixPath(entry.filename)
            assert not p.is_absolute() and '..' not in p.parts and ':' not in entry.filename and '\\' not in entry.filename
            assert ((entry.external_attr >> 16) & 0o170000) != 0o120000
            target = package/'source'/p
            if entry.is_dir(): target.mkdir(parents=True,exist_ok=True)
            else:
                target.parent.mkdir(parents=True,exist_ok=True)
                with target.open('xb') as stream: stream.write(archive.read(entry))
    provenance = read(ROOT/'UnityWorld/Assets/Avatar/provenance.json')
    for e in provenance['files']:
        source = ROOT/'UnityWorld/Assets/Avatar'/e['file']
        assert source.name in ('MikeAlger.fbx','MikeAlger_Texture.jpeg') and digest(source) == e['sha256'].upper()
        shutil.copyfile(source,package/'source/UnityWorld/Assets/Avatar'/source.name)
    spec=importlib.util.spec_from_file_location('force_unity_metadata',ROOT/'tools/unity_metadata.py')
    metadata=importlib.util.module_from_spec(spec);spec.loader.exec_module(metadata)
    write(package/'unity-metadata.json',metadata.validate(package/'source/UnityWorld'))
    saved = ['configuration.json','analysis.json','window-status.json']
    saved += [f'native/{c["name"]}/{f}' for c in selected for f in ('result.json','endpoints.jsonl')]
    for name in saved:
        entry = entries[name]
        assert digest(ORIGINAL/name) == digest(protected/name) == entry['Sha256']
        target = package/'original'/name; target.parent.mkdir(parents=True,exist_ok=True)
        shutil.copyfile(ORIGINAL/name,target)
    shutil.copyfile(ORIGINAL/'backup-manifest.json',package/'original-manifest.json')
    if full_friction:
        review=ROOT/'artifacts/tendon-mechanics-20261123-9d73a7-review'
        reviewed=read(review/'review.json'); proof=read(review/'backup-manifest.json'); copy=Path(proof['Destination'])
        assert reviewed['declaredInterventionsVerified'] and reviewed['originalEndpointsIdentical'] and reviewed['accumulatorChecks']==61920
        assert digest(review/'backup-manifest.json')==digest(copy/'backup-manifest.json')
        target=package/'prior-mechanical-review';target.mkdir()
        for entry in proof['Files']:
            relative=entry['Path'].replace('\\','/');assert '/' not in relative
            assert digest(review/relative)==digest(copy/relative)==entry['Sha256']
            shutil.copyfile(review/relative,target/relative)
        shutil.copyfile(review/'backup-manifest.json',target/'backup-manifest.json')
    shutil.copyfile(package/'source/tools/backup-unity-run.ps1',package/'backup-run.ps1')
    files = [dict(Path=p.relative_to(package).as_posix(),Sha256=digest(p)) for p in sorted(package.rglob('*')) if p.is_file()]
    unity = Path(old['UnityExecutable']); dotnet = Path(shutil.which('dotnet')); python = Path(sys.executable)
    write(package/'configuration.json',dict(Revision=revision,BaselineRevision=h.git('rev-parse',BASE),PackageFiles=files,
        BackupDestination=str(backup),UnityBuildWorkspace=str(package.parent/(package.name+'-unity')),
        UnityExecutable=str(unity),UnityExecutableSha256=digest(unity),DotnetExecutable=str(dotnet),DotnetExecutableSha256=digest(dotnet),
        PythonExecutable=str(python),PythonExecutableSha256=digest(python),PythonRuntime=h.python_runtime(),
        NoLiveMonitoring=True,IsolatedFullAvatarTendons=True,NativeMechanicsDiagnosticStudy=True, FullFrictionMatrix=full_friction,
        ExpectedTestCases=112,ExpectedTestMethods=h.inventory(),TestFilter='|'.join('FullyQualifiedName~'+c for c in h.COUNTS),
        ExpectedNativeCases=len(planned),ExpectedNativeSteps=len(planned)*8,Cases=planned,SyntheticAttachments=True,
        NeuronalSourceExecuted=False,AnatomicalCalibrationQualified=False,AvatarBalanceQualified=False,
        Limits=old['Limits'], MaximumLimitViolationRadians=0.05000000074505806 if full_friction else None,
        OriginalManifestSha256=digest(ORIGINAL/'backup-manifest.json')))
    preflight(package)
    print(json.dumps(dict(Package=str(package),Backup=str(backup),Revision=revision,
        Launcher=str(package/'source/tools/start-avatar-tendons-window.ps1')),indent=2),flush=True)


def preflight(package):
    c=read(package/'configuration.json')
    assert c['NativeMechanicsDiagnosticStudy'] and c['NoLiveMonitoring'] and c['IsolatedFullAvatarTendons'] and c['SyntheticAttachments']
    full=c.get('FullFrictionMatrix',False); planned=cases(full)
    assert c['ExpectedTestCases']==112 and c['ExpectedNativeCases']==len(planned)==len(c['Cases']) and c['ExpectedNativeSteps']==len(planned)*8 and c['Cases']==planned
    if full: assert c['MaximumLimitViolationRadians']==0.05000000074505806
    assert not any(c[k] for k in ('NeuronalSourceExecuted','AnatomicalCalibrationQualified','AvatarBalanceQualified'))
    for entry in c['PackageFiles']: assert digest(package/entry['Path'])==entry['Sha256'],entry['Path']
    for name in ('Unity','Dotnet','Python'): assert digest(c[name+'Executable'])==c[name+'ExecutableSha256'],name
    assert c['PythonRuntime']==h.python_runtime() and c['ExpectedTestMethods']==h.inventory()
    assert digest(package/'original-manifest.json')==c['OriginalManifestSha256']
    print('Frozen one-factor variants, original captures and dependency identities verified; no model/test executed by preflight.',flush=True)


def records(path): return [json.loads(line) for line in path.read_text(encoding='utf-8-sig').splitlines()]


def bits(value): return h.struct.pack('>d',value).hex().upper()


def settings(evidence, mode):
    import copy
    assert evidence['version']=='isolated-tendon-mechanical-interventions-v1' and evidence['mode']==mode
    assert not evidence['liveConfigurationChanged'] and not evidence['physiologicalCalibrationQualified']
    expected=copy.deepcopy(evidence['original'])
    assert len(expected['bodies'])==len({b['id'] for b in expected['bodies']})==15
    changed=[]
    for b in expected['bodies']:
        before=copy.deepcopy(b)
        if mode=='no-joint-friction': b['friction']=bits(0.)
        if mode=='no-body-damping': b['linearDamping']=b['angularDamping']=bits(0.)
        if mode=='joint-speed-floor' and b['activeCount']>0: b['maxJointVelocity']=bits(max(h.real(b['maxJointVelocity']),1000.))
        if mode=='free-active-axes':
            if b['activeCount']>0: b['locks'][0]='FreeMotion'
            if b['activeCount']==2: b['locks'][b['secondPhysicalAxis']]='FreeMotion'
        if mode=='no-gravity': b['gravity']=False
        changed.extend(b['id']+'.'+key for key in b if b[key]!=before[key])
    assert expected==evidence['intervention'], ('Unexpected intervention',mode)
    # The ordinary direct-tendon owner then turns off exactly the existing
    # active spring/damper drives, just as it did in the original capture.
    for b in expected['bodies']:
        axes=[] if b['activeCount']==0 else [0] if b['activeCount']==1 else [0,b['secondPhysicalAxis']]
        for axis in axes:
            for key in ('stiffness','damping','forceLimit'): b['drives'][axis][key]=bits(0.)
    assert expected==evidence['owned'], ('Unexpected owner change',mode)
    return changed


def force_checks(case, endpoint, rows, limits):
    trace=json.loads(endpoint['nativeForcesJson']); points=json.loads(endpoint['applicationsJson'])['points']
    assert trace['observationOnly'] and trace['startStep']==endpoint['step']-1 and len(points)==76
    nonzero=[i+1 for i,p in enumerate(points) if any(h.vec(p['appliedBits']))]
    stages=trace['observations']
    assert [s['phase'] for s in stages]==['before-applications']+['after-nonzero-application']*len(nonzero)+['after-all-applications','after-physics']
    assert [s['applicationCount'] for s in stages]==[0]+nonzero+[76,76]
    before={b['id']:b for b in stages[0]['bodies']}; assert len(before)==15
    for stage in stages:
        assert len(stage['bodies'])==15 and {b['id'] for b in stage['bodies']}==before.keys()
        assert len(stage['reducedCounteractingForceBits'])==stage['nativeDofCount']
        list(map(h.real,stage['reducedCounteractingForceBits']))
        for b in stage['bodies']:
            baseline=before[b['id']]; force=[0.,0.,0.]; torque=[0.,0.,0.]
            assert 0<=b['dofStart']<=b['dofStart']+b['dofCount']<=stage['nativeDofCount']
            if stage['phase']!='after-physics':
                for point in points[:stage['applicationCount']]:
                    if point['bodyId']!=b['id']: continue
                    f=h.vec(point['appliedBits']); t=h.cross(h.sub(h.vec(point['pointBits']),h.vec(baseline['centerBits'])),f)
                    force=[a+v for a,v in zip(force,f,strict=True)]; torque=[a+v for a,v in zip(torque,t,strict=True)]
            for kind,expected,field,bound in [('force',force,'accumulatedForceBits',limits['forceAbsoluteNewtons']),
                                            ('torque',torque,'accumulatedTorqueBits',limits['torqueAbsoluteNewtonMeters'])]:
                actual=h.vec(b[field]); origin=h.vec(baseline[field]) if stage['phase'] not in ('before-applications','after-physics') else (0.,0.,0.)
                error=max(abs(a-o-e) for a,o,e in zip(actual,origin,expected,strict=True))
                allowance=bound+limits['relativeAccumulatorError']*max(map(abs,expected))
                rows.append(dict(case=case['name'],step=endpoint['step'],phase=stage['phase'],body=b['id'],kind=kind,
                    error=error,allowance=allowance,passed=error<=allowance,actual=actual,baseline=origin,expectedIncrement=expected))


def analyse(package):
    cfg=read(package/'configuration.json'); full=cfg.get('FullFrictionMatrix',False); assert cfg['Cases']==cases(full)
    modes=MODES[:2] if full else MODES
    original_cfg=read(package/'original/configuration.json'); assert cfg['Limits']==original_cfg['Limits']
    summaries=[]; rows=[]; limit_rows=[]; total=0; baseline_settings=None
    quiet={mode:records(package/'native'/(mode+'__quiet')/'endpoints.jsonl') for mode in modes}
    for case in cfg['Cases']:
        folder=package/'native'/case['name']; report=read(folder/'result.json'); values=records(folder/'endpoints.jsonl')
        prior_report=read(package/'original/native'/case['original']/'result.json')
        old=records(package/'original/native'/case['original']/'endpoints.jsonl')
        assert report['completed'] and report['passed'] and not report['error'] and report['finalStep']==8
        assert report['nativeForceObservationEnabled'] and report['condition']==case['condition'] and report['muscle']==case['muscle']
        assert report['unity']=='6000.3.19f1' and report['links']==15 and report['paths']==38 and len(values)==len(old)==9
        assert not any(report[k] for k in ('neuronalSourceExecuted','anatomicalCalibrationQualified','avatarBalanceQualified'))
        assert report['fixtures']==prior_report['fixtures'] and report['stepMsBits']==prior_report['stepMsBits']
        configuration=json.loads(report['mechanicsDiagnosticJson']); changed=settings(configuration,case['mode'])
        geometry=json.loads(report['geometryJson'])
        if full:
            original_geometry=json.loads(prior_report['geometryJson'])
            assert len(geometry['links'])==15 and sum(len(b['activeNativeIndices']) for b in geometry['links'])==19
            assert [(b['id'],b['activeNativeIndices'],b['activeDrives']) for b in geometry['links']]==[(b['id'],b['activeNativeIndices'],b['activeDrives']) for b in original_geometry['links']]
        if baseline_settings is None: baseline_settings=configuration['original']
        assert configuration['original']==baseline_settings, ('Different initial body settings',case['name'])
        # No initial pose, velocity, path or time change is permitted by an intervention.
        keys=('stateJson','stateHash','applicationsJson','paths','leftFootForce','rightFootForce')
        assert all(values[0][k]==old[0][k] for k in keys), ('Initial state changed',case['name'])
        identical=None if case['mode']!='original' else True
        for n,endpoint in enumerate(values):
            assert endpoint['step']==n and endpoint['physicalTimeMsBits']==old[n]['physicalTimeMsBits']
            assert h.hashlib.sha256(endpoint['stateJson'].encode()).hexdigest().upper()==endpoint['stateHash']
            assert len(endpoint['paths'])==38
            if full:
                physical={b['id']:b for b in json.loads(endpoint['stateJson'])['links']}
                f32=lambda x:h.struct.unpack('<f',h.struct.pack('<f',x))[0]
                for link in geometry['links']:
                    for axis,(index,drive) in enumerate(zip(link['activeNativeIndices'],link['activeDrives'],strict=True)):
                        lower=f32(h.real(drive['lowerBits'])*f32(math.pi/180));upper=f32(h.real(drive['upperBits'])*f32(math.pi/180))
                        angle=h.real(physical[link['id']]['jointPositionBits'][index]);violation=max(0.,f32(lower-angle),f32(angle-upper))
                        limit_rows.append(dict(case=case['name'],step=n,joint=link['id'],axis=axis,nativeIndex=index,
                            angleRadians=angle,lowerRadians=lower,upperRadians=upper,violationRadians=violation,
                            passed=violation<cfg['MaximumLimitViolationRadians']))
            if case['mode']=='original': identical &= all(endpoint[k]==old[n][k] for k in keys)
            if not n:
                assert not endpoint['nativeForcesJson']; continue
            trace=json.loads(endpoint['nativeForcesJson'])
            assert h.real(trace['physicsStepSecondsBits'])*1000==h.real(report['stepMsBits'])
            force_checks(case,endpoint,rows,cfg['Limits'])
            # Check the prescribed tension and line of action independently of the queue.
            applications=json.loads(endpoint['applicationsJson'])['points']
            for p in values[n-1]['paths']:
                assert len(p['points'])==2
                a,b=p['points']; direction=h.sub(h.vec(b['positionBits']),h.vec(a['positionBits']))
                length=math.sqrt(h.dot(direction,direction)); assert length>0
                tension=40. if p['muscleId']==case['muscle'] else 0.
                for point,sign in ((a,1.),(b,-1.)):
                    matches=[v for v in applications if v['muscleId']==p['muscleId'] and v['attachmentId']==point['attachmentId']]
                    assert len(matches)==1
                    applied=matches[0]; assert applied['bodyId']==point['bodyId']
                    expected=tuple(sign*tension*x/length for x in direction)
                    assert max(abs(x-y) for x,y in zip(h.vec(applied['appliedBits']),expected,strict=True))<=2e-5
                    assert h.vec(applied['pointBits'])==h.vec(point['positionBits'])
        response=None
        if case['condition']=='pulse':
            fixture=next(f for f in report['fixtures'] if f['muscleId']==case['muscle'])
            link=next(b for b in geometry['links'] if b['id']==fixture['child'])
            index=link['activeNativeIndices'][fixture['axis']]; angles=[];speeds=[]
            for endpoint,control in zip(values,quiet[case['mode']],strict=True):
                actual=next(b for b in json.loads(endpoint['stateJson'])['links'] if b['id']==fixture['child'])
                resting=next(b for b in json.loads(control['stateJson'])['links'] if b['id']==fixture['child'])
                angles.append(h.real(actual['jointPositionBits'][index])-h.real(resting['jointPositionBits'][index]))
                speeds.append(h.real(actual['jointVelocityBits'][index])-h.real(resting['jointVelocityBits'][index]))
            response=dict(joint=fixture['child'],axis=fixture['axis'],nativeIndex=index,anglesRadians=angles,speedsRadiansPerSecond=speeds,
                maximumAbsoluteAngleDifferenceRadians=max(map(abs,angles)),maximumAbsoluteSpeedDifferenceRadiansPerSecond=max(map(abs,speeds)))
        summaries.append(dict(name=case['name'],mode=case['mode'],muscle=case['muscle'],steps=8,changedSettings=changed,
            originalEndpointsIdentical=identical,jointResponse=response,practicalControlQualified=False))
        total+=8
    original_ok=all(s['originalEndpointsIdentical'] for s in summaries if s['mode']=='original')
    force_ok=bool(rows) and all(r['passed'] for r in rows)
    limits_ok=not full or bool(limit_rows) and all(r['passed'] for r in limit_rows)
    assert total==cfg['ExpectedNativeSteps'] and len(summaries)==cfg['ExpectedNativeCases']
    if full: assert len(limit_rows)==cfg['ExpectedNativeCases']*9*19
    write(package/'force-observations.json',rows)
    if full: write(package/'joint-limit-observations.json',limit_rows)
    write(package/'analysis.json',dict(completed=True,mechanicalCriteriaPassed=original_ok and force_ok and limits_ok,
        actualPhysicsSteps=total,cases=summaries,originalEndpointsIdentical=original_ok,settingsInterventionsVerified=True,
        initialStatesPreserved=True,accumulatedForceAndTorquePassed=force_ok,accumulatorChecks=len(rows),
        maximumAllowanceFraction=max(r['error']/r['allowance'] for r in rows),
        fullFrictionMatrix=full,existingJointLimitGuardPassed=limits_ok if full else None,
        jointLimitObservations=len(limit_rows),maximumJointLimitViolationRadians=max((r['violationRadians'] for r in limit_rows),default=None),
        usefulAllAxisControlQualified=False,neuronalSourceExecuted=False,physiologyQualified=False,avatarBalanceQualified=False))
    print(f'{len(summaries)} native cases / {total} steps: original replay {original_ok}; one-factor settings verified; force accounting {force_ok}.',flush=True)
    for mode in modes:
        responses=[s['jointResponse']['maximumAbsoluteAngleDifferenceRadians'] for s in summaries if s['mode']==mode and s['jointResponse']]
        print(mode,'largest response relative to its own quiet:',max(responses),'rad; movement is diagnostic, not a qualification.',flush=True)
    if full: print('Existing 0.05-radian mechanical limit guard:',limits_ok,'; this is not anatomical accuracy.',flush=True)
    if not original_ok or not force_ok or not limits_ok: raise RuntimeError('Mechanical diagnostic accounting failed; preserve original settings, limits and all traces.')


if __name__=='__main__':
    if not __debug__: raise RuntimeError('Assertions must remain enabled.')
    p=argparse.ArgumentParser();p.add_argument('stage',choices=('prepare','preflight','check-tests','analyse'));p.add_argument('package',nargs='?',type=Path);p.add_argument('--full-friction',action='store_true');a=p.parse_args()
    if a.stage=='prepare': prepare(a.full_friction)
    else:
        assert a.package is not None and not a.full_friction, 'Frozen configuration chooses the native matrix.'
        {'preflight':preflight,'check-tests':h.check_tests,'analyse':analyse}[a.stage](a.package.resolve())

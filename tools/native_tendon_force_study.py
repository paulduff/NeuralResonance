"""Paired native force observations, preserving the completed original capture."""
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
BASE = '7231701'
ORIGINAL = Path('D:/DNNE-build-work/avatar-tendons-20261123-bb320d')
spec = importlib.util.spec_from_file_location('tendon_force_helpers', ROOT/'tools/native_avatar_tendon_study.py')
h = importlib.util.module_from_spec(spec); spec.loader.exec_module(h)
read, write, digest = h.read, h.write, h.digest


def prepare():
    assert not h.git('status','--porcelain'), 'Commit clean source first.'
    revision = h.git('rev-parse','HEAD'); assert revision == h.git('rev-parse','origin/main')
    assert not h.git('diff','--name-only',BASE,revision,'--','src','Structures','tests','Shared.Contracts','tools/NativeMotorBridgeProbe')
    old = read(ORIGINAL/'configuration.json'); old_analysis = read(ORIGINAL/'analysis.json')
    assert old_analysis['mechanicalCriteriaPassed'] and old['Revision'] == '218054afac3165edbb1cac7fdd0a98629d786f95'
    manifest = read(ORIGINAL/'backup-manifest.json'); protected = Path(manifest['Destination'])
    assert digest(ORIGINAL/'backup-manifest.json') == digest(protected/'backup-manifest.json')
    entries = {e['Path'].replace('\\','/'):e for e in manifest['Files']}
    selected = [c for c in old['Cases'] if c['condition'] in ('quiet','pulse')]; assert len(selected) == 39
    name = 'tendon-force-'+datetime.now(timezone.utc).strftime('%Y%m%d')+'-'+uuid.uuid4().hex[:6]
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
    shutil.copyfile(package/'source/tools/backup-unity-run.ps1',package/'backup-run.ps1')
    cases = [dict(name=c['name']+('-observed' if observe else '-plain'), original=c['name'], condition=c['condition'],
                  muscle=c['muscle'], observe=observe) for c in selected for observe in (False,True)]
    files = [dict(Path=p.relative_to(package).as_posix(),Sha256=digest(p)) for p in sorted(package.rglob('*')) if p.is_file()]
    unity = Path(old['UnityExecutable']); dotnet = Path(shutil.which('dotnet')); python = Path(sys.executable)
    write(package/'configuration.json',dict(Revision=revision,BaselineRevision=h.git('rev-parse',BASE),PackageFiles=files,
        BackupDestination=str(backup),UnityBuildWorkspace=str(package.parent/(package.name+'-unity')),
        UnityExecutable=str(unity),UnityExecutableSha256=digest(unity),DotnetExecutable=str(dotnet),DotnetExecutableSha256=digest(dotnet),
        PythonExecutable=str(python),PythonExecutableSha256=digest(python),PythonRuntime=h.python_runtime(),
        NoLiveMonitoring=True,IsolatedFullAvatarTendons=True,NativeForceObservationStudy=True,
        ExpectedTestCases=112,ExpectedTestMethods=h.inventory(),TestFilter='|'.join('FullyQualifiedName~'+c for c in h.COUNTS),
        ExpectedNativeCases=78,ExpectedNativeSteps=624,Cases=cases,SyntheticAttachments=True,
        NeuronalSourceExecuted=False,AnatomicalCalibrationQualified=False,AvatarBalanceQualified=False,
        Limits=dict(forceAbsoluteNewtons=2e-4,torqueAbsoluteNewtonMeters=2e-5,relativeAccumulatorError=2e-5),
        OriginalManifestSha256=digest(ORIGINAL/'backup-manifest.json')))
    preflight(package)
    print(json.dumps(dict(Package=str(package),Backup=str(backup),Revision=revision,
        Launcher=str(package/'source/tools/start-avatar-tendons-window.ps1')),indent=2),flush=True)


def preflight(package):
    c=read(package/'configuration.json')
    assert c['NativeForceObservationStudy'] and c['NoLiveMonitoring'] and c['IsolatedFullAvatarTendons'] and c['SyntheticAttachments']
    assert c['ExpectedTestCases']==112 and c['ExpectedNativeCases']==len(c['Cases'])==78 and c['ExpectedNativeSteps']==624
    assert not any(c[k] for k in ('NeuronalSourceExecuted','AnatomicalCalibrationQualified','AvatarBalanceQualified'))
    for entry in c['PackageFiles']: assert digest(package/entry['Path'])==entry['Sha256'],entry['Path']
    for name in ('Unity','Dotnet','Python'): assert digest(c[name+'Executable'])==c[name+'ExecutableSha256'],name
    assert c['PythonRuntime']==h.python_runtime() and c['ExpectedTestMethods']==h.inventory()
    assert digest(package/'original-manifest.json')==c['OriginalManifestSha256']
    print('Frozen body, completed original endpoints and dependency identities verified; no model/test executed by preflight.',flush=True)


def records(path): return [json.loads(line) for line in path.read_text(encoding='utf-8-sig').splitlines()]


def analyse(package):
    cfg=read(package/'configuration.json'); summaries=[]; rows=[]; total=0
    quiet=records(package/'native/quiet-plain/endpoints.jsonl')
    for case in cfg['Cases']:
        folder=package/'native'/case['name']; saved=read(folder/'result.json'); values=records(folder/'endpoints.jsonl')
        old=records(package/'original/native'/case['original']/'endpoints.jsonl')
        plain=values if not case['observe'] else records(package/'native'/(case['original']+'-plain')/'endpoints.jsonl')
        assert saved['completed'] and saved['passed'] and not saved['error'] and saved['finalStep']==8
        assert saved['nativeForceObservationEnabled']==case['observe'] and saved['condition']==case['condition'] and saved['muscle']==case['muscle']
        assert saved['links']==15 and saved['paths']==38 and len(values)==len(old)==len(plain)==9
        assert not any(saved[k] for k in ('neuronalSourceExecuted','anatomicalCalibrationQualified','avatarBalanceQualified'))
        original_equal=True; paired_equal=True
        for n,(current,prior,control) in enumerate(zip(values,old,plain,strict=True)):
            assert current['step']==n and current['physicalTimeMsBits']==prior['physicalTimeMsBits']
            assert h.hashlib.sha256(current['stateJson'].encode()).hexdigest().upper()==current['stateHash']
            keys=('stateJson','stateHash','applicationsJson','paths','leftFootForce','rightFootForce')
            original_equal &= all(current[k]==prior[k] for k in keys)
            paired_equal &= all(current[k]==control[k] for k in keys)
            if not case['observe'] or not n:
                assert not current.get('nativeForcesJson'); continue
            trace=json.loads(current['nativeForcesJson']); assert trace['observationOnly'] and trace['startStep']==n-1
            assert h.real(trace['physicsStepSecondsBits'])*1000==h.real(saved['stepMsBits'])
            points=json.loads(current['applicationsJson'])['points']; assert len(points)==76
            nonzero=[i+1 for i,p in enumerate(points) if any(h.vec(p['appliedBits']))]
            obs=trace['observations']
            assert [o['phase'] for o in obs]==['before-applications']+['after-nonzero-application']*len(nonzero)+['after-all-applications','after-physics']
            assert [o['applicationCount'] for o in obs]==[0]+nonzero+[76,76]
            before={b['id']:b for b in obs[0]['bodies']}; assert len(before)==15
            for stage in obs:
                assert len(stage['bodies'])==15 and {b['id'] for b in stage['bodies']}==before.keys()
                assert len(stage['reducedCounteractingForceBits'])==stage['nativeDofCount']
                list(map(h.real,stage['reducedCounteractingForceBits']))
                for b in stage['bodies']:
                    assert 0<=b['dofStart']<=b['dofStart']+b['dofCount']<=stage['nativeDofCount']
                    for key in ('centerBits','accumulatedForceBits','accumulatedTorqueBits','jointPositionBits','jointVelocityBits'):
                        list(map(h.real,b[key]))
            for stage in obs:
                for b in stage['bodies']:
                    base=before[b['id']]; force=[0.,0.,0.]; torque=[0.,0.,0.]
                    if stage['phase']!='after-physics':
                        for p in points[:stage['applicationCount']]:
                            if p['bodyId']!=b['id']: continue
                            f=h.vec(p['appliedBits']); t=h.cross(h.sub(h.vec(p['pointBits']),h.vec(base['centerBits'])),f)
                            force=[a+v for a,v in zip(force,f,strict=True)]; torque=[a+v for a,v in zip(torque,t,strict=True)]
                    # Accumulators reset after physics; no prior load is carried into that expectation.
                    for kind,expected,field,bound in [('force',force,'accumulatedForceBits',cfg['Limits']['forceAbsoluteNewtons']),
                                                      ('torque',torque,'accumulatedTorqueBits',cfg['Limits']['torqueAbsoluteNewtonMeters'])]:
                        actual=h.vec(b[field]); baseline=h.vec(base[field]) if stage['phase'] not in ('before-applications','after-physics') else (0.,0.,0.)
                        error=max(abs(a-v-e) for a,v,e in zip(actual,baseline,expected,strict=True))
                        allowance=bound+cfg['Limits']['relativeAccumulatorError']*max(map(abs,expected))
                        rows.append(dict(case=case['name'],step=n,phase=stage['phase'],body=b['id'],kind=kind,error=error,
                            allowance=allowance,passed=error<=allowance,actual=actual,baseline=baseline,expectedIncrement=expected))
        total+=8
        joint_response=None
        if case['condition']=='pulse':
            fixture=next(f for f in saved['fixtures'] if f['muscleId']==case['muscle'])
            geometry=json.loads(saved['geometryJson'])
            link=next(l for l in geometry['links'] if l['id']==fixture['child'])
            index=link['activeNativeIndices'][fixture['axis']]
            angles=[];speeds=[]
            for endpoint,control in zip(values,quiet,strict=True):
                actual=next(b for b in json.loads(endpoint['stateJson'])['links'] if b['id']==fixture['child'])
                resting=next(b for b in json.loads(control['stateJson'])['links'] if b['id']==fixture['child'])
                angles.append(h.real(actual['jointPositionBits'][index])-h.real(resting['jointPositionBits'][index]))
                speeds.append(h.real(actual['jointVelocityBits'][index])-h.real(resting['jointVelocityBits'][index]))
            joint_response=dict(joint=fixture['child'],axis=fixture['axis'],nativeIndex=index,
                maximumAbsoluteAngleDifferenceRadians=max(map(abs,angles)),finalAngleDifferenceRadians=angles[-1],
                maximumAbsoluteSpeedDifferenceRadiansPerSecond=max(map(abs,speeds)),practicalControlQualified=False)
        summaries.append(dict(name=case['name'],originalEndpointsIdentical=original_equal,pairedEndpointsIdentical=paired_equal,steps=8,jointResponse=joint_response))
    assert total==624 and len(summaries)==78 and rows
    all_original=all(s['originalEndpointsIdentical'] for s in summaries); all_pairs=all(s['pairedEndpointsIdentical'] for s in summaries)
    ledger=all(r['passed'] for r in rows)
    write(package/'force-observations.json',rows)
    write(package/'analysis.json',dict(completed=True,mechanicalCriteriaPassed=all_original and all_pairs and ledger,
        actualPhysicsSteps=total,cases=summaries,originalEndpointsIdentical=all_original,observationLeavesMotionUnchanged=all_pairs,
        accumulatedForceAndTorquePassed=ledger,accumulatorChecks=len(rows),failedAccumulatorChecks=sum(not r['passed'] for r in rows),
        maximumAllowanceFraction=max(r['error']/r['allowance'] for r in rows),
        independentCoupledTrajectoryQualified=False,neuronalSourceExecuted=False,physiologyQualified=False,avatarBalanceQualified=False))
    print(f'78 paired cases / 624 steps. Original endpoints: {all_original}; observation unchanged: {all_pairs}; native accumulators: {ledger}.',flush=True)
    if not (all_original and all_pairs and ledger): raise RuntimeError('Native force observation criteria failed; retain original bounds and all records.')


if __name__=='__main__':
    if not __debug__: raise RuntimeError('Assertions must remain enabled.')
    p=argparse.ArgumentParser();p.add_argument('stage',choices=('prepare','preflight','check-tests','analyse'));p.add_argument('package',nargs='?',type=Path);a=p.parse_args()
    if a.stage=='prepare': prepare()
    else:
        assert a.package is not None
        {'preflight':preflight,'check-tests':h.check_tests,'analyse':analyse}[a.stage](a.package.resolve())

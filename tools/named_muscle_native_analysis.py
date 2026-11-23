"""Independent saved accounting of actual named-path Unity force applications."""
import hashlib
import importlib.util
import json
import math
from pathlib import Path
import struct

ROOT=Path(__file__).resolve().parents[1]
def module(name,path):
    s=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def rows(p):return [json.loads(line) for line in p.read_text(encoding='utf-8-sig').splitlines()]
def sha(s):return hashlib.sha256(s.encode()).hexdigest().upper()
def real(v):
    if isinstance(v,dict):v=v['bits']
    assert isinstance(v,str) and len(v)==16
    r=struct.unpack('>d',bytes.fromhex(v))[0];assert math.isfinite(r);return r
def bitsvec(a):return tuple(map(real,a))
def xyz(a):return tuple(a[k] for k in ('x','y','z'))
def sub(a,b):return tuple(x-y for x,y in zip(a,b,strict=True))
def add(a,b):return tuple(x+y for x,y in zip(a,b,strict=True))
def mul(a,k):return tuple(v*k for v in a)
def dot(a,b):return sum(x*y for x,y in zip(a,b,strict=True))
def cross(a,b):return (a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0])
def f32(v):return struct.unpack('<f',struct.pack('<f',v))[0]

def compare(package):
    cfg=read(package/'configuration.json');limits=cfg['NamedNativeLimits'];identity=module('named_geometry_identity',ROOT/'tools/native_geometry_identity.py')
    input=read(package/'anatomical-input/binding-input.json');original=json.loads(input['originalNativeEndpointJson'])
    old_geometry=json.loads(original['geometryJson']);old_canonical,old_objects,_,_=identity.geometry_identity(original['geometryJson'])
    mapped=read(package/'frame-review/mapped-lower-leg-paths.json');paths=sorted([r['path'] for r in mapped['paths']],key=lambda p:p['MuscleId'])
    expected_cases=[('quiet','',0)]+[(p['MuscleId'],p['MuscleId'],0) for p in paths]+[('quiet-inspected','',2)]
    capture=package/'native'; protocol=read(capture/'protocol.json'); result=read(capture/'result.json')
    assert [(c['name'],c['muscle'],c['inspectionsPerEndpoint']) for c in protocol['cases']]==expected_cases
    assert protocol['prescribedTensionNewtons']==40 and protocol['passivePreludeSteps']==1
    assert result['completed'] and [result[k] for k in ('nativeCases','physicsSteps','physicalEndpoints','headReceipts','extraInspections')]==[12,96,108,324,18]
    summaries=[];all_ack={};force_rows=[];max_point_error=0.;max_length_error=0.
    for name,selected,inspect in expected_cases:
        folder=capture/name;wire=folder/'tendon-bridge';c=read(folder/'input.json');c_text=(folder/'input.json').read_text(encoding='utf-8-sig')
        assert c==read(wire/'configuration.json') and c['protocol']=='named-avatar-tendon-wire-v1' and c['mechanicsMode']=='no-joint-friction'
        assert c['passivePreludeSteps']==1 and len(c['declarations'])==10
        for d,p in zip(c['declarations'],paths,strict=True):
            assert d['muscleId']==p['MuscleId'] and d['provenance']==p['Provenance'] and len(d['anchors'])==len(p['Anchors'])
            for a,b in zip(d['anchors'],p['Anchors'],strict=True):
                assert a['id']==b['AttachmentId'] and a['bodyId']==b['BodyId']
                assert tuple(real(a[k]) for k in ('x','y','z'))==tuple(b['LocalPositionMeters'][k] for k in ('X','Y','Z'))
        ack=rows(folder/'acknowledged.jsonl');heads=rows(folder/'heads.jsonl');inspections=rows(folder/'inspections.jsonl');all_ack[name]=ack
        assert len(ack)==len(heads)==9 and len(inspections)==inspect*9 and ack[0]==read(wire/'hello.json')
        assert ack[0]['stateJson']==original['stateJson'];raw=ack[0]['geometryJson'];g=json.loads(raw)
        assert g['version']=='declared-named-avatar-tendon-paths-v1' and sha(raw)==ack[0]['bodyFingerprint']
        # A comparison VIEW changes only the declared version/path set; the raw
        # new body/path identities below are independently verified unchanged.
        token='"version":"declared-named-avatar-tendon-paths-v1"';assert raw.count(token)==1
        view=raw.replace(token,'"version":"declared-full-avatar-tendon-paths-v2"',1)
        canonical,objects,_,_=identity.geometry_identity(view)
        quoted=json.dumps(g['declarationsJson'],ensure_ascii=False);assert canonical.count(quoted)==1
        canonical=canonical.replace(quoted,json.dumps(old_geometry['declarationsJson'],ensure_ascii=False),1)
        assert canonical==old_canonical,'Body mechanics changed beyond the explicit path set and process-local IDs'
        assert [(o['collection'],o['name']) for o in objects]==[(o['collection'],o['name']) for o in old_objects]
        declarations=json.loads(g['declarationsJson'])['paths']
        assert declarations==[dict(id=d['muscleId'],provenance=d['provenance'],anchors=[dict(id=a['id'],bodyId=a['bodyId'],
            xBits=a['x']['bits'],yBits=a['y']['bits'],zBits=a['z']['bits']) for a in d['anchors']]) for d in c['declarations']]
        hashes={d['id']:identity.path_digest(d,sha(raw)) for d in declarations}
        previous=ack[0];moving=[previous];retained_inspections=[];operations=[];efforts=set();dt=real(c['stepSeconds'])*1000
        requests=sorted(wire.glob('request-*.json'));replies=sorted(wire.glob('reply-*.json'));assert len(requests)==len(replies)==10+inspect*9
        for sequence,(qfile,rfile) in enumerate(zip(requests,replies,strict=True),1):
            q=read(qfile);r=read(rfile);op=q['operation'];operations.append(op)
            assert q['protocol']==r['protocol']==c['protocol'] and q['sessionId']==r['sessionId']==c['sessionId']
            assert q['configurationHash']==r['configurationHash']==sha(c_text) and q['sequence']==r['sequence']==sequence
            assert q['expectedStep']==previous['physicsStep'] and q['stateHash']==previous['stateHash'] and q['bodyFingerprint']==r['bodyFingerprint']==sha(raw)
            assert r['operation']==op and r['geometryJson']==raw and r['mechanicsDiagnosticJson']==previous['mechanicsDiagnosticJson']
            assert r['paused'] and r['isolated'] and not r['connected'] and r['unity']=='6000.3.19f1'
            assert r['physicsStep']==previous['physicsStep']+(op in ('step','prelude'))
            assert real(q['startTimeMs'])==previous['physicsStep']*dt and real(q['endTimeMs'])==real(r['physicalTimeMs'])==r['physicsStep']*dt
            assert r['stateHash']==sha(r['stateJson']) and r['observationsHash']==sha(r['observationsJson'])
            assert [(p['muscleId'],p['pathFingerprint']) for p in r['paths']]==list(hashes.items())
            assert all(real(p['lengthMeters'])>0 and math.isfinite(real(p['velocityMetersPerSecond'])) for p in r['paths'])
            if op in ('step','prelude'):moving.append(r)
            else:
                assert all(r[k]==previous[k] for k in ('stateJson','observationsJson','applicationsJson','paths'))
                if op=='inspect':retained_inspections.append(r)
            if op=='prelude':assert previous['physicsStep']==0 and not q['tensions'] and q['effortId']=='' and r['applicationsJson']==''
            elif op=='step':
                assert q['effortId'] not in efforts and r['lastAppliedEffortId']==q['effortId'];efforts.add(q['effortId'])
                assert [(t['muscleId'],real(t['axialNewtons'])) for t in q['tensions']]==[(p['MuscleId'],40. if p['MuscleId']==selected else 0.) for p in paths]
                apps=json.loads(r['applicationsJson'])['points'];assert len(apps)==44
                states={s['id']:s for s in json.loads(previous['stateJson'])['links']};index=0
                for p,measurement in zip(paths,previous['paths'],strict=True):
                    count=len(p['Anchors']);points=apps[index:index+count];index+=count
                    positions=[bitsvec(a['pointBits']) for a in points];directions=[];length=0.
                    for a,b in zip(positions,positions[1:]):
                        delta=sub(b,a);distance=math.sqrt(dot(delta,delta));assert distance>0;length+=distance;directions.append(mul(delta,1/distance))
                    error=abs(length-real(measurement['lengthMeters']));max_length_error=max(max_length_error,error);assert error<=limits['pathLengthMeters']
                    for i,(a,anchor) in enumerate(zip(points,p['Anchors'],strict=True)):
                        assert a['muscleId']==p['MuscleId'] and a['attachmentId']==anchor['AttachmentId'] and a['bodyId']==anchor['BodyId']
                        state=states[a['bodyId']];local=tuple(f32(anchor['LocalPositionMeters'][k]) for k in ('X','Y','Z'));rot=bitsvec(state['rotationBits']);v=rot[:3]
                        expected_point=add(bitsvec(state['positionBits']),add(local,mul(cross(v,add(cross(v,local),mul(local,rot[3]))),2)))
                        point_error=max(abs(x-y) for x,y in zip(expected_point,positions[i],strict=True));max_point_error=max(max_point_error,point_error)
                        assert point_error<=limits['nativePointMeters']
                        force=(0.,0.,0.);tension=40. if p['MuscleId']==selected else 0.
                        if i:force=sub(force,mul(directions[i-1],tension))
                        if i<len(directions):force=add(force,mul(directions[i],tension))
                        requested=tuple(real(a[k]) for k in ('requestedXBits','requestedYBits','requestedZBits'))
                        force_error=max(abs(x-y) for x,y in zip(force,requested,strict=True))
                        force_rows.append(dict(case=name,step=r['physicsStep'],muscle=p['MuscleId'],attachment=anchor['AttachmentId'],errorNewtons=force_error))
                        assert force_error<=limits['forceNewtons'] and bitsvec(a['appliedBits'])==tuple(f32(x) for x in requested)
            previous=r
        assert operations.count('step')==7 and operations.count('prelude')==operations.count('bind')==operations.count('stop')==1 and operations[-1]=='stop'
        assert moving==ack and retained_inspections==inspections
        for step,(a,h) in enumerate(zip(ack,heads,strict=True)):
            assert a['physicsStep']==h['physicsStep']==step and h['bodyStateSha256']==a['stateHash'] and h['originalObservationsJson']==a['observationsJson']
            obs=json.loads(a['observationsJson']);assert obs['physicsStep']==step and len(obs['contactsJson'])==15 and len(obs['skullJson'])==len(h['samples'])==3
            for sample,raw_skull in zip(h['samples'],obs['skullJson'],strict=True):
                assert sample['originalNativeJson']==raw_skull and sample['nativeSha256']==sha(raw_skull)
                assert sample['kinematics']['physicsStep']==step and (sample['kinematics']['vestibularInput'] is None)==(step==0)
        summaries.append(dict(name=name,physicsSteps=8,endpoints=9,headReceipts=27,inspections=inspect*9))
    quiet=all_ack['quiet'];old_quiet=rows(package/'anatomical-input/native-evidence/conditions/no-joint-friction/native/quiet/acknowledged.jsonl')
    assert [r['stateJson'] for r in quiet]==[r['stateJson'] for r in old_quiet]
    assert [r['stateJson'] for r in quiet]==[r['stateJson'] for r in all_ack['quiet-inspected']]
    assert [r['applicationsJson'] for r in quiet]==[r['applicationsJson'] for r in all_ack['quiet-inspected']]
    responses=[];quiet_end=json.loads(quiet[-1]['stateJson'])['links']
    for p in paths:
        state=json.loads(all_ack[p['MuscleId']][-1]['stateJson'])['links'];assert [r['id'] for r in state]==[r['id'] for r in quiet_end]
        maximum=max(abs(real(x)-real(y)) for a,b in zip(state,quiet_end,strict=True) for x,y in zip(a['jointPositionBits'],b['jointPositionBits'],strict=True))
        responses.append(dict(muscle=p['MuscleId'],maximumJointResponseRadians=maximum,observed=maximum>limits['responseRadians']))
    assert len(force_rows)==3696
    analysis=dict(completed=True,nativeAccountingPassed=True,namedMuscles=10,points=44,physicsSteps=96,headReceipts=324,
        originalQuietMotionExact=True,inspectionMotionExact=True,maximumPointErrorMeters=max_point_error,maximumLengthErrorMeters=max_length_error,
        maximumForceErrorNewtons=max(r['errorNewtons'] for r in force_rows),responses=responses,allNamedPathsResponded=all(r['observed'] for r in responses),
        limits=limits,footScaleIsDeclaredShinRatio=True,anatomicalFitQualified=False,independentCoupledTrajectoryQualified=False,
        neuronalSourceExecuted=False,muscleCellExecuted=False,avatarStandingQualified=False,cases=summaries)
    for name,value in [('named-native-analysis.json',analysis),('named-native-force-checks.json',force_rows)]:
        with (package/name).open('x',encoding='utf-8') as stream:json.dump(value,stream,indent=2,allow_nan=False);stream.write('\n')
    assert analysis['allNamedPathsResponded'],'Some named paths lack the prospectively declared response; preserve results and bounds.'
    print('12 actual Unity cases / 96 physical steps: ten named muscle paths, 44 attachments and 3,696 point forces reconciled. Original quiet motion retained. Standing and neuronal authority remain unqualified.',flush=True)
    return analysis

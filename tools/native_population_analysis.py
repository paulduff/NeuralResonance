"""Decode saved body, muscle and diagnostic packets; never integrate a model."""
import gzip
import importlib.util
import json
import math
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
def module(name,path):
    s=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m
n=module('population_force_accounting',ROOT/'tools/named_muscle_native_analysis.py')
h=module('population_audit_helpers',ROOT/'tools/native_avatar_tendon_study.py')
read,write=h.read,h.write
def lines(path):
    with (gzip.open(path,'rt',encoding='utf-8-sig') if path.suffix=='.gz' else path.open(encoding='utf-8-sig')) as f:
        for line in f:yield json.loads(line)
def finite(value):
    if isinstance(value,float):assert math.isfinite(value)
    elif isinstance(value,dict):
        for v in value.values():finite(v)
    elif isinstance(value,list):
        for v in value:finite(v)

def native_case(package,condition,paths,identity,old_canonical,old_geometry,original_quiet):
    name=condition['name'];folder=package/'native'/name;wire=folder/'tendon-bridge';limits=read(package/'configuration.json')['NativeLimits']
    c=read(folder/'input.json');ct=(folder/'input.json').read_text(encoding='utf-8-sig')
    decl=read(folder/'declaration.json');result=read(folder/'result.json');initial=read(folder/'initial.json')
    assert result['completed'] and result['condition']==condition and decl['condition']==condition
    assert decl['prescribedDiagnosticEvents'] and not decl['neuronalModelExecuted']
    assert c==read(wire/'configuration.json') and c['protocol']=='named-avatar-tendon-wire-v1' and c['mechanicsMode']=='no-joint-friction'
    assert c['passivePreludeSteps']==1 and len(c['declarations'])==2
    for d,p in zip(c['declarations'],paths,strict=True):
        assert d['muscleId']==p['MuscleId'] and d['provenance']==p['Provenance']
        for a,b in zip(d['anchors'],p['Anchors'],strict=True):
            assert a['id']==b['AttachmentId'] and a['bodyId']==b['BodyId']
            assert tuple(n.real(a[k]) for k in ('x','y','z'))==tuple(b['LocalPositionMeters'][k] for k in ('X','Y','Z'))
    ack=list(lines(folder/'acknowledged.jsonl'));heads=list(lines(folder/'heads.jsonl'));steps=list(lines(folder/'steps.jsonl.gz'))
    assert len(ack)==len(heads)==42 and len(steps)==40 and ack[0]==read(wire/'hello.json')
    assert [a['stateJson'] for a in ack[:2]]==[a['stateJson'] for a in original_quiet[:2]]
    raw=ack[0]['geometryJson'];g=json.loads(raw);body_hash=n.sha(raw)
    token='"version":"declared-named-avatar-tendon-paths-v1"';assert raw.count(token)==1
    canonical,_,_,_=identity.geometry_identity(raw.replace(token,'"version":"declared-full-avatar-tendon-paths-v2"',1))
    quoted=json.dumps(g['declarationsJson'],ensure_ascii=False);assert canonical.count(quoted)==1
    assert canonical.replace(quoted,json.dumps(old_geometry['declarationsJson'],ensure_ascii=False),1)==old_canonical
    declarations=json.loads(g['declarationsJson'])['paths']
    assert declarations==[dict(id=d['muscleId'],provenance=d['provenance'],anchors=[dict(id=a['id'],bodyId=a['bodyId'],
        xBits=a['x']['bits'],yBits=a['y']['bits'],zBits=a['z']['bits']) for a in d['anchors']]) for d in c['declarations']]
    hashes={d['id']:identity.path_digest(d,body_hash) for d in declarations}
    previous=ack[0];moving=[previous];operations=[];dt=n.real(c['stepSeconds'])*1000;step_index=0
    assert dt==4.999999888241291 and decl['clock']['physicsStepMs']==dt
    force_checks=[];requests=sorted(wire.glob('request-*.json'));replies=sorted(wire.glob('reply-*.json'))
    assert len(requests)==len(replies)
    for sequence,(qf,rf) in enumerate(zip(requests,replies,strict=True),1):
        q=read(qf);r=read(rf);op=q['operation'];operations.append(op)
        assert q['protocol']==r['protocol']==c['protocol'] and q['sessionId']==r['sessionId']==c['sessionId']
        assert q['configurationHash']==r['configurationHash']==n.sha(ct) and q['sequence']==r['sequence']==sequence
        assert q['expectedStep']==previous['physicsStep'] and q['stateHash']==previous['stateHash'] and q['bodyFingerprint']==r['bodyFingerprint']==body_hash
        assert r['operation']==op and r['geometryJson']==raw and r['mechanicsDiagnosticJson']==previous['mechanicsDiagnosticJson']
        assert r['paused'] and r['isolated'] and not r['connected'] and r['unity']=='6000.3.19f1'
        assert r['physicsStep']==previous['physicsStep']+(op in ('step','prelude'))
        assert n.real(q['startTimeMs'])==previous['physicsStep']*dt and n.real(q['endTimeMs'])==n.real(r['physicalTimeMs'])==r['physicsStep']*dt
        assert r['stateHash']==n.sha(r['stateJson']) and r['observationsHash']==n.sha(r['observationsJson'])
        assert [(p['muscleId'],p['pathFingerprint']) for p in r['paths']]==list(hashes.items())
        assert all(n.real(p['lengthMeters'])>0 and math.isfinite(n.real(p['velocityMetersPerSecond'])) for p in r['paths'])
        if op in ('step','prelude'):moving.append(r)
        else:assert all(r[k]==previous[k] for k in ('stateJson','observationsJson','applicationsJson','paths'))
        if op=='prelude':assert previous['physicsStep']==0 and not q['tensions'] and q['effortId']=='' and r['applicationsJson']==''
        elif op=='step':
            s=steps[step_index]['step'];before=initial['muscles'] if step_index==0 else steps[step_index-1]['step']['muscles'];step_index+=1
            assert s['before']['physicsStep']==previous['physicsStep'] and s['after']['physicsStep']==r['physicsStep']
            assert s['effort']['effortId'].replace('-','')==q['effortId'].replace('-','')==r['lastAppliedEffortId'].replace('-','')
            tensions={t['muscleId']:n.real(t['axialNewtons']) for t in q['tensions']}
            assert tensions=={t['muscleId']:t['axialNewtons'] for t in s['effort']['tensions']}
            assert tensions=={k:m['forces']['tendonAxialForceNewtons'] for k,m in before.items()}
            assert [(p['muscleId'],p['lengthMeters']) for p in s['before']['paths']]==[(p['muscleId'],n.real(p['lengthMeters'])) for p in previous['paths']]
            assert [(p['muscleId'],p['lengthMeters']) for p in s['after']['paths']]==[(p['muscleId'],n.real(p['lengthMeters'])) for p in r['paths']]
            assert all(s['muscles'][p['muscleId']]['musculotendonLengthMeters']==n.real(p['lengthMeters']) for p in r['paths'])
            expected_work=sum(tensions[a['muscleId']]*(a['lengthMeters']-b['lengthMeters']) for a,b in zip(s['before']['paths'],s['after']['paths'],strict=True))
            assert abs(expected_work-s['heldTensionWorkOnBodyJoules'])<=1e-12
            apps=json.loads(r['applicationsJson'])['points'];assert len(apps)==8
            states={s['id']:s for s in json.loads(previous['stateJson'])['links']};index=0
            for p,measurement in zip(paths,previous['paths'],strict=True):
                points=apps[index:index+len(p['Anchors'])];index+=len(points);positions=[n.bitsvec(a['pointBits']) for a in points];directions=[];length=0.
                for a,b in zip(positions,positions[1:]):
                    delta=n.sub(b,a);distance=math.sqrt(n.dot(delta,delta));assert distance>0;length+=distance;directions.append(n.mul(delta,1/distance))
                le=abs(length-n.real(measurement['lengthMeters']));assert le<=limits['pathLengthMeters']
                for i,(a,anchor) in enumerate(zip(points,p['Anchors'],strict=True)):
                    assert a['muscleId']==p['MuscleId'] and a['attachmentId']==anchor['AttachmentId'] and a['bodyId']==anchor['BodyId']
                    state=states[a['bodyId']];local=tuple(n.f32(anchor['LocalPositionMeters'][k]) for k in ('X','Y','Z'));rot=n.bitsvec(state['rotationBits']);v=rot[:3]
                    expected=n.add(n.bitsvec(state['positionBits']),n.add(local,n.mul(n.cross(v,n.add(n.cross(v,local),n.mul(local,rot[3]))),2)))
                    pe=max(abs(x-y) for x,y in zip(expected,positions[i],strict=True));assert pe<=limits['nativePointMeters']
                    force=(0.,0.,0.);tension=tensions[p['MuscleId']]
                    if i:force=n.sub(force,n.mul(directions[i-1],tension))
                    if i<len(directions):force=n.add(force,n.mul(directions[i],tension))
                    requested=tuple(n.real(a[k]) for k in ('requestedXBits','requestedYBits','requestedZBits'))
                    fe=max(abs(x-y) for x,y in zip(force,requested,strict=True));assert fe<=limits['forceNewtons']
                    assert n.bitsvec(a['appliedBits'])==tuple(n.f32(x) for x in requested)
                    force_checks.append(dict(step=r['physicsStep'],muscle=p['MuscleId'],attachment=anchor['AttachmentId'],pointErrorMeters=pe,lengthErrorMeters=le,forceErrorNewtons=fe))
        previous=r
    assert step_index==40 and operations.count('step')==40 and operations.count('prelude')==operations.count('bind')==operations.count('stop')==1 and operations[-1]=='stop'
    assert moving==ack and len(force_checks)==320
    for step,(a,head) in enumerate(zip(ack,heads,strict=True)):
        assert a['physicsStep']==head['physicsStep']==step and head['bodyStateSha256']==a['stateHash'] and head['originalObservationsJson']==a['observationsJson']
        obs=json.loads(a['observationsJson']);assert obs['physicsStep']==step and len(obs['contactsJson'])==15 and len(obs['skullJson'])==len(head['samples'])==3
        for sample,raw_skull in zip(head['samples'],obs['skullJson'],strict=True):
            assert sample['originalNativeJson']==raw_skull and sample['nativeSha256']==n.sha(raw_skull)
            assert sample['kinematics']['physicsStep']==step and (sample['kinematics']['vestibularInput'] is None)==(step==0)
    # Verify every original source field and every delivered identity/time.
    pop=read(package/'inputs/population.json');defs=decl['definitions'];assert [d['muscleId'] for d in defs]==['tibant_l','tibant_r']
    contacts=[]
    for side,d in enumerate(defs):
        scenario=next(c for c in pop['cases'] if c['key']==('left-mixed' if side==0 else 'right-mixed'))
        assert d['geometry']==scenario['geometry'] and d['initialFiberLengthMeters']==scenario['initialFiberLengthMeters'] and d['initialFiberVelocityMetersPerSecond']==0
        assert len(d['units'])==147
        for u,p in zip(d['units'],pop['parameterRows'],strict=True):
            assert all(u[k]==p[k] for k in ('calcium','lengthGainAboveOptimal','lengthGainBelowOptimal'));contacts.append(u['contact'])
        m=initial['muscles'][d['muscleId']];assert m['fiberLengthMeters']==scenario['initialFiberLengthMeters'] and m['musculotendonLengthMeters']==scenario['initialMusculotendonLengthMeters'] and m['timeMs']==scenario['initialTimeMs']
    assert len({c['sourceInstanceKey'] for c in contacts})==len({c['motorUnitId'] for c in contacts})==294
    assert all(c['conductionDelayMs']==0 for c in contacts)
    emitted={};by_interval=[[] for _ in steps];count=0;ordinal=[0]*294;previous=decl['motorClock']['neuralEpochMs']
    for record in lines(folder/'packets.jsonl.gz'):
        interval,index=divmod(count,294);count+=1;assert record['interval']==interval and record['source']==index
        b=record['batch'];c=contacts[index];through=steps[interval]['step']['after']['physicalTimeMs']
        assert b['sourceInstanceKey']==c['sourceInstanceKey'] and b['anatomyFingerprint']==decl['anatomyFingerprint'] and b['sequence']==interval+1
        assert b['completeThroughNeuralMs']==through and record['retry']==condition['retry']
        receipt=record['receipt'];assert all(receipt[k]==b[k] for k in ('version','sessionId','sourceInstanceKey','anatomyFingerprint','sequence')) and len(receipt['batchSha256'])==64
        lower=decl['motorClock']['neuralEpochMs'] if interval==0 else steps[interval-1]['step']['after']['physicalTimeMs']
        selected=[s for s in decl['fixtures'][index] if lower<s['timestampMs']<=through] if condition['left' if index<147 else 'right'] else []
        assert record['selected']==selected
        expected=[] if condition['cut'] else selected;assert len(b['spikes'])==len(expected)
        for spike,original in zip(b['spikes'],expected,strict=True):
            ordinal[index]+=1
            assert spike['ordinal']==ordinal[index] and spike['messageId']==original['messageId'] and spike['firedAtNeuralMs']==original['timestampMs']
            assert spike['sourceNeuronId']==c['sourceNeuronId'] and spike['projectionId']==c['projectionId'] and spike['sourceSynapseId']==original['synapseId']
            assert spike['neurotransmitter']=='ACETYLCHOLINE' and not spike['isFeedback'] and not spike['isSyntheticBackground']
            assert spike['messageId'] not in emitted;emitted[spike['messageId']]=(spike,c);by_interval[interval].append(spike['messageId'])
    assert count==11760 and len(emitted)==(0 if condition['cut'] else 588*(condition['left']+condition['right']))
    for i,row in enumerate(steps):
        finite(row);s=row['step'];state=row['state'];assert not state['faulted'] and state['pendingEvents']==0
        assert state['completedPhysicsStep']==state['observedPhysicsStep']==i+2 and state['bodyTimeMs']==state['completeThroughBodyMs']==(i+2)*dt
        assert {a['messageId'] for a in s['arrivals']}==set(by_interval[i]) and len(s['arrivals'])==len(by_interval[i])
        for a in s['arrivals']:
            spike,c=emitted[a['messageId']]
            assert a['firedAtNeuralMs']==a['arrivalAtBodyMs']==spike['firedAtNeuralMs'] and a['ordinal']==spike['ordinal']
            assert all(a[k]==c[k] for k in ('sourceInstanceKey','sourceNeuronId','projectionId','motorUnitId','contactId'))
            assert a['sourceSynapseId']==spike['sourceSynapseId']
        assert state['acceptedEvents']==state['deliveredEvents']==sum(len(x) for x in by_interval[:i+1])
        assert list(s['muscles'])==['tibant_l','tibant_r']
        for key,m in s['muscles'].items():
            assert m['timeMs']==(i+2)*dt and not m['faulted'] and len(m['units'])==147 and m['fiberLengthMeters']>0
            assert all(u['freeCalcium']>=0 and u['boundCalcium']>=0 and u['activation']>=0 for u in m['units'])
            assert [u['motorUnitId'] for u in m['units']]==[u['contact']['motorUnitId'] for d in defs if d['muscleId']==key for u in d['units']]
        assert sum(u['arrivals'] for m in s['muscles'].values() for u in m['units'])==state['deliveredEvents']
    assert result['events']==len(emitted) and result['packets']==count and result['waitChecks']==80 and result['retries']==(11760 if condition['retry'] else 0)
    write(folder/'saved-force-analysis.json',force_checks)
    return dict(name=name,events=len(emitted),packets=count,pointForces=len(force_checks),endpoints=len(ack),headReceipts=3*len(ack)),ack,steps

def compare(package):
    cfg=read(package/'configuration.json');identity=module('population_geometry_identity',ROOT/'tools/native_geometry_identity.py')
    original=json.loads(read(package/'inputs/binding.json')['originalNativeEndpointJson']);geometry=json.loads(original['geometryJson'])
    canonical,_,_,_=identity.geometry_identity(original['geometryJson'])
    paths=sorted([r['path'] for r in read(package/'inputs/mapping.json')['paths'] if r['path']['MuscleId'] in ('tibant_l','tibant_r')],key=lambda p:p['MuscleId'])
    quiet=list(lines(package/'inputs/quiet.jsonl'));report=[];captures={};errors=[]
    for condition in cfg['Cases']:
        try:
            result,ack,steps=native_case(package,condition,paths,identity,canonical,geometry,quiet);report.append(result);captures[condition['name']]=(ack,steps)
            print(condition['name'],result,flush=True)
        except Exception as e:
            import traceback
            errors.append(dict(case=condition['name'],error=str(e),traceback=traceback.format_exc()));print(errors[-1]['traceback'],flush=True)
    if not errors:
        try:
            for a,b in [('quiet','both-cut'),('both','both-retry')]:
                aa,ss=captures[a];bb,tt=captures[b]
                for field in ('stateJson','applicationsJson'):
                    assert [r[field] for r in aa]==[r[field] for r in bb],(a,b,field)
                assert [r['step']['muscles'] for r in ss]==[r['step']['muscles'] for r in tt],(a,b,'muscles')
        except Exception as e:errors.append(dict(case='cut/retry controls',error=str(e)))
    responses=[]
    for name in ('left','right','both','both-finer'):
        baseline='quiet-finer' if name.endswith('finer') else 'quiet'
        if name not in captures or baseline not in captures:continue
        maximum=max(abs(n.real(x)-n.real(y)) for a,b in zip(captures[name][0],captures[baseline][0],strict=True)
                    for la,lb in zip(json.loads(a['stateJson'])['links'],json.loads(b['stateJson'])['links'],strict=True)
                    for x,y in zip(la['jointPositionBits'],lb['jointPositionBits'],strict=True))
        responses.append(dict(case=name,maximumJointResponseRadians=maximum,observed=maximum>cfg['NativeLimits']['responseRadians']))
    analysis=dict(completed=True,nativeAccountingPassed=not errors,cases=report,errors=errors,responses=responses,
        allActiveCasesResponded=len(responses)==4 and all(r['observed'] for r in responses),cutAndRetryExact=not errors,
        independentCoupledTrajectoryQualified=False,physiologicalRecruitmentQualified=False,neuronalAuthorityQualified=False,avatarStandingQualified=False,
        prescribedDiagnosticEvents=True,bodyStepRefinementPerformed=False,muscleSubstepComparisonDiagnosticOnly=True)
    write(package/'native-population-analysis.json',analysis)
    assert not errors and len(report)==8 and sum(r['pointForces'] for r in report)==2560 and analysis['allActiveCasesResponded'],'Saved native population criteria failed; retain original evidence and bounds.'
    print('Eight native cases / 294 units / 2,560 point forces reconciled. Prescribed events; coupled accuracy, physiology and standing remain separate.',flush=True)

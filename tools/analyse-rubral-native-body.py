"""Reconcile original neuronal events and native muscle motion; solve only in visible PowerShell."""
import argparse
from copy import deepcopy
import gzip
import hashlib
import importlib.util
import json
import math
import os
from pathlib import Path
import traceback
import uuid
for name in ('OMP_NUM_THREADS','OPENBLAS_NUM_THREADS','MKL_NUM_THREADS','NUMEXPR_NUM_THREADS'):os.environ[name]='1'
import numpy as np
from native_hinge_protocol import read,decode
from native_hinge_reference import HingeGeometry,HingeCoupledReference

STEPS=7999
CONDITIONS=[('quiet','no-evoked-drive',0,False,0,17,False),('unit-left','drive-1',1,False,0,17,False),
    ('strong-left','drive-4',4,False,0,17,False),('strong-right','drive-4',4,False,1,17,False),
    ('strong-cut','drive-4',4,True,0,17,False),('strong-fast','drive-4',4,False,0,1,False),('strong-retry','drive-4',4,False,0,17,True)]


def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m


def save(path,value):
    with path.open('x') as f:json.dump(value,f,indent=2,allow_nan=False)


def digest(text):return hashlib.sha256(text.encode()).hexdigest().upper()


def file_hash(path):
    with path.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()


def stable(*parts):
    text=''.join(str(len(str(p)))+':'+str(p) for p in parts)
    return str(uuid.UUID(bytes_le=hashlib.sha256(text.encode()).digest()[:16]))


def records(path):
    with gzip.open(path,'rt',encoding='utf-8') as f:
        for line in f:yield json.loads(line)


def source_and_packets(package,folder,condition,initial,final,ms):
    key,reference,quanta,cut,active,cadence,retry=condition
    root=folder/'neuronal';baseline=package/'reference/rubral/study'/reference
    fixture=read(root/'input.json');prior_fixture=read(baseline/'input.json');topology=read(root/'topology.json');prior_topology=read(baseline/'topology.json')
    session=initial['session'];assert fixture['session']==session and fixture['quanta']==quanta
    assert fixture['instance']==prior_fixture['instance'] and fixture['originalEvents']==prior_fixture['originalEvents']
    for field in ('allRoutes','selected','contacts','contactIds'):assert topology[field]==prior_topology[field]
    contacts=dict(zip(topology['contactIds'],topology['contacts'],strict=True));routes={r['SourceNeuron']:r for r in topology['allRoutes']}
    selected=admitted=0;input_ids=set();original_ids=set();receiving={};fixture_by_id={e['MessageId']:e for e in fixture['originalEvents']}
    for n,(frame,prior,admissions) in enumerate(zip(records(root/'source-frames.jsonl.gz'),records(baseline/'source-frames.jsonl.gz'),records(root/'admissions.jsonl.gz'),strict=True),1):
        assert frame['Session']==session and frame['Sequence']==n and frame['CompleteThroughMs']==n*.125 and digest(frame['CanonicalJson'])==frame['Sha256']
        payload=json.loads(frame['CanonicalJson']);old=json.loads(prior['CanonicalJson'])
        assert payload['Session']==session and payload['Sequence']==n and payload['CompleteThroughMs']==n*.125
        assert payload['Observation']==old['Observation'] and payload['Energy']==old['Energy'] and payload['TimingConvention']==old['TimingConvention']
        assert [(e['SourceNeuronId'],e['TimestampMs']) for e in payload['OriginalEvents']]==[(e['SourceNeuronId'],e['TimestampMs']) for e in old['OriginalEvents']]
        assert payload['FiredCount']==len(payload['OriginalEvents']) and payload['AdmittedFixtureEvents']==old['AdmittedFixtureEvents']
        for e in payload['OriginalEvents']:
            route=routes[e['SourceNeuronId']];assert e['SourceInstanceKey']==route['SourceInstance']==fixture['instance']
            for field,target in [('TargetStructure','TargetStructure'),('TargetNeuronId','TargetNeuron'),('ProjectionId','Projection'),('SynapseId','Synapse'),('Neurotransmitter','Transmitter')]:assert e[field]==route[target]
            assert not e['IsFeedback'] and not e['IsSyntheticBackground'] and (n-1)*.125<e['TimestampMs']<=n*.125
            assert e['MessageId'] not in original_ids;original_ids.add(e['MessageId'])
        for d in payload['InputDeliveries']:
            e=fixture_by_id[d['OriginalMessageId']];assert e['MessageId'] not in input_ids;input_ids.add(e['MessageId'])
            assert json.loads(d['OriginalJson'])==e and d['NamedTarget']==d['ActualTarget']==e['TargetNeuronId']
            index=int(e['TargetNeuronId'][2:]);assert d['ActualTargetIndex']==index
            assert e['TimestampMs']<=d['ArrivalAtMs']==d['AppliedAtMs']<=n*.125 and d['AppliedAtMs']>(n-1)*.125
            assert d['ReceivingContactId']!='00000000-0000-0000-0000-000000000000'
            if index in receiving:assert receiving[index]==d['ReceivingContactId']
            receiving[index]=d['ReceivingContactId']
        assert admissions['sourceSequence']==n and admissions['SourceAcknowledgement']==dict(Session=session,Sequence=n,Sha256=frame['Sha256'])
        assert len(admissions['Admissions'])==10 and {a['Receipt']['ContactId'] for a in admissions['Admissions']}==set(contacts)
        for a in admissions['Admissions']:
            r=a['Receipt'];c=contacts[r['ContactId']];chosen=[e for e in payload['OriginalEvents'] if e['SourceNeuronId']==c['SourceNeuron'] and e['TargetNeuronId']==c['TargetNeuron']]
            delivered=[] if cut else chosen
            assert [json.loads(s) for s in a['SelectedOriginalJson']]==chosen and [json.loads(s) for s in a['DeliveredOriginalJson']]==delivered
            assert r==dict(SessionId=session,ContactId=r['ContactId'],Sequence=n,CompleteThroughSourceMs=n*.125,Events=len(delivered),Sha256=digest(a['CanonicalInputJson']))
            assert json.loads(a['CanonicalInputJson'])==dict(SessionId=session,ContactId=r['ContactId'],Sequence=n,CompleteThroughSourceMs=n*.125,
                OriginalEventHashes=[digest(s) for s in a['DeliveredOriginalJson']])
            selected+=len(chosen);admitted+=len(delivered)
    assert n==12000 and input_ids==set(fixture_by_id) and len(receiving)==len(set(receiving.values()))==(256 if quanta else 0)
    branch='contacts-disabled' if cut else 'packets1';old=read(baseline/branch/'final.json')['source']
    assert file_hash(root/'trajectory.csv')==file_hash(baseline/branch/'trajectory.csv')
    motor=final['motor'];cell=motor['cell'];events=motor['emissions'];identity=topology['identity'];contact=initial['activeContact']
    assert len(contacts)==10 and [r['SourceIndex'] for r in topology['selected']]==list(range(81,91))
    assert identity['Instance']=='isolated-rubral-motor' and identity['Neuron']=='auto-SpinalCordMotor-002'
    quiet_identity=initial['quietIdentity'];quiet_contact=initial['quietContact']
    assert quiet_identity==dict(instance='rubral-native-quiet',neuron='FivecomptcMG-quiet',projection=stable('rubral-neuronal-native-hinge-v1','quiet-projection'),synapse=stable('rubral-neuronal-native-hinge-v1','quiet-synapse'))
    assert quiet_contact['sourceInstanceKey']==quiet_identity['instance'] and quiet_contact['sourceNeuronId']==quiet_identity['neuron'] and quiet_contact['projectionId']==quiet_identity['projection']
    assert sorted(initial['anatomy'],key=lambda c:c['contactId'])==sorted([contact,quiet_contact],key=lambda c:c['contactId'])
    for c in initial['anatomy']:
        assert c['contactId']==stable('motor-endplate-group-v1',c['sourceInstanceKey'],c['sourceNeuronId'],c['projectionId'],c['motorUnitId']) and c['conductionDelayMs']==2.0625
    assert motor['completedSamples']==15000 and cell['timeMs']==1500 and not motor['faulted'] and not cell['faulted']
    assert cell['membraneState']==old['Cell']['MembraneState'] and cell['crossings']==old['Cell']['Crossings']
    assert cell['admittedEvents']==cell['deliveredEvents']==admitted
    assert final['source']['completedSamples']==12000 and final['source']['producedEvents']==len(original_ids) and not final['source']['hasPendingFrame'] and not final['source']['faulted']
    primed=initial['primedMotor'];assert primed['completedSamples']==10000 and primed['cell']['timeMs']==1000 and not primed['emissions'] and not primed['cell']['crossings']
    assert initial['primedSource']['completedSamples']==8000 and not initial['primedSource']['hasPendingFrame']
    assert identity['Projection']==stable('isolated-rubral-receptor-motor-study-v1',session,'motor-projection') and identity['Synapse']==stable('isolated-rubral-receptor-motor-study-v1',session,'motor-synapse')
    assert contact['sourceInstanceKey']==identity['Instance'] and contact['sourceNeuronId']==identity['Neuron'] and contact['projectionId']==identity['Projection']
    for ordinal,e in enumerate(events,1):
        assert e==dict(ordinal=ordinal,messageId=stable('isolated-receptor-motor-source-cubic-v2',session,identity['Instance'],identity['Neuron'],identity['Projection'],identity['Synapse'],ordinal),
            firedAtNeuralMs=cell['crossings'][ordinal-1],sourceInstanceKey=identity['Instance'],sourceNeuronId=identity['Neuron'],projectionId=identity['Projection'],sourceSynapseId=identity['Synapse'])
    assert len(events)==len(cell['crossings'])
    quiet=final['quiet'];assert quiet['source']=='quiet' and quiet['completedSamples']==15000 and quiet['timeMs']==1500 and not quiet['emissions'] and not quiet['crossingsMs'] and not quiet['faulted']
    assert initial['primedQuiet']['completedSamples']==10000 and initial['primedQuiet']['timeMs']==1000 and not initial['primedQuiet']['emissions'] and not initial['primedQuiet']['crossingsMs']
    with (folder/'quiet-neuron.csv').open('rb') as actual,(package/'reference/quiet-h0.003125.csv').open('rb') as original:
        for rows,line in enumerate(actual,1):assert line==original.readline(),(key,rows)
        assert rows==15002 and list(map(float,line.split(b',')))==[1500]+quiet['state']
    assert final['originalRubralObservationsPreserved'] and final['originalMotorTrajectoryPreserved'] and final['quietPrefixPreserved'] and final['quietPrefixRows']==15001
    boundaries=[]
    for frame in range(8001,12001):
        if (frame-8000)%cadence==0 or frame==12000:
            sample=min(15000,int(frame*.125/.1)+1)
            while sample*.1>frame*.125:sample-=1
            boundaries.append(sample*.1)
    packets=iter(records(folder/'packets.jsonl.gz'));previous=1000.;seen=[];retries=0
    for sequence,through in enumerate(boundaries,1):
        tick=min(STEPS,int(np.count_nonzero(np.arange(STEPS+1)*ms<=previous-1000)-1))
        for source in range(2):
            p=next(packets);batch=p['batch'];receipt=p['receipt'];c=contact if source==0 else initial['quietContact']
            assert p['action']=='accept' and p['sourceIndex']==source and p['bodyStepAtReceipt']==tick
            assert batch==json.loads(p['canonicalBatchJson']) and digest(p['canonicalBatchJson'])==receipt['BatchSha256']
            assert batch['Version']==receipt['Version']==1 and batch['SessionId']==receipt['SessionId']==session and batch['Sequence']==receipt['Sequence']==sequence
            assert batch['SourceInstanceKey']==receipt['SourceInstanceKey']==c['sourceInstanceKey'] and batch['AnatomyFingerprint']==receipt['AnatomyFingerprint']==initial['anatomyFingerprint']
            assert batch['CompleteThroughNeuralMs']==through and p['completeThroughBodyMs']==(previous if source==0 else through)-1000
            chosen=[e for e in events if previous<e['firedAtNeuralMs']<=through] if source==0 else []
            assert len(batch['Spikes'])==len(chosen)
            for spike,e in zip(batch['Spikes'],chosen,strict=True):
                for field in ('MessageId','Ordinal','SourceNeuronId','ProjectionId','SourceSynapseId','FiredAtNeuralMs'):assert spike[field]==e[field[0].lower()+field[1:]]
                assert spike['Neurotransmitter']==5 and not spike['IsSyntheticBackground'] and not spike['IsFeedback'];seen.append(e)
            if retry and (sequence%7==0 or sequence==len(boundaries)):
                assert next(packets)==dict(p,action='retry');retries+=1
        previous=through
    assert next(packets,None) is None and seen==events and final['published']==len(boundaries) and final['waiting']==2*len(boundaries)
    assert final['blocked']==final['retries']==retries
    assert final['motorClock']==initial['motorClock']==dict(sessionId=session,neuralEpochMs=1000,bodyEpochMs=0)
    arrivals=[dict(sessionId=session,sourceInstanceKey=e['sourceInstanceKey'],messageId=e['messageId'],ordinal=e['ordinal'],sourceNeuronId=e['sourceNeuronId'],
        projectionId=e['projectionId'],sourceSynapseId=e['sourceSynapseId'],contactId=contact['contactId'],motorUnitId=contact['motorUnitId'],
        firedAtNeuralMs=e['firedAtNeuralMs'],arrivalAtBodyMs=e['firedAtNeuralMs']-1000+contact['conductionDelayMs']) for e in events if e['firedAtNeuralMs']-1000+contact['conductionDelayMs']<=STEPS*ms]
    return arrivals,boundaries,dict(selectedEvents=selected,motorEvents=len(events),received=len(arrivals),sourceFrames=12000,motorRows=15001)


def native_audit(package):
    from rubral_native_gate import load_reference
    base,b=load_reference(package)
    config=read(package/'configuration.json')
    capture=Path(config.get('NativeCapture',str(package)))
    if capture != package:
        from rubral_native_continuation import require_review
        require_review(package)
        controls=read(package/'loader-controls.json')
        assert controls['completed'] and controls['repairedProductionLoaderAccepted'] and controls['historicalLoaderReproducedFailure']
        assert not controls['modelExecuted'] and not controls['historicalRuntimeEquivalenceEstablished']
    root=capture/'native-bridge';protocol=read(root/'protocol.json');result=read(root/'results.json')
    assert protocol['version']=='rubral-neuronal-native-hinge-v1' and protocol['circuitVersion']=='owned-rubral-motor-circuit-v1'
    assert protocol['steps']==STEPS and protocol['physicsResolution']==1 and protocol['muscleSubsteps']==2
    assert protocol['neuralEpochMs']==1000 and protocol['physicalEpochMs']==0 and protocol['sourceFrames']==12000 and protocol['motorSamples']==15000 and protocol['warmFrames']==8000
    assert [(c['key'],c['referenceKey'],c['quanta'],c['disconnected'],c['activeMuscle'],c['packetFrames'],c['retry']) for c in protocol['conditions']]==CONDITIONS
    assert result['completed'] and result['sourceBodyAccountingPassed'] and result['observedNeuronalMuscleMovement'] and result['actualPhysicsSteps']==7*STEPS
    assert [r['condition'] for r in result['cases']]==[c[0] for c in CONDITIONS]
    assert not protocol['oldRecordsDriveModel'] and not protocol['liveBrainAccessed'] and not protocol['sensoryFeedbackImplemented'] and not protocol['physiologyQualified'] and not protocol['avatarRecoveryAssessed']
    wire=b.audit_wire(capture);spec=deepcopy(read(package/'reference/qualified-hinge-input.json'));spec['cases']=[]
    data={};reports=[];h=b.H*.5;ms=h*1000;geometry_text=None
    assert STEPS*ms==protocol['durationMs']<500<(STEPS+1)*ms
    assert protocol['minimumStrongResponseRadians']==1e-7 and protocol['quietAndMirrorBoundRadians']==1e-5
    for condition in CONDITIONS:
        key=condition[0];folder=root/key;initial=read(folder/'initial.json');final=read(folder/'final.json');clock=initial['clock']
        assert initial['scenario']==key and initial['actualSteps']==STEPS and initial['muscleSubsteps']==2 and initial['neuronalModelExecuted'] and not initial['syntheticMotorEvents']
        assert clock['sessionId']==initial['session'] and clock['initialPhysicsStep']==0 and clock['physicalEpochMs']==0 and clock['neuralEpochMs']==1000 and clock['physicsStepMs']==ms
        descriptor=json.loads(initial['native']['geometryJson']);original=deepcopy(spec['nativeGeometry']);original['stepSeconds']=h;assert descriptor==original
        if geometry_text is None:geometry_text=initial['native']['geometryJson']
        assert initial['native']['geometryJson']==geometry_text
        for i,d in enumerate(initial['definitions']):
            assert d['muscleId']=='m'+str(i) and d['geometry']==spec['geometry'] and d['initialFiberLengthMeters']==spec['initialFiberLengthMeters'] and d['initialFiberVelocityMetersPerSecond']==0 and len(d['units'])==1
            unit=d['units'][0];assert unit['contact']==initial['activeContact' if i==condition[4] else 'quietContact'] and unit['contact']['conductionDelayMs']==2.0625
            assert all(unit[k]==spec['parameterRows'][0][k] for k in ('calcium','lengthGainAboveOptimal','lengthGainBelowOptimal'))
        expected,boundaries,source_report=source_and_packets(package,folder,condition,initial,final,ms)
        case=dict(key=key,durationMs=STEPS*ms,physicalStepMs=ms,physicalSteps=STEPS,initialAngleRadians=0,initialSpeedRadiansPerSecond=0,
            events=[dict(muscleIndex=condition[4],arrivalAtMs=a['arrivalAtBodyMs'],originalMessageId=a['messageId'],originalSourceNeuronId=a['sourceNeuronId'],originalContactId=a['contactId']) for a in expected])
        spec['cases'].append(case);data[key]=base.candidate_data(folder,case,allowed_steps=(STEPS,))
        bridge=folder/'bridge';previous=read(bridge/'hello.json');before=b.state(previous,ms)
        assert previous['operation']=='hello' and previous['sequence']==previous['physicsStep']==0 and before['angle']==before['speed']==0
        requests=sorted(bridge.glob('request-*.json'));replies=sorted(bridge.glob('reply-*.json'));assert len(requests)==len(replies)
        receipts={};efforts=set();max_impulse=max_angle=0.
        for sequence,path in enumerate(replies,1):
            assert path.name==f'reply-{sequence:06d}.json';command=read(bridge/f'request-{sequence:06d}.json');reply=read(path);after=b.state(reply,ms)
            assert command['sequence']==reply['sequence']==sequence and command['version']==reply['version']==2
            assert command['sessionId']==reply['sessionId']==previous['sessionId'] and command['bodyFingerprint']==reply['bodyFingerprint']==previous['bodyFingerprint']==initial['bodyFingerprint']
            assert reply['geometryJson']==geometry_text and command['stateHash']==previous['stateHash'] and command['expectedStep']==previous['physicsStep'] and command['startTimeMs']==previous['physicalTimeMs']
            assert command['operation']==reply['operation'] and command['plantFingerprint']==reply['plantFingerprint']==initial['plantFingerprint']
            if command['operation']=='step':
                tick=previous['physicsStep']+1;assert reply['physicsStep']==tick and command['endTimeMs']==reply['physicalTimeMs']==tick*ms
                assert command['effortId']==reply['lastAppliedEffortId'] and command['effortId'] not in efforts;efforts.add(command['effortId'])
                torque=0.;assert [t['muscleId'] for t in command['tensions']]==['m0','m1']
                for i,t in enumerate(command['tensions']):
                    assert math.isfinite(t['axialNewtons']) and t['axialNewtons']>=0
                    root_point,child=before['m'+str(i)];delta=b.sub(b.vec(child['point']),b.vec(root_point['point']));length=b.norm(delta)
                    force=tuple(b.f32(-v/length*t['axialNewtons']) for v in delta);point=b.vec(child['point']);torque+=point[1]*force[2]-point[2]*force[1]
                acceleration=torque/(b.f32(.15)+2*b.f32(.3)**2)
                impulse=abs(after['speed']-(before['speed']+acceleration*h));angle=abs(after['angle']-(before['angle']+before['speed']*h+.5*acceleration*h*h))
                assert impulse<=2e-6 and angle<=2e-6+.5*abs(acceleration)*h*h
                max_impulse=max(max_impulse,impulse);max_angle=max(max_angle,angle);receipts[tick]=(command,reply)
            else:
                assert command['operation'] in ('bind','inspect','stop') and not command['tensions'] and command['effortId']==''
                assert command['endTimeMs']==reply['physicalTimeMs']==previous['physicalTimeMs'] and reply['stateHash']==previous['stateHash']
                assert reply['stateJson']==previous['stateJson'] and reply['paths']==previous['paths'] and reply['lastAppliedEffortId']==previous['lastAppliedEffortId']
            previous=reply;before=after
        assert len(receipts)==STEPS and previous['operation']=='stop' and previous['physicsStep']==STEPS and previous['stateHash']==final['native']['stateHash']
        observed=[];previous_muscles=initial['muscles']
        for tick,line in enumerate((folder/'steps.jsonl').read_text().splitlines(),1):
            row=decode(json.loads(line));step=row['step'];command,reply=receipts[tick]
            # CaptureGeometry adds a read-only inspect after the physical reply.
            # Its operation/sequence differ; the actual state and paths must not.
            assert row['native']['operation']=='inspect' and row['native']['sequence']>reply['sequence']
            assert row['native']['physicsStep']==tick and row['native']['stateHash']==reply['stateHash'] and row['native']['paths']==reply['paths']
            assert step['effort']['effortId'].replace('-','')==command['effortId']
            assert step['effort']['tensions']==command['tensions']==[dict(muscleId='m'+str(i),axialNewtons=previous_muscles['m'+str(i)]['forces']['tendonAxialForceNewtons']) for i in range(2)]
            assert step['effort']['startTimeMs']==(tick-1)*ms and step['effort']['endTimeMs']==tick*ms
            assert row['sourceCompleteThroughBodyMs']>=tick*ms and row['sourceCompleteThroughBodyMs'] in [t-1000 for t in boundaries]
            assert all((tick-1)*ms<a['arrivalAtBodyMs']<=tick*ms for a in step['arrivals'])
            observed.extend(step['arrivals']);previous_muscles=step['muscles']
        assert observed==expected and final['arrivals']==len(expected) and final['plant']['deliveredEvents']==len(expected)
        assert final['plant']['acceptedEvents']==source_report['motorEvents'] and final['plant']['pendingEvents']==source_report['motorEvents']-len(expected) and not final['plant']['running'] and not final['plant']['faulted']
        reports.append(dict(case=key,**source_report,maximumStepImpulseError=max_impulse,maximumStepAngleError=max_angle))
    spec['nativeGeometry']=json.loads(geometry_text);spec['nativeGeometryJson']=geometry_text
    for key in ('strong-fast','strong-retry'):
        for field in ('timeMs','forces','path','release','arrivals'):assert np.array_equal(data[key][field],data['strong-left'][field]),(key,field)
        assert np.array_equal(data[key]['state'][:,base.OBSERVED],data['strong-left']['state'][:,base.OBSERVED])
    angles={key:float(value['state'][-1,0]) for key,value in data.items()}
    assert abs(angles['quiet'])<=1e-5 and abs(angles['strong-cut'])<=1e-5 and angles['strong-left']>1e-7 and angles['strong-right']< -1e-7 and abs(angles['strong-left']+angles['strong-right'])<=1e-5
    control=read(root/'quiet/bridge/host-reply-lock-control.json');assert control['injectedReadRejections']==2 and control['physicsStep']==1 and not control['commandResent'] and not control['physicalStepRepeated']
    return base,spec,data,dict(completed=True,sourceBodyAccountingPassed=True,observedNeuronalMuscleMovement=True,cases=reports,wire=wire,
        actualPhysicsSteps=7*STEPS,packetAndRetryStateBitwiseEqual=True,neuronalModelExecuted=True,nativePhysicsExecuted=True,
        coupledTrajectoryAccuracyQualified=False,physiologyQualified=False,avatarRecoveryAssessed=False)


def solve(package):
    from rubral_native_gate import check_reference_controls
    check_reference_controls(package)
    base,spec,data,evidence=native_audit(package);save(package/'rubral-hinge-input.json',spec);save(package/'native-source-analysis.json',evidence)
    output=package/'rubral-reference';output.mkdir();execution=[];geometry=HingeGeometry(spec['nativeGeometry'])
    for case in spec['cases'][:5]:
        grid=data[case['key']]['timeMs']/1000
        for settings in spec['referenceSettings']:
            key=case['key']+'-'+settings['key'];print('RUBRAL MOTOR HINGE REFERENCE',key,flush=True);model=HingeCoupledReference(spec,case,geometry)
            try:
                y,forces,stats=model.solve(settings,grid);np.savez_compressed(output/(key+'.npz'),timeSeconds=grid,state=y,forces=forces);stats=dict(completed=True,**stats)
            except Exception:
                np.savez_compressed(output/(key+'-partial.npz'),**getattr(model,'partial',{}));stats=dict(completed=False,error=traceback.format_exc());print(stats['error'],flush=True)
            save(output/(key+'-statistics.json'),stats);execution.append(dict(key=key,**stats))
    save(output/'execution.json',dict(completed=True,references=execution))


def audit(package):
    from rubral_native_gate import check_reference_controls
    check_reference_controls(package)
    base,spec,data,evidence=native_audit(package)
    assert spec==read(package/'rubral-hinge-input.json') and evidence==read(package/'native-source-analysis.json')
    geometry=HingeGeometry(spec['nativeGeometry']);references={};execution=read(package/'rubral-reference/execution.json')
    assert execution['completed'] and len(execution['references'])==15
    for case in spec['cases'][:5]:
        key=case['key'];refs={}
        for settings in spec['referenceSettings']:
            name=key+'-'+settings['key'];stats=read(package/'rubral-reference'/(name+'-statistics.json'))
            assert stats=={k:v for k,v in next(r for r in execution['references'] if r['key']==name).items() if k!='key'}
            assert stats['completed'] and stats['acceptedInternalDomainPassed'] and stats['sampleDomainPassed']
            with np.load(package/'rubral-reference'/(name+'.npz')) as f:y,forces,grid=f['state'],f['forces'],f['timeSeconds']
            assert y.shape==(STEPS+1,28) and forces.shape==(STEPS+1,2,7) and np.array_equal(grid,data[key]['timeMs']/1000)
            assert base.accounting(spec,case,y,geometry.inertia)['passed'] and base.reference_energy(spec,geometry,case,y)['passed']
            for ry,rf in refs.values():assert base.within(base.errors(y,ry,forces,rf),spec['referenceBounds'])
            refs[settings['key']]=(y,forces)
        references[key]=refs
    rows=[]
    for case in spec['cases']:
        key=case['key'];candidate=data[key];model=HingeCoupledReference(spec,case,geometry);y=candidate['state'];f=candidate['forces'];cosine=np.cos(spec['geometry']['pennationRadians'])
        domain=all(m.domain(y[:,sl]) and np.min(candidate['path'][:,i]-cosine*y[:,sl.start])>0 for i,(m,sl) in enumerate(zip(model.muscles,model.slices)))
        release=max(float(np.max(abs(np.array([m.release(t/1000,0) for t in candidate['timeMs']])-candidate['release'][:,i]))) for i,m in enumerate(model.muscles))
        path=float(np.max(abs(geometry.measure(y[:,0])['length']-candidate['path'])))
        assert domain and release<=spec['releaseWaveformAbsolute'] and path<=spec['pathGeometryMeters'] and base.accounting(spec,case,y)['passed']
        assert all(candidate['recordedLedgers'][k]<=spec[v] for k,v in [('calciumLedger','calciumLedgerAbsolute'),('fiberMomentumLedger','fiberMomentumLedgerNewtonSeconds'),('fiberKineticLedger','fiberKineticLedgerJoules')])
        refs=references['strong-left' if key in ('strong-fast','strong-retry') else key]
        comparisons=[dict(reference=name,errors=base.errors(y,ry,f,rf)) for name,(ry,rf) in refs.items()]
        for comparison in comparisons:
            comparison['failedMetrics']={metric:dict(error=error,bound=spec['candidateBounds'][metric])
                for metric,error in comparison['errors'].items() if error>spec['candidateBounds'][metric]}
            if comparison['failedMetrics']:
                print('OUTSIDE ORIGINAL BOUNDS '+key+' '+comparison['reference']+' '+json.dumps(comparison['failedMetrics']),flush=True)
        rows.append(dict(case=key,comparisons=comparisons,passed=all(base.within(r['errors'],spec['candidateBounds']) for r in comparisons)))
    result=dict(completed=True,numericalCriteriaPassed=all(r['passed'] for r in rows),sampledSingleResolutionOnly=True,cases=rows,
        independentReferences=15,allNativeEndpointsCompared=True,physicalStepRefinementForThisInputAssessed=False,
        originalBoundsPreserved=True,upstreamNumericalAccuracyQualified=False,physiologyQualified=False,avatarRecoveryAssessed=False)
    save(package/'analysis.json',result)
    if not result['numericalCriteriaPassed']:raise RuntimeError('Rubral/native trajectory criteria failed; retain original bounds and evidence.')


if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    parser=argparse.ArgumentParser();parser.add_argument('command',choices=['solve','analyse']);parser.add_argument('package',type=Path);args=parser.parse_args()
    (solve if args.command=='solve' else audit)(args.package.resolve())

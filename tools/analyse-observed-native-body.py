"""Observed neuronal/native-body audit; new reference solves only in visible PowerShell."""
import argparse
from copy import deepcopy
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

CONDITIONS=[('quiet',False,False,100,False),('left',True,False,100,False),('right',False,True,100,False),
            ('balanced',True,True,100,False),('left-fast',True,False,10,False),('left-slow',True,False,500,False),('left-retry',True,False,100,True)]


def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m


def save(path,value):
    with path.open('x') as f:json.dump(value,f,indent=2,allow_nan=False)


def stable(*parts):
    text=''.join(str(len(str(p)))+':'+str(p) for p in parts)
    return str(uuid.UUID(bytes_le=hashlib.sha256(text.encode()).digest()[:16]))


def load(package):
    base=module('hinge_comparison',package/'source/tools/analyse-native-hinge-comparison.py')
    bridge=module('native_mechanics',package/'source/tools/analyse-native-motor-bridge.py')
    assert base.environment(package)==read(package/'preparation-environment.json')==read(package/'reference/qualified-environment.json')
    review=read(package/'reference/hinge-refinement-review.json')
    assert review['operatorReportedCompletion'] and review['savedAnalysisExactlyReproduced'] and review['numericalCriteriaPassed']
    return base,bridge


def source_and_packets(package,folder,condition,protocol,initial,final,ms):
    key,left,right,samples,retry=condition;identities=protocol['identities'];contacts=protocol['contacts']
    baseline=read(package/'reference/source-results.json');expected_events=[];emissions=[]
    for i,stimulated in enumerate((left,right)):
        source=final['sources'][i];primed=initial['primedSources'][i];name='soma-6000' if stimulated else 'quiet'
        prior=next(c for c in baseline['cases'] if c['key']==name+'-h0.003125')
        assert source['source']==name and source['timeMs']==11281*.1 and source['completedSamples']==11281 and not source['faulted']
        assert primed['timeMs']==1000 and primed['completedSamples']==10000 and not primed['crossingsMs'] and not primed['emissions']
        assert int(source['suppliedDurationUnits'])==int(source['timeMs']*2**80) and int(primed['suppliedDurationUnits'])==1000*2**80
        assert source['integrationCalls']>primed['integrationCalls']>10000
        with (folder/f'neuron-{i}.csv').open('rb') as actual,(package/'reference'/(name+'-h0.003125.csv')).open('rb') as reference:
            count=0;last=None
            for count,line in enumerate(actual,1):
                assert line==reference.readline(),(key,i,count)
                if count==10002:assert list(map(float,line.strip().split(b',')))==[primed['timeMs']]+primed['state']
                last=line
            assert count==11283 and list(map(float,last.strip().split(b',')))==[source['timeMs']]+source['state']
        times=[t for t in prior['crossingsMs'] if t<=source['timeMs']]
        assert source['crossingsMs']==times and len(times)==(2 if stimulated else 0)
        identity=identities[i];contact=contacts[i];original=[]
        for ordinal,t in enumerate(times,1):
            event=dict(ordinal=ordinal,messageId=stable('isolated-observed-motor-source-v1',protocol['evidence'],name,
                identity['instance'],identity['neuron'],identity['projection'],identity['synapse'],ordinal),firedAtNeuralMs=t,
                sourceInstanceKey=identity['instance'],sourceNeuronId=identity['neuron'],projectionId=identity['projection'],sourceSynapseId=identity['synapse'])
            original.append(event)
            if t-1000+2.0625<=2048*ms:
                expected_events.append(dict(sessionId=initial['clock']['sessionId'],sourceInstanceKey=identity['instance'],messageId=event['messageId'],
                    ordinal=ordinal,sourceNeuronId=identity['neuron'],projectionId=identity['projection'],sourceSynapseId=identity['synapse'],
                    contactId=contact['contactId'],motorUnitId=contact['motorUnitId'],firedAtNeuralMs=t,arrivalAtBodyMs=t-1000+2.0625))
        assert original==source['emissions'];emissions.append(original)
    packets=[read_line(line) for line in (folder/'packets.jsonl').read_text().splitlines()]
    boundaries=[s*.1 for s in range(10001,11282) if (s-10000)%samples==0 or s==11281]
    seen=[[],[]];cursor=0;previous=1000.;retry_count=0
    for sequence,through in enumerate(boundaries,1):
        body_step=min(2048,int(np.count_nonzero(np.arange(2049)*ms<=previous-1000)-1))
        for i in range(2):
            p=packets[cursor];cursor+=1;batch=p['batch'];receipt=p['receipt']
            assert p['action']=='accept' and p['sourceIndex']==i and p['bodyStepAtReceipt']==body_step
            assert batch['sequence']==receipt['sequence']==sequence and batch['sessionId']==receipt['sessionId']==initial['clock']['sessionId']
            assert batch['sourceInstanceKey']==receipt['sourceInstanceKey']==identities[i]['instance']
            assert batch['anatomyFingerprint']==receipt['anatomyFingerprint'] and batch['completeThroughNeuralMs']==through
            assert p['completeThroughBodyMs']==(previous if i==0 else through)-1000
            expected=[e for e in emissions[i] if previous<e['firedAtNeuralMs']<=through]
            assert len(expected)==len(batch['spikes'])
            for actual,e in zip(batch['spikes'],expected):
                assert all(actual[k]==e[k] for k in ('messageId','ordinal','sourceNeuronId','projectionId','sourceSynapseId','firedAtNeuralMs'))
                assert actual['neurotransmitter']==5 and not actual['isSyntheticBackground'] and not actual['isFeedback']
                seen[i].append(e)
            if retry and sequence%7==0:
                repeated=packets[cursor];cursor+=1;retry_count+=1
                assert repeated==dict(p,action='retry')
        previous=through
    assert cursor==len(packets) and seen==emissions and final['published']==len(boundaries)
    assert final['waiting']==2*len(boundaries) and final['blockedSource']==final['retries']==retry_count
    assert final['sourcePrefixExactlyReproduced'] and final['sourcePrefixRows']==11282
    assert final['motorClock']==dict(sessionId=initial['clock']['sessionId'],neuralEpochMs=1000,bodyEpochMs=0)
    return sorted(expected_events,key=lambda a:(a['arrivalAtBodyMs'],a['sourceInstanceKey'],a['ordinal'])),boundaries


def read_line(line):return decode(json.loads(line))


def native_audit(package):
    base,b=load(package);folder=package/'native-bridge';protocol=read(folder/'protocol.json');result=read(folder/'results.json')
    assert protocol['version']=='observed-neuronal-native-hinge-v1' and protocol['neuralEpochMs']==1000 and protocol['physicalEpochMs']==0
    assert protocol['physicsResolution']==1 and protocol['muscleSubsteps']==2 and protocol['steps']==2048 and protocol['finalSourceSample']==11281
    assert [(c['key'],c['left'],c['right'],c['packetSamples'],c['retry']) for c in protocol['conditions']]==CONDITIONS
    assert result['completed'] and result['sourceBodyCriteriaPassed'] and result['actualPhysicsSteps']==14336
    assert [r['condition'] for r in result['cases']]==[c[0] for c in CONDITIONS] and all(r['passed'] for r in result['cases'])
    wire=b.audit_wire(package);spec=read(package/'reference/qualified-hinge-input.json');spec=deepcopy(spec);spec['cases']=[]
    geometry_text=None;data={};rows=[];h=b.H*.5;ms=h*1000
    for condition in CONDITIONS:
        key=condition[0];case_folder=folder/key;initial=read(case_folder/'initial.json');final=read(case_folder/'final.json')
        assert initial['scenario']==key and initial['actualSteps']==2048 and initial['muscleSubsteps']==2 and initial['neuronalModelExecuted'] and not initial['syntheticMotorEvents']
        clock=initial['clock'];assert clock['initialPhysicsStep']==0 and clock['physicalEpochMs']==0 and clock['neuralEpochMs']==1000 and clock['physicsStepMs']==ms
        descriptor=json.loads(initial['native']['geometryJson']);old=deepcopy(spec['nativeGeometry']);old['stepSeconds']=h
        assert descriptor==old
        if geometry_text is None:geometry_text=initial['native']['geometryJson']
        assert geometry_text==initial['native']['geometryJson']
        for i,d in enumerate(initial['definitions']):
            assert d['muscleId']=='m'+str(i) and d['geometry']==spec['geometry'] and d['initialFiberLengthMeters']==spec['initialFiberLengthMeters']
            assert d['initialFiberVelocityMetersPerSecond']==0 and len(d['units'])==1
            unit=d['units'][0];assert unit['contact']==protocol['contacts'][i] and unit['contact']['conductionDelayMs']==2.0625
            assert all(unit[k]==spec['parameterRows'][0][k] for k in ('calcium','lengthGainAboveOptimal','lengthGainBelowOptimal'))
        expected,boundaries=source_and_packets(package,case_folder,condition,protocol,initial,final,ms)
        case=dict(key=key,durationMs=2048*ms,physicalStepMs=ms,physicalSteps=2048,initialAngleRadians=0,initialSpeedRadiansPerSecond=0,
            events=[dict(muscleIndex=next(i for i,c in enumerate(protocol['contacts']) if c['contactId']==a['contactId']),arrivalAtMs=a['arrivalAtBodyMs'],
                originalMessageId=a['messageId'],originalSourceNeuronId=a['sourceNeuronId'],originalContactId=a['contactId']) for a in expected])
        spec['cases'].append(case);data[key]=base.candidate_data(case_folder,case)
        bridge=case_folder/'bridge';previous=read(bridge/'hello.json');before=b.state(previous,ms)
        assert previous['operation']=='hello' and previous['sequence']==previous['physicsStep']==0 and before['angle']==before['speed']==0
        requests=sorted(bridge.glob('request-*.json'));replies=sorted(bridge.glob('reply-*.json'));assert len(requests)==len(replies)
        receipts={};efforts=set();max_impulse=0.;max_angle=0.
        for seq,path in enumerate(replies,1):
            assert path.name==f'reply-{seq:06d}.json'
            command=read(bridge/f'request-{seq:06d}.json');reply=read(path);after=b.state(reply,ms)
            assert command['sequence']==reply['sequence']==seq and command['version']==reply['version']==2
            assert command['sessionId']==reply['sessionId']==previous['sessionId'] and command['bodyFingerprint']==reply['bodyFingerprint']==previous['bodyFingerprint']
            assert reply['geometryJson']==geometry_text and command['stateHash']==previous['stateHash'] and command['expectedStep']==previous['physicsStep']
            assert command['startTimeMs']==previous['physicalTimeMs'] and command['operation']==reply['operation']
            assert command['plantFingerprint']==reply['plantFingerprint']==initial['plantFingerprint']
            if command['operation']=='step':
                tick=previous['physicsStep']+1;assert reply['physicsStep']==tick and command['endTimeMs']==reply['physicalTimeMs']==tick*ms
                assert command['effortId']==reply['lastAppliedEffortId'] and command['effortId'] not in efforts;efforts.add(command['effortId'])
                torque=0.;assert [t['muscleId'] for t in command['tensions']]==['m0','m1']
                for i,t in enumerate(command['tensions']):
                    assert math.isfinite(t['axialNewtons']) and t['axialNewtons']>=0
                    root,child=before['m'+str(i)];delta=b.sub(b.vec(child['point']),b.vec(root['point']));length=b.norm(delta)
                    force=tuple(b.f32(-v/length*t['axialNewtons']) for v in delta);point=b.vec(child['point']);torque+=point[1]*force[2]-point[2]*force[1]
                acceleration=torque/(b.f32(.15)+2*b.f32(.3)**2)
                impulse=abs(after['speed']-(before['speed']+acceleration*h));angle=abs(after['angle']-(before['angle']+before['speed']*h+.5*acceleration*h*h))
                assert impulse<=2e-6 and angle<=2e-6+.5*abs(acceleration)*h*h
                max_impulse=max(max_impulse,impulse);max_angle=max(max_angle,angle);receipts[tick]=(command,reply)
            else:
                assert command['operation'] in ('bind','inspect','stop') and not command['tensions'] and command['effortId']==''
                assert command['endTimeMs']==command['startTimeMs'] and reply['physicsStep']==previous['physicsStep']
                assert reply['stateJson']==previous['stateJson'] and reply['paths']==previous['paths'] and reply['lastAppliedEffortId']==previous['lastAppliedEffortId']
            previous,before=reply,after
        assert len(receipts)==2048 and previous['operation']=='stop' and previous['stateHash']==final['native']['stateHash']
        observed=[]
        for tick,line in enumerate((case_folder/'steps.jsonl').read_text().splitlines(),1):
            entry=read_line(line);command,reply=receipts[tick];step=entry['step']
            assert entry['native']['stateHash']==reply['stateHash'] and entry['native']['paths']==reply['paths']
            assert step['effort']['effortId'].replace('-','')==command['effortId'] and step['effort']['tensions']==command['tensions']
            assert entry['sourceCompleteThroughBodyMs']>=tick*ms and entry['sourceCompleteThroughBodyMs'] in [t-1000 for t in boundaries]
            for a in step['arrivals']:assert (tick-1)*ms<a['arrivalAtBodyMs']<=tick*ms
            observed.extend(step['arrivals'])
        assert observed==expected and final['arrivals']==len(expected) and final['plant']['deliveredEvents']==len(expected)
        assert final['plant']['acceptedEvents']==len(expected) and final['plant']['pendingEvents']==0 and not final['plant']['running'] and not final['plant']['faulted']
        rows.append(dict(case=key,observedCrossingsDelivered=len(expected),maximumStepImpulseError= max_impulse,maximumStepAngleError=max_angle))
    spec['nativeGeometry']=json.loads(geometry_text);spec['nativeGeometryJson']=geometry_text
    for key in ('left-fast','left-slow','left-retry'):
        for field in ('timeMs','forces','path','release','arrivals'):assert np.array_equal(data[key][field],data['left'][field]),(key,field)
        assert np.array_equal(data[key]['state'][:,base.OBSERVED],data['left']['state'][:,base.OBSERVED])
    angles={key:float(value['state'][-1,0]) for key,value in data.items()}
    assert abs(angles['quiet'])<=1e-5 and abs(angles['balanced'])<=1e-5 and angles['left']>1e-7 and angles['right']< -1e-7 and abs(angles['left']+angles['right'])<=1e-5
    control=read(folder/'quiet/bridge/host-reply-lock-control.json');assert control['injectedReadRejections']==2 and control['physicsStep']==1 and not control['commandResent'] and not control['physicalStepRepeated']
    return base,spec,data,dict(completed=True,sourceBodyCriteriaPassed=True,wire=wire,cases=rows,actualPhysicsSteps=14336,
        packetAndRetryStateBitwiseEqual=True,sourceTracePrefixesExactlyReproduced=True,neuronalModelExecuted=True,
        nativePhysicsExecuted=True,physiologyQualified=False,avatarRecoveryAssessed=False)


def solve(package):
    base,spec,data,evidence=native_audit(package);save(package/'neuronal-hinge-input.json',spec);save(package/'native-source-analysis.json',evidence)
    output=package/'neuronal-reference';output.mkdir();records=[];geometry=HingeGeometry(spec['nativeGeometry'])
    for case in spec['cases'][:4]:
        grid=data[case['key']]['timeMs']/1000
        for settings in spec['referenceSettings']:
            key=case['key']+'-'+settings['key'];print('OBSERVED MOTOR HINGE REFERENCE',key,flush=True);model=HingeCoupledReference(spec,case,geometry)
            try:
                y,forces,stats=model.solve(settings,grid);np.savez_compressed(output/(key+'.npz'),timeSeconds=grid,state=y,forces=forces);stats=dict(completed=True,**stats)
            except Exception:
                np.savez_compressed(output/(key+'-partial.npz'),**getattr(model,'partial',{}));stats=dict(completed=False,error=traceback.format_exc());print(stats['error'],flush=True)
            save(output/(key+'-statistics.json'),stats);records.append(dict(key=key,**stats))
    save(output/'execution.json',dict(completed=True,references=records))


def audit(package):
    base,spec,data,evidence=native_audit(package);assert spec==read(package/'neuronal-hinge-input.json') and evidence==read(package/'native-source-analysis.json')
    geometry=HingeGeometry(spec['nativeGeometry']);rows=[];references={};executed=read(package/'neuronal-reference/execution.json')
    assert executed['completed'] and len(executed['references'])==12
    for case in spec['cases'][:4]:
        key=case['key'];refs={}
        for settings in spec['referenceSettings']:
            name=key+'-'+settings['key'];stats=read(package/'neuronal-reference'/(name+'-statistics.json'))
            assert stats=={k:v for k,v in next(r for r in executed['references'] if r['key']==name).items() if k!='key'}
            assert stats['completed'] and stats['acceptedInternalDomainPassed'] and stats['sampleDomainPassed']
            with np.load(package/'neuronal-reference'/(name+'.npz')) as f:y,forces,grid=f['state'],f['forces'],f['timeSeconds']
            assert y.shape==(2049,28) and forces.shape==(2049,2,7) and np.array_equal(grid,data[key]['timeMs']/1000)
            assert base.accounting(spec,case,y,geometry.inertia)['passed'] and base.reference_energy(spec,geometry,case,y)['passed']
            for ry,rf in refs.values():assert base.within(base.errors(y,ry,forces,rf),spec['referenceBounds'])
            refs[settings['key']]=(y,forces)
        references[key]=refs
    for case in spec['cases']:
        key=case['key'];candidate=data[key];model=HingeCoupledReference(spec,case,geometry);y=candidate['state'];f=candidate['forces']
        cosine=np.cos(spec['geometry']['pennationRadians'])
        domain=all(m.domain(y[:,sl]) and np.min(candidate['path'][:,i]-cosine*y[:,sl.start])>0 for i,(m,sl) in enumerate(zip(model.muscles,model.slices)))
        release=max(float(np.max(abs(np.array([m.release(t/1000,0) for t in candidate['timeMs']])-candidate['release'][:,i]))) for i,m in enumerate(model.muscles))
        path=float(np.max(abs(geometry.measure(y[:,0])['length']-candidate['path'])))
        assert domain and release<=spec['releaseWaveformAbsolute'] and path<=spec['pathGeometryMeters'] and base.accounting(spec,case,y)['passed']
        assert all(candidate['recordedLedgers'][k]<=spec[v] for k,v in [('calciumLedger','calciumLedgerAbsolute'),('fiberMomentumLedger','fiberMomentumLedgerNewtonSeconds'),('fiberKineticLedger','fiberKineticLedgerJoules')])
        comparisons=[dict(reference=name,errors=base.errors(y,ry,f,rf)) for name,(ry,rf) in references['left' if key.startswith('left') else key].items()]
        rows.append(dict(case=key,comparisons=comparisons,passed=all(base.within(r['errors'],spec['candidateBounds']) for r in comparisons)))
    return dict(completed=True,numericalCriteriaPassed=all(r['passed'] for r in rows),cases=rows,nativeSourceEvidence=evidence,
        independentReferences=12,allNativeEndpointsCompared=True,physicalStepRefinementForThisInputAssessed=False,
        neuronalSourcePhysiologyQualified=False,liveMotorAuthorityQualified=False,physiologyQualified=False,avatarRecoveryAssessed=False)


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('package',type=Path);parser.add_argument('--stage',choices=('preflight','solve','audit'),required=True);a=parser.parse_args();p=a.package.resolve()
    if a.stage=='preflight':load(p);print('Qualified reference environment verified; no integration.',flush=True)
    elif a.stage=='solve':solve(p)
    else:
        report=audit(p);save(p/'neuronal-body-analysis.json',report);print(json.dumps({k:v for k,v in report.items() if k not in ('cases','nativeSourceEvidence')},indent=2),flush=True)
        if not report['numericalCriteriaPassed']:raise RuntimeError('Observed neuronal/native trajectory criteria failed; preserve original bounds.')

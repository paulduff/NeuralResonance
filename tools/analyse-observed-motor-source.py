"""Audit saved source traces and exact motor deliveries; no model execution."""
import argparse
import hashlib
import json
from pathlib import Path

read=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))


def digest(path):
    h=hashlib.sha256()
    with path.open('rb') as f:
        for block in iter(lambda:f.read(4*1024*1024),b''):h.update(block)
    return h.hexdigest()


def audit(package):
    study=package/'source-study';protocol=read(study/'protocol.json');results=read(study/'results.json')
    baseline=read(package/'reference/source-results.json')
    assert results['completed'] and results['sourceAdapterCriteriaPassed'] and results['successfulCases']==results['expectedCases']==5
    assert protocol['version']=='isolated-observed-motor-source-v1' and protocol['durationMs']==6000
    assert protocol['sourceSampleMs']==.1 and protocol['sourceMaximumStepMs']==.003125
    assert not results['nativePhysicsExecuted'] and not results['muscleDynamicsExecuted'] and results['neuronalModelExecuted']
    expected=[('quiet-10',False,100,False),('active-1',True,10,False),('active-10',True,100,False),
              ('active-50',True,500,False),('active-retry-10',True,100,True)]
    assert [(c['key'],c['stimulated'],c['packetSamples'],c['retry']) for c in protocol['conditions']]==expected
    assert [c['condition'] for c in results['cases']]==[c[0] for c in expected] and all(c['passed'] for c in results['cases'])
    identity,contact=protocol['identity'],protocol['contact'];rows=[];active=None
    assert contact['conductionDelayMs']==2.0625
    for key,stimulated,samples,retry in expected:
        folder=study/key;final=read(folder/'final.json');source=final['source'];clock=final['clock']
        name=('soma-6000' if stimulated else 'quiet')+'-h0.003125'
        old=next(c for c in baseline['cases'] if c['key']==name)
        assert source['source']==('soma-6000' if stimulated else 'quiet')
        assert old['completed'] and source['timeMs']==6000 and source['completedSamples']==60000 and not source['faulted']
        assert source['integrationCalls']==old['calls'] and source['suppliedDurationUnits']==old['suppliedUnits']
        assert source['crossingsMs']==old['crossingsMs'] and len(source['state'])==30
        assert digest(folder/'source.csv')==digest(package/'reference'/(name+'.csv'))
        emitted=[json.loads(line) for line in (folder/'emissions.jsonl').read_text().splitlines()]
        assert emitted==source['emissions'] and [e['firedAtNeuralMs'] for e in emitted]==old['crossingsMs']
        assert [e['ordinal'] for e in emitted]==list(range(1,len(emitted)+1))
        assert len({e['messageId'] for e in emitted})==len(emitted)==(28 if stimulated else 0)
        for e in emitted:
            assert (e['sourceInstanceKey'],e['sourceNeuronId'],e['projectionId'],e['sourceSynapseId'])==(
                identity['instance'],identity['neuron'],identity['projection'],identity['synapse'])
        if stimulated:
            if active is None:active=emitted
            else:assert active==emitted
        assert clock['neuralEpochMs']==clock['bodyEpochMs']==0
        expected_arrivals=[dict(sessionId=clock['sessionId'],sourceInstanceKey=e['sourceInstanceKey'],messageId=e['messageId'],
            ordinal=e['ordinal'],sourceNeuronId=e['sourceNeuronId'],projectionId=e['projectionId'],sourceSynapseId=e['sourceSynapseId'],
            contactId=contact['contactId'],motorUnitId=contact['motorUnitId'],firedAtNeuralMs=e['firedAtNeuralMs'],
            arrivalAtBodyMs=e['firedAtNeuralMs']+contact['conductionDelayMs']) for e in emitted if e['firedAtNeuralMs']+contact['conductionDelayMs']<=6000]
        arrivals=[json.loads(line) for line in (folder/'arrivals.jsonl').read_text().splitlines()]
        assert arrivals==expected_arrivals and final['delivered']==len(arrivals) and final['pendingEvents']==len(emitted)-len(arrivals)
        packets=[json.loads(line) for line in (folder/'packets.jsonl').read_text().splitlines()]
        accepted=[p for p in packets if p['action']=='accept'];replayed=[p for p in packets if p['action']=='retry']
        count=60000//samples;assert len(accepted)==count and len(replayed)==(count//7 if retry else 0)
        assert len(packets)==len(accepted)+len(replayed) and final['published']==count and final['retried']==final['blocked']==len(replayed)
        received=[];by_sequence={};expected_packet_log=[]
        for i,p in enumerate(accepted,1):
            batch=p['batch'];receipt=p['receipt'];by_sequence[i]=p
            assert batch['sequence']==receipt['sequence']==i and batch['sessionId']==receipt['sessionId']==clock['sessionId']
            assert batch['sourceInstanceKey']==receipt['sourceInstanceKey']==identity['instance']
            assert batch['anatomyFingerprint']==receipt['anatomyFingerprint'] and batch['completeThroughNeuralMs']==i*samples*.1
            for e in batch['spikes']:
                source_event=emitted[e['ordinal']-1]
                assert all(e[field]==source_event[field] for field in ('messageId','ordinal','sourceNeuronId','projectionId','sourceSynapseId','firedAtNeuralMs'))
                assert e['neurotransmitter']==5 and not e['isSyntheticBackground'] and not e['isFeedback']
                assert (i-1)*samples*.1<e['firedAtNeuralMs']<=i*samples*.1
                received.append(source_event)
            expected_packet_log.append(p)
            if retry and i%7==0:expected_packet_log.append(dict(p,action='retry'))
        assert packets==expected_packet_log and received==emitted
        rows.append(dict(condition=key,sourceBytesExactlyReproduced=True,originalEventsPreserved=True,
                         crossings=len(emitted),delivered=len(arrivals),published=count,retried=len(replayed)))
    return dict(completed=True,sourceAdapterCriteriaPassed=True,cases=rows,activeCadenceEventIdentityEqual=True,
        analysisExecutedNeurons=False,analysisExecutedTests=False,nativePhysicsExecuted=False,muscleDynamicsExecuted=False,
        liveMotorAuthorityQualified=False,physiologyQualified=False,avatarRecoveryAssessed=False)


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('package',type=Path);a=parser.parse_args()
    report=audit(a.package.resolve())
    with (a.package/'source-analysis.json').open('x') as f:json.dump(report,f,indent=2,allow_nan=False)
    print(json.dumps(report,indent=2))

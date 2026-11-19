"""Reconcile saved recurrent traces without rerunning a neuron or test."""
import hashlib
import json
from pathlib import Path
from afferent_precision_gate import h,read

ZERO='00000000-0000-0000-0000-000000000000'

def hash_text(value):return hashlib.sha256(value.encode('utf-8')).hexdigest().upper()
def ordered(values):return sorted(values,key=lambda value:json.dumps(value,sort_keys=True,separators=(',',':')))

def decode_normalized(values,waiting=False):
    result=[]
    for value in values:
        row=json.loads(value)
        target=row if waiting else row['Delivery'];key='MessageJson' if waiting else 'OriginalJson'
        target[key]=json.loads(target[key]);result.append(row)
    return ordered(result)

def reconcile(run):
    sent={};delivered=set();waiting=set();normal_deliveries=[];normal_waiting=[];raw_deliveries=[];neuronal=0
    def packet(value,original_events=None):
        assert hash_text(value['CanonicalJson'])==value['Sha256']
        root=json.loads(value['CanonicalJson']);assert len(root['Events'])==value['EventCount']
        for row in root['Events']:
            original=json.loads(row['OriginalEventJson']);routed=json.loads(row['RoutedEventJson'])
            assert original['MessageId']!=ZERO and routed['MessageId']!=ZERO
            if original_events is not None:assert original_events[original['MessageId']]==original
            key=(value['TargetInstance'],routed['MessageId']);assert key not in sent
            sent[key]=row['RoutedEventJson']
    for frame in run['Inputs']:
        for p in frame['Packets']:packet(p)
    frames=[]
    for frame in run['Frames']:
        assert hash_text(frame['CanonicalJson'])==frame['Sha256']
        root=json.loads(frame['CanonicalJson']);frames.append(root)
        for transfer in root['Transfers']:
            original=transfer['Original'];projection=transfer['Projection']
            assert hash_text(original['CanonicalJson'])==original['Sha256']==projection['SourceFrameSha256']
            source=json.loads(original['CanonicalJson']);events={}
            for step_json in source['ReceiverStepJson']:
                for event in json.loads(step_json)['OutboundSpikes']:
                    assert event['MessageId'] not in events;events[event['MessageId']]=event
            neuronal+=len(events);assert len(projection['Packets'])==1 and projection['Packets'][0]['EventCount']==len(events)
            for p in projection['Packets']:packet(p,events)
    for frame in frames:
        for transfer in frame['Transfers']:
            source=json.loads(transfer['Original']['CanonicalJson']);recipient=source['SourceInstance']
            for original in source['ActualDeliveries']:
                key=(recipient,original['OriginalMessageId']);assert key not in delivered and sent[key]==original['OriginalJson']
                assert original['ArrivalAtMs']==original['AppliedAtMs'];delivered.add(key);raw_deliveries.append(original)
                spike=json.loads(original['OriginalJson']);assert spike['MessageId']==original['OriginalMessageId'];spike['MessageId']=ZERO
                normal_deliveries.append(dict(Recipient=recipient,Delivery=dict(original,OriginalMessageId=ZERO,OriginalJson=spike)))
    for name in ('Reticular','Spinal'):
        state=frames[-1][name]['Source'];assert state['Actual']['QueuedEvents']==0
        retained=json.loads(state['RetainedEvidenceJson']);recipient=retained['SourceInstance']
        for row in retained['WaitingEvents']:
            spike=json.loads(row['MessageJson']);key=(recipient,spike['MessageId'])
            assert key not in delivered and key not in waiting and sent[key]==row['MessageJson'];waiting.add(key);spike['MessageId']=ZERO
            normal_waiting.append(dict(Recipient=recipient,MessageJson=spike,Arrival=row['Arrival']))
    assert len(sent)==len(delivered)+len(waiting)
    assert ordered(raw_deliveries)==ordered([json.loads(value) for value in json.loads(run['Deliveries'])])
    return dict(OriginalNeuronalEvents=neuronal,RoutedEvents=len(sent),DeliveredEvents=len(delivered),WaitingEvents=len(waiting),
        CanonicalDeliveries=ordered(normal_deliveries),CanonicalWaiting=ordered(normal_waiting))

def audit(package):
    folder=package/'partition-evidence';runs=read(folder/'runs.json');saved=read(folder/'comparison.json')
    assert set(runs)=={'Whole','Partitioned'}
    computed={key:reconcile(run) for key,run in runs.items()}
    for key,result in computed.items():
        for field in ('OriginalNeuronalEvents','RoutedEvents','DeliveredEvents','WaitingEvents'):assert saved[key][field]==result[field]
        assert decode_normalized(saved[key]['CanonicalDeliveries'])==result['CanonicalDeliveries']
        assert decode_normalized(saved[key]['CanonicalWaiting'],True)==result['CanonicalWaiting']
    left,right=computed['Whole'],computed['Partitioned'];a,b=runs['Whole'],runs['Partitioned']
    checks=dict(ReticularEqual=a['Reticular']==b['Reticular'],SpinalEqual=a['Spinal']==b['Spinal'],RawDeliveriesEqual=a['Deliveries']==b['Deliveries'],
        CanonicalDeliveriesEqual=left['CanonicalDeliveries']==right['CanonicalDeliveries'],CanonicalWaitingEqual=left['CanonicalWaiting']==right['CanonicalWaiting'])
    assert all(saved[k]==v for k,v in checks.items())
    assert saved['ExcludedAcrossExecutions']==['OriginalMessageId','OriginalJson.MessageId','waiting MessageJson.MessageId']
    assert saved['OriginalIdentifiersVerifiedWithinEachExecution'] is True and saved['NumericalTolerance']==0
    return dict(checks=checks,counts={key:{field:r[field] for field in ('OriginalNeuronalEvents','RoutedEvents','DeliveredEvents','WaitingEvents')} for key,r in computed.items()},
        originalIdentifiersAndPayloadsReconciledWithinEachExecution=True,numericalTolerance=0,
        rawEvidenceSha256=h.digest(folder/'runs.json'),comparisonSha256=h.digest(folder/'comparison.json'))

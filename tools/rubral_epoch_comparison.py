"""Reconcile already-audited strong-left motor identities across fresh epochs.

This does not replace source_and_packets() or the all-native-record audit.
Those must still validate every original message, receipt, contact and state.
"""
import hashlib
import math
import uuid

VERSION='rubral-physical-event-epoch-comparison-v1'
CIRCUIT='isolated-rubral-receptor-motor-study-v1'
EMISSION='isolated-receptor-motor-source-cubic-v2'
INSTANCE='isolated-rubral-motor'
NEURON='auto-SpinalCordMotor-002'
UNIT='rubral-native-unit'


def stable(*parts):
    text=''.join(str(len(str(p)))+':'+str(p) for p in parts)
    return str(uuid.UUID(bytes_le=hashlib.sha256(text.encode()).digest()[:16]))


def epoch(initial,topology,case):
    session=initial['session'];parsed=uuid.UUID(session)
    assert parsed.int!=0 and str(parsed)==session,'Canonical nonempty captured session required.'
    assert initial['scenario']==case['key']=='strong-left'
    identity=topology['identity'];projection=stable(CIRCUIT,session,'motor-projection');synapse=stable(CIRCUIT,session,'motor-synapse')
    assert identity==dict(Instance=INSTANCE,Neuron=NEURON,Projection=projection,Synapse=synapse),'Motor identity is not derived from its captured epoch.'
    contact=initial['activeContact'];contact_id=stable('motor-endplate-group-v1',INSTANCE,NEURON,projection,UNIT)
    assert contact==dict(sourceInstanceKey=INSTANCE,sourceNeuronId=NEURON,projectionId=projection,
        contactId=contact_id,motorUnitId=UNIT,conductionDelayMs=2.0625),'Motor contact/route changed.'
    assert initial['motorClock']==dict(sessionId=session,neuralEpochMs=1000,bodyEpochMs=0)
    # The complete source audit proves the quiet prefix contains no emitted
    # events. Delivered body events are the retained prefix of this one source.
    events=case['events'];assert events,'Strong-left comparison requires delivered neuronal events.'
    previous=-math.inf;physical=[];recorded=[]
    for ordinal,event in enumerate(events,1):
        assert event.keys()=={'muscleIndex','arrivalAtMs','originalMessageId','originalSourceNeuronId','originalContactId'}
        assert event['muscleIndex']==0 and event['originalSourceNeuronId']==NEURON
        assert event['originalContactId']==contact_id,'Event references another epoch/contact.'
        expected_message=stable(EMISSION,session,INSTANCE,NEURON,projection,synapse,ordinal)
        assert event['originalMessageId']==expected_message,'Event identity/order differs from its neuronal epoch.'
        at=event['arrivalAtMs'];assert isinstance(at,(int,float)) and math.isfinite(at) and 0<=at<=case['durationMs'] and at>=previous
        previous=at
        physical.append(dict(ordinal=ordinal,muscleIndex=0,arrivalAtMs=at,sourceInstance=INSTANCE,sourceNeuron=NEURON,motorUnit=UNIT,conductionDelayMs=2.0625))
        recorded.append(dict(event))
    return dict(session=session,identity=identity,contact=contact,recordedEvents=recorded,physicalEvents=physical)


def compare(original_initial,original_topology,original_case,current_initial,current_topology,current_case):
    assert original_case['durationMs']==current_case['durationMs'],'Comparison duration changed.'
    a=epoch(original_initial,original_topology,original_case);b=epoch(current_initial,current_topology,current_case)
    assert a['session']!=b['session'],'A fresh neuronal epoch must not reuse the original session.'
    assert a['identity']['Projection']!=b['identity']['Projection'] and a['contact']['contactId']!=b['contact']['contactId']
    assert a['physicalEvents']==b['physicalEvents'],'Physical source/target, order or arrival timing changed.'
    return dict(version=VERSION,passed=True,original=a,current=b,physicalEventsCompared=len(a['physicalEvents']),
        withinEpochIdentitiesValidated=True,rawIdentitiesPreserved=True,arrivalTimingComparedExactly=True,
        contactIdentityEqualityAcrossSessionsRequired=False,fullNativeSourceAuditStillRequired=True,
        modelsExecuted=False,physiologyQualified=False,avatarRecoveryAssessed=False)

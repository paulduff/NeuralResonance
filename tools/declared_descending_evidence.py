"""Saved original neuronal emissions and detailed motor admissions; no integration."""
import hashlib
import json
from afferent_precision_gate import read


def digest(text):
    return hashlib.sha256(text.encode('utf-8')).hexdigest().upper()


def emissions(frame):
    assert digest(frame['CanonicalJson']) == frame['Sha256']
    body = json.loads(frame['CanonicalJson'])
    assert body['Failure'] is None and frame['CompletedTicks'] == 320 and frame['CompletedThroughMs'] == 40
    steps = [json.loads(s) for s in body['ReceiverStepJson']]
    assert [s['Ack']['Tick'] for s in steps] == list(range(1, 321))
    events = [e for s in steps for e in s['OutboundSpikes']]
    assert events and len({e['MessageId'] for e in events}) == len(events)
    for i, step in enumerate(steps):
        assert all(i * .125 < e['TimestampMs'] <= (i + 1) * .125 for e in step['OutboundSpikes'])
        assert all(not e['IsSyntheticBackground'] for e in step['OutboundSpikes'])
    return events


def assert_declared(events, routes):
    by_neuron = {r['SourceNeuron']: r for r in routes}
    assert len(routes) == len(by_neuron) == 256
    assert {r['SourceIndex'] for r in routes} == set(range(256))
    assert len({r['SourceSynapse'] for r in routes}) == 256
    for event in events:
        route = by_neuron[event['SourceNeuronId']]
        for event_key, route_key in (('SourceInstanceKey', 'SourceInstance'), ('SourceStructure', 'SourceStructure'),
                ('TargetStructure', 'TargetStructure'), ('TargetNeuronId', 'TargetNeuron'),
                ('SynapseId', 'SourceSynapse'), ('ProjectionId', 'Projection'), ('Neurotransmitter', 'Transmitter')):
            assert event[event_key] == route[route_key], (event_key, route_key)
        assert not event['IsFeedback']


def audit(package):
    comparison = read(package / 'descending-evidence/actual-source-comparison.json')
    original, candidate = emissions(comparison['Original']), emissions(comparison['Candidate'])
    assert comparison['OriginalState']['Actual']['CellStateJson'] == comparison['CandidateState']['Actual']['CellStateJson']
    changed = {'MessageId', 'SynapseId', 'ProjectionId', 'TargetStructure', 'TargetNeuronId'}
    assert [{k: v for k, v in e.items() if k not in changed} for e in original] == [
        {k: v for k, v in e.items() if k not in changed} for e in candidate]
    assert_declared(candidate, comparison['Routes'])
    motor = read(package / 'descending-evidence/actual-motor-admission.json')
    assert motor['Input'] == comparison['Input'] and json.loads(motor['Input'])['ExplicitUpstreamFixture']
    emitted = emissions(motor['OriginalNeuronalFrame'])
    assert_declared(emitted, motor['Routes'])
    packet, frame, receipt = motor['Packet'], motor['OriginalSourceFrame'], motor['Receipt']
    assert frame['Packets'] == [packet] and digest(packet['CanonicalJson']) == packet['Sha256']
    assert packet['SourceFrameSha256'] == frame['SourceFrameSha256'] == motor['OriginalNeuronalFrame']['Sha256']
    body = json.loads(packet['CanonicalJson'])
    assert body['StartingTick'] == 0 and body['CompletedTick'] == 320 and body['CompletedThroughMs'] == 40
    assert body['SourceFrameSha256'] == packet['SourceFrameSha256']
    assert [json.loads(r['OriginalEventJson']) for r in body['Events']] == emitted
    assert packet['EventCount'] == receipt['AcceptedEvents'] == len(emitted)
    assert receipt['PacketSha256'] == packet['Sha256'] and receipt['SourceFrameSha256'] == packet['SourceFrameSha256']
    contacts = [c for d in motor['Definitions'] for c in d['Contacts']]
    manifest = motor['ContactManifest']
    assert [c['Definition'] for c in manifest] == contacts
    assert len(contacts) == 256 and len({c['ContactId'] for c in manifest}) == 256
    for row in body['Events']:
        source, routed = json.loads(row['OriginalEventJson']), json.loads(row['RoutedEventJson'])
        assert {k: v for k, v in source.items() if k not in ('MessageId', 'ProjectionId')} == {
            k: v for k, v in routed.items() if k not in ('MessageId', 'ProjectionId')}
        assert routed['ProjectionId'] == row['ProjectionId']
        matches = [c for c in contacts if c['SourceInstance'] == routed['SourceInstanceKey']
            and c['SourceNeuron'] == routed['SourceNeuronId'] and c['SourceSynapse'] == routed['SynapseId']
            and c['Projection'] == routed['ProjectionId'] and c['TargetNeuron'] == routed['TargetNeuronId']]
        assert len(matches) == 1
    state = motor['State']
    assert state['Failure'] is None and state['Pending'] is None and state['KnownSourceThroughMs'] == 40
    assert len(state['Cells']) == 2 and sum(c['Cell']['AdmittedEvents'] for c in state['Cells']) == len(emitted)
    delivered = sum(c['Cell']['DeliveredEvents'] for c in state['Cells'])
    assert 0 < delivered <= len(emitted)
    for cell in state['Cells']:
        assert not cell['Faulted'] and not cell['Cell']['Faulted'] and cell['Cell']['TimeMs'] == 40 and cell['CompletedSamples'] == 320
        assert sum(l['AdmittedEvents'] for l in cell['Cell']['InputLanes']) == cell['Cell']['AdmittedEvents']
        assert sum(l['DeliveredEvents'] for l in cell['Cell']['InputLanes']) == cell['Cell']['DeliveredEvents']
    assert {c['ContactId'] for c in manifest} == {l['ContactId'] for cell in state['Cells'] for l in cell['Cell']['InputLanes']}
    for captured in motor['MotorFrames']:
        assert digest(captured['CanonicalJson']) == captured['Sha256']
    return dict(originalSourceEvents=len(original), candidateSourceEvents=len(candidate),
        originalCellStatesExactlyEqual=True, declaredOriginalEventIdentitiesReconciled=True,
        retainedSourceEventsToMotor=len(emitted), admittedMotorEvents=len(emitted), deliveredMotorEvents=delivered,
        retainedFutureMotorEvents=len(emitted)-delivered, actualMotorCells=2, contacts=256,
        upstreamInputsAreFixtures=True, isolatedOriginalReticularEventsObserved=True,
        independentCoupledAccuracyQualified=False, physiologicalCalibrationQualified=False,
        liveBrainAuthorityQualified=False, avatarBalanceAssessed=False)

"""Reconcile saved native endpoints and original sensory/nucleus packets."""
import hashlib
import json
from head_observation_identity import original_observation
from native_geometry_identity import CrossSessionGeometry


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def rows(path):
    return [json.loads(line) for line in path.read_text(encoding='utf-8-sig').splitlines()]


def sha(value):
    return hashlib.sha256(value.encode('utf-8')).hexdigest().upper()


def camel(value):
    # Only these declared .NET record payloads use PascalCase. Raw evidence
    # strings remain unchanged, including their original hashes and schemas.
    if isinstance(value, list):
        return [camel(v) for v in value]
    if isinstance(value, dict):
        result = {k[0].lower()+k[1:]: camel(v) for k, v in value.items()}
        assert len(result) == len(value)
        return result
    return value


def native_case(package, folder, condition):
    original = rows(package/'native-evidence/conditions/no-joint-friction/native'/condition/'acknowledged.jsonl')
    ack = rows(folder/'acknowledged.jsonl'); heads = rows(folder/'heads.jsonl'); inspected = rows(folder/'inspections.jsonl')
    assert len(original) == len(ack) == len(heads) == 9
    wire = folder/'tendon-bridge'
    replies = [read(wire/'hello.json')] + [read(p) for p in sorted(wire.glob('reply-*.json'))]
    assert len(replies) == 11+len(inspected) and [r['sequence'] for r in replies] == list(range(len(replies)))
    assert len(list(wire.glob('request-*.json'))) == len(replies)-1
    assert replies[-1]['operation'] == 'stop' and replies[-1]['physicsStep'] == 8
    assert all(r['paused'] and not r['connected'] and r['isolated'] for r in replies)
    assert all(r['mechanicsDiagnosticJson'] == original[0]['mechanicsDiagnosticJson'] for r in replies)
    geometry = CrossSessionGeometry(ack[0], original[0])
    observations = []
    for step, (a, head, old) in enumerate(zip(ack, heads, original, strict=True)):
        assert a == replies[a['sequence']] and a['physicsStep'] == head['physicsStep'] == step
        geometry.check(a, old)
        assert sha(a['stateJson']) == a['stateHash'] == head['bodyStateSha256']
        assert sha(a['observationsJson']) == a['observationsHash'] == head['observationsSha256']
        assert a['observationsJson'] == head['originalObservationsJson']
        native = json.loads(a['observationsJson']); saved = json.loads(old['observationsJson'])
        assert native['skullJson'] == saved['skullJson'] and len(native['contactsJson']) == 15
        assert len(native['skullJson']) == len(head['samples']) == 3
        for sample, raw in zip(head['samples'], native['skullJson'], strict=True):
            assert sample['originalNativeJson'] == raw and sample['nativeSha256'] == sha(raw)
            receipt = sample['kinematics']
            assert receipt['rawObservationSha256'] == sha(receipt['rawObservationJson'])
            original_observation(receipt['rawObservationJson'], sample['observation'])
            assert receipt['physicsStep'] == step and (receipt['vestibularInput'] is None) == (step == 0)
        selected = next(s for s in head['samples'] if s['sensorId'] == 'right')
        observations.append(selected['observation'])
    for a in inspected:
        assert a == replies[a['sequence']] and a['operation'] == 'inspect'
        assert all(a[k] == ack[a['physicsStep']][k] for k in ('stateJson', 'observationsJson', 'applicationsJson', 'paths'))
    final = read(folder/'result.json')
    assert final['keepaliveInspections'] == len(inspected)
    return dict(case=folder.name, originalPhysicalStatesExact=True, nativeHeadPayloadsExact=True,
                crossSessionGeometry=geometry.report(),
                physicalSteps=8, headReceipts=27, keepaliveInspections=len(inspected)), observations


def nucleus_packets(folder, initial, final):
    frames = rows(folder/'nucleus-frames.jsonl'); packets = []; admitted = {}; delivered = {}; ticks = 0
    source = initial['source']; epoch = source['epochMs']; through = epoch
    route = initial['route']; assert route['remainingDelayMs'] == .25 and route['targetCell'] == 17
    for sequence, frame in enumerate(frames, 1):
        assert frame['sequence'] == sequence and sha(frame['canonicalJson']) == frame['sha256']
        record = camel(json.loads(frame['canonicalJson']))
        assert record['fingerprint'] == initial['nucleusFingerprint'] and record['failure'] is None and record['undrainedFailureOutputJson'] is None
        packet = record['packet']; receipt = record['admission']; packets.append(packet)
        assert frame['packetFingerprint'] == packet['fingerprint'] == receipt['packetFingerprint']
        assert receipt['sourceCompletedStep'] == packet['completedStep'] and receipt['sourceCompletedMs'] == packet['endMs']
        assert receipt['targetSafeThroughMs'] == packet['endMs']+.25
        assert len(receipt['deliveries']) == len(packet['events'])
        for event, evidence in zip(packet['events'], receipt['deliveries'], strict=True):
            assert evidence['originalEventId'] == event['messageId'] and evidence['sourceTimeMs'] == event['crossingTimeMs']
            assert evidence['scheduledArrivalMs'] == event['crossingTimeMs']+.25
            assert evidence['projectionId'] == route['projectionId'] and evidence['targetCell'] == 17
            assert evidence['deliveryId'] not in admitted
            admitted[evidence['deliveryId']] = evidence
        assert record['receiverTicks'] >= ticks and len(record['receiverStepJson']) == record['receiverTicks']-ticks
        ticks = record['receiverTicks']; through = record['receiverThroughMs']
        assert through == epoch+ticks*.125 and through <= receipt['targetSafeThroughMs'] < epoch+(ticks+1)*.125
        for actual in record['actualDeliveries']:
            identity = actual['originalMessageId']; assert identity in admitted and identity not in delivered
            expected = admitted[identity]
            assert actual['receivingContactId'] == expected['contactId'] and actual['actualTargetIndex'] == expected['targetCell']
            assert actual['arrivalAtMs'] == actual['appliedAtMs'] == expected['scheduledArrivalMs'] <= through
            delivered[identity] = actual
    pending = {k: v for k, v in admitted.items() if k not in delivered}
    assert all(v['scheduledArrivalMs'] > through for v in pending.values())
    assert final['nucleus']['receiverTicks'] == ticks and final['nucleus']['receiverThroughMs'] == through
    assert final['nucleus']['sourceSteps'] == final['nucleus']['acknowledgedSourceSteps'] == final['sensorySteps']
    assert final['nucleus']['pending'] is None and final['nucleus']['failure'] is None
    return packets, dict(case=folder.name, frames=len(frames), originalEventsAdmitted=len(admitted),
                         actualContactDeliveries=len(delivered), futureDeliveries=len(pending), receiverTicks=ticks,
                         receiverThroughMs=through, fixedReceiverGridPreserved=True)

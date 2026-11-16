"""Independently audit retained native commands, muscle events and point mechanics.

No differential equation or physics is run by this saved-data analysis. Individual
impulses do not establish an accurate coupled trajectory or biological calibration.
"""
import argparse
from datetime import datetime, timezone
import hashlib
import json
import math
from pathlib import Path
import struct

def decode_wire(value):
    if isinstance(value, dict):
        if set(value) == {'bits'}:
            bits = value['bits']
            assert isinstance(bits, str) and len(bits) == 16 and all(c in '0123456789ABCDEF' for c in bits)
            decoded = struct.unpack('>d', bytes.fromhex(bits))[0]
            assert math.isfinite(decoded)
            return decoded
        return {key: decode_wire(child) for key, child in value.items()}
    if isinstance(value, list): return [decode_wire(child) for child in value]
    return value


read = lambda p: decode_wire(json.loads(p.read_text(encoding='utf-8-sig')))
f32 = lambda v: struct.unpack('<f', struct.pack('<f', v))[0]
H = f32(.000125)
MS = H * 1000
vec = lambda v: tuple(f32(v[k]) for k in ('x', 'y', 'z'))
sub = lambda a, b: tuple(x-y for x, y in zip(a, b))
dot = lambda a, b: sum(x*y for x, y in zip(a, b))
norm = lambda v: math.sqrt(dot(v, v))
digest = lambda s: hashlib.sha256(s.encode()).hexdigest().upper()
POSITIVES = ('quiet', 'left', 'right', 'balanced')
NEGATIVES = ('session', 'sequence', 'clock', 'state', 'plant', 'duplicate', 'negative', 'inspect-force')


def finite_tree(value):
    if isinstance(value, dict):
        for child in value.values(): finite_tree(child)
    elif isinstance(value, list):
        for child in value: finite_tree(child)
    elif isinstance(value, float): assert math.isfinite(value)


def audit_wire(package):
    folder = package / 'native-bridge'
    # Inspect the encoded form, not just decoded arithmetic values, including
    # signed zero, subnormals and every exactly represented physical endpoint.
    raw = lambda p: json.loads(p.read_text(encoding='utf-8-sig'))
    sent = raw(folder / 'wire-preflight-input.json'); native = raw(folder / 'wire-preflight-native.json')
    host = raw(folder / 'wire-preflight-host.json')
    assert native['completed'] and native['passed'] and native['unity'] == '6000.3.19f1'
    assert native['exactCases'] == host['exactRoundTrips'] == 1040
    assert host['passed'] and host['originalDecimalFailureReproduced']
    assert native['rejectedMalformedScalars'] == host['malformedScalarsRejectedEachSide'] == len(sent['invalidBits']) == 10
    assert not native['physicsExecuted'] and not host['physicsExecuted']
    assert sent['legacyRequestJson'] == (package / 'reference/failed-request-000033.json').read_text(encoding='utf-8-sig')
    previous = raw(package / 'reference/last-reply-000032.json')
    assert sent['legacyLastEffortId'] == previous['lastAppliedEffortId']
    assert native['legacyGuidValid'] and native['legacyFreshEffort'] and native['legacyStartExact'] and not native['legacyEndExact']
    bits = lambda x: struct.pack('>d', x).hex().upper()
    assert native['legacyStartBits'] == native['expectedStartBits'] == bits(9*MS)
    assert native['legacyEndBits'] != native['expectedEndBits'] == bits(10*MS)
    assert len(sent['samples']) == len(native['cases']) == 1040
    differences = 0
    for index, (sample, row) in enumerate(zip(sent['samples'], native['cases'])):
        assert sample['key'] == row['key']
        if index < 1025:
            assert sample['key'] == 'clock-' + str(index) and sample['expectedBits'] == bits(index*MS) and sample['compareDecimal']
        assert sample['exactValue']['bits'] == sample['expectedBits'] == row['expectedBits'] == row['exactDecodedBits'] == row['returnedExact']['bits']
        assert row['compareDecimal'] == sample['compareDecimal']
        assert math.isfinite(struct.unpack('>d', bytes.fromhex(sample['expectedBits']))[0])
        if sample['compareDecimal']: differences += row['decimalParsedBits'] != sample['expectedBits']
    assert native['cases'][10]['decimalParsedBits'] == native['legacyEndBits']
    assert differences == native['decimalDifferences'] and differences > 0
    return dict(exactScalarRoundTrips=1040, malformedScalarsRejectedEachSide=10,
        decimalDecodedDifferences=differences, originalDecimalFailureReproduced=True)


def state(reply):
    assert reply['version'] == 2 and reply['isolated'] and reply['paused'] and reply['unity'] == '6000.3.19f1'
    assert reply['physicsStepMs'] == MS and reply['physicalTimeMs'] == reply['physicsStep'] * MS
    assert digest(reply['stateJson']) == reply['stateHash'] and digest(reply['geometryJson']) == reply['bodyFingerprint']
    result = json.loads(reply['stateJson']); finite_tree(result)
    assert [p['muscleId'] for p in reply['paths']] == ['m0', 'm1']
    for i, path in enumerate(reply['paths']):
        bodies = result['m' + str(i)]; assert [b['id'] for b in bodies] == ['fixed-root', 'hinge-child']
        root, child = bodies; a, b = vec(root['point']), vec(child['point'])
        delta = sub(b, a); length = norm(delta)
        speed = dot(delta, sub(vec(child['pointVelocity']), vec(root['pointVelocity']))) / length
        assert abs(path['lengthMeters'] - length) <= 1e-12
        assert abs(path['velocityMetersPerSecond'] - speed) <= 1e-12
        q, w = result['angle'], result['speed']; radius = f32(.4); center = f32(.3)
        assert child['mass'] == 2 and vec(child['inertia']) == (f32(.15),)*3
        expected = (0, radius * math.cos(q), radius * math.sin(q))
        assert norm(sub(b, expected)) <= 2e-6
        assert norm(sub(vec(child['pointVelocity']), (0, -w*radius*math.sin(q), w*radius*math.cos(q)))) <= 2e-6
        assert norm(sub(vec(child['velocity']), (0, -w*center*math.sin(q), w*center*math.cos(q)))) <= 2e-6
        assert norm(sub(vec(child['angularVelocity']), (w, 0, 0))) <= 2e-6
        assert norm(vec(root['position'])) <= 2e-6 and norm(vec(root['velocity'])) <= 2e-6 and norm(vec(root['angularVelocity'])) <= 2e-6
        assert norm(vec(root['rotation'])) <= 2e-6 and abs(abs(root['rotation']['w'])-1) <= 2e-6
    return result


def audit(package):
    wire = audit_wire(package)
    folder = package / 'native-bridge'; result = read(folder / 'results.json')
    assert result['completed'] and result['passed'] and result['actualPhysicsSteps'] == 4096
    assert result['responseBoundRadians'] == 1e-5 and result['minimumResponseRadians'] == 1e-7
    assert not any(result[k] for k in ('liveBodySelected', 'neuronalModelExecuted', 'trajectoryAccuracyQualified', 'physiologicalCalibrationQualified', 'avatarRecoveryAssessed'))
    names = list(POSITIVES) + ['reject-' + n for n in NEGATIVES]
    assert [r['scenario'] for r in result['cases']] == names and all(r['passed'] for r in result['cases'])
    total = 0; angles = {}; maximum_impulse_error = 0.; maximum_angle_error = 0.; complete_events = 0
    for name in names:
        case = folder / name; bridge = case / 'bridge'; initial = read(case / 'initial.json')
        hello = read(bridge / 'hello.json'); previous = hello; before = state(hello)
        assert hello['operation'] == 'hello' and hello['sequence'] == hello['physicsStep'] == 0
        assert before['angle'] == before['speed'] == 0 and hello['plantFingerprint'] == hello['lastAppliedEffortId'] == ''
        assert initial['muscleSubsteps'] == 2 and initial['clock']['physicsStepMs'] == MS
        assert not initial['neuronalModelExecuted'] and initial['syntheticMotorEvents']
        step_receipts = {}; effort_ids = set(); physical_steps = 0
        requests = sorted(bridge.glob('request-*.json')); replies = sorted(bridge.glob('reply-*.json'))
        assert len(requests) == len(replies) + int(name.startswith('reject-'))
        for sequence, file in enumerate(replies, 1):
            assert file.name == f'reply-{sequence:06d}.json'
            command = read(bridge / f'request-{sequence:06d}.json'); reply = read(file); after = state(reply)
            assert command['sequence'] == reply['sequence'] == sequence and command['version'] == 2
            assert command['sessionId'] == reply['sessionId'] == hello['sessionId']
            assert command['bodyFingerprint'] == reply['bodyFingerprint'] == hello['bodyFingerprint']
            assert reply['geometryJson'] == hello['geometryJson']
            assert command['stateHash'] == previous['stateHash'] and command['expectedStep'] == previous['physicsStep']
            assert command['startTimeMs'] == previous['physicalTimeMs'] and command['operation'] == reply['operation']
            assert command['plantFingerprint'] == reply['plantFingerprint'] == initial['plantFingerprint']
            assert [p['pathFingerprint'] for p in reply['paths']] == [p['pathFingerprint'] for p in hello['paths']]
            if command['operation'] == 'step':
                assert command['endTimeMs'] == reply['physicalTimeMs'] == (physical_steps + 1) * MS
                assert reply['physicsStep'] == physical_steps + 1 and reply['lastAppliedEffortId'] == command['effortId']
                assert command['effortId'] not in effort_ids; effort_ids.add(command['effortId'])
                tensions = command['tensions']; assert [t['muscleId'] for t in tensions] == ['m0', 'm1']
                torque = 0
                for i, t in enumerate(tensions):
                    assert math.isfinite(t['axialNewtons']) and t['axialNewtons'] >= 0
                    root, child = before['m' + str(i)]; delta = sub(vec(child['point']), vec(root['point'])); length = norm(delta)
                    force = tuple(f32(-v / length * t['axialNewtons']) for v in delta)
                    point = vec(child['point']); torque += point[1]*force[2]-point[2]*force[1]
                inertia = f32(.15) + 2*f32(.3)**2; acceleration = torque / inertia
                impulse_error = abs(after['speed'] - (before['speed'] + acceleration*H))
                angle_error = abs(after['angle'] - (before['angle'] + before['speed']*H + .5*acceleration*H*H))
                assert impulse_error <= 2e-6 and angle_error <= 2e-6 + .5*abs(acceleration)*H*H
                maximum_impulse_error = max(maximum_impulse_error, impulse_error); maximum_angle_error = max(maximum_angle_error, angle_error)
                physical_steps += 1; step_receipts[physical_steps] = (command, reply)
            else:
                assert command['operation'] in ('bind', 'inspect', 'stop') and command['tensions'] == [] and command['effortId'] == ''
                assert command['endTimeMs'] == command['startTimeMs'] and reply['physicsStep'] == previous['physicsStep']
                assert reply['stateJson'] == previous['stateJson'] and reply['paths'] == previous['paths']
                assert reply['lastAppliedEffortId'] == previous['lastAppliedEffortId']
                if command['operation'] == 'bind': assert sequence == 1 and physical_steps == 0
            previous, before = reply, after
        if name.startswith('reject-'):
            failure = read(bridge / 'failure.json')
            assert physical_steps == 0 and failure['physicsStep'] == 0 and not failure['physicsAttempted']
            assert failure['observed']['stateHash'] == previous['stateHash'] and failure['sessionId'] == hello['sessionId']
            state(failure['observed']); assert failure['error']
            reason = {'session': 'Command identity/sequence differs', 'sequence': 'Command identity/sequence differs',
                'clock': 'Effort identity or physical endpoint differs', 'state': 'Command does not name the current body endpoint',
                'plant': 'Plant binding differs', 'duplicate': 'Require exactly the ordered finite nonnegative muscle tensions',
                'negative': 'Require exactly the ordered finite nonnegative muscle tensions',
                'inspect-force': 'Nonmoving operation must not carry force or time advancement'}[name[7:]]
            assert reason in failure['error'], (name, failure['error'])
            continue
        assert physical_steps == 1024 and previous['operation'] == 'stop'; total += physical_steps
        final = read(case / 'final.json'); expected = {}; delivered = []
        for i in range(2):
            batch = read(case / f'source-{i}.json'); contact = initial['definitions'][i]['units'][0]['contact']
            assert batch['completeThroughNeuralMs'] == 1024*MS
            for spike in batch['spikes']:
                expected[spike['messageId']] = dict(sessionId=batch['sessionId'], sourceInstanceKey=batch['sourceInstanceKey'],
                    messageId=spike['messageId'], ordinal=spike['ordinal'], sourceNeuronId=spike['sourceNeuronId'], projectionId=spike['projectionId'],
                    sourceSynapseId=spike['sourceSynapseId'], contactId=contact['contactId'], motorUnitId=contact['motorUnitId'],
                    firedAtNeuralMs=spike['firedAtNeuralMs'], arrivalAtBodyMs=spike['firedAtNeuralMs']+contact['conductionDelayMs'])
        lines = (case / 'steps.jsonl').read_text().splitlines(); assert len(lines) == 1024
        for tick, line in enumerate(lines, 1):
            entry = decode_wire(json.loads(line)); finite_tree(entry); step = entry['step']; command, reply = step_receipts[tick]
            assert step['after']['physicsStep'] == tick and step['before']['physicsStep'] == tick-1
            assert entry['native']['stateHash'] == reply['stateHash'] and entry['native']['paths'] == reply['paths']
            assert step['effort']['effortId'].replace('-', '') == command['effortId']
            assert step['effort']['tensions'] == command['tensions']
            for event in step['arrivals']:
                assert event == expected[event['messageId']]
                assert (tick-1)*MS < event['arrivalAtBodyMs'] <= tick*MS
                delivered.append(event['messageId'])
        assert len(delivered) == len(set(delivered)) == len(expected) == final['arrivals']
        assert set(delivered) == set(expected) and len(expected) == {'quiet': 0, 'left': 2, 'right': 2, 'balanced': 4}[name]
        complete_events += len(expected); angles[name] = json.loads(final['native']['stateJson'])['angle']
        assert final['native']['stateHash'] == previous['stateHash'] and not final['plant']['faulted'] and not final['plant']['running']
    assert total == 4096 and complete_events == 8
    assert abs(angles['quiet']) <= 1e-5 and abs(angles['balanced']) <= 1e-5
    assert angles['left'] > 1e-7 and angles['right'] < -1e-7 and abs(angles['left'] + angles['right']) <= 1e-5
    return dict(completed=True, originalCriteriaPassed=True, independentlyAuditedCases=12, actualPhysicsSteps=total, wire=wire,
        fixtureEventsExactlyDelivered=complete_events, angles=angles, maximumStepImpulseError=maximum_impulse_error,
        maximumStepAngleError=maximum_angle_error, testsOrPhysicsExecutedByAnalysis=False,
        coupledTrajectoryAccuracyQualified=False, physiologicalCalibrationQualified=False, avatarRecoveryAssessed=False)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__); parser.add_argument('package', type=Path); args = parser.parse_args()
    result = audit(args.package.resolve()); result['analysedUtc'] = datetime.now(timezone.utc).isoformat()
    with (args.package / 'bridge-analysis.json').open('x') as stream: json.dump(result, stream, indent=2, allow_nan=False)
    print(json.dumps(result, indent=2))

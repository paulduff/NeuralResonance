"""Audit saved active motor source/Avatar receiver evidence; never solve a cell."""
from hashlib import sha256
import importlib.util
import json
from pathlib import Path
import re
import sys
import numpy as np
from receptor_motor_output_protocol import protocol
from receptor_membrane_protocol import crossing_mode

read = lambda p: json.loads(p.read_text(encoding='utf-8-sig'))


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    value = importlib.util.module_from_spec(spec); spec.loader.exec_module(value)
    return value


def pascal(value):
    if isinstance(value, list): return [pascal(v) for v in value]
    if isinstance(value, dict): return {k[0].upper() + k[1:]: pascal(v) for k, v in value.items()}
    return value


def audit(package, candidate=None):
    if not __debug__: raise RuntimeError('Saved-data acceptance assertions must remain enabled.')
    base = module('motor_output_baseline_audit', Path(__file__).with_name('audit-receptor-membrane.py'))
    if candidate is None: candidate = base.audit(package)
    membrane = read(package / 'receptor-membrane-input.json')
    spec = read(package / 'receptor-motor-output-input.json'); assert spec == protocol(crossing_mode(membrane))
    config = read(package / 'configuration.json')
    assert config['IsolatedReceptorMembraneComparison'] and config['SourceOutputComparisonRequested']
    assert config['ExpectedSourceOutputConditions'] == spec['sourceConditions'] == 36
    root = package / 'motor-output-study'; meta = read(root / 'protocol.json'); result = read(root / 'results.json')
    assert meta['inputSha256'] == base.digest(package / 'receptor-motor-output-input.json')
    assert meta['membraneInputSha256'] == base.digest(package / 'receptor-membrane-input.json')
    assert meta['baselineResultsSha256'] == base.digest(package / 'membrane-study/results.json')
    assert meta['specification'] == {k: spec[k] for k in meta['specification']}
    assert result['completed'] and result['exactObservedStateAndDeliveryChecksPassed']
    assert result['quietHasNoCrossings'] and result['nontrivialExcitedCrossings']
    assert result['postsynapticNeuronExecuted'] and result['actualAvatarReceiverExecuted']
    assert meta['postsynapticNeuronExecutionRequested'] and meta['actualAvatarReceiverRequested']
    assert result['independentNumericalAccuracyQualified'] is False
    for container in (meta, result):
        assert container['presynapticEventsAreFixtures'] and not container['presynapticNeuronsExecuted']
        for key in ('nativePhysicsExecuted', 'muscleDynamicsExecuted', 'physiologicalCalibrationComplete', 'avatarRecoveryAssessed'):
            assert container[key] is False, key
    transmitters = base.enums(package / 'source/Protocol/NTEnum.cs')
    expected = [(c['key'] + '-h' + str(step), cadence) for c in membrane['cases']
        for step in membrane['stepsMs'] for cadence in spec['publicationEverySamples']]
    assert [(r['baselineKey'], r['publicationEverySamples']) for r in result['cases']] == expected
    rows = []; observation_total = input_total = 0; excited_crossings = False
    for scenario in membrane['cases']:
        for raw in candidate['cases'][scenario['key']]['conditions']:
            if scenario['key'] == 'quiet': assert len(raw['crossings']) == 0
            if scenario['key'] in ('soma-excitation', 'local-excitation', 'distributed-excitation'):
                excited_crossings |= len(raw['crossings']) > 0
            baseline_key = scenario['key'] + '-h' + str(raw['maximumStepMs'])
            baseline = package / 'membrane-study' / baseline_key
            inputs = read(baseline / 'inputs.json'); original_final = read(baseline / 'final.json')
            original = original_final['state']; session = inputs['session']
            target = inputs['admissions'][0]['definition']
            identity = dict(instance=target['targetInstance'], neuron=target['targetNeuron'],
                projection=base.stable(spec['sourceVersion'], 'projection', session, target['targetInstance'], target['targetNeuron']),
                synapse=base.stable(spec['sourceVersion'], 'synapse', session, target['targetInstance'], target['targetNeuron']))
            contact_id = base.stable('motor-endplate-group-v1', identity['instance'], identity['neuron'], identity['projection'], spec['motorUnit'])
            contact = dict(sourceInstanceKey=identity['instance'], sourceNeuronId=identity['neuron'], projectionId=identity['projection'],
                contactId=contact_id, motorUnitId=spec['motorUnit'], conductionDelayMs=spec['conductionDelayMs'])
            anatomy_hash = base.json_hash(dict(Version=1, Contacts=[pascal(contact)]))
            emission = [dict(ordinal=i + 1, messageId=base.stable(spec['sourceVersion'], session, identity['instance'], identity['neuron'],
                identity['projection'], identity['synapse'], i + 1), firedAtNeuralMs=float(t), sourceInstanceKey=identity['instance'],
                sourceNeuronId=identity['neuron'], projectionId=identity['projection'], sourceSynapseId=identity['synapse'])
                for i, t in enumerate(raw['crossings'])]
            arrivals = [dict(sessionId=session, sourceInstanceKey=identity['instance'], messageId=e['messageId'], ordinal=e['ordinal'],
                sourceNeuronId=identity['neuron'], projectionId=identity['projection'], sourceSynapseId=identity['synapse'],
                contactId=contact_id, motorUnitId=spec['motorUnit'], firedAtNeuralMs=e['firedAtNeuralMs'],
                arrivalAtBodyMs=e['firedAtNeuralMs'] + spec['conductionDelayMs']) for e in emission]
            arrival_times = np.array([a['arrivalAtBodyMs'] for a in arrivals])
            input_times = np.array(sorted(e['timestampMs'] + a['definition']['axonalDelayMs']
                for a in inputs['admissions'] for e in a['originalEvents']))
            total_inputs = len(input_times)
            times = np.arange(15001, dtype=np.float64) * spec['sampleMs']
            indices = np.searchsorted(raw['time'], times)
            assert np.array_equal(raw['time'][indices], times)
            headers = ['timeMs'] + ['state' + str(i) for i in range(30)] + ['receptor_' + g for g in original_final['contactOrder']]
            headers += ['integrationSteps', 'admittedEvents', 'deliveredEvents', 'observedCrossings']
            prior_states = fingerprint = None
            for cadence in spec['publicationEverySamples']:
                key = baseline_key + '-packets' + str(cadence)
                outcome = next(r for r in result['cases'] if r['key'] == key); assert outcome['completed']
                folder = root / key; initial = read(folder / 'initial.json'); final = read(folder / 'final.json')
                assert initial['baselineKey'] == baseline_key and initial['publicationEverySamples'] == cadence
                assert initial['identity'] == identity and initial['clock'] == dict(sessionId=session, neuralEpochMs=0, bodyEpochMs=0)
                assert initial['contacts'] == [contact] and initial['anatomyFingerprint'] == anatomy_hash
                assert initial['baselineInputsSha256'] == base.digest(baseline / 'inputs.json')
                source = final['source']; fresh = initial['source']
                for snapshot, sample in ((fresh, 0), (source, 15000)):
                    assert snapshot['sessionId'] == session and snapshot['completedSamples'] == sample
                    assert snapshot['sampleMs'] == .1 and snapshot['maximumStepMs'] == raw['maximumStepMs']
                    assert snapshot['diagnosticBlock'] == 0 and snapshot['faulted'] is False
                    assert re.fullmatch('[0-9A-F]{64}', snapshot['configurationFingerprint'])
                assert fresh['emissions'] == [] and fresh['cell']['crossings'] == [] and fresh['cell']['timeMs'] == 0
                assert source['cell'] == original and source['emissions'] == emission
                assert fresh['configurationFingerprint'] == source['configurationFingerprint']
                if fingerprint is None: fingerprint = source['configurationFingerprint']
                else: assert fingerprint == source['configurationFingerprint']
                assert base.digest(folder / 'trajectory.csv') == outcome['trajectorySha256']
                assert base.digest(folder / 'transport.jsonl') == outcome['transportSha256']
                with (folder / 'trajectory.csv').open() as stream: assert stream.readline().strip().split(',') == headers
                table = np.loadtxt(folder / 'trajectory.csv', delimiter=',', skiprows=1, ndmin=2)
                assert table.shape == (15001, len(headers)) and np.isfinite(table).all()
                assert np.array_equal(table[:, 0], times)
                states = np.ascontiguousarray(table[:, 1:-4]); reference_states = np.ascontiguousarray(raw['state'][indices])
                assert np.array_equal(states.view(np.uint64), reference_states.view(np.uint64)), key
                if prior_states is not None: assert np.array_equal(states.view(np.uint64), prior_states.view(np.uint64))
                prior_states = states
                assert np.array_equal(table[:, -4], indices)
                assert np.all(table[:, -3] == total_inputs)
                assert np.array_equal(table[:, -2], np.searchsorted(input_times, times, side='right'))
                assert np.array_equal(table[:, -1], np.searchsorted(raw['crossings'], times, side='right'))
                samples = sorted(set(range(cadence, 15001, cadence)) | {15000})
                published = delivered = retries = blocked = batches = 0
                with (folder / 'transport.jsonl').open(encoding='utf-8-sig') as stream:
                    for row, sample in zip(stream, samples, strict=True):
                        record = json.loads(row); batches += 1; time = sample * .1
                        assert record['sample'] == sample
                        upto = int(np.searchsorted(raw['crossings'], time, side='right'))
                        spikes = [dict(messageId=e['messageId'], ordinal=e['ordinal'], sourceNeuronId=e['sourceNeuronId'],
                            projectionId=e['projectionId'], sourceSynapseId=e['sourceSynapseId'], firedAtNeuralMs=e['firedAtNeuralMs'],
                            neurotransmitter=transmitters['ACETYLCHOLINE'], isSyntheticBackground=False, isFeedback=False)
                            for e in emission[published:upto]]
                        batch = dict(version=1, sessionId=session, sourceInstanceKey=identity['instance'], anatomyFingerprint=anatomy_hash,
                            sequence=batches, completeThroughNeuralMs=time, spikes=spikes)
                        assert record['batch'] == batch
                        # Retain the exact .NET wire representation so its hash is
                        # checked without guessing a different JSON float formatter.
                        canonical = record['canonicalBatchJson']
                        assert json.loads(canonical) == pascal(batch)
                        receipt = dict(version=1, sessionId=session, sourceInstanceKey=identity['instance'], anatomyFingerprint=anatomy_hash,
                            sequence=batches, batchSha256=sha256(canonical.encode('utf-8')).hexdigest().upper())
                        assert record['receipt'] == receipt
                        replayed = batches % spec['retryEveryBatches'] == 0 or sample == 15000
                        blocked_check = batches % spec['backpressureEveryBatches'] == 0 or sample == 15000
                        assert record['replayed'] is replayed and record['blockedAdvanceChecked'] is blocked_check
                        retries += replayed; blocked += blocked_check
                        due = int(np.searchsorted(arrival_times, time, side='right'))
                        assert record['arrivals'] == arrivals[delivered:due]
                        assert record['receiverBodyTimeMs'] == record['receiverCompleteThroughBodyMs'] == time
                        assert record['receiverPendingEvents'] == upto - due and record['receiverDeliveredEvents'] == due
                        published, delivered = upto, due
                assert published == len(emission) and batches == len(samples)
                assert final['batches'] == outcome['batches'] == batches and final['retries'] == outcome['retries'] == retries
                assert final['blockedAdvanceChecks'] == outcome['blockedAdvanceChecks'] == blocked
                assert final['receiverDeliveredEvents'] == delivered and final['receiverPendingEvents'] == len(emission) - delivered
                assert final['receiverBodyTimeMs'] == final['receiverCompleteThroughBodyMs'] == final['publishedThroughNeuralMs'] == 1500
                assert final['unpublishedEmissions'] == 0 and outcome['crossings'] == len(emission) and outcome['observationPoints'] == 15001
                rows.append(dict(key=key, observationPoints=15001, inputContactDeliveries=total_inputs, crossings=len(emission),
                    batches=batches, exactRetries=retries, blockedAdvanceChecks=blocked,
                    receivedMotorEvents=len(emission), deliveredMotorEvents=delivered, retainedBeyondHorizon=len(emission) - delivered,
                    exactObservationStates=True, exactCrossings=True, exactReceiverDelivery=True))
                observation_total += 15001; input_total += total_inputs
    assert len(rows) == 36 and observation_total == 540036 and input_total == 1296 and excited_crossings
    return dict(completed=True, sourceConditions=36, observationPoints=observation_total, inputContactDeliveries=input_total,
        exactObservedStateAndDeliveryChecksPassed=True, cases=rows,
        quietHasNoCrossings=True, nontrivialExcitedCrossings=True,
        modelsExecutedDuringAudit=False, testsExecutedDuringAudit=False,
        independentNumericalAccuracyQualified=False, presynapticNeuronalAuthorityQualified=False,
        nativePhysicsExecuted=False, muscleDynamicsExecuted=False, physiologicalCalibrationComplete=False, avatarRecoveryAssessed=False)


if __name__ == '__main__':
    package = Path(sys.argv[1]).resolve(); result = audit(package)
    with (package / 'motor-output-analysis.json').open('x') as stream: json.dump(result, stream, indent=2, allow_nan=False)
    print(json.dumps({k: result[k] for k in ('completed', 'sourceConditions', 'observationPoints', 'inputContactDeliveries')}, indent=2), flush=True)

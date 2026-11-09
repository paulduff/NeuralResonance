"""Audit somatic-source/transport/muscle evidence; solve references only when explicitly requested in the visible window."""
import argparse
from hashlib import sha256
import importlib.util
import json
from pathlib import Path

import numpy as np
import scipy
from scipy.io import loadmat

read = lambda p: json.loads(p.read_text(encoding='utf-8-sig'))


def digest(path):
    result = sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(4 * 1024 * 1024), b''):
            result.update(block)
    return result.hexdigest().upper()


def module(path):
    spec = importlib.util.spec_from_file_location('isolated_calcium_reference', path)
    result = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result)
    return result


def lines(path):
    return [json.loads(line) for line in path.read_text(encoding='utf-8').splitlines()]


def conditions():
    return [dict(key=k, source=s, packetMs=p, refinement=r, retry=retry, disconnected=cut)
            for k, s, p, r, retry, cut in (
                ('packet-1', 'soma-6000', 1, 0, False, False),
                ('packet-10', 'soma-6000', 10, 0, False, False),
                ('packet-50', 'soma-6000', 50, 0, False, False),
                ('retry-10', 'soma-6000', 10, 0, True, False),
                ('refined-1', 'soma-6000', 10, 1, False, False),
                ('refined-2', 'soma-6000', 10, 2, False, False),
                ('quiet', 'quiet', 10, 0, False, False),
                ('disconnected', 'soma-6000', 10, 0, False, True))]


# Reuse the previously declared calcium thresholds, including its numerical
# refinement floor. These do not replace or loosen any motor-neuron criterion.
CRITERIA = dict(referenceAbsoluteState=1e-8, referenceAbsoluteFluxIntegral=1e-7,
                finestCandidateAbsoluteState=1e-6, finestCandidateForceNewtons=1e-6,
                calciumLedger=1e-9, refinementFraction=.2, refinementFloor=2e-8)


def preflight(package):
    from motor_boundary_protocol import protocol
    assert np.__version__ == '2.2.6' and scipy.__version__ == '1.16.3'
    root = package / 'reference'
    gate = read(root / 'boundary-analysis.json')
    assert all(gate[k] for k in ('completed', 'numericalCriteriaPassed', 'originalControlBytesReproduced', 'originalAnalysisReproduced'))
    assert not gate['originalBoundsChanged'] and len(gate['cases']) == 10 and all(c['passed'] for c in gate['cases'])
    assert read(root / 'boundary-input.json') == protocol()
    baseline = read(root / 'source-results.json')
    assert baseline['completed'] and baseline['successfulCases'] == 30
    metadata = read(root / 'source-metadata.json')
    assert metadata['clockScheduling'] == 'endpoint-difference'
    assert metadata['stateAccumulation'] == 'Compensated'
    for name in ('soma-6000', 'quiet'):
        key = name + '-h0.003125'
        saved = next(c for c in baseline['cases'] if c['key'] == key)
        assert saved['completed'] and saved['elapsedTimeErrorUnits'] == '0'
        trace = np.loadtxt(root / (key + '.csv'), delimiter=',', skiprows=1)
        assert trace.shape == (60001, 31) and np.isfinite(trace).all()
        assert np.array_equal(trace[:, 0], np.arange(60001) * .1)
    reference = module(package / 'reference-source/motor-calcium-reference.py')
    earlier = read(root / 'earlier-calcium-comparison.json')
    assert earlier['completed'] and earlier['numericalCriteriaPassed'] and earlier['criteria'] == CRITERIA
    mat = package / 'muscle-input/modelParameter.mat'
    assert digest(mat).lower() == reference.MAT_HASH
    data = loadmat(mat, simplify_cells=True)['modelParameter']
    parameters = np.r_[np.asarray(data['parameterMatrix'])[0][:12], np.asarray(data['PTi']).reshape(-1)[0]]
    assert parameters.shape == (13,) and np.isfinite(parameters).all()
    return reference, parameters


def audit(package, solve_references=False):
    reference, parameters = preflight(package)
    study = package / 'circuit-study'
    spec = read(study / 'protocol.json')
    report = read(study / 'results.json')
    assert spec['version'] == 'somatic-motor-unit-circuit-v1' and spec['conditions'] == conditions()
    assert spec['durationMs'] == 6000 and spec['sampleMs'] == .25 and spec['sourceSampleMs'] == .1
    assert spec['delayMs'] == 2.0625 and spec['neuronStepMs'] == .003125 and spec['sourceClock'] == 'endpoint-difference'
    assert spec['sourceOrigin'] == 'observed-somatic-threshold-crossing'
    assert spec['fixedDelayAbstraction'] and spec['idealEndplateTransmission'] and spec['referenceBath'] and spec['fixedOptimalLength']
    assert not spec['liveBrainAccessed'] and not spec['nativeNeuronExecuted']
    assert spec['sourceBaselineAnalysisSha256'] == digest(package / 'reference/boundary-analysis.json')
    assert spec['neuronParameters'] == read(package / 'reference/source-metadata.json')['parameters']
    assert np.array_equal(parameters, [spec['parameters'][name] for name in reference.NAMES])
    assert report['completed'] and report['successfulCases'] == report['expectedCases'] == len(report['cases']) == 8
    assert all(not report[k] for k in ('numericalCriteriaAssessed', 'axonalPropagationQualified', 'neuromuscularJunctionQualified',
                                      'physiologicalCalibrationComplete', 'liveMotorAuthorityQualified', 'avatarRecoveryAssessed'))
    baseline = read(package / 'reference/source-results.json')
    grid_ms = np.arange(24001) * .25
    traces, events, arrival_traces, case_summaries = {}, {}, {}, []
    for condition, row in zip(conditions(), report['cases']):
        key = condition['key']
        assert row['key'] == key and row['completed'] and row['sourceBytesReproduced']
        folder = study / key
        source_key = condition['source'] + '-h0.003125'
        original = next(c for c in baseline['cases'] if c['key'] == source_key)
        assert digest(folder / 'source.csv') == digest(package / 'reference' / (source_key + '.csv'))
        assert row['sourceCrossingsMs'] == original['crossingsMs']
        assert row['calls'] == original['calls'] and row['suppliedUnits'] == original['suppliedUnits']
        emitted = lines(folder / 'emissions.jsonl'); arrivals = lines(folder / 'arrivals.jsonl')
        assert len(emitted) == row['emittedEvents'] == len(original['crossingsMs'])
        assert len({e['spike']['messageId'] for e in emitted}) == len(emitted)
        for ordinal, (emission, time) in enumerate(zip(emitted, original['crossingsMs']), 1):
            spike = emission['spike']
            assert emission['ordinal'] == ordinal and emission['intentionallyDisconnected'] == condition['disconnected']
            assert spike['timestampMs'] == time and spike['sourceInstanceKey'] == spec['instance']
            assert spike['sourceNeuronId'] == spec['neuron'] and spike['projectionId'] == spec['projection']
            assert spike['synapseId'] == spec['synapse'] and spike['neurotransmitter'] == 5
            assert not spike['isSyntheticBackground'] and not spike['isFeedback']
        sent = [] if condition['disconnected'] else emitted
        due = [e for e in sent if e['spike']['timestampMs'] + spec['delayMs'] <= 6000]
        pending = [e['spike'] for e in sent if e['spike']['timestampMs'] + spec['delayMs'] > 6000]
        assert row['pending'] == pending and row['intentionallyDisconnected'] == (len(emitted) if condition['disconnected'] else 0)
        assert len(arrivals) == len(due)
        for arrival, emission in zip(arrivals, due):
            spike = emission['spike']
            assert arrival == dict(sessionId=row['clock']['sessionId'], sourceInstanceKey=spec['instance'],
                messageId=spike['messageId'], ordinal=emission['ordinal'], sourceNeuronId=spec['neuron'],
                projectionId=spec['projection'], sourceSynapseId=spec['synapse'], contactId=row['contact']['contactId'],
                motorUnitId=spec['unit'], firedAtNeuralMs=spike['timestampMs'], arrivalAtBodyMs=spike['timestampMs'] + spec['delayMs'])
        packets = lines(folder / 'packets.jsonl'); accepted = [p for p in packets if p['action'] == 'accept']
        retried = [p for p in packets if p['action'] == 'retry']
        assert len(accepted) == row['published'] == 6000 // condition['packetMs']
        expected_retries = len(accepted) // 7 if condition['retry'] else 0
        assert len(retried) == row['replays'] == row['delayedReceipts'] == row['blockedBodyAdvances'] == expected_retries
        assert len(packets) == len(accepted) + len(retried)
        packet_events = []
        for sequence, packet in enumerate(accepted, 1):
            batch, receipt = packet['batch'], packet['receipt']
            assert batch['sequence'] == receipt['sequence'] == sequence
            assert batch['sessionId'] == receipt['sessionId'] == row['clock']['sessionId']
            assert batch['version'] == receipt['version'] == 1
            assert batch['sourceInstanceKey'] == receipt['sourceInstanceKey'] == spec['instance']
            assert batch['anatomyFingerprint'] == receipt['anatomyFingerprint'] == row['final']['anatomyFingerprint']
            assert batch['completeThroughNeuralMs'] == sequence * condition['packetMs']
            for event in batch['spikes']:
                assert (sequence - 1) * condition['packetMs'] < event['firedAtNeuralMs'] <= sequence * condition['packetMs']
            packet_events.extend(batch['spikes'])
        for packet in retried:
            original_packet = accepted[packet['batch']['sequence'] - 1]
            assert packet['batch']['sequence'] % 7 == 0 and packet['batch'] == original_packet['batch'] and packet['receipt'] == original_packet['receipt']
        assert packet_events == [dict(messageId=e['spike']['messageId'], ordinal=e['ordinal'], sourceNeuronId=spec['neuron'],
            projectionId=spec['projection'], sourceSynapseId=spec['synapse'], firedAtNeuralMs=e['spike']['timestampMs'],
            neurotransmitter=5, isSyntheticBackground=False, isFeedback=False) for e in sent]
        final = row['final']; unit = final['units'][0]
        assert row['clock']['neuralEpochMs'] == row['clock']['bodyEpochMs'] == 0
        assert row['contact']['conductionDelayMs'] == spec['delayMs']
        assert row['contact']['sourceInstanceKey'] == spec['instance'] and row['contact']['sourceNeuronId'] == spec['neuron']
        assert row['contact']['projectionId'] == spec['projection'] and row['contact']['motorUnitId'] == spec['unit']
        assert unit['contactId'] == row['contact']['contactId'] and unit['motorUnitId'] == spec['unit']
        assert len(final['units']) == 1 and not final['faulted'] and final['fault'] is None
        assert final['bodyTimeMs'] == final['completeThroughBodyMs'] == unit['timeMs'] == 6000
        assert final['stepMs'] == .125 / 2 ** condition['refinement']
        assert final['completedTick'] * final['stepMs'] == 6000
        assert final['acceptedEvents'] == len(sent) and final['deliveredEvents'] == unit['arrivals'] == len(due)
        assert final['pendingEvents'] == len(pending)
        assert unit['lastMessageId'] == (due[-1]['spike']['messageId'] if due else '00000000-0000-0000-0000-000000000000')
        trace = np.loadtxt(folder / 'muscle.csv', delimiter=',', skiprows=1)
        assert trace.shape == (24001, 12) and np.isfinite(trace).all() and np.array_equal(trace[:, 0], grid_ms)
        assert np.array_equal(trace[-1, [0, 1, 2, 3, 5, 6, 7, 8, 9, 10, 11]],
            [unit[n] for n in ('timeMs', 'releaseDrive', 'freeCalcium', 'boundCalcium', 'activation', 'forceNewtons',
                              'releasedCalcium', 'recoveredCalcium', 'netBoundCalcium', 'maximumCalciumLedgerError', 'arrivals')])
        assert np.array_equal(trace[:, 11], np.searchsorted([a['arrivalAtBodyMs'] for a in arrivals], grid_ms, side='right'))
        assert np.min(trace[:, 2:6]) >= 0 and np.max(trace[:, [3, 5]]) <= 1
        assert np.max(np.abs(trace[:, 4] + trace[:, 2] + trace[:, 3] - parameters[1])) <= 1e-12
        assert np.max(np.abs(trace[:, 6] - trace[:, 5] * parameters[-1])) <= 1e-14
        ledger = max(float(np.max(trace[:, 10])), float(np.max(np.abs(trace[:, 2] - trace[:, 7] + trace[:, 8] + trace[:, 9]))),
                     float(np.max(np.abs(trace[:, 3] - trace[:, 9]))))
        assert ledger <= CRITERIA['calciumLedger']
        direct_release = np.zeros(len(grid_ms))
        for arrival in arrivals:
            chosen = grid_ms >= arrival['arrivalAtBodyMs']; age = (grid_ms[chosen] - arrival['arrivalAtBodyMs']) / 1000
            direct_release[chosen] += -np.expm1(-age / parameters[6]) * np.exp(-age / parameters[7])
        release_error = float(np.max(np.abs(trace[:, 1] - direct_release)))
        assert release_error <= 1e-10
        if key in ('quiet', 'disconnected'):
            assert not arrivals and np.all(trace[:, [1, 2, 3, 5, 6, 7, 8, 9, 10, 11]] == 0)
        else:
            assert len(arrivals) > 0 and float(np.max(trace[:, 6])) > 0
            assert np.all(trace[grid_ms <= arrivals[0]['arrivalAtBodyMs'], 6] == 0)
        if key == 'quiet': assert not emitted
        if key == 'disconnected': assert len(emitted) > 0
        traces[key], events[key], arrival_traces[key] = trace, emitted, arrivals
        case_summaries.append(dict(key=key, sourceBytesReproduced=True, emitted=len(emitted), delivered=len(due),
            pending=len(pending), intentionalCuts=row['intentionallyDisconnected'], retries=expected_retries,
            peakForceNewtons=float(np.max(trace[:, 6])), calciumLedger=ledger, releaseWaveformError=release_error))
    for key in ('packet-10', 'packet-50', 'retry-10', 'refined-1', 'refined-2', 'disconnected'):
        assert [e['spike'] for e in events[key]] == [e['spike'] for e in events['packet-1']]
    for key in ('packet-10', 'packet-50', 'retry-10'):
        assert np.array_equal(traces[key], traces['packet-1'])
    for key in ('packet-10', 'packet-50', 'retry-10', 'refined-1', 'refined-2'):
        without_session = lambda rows: [{k: v for k, v in a.items() if k != 'sessionId'} for a in rows]
        assert without_session(arrival_traces[key]) == without_session(arrival_traces['packet-1'])
    ref_dir = package / 'circuit-reference'
    if solve_references: ref_dir.mkdir()  # A rerun must use a fresh package.
    grid = grid_ms / 1000
    arrivals = np.asarray([a['arrivalAtBodyMs'] for a in arrival_traces['packet-1']]) / 1000
    references = {}
    for key, method, rtol, atol in reference.SETTINGS:
        path = ref_dir / (key + '.npz')
        if solve_references:
            print('CALCIUM REFERENCE', key, flush=True)
            state, statistics = reference.solve(parameters, arrivals, grid, method, rtol, atol)
            np.savez_compressed(path, timeSeconds=grid, state=state, arrivalsSeconds=arrivals)
            (ref_dir / (key + '.json')).write_text(json.dumps(dict(method=method, rtol=rtol, atol=atol, statistics=statistics), indent=2) + '\n')
        with np.load(path) as saved:
            assert np.array_equal(saved['timeSeconds'], grid) and np.array_equal(saved['arrivalsSeconds'], arrivals)
            state = saved['state'].copy()
        assert state.shape == (24001, 6) and np.isfinite(state).all()
        assert np.min(state[:, :3]) >= -1e-12 and np.max(state[:, 1:3]) <= 1 + 1e-12
        assert np.min(parameters[1] - state[:, 0] - state[:, 1]) >= -1e-12
        references[key] = state
    contrasts = []
    for left, right in (('dop853', 'dop853-tight'), ('dop853-tight', 'radau')):
        error = reference.maximum_difference(references[left], references[right])
        passed = max(error[n] for n in reference.STATE_NAMES[:3]) <= CRITERIA['referenceAbsoluteState'] and max(error[n] for n in reference.STATE_NAMES[3:]) <= CRITERIA['referenceAbsoluteFluxIntegral']
        contrasts.append(dict(left=left, right=right, maximumAbsolute=error, passed=bool(passed)))
    candidates = []
    for key in ('packet-10', 'refined-1', 'refined-2'):
        trace = traces[key]; error = reference.maximum_difference(trace[:, [2, 3, 5, 7, 8, 9]], references['dop853-tight'])
        candidates.append(dict(key=key, maximumAbsolute=error, stateError=max(error[n] for n in reference.STATE_NAMES[:3]),
            forceErrorNewtons=float(np.max(np.abs(trace[:, 6] - references['dop853-tight'][:, 2] * parameters[-1])))))
    finest = candidates[-1]
    refinement = finest['stateError'] <= CRITERIA['refinementFraction'] * candidates[0]['stateError'] + CRITERIA['refinementFloor']
    numerical = (all(c['passed'] for c in contrasts) and refinement and finest['stateError'] <= CRITERIA['finestCandidateAbsoluteState']
                 and finest['forceErrorNewtons'] <= CRITERIA['finestCandidateForceNewtons'])
    return dict(completed=True, criteria=CRITERIA, cases=case_summaries, allSourceBytesReproduced=True,
        originalEventsPreserved=True, packetAndRetryInvariancePassed=True, disconnectionControlPassed=True,
        referenceContrasts=contrasts, candidates=candidates, refinementPassed=bool(refinement), numericalCriteriaPassed=bool(numerical),
        physiologicalCalibrationComplete=False, activeAxonQualified=False, neuromuscularJunctionQualified=False,
        liveMotorAuthorityQualified=False, avatarRecoveryAssessed=False)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('package', type=Path)
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument('--preflight', action='store_true')
    mode.add_argument('--solve-reference', action='store_true')
    mode.add_argument('--audit-saved', action='store_true')
    args = parser.parse_args(); package = args.package.resolve()
    if args.preflight:
        preflight(package); print('Source prerequisite and pinned muscle parameters verified; no model executed.')
    else:
        result = audit(package, args.solve_reference)
        if args.audit_saved:
            assert result == read(package / 'circuit-analysis.json')
            print('Saved circuit evidence re-audited without integrating any model.')
        else:
            with (package / 'circuit-analysis.json').open('x', encoding='utf-8') as stream:
                stream.write(json.dumps(result, indent=2, allow_nan=False) + '\n')
            if not result['numericalCriteriaPassed']:
                raise RuntimeError('Circuit numerical criteria failed; preserve evidence and original bounds.')
            print('Isolated integration checks passed. Physiology, live motor authority and Avatar recovery remain unqualified.')

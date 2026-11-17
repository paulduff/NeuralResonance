"""Independent reduced-equation comparison. Execute only in the visible study.

The matrix exponential and ODE derivative do not import the C# transition.
Saved-data audits may be rerun after an operator completion report; this CLI
does not itself grant permission to inspect any pending study.
"""
import argparse
from datetime import datetime, timezone
from hashlib import sha256
import importlib.util
import json
import os
from pathlib import Path
import sys

for key in ('OMP_NUM_THREADS', 'OPENBLAS_NUM_THREADS', 'MKL_NUM_THREADS', 'NUMEXPR_NUM_THREADS'):
    os.environ[key] = '1'
sys.path.insert(0, str(Path(__file__).resolve().parent))
from vestibular_numerics_protocol import protocol, COUNTS, CHANNELS, FIELDS, FALSE_CLAIMS
import numpy as np
from scipy.linalg import expm
from scipy.integrate import solve_ivp

ROOT = Path(__file__).resolve().parents[1]
read = lambda p: json.loads(p.read_text(encoding='utf-8-sig'))


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec); spec.loader.exec_module(result); return result


def save(path, value):
    with path.open('x', encoding='utf-8') as stream:
        json.dump(value, stream, indent=2, allow_nan=False); stream.write('\n')


def digest_text(value):
    return sha256(value.encode('utf-8')).hexdigest().upper()


def environment():
    return module('vestibular_environment', ROOT / 'tools/golgi-reference-resume.py').environment()


def preflight(package):
    spec = read(package / 'vestibular-input.json')
    assert spec == protocol() and sum(COUNTS.values()) == 62
    current = environment()
    assert current == read(package / 'preparation-environment.json'), 'Numerical dependencies changed before the study.'
    save(package / 'reference-preflight.json', dict(environment=current,
        completedUtc=datetime.now(timezone.utc).isoformat(), modelsExecuted=False, testsExecuted=False))


def postflight(package):
    current = environment(); unchanged = current == read(package / 'reference-preflight.json')['environment']
    save(package / 'reference-postflight.json', dict(environment=current, unchanged=unchanged,
        completedUtc=datetime.now(timezone.utc).isoformat()))
    assert unchanged, 'Numerical dependencies changed during the study.'


def vectors(values):
    pitch, yaw, roll, right, down, forward = values
    # Independent projection matrix, with the documented inherited axis ordering.
    s = 1 / np.sqrt(2.)
    projection = np.array([[0, 1, 0], [0, -1, 0], [s, 0, -s], [s, 0, s], [-s, 0, -s], [-s, 0, s]])
    return projection @ np.array([pitch, yaw, roll]), np.array([right, down, forward])


def affine_matrix(values, constants):
    tc, ta, to = constants; u, v = vectors(values)
    matrix = np.zeros((16, 16))
    for i in range(6):
        matrix[i, i] = -1 / tc; matrix[i, 6+i] = -1 / tc; matrix[i, 15] = u[i] / tc
        matrix[6+i, 6+i] = -1 / ta; matrix[6+i, 15] = u[i] / ta
    for i in range(3):
        matrix[12+i, 12+i] = -1 / to; matrix[12+i, 15] = v[i] / to
    return matrix


def derivative(values, constants):
    tc, ta, to = constants; u, v = vectors(values)
    def rhs(_time, y):
        return np.concatenate(((u - y[6:12] - y[:6]) / tc,
                               (u - y[6:12]) / ta, (v - y[12:15]) / to))
    return rhs


def initial(values):
    return np.r_[np.zeros(12), np.asarray(values[3:], dtype=float)]


def fixtures(case):
    changes = case['changes']
    return np.array([next(c['values'] for c in reversed(changes) if c['endpointIndex'] <= i)
                     for i in range(case['intervals'] + 1)], dtype=float)


def controls(package):
    spec = read(package / 'vestibular-input.json'); constants = spec['timeConstantsSeconds']
    tc, ta, to = constants; bound = spec['bounds']['controlAbsolute']; rows = []
    def check(key, actual, expected, details):
        error = float(np.max(np.abs(np.asarray(actual) - np.asarray(expected))))
        rows.append(dict(key=key, error=error, bound=bound, passed=error <= bound, **details))
    zero = [0.] * 6; h = .013
    matrix = affine_matrix(zero, constants)
    check('zero', (expm(matrix*h) @ np.r_[np.zeros(15), 1])[:15], np.zeros(15), dict(seconds=h))
    value = [.3, -.8, .2, 2., 7., -1.]; u, v = vectors(value)
    equilibrium = np.r_[np.zeros(6), u, v, 1.]
    check('equilibrium', expm(affine_matrix(value, constants)*h) @ equilibrium, equilibrium,
          dict(seconds=h, input=value, initial=equilibrium.tolist()))
    state = np.zeros(16); state[0] = 2.; state[15] = 1.
    expected = np.zeros(15); expected[0] = 2*np.exp(-h/tc)
    check('cupula-decay', (expm(matrix*h) @ state)[:15], expected,
          dict(seconds=h, initial=state.tolist()))
    state = np.zeros(16); state[6] = 2.; state[15] = 1.
    check('adaptation-decay', (expm(matrix*h) @ state)[6:12], [2*np.exp(-h/ta), 0, 0, 0, 0, 0],
          dict(seconds=h, initial=state.tolist(), checkedCoordinates='adaptation only'))
    state = np.r_[np.zeros(12), [1., -2., 3.], 1.]
    expected = np.r_[np.zeros(12), state[12:15]*np.exp(-h/to)]
    check('otolith-relaxation', (expm(matrix*h) @ state)[:15], expected,
          dict(seconds=h, initial=state.tolist()))
    matrix = affine_matrix(value, constants); state = np.r_[np.arange(15)/15, 1.]
    check('matrix-subdivision', expm(matrix*.051) @ state,
          expm(matrix*.032) @ (expm(matrix*.019) @ state),
          dict(input=value, initial=state.tolist(), seconds=[.019, .032, .051]))
    save(package / 'reference-controls.json', dict(completed=True, controls=rows,
        passed=len(rows) == 6 and all(r['passed'] for r in rows)))
    assert len(rows) == 6 and all(r['passed'] for r in rows), 'Reference controls failed; retain original bounds.'


def reference(package):
    spec = read(package / 'vestibular-input.json'); constants = spec['timeConstantsSeconds']
    output = package / 'reference'; output.mkdir()
    for case in spec['cases']:
        key = case['key']; n = case['intervals']; dt = case['stepMs'] / 1000
        values = fixtures(case); times = np.arange(n+1)*dt
        states = np.empty((n+1, 15)); states[0] = initial(values[0]); matrices = {}
        for i in range(n):
            held = tuple(values[i])
            if held not in matrices: matrices[held] = expm(affine_matrix(held, constants)*dt)
            advanced = matrices[held] @ np.r_[states[i], 1.]
            assert advanced[15] == 1 and np.isfinite(advanced).all()
            states[i+1] = advanced[:15]
        np.savez_compressed(output / (key+'-expm.npz'), times=times, states=states, inputs=values,
                            matrices=np.array(list(matrices.values())), matrixInputs=np.array(list(matrices)))
        for level in spec['referenceLevels']:
            states = np.empty((n+1, 15)); states[0] = initial(values[0]); metadata = []
            nodes = []; accepted = []; segments = []
            boundaries = [c['endpointIndex'] for c in case['changes']] + [n]
            for segment, (start, end) in enumerate(zip(boundaries, boundaries[1:])):
                assert end > start
                span = (float(times[start]), float(times[end]))
                solution = solve_ivp(derivative(values[start], constants), span, states[start].copy(),
                    method='DOP853', rtol=level['rtol'], atol=level['atol'],
                    max_step=min(level['maxStepSeconds'], span[1]-span[0]), dense_output=True)
                # Preserve accepted output, including a failed prefix, before checking success.
                nodes.extend(solution.t.tolist()); accepted.extend(solution.y.T.tolist())
                segments.extend([segment]*len(solution.t))
                np.savez_compressed(output / (key+'-'+level['key']+'-accepted.npz'),
                    times=np.asarray(nodes), states=np.asarray(accepted), segments=np.asarray(segments))
                metadata.append(dict(segment=segment, start=start, end=end, input=values[start].tolist(),
                    success=solution.success, message=solution.message, nfev=solution.nfev,
                    acceptedNodes=len(solution.t), acceptedEnd=float(solution.t[-1])))
                if not solution.success:
                    save(output / (key+'-'+level['key']+'-failure.json'), dict(segments=metadata))
                    raise RuntimeError('Independent ODE failed; preserve its original prefix.')
                assert solution.t[-1] == span[1] and np.isfinite(solution.y).all()
                states[start+1:end+1] = solution.sol(times[start+1:end+1]).T
                # Continuation uses the accepted endpoint, not dense interpolation.
                states[end] = solution.y[:, -1]
            assert np.isfinite(states).all()
            np.savez_compressed(output / (key+'-'+level['key']+'.npz'), times=times, states=states, inputs=values)
            save(output / (key+'-'+level['key']+'.json'), dict(case=key, level=level,
                segments=metadata, acceptedNodes=len(nodes), completed=True))
        print('Independent vestibular references: '+key, flush=True)


def state_vector(state):
    return [state['cupula'][c] for c in CHANNELS] + [state['adaptation'][c] for c in CHANNELS] + \
        [state['otolith'][c] for c in ('right', 'down', 'forward')]


def candidate(package, case, result):
    values = fixtures(case); n = case['intervals']; clock = result['clock']
    assert result['completed'] and result['deliveryGroupingIdentical'] and result['prescribedKinematics']
    assert result['condition'] == case
    assert clock['initialPhysicsStep'] == case['initialStep'] and clock['physicsStepMs'] == case['stepMs']
    assert clock['physicalEpochMs'] == case['physicalEpochMs'] and clock['neuralEpochMs'] == case['neuralEpochMs']
    paths = [package / 'study' / case['key'] / f'delivery-{g}.jsonl' for g in (1, 17)]
    assert paths[0].read_bytes() == paths[1].read_bytes(), 'Grouping changed original inputs/receipts/states.'
    original = paths[0].read_bytes(); records = [json.loads(line) for line in original.splitlines()]
    assert len(records) == n+1
    fingerprint = None; identities = set(); output = []
    for i, record in enumerate(records):
        assert record['index'] == i
        observation = json.loads(record['observationJson']); source = record['input']; receipt = record['receipt']; state = receipt['state']
        assert observation['fixture'] and observation['caseKey'] == case['key'] and observation['endpointIndex'] == i
        assert observation['values'] == values[i].tolist()
        assert [source[f] for f in FIELDS] == values[i].tolist()
        original_input = json.loads(record['inputJson'])
        assert {k[0].lower()+k[1:]: v for k, v in original_input.items()} == source
        assert source['physicalObservationSha256'] == digest_text(record['observationJson'])
        assert receipt['inputSha256'] == digest_text(record['inputJson'])
        assert receipt['sessionId'] == clock['sessionId']
        assert source['physicalSampleId'] == receipt['physicalSampleId'] and source['physicalSampleId'] not in identities
        identities.add(source['physicalSampleId'])
        assert source['physicsStep'] == receipt['physicsStep'] == state['completedThroughStep'] == case['initialStep']+i
        assert state['physicalTimeMs'] == case['physicalEpochMs']+i*case['stepMs']
        assert state['neuralTimeMs'] == case['neuralEpochMs']+i*case['stepMs']
        assert state['integratedIntervals'] == i and state['version'] == protocol()['candidateVersion']
        assert not state['faulted'] and state['faultReason'] is None
        if fingerprint is None: fingerprint = state['fingerprint']
        assert state['fingerprint'] == fingerprint and len(fingerprint) == 64
        output.append(state_vector(state))
    assert len(result['deliveries']) == 2
    for group, delivery in zip((1, 17), result['deliveries']):
        assert delivery['group'] == group and delivery['records'] == delivery['retries'] == n+1
        assert delivery['sha256'] == sha256(original).hexdigest().upper()
        assert delivery['finalState'] == records[-1]['receipt']['state']
    output = np.asarray(output)
    assert np.isfinite(output).all() and np.array_equal(output[0], initial(values[0]))
    return output


def compare(package):
    spec = read(package / 'vestibular-input.json'); assert spec == protocol()
    tests = module('vestibular_test_gate', ROOT / 'tools/receptor_membrane_gate.py').check_tests(package, COUNTS)
    control = read(package / 'reference-controls.json'); assert control['passed'] and len(control['controls']) == 6
    native = read(package / 'study/results.json')
    assert native['completed'] and native['mechanicalModelExecuted'] and len(native['cases']) == 20
    assert native['candidateVersion'] == spec['candidateVersion']
    assert all(native[k] is False for k in FALSE_CLAIMS)
    rows = []; saved = {}; reference_rows = []; candidate_rows = []; total_records = 0; accepted_nodes = 0
    def contrast(a, b, bound):
        difference = np.abs(a-b); maximum = float(np.max(difference))
        return dict(maximumAbsolute=maximum, cupula=float(np.max(difference[:, :6])),
            adaptation=float(np.max(difference[:, 6:12])), otolith=float(np.max(difference[:, 12:15])),
            bound=bound, passed=maximum <= bound)
    for case, result in zip(spec['cases'], native['cases']):
        key = case['key']; assert result['key'] == key
        assert result == read(package / 'study' / key / 'result.json')
        observed = candidate(package, case, result); saved[key] = observed; total_records += 2*len(observed)
        refs = []
        for level in ['expm'] + [r['key'] for r in spec['referenceLevels']]:
            with np.load(package / 'reference' / (key+'-'+level+'.npz'), allow_pickle=False) as data:
                assert np.array_equal(data['inputs'], fixtures(case))
                assert np.array_equal(data['times'], np.arange(case['intervals']+1)*(case['stepMs']/1000))
                assert data['states'].shape == observed.shape and np.isfinite(data['states']).all()
                refs.append(data['states'].copy())
            if level != 'expm':
                meta = read(package / 'reference' / (key+'-'+level+'.json'))
                assert meta['completed'] and meta['case'] == key and all(s['success'] for s in meta['segments'])
                with np.load(package / 'reference' / (key+'-'+level+'-accepted.npz'), allow_pickle=False) as accepted:
                    assert len(accepted['times']) == meta['acceptedNodes'] and np.isfinite(accepted['states']).all()
                    assert len(accepted['segments']) == len(accepted['times'])
                    for segment, row in enumerate(meta['segments']):
                        mask = accepted['segments'] == segment
                        assert int(mask.sum()) == row['acceptedNodes']
                        t = accepted['times'][mask]; y = accepted['states'][mask]
                        assert np.all(np.diff(t) > 0) and t[-1] == row['acceptedEnd']
                        assert np.array_equal(y[0], refs[-1][row['start']])
                        assert np.array_equal(y[-1], refs[-1][row['end']])
                    accepted_nodes += meta['acceptedNodes']
        for first, second in ((0, 1), (0, 2), (1, 2)):
            reference_rows.append(dict(case=key, pair=[first, second], **contrast(refs[first], refs[second], spec['bounds']['referenceAbsolute'])))
        for index, ref in enumerate(refs):
            candidate_rows.append(dict(case=key, reference=index, **contrast(observed, ref, spec['bounds']['candidateAbsolute'])))
        rows.append(dict(case=key, endpoints=len(observed), deliveryGroups=2, originalInputAndStateRecordsReconciled=True))
    subdivisions = []
    for group in sorted({c['group'] for c in spec['cases'] if c['group'] not in ('tiny-step', 'long-step')}):
        fine = saved[group+'-125us']
        for suffix, stride in (('1ms', 8), ('4ms', 32)):
            subdivisions.append(dict(group=group, coarse=suffix,
                **contrast(fine[::stride], saved[group+'-'+suffix], spec['bounds']['subdivisionAbsolute'])))
    passed = all(r['passed'] for r in reference_rows+candidate_rows+subdivisions)
    analysis = dict(completed=True, numericalCriteriaPassed=passed, testInventory=tests, controls=6,
        cases=rows, nativeCaptures=40, originalRecords=total_records, exactRetries=total_records,
        referenceTrajectories=60, odeAcceptedNodes=accepted_nodes, referenceContrasts=reference_rows,
        candidateContrasts=candidate_rows, subdivisionContrasts=subdivisions, originalBoundsChanged=False,
        nativePhysiologyQualified=False, continuousHeadMotionAccuracyQualified=False,
        **{k: False for k in FALSE_CLAIMS})
    assert len(reference_rows) == len(candidate_rows) == 60 and len(subdivisions) == 12
    save(package / 'analysis.json', analysis)
    if not passed: raise RuntimeError('Vestibular numerical criteria failed; preserve results and original bounds.')
    print('20 prescribed conditions, 40 native captures and 60 independent references reconciled.', flush=True)


if __name__ == '__main__':
    if not __debug__: raise RuntimeError('Assertions must remain enabled.')
    parser = argparse.ArgumentParser(); parser.add_argument('package', type=Path)
    parser.add_argument('--stage', required=True, choices=['preflight', 'controls', 'reference', 'compare', 'postflight', 'check-tests'])
    args = parser.parse_args(); package = args.package.resolve()
    if args.stage == 'check-tests':
        print(module('vestibular_gate', ROOT / 'tools/receptor_membrane_gate.py').check_tests(package, COUNTS))
    else: globals()[args.stage](package)

"""Coupled mechanical comparison. Solves run only in the visible PowerShell rig."""
import argparse
import importlib.util
import json
import os
from pathlib import Path
import traceback

for name in ('OMP_NUM_THREADS', 'OPENBLAS_NUM_THREADS', 'MKL_NUM_THREADS', 'NUMEXPR_NUM_THREADS'):
    os.environ[name] = '1'
import numpy as np
from causal_motor_protocol import make_protocol
from causal_motor_reference import CoupledReference, tendon_energy_change

read = lambda p: json.loads(p.read_text(encoding='utf-8-sig'))


def save(path, value):
    with path.open('x', encoding='utf-8') as stream:
        stream.write(json.dumps(value, indent=2, allow_nan=False) + '\n')


def environment(package):
    spec = importlib.util.spec_from_file_location('dependency_capture', package / 'source/tools/golgi-reference-resume.py')
    module = importlib.util.module_from_spec(spec); spec.loader.exec_module(module)
    return module.environment()


def validate(package):
    spec = read(package / 'causal-input.json')
    assert spec == make_protocol(package / 'reference/modelParameter.mat', package / 'reference/arrivals.jsonl')
    prerequisite = read(package / 'reference/causal-component-review.json')
    assert prerequisite['operatorReportedCompletion'] and prerequisite['passedTests'] == 120
    assert prerequisite['exactQualifiedCorePreserved']
    return spec


def preflight(package):
    spec = validate(package)
    save(package / 'reference-preflight.json', dict(environment=environment(package), protocol=spec,
                                                  completed=True, modelExecuted=False))


def solve(package):
    spec = validate(package)
    assert environment(package) == read(package / 'reference-preflight.json')['environment']
    output = package / 'causal-reference'; output.mkdir()
    records = []
    for case in spec['cases']:
        grid = np.arange(round(case['durationMs'] / spec['referenceSampleMs']) + 1) * spec['referenceSampleMs'] / 1000
        for settings in spec['referenceSettings']:
            key = case['key'] + '-' + settings['key']; print('COUPLED REFERENCE', key, flush=True)
            ref = CoupledReference(spec, case)
            try:
                state, forces, statistics = ref.solve(settings, grid)
                np.savez_compressed(output / (key + '.npz'), timeSeconds=grid, state=state, forces=forces)
                statistics = dict(completed=True, **statistics)
            except Exception:
                partial = ref.partial
                np.savez_compressed(output / (key + '-partial.npz'), timeSeconds=grid,
                    partialSamples=partial['samples'], internalTimes=partial['internalTimes'], internalStates=partial['internalStates'])
                statistics = dict(completed=False, completedBoundarySeconds=partial['completedBoundarySeconds'], error=traceback.format_exc())
                print(statistics['error'], flush=True)
            save(output / (key + '-statistics.json'), statistics)
            records.append(dict(key=key, **statistics))
    save(output / 'execution.json', dict(completed=len(records) == 24, successfulReferences=sum(r['completed'] for r in records),
                                        references=records, physiologicalCalibrationComplete=False))


def errors(a, b, af, bf):
    difference = np.max(abs(a - b), axis=0)
    result = dict(jointAngleRadians=float(difference[0]), jointSpeedRadiansPerSecond=float(difference[1]),
                  jointImpulseNewtonMeterSeconds=float(difference[2]), jointWorkJoules=float(difference[3]))
    for i in range(2):
        d = difference[4 + 11 * i:15 + 11 * i]; f = np.max(abs(af[:, i] - bf[:, i]), axis=0)
        local = dict(fiberLengthMeters=d[0], fiberVelocityMetersPerSecond=d[1], yieldFraction=d[2],
            calcium=max(d[3:5]), activation=d[5], fluxIntegral=max(d[6:9]), forceNewtons=max(f[:6]),
            accelerationMetersPerSecondSquared=f[6], impulseNewtonSeconds=d[9], workJoules=d[10])
        for name, value in local.items():
            result[name] = max(result.get(name, 0.), float(value))
    return result


def within(error, bounds):
    assert error.keys() == bounds.keys()
    return all(error[name] <= bound for name, bound in bounds.items())


def accounting(spec, case, y):
    g = spec['geometry']; cosine = np.cos(g['pennationRadians']); inertia = spec['inertiaKgMetersSquared']
    result = dict(calciumLedger=0., fiberMomentumLedger=0., fiberKineticLedger=0.,
        jointMomentumLedger=float(np.max(abs(inertia * (y[:, 1] - case['initialSpeedRadiansPerSecond']) - y[:, 2]))),
        jointKineticLedger=float(np.max(abs(.5 * inertia * (y[:, 1] ** 2 - case['initialSpeedRadiansPerSecond'] ** 2) - y[:, 3]))))
    for i in range(2):
        p = y[:, 4 + i * 11:15 + i * 11]
        result['calciumLedger'] = max(result['calciumLedger'], float(np.max(abs(p[:, 3] - p[:, 6] + p[:, 7] + p[:, 8]))), float(np.max(abs(p[:, 4] - p[:, 8]))))
        result['fiberMomentumLedger'] = max(result['fiberMomentumLedger'], float(np.max(abs(g['fiberMassKg'] * p[:, 1] - p[:, 9]))))
        result['fiberKineticLedger'] = max(result['fiberKineticLedger'], float(np.max(abs(.5 * g['fiberMassKg'] * p[:, 1] ** 2 - p[:, 10]))))
    result['passed'] = all(result[name] <= spec[bound] for name, bound in (
        ('calciumLedger', 'calciumLedgerAbsolute'), ('fiberMomentumLedger', 'fiberMomentumLedgerNewtonSeconds'),
        ('fiberKineticLedger', 'fiberKineticLedgerJoules'), ('jointMomentumLedger', 'jointMomentumLedgerNewtonMeterSeconds'),
        ('jointKineticLedger', 'jointKineticLedgerJoules')))
    return result


def reference_energy(spec, case, y):
    g = spec['geometry']; cosine = np.cos(g['pennationRadians']); worst_quadrature = worst_ledger = 0.
    for i in range(2):
        initial = spec['restPathLengthMeters'] - spec['momentArmsMeters'][i] * case['initialAngleRadians'] - cosine * spec['initialFiberLengthMeters']
        tendon = spec['restPathLengthMeters'] - spec['momentArmsMeters'][i] * y[:, 0] - cosine * y[:, 4 + i * 11]
        low = tendon_energy_change(g, initial, tendon, 64); high = tendon_energy_change(g, initial, tendon, 128)
        worst_quadrature = max(worst_quadrature, float(np.max(abs(high - low))))
        worst_ledger = max(worst_ledger, float(np.max(abs(high - y[:, 26 + i]))))
    return dict(quadratureErrorJoules=worst_quadrature, tendonEnergyLedgerJoules=worst_ledger,
        passed=worst_quadrature <= spec['tendonQuadratureAgreementJoules'] and worst_ledger <= spec['referenceTendonEnergyLedgerJoules'])


def candidate_data(path):
    raw = np.atleast_1d(np.genfromtxt(path, names=True, delimiter=',')); n = len(raw)
    y = np.zeros((n, 28)); f = np.empty((n, 2, 7))
    for index, name in enumerate(('jointAngleRadians', 'jointSpeedRadiansPerSecond', 'jointImpulseNewtonMeterSeconds', 'jointWorkJoules')):
        y[:, index] = raw[name]
    names = ('fiberLengthMeters', 'fiberVelocityMetersPerSecond', 'yieldFraction', 'freeCalcium', 'boundCalcium',
             'activation', 'released', 'recovered', 'netBound', 'impulseNewtonSeconds', 'workJoules')
    force_names = ('activeFiberForceNewtons', 'passiveFiberForceNewtons', 'tendonAxialForceNewtons', 'projectedTendonForceNewtons',
                   'projectedFiberForceNewtons', 'geometricForceNewtons', 'accelerationMetersPerSecondSquared')
    for i in range(2):
        for j, name in enumerate(names):
            y[:, 4 + 11 * i + j] = raw['m' + str(i) + '_' + name]
        for j, name in enumerate(force_names):
            f[:, i, j] = raw['m' + str(i) + '_' + name]
    assert all(np.isfinite(raw[name]).all() for name in raw.dtype.names)
    return raw, y, f


def boundary_energy(spec, raw, path):
    rows = np.atleast_1d(np.genfromtxt(path, names=True, delimiter=','))
    assert len(rows) == len(raw) - 1 and np.array_equal(rows['timeMs'], raw['timeMs'][1:])
    assert all(np.isfinite(rows[name]).all() for name in rows.dtype.names)
    g = spec['geometry']; cosine = np.cos(g['pennationRadians'])
    low = rows['bodyWorkJoules'].copy(); high = low.copy(); work = np.zeros(len(rows))
    for i in range(2):
        m = 'm' + str(i)
        assert np.array_equal(rows[m + '_startLength'], raw[m + '_totalLengthMeters'][:-1])
        assert np.array_equal(rows[m + '_endLength'], raw[m + '_totalLengthMeters'][1:])
        assert np.array_equal(rows[m + '_endFiber'], raw[m + '_fiberLengthMeters'][1:])
        assert np.array_equal(rows[m + '_startForce'], raw[m + '_tendonAxialForceNewtons'][:-1])
        tendon_start = rows[m + '_startLength'] - cosine * rows[m + '_endFiber']
        tendon_end = rows[m + '_endLength'] - cosine * rows[m + '_endFiber']
        low += tendon_energy_change(g, tendon_start, tendon_end, 64)
        high += tendon_energy_change(g, tendon_start, tendon_end, 128)
        work += rows[m + '_startForce'] * (rows[m + '_startLength'] - rows[m + '_endLength'])
    quadrature = float(np.max(abs(np.cumsum(high) - np.cumsum(low))))
    virtual_work = float(np.max(abs(work - rows['bodyWorkJoules'])))
    work_ledger = float(np.max(abs(np.cumsum(rows['bodyWorkJoules']) - raw['boundaryWorkJoules'][1:])))
    return dict(maximumCumulativeDefectJoules=float(np.max(abs(np.cumsum(high)))),
        totalAbsoluteDefectJoules=float(np.sum(abs(high))), quadratureErrorJoules=quadrature,
        virtualWorkErrorJoules=virtual_work, recordedBoundaryWorkErrorJoules=work_ledger,
        accountingPassed=quadrature <= spec['tendonQuadratureAgreementJoules'] and
            max(virtual_work, work_ledger) <= spec['jointKineticLedgerJoules'])


def audit(package):
    spec = validate(package); assert environment(package) == read(package / 'reference-preflight.json')['environment']
    controls = read(package / 'reference-controls.json')
    assert controls['completed'] and controls['passed'] and controls['exactAnalyticMechanicalFixtures']
    assert controls['analyticMechanicalConditions'] == 2 and controls['analyticMethodChecks'] == 4 and controls['tendonQuadratureCases'] == 4
    assert not controls['biologicalDynamicsIntegrated'] and len(controls['mechanics']) == len(controls['quadrature']) == 4
    assert all(row['passed'] for row in controls['mechanics'] + controls['quadrature'])
    candidate = package / 'causal-study'; reference = package / 'causal-reference'
    executed = read(candidate / 'results.json'); refs_executed = read(reference / 'execution.json')
    assert executed['completed'] and executed['successfulCases'] == 40 and len(executed['cases']) == 40
    assert refs_executed['completed'] and len(refs_executed['references']) == 24
    assert read(candidate / 'protocol.json') == spec
    originals = [json.loads(line) for line in (package / 'reference/arrivals.jsonl').read_text().splitlines()]
    cases = []
    for case in spec['cases']:
        result = dict(case=case['key'], references=[], referenceContrasts=[], candidates=[], refinement=[], passed=False)
        cases.append(result); ref = CoupledReference(spec, case)
        expected_ms = np.arange(round(case['durationMs'] / spec['referenceSampleMs']) + 1) * spec['referenceSampleMs']
        saved = {}
        for settings in spec['referenceSettings']:
            key = case['key'] + '-' + settings['key']; stats = read(reference / (key + '-statistics.json'))
            if not stats['completed']:
                result['references'].append(dict(key=settings['key'], completed=False, passed=False)); continue
            data = np.load(reference / (key + '.npz')); y, f = data['state'], data['forces']
            assert np.array_equal(data['timeSeconds'], expected_ms / 1000)
            assert y.shape == (len(expected_ms), 28) and f.shape == (len(expected_ms), 2, 7)
            ledgers = accounting(spec, case, y); energy = reference_energy(spec, case, y)
            passed = bool(stats['acceptedInternalDomainPassed'] and stats['sampleDomainPassed'] and
                          ref.domain(y) and np.isfinite(f).all() and ledgers['passed'] and energy['passed'])
            result['references'].append(dict(key=settings['key'], completed=True, ledgers=ledgers, energy=energy, passed=passed))
            saved[settings['key']] = (y, f)
        if len(saved) != 3:
            continue
        for left, right in (('dop853', 'dop853-tight'), ('dop853-tight', 'radau')):
            error = errors(saved[left][0], saved[right][0], saved[left][1], saved[right][1])
            result['referenceContrasts'].append(dict(left=left, right=right, errors=error, passed=within(error, spec['referenceBounds'])))
        common_errors = []; energies = []; candidate_states = []
        for resolution in spec['resolutions']:
            key = case['key'] + '-' + resolution['key']; raw, y, f = candidate_data(candidate / (key + '.csv'))
            assert np.array_equal(raw['timeMs'], np.arange(round(case['durationMs'] / resolution['physicalStepMs']) + 1) * resolution['physicalStepMs'])
            indices = np.rint(raw['timeMs'] / spec['referenceSampleMs']).astype(int)
            assert np.array_equal(raw['timeMs'], expected_ms[indices])
            inputs = read(candidate / (key + '-inputs.json'))
            assert inputs['scenario'] == case and inputs['resolution'] == resolution
            arrivals = [json.loads(line) for line in (candidate / (key + '-arrivals.jsonl')).read_text().splitlines()]
            expected = inputs['expectedArrivals']; assert len({a['messageId'] for a in arrivals}) == len(expected)
            assert sorted(arrivals, key=lambda a: a['messageId']) == sorted(expected, key=lambda a: a['messageId'])
            assert [a['arrivalAtBodyMs'] for a in arrivals] == sorted(a['arrivalAtBodyMs'] for a in arrivals)
            if case['replayOriginalArrivals']:
                assert arrivals == originals
            domain = ref.domain(y); release_error = 0.
            for i, muscle in enumerate(ref.muscles):
                direct = np.array([muscle.release(t / 1000, 0) for t in raw['timeMs']])
                release_error = max(release_error, float(np.max(abs(direct - raw['m' + str(i) + '_releaseDrive']))))
                event_times_ms = np.array(sorted(e['arrivalAtMs'] for e in case['events'] if e['muscleIndex'] == i))
                expected_arrivals = np.searchsorted(event_times_ms, raw['timeMs'], side='right')
                assert np.array_equal(raw['m' + str(i) + '_arrivals'], expected_arrivals)
                expected_path = spec['restPathLengthMeters'] - spec['momentArmsMeters'][i] * raw['jointAngleRadians']
                assert np.array_equal(raw['m' + str(i) + '_totalLengthMeters'], expected_path)
            ledger = accounting(spec, case, y); energy = boundary_energy(spec, raw, candidate / (key + '-boundaries.csv'))
            candidates = []
            for setting in ('dop853-tight', 'radau'):
                ry, rf = saved[setting]; error = errors(y, ry[indices], f, rf[indices])
                candidates.append(dict(reference=setting, errors=error, passed=within(error, spec['candidateBounds'])))
            stored_ledgers_passed = all(float(np.max(raw['m' + str(i) + '_' + name])) <= spec[bound]
                for i in range(2) for name, bound in (('momentumLedger', 'fiberMomentumLedgerNewtonSeconds'),
                    ('kineticLedger', 'fiberKineticLedgerJoules'), ('calciumLedger', 'calciumLedgerAbsolute')))
            stored_ledgers_passed &= all(float(np.max(raw[name])) <= spec[bound] for name, bound in (
                ('jointMomentumLedger', 'jointMomentumLedgerNewtonMeterSeconds'), ('jointKineticLedger', 'jointKineticLedgerJoules'),
                ('virtualWorkLedger', 'jointKineticLedgerJoules')))
            controls = bool(domain and ledger['passed'] and energy['accountingPassed'] and stored_ledgers_passed and release_error <= spec['releaseWaveformAbsolute'])
            result['candidates'].append(dict(resolution=resolution['key'], controlsPassed=controls, ledgers=ledger,
                releaseError=release_error, energy=energy, contrasts=candidates))
            # Compare refinement on the SAME coarsest grid, while the absolute
            # accuracy gate above includes EVERY candidate physical endpoint.
            stride = round(1. / resolution['physicalStepMs']); common = np.arange(0, len(y), stride)
            ry, rf = saved['dop853-tight']; common_errors.append(errors(y[common], ry[indices[common]], f[common], rf[indices[common]]))
            energies.append(energy); candidate_states.append((y, f))
        for i in range(3):
            checks = {name: common_errors[i + 1][name] <= max(spec['refinementMaximumRatio'] * common_errors[i][name],
                        spec['refinementFloorFraction'] * bound) for name, bound in spec['candidateBounds'].items()}
            energy_checks = {name: energies[i + 1][name] <= max(spec['refinementMaximumRatio'] * energies[i][name],
                                spec['refinementFloorFraction'] * spec['finestCouplingDefectJoules'])
                             for name in ('maximumCumulativeDefectJoules', 'totalAbsoluteDefectJoules')}
            result['refinement'].append(dict(coarse=spec['resolutions'][i]['key'], fine=spec['resolutions'][i + 1]['key'],
                coarseErrors=common_errors[i], fineErrors=common_errors[i + 1], checks=checks,
                energyChecks=energy_checks, passed=all(checks.values()) and all(energy_checks.values())))
        muscle_delta = errors(candidate_states[3][0], candidate_states[4][0], candidate_states[3][1], candidate_states[4][1])
        result['muscleRefinement'] = dict(errors=muscle_delta, passed=all(muscle_delta[name] <= bound * spec['muscleRefinementBoundFraction'] for name, bound in spec['candidateBounds'].items()))
        finest_energy = all(energies[i][name] <= spec['finestCouplingDefectJoules'] for i in (3, 4)
                            for name in ('maximumCumulativeDefectJoules', 'totalAbsoluteDefectJoules'))
        result['finestEnergyPassed'] = finest_energy
        result['passed'] = bool(all(r['passed'] for r in result['references'] + result['referenceContrasts'] + result['refinement']) and
            all(c['controlsPassed'] for c in result['candidates']) and all(c['passed'] for r in result['candidates'][3:] for c in r['contrasts']) and
            result['muscleRefinement']['passed'] and finest_energy)
    return dict(completed=len(cases) == 8, numericalCriteriaPassed=all(c['passed'] for c in cases), cases=cases,
        candidateConditions=40, independentReferences=24, originalRecordedArrivals=28, neuronalModelExecuted=False,
        liveUnityIntegrated=False, physiologyQualified=False, avatarRecoveryAssessed=False)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__); parser.add_argument('package', type=Path)
    parser.add_argument('--preflight', action='store_true'); parser.add_argument('--solve-reference', action='store_true')
    args = parser.parse_args(); package = args.package.resolve()
    if args.preflight:
        preflight(package)
    else:
        if args.solve_reference:
            solve(package)
        analysis = audit(package); save(package / 'causal-analysis.json', analysis)
        if not analysis['numericalCriteriaPassed']:
            raise RuntimeError('Coupled mechanical criteria not all met. Preserve evidence and original bounds.')

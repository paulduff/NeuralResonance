"""Independent numerical study. Solve only when requested by the visible runner."""
import argparse
import importlib.util
import json
import os
from pathlib import Path
import traceback

for name in ('OMP_NUM_THREADS', 'OPENBLAS_NUM_THREADS', 'MKL_NUM_THREADS', 'NUMEXPR_NUM_THREADS'):
    os.environ[name] = '1'
import numpy as np
from dynamic_muscle_protocol import make_protocol
from dynamic_muscle_reference import Reference

read = lambda p: json.loads(p.read_text(encoding='utf-8-sig'))


def save(path, value):
    with path.open('x', encoding='utf-8') as stream:
        stream.write(json.dumps(value, indent=2, allow_nan=False) + '\n')


def environment(package):
    spec = importlib.util.spec_from_file_location('dependency_capture', package / 'source/tools/golgi-reference-resume.py')
    module = importlib.util.module_from_spec(spec); spec.loader.exec_module(module)
    return module.environment()


def validate(package):
    spec = read(package / 'dynamic-input.json')
    assert spec == make_protocol(package / 'reference/modelParameter.mat', package / 'reference/arrivals.jsonl')
    prerequisite = read(package / 'reference/component-review.json')
    assert prerequisite['operatorReportedCompletion'] and prerequisite['passedTests'] == 93
    return spec


def preflight(package):
    spec = validate(package)
    save(package / 'reference-preflight.json', dict(environment=environment(package), protocol=spec,
        completed=True, modelExecuted=False))


def solve(package):
    spec = validate(package)
    assert read(package / 'reference-preflight.json')['environment'] == environment(package)
    output = package / 'dynamic-reference'; output.mkdir()
    records, ordering = [], []
    for case in spec['cases']:
        grid = np.arange(round(case['durationMs'] / spec['sampleMs']) + 1) * spec['sampleMs'] / 1000
        ref = Reference(spec, case)
        for settings in spec['referenceSettings']:
            key = case['key'] + '-' + settings['key']; print('REFERENCE', key, flush=True)
            try:
                state, forces, statistics = ref.solve(settings, grid)
            except Exception:
                partial = ref.reference_partial
                np.savez_compressed(output / (key + '-partial.npz'), timeSeconds=grid,
                    partialSamples=partial['samples'], internalTimes=partial['internalTimes'], internalStates=partial['internalStates'])
                save(output / (key + '-failure.json'), dict(completed=False,
                    completedBoundarySeconds=partial['completedBoundarySeconds'], error=traceback.format_exc()))
                raise
            np.savez_compressed(output / (key + '.npz'), timeSeconds=grid, state=state, forces=forces)
            records.append(dict(key=key, **statistics))
            save(output / (key + '-statistics.json'), statistics)
        if case['key'] in spec['sourceOrderingCases']:
            for step in spec['stepsMs']:
                key = case['key'] + '-source-order-h' + str(step); print('SOURCE ORDER', key, flush=True)
                try:
                    state, forces = ref.source_ordering(step, grid)
                    np.savez_compressed(output / (key + '.npz'), timeSeconds=grid, state=state, forces=forces)
                    outcome = dict(key=key, case=case['key'], stepMs=step, completed=True)
                except Exception:
                    partial = ref.source_partial
                    np.savez_compressed(output / (key + '-partial.npz'),
                        timeSeconds=grid[:len(partial['states'])], state=np.array(partial['states']), lastState=partial['lastState'])
                    outcome = dict(key=key, case=case['key'], stepMs=step, completed=False,
                        completedTimeMs=partial['completedTimeMs'], error=traceback.format_exc())
                ordering.append(outcome); save(output / (key + '-status.json'), outcome)
    save(output / 'execution.json', dict(completed=True, references=records, sourceOrdering=ordering,
        nativeMatlabExecuted=False, physiologyQualified=False))


def errors(a, b, af, bf, n):
    difference = np.max(abs(a - b), axis=0); force = np.max(abs(af - bf), axis=0)
    return dict(fiberLengthMeters=float(difference[0]), fiberVelocityMetersPerSecond=float(difference[1]),
        yieldFraction=float(difference[2]), calcium=float(max(difference[3 + 6 * i:5 + 6 * i].max() for i in range(n))),
        activation=float(max(difference[5 + 6 * i] for i in range(n))),
        fluxIntegral=float(max(difference[6 + 6 * i:9 + 6 * i].max() for i in range(n))),
        forceNewtons=float(force[:6].max()), accelerationMetersPerSecondSquared=float(force[6]),
        impulseNewtonSeconds=float(difference[-2]), workJoules=float(difference[-1]))


def within(error, bounds):
    assert error.keys() == bounds.keys()
    return all(error[name] <= limit for name, limit in bounds.items())


def audit(package):
    spec = validate(package)
    assert read(package / 'reference-preflight.json')['environment'] == environment(package), 'Numerical dependencies changed during study.'
    result = read(package / 'dynamic-study/results.json')
    assert read(package / 'dynamic-study/protocol.json') == spec
    assert result['completed'] and result['successfulCases'] == 33 and len(result['cases']) == 33
    assert {r['key'] for r in result['cases']} == {c['key'] + '-h' + str(h) for c in spec['cases'] for h in spec['stepsMs']}
    execution = read(package / 'dynamic-reference/execution.json')
    assert execution['completed'] and len(execution['references']) == 33 and len(execution['sourceOrdering']) == 15
    records = []; all_passed = True
    original_arrivals = [json.loads(line) for line in (package / 'reference/arrivals.jsonl').read_text().splitlines()]
    original_calcium = np.loadtxt(package / 'reference/isometric.csv', delimiter=',', skiprows=1)
    for case in spec['cases']:
        ref = Reference(spec, case); n = ref.n
        grid_ms = np.arange(round(case['durationMs'] / spec['sampleMs']) + 1) * spec['sampleMs']
        grid = grid_ms / 1000
        references = {}
        domain_passed = True
        for settings in spec['referenceSettings']:
            key = case['key'] + '-' + settings['key']
            with np.load(package / 'dynamic-reference' / (key + '.npz'), allow_pickle=False) as stored:
                assert np.array_equal(stored['timeSeconds'], grid), key
                state, forces = stored['state'].copy(), stored['forces'].copy()
            assert state.shape == (len(grid), 3 + 6 * n + 2) and forces.shape == (len(grid), 7)
            assert np.isfinite(state).all() and np.isfinite(forces).all()
            stats = read(package / 'dynamic-reference' / (key + '-statistics.json'))
            assert next(r for r in execution['references'] if r['key'] == key) == dict(key=key, **stats)
            domain_passed &= stats['acceptedInternalDomainPassed'] and stats['sampleDomainPassed'] and ref.domain(state)
            references[settings['key']] = (state, forces)
        contrasts = []
        for left, right in (('dop853', 'dop853-tight'), ('dop853-tight', 'radau')):
            a, af = references[left]; b, bf = references[right]
            error = errors(a, b, af, bf, n)
            contrasts.append(dict(left=left, right=right, errors=error, passed=within(error, spec['referenceBounds'])))
        candidates = []
        for h in spec['stepsMs']:
            key = case['key'] + '-h' + str(h)
            entry = next(r for r in result['cases'] if r['key'] == key)
            assert entry['completed'] and entry['delivered'] == len(case['events']) and entry['sampleCount'] == len(grid)
            inputs = read(package / 'dynamic-study' / (key + '-inputs.json'))
            assert inputs['scenario'] == case and inputs['stepMs'] == h
            assert len(inputs['definitions']) == n
            for unit, row_index in zip(inputs['definitions'], case['unitRows']):
                row = spec['parameterRows'][row_index]
                assert all(unit[k] == row[k] for k in ('calcium', 'lengthGainAboveOptimal', 'lengthGainBelowOptimal'))
            delivered = [json.loads(line) for line in (package / 'dynamic-study' / (key + '-arrivals.jsonl')).read_text().splitlines()]
            assert delivered == inputs['arrivals']
            assert [a['arrivalAtBodyMs'] for a in delivered] == [e['arrivalAtMs'] for e in case['events']]
            if case['replayOriginalArrivals']:
                assert delivered == original_arrivals
            for event, arrival in zip(case['events'], delivered):
                contact = inputs['definitions'][event['unitIndex']]['contact']
                assert all(arrival[k] == contact[k] for k in ('motorUnitId', 'contactId', 'sourceNeuronId', 'projectionId', 'sourceInstanceKey'))
                assert arrival['firedAtNeuralMs'] + contact['conductionDelayMs'] == arrival['arrivalAtBodyMs']
            data = np.loadtxt(package / 'dynamic-study' / (key + '.csv'), delimiter=',', skiprows=1)
            assert data.shape == (len(grid), 16 + 9 * n) and np.isfinite(data).all()
            assert np.array_equal(data[:, 0], grid_ms)
            assert np.max(abs(data[:, 1] - np.array([ref.length(t) for t in grid]))) <= 1e-15
            state = np.concatenate([data[:, 2:5]] + [data[:, 17 + 9 * i:23 + 9 * i] for i in range(n)] + [data[:, 12:14]], axis=1)
            forces = data[:, 5:12]
            comparisons = {name: errors(state, rs, forces, rf, n) for name, (rs, rf) in references.items() if name != 'dop853'}
            wave_error, ledger = 0., 0.
            for i in range(n):
                j = 16 + 9 * i
                wave_error = max(wave_error, float(np.max(abs(data[:, j] - np.array([ref.release(t, i) for t in grid])))))
                ledger = max(ledger, float(np.max(data[:, j + 7])), float(np.max(abs(data[:, j + 1] - data[:, j + 4] + data[:, j + 5] + data[:, j + 6]))),
                    float(np.max(abs(data[:, j + 2] - data[:, j + 6]))))
                expected_counts = np.searchsorted([e['arrivalAtMs'] for e in case['events'] if e['unitIndex'] == i], grid_ms, side='right')
                assert np.array_equal(data[:, j + 8], expected_counts)
            mass = spec['geometry']['fiberMassKg']
            momentum = max(float(np.max(data[:, 14])), float(np.max(abs(mass * state[:, 1] - state[:, -2]))))
            kinetic = max(float(np.max(data[:, 15])), float(np.max(abs(.5 * mass * state[:, 1] ** 2 - state[:, -1]))))
            legacy = None
            if case['key'] == 'recorded-fixed' and h == .125:
                assert original_calcium.shape == (len(grid), 12) and np.array_equal(original_calcium[:, 0], grid_ms)
                error = float(np.max(abs(data[:, [16, 17, 18, 19, 20, 21, 22]] - original_calcium[:, [1, 2, 3, 5, 7, 8, 9]])))
                force_error = float(np.max(abs(data[:, 19] * ref.rows[0]['calcium']['peakTetanicForceNewtons'] - original_calcium[:, 6])))
                legacy = dict(calciumMaximumAbsolute=error, activationEquivalentForceMaximumAbsoluteNewtons=force_error,
                    passed=error <= spec['legacyCalciumAbsolute'] and force_error <= spec['legacyForceAbsoluteNewtons'])
            accounting = wave_error <= spec['releaseWaveformAbsolute'] and ledger <= spec['calciumLedgerAbsolute'] and momentum <= spec['momentumLedgerNewtonSeconds']
            candidates.append(dict(stepMs=h, errorsAgainst= comparisons, physicalDomainPassed=ref.domain(state, 0),
                releaseWaveformError=wave_error, calciumLedgerError=ledger, momentumLedgerError=momentum, kineticLedgerError=kinetic,
                accountingPassed=accounting, legacyControl=legacy))
        finest = candidates[-1]; refinement = {}
        for reference in ('dop853-tight', 'radau'):
            coarse = candidates[0]['errorsAgainst'][reference]; fine = finest['errorsAgainst'][reference]
            refinement[reference] = all(fine[k] <= spec['refinementFraction'] * coarse[k] +
                spec['refinementFloorFraction'] * spec['candidateBounds'][k] for k in coarse)
        passed = bool(domain_passed and all(c['passed'] for c in contrasts) and all(refinement.values()) and
            all(c['physicalDomainPassed'] and c['accountingPassed'] and (c['legacyControl'] is None or c['legacyControl']['passed']) for c in candidates) and
            all(within(e, spec['candidateBounds']) for e in finest['errorsAgainst'].values()) and finest['kineticLedgerError'] <= spec['finestKineticLedgerJoules'])
        ordering = []
        for original in (o for o in execution['sourceOrdering'] if o['case'] == case['key']):
            outcome = dict(original)
            if original['completed']:
                with np.load(package / 'dynamic-reference' / (original['key'] + '.npz'), allow_pickle=False) as data:
                    assert np.array_equal(data['timeSeconds'], grid)
                    state, force = data['state'], data['forces']
                    target, target_force = references['dop853-tight']
                    outcome['errorsAgainstContinuousReference'] = errors(state, target, force, target_force, n)
            else:
                assert (package / 'dynamic-reference' / (original['key'] + '-partial.npz')).is_file()
            ordering.append(outcome)
        records.append(dict(case=case['key'], referenceDomainPassed=bool(domain_passed), referenceContrasts=contrasts,
            candidates=candidates, refinementPassed=refinement, sourceOrdering=ordering, passed=passed))
        all_passed &= passed
    return dict(completed=True, numericalCriteriaPassed=bool(all_passed), cases=records, exactRecordedArrivalsPreserved=True,
        candidateConditions=33, independentReferences=33, sourceOrderingAttempts=15,
        originalBoundsChanged=False, nativeMatlabExecuted=False, liveBodyIntegrated=False,
        physiologicalCalibrationComplete=False, avatarRecoveryAssessed=False)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__); parser.add_argument('package', type=Path)
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument('--preflight', action='store_true'); mode.add_argument('--solve-reference', action='store_true'); mode.add_argument('--audit', action='store_true')
    args = parser.parse_args(); package = args.package.resolve()
    if args.preflight:
        preflight(package)
    elif args.solve_reference:
        solve(package); report = audit(package); save(package / 'dynamic-analysis.json', report)
        print(json.dumps(report, indent=2))
        if not report['numericalCriteriaPassed']:
            raise RuntimeError('Dynamic muscle numerical criteria failed; preserve evidence and unchanged bounds.')
    else:
        print(json.dumps(audit(package), indent=2))

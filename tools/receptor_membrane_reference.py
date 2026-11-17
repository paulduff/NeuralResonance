"""Independent coupled receptor/motor ODE comparison, visible-window execution only.

The reference integrates each receptor occupancy as an additional ODE state;
it does not call the C# analytic receptor evaluator or replay candidate states.
Existing source-derived motor currents/rates are reused, with contact-local
currents added in physical units. All original membrane criteria are retained.
"""
from hashlib import sha256
import importlib.util
import json
import math
from pathlib import Path
import re
import sys
import warnings
import numpy as np
from scipy.integrate import solve_ivp
from receptor_membrane_protocol import protocol

read = lambda p: json.loads(p.read_text(encoding='utf-8-sig'))
GATES = np.r_[np.arange(5, 14), np.arange(15, 27)]


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec); spec.loader.exec_module(result)
    return result


def digest(path):
    h = sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(4 * 1024 * 1024), b''): h.update(block)
    return h.hexdigest().upper()


def receptor_parameters(root):
    result = {}
    for kind, name in [('AMPA', 'ampa.mod'), ('GABAA', 'gabaa.mod')]:
        text = (root / name).read_text()
        block = re.search(r'\bPARAMETER\s*\{(.*?)\}', text, re.S).group(1)
        values = {key: float(re.search(r'(?m)^\s*' + key + r'\s*=\s*([-+\d.eE]+)', block).group(1))
                  for key in ('Cmax', 'Cdur', 'Alpha', 'Beta', 'Erev')}
        expected = dict(Cmax=1, Cdur=1, Alpha=1.1 if kind == 'AMPA' else 5,
            Beta=.19 if kind == 'AMPA' else .18, Erev=0 if kind == 'AMPA' else -80)
        assert values == expected, 'Changed source receptor parameters.'
        result[kind] = values
    return result


def system(motor_root, contacts, rates):
    motor = module('coupled_motor_reference', Path(__file__).with_name('motoneuron-active-reference.py'))
    factory, physical, area, cap, axial = motor.source_model(motor_root)
    base = factory('None', dict(somaCurrentPa=0, dendriteExcitatoryNs=0, dendriteInhibitoryNs=0))
    base_jac = motor.bounded_jacobian(base)
    n = len(contacts); initial = np.r_[physical, np.zeros(n)]
    compartments = np.array([c['compartment'] for c in contacts], dtype=int)
    maximum = np.array([c['maximumConductanceNs'] for c in contacts])
    reversal = np.array([rates[c['kind']]['Erev'] for c in contacts])
    alpha = np.array([rates[c['kind']]['Alpha'] for c in contacts])
    beta = np.array([rates[c['kind']]['Beta'] for c in contacts])

    def functions(transmitter):
        binding = alpha * np.asarray(transmitter)

        def rhs(t, y):
            assert y.shape == (30 + n,)
            result = np.r_[base(t, y[:30]), binding * (1 - y[30:]) - beta * y[30:]]
            currents = maximum * y[30:] * (reversal - y[compartments])
            np.add.at(result, compartments, currents / cap[compartments])
            result[29] += np.sum(currents)
            return result

        def jac(t, y):
            # Reuse the checked, bounded motor Jacobian. Coupling/receptor
            # derivatives are analytic; passive accounting columns remain zero.
            result = np.zeros((30 + n, 30 + n)); result[:30, :30] = base_jac(t, y[:30])
            for j, compartment in enumerate(compartments):
                conductance = maximum[j] * y[30 + j]
                sensitivity = maximum[j] * (reversal[j] - y[compartment])
                result[compartment, compartment] -= conductance / cap[compartment]
                result[compartment, 30 + j] += sensitivity / cap[compartment]
                result[29, compartment] -= conductance; result[29, 30 + j] += sensitivity
                result[30 + j, 30 + j] = -binding[j] - beta[j]
            return result
        return rhs, jac
    return functions, initial, cap


def transitions(contacts, duration, rates):
    arrivals = [np.asarray(c['sourceTimesMs']) + c['axonalDelayMs'] for c in contacts]
    points = {0., duration}
    for c, times in zip(contacts, arrivals):
        pulse = rates[c['kind']]['Cdur']
        assert np.all(np.diff(times) >= pulse) and np.all((times >= 0) & (times + pulse <= duration))
        points.update(times); points.update(times + pulse)
    ordered = sorted(points)
    for begin, end in zip(ordered, ordered[1:]):
        transmitter = np.array([rates[c['kind']]['Cmax'] if np.any((times <= begin) & (begin < times + rates[c['kind']]['Cdur'])) else 0.
                                for c, times in zip(contacts, arrivals)])
        yield begin, end, transmitter


def solve(functions, initial, contacts, rates, method, grid, duration):
    assert grid[0] == 0 and grid[-1] == duration and np.all(np.diff(grid) > 0)
    result = np.empty((len(grid), len(initial))); result[0] = initial
    atol = np.r_[np.full(5, 1e-9), np.full(9, 1e-11), 1e-13, np.full(12, 1e-11),
        1e-13, 1e-13, 1e-7, np.full(len(contacts), 1e-11)] * method['atolScale']
    state = initial.copy(); crossings = []; nodes_t = [np.array([0.])]; nodes_y = [initial[None, :]]
    steps = evaluations = 0

    def crossing(t, y): return y[0] + 20
    crossing.direction = 1; crossing.terminal = False
    for begin, end, transmitter in transitions(contacts, duration, rates):
        rhs, jac = functions(transmitter)
        with warnings.catch_warnings(), np.errstate(over='raise', invalid='raise', divide='raise'):
            warnings.simplefilter('error', RuntimeWarning)
            solution = solve_ivp(rhs, (begin, end), state, method=method['method'], rtol=method['rtol'], atol=atol,
                max_step=method['maximumStepMs'], dense_output=True, events=crossing,
                **({'jac': jac} if method['method'] == 'Radau' else {}))
        if not solution.success: raise RuntimeError(solution.message)
        assert solution.t[0] == begin and solution.t[-1] == end and np.isfinite(solution.y).all()
        mask = (grid > begin) & (grid <= end)
        if np.any(mask): result[mask] = solution.sol(grid[mask]).T
        # Preserve an accepted endpoint instead of its interpolant's last bit.
        result[grid == end] = solution.y[:, -1]
        crossings.extend(float(t) for t in solution.t_events[0] if t > begin)
        state = solution.y[:, -1]; nodes_t.append(solution.t[1:]); nodes_y.append(solution.y[:, 1:].T)
        steps += len(solution.t) - 1; evaluations += solution.nfev
    assert np.isfinite(result).all()
    return result, np.asarray(crossings), np.concatenate(nodes_t), np.concatenate(nodes_y), dict(acceptedSteps=steps, functionEvaluations=evaluations)


def jacobian_control(functions, initial, bound):
    n = len(initial) - 30; reports = []
    for drive in (0., .5, 1.):
        rhs, jac = functions(np.full(n, drive))
        for occupancy in (.1, .7):
            y = initial.copy(); y[:5] = np.linspace(-60, -52, 5); y[14] = .001
            y[GATES] = .4; y[30:] = occupancy
            before = y.copy(); derivative = jac(0, y)
            direction = np.r_[np.linspace(-.3, .3, 5), np.full(9, .01), 2e-6, np.full(12, -.01), np.zeros(3), np.full(n, .03)]
            tangent = derivative @ direction; errors = []
            for epsilon in (.001, .0005):
                independent = (rhs(0, y + epsilon * direction) - rhs(0, y - epsilon * direction)) / (2 * epsilon)
                errors.append(float(np.max(np.abs(independent - tangent) / np.maximum(1., np.abs(tangent)))))
            assert np.array_equal(before, y) and np.all(derivative[:, 27:30] == 0)
            assert errors[-1] <= bound and errors[-1] <= max(1e-8, 1.1 * errors[0])
            reports.append(dict(transmitterMm=drive, occupancy=occupancy, relativeDirectionalErrors=errors))
    return reports


def analytic_controls(rates, methods, bound, output):
    reports = []
    for kind, p in rates.items():
        for method in methods:
            numeric = np.array([0.]); exact = 0.; maximum = 0.; saved_t = [0.]; saved_r = [0.]
            for begin, end, concentration in [(0., 1., 1.), (1., 2., 0.), (2., 3., 1.), (3., 6., 0.)]:
                alpha = p['Alpha'] * concentration; rate = alpha + p['Beta']; equilibrium = alpha / rate
                with warnings.catch_warnings(), np.errstate(over='raise', invalid='raise', divide='raise'):
                    warnings.simplefilter('error', RuntimeWarning)
                    solution = solve_ivp(lambda t, y: alpha * (1 - y) - p['Beta'] * y, (begin, end), numeric,
                        method=method['method'], rtol=method['rtol'], atol=1e-11 * method['atolScale'],
                        max_step=method['maximumStepMs'], dense_output=True,
                        **({'jac': lambda t, y: np.array([[-rate]])} if method['method'] == 'Radau' else {}))
                assert solution.success and solution.t[-1] == end
                expected = equilibrium + (exact - equilibrium) * np.exp(-rate * (solution.t - begin))
                maximum = max(maximum, float(np.max(np.abs(solution.y[0] - expected))))
                numeric = solution.y[:, -1]; exact = equilibrium + (exact - equilibrium) * math.exp(-rate * (end - begin))
                saved_t.extend(solution.t[1:]); saved_r.extend(solution.y[0, 1:])
            assert maximum <= bound
            key = kind + '-' + method['key']; np.savez_compressed(output / (key + '.npz'), timeMs=saved_t, occupancy=saved_r)
            reports.append(dict(key=key, passed=True, maximumOccupancyError=maximum))
    return reports


def physical(state, cap, criteria, initial_voltage=-65):
    assert state.ndim == 2 and state.shape[1] >= 31 and np.isfinite(state).all()
    result = dict(minimumGate=float(state[:, GATES].min()), maximumGate=float(state[:, GATES].max()),
        minimumReceptor=float(state[:, 30:].min()), maximumReceptor=float(state[:, 30:].max()), minimumCalciumMm=float(state[:, 14].min()),
        calciumLedgerMm=float(np.max(np.abs(state[:, 14] - .0001 - state[:, 27] + state[:, 28]))),
        chargeLedgerFc=float(np.max(np.abs((state[:, :5] - initial_voltage) @ cap - state[:, 29]))))
    slack = criteria['referenceDomainSlack']
    result['passed'] = (result['minimumGate'] >= -slack and result['maximumGate'] <= 1 + slack and
        result['minimumReceptor'] >= -slack and result['maximumReceptor'] <= 1 + slack and result['minimumCalciumMm'] >= -1e-13 and
        result['calciumLedgerMm'] <= criteria['calciumLedgerMm'] and result['chargeLedgerFc'] <= criteria['chargeLedgerFc'])
    return result


def errors(a, b, spikes_a, spikes_b):
    equal = len(spikes_a) == len(spikes_b)
    return dict(voltageMv=float(np.max(np.abs(a[:, :5] - b[:, :5]))), gate=float(np.max(np.abs(a[:, GATES] - b[:, GATES]))),
        calciumMm=float(np.max(np.abs(a[:, 14] - b[:, 14]))), receptor=float(np.max(np.abs(a[:, 30:] - b[:, 30:]))),
        equalCrossingCounts=equal, crossingMs=float(np.max(np.abs(spikes_a - spikes_b), initial=0)) if equal else None)


def meets(error, criteria, prefix):
    return error['equalCrossingCounts'] and all(error[k] <= criteria[prefix + k[0].upper() + k[1:]]
        for k in ('voltageMv', 'gate', 'calciumMm', 'receptor', 'crossingMs'))


def main(package):
    if not __debug__: raise RuntimeError('Numerical acceptance assertions must remain enabled.')
    assert Path(__file__).resolve() == (package / 'source/tools/receptor_membrane_reference.py').resolve(), 'Execute the frozen reference source.'
    from receptor_membrane_protocol import crossing_mode
    spec = read(package / 'receptor-membrane-input.json'); crossing_mode(spec)
    config = read(package / 'configuration.json')
    assert config['IsolatedReceptorMembraneComparison'] and config['NoLiveMonitoring']
    assert config['MotorSourceRevision'] == spec['motorSourceRevision'] and config['ReceptorSourceRevision'] == spec['receptorSourceRevision']
    assert config['ExpectedCandidateConditions'] == config['ExpectedReferenceConditions'] == 18
    assert config['SourceOutputComparisonRequested'] and config['ExpectedSourceOutputConditions'] == 36
    required = {'receptor-membrane-input.json', 'receptor-motor-output-input.json', 'reference-preflight.json'}
    required.update('reference/motor-source/' + name for name in ('FivecomptcMG_0.hoc','na3rp.mod','naps.mod','kdrRL.mod','mAHP.mod','gh.mod','L_Ca_inact.mod'))
    required.update('reference/receptor-source/' + name for name in ('ampa.mod','gabaa.mod'))
    required.update('source/tools/' + name for name in ('receptor_membrane_reference.py','receptor_membrane_protocol.py',
        'audit-receptor-membrane.py','motoneuron-active-reference.py','motoneuron-reference-check.py',
        'motoneuron_active_protocol.py','motoneuron_bounded_jacobian.py','golgi-reference-resume.py',
        'receptor_motor_output_protocol.py','analyse-receptor-motor-output.py'))
    assert required <= {entry['Path'].replace('\\','/') for entry in config['PackageFiles']}, 'Unprotected reference input.'
    for entry in config['PackageFiles']: assert digest(package / entry['Path']) == entry['Sha256']
    environment_module = module('receptor_numeric_environment', Path(__file__).with_name('golgi-reference-resume.py'))
    environment = environment_module.environment()
    assert environment == read(package / 'reference-preflight.json')['environment'], 'Numerical dependencies differ.'
    rates = receptor_parameters(package / 'reference/receptor-source')
    output = package / 'membrane-reference'; output.mkdir()
    report = dict(completed=False, numericalCriteriaPassed=False, criteria=spec['criteria'], cases=[],
        presynapticEventsAreFixtures=True, presynapticNeuronsExecuted=False,
        physiologicalCalibrationComplete=False, avatarRecoveryAssessed=False)

    def save(): (output / 'comparison.json').write_text(json.dumps(report, indent=2, allow_nan=False) + '\n')
    try:
        # Imported only for saved-data validation. This audit performs no solve.
        audit_module = module('receptor_candidate_audit', Path(__file__).with_name('audit-receptor-membrane.py'))
        candidate = audit_module.audit(package); report['candidateAudit'] = candidate['summary']; save()
        output_audit = module('receptor_output_audit', Path(__file__).with_name('analyse-receptor-motor-output.py'))
        report['sourceOutputAudit'] = output_audit.audit(package, candidate)
        with (package / 'motor-output-analysis.json').open('x') as stream:
            json.dump(report['sourceOutputAudit'], stream, indent=2, allow_nan=False)
        save()
        report['analyticControls'] = analytic_controls(rates, spec['referenceMethods'], spec['criteria']['analyticControlReceptor'], output)
        assert len(report['analyticControls']) == spec['expectedAnalyticControls']
        systems = {}
        for scenario in spec['cases']:
            contacts = candidate['cases'][scenario['key']]['contacts']
            functions, initial, cap = system(package / 'reference/motor-source', contacts, rates)
            systems[scenario['key']] = functions, initial, cap
            row = dict(key=scenario['key'], jacobianControls=jacobian_control(functions, initial, spec['criteria']['jacobianRelativeError']),
                referenceMethods=[], referenceContrasts=[], candidateContrasts=[], refinement=[])
            report['cases'].append(row); save()
        assert sum(len(r['jacobianControls']) for r in report['cases']) == 36
        for scenario, row in zip(spec['cases'], report['cases']):
            print('REFERENCE ' + scenario['key'], flush=True)
            data = candidate['cases'][scenario['key']]; contacts = data['contacts']; native = data['conditions']
            functions, initial, cap = systems[scenario['key']]
            grid = np.unique(np.concatenate([c['time'] for c in native])); references = []
            for method in spec['referenceMethods']:
                state, crossings, accepted_t, accepted_y, stats = solve(functions, initial, contacts, rates, method, grid, spec['durationMs'])
                record = dict(key=method['key'], crossingsMs=crossings.tolist(), acceptedNodes=physical(accepted_y, cap, spec['criteria']),
                    comparisonPoints=physical(state, cap, spec['criteria']), **stats)
                row['referenceMethods'].append(record); references.append((state, crossings))
                np.savez_compressed(output / (scenario['key'] + '-' + method['key'] + '.npz'), timeMs=grid, state=state,
                    crossingMs=crossings, acceptedTimeMs=accepted_t, acceptedState=accepted_y)
                save()
            for left, right in [(0, 1), (0, 2), (1, 2)]:
                error = errors(references[left][0], references[right][0], references[left][1], references[right][1])
                row['referenceContrasts'].append(dict(left=spec['referenceMethods'][left]['key'], right=spec['referenceMethods'][right]['key'],
                    error=error, passed=meets(error, spec['criteria'], 'reference')))
            primary, primary_crossings = references[1]
            for c in native:
                indices = np.searchsorted(grid, c['time']); assert np.array_equal(grid[indices], c['time'])
                error = errors(c['state'], primary[indices], c['crossings'], primary_crossings)
                domain = physical(c['state'], cap, spec['criteria'])
                row['candidateContrasts'].append(dict(maximumStepMs=c['maximumStepMs'], error=error, physical=domain,
                    comparedPoints=len(indices), passed=domain['passed'] and meets(error, spec['criteria'], 'candidate')))
            for coarse, fine in zip(row['candidateContrasts'], row['candidateContrasts'][1:]):
                checks = {k: coarse['error'][k] is not None and fine['error'][k] is not None and
                    fine['error'][k] <= max(spec['criteria']['refinementFloor'], spec['criteria']['refinementRatio'] * coarse['error'][k])
                    for k in ('voltageMv', 'gate', 'calciumMm', 'receptor', 'crossingMs')}
                row['refinement'].append(dict(coarseStepMs=coarse['maximumStepMs'], fineStepMs=fine['maximumStepMs'],
                    checks=checks, passed=all(checks.values())))
            row['passed'] = (all(r['acceptedNodes']['passed'] and r['comparisonPoints']['passed'] for r in row['referenceMethods']) and
                all(r['passed'] for category in ('referenceContrasts', 'candidateContrasts', 'refinement') for r in row[category]))
            save()
        quiet = next(r for r in report['cases'] if r['key'] == 'quiet')
        excited = [r for r in report['cases'] if r['key'] in ('soma-excitation', 'local-excitation', 'distributed-excitation')]
        report['quietHasNoCrossings'] = all(not r['crossingsMs'] for r in quiet['referenceMethods'])
        report['nontrivialExcitedCrossings'] = any(len(r['referenceMethods'][1]['crossingsMs']) > 0 for r in excited)
        report['completed'] = True
        report['numericalCriteriaPassed'] = (len(report['cases']) == 6 and all(r['passed'] for r in report['cases']) and
            report['quietHasNoCrossings'] and report['nontrivialExcitedCrossings'])
        save()
        if not report['numericalCriteriaPassed']: raise RuntimeError('Coupled numerical criteria failed; preserve evidence and original bounds.')
    except Exception as error:
        report['error'] = repr(error); save(); raise
    finally:
        try:
            assert environment_module.environment() == environment, 'Numerical dependencies changed during the comparison.'
            for entry in config['PackageFiles']: assert digest(package / entry['Path']) == entry['Sha256']
            report['frozenInputsAndEnvironmentPreserved'] = True
        except Exception as integrity_error:
            report['frozenInputsAndEnvironmentPreserved'] = False
            report['integrityError'] = repr(integrity_error); report['numericalCriteriaPassed'] = False
            save(); raise
        save()


if __name__ == '__main__': main(Path(sys.argv[1]).resolve())

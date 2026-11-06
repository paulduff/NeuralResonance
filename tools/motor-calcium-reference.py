"""Independent continuous slow-unit equations; run only in the visible test window.

Parameters are read from the authors' pinned MAT data, not the candidate's defaults.
Release is evaluated by summing individual causal waveforms, not its recurrence.
No rate generator, live neuron, dynamic muscle or old brain state is accessed.
"""
from hashlib import sha256
import json
from pathlib import Path
import sys

import numpy as np
import scipy
from scipy.integrate import solve_ivp
from scipy.io import loadmat

REVISION = 'c317be803f01965a1ca8fc4cbad1ba9156433af8'
MAT_HASH = '01123aec76d1acc18b306ae1bd9daabfd29fe9287b54212aef5db3f15f719ebd'
NAMES = ('storeSites', 'totalCalcium', 'releaseRatePerSecond', 'pumpRatePerSecond',
         'bindingRatePerSecond', 'unbindingRatePerSecond', 'releaseRiseSeconds',
         'releaseDecaySeconds', 'hillExponent', 'halfActivationBinding',
         'activationSeconds', 'cooperativity', 'peakTetanicForceNewtons')
STATE_NAMES = ('freeCalcium', 'boundCalcium', 'activation', 'released', 'recovered', 'netBound')
SETTINGS = (('dop853', 'DOP853', 1e-10, 1e-12),
            ('dop853-tight', 'DOP853', 1e-12, 1e-14),
            ('radau', 'Radau', 1e-11, 1e-13))


def write(path, value):
    path.write_text(json.dumps(value, indent=2, allow_nan=False)+'\n', encoding='utf-8')


def solve(parameters, arrivals, grid, method, rtol, atol):
    S, C, k1, k2, k3, k4, t1, t2, N, K, t3, gamma, _ = parameters
    output = np.full((len(grid), 6), np.nan)
    state = np.zeros(6)
    nfev = njev = nlu = 0
    boundaries = np.unique(np.r_[grid[0], arrivals, grid[-1]])
    for left, right in zip(boundaries[:-1], boundaries[1:]):
        active = arrivals[arrivals <= left]

        def rates(t, y):
            age = t - active
            release_drive = np.sum(-np.expm1(-age / t1) * np.exp(-age / t2))
            c, cf, activation = y[:3]
            release = k1 * (C - c - cf) * release_drive
            pump = k2 * c * (S - C + c + cf)
            binding = (1 - cf) * (k3 * c - k4 * cf / (1 + gamma * activation))
            # Continuous extension only for solver trial iterates below zero.
            # Accepted samples are retained unchanged and checked separately.
            power = abs(cf / K) ** N
            return (release - pump - binding, binding,
                    (power / (1 + power) - activation) / t3, release, pump, binding)

        answer = solve_ivp(rates, (left, right), state, method=method,
                           rtol=rtol, atol=atol, max_step=.002, dense_output=True)
        if not answer.success:
            raise RuntimeError(answer.message)
        chosen = (grid >= left) & ((grid < right) if right < grid[-1] else (grid <= right))
        output[chosen] = answer.sol(grid[chosen]).T
        state = answer.y[:, -1]
        nfev += answer.nfev; njev += answer.njev; nlu += answer.nlu
    if not np.isfinite(output).all():
        raise RuntimeError('Reference returned non-finite or incomplete samples.')
    return output, dict(nfev=nfev, njev=njev, nlu=nlu)


def metrics(trace, grid, peak_force):
    force = trace[:, 2] * peak_force
    peak = int(np.argmax(force))
    plateau = force[(grid >= 2.5) & (grid <= 3.9)]
    return dict(peakForceNewtons=float(force[peak]), peakAtMs=float(grid[peak]*1000),
                forceIntegralNewtonSeconds=float(np.trapezoid(force, grid)),
                lateDriveMeanForceNewtons=float(np.mean(plateau)),
                lateDriveRangeNewtons=float(np.ptp(plateau)),
                finalActivation=float(trace[-1, 2]))


def maximum_difference(left, right):
    return dict(zip(STATE_NAMES, map(float, np.max(np.abs(left-right), axis=0))))


def main(package):
    output = package/'calcium-reference'
    output.mkdir()  # Never replace an earlier attempt.
    report = dict(referenceRevision=REVISION, numpy=np.__version__, scipy=scipy.__version__,
                  completed=False, numericalCriteriaPassed=False, physiologyQualified=False,
                  neuronalSourceQualified=False, avatarRecoveryAssessed=False,
                  criteria=dict(referenceAbsoluteState=1e-8, referenceAbsoluteFluxIntegral=1e-7,
                                finestCandidateAbsoluteState=1e-6, finestCandidateForceNewtons=1e-6,
                                calciumLedger=1e-9, refinementFraction=.2, refinementFloor=2e-8),
                  protocols=[])
    try:
        data_path = package/'muscle-input/modelParameter.mat'
        assert sha256(data_path.read_bytes()).hexdigest() == MAT_HASH, 'Pinned parameter data changed.'
        data = loadmat(data_path, simplify_cells=True)['modelParameter']
        row = np.asarray(data['parameterMatrix'])[0]
        parameters = np.r_[row[:12], np.asarray(data['PTi']).reshape(-1)[0]]
        protocol = json.loads((package/'calcium-study/protocol.json').read_text())
        assert protocol['referenceRevision'] == REVISION and protocol['referenceRowOneBased'] == 1
        assert protocol['referenceLength'] == protocol['referenceYield'] == protocol['referenceSag'] == 1
        assert not protocol['endogenousSpikes'] and protocol['diagnosticArrivals']
        assert parameters.shape == (13,) and np.array_equal(parameters, [protocol['parameters'][n] for n in NAMES])
        expected_protocols = dict(quiet=[], single=[1001.125], pair=[1001.125, 1051.125])
        expected_protocols.update({'train-'+str(hz): [1000+i*1000/hz for i in range(3*hz)] for hz in (5,10,20,40)})
        assert protocol['protocols'] == expected_protocols
        assert protocol['stepsMs'] == [.125, .0625, .03125] and protocol['sampleMs'] == .25 and protocol['durationMs'] == 6000
        candidate_report = json.loads((package/'calcium-study/results.json').read_text())
        assert candidate_report['completed'] and len(candidate_report['cases']) == 21
        cases = {c['key']: c for c in candidate_report['cases']}
        assert len(cases) == 21
        grid = np.arange(24001, dtype=float)*.00025
        report['parametersFromMat'] = dict(zip(NAMES, map(float, parameters)))
        all_passed = True
        for name, times_ms in expected_protocols.items():
            print('REFERENCE', name, flush=True)
            arrivals = np.asarray(times_ms)/1000
            references = {}
            result = dict(protocol=name, references={}, contrasts=[], candidates=[])
            reference_domain_passed = True
            for key, method, rtol, atol in SETTINGS:
                trace, stats = solve(parameters, arrivals, grid, method, rtol, atol)
                references[key] = trace
                np.savez_compressed(output/(name+'-'+key+'.npz'), timeSeconds=grid, state=trace)
                store = parameters[1] - trace[:, 0] - trace[:, 1]
                physical = bool(np.min(trace[:, :3]) >= -1e-12 and np.min(store) >= -1e-12 and
                                np.max(trace[:, 1:3]) <= 1+1e-12)
                reference_domain_passed &= physical
                result['references'][key] = dict(method=method, rtol=rtol, atol=atol, maxStepSeconds=.002,
                    physicalSampleDomainPassed=physical, minimumState=float(np.min(trace[:, :3])),
                    statistics=stats, metrics=metrics(trace, grid, parameters[-1]))
            contrast_passed = True
            for left, right in (('dop853','dop853-tight'), ('dop853-tight','radau')):
                error = maximum_difference(references[left], references[right])
                passed = max(error[n] for n in STATE_NAMES[:3]) <= 1e-8 and max(error[n] for n in STATE_NAMES[3:]) <= 1e-7
                result['contrasts'].append(dict(left=left, right=right, maximumAbsolute=error, passed=passed))
                contrast_passed &= passed
            state_errors = []
            for step in protocol['stepsMs']:
                key = name+'-h'+str(step)
                entry = cases[key]
                assert entry['arrivals'] == len(times_ms) and entry['arrivalsMs'] == times_ms
                trace = np.loadtxt(package/'calcium-study'/(key+'.csv'), delimiter=',', skiprows=1)
                assert trace.shape == (24001, 11) and np.isfinite(trace).all()
                assert np.array_equal(trace[:, 0], np.arange(24001)*.25)
                # Candidate's three states plus its separate flux integrals.
                state = trace[:, [2,3,5,7,8,9]]
                error = maximum_difference(state, references['dop853-tight'])
                force_error = float(np.max(np.abs(trace[:,6]-references['dop853-tight'][:,2]*parameters[-1])))
                state_errors.append(max(error[n] for n in STATE_NAMES[:3]))
                direct_release = np.zeros(len(grid))
                for arrival in arrivals:
                    mask = grid >= arrival
                    age = grid[mask]-arrival
                    direct_release[mask] += -np.expm1(-age/parameters[6])*np.exp(-age/parameters[7])
                release_error = float(np.max(np.abs(trace[:,1]-direct_release)))
                ledger = max(float(np.max(trace[:,10])),
                             float(np.max(np.abs(trace[:,2]-trace[:,7]+trace[:,8]+trace[:,9]))),
                             float(np.max(np.abs(trace[:,3]-trace[:,9]))))
                physical = bool(np.min(state[:,:3]) >= 0 and np.min(trace[:,4]) >= 0 and
                                np.max(state[:,1:3]) <= 1)
                accounting = bool(ledger <= 1e-9 and release_error <= 1e-10 and
                    np.max(np.abs(trace[:,4]+trace[:,2]+trace[:,3]-parameters[1])) <= 1e-12 and
                    np.max(np.abs(trace[:,6]-trace[:,5]*parameters[-1])) <= 1e-14)
                silence = name != 'quiet' or bool(np.all(trace[:,1:4] == 0) and np.all(trace[:,5:] == 0))
                result['candidates'].append(dict(stepMs=step, maximumAbsolute=error,
                    maximumForceErrorNewtons=force_error, releaseWaveformError=release_error,
                    maximumCalciumLedgerError=ledger, physicalDomainPassed=physical,
                    accountingPassed=accounting, silencePassed=silence, metrics=metrics(state, grid, parameters[-1])))
            finest = result['candidates'][-1]
            result['refinementPassed'] = state_errors[-1] <= .2*state_errors[0]+2e-8
            result['passed'] = bool(reference_domain_passed and contrast_passed and result['refinementPassed'] and
                state_errors[-1] <= 1e-6 and finest['maximumForceErrorNewtons'] <= 1e-6 and
                all(c['physicalDomainPassed'] and c['accountingPassed'] and c['silencePassed'] for c in result['candidates']))
            all_passed &= result['passed']
            report['protocols'].append(result)
            write(output/'comparison.json', report)
        report['completed'] = True
        report['numericalCriteriaPassed'] = all_passed
        write(output/'comparison.json', report)
        if not all_passed:
            raise RuntimeError('Muscle candidate/reference criteria not met; retain traces and review without relaxing bounds.')
        print('Numerical checks passed. Physiological calibration, neuronal source and Avatar recovery remain unqualified.', flush=True)
    except Exception as error:
        report['error'] = repr(error)
        write(output/'comparison.json', report)
        raise


if __name__ == '__main__':
    main(Path(sys.argv[1]).resolve())

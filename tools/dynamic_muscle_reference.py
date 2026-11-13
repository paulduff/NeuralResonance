"""Independent continuous and source-ordering calculations; execute only in PowerShell.

All values originate in the verified author MAT data. Release is a direct sum
over causal arrivals, not the C# two-exponential recurrence. No live body input.
"""
import numpy as np
from scipy.integrate import solve_ivp


class Reference:
    def __init__(self, protocol, case):
        self.protocol, self.case = protocol, case
        self.g = protocol['geometry']
        self.rows = [protocol['parameterRows'][i] for i in case['unitRows']]
        self.n = len(self.rows)
        self.events = [np.array([e['arrivalAtMs'] / 1000 for e in case['events'] if e['unitIndex'] == i]) for i in range(self.n)]
        self.knots = np.array([[k['timeMs'] / 1000, k['offsetMeters']] for k in case['lengthKnots']])
        self.free = case['boundary'] == 'Free'

    def initial(self):
        y = np.zeros(3 + 6 * self.n + 2)
        y[0] = self.case['initialFiberLengthMeters']; y[2] = 1
        return y

    def length(self, t):
        return self.case['initialMusculotendonLengthMeters'] + np.interp(t, self.knots[:, 0], self.knots[:, 1])

    def release(self, t, unit, active=None):
        arrivals = self.events[unit] if active is None else active[unit]
        ages = t - arrivals[arrivals <= t]
        p = self.rows[unit]['calcium']
        return np.sum(-np.expm1(-ages / p['releaseRiseSeconds']) * np.exp(-ages / p['releaseDecaySeconds']))

    def adjusted(self, unit, normalized):
        row = self.rows[unit]; p = row['calcium']
        phi = row['lengthGainAboveOptimal' if normalized >= 1 else 'lengthGainBelowOptimal']
        gain = phi * (normalized - 1)
        if abs(gain) >= 1:
            raise RuntimeError('Reference length-dependent parameters left their mathematical domain.')
        return (p['bindingRatePerSecond'] * (1 + gain), p['hillExponent'] * (1 - gain),
                p['halfActivationBinding'] * (1 - gain), p['cooperativity'] * (1 + gain))

    def mechanics(self, t, y, held_tendon=None, held_activation=None):
        g = self.g
        x, v = y[:2]; length = x / g['optimalFiberLengthMeters']; velocity = v / g['optimalFiberLengthMeters']
        if x <= 0:
            raise RuntimeError('Reference trial fiber length is nonpositive.')
        cosine = np.cos(g['pennationRadians'])
        tendon = (self.length(t) - x * cosine) / g['referenceTendonLengthMeters'] if held_tendon is None else held_tendon
        fl = np.exp(-abs((length ** 2.3 - 1) / 1.12) ** 1.62)
        fv = (7.88 + velocity) / (7.88 - 5.88 * velocity) if velocity <= 0 else (0.35 + (4.7 - 8.41 * length + 5.34 * length ** 2) * velocity) / (0.35 + velocity)
        pe2 = -0.02 * np.exp(-21 * (length - 0.70) - 1)
        active = sum((y[5 + 6 * i] if held_activation is None else held_activation[i]) *
            row['calcium']['peakTetanicForceNewtons'] * (fl * fv + pe2) for i, row in enumerate(self.rows))
        passive = g['referenceForceNewtons'] * (23 * 0.046 * np.logaddexp(0, (length / g['passiveLengthScale'] - 1.17) / .046) + .01 * velocity)
        axial = g['referenceForceNewtons'] * 27.8 * .0047 * np.logaddexp(0, (tendon - .964) / .0047)
        projected_tendon, projected_fiber = axial * cosine, (active + passive) * cosine ** 2
        geometric = g['fiberMassKg'] * v ** 2 * np.tan(g['pennationRadians']) ** 2 / x
        acceleration = (projected_tendon - projected_fiber + geometric) / g['fiberMassKg']
        return np.array([active, passive, axial, projected_tendon, projected_fiber, geometric, acceleration])

    def rhs(self, t, y, active=None):
        g = self.g; result = np.zeros_like(y)
        force = self.mechanics(t, y)
        if self.free:
            result[0], result[1] = y[1], force[-1]
        velocity = y[1] / g['optimalFiberLengthMeters']
        result[2] = (1 - .35 * (1 - np.exp(-abs(velocity) / .1)) - y[2]) / .2
        for i, row in enumerate(self.rows):
            p = row['calcium']; j = 3 + 6 * i; c, b, a = y[j:j + 3]
            k3, exponent, half, gamma = self.adjusted(i, y[0] / g['optimalFiberLengthMeters'])
            release = p['releaseRatePerSecond'] * (p['totalCalcium'] - c - b) * self.release(t, i, active)
            pump = p['pumpRatePerSecond'] * c * (p['storeSites'] - p['totalCalcium'] + c + b)
            binding = (1 - b) * (k3 * c - p['unbindingRatePerSecond'] * b / (1 + gamma * a))
            # Even extension for negative trial binding during implicit Newton/
            # finite-difference evaluations, never clipping accepted samples.
            power = abs(b * y[2] / half) ** exponent
            result[j:j + 6] = (release - pump - binding, binding,
                (power / (1 + power) - a) / p['activationSeconds'], release, pump, binding)
        result[-2] = g['fiberMassKg'] * result[1]
        result[-1] = result[-2] * y[1]
        return result

    def domain(self, states, tolerance=1e-12):
        y = np.atleast_2d(states)
        good = np.isfinite(y).all() and np.min(y[:, 0]) > 0 and np.min(y[:, 2]) >= .65 - tolerance and np.max(y[:, 2]) <= 1 + tolerance
        good &= np.min(y[:, 1] / self.g['optimalFiberLengthMeters']) >= -7.88 - tolerance
        for i, row in enumerate(self.rows):
            c, b, a = y[:, 3 + 6 * i:6 + 6 * i].T
            good &= np.min(c) >= -tolerance and np.min(b) >= -tolerance and np.min(a) >= -tolerance
            good &= np.max(b) <= 1 + tolerance and np.max(a) <= 1 + tolerance
            good &= np.min(row['calcium']['totalCalcium'] - c - b) >= -tolerance
            length = y[:, 0] / self.g['optimalFiberLengthMeters']
            gain = (length - 1) * np.where(length >= 1, row['lengthGainAboveOptimal'], row['lengthGainBelowOptimal'])
            good &= np.max(abs(gain)) < 1
        return bool(good)

    def solve(self, settings, grid):
        state = self.initial(); trace = np.full((len(grid), len(state)), np.nan)
        self.reference_partial = dict(samples=trace, completedBoundarySeconds=float(grid[0]),
            internalTimes=np.array([grid[0]]), internalStates=state[None, :].copy())
        boundary = np.unique(np.r_[grid[0], grid[-1], self.knots[:, 0], *self.events])
        stats = dict(nfev=0, njev=0, nlu=0, acceptedInternalPoints=0, acceptedInternalDomainPassed=True)
        scales = np.ones(len(state)); scales[0] = self.g['optimalFiberLengthMeters']; scales[1] = .1
        scales[-2:] = [.01, .001]
        for left, right in zip(boundary[:-1], boundary[1:]):
            active = [times[times <= left] for times in self.events]
            rhs = lambda t, y: self.rhs(t, y, active)
            # Bounded central Jacobian avoids unbounded adaptive factor growth
            # on constant ledger/silent-state columns. Cross-method agreement is
            # still mandatory; this is not an analytic-Jacobian claim.
            def jacobian(t, y):
                result = np.empty((len(y), len(y)))
                for col in range(len(y)):
                    h = 1e-7 * max(abs(y[col]), scales[col])
                    plus = y.copy(); minus = y.copy(); plus[col] += h; minus[col] -= h
                    result[:, col] = (rhs(t, plus) - rhs(t, minus)) / (2 * h)
                return result
            extra = dict(jac=jacobian) if settings['method'] == 'Radau' else {}
            answer = solve_ivp(rhs, (left, right), state, method=settings['method'], rtol=settings['rtol'],
                atol=settings['atol'], max_step=self.protocol['maximumReferenceStepSeconds'], dense_output=True, **extra)
            self.reference_partial['internalTimes'] = answer.t
            self.reference_partial['internalStates'] = answer.y.T
            if not answer.success:
                raise RuntimeError(answer.message)
            chosen = (grid >= left) & ((grid < right) if right < grid[-1] else (grid <= right))
            trace[chosen] = answer.sol(grid[chosen]).T; state = answer.y[:, -1]
            self.reference_partial['completedBoundarySeconds'] = float(right)
            stats['acceptedInternalPoints'] += len(answer.t)
            stats['acceptedInternalDomainPassed'] &= self.domain(answer.y.T)
            tendon = (np.array([self.length(t) for t in answer.t]) - answer.y[0] * np.cos(self.g['pennationRadians'])) / self.g['referenceTendonLengthMeters']
            stats['acceptedInternalDomainPassed'] &= bool(np.min(tendon) > 0)
            for key in ('nfev', 'njev', 'nlu'):
                stats[key] += getattr(answer, key)
        assert np.isfinite(trace).all(), 'Reference has missing/nonfinite samples.'
        forces = np.array([self.mechanics(t, y) for t, y in zip(grid, trace)])
        stats['sampleDomainPassed'] = self.domain(trace)
        return trace, forces, stats

    def source_ordering(self, step_ms, grid):
        """Controlled reduction of author ordering, not native MATLAB execution.

        Start-step release; Euler yield; Euler calcium; activation from updated
        binding/yield; mechanical RK4 holding activation and initial tendon.
        Equilibrium yield, prescribed inputs and common endpoint readout are
        declared diagnostic adaptations to isolate ordering from initial state.
        """
        assert self.n == 1
        state = self.initial(); trace = [state.copy()]; current_ms = 0.; next_sample = 1
        self.source_partial = dict(states=trace, completedTimeMs=0., lastState=state.copy())
        boundaries = sorted(set([e['arrivalAtMs'] for e in self.case['events']] + [k['timeMs'] for k in self.case['lengthKnots']]))
        duration = self.case['durationMs']; p = self.rows[0]['calcium']; g = self.g
        for tick in range(1, round(duration / step_ms) + 1):
            end = tick * step_ms
            pieces = [b for b in boundaries if current_ms < b < end] + [end]
            for stop in pieces:
                self.source_partial['lastState'] = state.copy()
                self.source_partial['completedTimeMs'] = current_ms
                h = (stop - current_ms) / 1000; t = current_ms / 1000
                rate = self.rhs(t, state)
                old = state.copy(); state[2] += h * rate[2]
                state[3:5] += h * rate[3:5]; state[6:9] += h * rate[6:9]
                _, exponent, half, _ = self.adjusted(0, old[0] / g['optimalFiberLengthMeters'])
                power = (max(state[4], 0) * state[2] / half) ** exponent
                state[5] += h * (power / (1 + power) - old[5]) / p['activationSeconds']
                tendon = (self.length(t) - old[0] * np.cos(g['pennationRadians'])) / g['referenceTendonLengthMeters']
                activation = [state[5]]
                def mechanical(xv):
                    trial = state.copy(); trial[:2] = xv
                    acceleration = self.mechanics(t, trial, tendon, activation)[-1] if self.free else 0
                    speed = xv[1] if self.free else 0
                    return np.array([speed, acceleration, g['fiberMassKg'] * acceleration, g['fiberMassKg'] * acceleration * speed])
                k1 = mechanical(old[:2]); k2 = mechanical(old[:2] + h / 2 * k1[:2])
                k3 = mechanical(old[:2] + h / 2 * k2[:2]); k4 = mechanical(old[:2] + h * k3[:2])
                delta = h / 6 * (k1 + 2 * k2 + 2 * k3 + k4)
                state[:2] = old[:2] + delta[:2]; state[-2:] += delta[2:]
                if not self.domain(state):
                    raise RuntimeError('Source-ordering diagnostic left its accepted physical domain; no state repair.')
                current_ms = stop
            if next_sample < len(grid) and end == next_sample * self.protocol['sampleMs']:
                trace.append(state.copy()); next_sample += 1
        trace = np.array(trace)
        assert trace.shape == (len(grid), len(state))
        return trace, np.array([self.mechanics(t, y) for t, y in zip(grid, trace)])

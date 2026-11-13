"""Continuous coupled joint/muscle oracle. Solve only in a visible study window.

The established Python muscle equations are reused; mechanical geometry and
joint dynamics participate at EVERY adaptive derivative evaluation. This is
independent of the candidate's start-held partition and C# muscle integrator.
It is not an independent physiological model or anatomical calibration.
"""
import numpy as np
from scipy.integrate import solve_ivp
from dynamic_muscle_reference import Reference


class MechanicalMuscle(Reference):
    def length(self, t):
        # Evaluation context, assigned from the current trial joint state by
        # CoupledReference before every rate or force call. Never integrated,
        # extrapolated or carried forward as an accepted physical state.
        return self.trial_path_length


class CoupledReference:
    def __init__(self, spec, case):
        self.spec, self.case, self.g = spec, case, spec['geometry']
        self.arms = np.asarray(spec['momentArmsMeters'])
        self.inertia = spec['inertiaKgMetersSquared']
        self.muscles = []
        for i in range(2):
            muscle_case = dict(boundary='Free', unitRows=[spec['unitRows'][i]],
                initialFiberLengthMeters=spec['initialFiberLengthMeters'],
                initialMusculotendonLengthMeters=spec['restPathLengthMeters'],
                lengthKnots=[dict(timeMs=0., offsetMeters=0.), dict(timeMs=case['durationMs'], offsetMeters=0.)],
                events=[dict(unitIndex=0, arrivalAtMs=e['arrivalAtMs']) for e in case['events'] if e['muscleIndex'] == i])
            self.muscles.append(MechanicalMuscle(spec, muscle_case))
        # q, angular velocity, torque impulse, joint work; two 11-value muscle
        # states (including independent flux/impulse/work integrals); finally
        # each tendon force-times-tendon-velocity integral for energy auditing.
        self.slices = [slice(4, 15), slice(15, 26)]

    def initial(self):
        return np.r_[self.case['initialAngleRadians'], self.case['initialSpeedRadiansPerSecond'], 0., 0.,
                     *(m.initial() for m in self.muscles), 0., 0.]

    def forces(self, t, y):
        answer = []
        for i, muscle in enumerate(self.muscles):
            muscle.trial_path_length = self.spec['restPathLengthMeters'] - self.arms[i] * y[0]
            answer.append(muscle.mechanics(t, y[self.slices[i]]))
        return np.asarray(answer)

    def rhs(self, t, y, active=None):
        result = np.zeros_like(y); forces = self.forces(t, y)
        torque = np.dot(self.arms, forces[:, 2])
        result[:4] = y[1], torque / self.inertia, torque, torque * y[1]
        cosine = np.cos(self.g['pennationRadians'])
        for i, muscle in enumerate(self.muscles):
            part = y[self.slices[i]]
            # self.forces assigned this trial state's path for each muscle;
            # nothing else modifies it before the corresponding muscle rates.
            result[self.slices[i]] = muscle.rhs(t, part, None if active is None else active[i])
            result[26 + i] = forces[i, 2] * (-self.arms[i] * y[1] - cosine * part[1])
        return result

    def domain(self, states):
        y = np.atleast_2d(states)
        if not np.isfinite(y).all():
            return False
        cosine = np.cos(self.g['pennationRadians'])
        for i, muscle in enumerate(self.muscles):
            part = y[:, self.slices[i]]
            path = self.spec['restPathLengthMeters'] - self.arms[i] * y[:, 0]
            if not muscle.domain(part) or np.min(path) <= 0 or np.min(path - cosine * part[:, 0]) <= 0:
                return False
        return True

    def solve(self, settings, grid):
        y = self.initial(); trace = np.full((len(grid), len(y)), np.nan)
        self.partial = dict(samples=trace, completedBoundarySeconds=float(grid[0]),
                            internalTimes=np.array([grid[0]]), internalStates=y[None, :].copy())
        boundaries = np.unique(np.r_[grid[0], grid[-1], *(m.events[0] for m in self.muscles)])
        if boundaries[0] != grid[0] or boundaries[-1] != grid[-1]:
            raise ValueError('Motor event lies outside the declared physical interval.')
        stats = dict(nfev=0, njev=0, nlu=0, acceptedInternalPoints=0, acceptedInternalDomainPassed=True)
        scales = np.ones(len(y)); scales[:4] = [.1, .1, .01, .001]; scales[-2:] = .001
        for sl in self.slices:
            scales[sl.start:sl.start + 2] = [self.g['optimalFiberLengthMeters'], .1]
            scales[sl.stop - 2:sl.stop] = [.01, .001]
        for left, right in zip(boundaries[:-1], boundaries[1:]):
            active = [[m.events[0][m.events[0] <= left]] for m in self.muscles]
            rhs = lambda t, state: self.rhs(t, state, active)

            def jacobian(t, state):
                result = np.empty((len(state), len(state)))
                for col in range(len(state)):
                    h = 1e-7 * max(abs(state[col]), scales[col])
                    plus = state.copy(); minus = state.copy(); plus[col] += h; minus[col] -= h
                    result[:, col] = (rhs(t, plus) - rhs(t, minus)) / (2 * h)
                return result

            extra = dict(jac=jacobian) if settings['method'] == 'Radau' else {}
            answer = solve_ivp(rhs, (left, right), y, method=settings['method'], rtol=settings['rtol'],
                atol=settings['atol'], max_step=self.spec['maximumReferenceStepSeconds'], dense_output=True, **extra)
            self.partial['internalTimes'], self.partial['internalStates'] = answer.t, answer.y.T
            if not answer.success:
                raise RuntimeError(answer.message)
            selected = (grid >= left) & ((grid < right) if right < grid[-1] else (grid <= right))
            if np.any(selected):
                trace[selected] = answer.sol(grid[selected]).T
            y = answer.y[:, -1]; self.partial['completedBoundarySeconds'] = float(right)
            stats['acceptedInternalPoints'] += len(answer.t)
            stats['acceptedInternalDomainPassed'] &= self.domain(answer.y.T)
            for key in ('nfev', 'njev', 'nlu'):
                stats[key] += getattr(answer, key)
        if not np.isfinite(trace).all():
            raise RuntimeError('Missing or nonfinite coupled reference samples.')
        forces = np.asarray([self.forces(t, state) for t, state in zip(grid, trace)])
        stats['sampleDomainPassed'] = self.domain(trace)
        return trace, forces, stats


def tendon_energy_change(geometry, start_tendon_meters, end_tendon_meters, order=64):
    """Integrate the declared constitutive force over actual tendon length.

    A second, higher quadrature order must agree before interpreting the result.
    This is mechanical potential energy, not ATP consumption or total tissue
    energy. No trajectory or state is changed by this diagnostic calculation.
    """
    start, end = np.broadcast_arrays(np.asarray(start_tendon_meters), np.asarray(end_tendon_meters))
    if not np.isfinite(start).all() or not np.isfinite(end).all() or np.min(start) <= 0 or np.min(end) <= 0:
        raise ValueError('Nonpositive/nonfinite tendon length for constitutive energy.')
    nodes, weights = np.polynomial.legendre.leggauss(order)
    flat_start, flat_end = start.reshape(-1), end.reshape(-1); output = np.empty(flat_start.shape)
    for index in range(0, len(output), 4096):
        left, right = flat_start[index:index + 4096], flat_end[index:index + 4096]
        half = (right - left) / 2
        sample = (left + half)[:, None] + half[:, None] * nodes[None, :]
        strain = (sample / geometry['referenceTendonLengthMeters'] - .964) / .0047
        force = geometry['referenceForceNewtons'] * 27.8 * .0047 * np.logaddexp(0, strain)
        output[index:index + 4096] = half * (force @ weights)
    return output.reshape(start.shape)

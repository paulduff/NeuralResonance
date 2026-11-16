# SPDX-License-Identifier: AGPL-3.0-only
"""Independent fixed-length integration of the pinned intrafusal equations.

Equations: Neuromechanics-Lab/spindleModelTutorial, revision
721ba03cb775261548766ed1349c4bf2e0e35385, halfSarcWithCoopBag/Chain.
See spindle-reference-LICENSE.txt; each package also retains the upstream license.
It is not an afferent, slack solver or live Avatar implementation.
No integration occurs at import; execute only in the visible study window.
"""
import numpy as np
from scipy.integrate import solve_ivp

VERSION = 'fixed-length-spindle-reference-v1'
FIBERS = ('bag', 'chain')
PROTOCOLS = ('quiet-hold', 'active-hold', 'drive-on-off')
STEPS_US = (1000, 500, 250)
SETTINGS = (
    dict(label='dop853-strict', method='DOP853', rtol=1e-10, atol=1e-12, max_step=.00025),
    dict(label='dop853-tighter', method='DOP853', rtol=1e-12, atol=1e-14, max_step=.000125),
    dict(label='radau-strict', method='Radau', rtol=1e-11, atol=1e-13, max_step=.000125),
)
# Prospective engineering criteria, not fitted physiology or inherited claims.
CRITERIA = dict(referenceStateAbsolute=1e-8, referenceStressAbsolutePa=.01,
    candidateStateAbsolute=1e-5, candidateStressAbsolutePa=1.,
    domainAbsolute=1e-8, conservationAbsolute=1e-10,
    analyticStateAbsolute=1e-9, jacobianAbsolute=2e-5)


def protocol():
    return dict(version=VERSION, fibers=list(FIBERS), protocols=list(PROTOCOLS),
        stepsUs=list(STEPS_US), settings=list(SETTINGS), criteria=CRITERIA,
        durationSeconds=.1, halfSarcomereLengthNm=1250.,
        nativeTrajectories=18, referenceSolves=18, analyticControls=6,
        referenceComparisons=18, candidateComparisons=54,
        nativeRerun=False, movingLengthAssessed=False, slackAssessed=False,
        afferentFiringAssessed=False, physiologicalCalibrationAssessed=False)


def intervals(name):
    if name == 'quiet-hold': return ((0., .1, 9.),)
    if name == 'active-hold': return ((0., .1, 6.5),)
    assert name == 'drive-on-off'
    return ((0., .02, 9.), (.02, .08, 6.5), (.08, .1, 9.))


class Kinetics:
    """84 physical state fractions; no clipping, projection or normalization."""
    def __init__(self, parameters, fiber):
        assert fiber in FIBERS
        self.p = parameters
        self.x = np.arange(-20., 20.5, .5)
        f0, g0, slack, passive, off = (600., 40., 1050., 90., 20.) if fiber == 'bag' else (400., 300., 1200., 250., 200.)
        for key, value in dict(f=f0, g=g0, power_stroke=2.5, hs_length=1250.,
                cmd_length=1250., hsl_slack=slack, k_passive=passive,
                compliance_factor=.5, k_cb=.001, cb_number_density=6.9e16,
                cb_force_constant=69000., bin_width=.5, thick_filament_length=815.,
                thin_filament_length=1120., bare_zone_length=80.).items():
            assert parameters[key] == value, key
        assert np.array_equal(parameters['x_bins'], self.x)
        # Recalculate the constructor once, checking the native MAT rates before
        # using its exact recorded binary64 constants in the independent solver.
        forward = np.minimum(f0 * np.exp(-.001 * 10 * self.x**2 / (1e18 * 1.381e-23 * 288)), 5000.)
        coefficient = np.full(81, .2) if fiber == 'bag' else np.where(self.x < -5, .2, .4)
        reverse = np.minimum(g0 + coefficient * np.abs(self.x + 5)**3 + (.5 if fiber == 'bag' else 10.), 5000.)
        assert np.allclose(forward, parameters['forward_rates'], rtol=5e-14, atol=1e-300)
        assert np.allclose(reverse, parameters['reverse_rates'], rtol=5e-14, atol=1e-12)
        self.attach = np.asarray(parameters['forward_rates']) * .5
        self.detach = np.asarray(parameters['reverse_rates'])
        self.overlap = (1120. - (1250. - 815.)) / (815. - 80.)
        self.on = 80000000.
        self.off = off

    def initial(self):
        y = np.zeros(84); y[0] = y[-2] = 1.
        return y

    def rhs(self, pca):
        attach, detach, overlap, on, off = self.attach, self.detach, self.overlap, self.on * 10.**(-pca), self.off
        def evaluate(t, y):
            bound = y[1:-2]; total_bound = np.sum(bound); active = y[-1]
            flux = attach * y[0] * (active - total_bound) - detach * bound
            activation = on * (overlap - active) * (1 + active / overlap)
            deactivation = off * (active - total_bound) * (2 - active / overlap)
            result = np.empty(84)
            result[0] = -np.sum(flux); result[1:-2] = flux
            result[-1] = activation - deactivation; result[-2] = -result[-1]
            return result
        return evaluate

    def jac(self, pca):
        attach, detach, overlap, on, off = self.attach, self.detach, self.overlap, self.on * 10.**(-pca), self.off
        def evaluate(t, y):
            total_bound = np.sum(y[1:-2]); active = y[-1]
            j = np.zeros((84, 84))
            j[1:-2, 0] = attach * (active - total_bound)
            j[1:-2, 1:-2] = -(attach * y[0])[:, None] - np.diag(detach)
            j[1:-2, -1] = attach * y[0]
            j[0] = -np.sum(j[1:-2], axis=0)
            j[-1, 1:-2] = off * (2 - active / overlap)
            j[-1, -1] = -2 * on * active / overlap - off * (2 - 2 * active / overlap + total_bound / overlap)
            j[-2] = -j[-1]
            return j
        return evaluate

    def stress(self, states):
        # Separate matrix multiplication avoids reusing native force evaluation.
        cross = 69000. * (np.asarray(states)[..., 1:-2] @ (self.x + 2.5))
        passive = self.p['k_passive'] * (1250. - self.p['hsl_slack'])
        return cross + passive, cross


def accounting(states, overlap):
    y = np.asarray(states); assert y.ndim == 2 and y.shape[1] == 84 and len(y)
    assert np.isfinite(y).all(), 'Nonfinite state retained; no repair permitted.'
    bound = y[:, 1:-2].sum(axis=1)
    mass = float(np.max(np.abs(y[:, 0] + bound - 1)))
    actin = float(np.max(np.abs(y[:, -2] + y[:, -1] - 1)))
    violation = float(max(0., -y.min(), np.max(bound - y[:, -1]), np.max(y[:, -1] - overlap)))
    return dict(myosinMassError=mass, actinMassError=actin, domainViolation=violation,
        passed=bool(max(mass, actin) <= CRITERIA['conservationAbsolute'] and violation <= CRITERIA['domainAbsolute']))


def integrate(model, name, times, setting, out):
    """Continuous state across input edges; store each accepted solver node."""
    assert np.all(np.diff(times) > 0) and times[0] == 0 and times[-1] == .1
    y = model.initial(); sampled = np.empty((len(times), 84)); sampled[0] = y
    filled = np.zeros(len(times), dtype=bool); filled[0] = True
    nodes_t, nodes_y, stats = [], [], []
    for index, (start, end, pca) in enumerate(intervals(name)):
        options = {k: v for k, v in setting.items() if k != 'label'}
        if options['method'] == 'Radau': options['jac'] = model.jac(pca)
        solution = solve_ivp(model.rhs(pca), (start, end), y, dense_output=True, **options)
        # Save a completed prefix before assessing success. No retries or new
        # tolerances follow an unsuccessful or nonfinite result.
        np.savez(out / f'segment-{index}.npz', times=solution.t, states=solution.y.T)
        stats.append(dict(startSeconds=start, endSeconds=end, pCa=pca, success=bool(solution.success),
            message=solution.message, nfev=solution.nfev, njev=solution.njev, nlu=solution.nlu,
            acceptedNodes=len(solution.t)))
        if not solution.success: raise RuntimeError(str(stats[-1]))
        assert solution.t[-1] == end and np.isfinite(solution.y).all()
        selected = (times > start) & (times <= end)
        assert not filled[selected].any()
        sampled[selected] = solution.sol(times[selected]).T; filled[selected] = True
        # The accepted endpoint is the authoritative continuation state. Some
        # dense interpolants round differently at their own right endpoint.
        sampled[times == end] = solution.y[:, -1]
        y = solution.y[:, -1].copy()
        nodes_t.extend(solution.t); nodes_y.extend(solution.y.T)
    assert filled.all() and np.isfinite(sampled).all()
    np.savez(out / 'trace.npz', times=times, states=sampled,
        internal_times=np.asarray(nodes_t), internal_states=np.asarray(nodes_y))
    return dict(completed=True, settings=setting, segments=stats,
        internal=accounting(nodes_y, model.overlap), sampled=accounting(sampled, model.overlap))


def analytic_controls(parameters, out):
    """Two exact limiting systems and a finite-difference Jacobian check."""
    records = []; times = np.linspace(0, .01, 101)
    for condition in ('detachment-only', 'actin-only'):
        for setting in SETTINGS:
            model = Kinetics(parameters, 'bag'); y = model.initial()
            model.attach = np.zeros(81); model.off = 0.
            expected = np.tile(y, (len(times), 1))
            if condition == 'detachment-only':
                model.on = 0.; y[1:-2] = .2 / 81; y[0] = .8; y[-1] = .4; y[-2] = .6
                expected[:, 1:-2] = y[1:-2] * np.exp(-times[:, None] * model.detach)
                expected[:, 0] = 1 - expected[:, 1:-2].sum(axis=1)
                expected[:, -1] = .4; expected[:, -2] = .6
            else:
                model.detach = np.zeros(81)
                expected[:, -1] = model.overlap * np.tanh(model.on * 10**-6.5 * times)
                expected[:, -2] = 1 - expected[:, -1]
            options = {k: v for k, v in setting.items() if k != 'label'}
            if options['method'] == 'Radau': options['jac'] = model.jac(6.5)
            result = solve_ivp(model.rhs(6.5), (0., .01), y, dense_output=True, **options)
            assert result.success and result.t[-1] == .01
            actual = result.sol(times).T
            np.savez(out / f'{condition}-{setting["label"]}.npz', times=times, states=actual,
                expected=expected, internal_times=result.t, internal_states=result.y.T)
            error = float(np.max(np.abs(actual - expected)))
            records.append(dict(condition=condition, setting=setting, maximumStateError=error,
                passed=bool(error <= CRITERIA['analyticStateAbsolute'])))
    model = Kinetics(parameters, 'bag'); y = model.initial()
    y[1:-2] = .2 / 81; y[0] = .8; y[-1] = .4; y[-2] = .6
    rhs = model.rhs(6.5); step = 1e-6
    approximate = np.column_stack([(rhs(0., y + direction * step) - rhs(0., y - direction * step)) / (2 * step)
                                   for direction in np.eye(84)])
    exact = model.jac(6.5)(0., y)
    np.savez(out / 'jacobian.npz', approximate=approximate, analytic=exact, state=y)
    error = float(np.max(np.abs(exact - approximate)))
    return dict(completed=True, analytic=records, jacobianMaximumError=error,
        passed=bool(all(r['passed'] for r in records) and error <= CRITERIA['jacobianAbsolute']),
        physiologicalControls=False)

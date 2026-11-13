"""Analytic reference/recorder controls; execute only in the visible study window."""
import json
import os
from pathlib import Path
import sys
import traceback
for name in ('OMP_NUM_THREADS', 'OPENBLAS_NUM_THREADS', 'MKL_NUM_THREADS', 'NUMEXPR_NUM_THREADS'):
    os.environ[name] = '1'
import numpy as np
from scipy.integrate import quad
from causal_motor_reference import CoupledReference, tendon_energy_change


class ConstantForceFixture:
    """Explicit artificial constant tension, with unevolving muscle state.

    Replaces the biological muscle ONLY for a closed-form Newtonian oracle.
    Clustered event boundaries still exercise the real reference recorder.
    """
    def __init__(self, force, spec):
        self.force, self.spec = force, spec
        self.events = [np.array([.000015625, .00003125, .000109375])]

    def initial(self):
        state = np.zeros(11); state[0] = self.spec['initialFiberLengthMeters']; state[2] = 1
        return state

    def mechanics(self, t, y):
        return np.array([0., 0., self.force, self.force * np.cos(self.spec['geometry']['pennationRadians']), 0., 0., 0.])

    def rhs(self, t, y, active=None):
        return np.zeros_like(y)

    def domain(self, y):
        return bool(np.isfinite(y).all())


def run(package):
    spec = json.loads((package / 'causal-input.json').read_text(encoding='utf-8-sig'))
    outcomes = []
    for name, tension in (('balanced-constant-tension', (2., 2.)), ('unequal-constant-tension', (2., 1.))):
        case = dict(key=name, initialAngleRadians=0., initialSpeedRadiansPerSecond=.01, durationMs=1., events=[])
        grid = np.arange(9) * .000125
        for settings in (s for s in spec['referenceSettings'] if s['key'] in ('dop853-tight', 'radau')):
            ref = CoupledReference(spec, case)
            ref.muscles = [ConstantForceFixture(force, spec) for force in tension]
            expected = np.tile(ref.initial(), (len(grid), 1))
            torque = sum(arm * force for arm, force in zip(spec['momentArmsMeters'], tension))
            inertia = spec['inertiaKgMetersSquared']; acceleration = torque / inertia
            expected[:, 0] = case['initialSpeedRadiansPerSecond'] * grid + .5 * acceleration * grid ** 2
            expected[:, 1] = case['initialSpeedRadiansPerSecond'] + acceleration * grid
            expected[:, 2] = torque * grid
            expected[:, 3] = .5 * inertia * (expected[:, 1] ** 2 - case['initialSpeedRadiansPerSecond'] ** 2)
            for i in range(2):
                expected[:, 26 + i] = -tension[i] * spec['momentArmsMeters'][i] * expected[:, 0]
            try:
                state, forces, stats = ref.solve(settings, grid)
                error = float(np.max(abs(state - expected)))
                force_error = max(float(np.max(abs(forces[:, i, 2] - tension[i]))) for i in range(2))
                if not np.isfinite(error) or not np.isfinite(force_error):
                    raise RuntimeError('Nonfinite analytic control comparison.')
                passed = bool(error <= 1e-12 and force_error == 0 and stats['acceptedInternalDomainPassed'] and stats['sampleDomainPassed'])
                # Retain the actual traces even when the analytic bound fails.
                np.savez_compressed(package / (name + '-' + settings['key'] + '-control.npz'),
                                    timeSeconds=grid, state=state, expected=expected, forces=forces)
                outcomes.append(dict(case=name, method=settings['key'], maximumAnalyticStateError=error,
                    maximumTensionErrorNewtons=force_error, emptyRecordingIntervalsPresent=True, passed=passed))
            except Exception:
                if hasattr(ref, 'partial'):
                    np.savez_compressed(package / (name + '-' + settings['key'] + '-control-partial.npz'),
                        timeSeconds=grid, expected=expected, **ref.partial)
                outcomes.append(dict(case=name, method=settings['key'], error=traceback.format_exc(), passed=False))
    g = spec['geometry']; length = g['referenceTendonLengthMeters']; quadrature = []
    intervals = [(length * .93, length * .95), (length * .95, length * .93),
                 (length * .964, length * .964), (length * .962, length * .966)]
    def force_at(x):
        return g['referenceForceNewtons'] * 27.8 * .0047 * np.logaddexp(0., (x / length - .964) / .0047)
    for left, right in intervals:
        independent, estimate = quad(force_at, left, right, epsabs=1e-14, epsrel=1e-12)
        low = float(tendon_energy_change(g, left, right, 64)); high = float(tendon_energy_change(g, left, right, 128))
        errors = [abs(low - independent), abs(high - independent), abs(low - high)]
        quadrature.append(dict(startTendonMeters=left, endTendonMeters=right, quadJoules=independent,
            quadEstimatedErrorJoules=estimate, gauss64Joules=low, gauss128Joules=high,
            maximumDifferenceJoules=max(errors), passed=max(errors) <= spec['tendonQuadratureAgreementJoules'] and estimate <= 1e-12))
    result = dict(completed=True, analyticMechanicalConditions=2, analyticMethodChecks=4,
        tendonQuadratureCases=4, exactAnalyticMechanicalFixtures=True, biologicalDynamicsIntegrated=False,
        tendonConstitutiveEvaluated=True, physiologicalCalibrationAssessed=False,
        mechanics=outcomes, quadrature=quadrature, passed=all(o['passed'] for o in outcomes + quadrature))
    with (package / 'reference-controls.json').open('x', encoding='utf-8') as stream:
        stream.write(json.dumps(result, indent=2, allow_nan=False) + '\n')
    if not result['passed']:
        raise RuntimeError('Coupled reference controls failed; preserve evidence before model comparison.')
    print('Four analytic joint integration checks and four constitutive quadrature checks passed.', flush=True)


if __name__ == '__main__':
    if len(sys.argv) != 2:
        raise SystemExit('Supply the frozen package directory.')
    run(Path(sys.argv[1]).resolve())

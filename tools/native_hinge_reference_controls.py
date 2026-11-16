"""Nonlinear geometry and adaptive-reference controls; run only in PowerShell."""
import cmath
import json
import traceback
import numpy as np
from native_hinge_reference import HingeGeometry, HingeCoupledReference


class ConstantTension:
    """Artificial method control only; all muscle/calcium states remain fixed."""
    def __init__(self, tension, spec):
        self.tension, self.spec = tension, spec
        self.events = [np.array([.000015625, .00003125, .000109375])]

    def initial(self):
        y = np.zeros(11); y[0] = self.spec['initialFiberLengthMeters']; y[2] = 1
        return y

    def mechanics(self, t, y):
        return np.array([0., 0., self.tension, self.tension*np.cos(self.spec['geometry']['pennationRadians']), 0., 0., 0.])

    def rhs(self, t, y, active=None): return np.zeros_like(y)
    def domain(self, y): return bool(np.isfinite(y).all())


def run_controls(spec, descriptor, output):
    output.mkdir(); geometry = HingeGeometry(descriptor); geometric = []
    for q in np.linspace(-.75, .75, 49):
        measured = geometry.measure(q); lengths = []; derivatives = []; powers = []; torques = []
        for i in range(2):
            ax, ay, az = geometry.roots[i]; cx, cy, cz = geometry.locals[i]
            z = complex(q, 1e-20)
            # Independent scalar law of cosines, with complex-step derivative.
            square = (cx-ax)**2 + cy*cy + cz*cz + ay*ay + az*az - 2*(
                ay*(cy*cmath.cos(z)-cz*cmath.sin(z)) + az*(cy*cmath.sin(z)+cz*cmath.cos(z)))
            analytic = cmath.sqrt(square)
            lengths.append(abs(analytic.real - measured['length'][i]))
            derivatives.append(abs(analytic.imag / 1e-20 - measured['derivative'][i]))
            force = -10*measured['directions'][i]
            torque = np.cross(measured['points'][i], force)[0]
            torques.append(abs(torque + 10*measured['derivative'][i]))
            for speed in (-.25, .25):
                point_power = np.dot(force, measured['pointDerivative'][i]*speed)
                powers.append(abs(point_power - torque*speed))
        errors = dict(lengthMeters=float(max(lengths)), derivativeMetersPerRadian=float(max(derivatives)),
            torqueNewtonMeters=float(max(torques)), powerWatts=float(max(powers)))
        geometric.append(dict(angleRadians=float(q), **errors,
            passed=errors['lengthMeters']<=2e-15 and errors['derivativeMetersPerRadian']<=2e-12 and
                   errors['torqueNewtonMeters']<=2e-12 and errors['powerWatts']<=2e-12))
    rows = []; traces = {}; grid = np.arange(81)*.000125
    for name, tension, q0, w0 in [('free', (0., 0.), .2, .1), ('balanced', (2., 2.), 0., 0.),
                                ('nonlinear-conservative', (2., 1.), .2, .1)]:
        case = dict(key=name, initialAngleRadians=q0, initialSpeedRadiansPerSecond=w0, durationMs=10., events=[])
        for settings in (s for s in spec['referenceSettings'] if s['key'] in ('dop853-tight', 'radau')):
            key = name + '-' + settings['key']; ref = HingeCoupledReference(spec, case, geometry)
            ref.muscles = [ConstantTension(force, spec) for force in tension]
            try:
                y, forces, stats = ref.solve(settings, grid); traces[key] = y
                path = geometry.measure(y[:, 0])['length']; initial_path = geometry.measure(q0)['length']
                expected_tendon_work = (path-initial_path)*np.asarray(tension)
                potential_change = np.sum(expected_tendon_work, axis=1)
                kinetic_change = .5*geometry.inertia*(y[:, 1]**2-w0*w0)
                energy = float(np.max(abs(kinetic_change + potential_change)))
                momentum = float(np.max(abs(geometry.inertia*(y[:, 1]-w0)-y[:, 2])))
                work = float(np.max(abs(kinetic_change-y[:, 3])))
                tendon_work = float(np.max(abs(y[:, 26:28]-expected_tendon_work)))
                static_state = float(np.max(abs(y[:, 4:26] - ref.initial()[None, 4:26])))
                force_error = float(np.max(abs(forces[:, :, 2]-np.asarray(tension))))
                angle_error = float(np.max(abs(y[:, 0]-(q0+w0*grid)))) if name=='free' else (float(np.max(abs(y[:, 0]))) if name=='balanced' else None)
                speed_error = float(np.max(abs(y[:, 1]-w0))) if name!='nonlinear-conservative' else None
                passed = bool(max(energy, momentum, work, tendon_work)<=1e-10 and static_state<=1e-12 and force_error==0 and
                    (angle_error is None or max(angle_error, speed_error)<=1e-12) and stats['acceptedInternalDomainPassed'] and stats['sampleDomainPassed'])
                np.savez_compressed(output/(key+'.npz'), timeSeconds=grid, state=y, forces=forces,
                    pathLengths=path, kineticChange=kinetic_change, potentialChange=potential_change)
                rows.append(dict(case=name, method=settings['key'], maximumEnergyResidualJoules=energy,
                    maximumMomentumResidualNewtonMeterSeconds=momentum, maximumJointWorkResidualJoules=work,
                    maximumTendonWorkResidualJoules=tendon_work, maximumFrozenMuscleStateError=static_state,
                    maximumForceErrorNewtons=force_error, maximumAnalyticAngleErrorRadians=angle_error,
                    maximumAnalyticSpeedErrorRadiansPerSecond=speed_error, passed=passed))
            except Exception:
                if hasattr(ref, 'partial'): np.savez_compressed(output/(key+'-partial.npz'), **ref.partial)
                rows.append(dict(case=name, method=settings['key'], passed=False, error=traceback.format_exc()))
    cross = []
    for name in ('free', 'balanced', 'nonlinear-conservative'):
        if name+'-dop853-tight' not in traces or name+'-radau' not in traces:
            cross.append(dict(case=name, passed=False)); continue
        error = np.max(abs(traces[name+'-dop853-tight']-traces[name+'-radau']), axis=0)
        cross.append(dict(case=name, maximumAngleDifferenceRadians=float(error[0]), maximumSpeedDifferenceRadiansPerSecond=float(error[1]),
            passed=bool(error[0]<=1e-9 and error[1]<=1e-8)))
    result = dict(completed=len(geometric)==49 and len(rows)==6 and len(cross)==3,
        geometryCases=geometric, mechanicalMethodChecks=rows, crossMethodChecks=cross,
        passed=all(row['passed'] for row in geometric+rows+cross),
        artificialConstantTensionControls=True, biologicalDynamicsIntegrated=False, nativePhysicsExecuted=False,
        physiologicalCalibrationQualified=False)
    with (output/'controls.json').open('x') as stream: json.dump(result, stream, indent=2, allow_nan=False)
    if not result['passed']: raise RuntimeError('Nonlinear hinge reference controls failed; retain the original criteria.')
    return result

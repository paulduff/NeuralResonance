"""Continuous nonlinear hinge/muscle reference; execute only in a visible study.

Reuses the qualified continuous muscle equations and adaptive recorder, not the
candidate's held-force partition, native stepping or C# geometry implementation.
The fixture remains synthetic and is not calibrated human joint anatomy.
"""
import numpy as np
from causal_motor_reference import CoupledReference, MechanicalMuscle


class HingeGeometry:
    def __init__(self, descriptor):
        if descriptor['version'] != 'native-opposing-hinge-v1' or not descriptor['rootImmovable'] or any(
                descriptor[k] for k in ('gravity', 'damping', 'friction', 'driveForce')):
            raise ValueError('Require the declared isolated free hinge, without external drive or gravity.')
        # The descriptor fields below originate as actual Unity binary32 values.
        # Recover those values, not an idealized decimal 0.3/0.4 metre fixture.
        anchors = np.asarray([[a[k] for k in ('x', 'y', 'z')] for a in descriptor['anchors']], dtype=np.float32).astype(float)
        if anchors.shape != (4, 3) or not np.isfinite(anchors).all():
            raise ValueError('Require both complete finite root/child attachment pairs.')
        self.roots = anchors[[0, 2]].copy(); self.locals = anchors[[1, 3]].copy()
        self.roots.setflags(write=False); self.locals.setflags(write=False)
        mass, central, center = (float(np.float32(descriptor[k])) for k in ('mass', 'centralInertia', 'centerY'))
        self.inertia = central + mass * center * center
        if not np.isfinite([mass, central, center, self.inertia]).all() or mass <= 0 or central <= 0:
            raise ValueError('Invalid declared hinge mass or inertia.')

    def measure(self, angle):
        if np.iscomplexobj(angle):
            raise ValueError('Physical hinge angle must be real.')
        q = np.asarray(angle, dtype=float)
        if not q.size or not np.isfinite(q).all():
            raise ValueError('Nonfinite or empty physical coordinate.')
        cosine, sine = np.cos(q)[..., None], np.sin(q)[..., None]
        x = np.broadcast_to(self.locals[:, 0], q.shape + (2,))
        y = cosine * self.locals[:, 1] - sine * self.locals[:, 2]
        z = sine * self.locals[:, 1] + cosine * self.locals[:, 2]
        points = np.stack((x, y, z), axis=-1)
        derivative = np.stack((np.zeros_like(y), -z, y), axis=-1)
        delta = points - self.roots
        length = np.sqrt(np.sum(delta * delta, axis=-1))
        if not np.isfinite(length).all() or np.any(length <= 0):
            raise ValueError('Collapsed or nonfinite muscle path; no geometric clipping.')
        units = delta / length[..., None]
        length_derivative = np.sum(units * derivative, axis=-1)
        return dict(length=length, derivative=length_derivative, points=points,
                    pointDerivative=derivative, directions=units)


class HingeCoupledReference(CoupledReference):
    """Only the already-reviewed adaptive integration/recorder is inherited.

    Geometry, forces, angular dynamics and tendon work are recomputed at each
    adaptive trial state. No native endpoint is interpolated into the reference.
    """
    def __init__(self, spec, case, geometry):
        self.spec, self.case, self.g = spec, case, spec['geometry']
        self.hinge = geometry; self.inertia = geometry.inertia
        if len(spec['unitRows']) != 2 or not np.isfinite(case['initialSpeedRadiansPerSecond']):
            raise ValueError('Require two declared one-unit fixture muscles and finite initial speed.')
        initial_paths = geometry.measure(case['initialAngleRadians'])['length']
        self.muscles = []
        for i in range(2):
            muscle_case = dict(boundary='Free', unitRows=[spec['unitRows'][i]],
                initialFiberLengthMeters=spec['initialFiberLengthMeters'],
                initialMusculotendonLengthMeters=float(initial_paths[i]),
                lengthKnots=[dict(timeMs=0., offsetMeters=0.), dict(timeMs=case['durationMs'], offsetMeters=0.)],
                events=[dict(unitIndex=0, arrivalAtMs=e['arrivalAtMs']) for e in case['events'] if e['muscleIndex'] == i])
            self.muscles.append(MechanicalMuscle(spec, muscle_case))
        self.slices = [slice(4, 15), slice(15, 26)]

    def forces(self, t, y):
        lengths = self.hinge.measure(y[0])['length']; forces = []
        for i, muscle in enumerate(self.muscles):
            muscle.trial_path_length = float(lengths[i])
            forces.append(muscle.mechanics(t, y[self.slices[i]]))
        return np.asarray(forces)

    def rhs(self, t, y, active=None):
        result = np.zeros_like(y); forces = self.forces(t, y)
        derivatives = self.hinge.measure(y[0])['derivative']
        torque = -np.dot(derivatives, forces[:, 2])
        result[:4] = y[1], torque / self.inertia, torque, torque * y[1]
        cosine = np.cos(self.g['pennationRadians'])
        for i, muscle in enumerate(self.muscles):
            part = y[self.slices[i]]
            result[self.slices[i]] = muscle.rhs(t, part, None if active is None else active[i])
            result[26 + i] = forces[i, 2] * (derivatives[i] * y[1] - cosine * part[1])
        return result

    def domain(self, states):
        y = np.atleast_2d(states)
        if not np.isfinite(y).all():
            return False
        try:
            paths = self.hinge.measure(y[:, 0])['length']
        except ValueError:
            return False
        cosine = np.cos(self.g['pennationRadians'])
        return all(m.domain(y[:, sl]) and np.min(paths[:, i] - cosine * y[:, sl.start]) > 0
                   for i, (m, sl) in enumerate(zip(self.muscles, self.slices)))

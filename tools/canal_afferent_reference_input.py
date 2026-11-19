"""Prospective independent canal input for sensory ODE comparisons.

Execute equations only in a visible frozen study. This module imports no native
trajectory or stage reader. It does not launch a study or qualify a reference.
Physical intervals are explicit: an ODE solver must stop at each input change.
"""
from dataclasses import dataclass
from functools import lru_cache
import math
from uuid import UUID

import numpy as np
from scipy.linalg import expm

from coupled_canal_reference import generator_binary64, source_matrices


VERSION = 'independent-canal-sensory-input-v1'


def integer(value, name, minimum, maximum):
    if type(value) is not int or not minimum <= value <= maximum:
        raise ValueError(f'{name} must be an integer in [{minimum}, {maximum}].')
    return value


def finite(value, name):
    if isinstance(value, (bool, str, bytes)):
        raise ValueError(name + ' must be a finite number.')
    value = float(value)
    if not math.isfinite(value):
        raise ValueError(name + ' must be finite.')
    return value


def within(value, left, right):
    value = finite(value, 'Local time')
    if not left <= value <= right:
        raise ValueError('Query outside the declared physical or source interval.')
    return value


@dataclass(frozen=True)
class BundleProjection:
    """Declared shear geometry, derived from source stiffness, not native output."""

    canal: int
    organ_id: str
    hair_cell_id: str
    radius_m: float
    area_m2: float
    gain_nm_per_m3: float

    @classmethod
    def from_spec(cls, spec, catalog):
        geometry, attachment = spec['geometry'], spec['attachment']
        canal = integer(geometry['canal'], 'Canal', 0, 2)
        expected_order = [kind + '-' + axis for kind in ('endolymph', 'cupula') for axis in ('HC', 'AC', 'PC')]
        if catalog['stateOrder'] != expected_order:
            raise ValueError('Mechanical state order differs from the declared source coordinates.')
        if geometry['sourceSha256'].lower() != catalog['retainedHtmlSha256'].lower():
            raise ValueError('Spatial material and mechanical source identities differ.')
        stiffness = float(source_matrices(catalog)[2][3 + canal][3 + canal])
        shear = finite(geometry['shearModulusPascal'], 'Shear modulus')
        aspect = finite(geometry['thicknessOverRadius'], 'Thickness/radius')
        length = finite(attachment['bundleLengthMeters'], 'Bundle length')
        x = finite(attachment['rootXOverRadius'], 'Root coordinate')
        polarity = attachment['excitatoryPolarity']
        if type(polarity) is not int or polarity not in (-1, 1):
            raise ValueError('Polarity must be explicitly -1 or +1.')
        if not all(math.isfinite(v) and v > 0 for v in (stiffness, shear, aspect, length)) or not abs(x) < 1:
            raise ValueError('Invalid stiffness, material, aspect or attachment.')
        radius = (8 * shear * aspect / (math.pi * stiffness)) ** (1 / 3)
        area = math.pi * radius * radius
        depth = math.sqrt(1 - x * x)
        height = length / radius
        if not 0 < height < 2 * depth:
            raise ValueError('Bundle tip must lie strictly inside the declared disk.')
        # Integral mode u=2 Q/A (1-r^2/R^2), root on the lower boundary.
        # Factoring avoids subtracting nearly equal squares at short lengths.
        shape = 2 * height * (2 * depth - height)
        gain = polarity * shape / area * 1e9
        if not all(math.isfinite(v) and v > 0 for v in (radius, area)) or not math.isfinite(gain):
            raise ValueError('Unrepresentable spatial projection.')
        hair_id = attachment['hairCellId']
        if not isinstance(hair_id, str) or UUID(hair_id).int == 0:
            raise ValueError('An explicit receiving hair-cell identity is required.')
        if not isinstance(geometry['organId'], str) or not geometry['organId'].strip():
            raise ValueError('An explicit organ identity is required.')
        return cls(canal, geometry['organId'], hair_id, radius, area, gain)


@dataclass(frozen=True)
class HeldBundleInput:
    """One closed integration segment; the next segment owns its new forcing.

    The right endpoint uses this segment's held value when the ODE evaluates its
    final stage. Callers must start a separate solve for the next source step.
    There is deliberately no floor(time/step) dispatch inside a solver callback.
    """

    source_step: int
    start_ms: float
    end_ms: float
    bundle_nm: float

    def __post_init__(self):
        integer(self.source_step, 'Source step', 0, 2**53 - 1)
        for key in ('start_ms', 'end_ms', 'bundle_nm'):
            finite(getattr(self, key), key)
        if not 0 <= self.start_ms < self.end_ms:
            raise ValueError('A held segment needs distinct nonnegative endpoints.')

    def at_ms(self, local_ms):
        within(local_ms, self.start_ms, self.end_ms)
        return self.bundle_nm


class CanalInterval:
    """Independent linear response on one explicit physical interval.

    State is six volumes, six flows, three held angular accelerations. Seconds
    belong to the published mechanical equations; sensory callbacks use ms.
    Queries are pure matrix-exponential evaluations from the interval's initial
    state. Their order, retries and solver rejection do not advance any state.
    SciPy arithmetic still needs comparison against the 60/90-digit reference.
    """

    def __init__(self, generator, initial, angular, duration_ms, canal, gain):
        self.duration_ms = finite(duration_ms, 'Physical interval duration')
        self.canal = integer(canal, 'Canal', 0, 2)
        self.gain = finite(gain, 'Declared bundle projection')
        if self.duration_ms <= 0 or self.gain == 0:
            raise ValueError('Duration and bundle projection must be nonzero.')
        self._generator = np.array(generator, dtype=np.float64, copy=True)
        self._initial = np.array(initial, dtype=np.float64, copy=True)
        self._angular = np.array(angular, dtype=np.float64, copy=True)
        if self._generator.shape != (15, 15) or self._initial.shape != (12,) or self._angular.shape != (3,):
            raise ValueError('Expected a 15-state generator, 12-state initial point and three angular inputs.')
        if not all(np.isfinite(a).all() for a in (self._generator, self._initial, self._angular)):
            raise ValueError('Nonfinite independent mechanical input.')
        if np.any(self._generator[12:]):
            raise ValueError('Angular input must remain held on the physical interval.')
        for array in (self._generator, self._initial, self._angular):
            array.setflags(write=False)
        # Per-interval bounded cache. Return immutable tuples so a caller cannot
        # mutate a later callback or share mutable solver state through the cache.
        self._point = lru_cache(maxsize=32)(self._evaluate)

    @classmethod
    def from_source(cls, catalog, projection, initial, angular, duration_ms):
        return cls(generator_binary64(catalog), initial, angular, duration_ms,
                   projection.canal, projection.gain_nm_per_m3)

    def _evaluate(self, local_ms):
        if local_ms == 0:
            return tuple(self._initial)
        seconds = local_ms / 1000
        if seconds == 0:
            raise ValueError('Milliseconds-to-seconds conversion lost time resolution.')
        point = np.concatenate((self._initial, self._angular))
        value = expm(self._generator * seconds) @ point
        if not np.isfinite(value).all():
            raise ArithmeticError('Independent mechanical exponential became nonfinite.')
        return tuple(value[:12])

    def point_at_ms(self, local_ms):
        return self._point(within(local_ms, 0, self.duration_ms))

    def at_ms(self, local_ms):
        value = self.gain * self.point_at_ms(local_ms)[3 + self.canal]
        if not math.isfinite(value):
            raise ArithmeticError('Independent bundle projection became nonfinite.')
        return value

    def held_step(self, source_step, steps_per_interval):
        count = integer(steps_per_interval, 'Steps per interval', 1, 2**24)
        if count & (count - 1):
            raise ValueError('The connected source clock requires a dyadic step count.')
        index = integer(source_step, 'Source step', 0, count - 1)
        step_ms = self.duration_ms / count
        start, end = index * step_ms, (index + 1) * step_ms
        if not 0 <= start < end <= self.duration_ms:
            raise ValueError('Source grid lost representable time resolution.')
        return HeldBundleInput(index, start, end, self.at_ms(start))

    def continue_with(self, angular):
        """Retain the complete mechanical endpoint; change only next input."""
        return CanalInterval(self._generator, self.point_at_ms(self.duration_ms), angular,
                             self.duration_ms, self.canal, self.gain)


class DrivenSensoryEquation:
    """Apply an explicit independent/held input to the existing reference ODE.

    Mechanical input has no dependence on neuronal state in this declared
    one-way closure. The existing full neuronal/ledger Jacobian therefore stays
    valid; the input is evaluated at exactly the same local time for both calls.
    No candidate current, stage, voltage reset or equilibrium gate is inserted.
    """

    def __init__(self, equation, bundle_input):
        self.equation = equation
        self.bundle_input = bundle_input

    def forcing(self, local_ms):
        return {'bundleDisplacementNanometers': self.bundle_input.at_ms(local_ms)}

    def rates(self, local_ms, state):
        return self.equation.rates(state, self.forcing(local_ms))

    def jacobian(self, local_ms, state):
        return self.equation.jacobian(state, self.forcing(local_ms))

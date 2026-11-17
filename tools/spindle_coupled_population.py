# SPDX-License-Identifier: AGPL-3.0-only
"""Isolated retained-interval intrafusal chemistry; never selected by live Avatar.

Rate/overlap/force conventions: spindleModelTutorial commit
721ba03cb775261548766ed1349c4bf2e0e35385 (upstream license retained in packages).
Reaction rates are piecewise constant on a declared strain grid. Material
intervals are split at rate boundaries, never averaged back onto that grid.
No integration occurs at import. Execute only in the operator's visible window.
"""
import math
import numpy as np
from scipy.integrate import solve_ivp

VERSION = 'spindle-retained-chemistry-v1'
LIMITS = dict(conservationAbsolute=1e-10, domainAbsolute=1e-8,
              analyticStateAbsolute=1e-9, analyticForceAbsolutePa=1e-7,
              fixedStateAbsolute=1e-5, fixedForceAbsolutePa=1.,
              maximumIntervals=100000)


def protocol():
    return dict(version=VERSION, criteria=LIMITS, analyticControls=12,
        fixedCases=18, fixedReferenceComparisons=54, movingCases=36,
        movingTemporalContrasts=24, movingRangeContrasts=18,
        fibers=['bag', 'chain'], stepsUs=[1000, 500, 250],
        halfRangesNm=[20.25, 40.25], rateWidthNm=.5,
        movingProtocols=['stretch', 'stretch-return', 'shorten-return'],
        movingDurationSeconds=.04, movingLengthExcursionNm=4.,
        reaction=dict(method='DOP853', rtol=1e-12, atol=1e-14),
        splitting='reaction half / exact prescribed transport / reaction half',
        boundary='absorbing; numerical outflow transfers to detached myosin',
        representation='retained uniform intervals split at piecewise rate faces; no merging',
        movingIndependentAccuracyQualified=False, slackTrajectoryAssessed=False,
        sensoryFiringAssessed=False, physiologyQualified=False, avatarRecoveryAssessed=False)


def overlap(length):
    if not math.isfinite(length):
        raise ValueError('Length must be finite.')
    value = min(1., max(0., (1120. - (length - 815.)) / 735.))
    protrusion = 1120. - (length + 80.)
    if protrusion > 0:
        value = (735. - protrusion) / 735.
    if not 0 < value <= 1:
        raise ValueError('Pinned chemistry does not specify zero/invalid overlap.')
    return value


class CoupledPopulation:
    def __init__(self, fiber, half_range=20.25, width=.5, maximum_intervals=100000):
        if fiber not in ('bag', 'chain'):
            raise ValueError('Unknown intrafusal fiber.')
        if not math.isfinite(half_range) or not math.isfinite(width) or half_range <= 0 or width <= 0:
            raise ValueError('Require a finite positive rate domain and width.')
        count = round(2 * half_range / width)
        if count < 1 or count * width != 2 * half_range or maximum_intervals < count:
            raise ValueError('Rate cells must tile the domain within the interval capacity.')
        self.edges = np.linspace(-half_range, half_range, count + 1)
        self.centres = (self.edges[:-1] + self.edges[1:]) * .5
        f, g, self.slack, self.passive, self.off = ((600., 40., 1050., 90., 20.)
            if fiber == 'bag' else (400., 300., 1200., 250., 200.))
        coefficient = np.full(count, .2) if fiber == 'bag' else np.where(self.centres < -5, .2, .4)
        self.attach = np.minimum(f * np.exp(-.001 * 10 * self.centres**2 / (1e18 * 1.381e-23 * 288)), 5000.)
        self.detach = np.minimum(g + coefficient * abs(self.centres + 5)**3 + (.5 if fiber == 'bag' else 10.), 5000.)
        self.on = 80000000.
        self.fiber = fiber; self.maximum_intervals = maximum_intervals
        self.origin = self.edges.copy(); self.density = np.zeros(count)
        self.detached = 1.; self.active = 0.; self.inactive = 1.
        self.length = 1250.; self.position = 0.
        self.left_outflow = 0.; self.right_outflow = 0.
        self.last_reaction = None; self.reactions = 0; self.evaluations = 0
        self.peak_intervals = count

    def _capacity(self, faces):
        if len(faces) - 1 > self.maximum_intervals:
            raise RuntimeError('Retained interval capacity exceeded; no merging or projection permitted.')
        if not np.isfinite(faces).all() or not (np.diff(faces) > 0).all():
            raise ValueError('Material faces must be finite and distinguishable.')

    def _density_on(self, faces):
        # Every old interior discontinuity is included in faces. Selecting its
        # left endpoint avoids a rounded midpoint in an extremely thin interval.
        indices = np.searchsorted(self.origin, faces[:-1], side='right') - 1
        valid = (indices >= 0) & (indices < len(self.density))
        result = np.zeros(len(faces) - 1)
        result[valid] = self.density[indices[valid]]
        return result

    def move_to_length(self, length):
        overlap(length)  # Validate before touching any state.
        position = .5 * (length - 1250.)  # Pinned filament compliance.
        low, high = self.edges[[0, -1]] - position
        a, b = self.origin[:-1], self.origin[1:]
        left = math.fsum(self.density * np.maximum(0., np.minimum(b, low) - a))
        right = math.fsum(self.density * np.maximum(0., b - np.maximum(a, high)))
        faces = np.unique(np.concatenate(([low, high], self.origin[(self.origin > low) & (self.origin < high)])))
        self._capacity(faces)
        density = self._density_on(faces)
        self.origin, self.density = faces, density
        self.detached += left + right
        self.left_outflow += left; self.right_outflow += right
        self.length = float(length); self.position = float(position)
        self.peak_intervals = max(self.peak_intervals, len(density))

    def observe(self, faces=None):
        faces = self.edges if faces is None else np.asarray(faces, dtype=float)
        if faces.ndim != 1 or len(faces) < 2 or not np.isfinite(faces).all() or not (np.diff(faces) > 0).all():
            raise ValueError('Observation faces must increase and remain finite.')
        mass = self.density * np.diff(self.origin)
        prefix = np.concatenate(([0.], np.cumsum(mass)))
        x = faces - self.position
        index = np.searchsorted(self.origin[1:], x, side='right')
        integral = prefix[index].copy(); inside = index < len(mass)
        j = index[inside]
        integral[inside] += self.density[j] * np.maximum(0., np.minimum(x[inside], self.origin[j+1]) - self.origin[j])
        return np.diff(integral)

    def physical_state(self):
        return np.concatenate(([self.detached], self.observe(), [self.inactive, self.active]))

    def react(self, seconds, pca):
        if not math.isfinite(seconds) or seconds < 0 or not math.isfinite(pca):
            raise ValueError('Reaction duration and calcium must be finite; duration cannot be negative.')
        try:
            calcium_on = self.on * 10.**(-pca)
        except OverflowError as error:
            raise ValueError('Calcium drive exceeds the numerical domain.') from error
        if not math.isfinite(calcium_on):
            raise ValueError('Calcium drive exceeds the numerical domain.')
        if seconds == 0:
            return
        rate_faces = self.edges - self.position
        faces = np.union1d(self.origin, rate_faces)
        self._capacity(faces)
        density = self._density_on(faces); widths = np.diff(faces)
        bin_index = np.searchsorted(rate_faces, faces[:-1], side='right') - 1
        assert (bin_index >= 0).all() and (bin_index < len(self.attach)).all()
        old_mass = np.bincount(bin_index, weights=density * widths, minlength=len(self.attach))
        rate_widths = np.diff(self.edges); fraction = overlap(self.length)
        initial = np.concatenate(([self.detached], np.zeros(len(self.attach)), [self.inactive, self.active]))

        def rhs(t, y):
            # Integrate births from zero, instead of subtracting two nearly
            # equal total populations to recover a small attachment contribution.
            surviving = old_mass * np.exp(-self.detach * t)
            bound = surviving + y[1:-2]; total = np.sum(bound)
            formation = self.attach * rate_widths * y[0] * (y[-1] - total)
            birth_flux = formation - self.detach * y[1:-2]
            on = calcium_on * (fraction - y[-1]) * (1 + y[-1] / fraction)
            off = self.off * (y[-1] - total) * (2 - y[-1] / fraction)
            return np.concatenate(([-np.sum(formation - self.detach * bound)], birth_flux, [off-on, on-off]))

        result = solve_ivp(rhs, (0., seconds), initial, method='DOP853', rtol=1e-12, atol=1e-14, max_step=seconds)
        physical = result.y.T.copy()
        physical[:, 1:-2] += np.exp(-result.t[:, None] * self.detach) * old_mass
        self.last_reaction = dict(times=result.t.copy(), birthStates=result.y.T.copy(),
            physicalStates=physical, oldBinMasses=old_mass.copy(), lengthNm=self.length,
            pCa=pca, durationSeconds=seconds, success=bool(result.success), message=result.message)
        if not result.success or result.t[-1] != seconds or not np.isfinite(physical).all():
            raise RuntimeError('Reaction solve failed; last accepted prefix retained without repair.')
        totals = physical[:, 1:-2].sum(axis=1)
        violation = max(0., -float(physical.min()), float(np.max(totals - physical[:, -1])),
                        float(np.max(physical[:, -1] - fraction)))
        ledger = max(float(np.max(abs(physical[:, 0] + totals - 1))),
                     float(np.max(abs(physical[:, -2] + physical[:, -1] - 1))))
        if violation > LIMITS['domainAbsolute'] or ledger > LIMITS['conservationAbsolute']:
            raise RuntimeError('Reaction domain/accounting failed; no clipping or renormalization.')
        final = result.y[:, -1]
        density = density * np.exp(-self.detach[bin_index] * seconds) + final[1:-2][bin_index] / rate_widths[bin_index]
        self.origin, self.density = faces, density
        self.detached, self.inactive, self.active = map(float, final[[0, -2, -1]])
        self.reactions += 1; self.evaluations += result.nfev
        self.peak_intervals = max(self.peak_intervals, len(density))

    def step(self, seconds, next_length, pca):
        # Caller must segment at calcium edges and motion reversals. This is a
        # prescribed-length mechanics candidate, not an equilibrium/root solver.
        if not math.isfinite(seconds) or seconds <= 0:
            raise ValueError('Completed physical interval must be positive.')
        overlap(next_length)
        self.react(seconds / 2, pca)
        self.move_to_length(next_length)
        self.react(seconds / 2, pca)
        return self.summary()

    def summary(self):
        a = self.origin[:-1] + self.position; b = self.origin[1:] + self.position
        mass = self.density * np.diff(self.origin)
        total = math.fsum(mass); first = math.fsum(mass * (a+b) / 2)
        second = math.fsum(mass * (a*a+a*b+b*b) / 3)
        cross = 69000. * (first + 2.5 * total)
        return dict(lengthNm=self.length, displacementNm=self.position, bound=total,
            detached=self.detached, active=self.active, inactive=self.inactive, overlap=overlap(self.length),
            firstMomentNm=first, secondMomentNm2=second, crossBridgeStressPa=cross,
            stressPa=cross + self.passive * (self.length-self.slack),
            leftNumericalOutflow=self.left_outflow, rightNumericalOutflow=self.right_outflow,
            myosinResidual=self.detached+total-1, actinResidual=self.active+self.inactive-1,
            minimumDensity=float(self.density.min()), intervals=len(mass),
            peakIntervals=self.peak_intervals, reactions=self.reactions, rhsEvaluations=self.evaluations)

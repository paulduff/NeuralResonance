# SPDX-License-Identifier: AGPL-3.0-only
"""Retained material intervals for isolated intrafusal population transport.

The population and force conventions originate in spindleModelTutorial revision
721ba03cb775261548766ed1349c4bf2e0e35385. This candidate deliberately changes its
finite-domain transport representation. Numerical outflow is not calibrated
physiological detachment. No cycling kinetics, actin state or roots are advanced.
"""
import math
import numpy as np

VERSION = 'spindle-retained-characteristics-v1'
CRITERIA = dict(populationAbsolute=1e-10, conservationAbsolute=1e-10,
    forceAbsolutePa=1e-7, domainAbsolute=1e-8, secondMomentAbsoluteNm2=1e-8)


def protocol():
    return dict(version=VERSION, criteria=CRITERIA, analyticControls=12,
        savedStateQueries=25308, historyCases=144, cadenceComparisons=96,
        widthsNm=[.5, .25, .125, .0625], halfRangesNm=[20.25, 40.25], steps=[16, 32, 64],
        boundaryConvention='absorbing cell faces with explicit per-cohort numerical outflow',
        motionConvention='absolute displacement; monotone segment between consecutive observations',
        readoutConvention='exact interval moments; grid projection is observational only',
        forceConstant=69000., powerStrokeNm=2.5, originalBoundsChanged=False,
        kineticsExecuted=False, rootSelected=False, slackTrajectoryAssessed=False,
        sensoryFiringAssessed=False, physiologyQualified=False, avatarRecoveryAssessed=False)


class RetainedPopulation:
    """Each original cell remains a distinct material interval, even after loss.

    Coordinates are retained in the original material frame; absolute movement
    avoids summing many rounded displacement increments. Observation on a grid
    never becomes the next state. An excursion outside the declared domain is
    absorbing: a later return cannot recreate lost population.
    """
    def __init__(self, mass, lower=-20.25, width=.5):
        source = np.asarray(mass, dtype=float)
        if source.ndim != 1 or not len(source) or not np.isfinite(source).all():
            raise ValueError('Require a finite one-dimensional population.')
        if not math.isfinite(lower) or not math.isfinite(width) or width <= 0:
            raise ValueError('Require a finite domain with positive cell width.')
        edges = lower + width * np.arange(len(source) + 1)
        if not np.isfinite(edges).all() or not (np.diff(edges) > 0).all():
            raise ValueError('Cell faces must be finite and distinguishable.')
        self._edges = edges
        self._left = edges[:-1].copy(); self._right = edges[1:].copy()
        self._density = source.copy() / np.diff(edges)
        if not np.isfinite(self._density).all():
            raise ValueError('Cell densities must remain finite.')
        self._left_loss = np.zeros(len(source)); self._right_loss = np.zeros(len(source))
        self._initial = source.copy(); self._position = 0.

    def move_to(self, displacement):
        if not math.isfinite(displacement):
            raise ValueError('Displacement must be finite.')
        low = self._edges[0] - displacement; high = self._edges[-1] - displacement
        if not math.isfinite(low) or not math.isfinite(high):
            raise ValueError('Displaced boundaries must remain finite.')
        # These are physical intersections with the declared absorbing domain,
        # not positivity clipping, renormalization or a force-balance choice.
        removed_left = np.maximum(0., np.minimum(self._right, low) - self._left)
        removed_right = np.maximum(0., self._right - np.maximum(self._left, high))
        self._left_loss += self._density * removed_left
        self._right_loss += self._density * removed_right
        self._left = np.minimum(self._right, np.maximum(self._left, low))
        self._right = np.maximum(self._left, np.minimum(self._right, high))
        self._position = float(displacement)
        return self.summary()

    def observe(self, edges=None):
        edges = self._edges if edges is None else np.asarray(edges, dtype=float)
        if edges.ndim != 1 or len(edges) < 2 or not np.isfinite(edges).all() or not (np.diff(edges) > 0).all():
            raise ValueError('Observation faces must be finite and increasing.')
        # Cumulative integrals through ordered, disjoint material intervals.
        # Empty cohorts retain their original order and identity.
        mass = self._density * (self._right - self._left)
        prefix = np.concatenate(([0.], np.cumsum(mass)))
        x = edges - self._position
        index = np.searchsorted(self._right, x, side='right')
        integral = prefix[index].copy(); inside = index < len(mass)
        j = index[inside]
        integral[inside] += self._density[j] * np.maximum(0., np.minimum(x[inside], self._right[j]) - self._left[j])
        return np.diff(integral)

    def summary(self):
        a = self._left + self._position; b = self._right + self._position
        mass = self._density * (self._right - self._left)
        retained = math.fsum(mass); first = math.fsum(mass * (a + b) * .5)
        second = math.fsum(mass * (a * a + a * b + b * b) / 3)
        left = math.fsum(self._left_loss); right = math.fsum(self._right_loss)
        return dict(displacementNm=self._position, retained=retained, firstMomentNm=first,
            secondMomentNm2=second, forcePa=69000. * (first + 2.5 * retained),
            leftOutflow=left, rightOutflow=right,
            conservationResidual=math.fsum([retained, left, right, -math.fsum(self._initial)]))

    def snapshot(self):
        return dict(**self.summary(), lowerNm=float(self._edges[0]), upperNm=float(self._edges[-1]),
            initialMasses=self._initial.copy(), leftOriginNm=self._left.copy(), rightOriginNm=self._right.copy(),
            densityPerNm=self._density.copy(), leftOutflowByCohort=self._left_loss.copy(),
            rightOutflowByCohort=self._right_loss.copy())

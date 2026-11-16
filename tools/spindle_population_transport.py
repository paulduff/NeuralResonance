# SPDX-License-Identifier: AGPL-3.0-only
"""Isolated conservative transport candidate; never selected by live Avatar.

Source population/force convention: Neuromechanics-Lab/spindleModelTutorial,
721ba03cb775261548766ed1349c4bf2e0e35385; spindle-reference-LICENSE.txt.
Each stored population is interpreted as uniform mass over a cell, not a
pointwise density sample. Cell faces extend half a bin beyond the source's
centres. This is a changed numerical boundary convention, not calibrated
detachment physiology. Kinetics and force-balance root selection are absent.
"""
import math
import numpy as np

VERSION = 'spindle-population-transport-v1'
SHIFTS_NM = (-40.5, -20.25, -.5, -.125, 0., .125, .5, 20.25, 40.5)
CRITERIA = dict(populationAbsolute=1e-10, conservationAbsolute=1e-10,
                forceAbsolutePa=1e-7, domainAbsolute=1e-8)


def protocol():
    return dict(version=VERSION, criteria=CRITERIA, savedStates=2812,
        shiftsNm=list(SHIFTS_NM), savedStateQueries=25308, analyticControls=10,
        refinementWidthsNm=[.5, .25, .125, .0625], refinementHalfRangesNm=[20.25, 40.25],
        historySteps=[16, 32, 64], historyCases=144, spatialRefinementComparisons=36,
        spatialRefinementRatio=.8, spatialRefinementFloor=1e-10, continuityEpsilonNm=1e-8,
        forceConstant=69000., powerStrokeNm=2.5,
        boundaryConvention='uniform cell mass; absorbing cell faces; explicit numerical outflow',
        kineticsExecuted=False, nativeRerun=False, rootSelected=False,
        slackTrajectoryAssessed=False, sensoryFiringAssessed=False, physiologyQualified=False,
        avatarRecoveryAssessed=False)


def grid(lower, width, count):
    assert math.isfinite(lower) and math.isfinite(width) and width > 0
    assert isinstance(count, int) and count > 0
    return lower + width * (np.arange(count, dtype=float) + .5)


def transport(mass, displacement, lower=-20.25, width=.5):
    """Push source mass to its two overlapping destination cells, without clipping.

    Unrepresented outflow is returned by side, never silently erased or called
    physiological detachment. Negative roundoff in saved states is preserved.
    The caller owns any detached-state update and must retain its ledger.
    """
    source = np.asarray(mass, dtype=float)
    assert source.ndim == 1 and len(source) and np.isfinite(source).all()
    assert math.isfinite(displacement)
    centres = grid(lower, width, len(source))
    offset = displacement / width; whole = math.floor(offset); fraction = offset - whole
    shifted = np.zeros_like(source); left = 0.; right = 0.
    for jump, weight in ((whole, 1. - fraction), (whole + 1, fraction)):
        destinations = np.arange(len(source)) + jump
        amount = source * weight
        inside = (destinations >= 0) & (destinations < len(source))
        shifted[destinations[inside]] += amount[inside]
        left += float(np.sum(amount[destinations < 0]))
        right += float(np.sum(amount[destinations >= len(source)]))
    retained = float(np.sum(shifted))
    moment = float(centres @ shifted)
    return dict(masses=shifted, leftOutflow=left, rightOutflow=right,
        retained=retained, firstMomentNm=moment,
        forcePa=69000. * (moment + 2.5 * retained),
        conservationResidual=float(retained + left + right - np.sum(source)))


def overlap_reference(mass, displacement, lower=-20.25, width=.5):
    """Separate cell-intersection construction, plus exact retained first moment.

    Source intervals are translated then intersected with every destination
    interval. No integer shift or deposition weights from the candidate are used.
    The continuum moment is distinct from the rebinned cell-centre moment.
    """
    source = np.asarray(mass, dtype=float)
    edges = lower + width * np.arange(len(source) + 1)
    starts = edges[:-1] + displacement; ends = edges[1:] + displacement
    lo = np.maximum(edges[:-1, None], starts[None, :])
    hi = np.minimum(edges[1:, None], ends[None, :])
    lengths = np.maximum(0., hi - lo)
    shifted = lengths @ (source / width)
    left_lengths = np.maximum(0., np.minimum(ends, edges[0]) - starts)
    right_lengths = np.maximum(0., ends - np.maximum(starts, edges[-1]))
    kept_lo = np.maximum(starts, edges[0]); kept_hi = np.minimum(ends, edges[-1])
    kept = np.maximum(0., kept_hi - kept_lo)
    first = float(np.sum((source / width) * kept * (kept_lo + kept_hi) * .5))
    centres = (edges[:-1] + edges[1:]) * .5
    moment = float(centres @ shifted); retained = float(np.sum(shifted))
    return dict(masses=shifted, leftOutflow=float(source @ (left_lengths / width)),
        rightOutflow=float(source @ (right_lengths / width)), retained=retained,
        firstMomentNm=moment, forcePa=69000. * (moment + 2.5 * retained),
        continuumFirstMomentNm=first, continuumForcePa=69000. * (first + 2.5 * retained))


def compare(mass, displacement, lower=-20.25, width=.5):
    before = np.asarray(mass, dtype=float).copy()
    candidate = transport(mass, displacement, lower, width)
    reference = overlap_reference(mass, displacement, lower, width)
    difference = float(np.max(np.abs(candidate['masses'] - reference['masses'])))
    flux_error = max(abs(candidate[k] - reference[k]) for k in ('leftOutflow', 'rightOutflow'))
    force_error = abs(candidate['forcePa'] - reference['forcePa'])
    conserved = abs(candidate['conservationResidual'])
    violation = max(0., -float(np.min(candidate['masses'])), -candidate['leftOutflow'], -candidate['rightOutflow'])
    passed = bool(difference <= CRITERIA['populationAbsolute'] and flux_error <= CRITERIA['populationAbsolute']
        and force_error <= CRITERIA['forceAbsolutePa'] and conserved <= CRITERIA['conservationAbsolute']
        and violation <= CRITERIA['domainAbsolute'] and np.array_equal(before, mass))
    return dict(candidate=candidate, reference=reference, maximumPopulationDifference=difference,
        maximumOutflowDifference=flux_error, forceDifferencePa=force_error,
        conservationResidual=conserved, domainViolation=violation,
        gridVsContinuumForcePa=candidate['forcePa'] - reference['continuumForcePa'],
        statePreserved=bool(np.array_equal(before, mass)), passed=passed)

# SPDX-License-Identifier: AGPL-3.0-only
"""All zero-load candidates of the pinned spindle's pure trial-force query.

Derived equations: Neuromechanics-Lab/spindleModelTutorial,
721ba03cb775261548766ed1349c4bf2e0e35385. See spindle-reference-LICENSE.txt.
No kinetics, state mutation, slack trajectory or physiological root selection.
Execution/controls belong to an independent visible PowerShell window.
"""
import math
from fractions import Fraction
import numpy as np
from scipy.optimize import brentq

VERSION = 'spindle-active-force-landscape-v2'
CRITERIA = dict(forceAbsolutePa=1e-7, rootAbsoluteNm=1e-9,
                conservationAbsolute=1e-10, domainAbsolute=1e-8)
FIBERS = ('bag', 'chain')
PROTOCOLS = ('stretch-shorten', 'imposed-below-slack')
STEPS_US = (1000, 500, 250)


def protocol():
    return dict(version=VERSION, fibers=list(FIBERS), protocols=list(PROTOCOLS),
        stepsUs=list(STEPS_US), criteria=CRITERIA, nativeTrajectories=12,
        savedStates=2812, analyticControls=8, targetStressPa=0.,
        nativeRerun=False, kineticsExecuted=False, slackTrajectoryAssessed=False,
        rootSelected=False, afferentFiringAssessed=False, physiologyQualified=False,
        avatarRecoveryAssessed=False)


def overlap(p, length):
    extent = p['thick_filament_length'] - p['bare_zone_length']
    value = min(1., max(0., (p['thin_filament_length'] + p['thick_filament_length'] - length) / extent))
    protrusion = p['thin_filament_length'] - length - p['bare_zone_length']
    if protrusion > 0: value = (extent - protrusion) / extent
    return value


def domain(p):
    # Exact open positive-overlap domain of the source geometry, not a clamp.
    return p['thin_filament_length'] - p['thick_filament_length'], p['thin_filament_length'] + p['thick_filament_length']


def validate(p, state, length):
    x = np.asarray(p['x_bins'], dtype=float)
    y = np.asarray(state, dtype=float)
    assert np.array_equal(x, np.arange(-20., 20.5, .5))
    assert y.shape == (84,) and np.isfinite(y).all() and math.isfinite(length)
    assert p['compliance_factor'] == .5 and p['bin_width'] == .5
    assert p['cb_force_constant'] == 69000. and p['power_stroke'] == 2.5
    assert p['k_passive'] > 0 and 0 < overlap(p, length) <= 1
    return x, y


def shifted_manual(p, y, old_length, trial_length):
    """Direct two-bin weights, with exact support endpoints kept separately."""
    x = np.asarray(p['x_bins']); q = x - (trial_length - old_length) * p['compliance_factor']
    result = np.zeros(len(x))
    inside = (q >= x[0]) & (q <= x[-1])
    positions = (q[inside] - x[0]) / p['bin_width']
    index = np.minimum(np.floor(positions).astype(int), len(x) - 2)
    fraction = positions - index
    bins = y[1:-2]
    result[inside] = bins[index] * (1 - fraction) + bins[index + 1] * fraction
    return result


def manual_force(p, y, old_length, trial_length):
    bins = shifted_manual(p, y, old_length, trial_length)
    return float(p['cb_force_constant'] * np.sum((np.asarray(p['x_bins']) + p['power_stroke']) * bins)
                 + p['k_passive'] * (trial_length - p['hsl_slack']))


def reference_force(p, y, old_length, trial_length):
    # Independent interpolation implementation for residual and Brent checks.
    x = np.asarray(p['x_bins'])
    bins = np.interp(x - (trial_length - old_length) * p['compliance_factor'], x, y[1:-2], left=0., right=0.)
    return float(p['cb_force_constant'] * np.dot(x + p['power_stroke'], bins)
                 + p['k_passive'] * (trial_length - p['hsl_slack']))


def knots(p, old_length):
    lower, upper = domain(p)
    # All x_i-x_j on the uniform grid. Out-of-support tails are affine too.
    offsets = np.arange(-80., 80.5, 1.)
    return sorted({lower, upper, *(old_length + float(dx) for dx in offsets if lower < old_length + dx < upper)})


def affine(p, y, old_length, a, b):
    """Exact affine expression on an OPEN interval between interpolation knots."""
    middle = a + (b - a) * .5
    x = np.asarray(p['x_bins']); q = x - (middle - old_length) * p['compliance_factor']
    inside = (q > x[0]) & (q < x[-1])
    index = np.floor((q[inside] - x[0]) / p['bin_width']).astype(int)
    bins = y[1:-2]; slopes = np.zeros(len(x)); values = np.zeros(len(x))
    delta = bins[index + 1] - bins[index]
    values[inside] = bins[index] + delta * (q[inside] - x[index]) / p['bin_width']
    slopes[inside] = -p['compliance_factor'] * delta / p['bin_width']
    weights = x + p['power_stroke']
    value = float(p['cb_force_constant'] * np.dot(weights, values) + p['k_passive'] * (middle - p['hsl_slack']))
    slope = float(p['cb_force_constant'] * np.dot(weights, slopes) + p['k_passive'])
    return middle, value, slope


def exact_affine(p, y, old_length, a, b):
    """Certify near-knot topology from exact rational stored binary inputs.

    A small residual is an accuracy check, never proof of a distinct root.
    This path also retains an interval root that rounds onto its boundary.
    """
    F=Fraction; middle=(F(a)+F(b))/2; c=F(p['compliance_factor']); h=F(p['bin_width'])
    x=[F(float(v)) for v in p['x_bins']]; bins=[F(float(v)) for v in y[1:-2]]
    value=F(p['k_passive'])*(middle-F(p['hsl_slack'])); slope=F(p['k_passive'])
    for output in x:
        q=output-(middle-F(old_length))*c
        if x[0] < q < x[-1]:
            j=int((q-x[0])//h); delta=bins[j+1]-bins[j]
            weight=F(p['cb_force_constant'])*(output+F(p['power_stroke']))
            value+=weight*(bins[j]+delta*(q-x[j])/h)
            slope-=weight*c*delta/h
    return middle,value,slope


def exact_query(p, y, old_length, trial_length):
    """Separate rational interpolation query for knot and reference checks."""
    F=Fraction; length=F(trial_length); h=F(p['bin_width']); xmin=F(float(p['x_bins'][0])); xmax=F(float(p['x_bins'][-1]))
    result=F(p['k_passive'])*(length-F(p['hsl_slack']))
    for i, output_value in enumerate(p['x_bins']):
        output=F(float(output_value)); q=output-(length-F(old_length))*F(p['compliance_factor'])
        if xmin <= q <= xmax:
            position=(q-xmin)/h; j=min(int(position//1),79); f=position-j
            mass=F(float(y[1+j]))*(1-f)+F(float(y[2+j]))*f
            result+=F(p['cb_force_constant'])*(output+F(p['power_stroke']))*mass
    return result


def candidate(p, state, old_length, target=0.):
    x, y = validate(p, state, old_length)
    assert math.isfinite(target)
    points = knots(p, old_length); roots = []; segments = []; flats = []; nonzero_knots = []
    for i, (a, b) in enumerate(zip(points, points[1:])):
        middle, value, slope = affine(p, y, old_length, a, b)
        row = dict(index=i, lowerNm=a, upperNm=b, middleNm=middle,
                   middleStressPa=value, slopePaPerNm=slope,
                   rightLimitAtLowerPa=value + slope * (a - middle),
                   leftLimitAtUpperPa=value + slope * (b - middle))
        segments.append(row)
        if min(abs(row['rightLimitAtLowerPa']-target),abs(row['leftLimitAtUpperPa']-target)) <= CRITERIA['forceAbsolutePa']:
            m,v,s=exact_affine(p,y,old_length,a,b)
            if not s:
                if v==Fraction(target): flats.append(row)
                continue
            exact_root=m+(Fraction(target)-v)/s
            if Fraction(a) < exact_root < Fraction(b):
                roots.append(dict(lengthNm=float(exact_root),kind='open-interval',segment=i,
                    slopePaPerNm=float(s),exactLengthRatio=str(exact_root),nearKnotCertified=True))
            continue
        if slope == 0.:
            if abs(value - target) <= CRITERIA['forceAbsolutePa']: flats.append(row)
            continue
        root = middle + (target - value) / slope
        if a < root < b:
            roots.append(dict(lengthNm=root, kind='open-interval', segment=i, slopePaPerNm=slope))
    boundaries = []
    for i, point in enumerate(points[1:-1], start=1):
        actual = manual_force(p, y, old_length, point)
        left = segments[i-1]['leftLimitAtUpperPa']; right = segments[i]['rightLimitAtLowerPa']
        boundaries.append(dict(lengthNm=point, actualStressPa=actual, leftLimitPa=left, rightLimitPa=right,
            jumpPa=max(abs(actual-left), abs(actual-right)),
            signChangeAcrossJump=bool((left-target)*(right-target) < 0 and abs(actual-target) > CRITERIA['forceAbsolutePa'])))
        if abs(actual - target) <= CRITERIA['forceAbsolutePa']:
            residual=exact_query(p,y,old_length,point)-Fraction(target)
            if residual==0:
                roots.append(dict(lengthNm=point, kind='knot', knot=i))
            else:
                nonzero_knots.append(dict(lengthNm=point,forceResidualPa=actual-target,
                    exactResidualSign=1 if residual>0 else -1))
    roots.sort(key=lambda r: r['lengthNm'])
    for root in roots:
        length = root['lengthNm']; bins = shifted_manual(p, y, old_length, length)
        detached = float(y[0] + y[1:-2].sum() - bins.sum())
        geom = overlap(p, length)
        root.update(residualPa=manual_force(p, y, old_length, length)-target,
            independentResidualPa=reference_force(p, y, old_length, length)-target,
            overlap=geom, bound=float(bins.sum()), detached=detached,
            stateDomainViolation=float(max(0., -bins.min(), -detached, -y[-1], -y[-2], bins.sum()-y[-1], y[-1]-geom)),
            massResidual=float(detached + bins.sum() - (y[0] + y[1:-2].sum())))
        root['admissibleState'] = bool(root['stateDomainViolation'] <= CRITERIA['domainAbsolute']
            and abs(root['massResidual']) <= CRITERIA['conservationAbsolute'])
    return dict(roots=roots, segments=segments, boundaries=boundaries, flatIntervals=flats,
                nonzeroNearKnots=nonzero_knots)


def independent_roots(p, y, old_length, target=0.):
    points = knots(p, old_length); roots = []
    fun = lambda length: reference_force(p, y, old_length, length) - target
    for point in points[1:-1]:
        if abs(fun(point)) <= CRITERIA['forceAbsolutePa'] and exact_query(p,y,old_length,point)==Fraction(target):
            roots.append(point)
    for a, b in zip(points, points[1:]):
        # Never bracket over a support discontinuity. Boundary roots are checked
        # separately. Preserve disagreement rather than widening tolerances.
        lo, hi = float(np.nextafter(a, b)), float(np.nextafter(b, a))
        fa, fb = fun(lo), fun(hi)
        if min(abs(fa),abs(fb)) <= CRITERIA['forceAbsolutePa']:
            # Independent affine reconstruction from TWO exact interior queries,
            # rather than the candidate's analytic coefficient construction.
            q1=(3*Fraction(a)+Fraction(b))/4; q2=(Fraction(a)+3*Fraction(b))/4
            v1=exact_query(p,y,old_length,q1)-Fraction(target)
            v2=exact_query(p,y,old_length,q2)-Fraction(target)
            if v1!=v2:
                exact_root=q1-v1*(q2-q1)/(v2-v1)
                if Fraction(a) < exact_root < Fraction(b): roots.append(float(exact_root))
            continue
        if fa * fb < 0:
            root = float(brentq(fun, lo, hi, xtol=5e-13, rtol=4*np.finfo(float).eps))
            roots.append(root)
    return sorted(roots)


def check_landscape(p, state, length, target=0.):
    before = np.asarray(state).copy()
    result = candidate(p, state, length, target)
    reference = independent_roots(p, state, length, target)
    assert np.array_equal(state, before), 'Force query changed its supplied state.'
    actual = [r['lengthNm'] for r in result['roots']]
    error = max((abs(a-b) for a,b in zip(actual, reference)), default=0.) if len(actual) == len(reference) else None
    residual = max((max(abs(r['residualPa']), abs(r['independentResidualPa'])) for r in result['roots']), default=0.)
    # Interior point checks also expose arithmetic or interval construction errors.
    sample_error = 0.
    for seg in result['segments']:
        for fraction in (.25, .5, .75):
            point = seg['lowerNm'] + fraction * (seg['upperNm'] - seg['lowerNm'])
            predicted = seg['middleStressPa'] + seg['slopePaPerNm'] * (point - seg['middleNm'])
            sample_error = max(sample_error, abs(predicted - reference_force(p, state, length, point)))
    for row in result['boundaries']:
        sample_error = max(sample_error, abs(row['actualStressPa'] - reference_force(p, state, length, row['lengthNm'])))
    result.update(independentRootsNm=reference, maximumRootDifferenceNm=error,
        maximumRootResidualPa=residual, maximumForceDifferencePa=sample_error, statePreserved=True,
        passed=bool(error is not None and error <= CRITERIA['rootAbsoluteNm'] and residual <= CRITERIA['forceAbsolutePa']
                    and sample_error <= CRITERIA['forceAbsolutePa'] and not result['flatIntervals']))
    return result

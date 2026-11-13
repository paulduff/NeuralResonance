"""Prospective coupled-muscle inputs and bounds; parameter decoding only here."""
from hashlib import sha256
import json
from pathlib import Path

VERSION = 'dynamic-muscle-comparison-v1'
MAT_HASH = '01123aec76d1acc18b306ae1bd9daabfd29fe9287b54212aef5db3f15f719ebd'
CALCIUM_NAMES = ('storeSites', 'totalCalcium', 'releaseRatePerSecond', 'pumpRatePerSecond',
    'bindingRatePerSecond', 'unbindingRatePerSecond', 'releaseRiseSeconds', 'releaseDecaySeconds',
    'hillExponent', 'halfActivationBinding', 'activationSeconds', 'cooperativity')
# Absolute errors in explicit physical units, declared before any new trajectory.
REFERENCE_BOUNDS = dict(fiberLengthMeters=1e-9, fiberVelocityMetersPerSecond=1e-7,
    yieldFraction=1e-8, calcium=1e-8, activation=1e-8, fluxIntegral=1e-7,
    forceNewtons=1e-5, accelerationMetersPerSecondSquared=1e-4, impulseNewtonSeconds=1e-8, workJoules=1e-8)
CANDIDATE_BOUNDS = dict(fiberLengthMeters=1e-7, fiberVelocityMetersPerSecond=1e-5,
    yieldFraction=1e-6, calcium=1e-6, activation=1e-6, fluxIntegral=1e-5,
    forceNewtons=1e-3, accelerationMetersPerSecondSquared=1e-2, impulseNewtonSeconds=1e-6, workJoules=1e-6)
SETTINGS = [dict(key='dop853', method='DOP853', rtol=1e-10, atol=1e-12),
    dict(key='dop853-tight', method='DOP853', rtol=1e-12, atol=1e-14),
    dict(key='radau', method='Radau', rtol=1e-11, atol=1e-13)]


def make_protocol(mat_path, arrival_path):
    import numpy as np
    from scipy.io import loadmat
    data_bytes = Path(mat_path).read_bytes()
    assert sha256(data_bytes).hexdigest() == MAT_HASH
    p = loadmat(mat_path, simplify_cells=True)['modelParameter']
    g = dict(optimalFiberLengthMeters=float(p['optimalLength']) / 100,
        referenceTendonLengthMeters=float(p['L0T']) / 100, fiberMassKg=float(p['mass']),
        pennationRadians=float(p['pennationAngle']), referenceForceNewtons=float(p['F0']),
        passiveLengthScale=float(p['Lmax']))
    rows = []
    for index in (0, 1):
        row = np.asarray(p['parameterMatrix'])[index]
        calcium = dict(zip(CALCIUM_NAMES, map(float, row[:12])))
        calcium['peakTetanicForceNewtons'] = float(np.asarray(p['PTi']).reshape(-1)[index])
        rows.append(dict(row=index, calcium=calcium, lengthGainAboveOptimal=float(row[12]),
                         lengthGainBelowOptimal=float(row[13])))
    recorded = [json.loads(line) for line in Path(arrival_path).read_text().splitlines()]
    assert len(recorded) == 28 and all(a['ordinal'] == i + 1 for i, a in enumerate(recorded))
    assert all(a['firedAtNeuralMs'] + 2.0625 == a['arrivalAtBodyMs'] for a in recorded)
    assert len({a['sessionId'] for a in recorded}) == len({a['motorUnitId'] for a in recorded}) == 1
    train = [dict(unitIndex=0, arrivalAtMs=100.03125 + 50 * i) for i in range(27)]
    cases = []

    def case(key, boundary='Free', initial=1., events=None, stretch=False, two=False, replay=False):
        duration = 6000. if replay else 2000.
        passive = 23 * .046 * np.logaddexp(0, (initial / g['passiveLengthScale'] - 1.17) / .046)
        tendon = .964 + .0047 * np.log(np.expm1(passive * np.cos(g['pennationRadians']) / (27.8 * .0047)))
        length = g['optimalFiberLengthMeters'] * initial * np.cos(g['pennationRadians']) + g['referenceTendonLengthMeters'] * tendon
        knots = [(0., 0.), (duration, 0.)]
        if stretch:
            knots = [(0., 0.), (500.015625, 0.), (1000.015625, .002), (1500.015625, 0.), (duration, 0.)]
        events = list(events or [])
        if replay:
            events = [dict(unitIndex=0, arrivalAtMs=a['arrivalAtBodyMs']) for a in recorded]
        cases.append(dict(key=key, boundary=boundary, durationMs=duration, unitRows=[0, 1] if two else [0],
            initialFiberLengthMeters=float(g['optimalFiberLengthMeters'] * initial),
            initialMusculotendonLengthMeters=float(length),
            lengthKnots=[dict(timeMs=t, offsetMeters=x) for t, x in knots],
            events=sorted(events, key=lambda e: (e['arrivalAtMs'], e['unitIndex'])), replayOriginalArrivals=replay))

    case('quiet-free')
    case('single-free', events=[dict(unitIndex=0, arrivalAtMs=10.03125)])
    case('train-free', events=train)
    case('fixed-optimal', boundary='FixedLengthDiagnostic', events=train)
    case('fixed-short', boundary='FixedLengthDiagnostic', initial=.9, events=train)
    case('fixed-long', boundary='FixedLengthDiagnostic', initial=1.1, events=train)
    case('passive-stretch', stretch=True)
    case('active-stretch', stretch=True, events=train)
    case('two-units', two=True, events=train + [dict(unitIndex=1, arrivalAtMs=125.046875 + 50 * i) for i in range(26)])
    case('recorded-free', replay=True)
    case('recorded-fixed', boundary='FixedLengthDiagnostic', replay=True)
    return dict(version=VERSION, candidateVersion='isolated-coupled-slow-muscle-rk4-v1', geometry=g, parameterRows=rows,
        stepsMs=[.125, .0625, .03125], sampleMs=.25, cases=cases,
        recordedArrivalsSha256=sha256(Path(arrival_path).read_bytes()).hexdigest(),
        referenceSettings=SETTINGS, maximumReferenceStepSeconds=.002,
        referenceBounds=REFERENCE_BOUNDS, candidateBounds=CANDIDATE_BOUNDS,
        refinementFraction=.2, refinementFloorFraction=.02,
        releaseWaveformAbsolute=1e-9, calciumLedgerAbsolute=1e-9,
        momentumLedgerNewtonSeconds=1e-10, finestKineticLedgerJoules=1e-7,
        legacyCalciumAbsolute=1e-10, legacyForceAbsoluteNewtons=1e-10,
        sourceOrderingCases=['quiet-free', 'single-free', 'train-free', 'active-stretch', 'recorded-free'],
        sourceOrderingScope='Controlled first-row input and equilibrium yield; author update ordering, not native 200-unit MATLAB reproduction',
        physiologicalCalibrationComplete=False, liveBodySelected=False)

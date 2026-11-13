"""Prospective causal motor/body comparison; preparation performs no simulation."""
from dynamic_muscle_protocol import make_protocol as muscle_protocol, SETTINGS

VERSION = 'causal-motor-comparison-v1'
REFERENCE_BOUNDS = dict(jointAngleRadians=1e-9, jointSpeedRadiansPerSecond=1e-8,
    fiberLengthMeters=1e-9, fiberVelocityMetersPerSecond=1e-7, yieldFraction=1e-8,
    calcium=1e-8, activation=1e-8, fluxIntegral=1e-7, forceNewtons=1e-5,
    accelerationMetersPerSecondSquared=1e-4, impulseNewtonSeconds=1e-8, workJoules=1e-8,
    jointImpulseNewtonMeterSeconds=1e-9, jointWorkJoules=1e-9)
CANDIDATE_BOUNDS = dict(jointAngleRadians=1e-5, jointSpeedRadiansPerSecond=1e-5,
    fiberLengthMeters=5e-7, fiberVelocityMetersPerSecond=5e-5, yieldFraction=1e-5,
    calcium=1e-5, activation=1e-5, fluxIntegral=1e-4, forceNewtons=.01,
    accelerationMetersPerSecondSquared=.1, impulseNewtonSeconds=1e-5, workJoules=1e-5,
    jointImpulseNewtonMeterSeconds=1e-5, jointWorkJoules=1e-6)


def make_protocol(mat_path, arrival_path):
    import json
    from pathlib import Path
    base = muscle_protocol(mat_path, arrival_path)
    recorded = [json.loads(line) for line in Path(arrival_path).read_text().splitlines()]
    quiet = base['cases'][0]
    train = [dict(muscleIndex=0, arrivalAtMs=100.03125 + 50 * i) for i in range(27)]
    cases = []

    def add(key, duration=1000., events=(), speed=0., replay=False):
        cases.append(dict(key=key, durationMs=duration, initialAngleRadians=0.,
            initialSpeedRadiansPerSecond=speed,
            events=sorted(list(events), key=lambda e: (e['arrivalAtMs'], e['muscleIndex'])),
            replayOriginalArrivals=replay))

    add('quiet')
    add('single', events=[dict(muscleIndex=0, arrivalAtMs=10.03125)])
    add('train', 2000., train)
    add('alternating', 1500., [dict(muscleIndex=j, arrivalAtMs=100.03125 + j * 50 + 100 * i)
                            for i in range(12) for j in range(2)])
    add('balanced', 1500., [dict(muscleIndex=j, arrivalAtMs=100.03125 + 50 * i)
                         for i in range(24) for j in range(2)])
    add('disturbed-quiet', speed=.01)
    add('disturbed-train', 1500., train, speed=.01)
    add('recorded', 6000., [dict(muscleIndex=0, arrivalAtMs=a['arrivalAtBodyMs']) for a in recorded], replay=True)
    return dict(version=VERSION, candidateVersion='isolated-start-held-motor-body-v1',
        muscleVersion=base['candidateVersion'], geometry=base['geometry'], parameterRows=base['parameterRows'],
        inertiaKgMetersSquared=1., momentArmsMeters=[.02, -.02], unitRows=[0, 0],
        initialFiberLengthMeters=quiet['initialFiberLengthMeters'],
        restPathLengthMeters=quiet['initialMusculotendonLengthMeters'],
        # Fix the muscle grid while refining the physical partition. The last
        # resolution then halves only the muscle step at the finest body step.
        resolutions=[dict(key='body-' + str(h), physicalStepMs=h, muscleStepMs=.0625)
                     for h in (1., .5, .25, .125)] +
                    [dict(key='muscle-fine', physicalStepMs=.125, muscleStepMs=.03125)],
        cases=cases, recordedArrivalsSha256=base['recordedArrivalsSha256'],
        referenceSettings=SETTINGS, referenceSampleMs=.125, maximumReferenceStepSeconds=.001,
        referenceBounds=REFERENCE_BOUNDS, candidateBounds=CANDIDATE_BOUNDS,
        refinementMaximumRatio=.8, refinementFloorFraction=.02, muscleRefinementBoundFraction=.05,
        calciumLedgerAbsolute=1e-9, releaseWaveformAbsolute=1e-9,
        fiberMomentumLedgerNewtonSeconds=1e-10, fiberKineticLedgerJoules=1e-7,
        jointMomentumLedgerNewtonMeterSeconds=1e-10, jointKineticLedgerJoules=1e-10,
        tendonQuadratureAgreementJoules=1e-12, finestCouplingDefectJoules=1e-6,
        referenceTendonEnergyLedgerJoules=1e-8,
        declaredAnatomy='Analytic constant-arm opposing pair; identical slow reference units, not calibrated human joint anatomy',
        neuronalModelExecuted=False, liveBodySelected=False, physiologicalCalibrationComplete=False,
        avatarRecoveryAssessed=False)

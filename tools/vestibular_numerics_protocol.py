"""Declared inputs and numerical bounds only; importing runs no model."""

VERSION = 'isolated-vestibular-numerics-v1'
COUNTS = dict(ClockedVestibularMechanicsTests=16, VestibularLabyrinthTransducerTests=7,
              PhysicalSampleTransportTests=22, PhysicalSampleProducerTests=16, SolutionManifestTests=1)
CHANNELS = ['leftHorizontal', 'rightHorizontal', 'leftAnterior', 'rightAnterior', 'leftPosterior', 'rightPosterior']
FIELDS = ['pitchVelocityRadiansPerSecond', 'yawVelocityRadiansPerSecond', 'rollVelocityRadiansPerSecond',
          'gravitoInertialRightMetersPerSecondSquared', 'gravitoInertialDownMetersPerSecondSquared',
          'gravitoInertialForwardMetersPerSecondSquared']
FALSE_CLAIMS = ['neuronalModelExecuted', 'nativeBodyExecuted', 'liveBrainAccessed',
                'skullTransformationQualified', 'physiologyQualified', 'avatarRecoveryAssessed']


def protocol():
    rest = [0., 0., 0., 0., 9.80665, 0.]
    turn = [.4, 1.2, -.3, 2., 8., -1.]
    reverse = [-.7, -.8, .2, -3., 7., 4.]
    histories = {
        'quiet': [rest] * 4,
        'constant-turn': [turn] * 4,
        'turn-stop': [turn, turn, rest, rest],
        'turn-reverse': [rest, turn, reverse, rest],
        'otolith-change': [rest, [0., 0., 0., 3., 6., -2.], [0., 0., 0., -4., 9., 1.], rest],
        'paired-cancellation': [[1., 0., 1., 0., 9.80665, 0.], [-1., 0., 1., 0., 9.80665, 0.],
                                [1., 0., -1., 0., 9.80665, 0.], rest],
    }
    cases = []
    for group, values in histories.items():
        for step_ms, suffix in ((.125, '125us'), (1., '1ms'), (4., '4ms')):
            n = int(64 / step_ms)
            cases.append(dict(key=group + '-' + suffix, group=group, stepMs=step_ms, intervals=n,
                initialStep=500, physicalEpochMs=500., neuralEpochMs=1000.,
                changes=[dict(endpointIndex=i * n // 4, values=v) for i, v in enumerate(values)]))
    for group, step_ms, n in (('tiny-step', .000001, 64), ('long-step', 10000., 4)):
        cases.append(dict(key=group, group=group, stepMs=step_ms, intervals=n,
            initialStep=500, physicalEpochMs=500., neuralEpochMs=1000.,
            changes=[dict(endpointIndex=i * n // 4, values=v) for i, v in enumerate((turn, rest, reverse, rest))]))
    return dict(version=VERSION, candidateVersion='isolated-clocked-vestibular-mechanics-v1',
        timeConstantsSeconds=[.045, 5., .080], deliveryGroups=[1, 17], cases=cases,
        referenceLevels=[dict(key='dop853-strict', rtol=1e-11, atol=1e-13, maxStepSeconds=.025),
                         dict(key='dop853-tighter', rtol=1e-12, atol=1e-14, maxStepSeconds=.0125)],
        bounds=dict(referenceAbsolute=1e-10, candidateAbsolute=1e-9, subdivisionAbsolute=1e-11,
                    controlAbsolute=1e-11),
        boundsMeaning='absolute numerical coordinates in rad/s and m/s^2; not physiological acceptance',
        movingHeadAccuracyQualified=False, liveSelectionChanged=False)

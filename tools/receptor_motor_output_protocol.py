"""Prospective active-cell output/Avatar receiver comparison; no model execution."""


def protocol():
    return dict(version='receptor-motor-output-comparison-v1', sourceVersion='isolated-receptor-motor-source-v1',
        baselineConditions=18, sourceConditions=36, sampleMs=.1, durationMs=1500,
        publicationEverySamples=[1, 17], retryEveryBatches=37, backpressureEveryBatches=113,
        motorUnit='fixture-motor-unit', conductionDelayMs=2.0625,
        requireBitwiseObservationStates=True, requireExactCrossings=True, requireExactDelivery=True,
        requireNontrivialExcitedCrossings=True, requireQuietNoCrossings=True,
        presynapticEventsAreFixtures=True, presynapticNeuronsExecuted=False,
        nativePhysicsExecuted=False, muscleDynamicsExecuted=False, physiologicalCalibrationComplete=False,
        avatarRecoveryAssessed=False)

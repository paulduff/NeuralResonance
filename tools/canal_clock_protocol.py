"""Prospective clock replay; construction evaluates no model."""
def protocol(frame):
    return dict(version='retained-head-coupled-canal-clock-v1',ownerVersion='physical-clock-coupled-canal-held-input-v1',
        frame=frame,selectedSensor='centre',subdivisions=[256,512,1024],initial=[0.]*12,expectedIntervals=199,
        expectedNativeRuns=3,expectedNativeSteps=356608,referencePrecisions=[60,90],
        candidateBounds=dict(volumeAbsolute=1e-18,flowAbsolute=1e-17,relative=1e-5),
        referenceBounds=dict(volumeAbsolute=1e-20,flowAbsolute=1e-19,relative=1e-7),
        matrixPrecisionAbsolute='1e-40',matrixPrecisionRelative='1e-40',refinementMaximumRemainingFraction=.3,refinementNormalizedFloor=1e-5,
        energyAbsoluteJoules=1e-32,energyRelative=5e-10,
        inputPolicy='angular input available at interval start; newly arriving input affects only the next interval',
        initialStatePolicy='explicit zero volumes/flows at first angular availability; no earlier integration',
        boundsSource='unchanged published-canal middle-regime bounds, prospectively applied to this entire replay',
        allNativeEndpointsCompared=True,continuousMotionAccuracyQualified=False,anatomicalCalibrationQualified=False,
        afferentFiringQualified=False,avatarBalanceAssessed=False)

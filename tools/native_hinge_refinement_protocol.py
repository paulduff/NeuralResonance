"""Prospective native resolution matrix; no model execution."""
CONDITIONS=[dict(key='coarse',physicsResolution=-1,muscleSubsteps=4,steps=512),
    dict(key='baseline',physicsResolution=0,muscleSubsteps=2,steps=1024),
    dict(key='fine',physicsResolution=1,muscleSubsteps=1,steps=2048),
    dict(key='baseline-muscle-fine',physicsResolution=0,muscleSubsteps=4,steps=1024),
    dict(key='fine-muscle-fine',physicsResolution=1,muscleSubsteps=2,steps=2048)]
CRITERIA=dict(refinementMaximumRatio=.8,refinementFloorFraction=.02,muscleRefinementBoundFraction=.05,
    baselineRepeatBoundFraction=.01,absoluteAccuracyRequired=['baseline','fine','baseline-muscle-fine','fine-muscle-fine'],
    commonRefinementSamples=513,referenceGridSamples=1025,finestHalfStepReferenceSamplesAssessed=False,
    fullCoupledTrajectoryQualified=False,physiologyQualified=False,avatarRecoveryAssessed=False)

"""Prospective strong-left partition study; no equation evaluation."""
VERSION='rubral-native-partition-refinement-v1'
RESOLUTIONS=[dict(key='repeat',physicsResolution=1,muscleSubsteps=2,steps=7999),
    dict(key='muscle-fine',physicsResolution=1,muscleSubsteps=4,steps=7999),
    dict(key='body-fine',physicsResolution=2,muscleSubsteps=2,steps=15998),
    dict(key='body-finer',physicsResolution=3,muscleSubsteps=1,steps=31996),
    dict(key='body-finer-muscle-fine',physicsResolution=3,muscleSubsteps=2,steps=31996)]
CRITERIA=dict(commonSamples=8000,baselineRepeatBoundFraction=.01,refinementMaximumRatio=.8,
    refinementFloorFraction=.02,muscleRefinementBoundFraction=.05,
    absoluteAccuracyRequired=['body-finer','body-finer-muscle-fine'],
    originalBoundsChanged=False,finestIntermediateReferenceSamplesAssessed=False,
    fullCaseMatrixAssessed=False,physiologyQualified=False,avatarRecoveryAssessed=False)

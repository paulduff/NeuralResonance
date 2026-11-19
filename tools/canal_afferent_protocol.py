"""Prospective connected capture; fixture selection executes no equations."""
from copy import deepcopy


def protocol(bundle,loaded):
    assert bundle['version']=='retained-head-spatial-bundle-v1' and loaded['restInitialized']
    quiet=next(c for c in loaded['cases'] if c['key']=='quiet')
    return dict(version='recorded-canal-connected-afferent-v1',bundle=deepcopy(bundle),fixture=deepcopy(quiet['fixture']),
        nonlinearSettings=deepcopy(loaded['nonlinearSettings']),sourceStepsPerInterval=[16384,32768,65536],
        mechanicalRefinement=4,physicalIntervals=10,expectedRuns=6,expectedSourceSteps=2293760,
        quietSessionId='76877da4-4e5c-418e-aa1a-608ecf16fd4f',observationNode='node2-000',thresholdMv=0.,maximumPacketSteps=512,
        activationPolicy='EndpointHold',originalActivationDefaultChanged=False,
        ledgerResidualBounds=deepcopy(loaded['ledgerResidualBounds']),
        conditions=['recorded-head','stationary-head'],
        quietPolicy='explicit stationary head at original initial pose; new control session, zero velocities, original gravity; no imposed neuronal current',
        initialStatePolicy='saved loaded quiet root, with every actual gate and ion preserved; no settling or perturbation; explicit first-angular epoch',
        recordingPolicy='every source step, state/compensation/flux, original observation bracket and quiet/firing packet retained',
        independentConnectedAccuracyQualified=False,physiologicalCalibrationQualified=False,avatarBalanceAssessed=False)

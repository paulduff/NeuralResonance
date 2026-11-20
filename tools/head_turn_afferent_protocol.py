"""Prospective diagnostic yaw input; no neuronal or mechanical execution."""
from copy import deepcopy

VERSION = 'prescribed-head-midpoint-afferent-v1'
SESSIONS = dict(quiet='b66c8b19-5aba-4668-9c87-26709a8fcd01',
    **{'yaw-positive': 'b66c8b19-5aba-4668-9c87-26709a8fcd02',
       'yaw-negative': 'b66c8b19-5aba-4668-9c87-26709a8fcd03'})


def protocol(original):
    assert original['version'] == 'recorded-canal-midpoint-afferent-v2'
    assert original['forcingPolicy'] == 'MidpointHeld' and original['activationPolicy'] == 'EndpointHold'
    p = deepcopy(original)
    p.update(version=VERSION, conditions=list(SESSIONS), sessions=deepcopy(SESSIONS),
        sourceStepsPerInterval=[16384, 32768], physicalIntervals=18, expectedRuns=6, expectedSourceSteps=2654208,
        motion=dict(axis='Yaw', angleRadians=.1, startOffsetSteps=2, turnSteps=10, physicsStepMs=10., neuralEpochMs=1000.),
        motionPolicy='Prescribed diagnostic 0/+0.1/-0.1 radian quintic yaw, 100 ms turn, canonical stationary head; not native motion or physiological calibration',
        quietPolicy='Same canonical stationary head and gravity, zero excursion, new control session, no neuronal current',
        independentConnectedAccuracyQualified=False, physiologicalCalibrationQualified=False,
        afferentFiringQualified=False, avatarBalanceAssessed=False)
    p.pop('quietSessionId')
    # Geometry/material, cell/gates/ions, initial state, thresholds and solver
    # settings remain byte-equivalent JSON values from the completed input.
    return p

"""Independent analytic prescribed-motion audit; no neuronal integration."""
import math


def audit(head, p, condition, clock):
    assert head['condition'] == condition and head['clock'] == clock
    assert head['prescribedDiagnosticInput'] and not head['nativeHeadMeasurements']
    expected_clock = dict(sessionId=p['sessions'][condition], bodyId='prescribed-head', sourceId='diagnostic-quintic-yaw',
        initialPhysicsStep=0, physicsStepMs=10., physicalEpochMs=0., neuralEpochMs=1000., fingerprint=clock['fingerprint'])
    assert clock == expected_clock
    assert head['observations'] == [s['observation'] for s in head['samples']]
    assert len(head['samples']) == p['physicalIntervals'] + 2 == 20
    amplitude = {'quiet': 0., 'yaw-positive': .1, 'yaw-negative': -.1}[condition]
    def near(actual, expected):
        assert math.isfinite(actual) and abs(actual-expected) <= 1e-12, (actual, expected)
    def vector(actual, expected):
        assert set(actual) == {'x', 'y', 'z'}
        for key, value in zip(('x','y','z'), expected, strict=True): near(actual[key], value)
    identities = []
    for step, sample in enumerate(head['samples']):
        u = min(1., max(0., (step-2)/10))
        angle = amplitude*(10*u**3-15*u**4+6*u**5)
        speed = amplitude/.1*30*u*u*(1-u)*(1-u)
        acceleration = amplitude/(.1*.1)*60*u*(1-u)*(1-2*u)
        near(sample['angleRadians'], angle)
        near(sample['angularSpeedRadiansPerSecond'], speed)
        near(sample['angularAccelerationRadiansPerSecondSquared'], acceleration)
        o = sample['observation']; identities.append(o['sampleId'])
        assert o['physicsStep'] == step and o['headBodyId'] == 'head' and o['sensorId'] == 'centre'
        for key in ('localPointMeters','originWorldMeters','pointWorldMeters','pointVelocityWorldMetersPerSecond'):
            vector(o[key], (0,0,0))
        vector(o['rightWorld'], (math.cos(angle),0,-math.sin(angle)))
        vector(o['upWorld'], (0,1,0)); vector(o['forwardWorld'], (math.sin(angle),0,math.cos(angle)))
        vector(o['angularVelocityWorldRadiansPerSecond'], (0,speed,0))
        vector(o['gravityWorldMetersPerSecondSquared'], (0,-9.80665,0))
        vector(sample['pointAccelerationWorldMetersPerSecondSquared'], (0,0,0))
    assert len(set(identities)) == 20
    return dict(samples=20,condition=condition,prescribedKinematicsPassed=True)

"""Analytic-input rejection controls; no neuronal or mechanical integration."""
import copy
import math
import unittest
from head_turn_input_evidence import audit
from head_turn_afferent_protocol import protocol, SESSIONS


class HeadTurnInputControls(unittest.TestCase):
    def fixture(self, condition='quiet'):
        p=dict(physicalIntervals=18,sessions=SESSIONS)
        clock=dict(sessionId=SESSIONS[condition],bodyId='prescribed-head',sourceId='diagnostic-quintic-yaw',
            initialPhysicsStep=0,physicsStepMs=10.,physicalEpochMs=0.,neuralEpochMs=1000.,fingerprint='retained')
        def v(x=0,y=0,z=0):return dict(x=x,y=y,z=z)
        a={'quiet':0.,'yaw-positive':.1,'yaw-negative':-.1}[condition];samples=[]
        for i in range(20):
            u=min(1,max(0,(i-2)/10));angle=a*u**3*(10-15*u+6*u*u)
            speed=300*a*u*u*(1-u)**2;acceleration=6000*a*u*(1-u)*(1-2*u)
            o=dict(physicsStep=i,sampleId=str(i),headBodyId='head',sensorId='centre',localPointMeters=v(),
                originWorldMeters=v(),pointWorldMeters=v(),pointVelocityWorldMetersPerSecond=v(),
                rightWorld=v(math.cos(angle),0,-math.sin(angle)),upWorld=v(0,1),forwardWorld=v(math.sin(angle),0,math.cos(angle)),
                angularVelocityWorldRadiansPerSecond=v(0,speed),gravityWorldMetersPerSecondSquared=v(0,-9.80665))
            samples.append(dict(observation=o,angleRadians=angle,angularSpeedRadiansPerSecond=speed,
                angularAccelerationRadiansPerSecondSquared=acceleration,pointAccelerationWorldMetersPerSecondSquared=v()))
        h=dict(condition=condition,clock=clock,prescribedDiagnosticInput=True,nativeHeadMeasurements=False,
            observations=[s['observation'] for s in samples],samples=samples)
        return h,p,condition,clock

    def test_quiet_and_opposite_analytic_controls_include_the_complete_held_tail(self):
        for condition in SESSIONS:
            args=self.fixture(condition);self.assertTrue(audit(*args)['prescribedKinematicsPassed'])
            self.assertEqual(args[0]['samples'][12]['angleRadians'],args[0]['samples'][-1]['angleRadians'])

    def test_wrong_rotation_direction_cannot_pass_as_the_requested_turn(self):
        h,p,c,clock=self.fixture('yaw-positive');h['observations'][7]['rightWorld']['z']*=-1
        with self.assertRaises(AssertionError):audit(h,p,c,clock)

    def test_millisecond_second_confusion_is_rejected(self):
        h,p,c,clock=self.fixture('yaw-negative');h['samples'][7]['angularSpeedRadiansPerSecond']/=1000
        with self.assertRaises(AssertionError):audit(h,p,c,clock)

    def test_changed_clock_or_session_is_rejected(self):
        for key,value in [('physicsStepMs',1.),('sessionId',SESSIONS['yaw-positive'])]:
            h,p,c,clock=self.fixture();clock[key]=value
            with self.assertRaises(AssertionError):audit(h,p,c,clock)

    def test_nonfinite_observation_is_rejected(self):
        h,p,c,clock=self.fixture();h['observations'][4]['gravityWorldMetersPerSecondSquared']['y']=float('nan')
        with self.assertRaises(AssertionError):audit(h,p,c,clock)

    def test_missing_samples_and_repeated_identities_are_rejected(self):
        h,p,c,clock=self.fixture();h['observations'][3]['sampleId']=h['observations'][2]['sampleId']
        with self.assertRaises(AssertionError):audit(h,p,c,clock)
        h,p,c,clock=self.fixture();h['samples'].pop();h['observations'].pop()
        with self.assertRaises(AssertionError):audit(h,p,c,clock)

    def test_original_biological_parameters_are_detached_and_preserved(self):
        old=dict(version='recorded-canal-midpoint-afferent-v2',forcingPolicy='MidpointHeld',activationPolicy='EndpointHold',
            quietSessionId='old',fixture={'calcium':[1,2]},thresholdMv=0.,nonlinearSettings={'tolerance':1e-12})
        before=copy.deepcopy(old);p=protocol(old)
        self.assertEqual(old,before);self.assertEqual(p['fixture'],old['fixture']);self.assertEqual(p['thresholdMv'],0.)
        self.assertEqual(p['nonlinearSettings'],old['nonlinearSettings']);p['fixture']['calcium'][0]=9
        self.assertEqual(old,before);self.assertEqual(p['expectedSourceSteps'],3*18*(16384+32768))

    def test_changed_gravity_or_unmeasured_claim_is_rejected(self):
        h,p,c,clock=self.fixture();h['observations'][5]['gravityWorldMetersPerSecondSquared']['y']*=-1
        with self.assertRaises(AssertionError):audit(h,p,c,clock)
        h,p,c,clock=self.fixture();h['nativeHeadMeasurements']=True
        with self.assertRaises(AssertionError):audit(h,p,c,clock)

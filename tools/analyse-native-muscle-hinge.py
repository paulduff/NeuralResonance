"""Audit saved native hinge states against analytic geometry and impulse laws."""
import argparse
from datetime import datetime, timezone
import json
import math
from pathlib import Path
import struct

read = lambda p: json.loads(p.read_text(encoding='utf-8-sig'))
f32 = lambda x: struct.unpack('<f', struct.pack('<f', x))[0]
vec = lambda v: tuple(float(v[k]) for k in ('x', 'y', 'z'))
sub = lambda a,b: tuple(x-y for x,y in zip(a,b))
mul = lambda a,s: tuple(x*s for x in a)
dot = lambda a,b: sum(x*y for x,y in zip(a,b))
norm = lambda a: math.sqrt(dot(a,a))
error = lambda a,b: norm(sub(a,b))


def cross(a,b):
    return (a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0])


def close(a,b,label):
    assert math.isfinite(a) and math.isfinite(b) and abs(a-b)<=1e-12,(label,a,b)


def matrix():
    return [(f32(a),side,f32(h),t) for a in (-.6,0,.6) for side in (-1,1)
            for h in (.001,.000125) for t in (0,10)]


def audit_case(row):
    before=row['before']; child=before[1]; q=row['angleBefore']; speed=row['speedBefore']; h=row['stepSeconds']
    radius=f32(.4); center=f32(.3); origin=(0.,center,row['side']*.25)
    point=(0.,radius*math.cos(q),radius*math.sin(q)); derivative=(0.,-radius*math.sin(q),radius*math.cos(q))
    delta=sub(point,origin); length=norm(delta); derivative_length=dot(delta,derivative)/length
    torque=-row['tensionNewtons']*derivative_length; inertia=f32(.15)+2*center*center
    measured_delta=sub(vec(child['point']),vec(before[0]['point'])); measured_length=norm(measured_delta)
    u=mul(measured_delta,1/measured_length); force=mul(u,-row['tensionNewtons'])
    measured_speed=dot(u,sub(vec(child['pointVelocity']),vec(before[0]['pointVelocity'])))
    power=dot(force,vec(child['pointVelocity']))-dot(force,vec(before[0]['pointVelocity']))
    measured_torque=cross(vec(child['point']),force)[0]
    assert error(vec(before[0]['point']),origin)<=2e-6
    assert error(force,vec(child['appliedForce']))<=3e-6 and error(mul(force,-1),vec(before[0]['appliedForce']))<=3e-6
    native_torque=cross(vec(child['point']),vec(child['appliedForce']))[0]; acceleration=native_torque/inertia
    metrics=dict(geometryError=max(error(point,vec(child['point'])),abs(measured_length-length)),
        pointVelocityError=max(error(mul(derivative,speed),vec(child['pointVelocity'])),abs(measured_speed-derivative_length*speed)),
        torqueError=abs(measured_torque-torque),powerError=abs(power-torque*speed),
        impulseSpeedError=abs(row['speedAfterForce']-(speed+acceleration*h)),
        clearSpeedError=abs(row['speedAfterClear']-row['speedAfterForce']),
        forcedAngleError=abs(row['angleAfterForce']-(q+speed*h+.5*acceleration*h*h)),
        forcedAngleAllowance=2e-6+.5*abs(acceleration)*h*h,
        clearAngleError=abs(row['angleAfterClear']-(row['angleAfterForce']+row['speedAfterForce']*h)),
        endpointPoseError=0.,endpointLinearVelocityError=0.,endpointAngularVelocityError=0.,rootPositionError=0.,rootLinearSpeed=0.,rootAngularSpeed=0.,rootRotationError=0.)
    for stage, angle, omega in (('before',q,speed),('afterForce',row['angleAfterForce'],row['speedAfterForce']),('afterClear',row['angleAfterClear'],row['speedAfterClear'])):
        root,body=row[stage]
        assert root['id']=='fixed-root' and body['id']=='hinge-child'
        for b in (root,body):
            assert b['mass']==2 and vec(b['inertia'])==(f32(.15),)*3
            for field in ('position','center','velocity','angularVelocity','point','pointVelocity','localAnchor','inertia','appliedForce'):
                assert all(math.isfinite(x) for x in vec(b[field]))
            assert all(math.isfinite(x) for x in b['rotation'].values())
        expected_point=(0.,radius*math.cos(angle),radius*math.sin(angle))
        expected_com=(0.,center*math.cos(angle),center*math.sin(angle)); w=(omega,0.,0.)
        metrics['endpointPoseError']=max(metrics['endpointPoseError'],norm(vec(body['position'])),error(expected_point,vec(body['point'])),error(expected_com,vec(body['center'])))
        metrics['endpointAngularVelocityError']=max(metrics['endpointAngularVelocityError'],error(w,vec(body['angularVelocity'])))
        metrics['endpointLinearVelocityError']=max(metrics['endpointLinearVelocityError'],error(cross(w,expected_com),vec(body['velocity'])),error(cross(w,expected_point),vec(body['pointVelocity'])))
        for name,field in (('rootPositionError','position'),('rootLinearSpeed','velocity'),('rootAngularSpeed','angularVelocity')):
            metrics[name]=max(metrics[name],norm(vec(root[field])))
        metrics['rootRotationError']=max(metrics['rootRotationError'],norm(vec(root['rotation'])),abs(abs(root['rotation']['w'])-1))
    expected_values=dict(effectiveInertia=inertia,analyticLengthMeters=length,analyticLengthDerivative=derivative_length,
        analyticTorque=torque,measuredTorque=measured_torque,lengthMeters=measured_length,pathSpeedMetersPerSecond=measured_speed,powerWatts=power)
    for name,value in dict(metrics,**expected_values).items():close(value,row[name],name)
    assert abs(q-(row['targetAngleRadians']+.25*h))<=2e-6 and abs(speed-.25)<=2e-6
    bounds={name:2e-6 for name in metrics if name not in ('forcedAngleError','forcedAngleAllowance')}
    bounds.update(torqueError=3e-6,powerError=2e-5)
    for name,bound in bounds.items():assert 0<=metrics[name]<=bound,(row['key'],name,metrics[name],bound)
    assert metrics['forcedAngleError']<=metrics['forcedAngleAllowance']
    return metrics


def audit(package):
    config=read(package/'configuration.json'); native=read(package/'native/results.json')
    assert config['IsolatedNativeMuscleHinge'] and config['ExpectedNativeCases']==24 and config['ExpectedNativePhysicsSteps']==72
    assert native['completed'] and native['passed'] and native['isolated'] and not native['error']
    assert not any(native[k] for k in ('neuronalModelExecuted','avatarLoaded','anatomicalCalibrationQualified'))
    assert native['unity']==config['ExpectedUnityVersion'] and native['sessionId']==config['SessionId']
    assert native['actualPhysicsSteps']==72 and native['warmupStepsPerCase']==1 and len(native['cases'])==24
    assert [native[k] for k in ('geometryBound','angleRoundoffBound','velocityBound','torqueBound','powerBound')]==[2e-6,2e-6,2e-6,3e-6,2e-5]
    rows=[]
    for index,(case,parameters) in enumerate(zip(native['cases'],matrix())):
        assert case['key']=='case-'+str(index).zfill(2) and case['completed'] and case['passed'] and not case['error']
        assert (case['targetAngleRadians'],case['side'],case['stepSeconds'],case['tensionNewtons'])==parameters
        assert case['startStep']==3*index and case['endStep']==3*(index+1)
        assert all(len(case[s])==2 for s in ('before','afterForce','afterClear'))
        assert read(package/'native'/(case['key']+'.json'))==case
        initial=read(package/'native'/(case['key']+'-initial.json')); forced=read(package/'native'/(case['key']+'-forced.json'))
        assert initial['before']==forced['before']==case['before'] and forced['afterForce']==case['afterForce']
        rows.append(dict(key=case['key'],passed=True,metrics=audit_case(case)))
    return dict(completed=True,originalCriteriaPassed=True,independentlyRecomputedCases=24,actualPhysicsSteps=72,
        maximumErrors={k:max(r['metrics'][k] for r in rows) for k in rows[0]['metrics']},cases=rows,
        testsOrPhysicsExecutedByAnalysis=False,wholeTrajectoryAccuracyQualified=False,physiologyQualified=False,avatarRecoveryAssessed=False)


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__); parser.add_argument('package',type=Path); args=parser.parse_args()
    package=args.package.resolve(); result=audit(package)
    result['analysedUtc']=datetime.now(timezone.utc).isoformat()
    with (package/'hinge-analysis.json').open('x',encoding='utf-8') as stream:stream.write(json.dumps(result,indent=2,allow_nan=False)+'\n')
    print(json.dumps({k:v for k,v in result.items() if k!='cases'},indent=2))

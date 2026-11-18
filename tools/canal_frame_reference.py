"""Independent Decimal coordinate reconstruction; execute in visible PowerShell."""
from decimal import Decimal,localcontext
import json
from pathlib import Path


def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def save(p,x):
    with p.open('x',encoding='utf-8') as f:json.dump(x,f,indent=2,allow_nan=False)
def decimal(x):return Decimal.from_float(x) if isinstance(x,float) else Decimal(x)
def vector(x):return [decimal(x[k]) for k in ('X','Y','Z')]
def det(a):
    return (a[0][0]*(a[1][1]*a[2][2]-a[1][2]*a[2][1])-
            a[0][1]*(a[1][0]*a[2][2]-a[1][2]*a[2][0])+
            a[0][2]*(a[1][0]*a[2][1]-a[1][1]*a[2][0]))
def solve(a,b):
    # Independent Cramer's rule at high precision, not native scaled LU.
    determinant=det(a)
    if determinant==0:raise ArithmeticError('Singular declared reference basis.')
    result=[]
    for i in range(3):
        m=[row[:] for row in a]
        for j in range(3):m[j][i]=b[j]
        result.append(det(m)/determinant)
    return result


def controls(output):
    rows=[]
    for name,a,b,x in [
        ('identity',[[1,0,0],[0,1,0],[0,0,1]],[2,-3,4],[2,-3,4]),
        ('oblique',[[1,1,0],[0,1,1],[0,0,1]],[3,5,3],[1,2,3]),
        ('scaled',[[2,0,0],[0,-4,0],[0,0,8]],[2,8,24],[1,-2,3]),
        ('pivot',[[0,0,1],[1,0,0],[0,1,0]],[3,1,2],[1,2,3]),
        ('zero',[[1,1,0],[0,1,1],[0,0,1]],[0,0,0],[0,0,0])]:
        for precision in (60,90):
            with localcontext() as ctx:
                ctx.prec=precision;actual=solve([[Decimal(v) for v in row] for row in a],[Decimal(v) for v in b])
                assert actual==[Decimal(v) for v in x]
        rows.append(dict(name=name,passed=True))
    try:solve([[Decimal(1)]*3]*3,[Decimal(1)]*3)
    except ArithmeticError:rows.append(dict(name='singular-rejected',passed=True))
    else:raise AssertionError('Singular reference was accepted.')
    save(output,dict(completed=True,passed=6,cases=rows))


def evaluate(spec,source,precision):
    with localcontext() as ctx:
        ctx.prec=precision
        cols=[[Decimal(v) for v in row] for row in source['primeDirections']['vectors']]
        matrix=[list(row) for row in zip(*cols)];previous={};rows=[]
        seconds=decimal(spec['clock']['physicsStepMs'])/Decimal(1000)
        for record in spec['records']:
            obs=json.loads(record['receipt']['rawObservationJson']);sensor=record['sensorId'];prior=previous.get(sensor)
            row=dict(step=obs['PhysicsStep'],sensorId=sensor,availableAtNeuralMs=record['receipt']['neuralTimeMs'],projection=None)
            if prior is not None:
                world=[(a-b)/seconds for a,b in zip(vector(obs['AngularVelocityWorldRadiansPerSecond']),vector(prior['AngularVelocityWorldRadiansPerSecond']))]
                local=[sum(a*b for a,b in zip(world,vector(obs[k]))) for k in ('RightWorld','UpWorld','ForwardWorld')]
                axial=[local[2],-local[0],-local[1]];prime=solve(matrix,axial)
                reconstruction=[sum(a*b for a,b in zip(m,prime)) for m in matrix]
                row.update(fromStep=prior['PhysicsStep'],projection={k:[str(v) for v in values] for k,values in
                    [('world',world),('skull',local),('source',axial),('prime',prime),('reconstructed',reconstruction)]})
            rows.append(row);previous[sensor]=obs
        return dict(completed=True,precision=precision,rows=rows,mechanicsIntegrated=False,sourceDirectionsNormalized=False)


def run(package):
    controls(package/'reference-controls.json')
    spec=read(package/'frame-input.json');source=read(package/'source/docs/reference-data/vestibular-ifediba-2007.json')
    for precision in (60,90):save(package/f'reference-{precision}.json',evaluate(spec,source,precision))
    print('600 angular samples reconstructed at 60 and 90 decimal digits; no physics or organ trajectory executed.',flush=True)


def compare(package, *, output_path=None):
    spec=read(package/'frame-input.json');a=read(package/'reference-60.json');b=read(package/'reference-90.json')
    assert read(package/'study/input.json')==spec
    controls=read(package/'reference-controls.json')
    assert controls['completed'] and controls['passed']==len(controls['cases'])==6 and all(c['passed'] for c in controls['cases'])
    assert a['precision']==60 and b['precision']==90
    assert a['completed'] and b['completed'] and len(a['rows'])==len(b['rows'])==603
    result=read(package/'study/results.json');assert result['completed'] and result['retainedSamples']==603 and result['angularSamples']==600
    assert result['originalSkullReceiptReplayExact'] and not result['newNativePhysicsExecuted'] and not result['mechanicsIntegrated']
    maxima=dict(precision=0.,native=0.,backward=0.);compared=0;by_step={}
    def close(actual,expected,absolute,relative):
        with localcontext() as ctx:
            ctx.prec=100;x=decimal(actual);y=Decimal(expected);ratio=abs(x-y)/(Decimal(absolute)+Decimal(relative)*abs(y))
        assert ratio<=1,(actual,expected,str(ratio));return float(ratio)
    for i,(low,high,original) in enumerate(zip(a['rows'],b['rows'],spec['records'],strict=True)):
        n=read(package/'study'/f'sample-{i:06}.json');r=n['receipt'];p=high['projection'];raw=original['receipt']
        assert n['step']==low['step']==high['step']==raw['physicsStep'] and n['sensorId']==low['sensorId']==high['sensorId']==original['sensorId']
        assert n['sourceFile']==original['sourceFile'] and n['sourceSha256']==original['sourceSha256'] and r['skull']==raw
        assert high['availableAtNeuralMs']==low['availableAtNeuralMs']==raw['neuralTimeMs']
        if p is None:
            assert low['projection'] is None and r['projection'] is None and r['differenceFromStep'] is None
            assert r['averageAngularAccelerationWorldRadiansPerSecondSquared'] is None
            assert r['averageAngularAccelerationSkullRadiansPerSecondSquared'] is None
            continue
        assert low['fromStep']==high['fromStep']==r['differenceFromStep']==raw['physicsStep']-1
        native=r['projection'];components=native['components']
        values=dict(world=r['averageAngularAccelerationWorldRadiansPerSecondSquared'],skull=r['averageAngularAccelerationSkullRadiansPerSecondSquared'],
            source=native['sourceAccelerationRadiansPerSecondSquared'],prime=components,reconstructed=native['reconstructedSourceAcceleration'])
        for key,expected in p.items():
            names=('hc','ac','pc') if key=='prime' else ('x','y','z')
            for j,name in enumerate(names):
                maxima['precision']=max(maxima['precision'],close(Decimal(low['projection'][key][j]),expected[j],'1e-45','1e-45'))
                maxima['native']=max(maxima['native'],close(values[key][name],expected[j],'1e-9','1e-12'))
        assert 0<=native['maximumBackwardError']<=1e-12;maxima['backward']=max(maxima['backward'],native['maximumBackwardError'])
        # All three positions belong to the same rigid head; their angular input
        # must be identical, without pooling their distinct sample provenance.
        if n['step'] in by_step:assert components==by_step[n['step']]
        else:by_step[n['step']]=components
        compared+=1
    assert compared==600
    backward=maxima.pop('backward')
    output=dict(completed=True,angularInputChecksPassed=True,originalReceiptReplayCount=603,independentAngularSamples=600,
        maximumAllowedFractions=maxima,maximumNativeBackwardError=backward,referenceControlsPassed=6,sourceDirectionsNormalized=False,
        printedPosteriorNormIssueResolved=False,continuousMotionAccuracyQualified=False,
        mechanicalClockCouplingQualified=False,anatomicalCalibrationQualified=False,afferentFiringQualified=False,avatarBalanceAssessed=False)
    save(package/'analysis.json' if output_path is None else output_path,output);return output

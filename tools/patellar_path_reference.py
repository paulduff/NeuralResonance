"""Independent piecewise-polynomial constraints and virtual-work derivatives."""
import bisect
import json
import math
from pathlib import Path
import xml.etree.ElementTree as ET
import mpmath as mp

NAMES=('position','first','second','applicationPoint','force','childTorque','parentTorque','movingVelocity')
LIMITS=dict(position='1e-12',first='1e-11',second='1e-10',applicationPoint='1e-12',force='1e-12',
    childTorque='1e-9',parentTorque='1e-9',movingVelocity='1e-10',generalizedForce='1e-9',speed='1e-12',power='1e-9',deliveredPower='1e-9')

def vector(d):return [mp.mpf(d[k]) for k in ('x','y','z')]
def dot(a,b):return sum(x*y for x,y in zip(a,b,strict=True))

def spline(curve):
    # Solve all interval coefficients together. This is deliberately separate
    # from the native tridiagonal second-derivative implementation.
    x=list(map(mp.mpf,curve['knots']));y=list(map(mp.mpf,curve['values']));n=len(x)-1
    a=mp.matrix(4*n);b=mp.matrix(4*n,1);row=0
    def put(interval,s,derivative,sign=1):
        for k in range(derivative,4):
            a[row,4*interval+k]+=sign*math.factorial(k)//math.factorial(k-derivative)*s**(k-derivative)
    for i in range(n):
        put(i,mp.mpf(0),0);b[row]=y[i];row+=1
        put(i,x[i+1]-x[i],0);b[row]=y[i+1];row+=1
    for i in range(n-1):
        for order in (1,2):
            put(i,x[i+1]-x[i],order);put(i+1,mp.mpf(0),order,-1);row+=1
    def third(start):
        return sum(y[j]/mp.fprod(x[j]-x[k] for k in range(start,start+4) if k!=j) for j in range(start,start+4))
    for interval,start in ((0,0),(n-1,n-3)):
        put(interval,mp.mpf(0),3);b[row]=6*third(start);row+=1
    assert row==4*n
    coefficients=mp.lu_solve(a,b)
    assert max(abs(v) for v in a*coefficients-b)<mp.mpf('1e-60')
    def evaluate(q,interval):
        s=q-x[interval]
        return sum(coefficients[4*interval+k]*s**k for k in range(4))
    return evaluate

def reference(row,side,curves,dps):
    with mp.workdps(dps):
        q=mp.mpf(row['q']);knots=side['curves']['angle']['knots'];i=min(len(knots)-2,bisect.bisect_right(knots,row['q'])-1)
        assert 0<=i<len(knots)-1
        point=next(p for p in side['points'] if p['id']==row['pointId']);local=list(map(mp.mpf,point['localMeters']))
        offset=list(map(mp.mpf,side['parentOffsetMeters']));z=mp.mpf(side['zTranslationMeters'])
        def position(t):
            angle=curves['angle'](t,i);c=mp.cos(angle);s=mp.sin(angle)
            return [offset[0]+curves['x'](t,i)+c*local[0]-s*local[1],
                offset[1]+curves['y'](t,i)+s*local[0]+c*local[1],offset[2]+z+local[2]]
        f=vector(row['force']);axis=vector(row['axis']);parent=vector(row['parentAngular']);child=vector(row['childAngular']);vp=vector(row['parentVelocity'])
        p=position(q);first=[mp.diff(lambda t:position(t)[j],q) for j in range(3)];second=[mp.diff(lambda t:position(t)[j],q,2) for j in range(3)]
        # Differentiate the work potential instead of reusing the candidate's
        # explicit rotation derivative or its dot(force, Jacobian) formula.
        generalized=mp.diff(lambda t:dot(f,position(t)),q)
        speed=dot(axis,[c-p for c,p in zip(child,parent,strict=True)])
        moving=[v+d*speed for v,d in zip(vp,first,strict=True)];torque=[v*generalized for v in axis]
        counter=[-v for v in torque];power=dot(f,moving);delivered=dot(f,vp)+dot(torque,child)+dot(counter,parent)
        values=dict(position=p,first=first,second=second,applicationPoint=p,force=f,childTorque=torque,parentTorque=counter,
            movingVelocity=moving,generalizedForce=generalized,speed=speed,power=power,deliveredPower=delivered)
        return {k:[mp.nstr(v,dps) for v in value] if k in NAMES else mp.nstr(value,dps) for k,value in values.items()}

def flat(values):
    return [(k,mp.mpf(v)) for k in LIMITS for v in (values[k] if k in NAMES else [values[k]])]

def candidate(row):
    p=row['point'];t=row['transfer']
    return dict(position=vector(p['parentLocalPositionMeters']),first=vector(p['derivativeMetersPerRadian']),second=vector(p['secondDerivativeMetersPerRadianSquared']),
        applicationPoint=vector(t['applicationPointMeters']),force=vector(t['parentPointForceNewtons']),childTorque=vector(t['childTorqueNewtonMeters']),
        parentTorque=vector(t['parentCounterTorqueNewtonMeters']),movingVelocity=vector(t['movingPointVelocityMetersPerSecond']),
        generalizedForce=t['generalizedForceNewtonMeters'],speed=t['relativeCoordinateSpeedRadiansPerSecond'],power=t['movingPointPowerWatts'],deliveredPower=t['deliveredPowerWatts'])

def compare(package):
    ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'};rows=[]
    for out in ET.parse(package/'test-results/focused.trx').findall('.//t:UnitTestResult/t:Output/t:StdOut',ns):
        for line in (out.text or '').splitlines():
            if line.strip().startswith('PATELLAR_PATH_JSON '):rows.append(json.loads(line.strip()[len('PATELLAR_PATH_JSON '):]))
    source=json.loads((package/'patellar-input/patellar-source.json').read_text(encoding='utf-8'))
    assert len(rows)==450 and {r['index'] for r in rows}==set(range(450))
    sides={s['side']:s for s in source['sides']};expected=set()
    for s in sides.values():
        knots=s['curves']['angle']['knots'];samples=knots+[(a+b)/2 for a,b in zip(knots,knots[1:])]
        expected.update((s['side'],p['id'],q) for p in s['points'] for q in samples)
    assert {(r['side'],r['pointId'],r['q']) for r in rows}==expected and len(expected)==450
    references={}
    for dps in (80,110):
        with mp.workdps(dps):
            curves={side:{key:spline(c) for key,c in s['curves'].items()} for side,s in sides.items()}
            references[dps]={r['index']:reference(r,sides[r['side']],curves[r['side']],dps) for r in rows}
        with (package/('patellar-reference-'+str(dps)+'.json')).open('x',encoding='utf-8') as f:
            json.dump(references[dps],f,indent=2,allow_nan=False)
        print('PATELLAR INDEPENDENT POLYNOMIALS',dps,'digits; 450 complete points',flush=True)
    results=[]
    with mp.workdps(120):
        for r in sorted(rows,key=lambda v:v['index']):
            original=next(p for p in sides[r['side']]['points'] if p['id']==r['pointId'])
            assert r['localPoint']==original['localMeters']
            refs=[references[d][r['index']] for d in (80,110)];strict=flat(refs[1])
            agreement=max(abs(a-b) for (_,a),(_,b) in zip(flat(refs[0]),strict,strict=True))
            assert agreement<=mp.mpf('1e-60'),'Reference precision disagreement; retain both references.'
            values=flat(candidate(r));errors=[abs(a-b) for (_,a),(_,b) in zip(values,strict,strict=True)]
            bounds=[mp.mpf(LIMITS[k]) for k,_ in strict];passed=all(e<=b for e,b in zip(errors,bounds,strict=True))
            results.append(dict(index=r['index'],side=r['side'],pointId=r['pointId'],q=r['q'],passed=passed,reference80=refs[0],reference110=refs[1],
                maximumReferenceDifference=str(agreement),errors=[str(e) for e in errors],maximumAllowanceFraction=float(max(e/b for e,b in zip(errors,bounds,strict=True)))))
    report=dict(completed=True,passed=all(r['passed'] for r in results),cases=450,referenceDigits=[80,110],referenceAgreementBound='1e-60',limits=LIMITS,
        independentPolynomialConstraints=True,forceComparedToWorkDerivative=True,sourceMassAndInertiaImported=False,nativeUnityExecuted=False,
        sourceKneeRangeRadians=[0,2.0944],avatarMaximumKneeAngleRadians=2.45,anatomicalCalibrationQualified=False,avatarStandingQualified=False,rows=results)
    with (package/'patellar-reference-analysis.json').open('x',encoding='utf-8') as f:json.dump(report,f,indent=2,allow_nan=False)
    assert report['passed'],'Patellar position/Jacobian/work criteria failed; retain every record and original limits.'
    print('450 patellar points and force transfers reconciled against 80/110-digit polynomial constraints and work derivatives. Native knee coupling and standing remain unqualified.',flush=True)
    return report

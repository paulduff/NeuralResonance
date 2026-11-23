"""Independent high-precision length gradients, not the native force formulas."""
import json
from pathlib import Path
import xml.etree.ElementTree as ET
import mpmath as mp

def vector(d):return [mp.mpf(d[k]) for k in ('x','y','z')]
def add(a,b):return [x+y for x,y in zip(a,b,strict=True)]
def sub(a,b):return [x-y for x,y in zip(a,b,strict=True)]
def scale(a,k):return [x*k for x in a]
def dot(a,b):return sum(x*y for x,y in zip(a,b,strict=True))
def cross(a,b):return [a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]]
def norm(a):return mp.sqrt(dot(a,a))
def rotate(v,axis,theta):return add(add(scale(v,mp.cos(theta)),scale(cross(axis,v),mp.sin(theta))),scale(axis,dot(axis,v)*(1-mp.cos(theta))))

def reference(row,dps):
    with mp.workdps(dps):
        a=vector(row['origin']['positionMeters']);b=vector(row['insertion']['positionMeters']);f=row['frame']
        centre=vector(f['originMeters']);x=vector(f['x']);y=vector(f['y']);radius=mp.mpf(row['radius']);sign=row['direction']
        phases=[mp.mpf(row[k]) for k in ('originAzimuth','insertionAzimuth')]
        def length(a,b,c,x,y):
            z=cross(x,y);p=sub(a,c);q=sub(b,c);ax,ay=dot(p,x),dot(p,y);bx,by=dot(q,x),dot(q,y)
            ra=mp.sqrt(ax*ax+ay*ay);rb=mp.sqrt(bx*bx+by*by);assert ra>radius and rb>radius
            angles=[]
            for principal,phase in zip((mp.atan2(ay,ax),mp.atan2(by,bx)),phases,strict=True):
                angles.append(principal+2*mp.pi*mp.nint((phase-principal)/(2*mp.pi)))
            arc=sign*(angles[1]-angles[0])-mp.acos(radius/ra)-mp.acos(radius/rb)
            if arc<=0:return norm(sub(a,b))
            horizontal=mp.sqrt(ra*ra-radius*radius)+radius*arc+mp.sqrt(rb*rb-radius*radius)
            return mp.sqrt(horizontal*horizontal+(dot(q,z)-dot(p,z))**2)
        def shifted(v,j,t):r=list(v);r[j]+=t;return r
        ga=[mp.diff(lambda t:length(shifted(a,j,t),b,centre,x,y),0) for j in range(3)]
        gb=[mp.diff(lambda t:length(a,shifted(b,j,t),centre,x,y),0) for j in range(3)]
        gc=[mp.diff(lambda t:length(a,b,shifted(centre,j,t),x,y),0) for j in range(3)]
        axes=[[mp.mpf(1 if j==k else 0) for k in range(3)] for j in range(3)]
        gr=[mp.diff(lambda t:length(a,b,centre,rotate(x,axis,t),rotate(y,axis,t)),0) for axis in axes]
        rate=dot(ga,vector(row['origin']['velocityMetersPerSecond']))+dot(gb,vector(row['insertion']['velocityMetersPerSecond']))+dot(gc,vector(f['velocityMetersPerSecond']))+dot(gr,vector(f['angularVelocityRadiansPerSecond']))
        tension=mp.mpf(row['tension'])
        return dict(length=mp.nstr(length(a,b,centre,x,y),dps),rate=mp.nstr(rate,dps),
            originForce=[mp.nstr(-tension*v,dps) for v in ga],insertionForce=[mp.nstr(-tension*v,dps) for v in gb],
            guideForce=[mp.nstr(-tension*v,dps) for v in gc],guideTorque=[mp.nstr(-tension*v,dps) for v in gr])

def compare(package):
    ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'};rows=[]
    for out in ET.parse(package/'test-results/focused.trx').findall('.//t:UnitTestResult/t:Output/t:StdOut',ns):
        for line in (out.text or '').splitlines():
            if line.strip().startswith('CYLINDER_PATH_JSON '):rows.append(json.loads(line.strip()[len('CYLINDER_PATH_JSON '):]))
    assert len(rows)==24 and {r['id'] for r in rows}==set(range(24))
    results=[]
    with mp.workdps(120):
        for row in sorted(rows,key=lambda r:r['id']):
            refs=[reference(row,d) for d in (80,110)]
            def flat(ref):return [mp.mpf(ref[k]) for k in ('length','rate')]+[mp.mpf(v) for k in ('originForce','insertionForce','guideForce','guideTorque') for v in ref[k]]
            strict=flat(refs[1]);agreement=max(abs(a-b) for a,b in zip(flat(refs[0]),strict,strict=True));assert agreement<=mp.mpf('1e-60')
            s=row['snapshot'];f=row['forces']['pointForces'];guide=[p for p in f if p['point']['bodyId']=='guide'];centre=vector(row['frame']['originMeters'])
            force=[sum(vector(p['forceNewtons'])[j] for p in guide) for j in range(3)]
            torque=[sum(cross(sub(vector(p['point']['positionMeters']),centre),vector(p['forceNewtons']))[j] for p in guide) for j in range(3)]
            candidate=[mp.mpf(s['musculotendonLengthMeters']),mp.mpf(s['musculotendonVelocityMetersPerSecond'])]+vector(f[0]['forceNewtons'])+vector(f[-1]['forceNewtons'])+force+torque
            limits=[mp.mpf('2e-12'),mp.mpf('2e-12')]+[mp.mpf('1e-10')]*12
            errors=[abs(a-b) for a,b in zip(candidate,strict,strict=True)];passed=all(e<=bound for e,bound in zip(errors,limits,strict=True))
            results.append(dict(id=row['id'],passed=passed,reference80=refs[0],reference110=refs[1],maximumReferenceDifference=str(agreement),
                errors=[str(e) for e in errors],maximumAllowanceFraction=float(max(e/b for e,b in zip(errors,limits,strict=True)))))
            print('CYLINDER GRADIENT',row['id'],'passed',passed,flush=True)
    report=dict(completed=True,passed=all(r['passed'] for r in results),cases=24,referenceDigits=[80,110],referenceAgreementBound='1e-60',
        lengthBoundMeters=2e-12,rateBoundMetersPerSecond=2e-12,forceBoundNewtons=1e-10,torqueBoundNewtonMeters=1e-10,
        guideWrenchComparedToLengthGradient=True,anatomicalCalibrationQualified=False,nativeUnityForceQualified=False,avatarStandingQualified=False,rows=results)
    with (package/'cylinder-reference-analysis.json').open('x',encoding='utf-8') as f:json.dump(report,f,indent=2,allow_nan=False)
    assert report['passed'],'Cylinder length/force/work criteria failed; retain original bounds and every case.'
    print('24 cases reconciled against 80/110-digit length derivatives. Native guide forces and knee anatomy remain unqualified.',flush=True)
    return report

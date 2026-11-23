"""Independent high-precision mapping of retained source attachment points.

Executed only by the visible study. This checks the declared similarity map;
it does not establish measured foot size, wrapping, strength or live delivery.
"""
from decimal import Decimal as D, localcontext
import hashlib
import json
import struct
import xml.etree.ElementTree as ET

POINT_BOUND_METERS = D('1e-12')
REFERENCE_BOUND_METERS = D('1e-60')

def read(p): return json.loads(p.read_text(encoding='utf-8-sig'))
def number(v): return D.from_float(v) if isinstance(v,float) else D(v)
def vec(obj): return tuple(number(obj[k]) for k in ('x','y','z'))
def textvec(obj): return tuple(number(float(x)) for x in obj['translationText'].split())
def add(a,b): return tuple(x+y for x,y in zip(a,b,strict=True))
def sub(a,b): return tuple(x-y for x,y in zip(a,b,strict=True))
def mul(a,v): return tuple(x*v for x in a)
def dot(a,b): return sum(x*y for x,y in zip(a,b,strict=True))
def norm(a): return dot(a,a).sqrt()
def unit(a): return mul(a,1/norm(a))
def cross(a,b): return (a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0])
def basis(proximal,distal,hint,parity):
    up=unit(sub(proximal,distal)); forward=unit(sub(hint,mul(up,dot(hint,up))))
    return forward,up,mul(cross(forward,up),D(parity))

def mapped(side,point,precision):
    with localcontext() as context:
        context.prec=precision
        sp=textvec(side['sourceKneeInTibia']); sd=textvec(side['sourceAnkleInTibia'])
        tp=vec(side['nativeShinGeometry']['anchorPosition']); td=vec(side['nativeFootGeometry']['parentAnchorPosition'])
        sb=basis(sp,sd,(D(1),D(0),D(0)),1); tb=basis(tp,td,(D(0),D(0),D(1)),-1)
        scale=norm(sub(tp,td))/norm(sub(sp,sd))
        p=tuple(number(x) for x in point['locationMeters'])
        if point['parentFrame']==side['sourceKneeInTibia']['parent']: source_origin,target_origin=sp,tp
        else:
            assert point['parentFrame']==side['sourceSubtalarInCalcaneus']['parent']
            source_origin=add(sub(textvec(side['sourceAnkleInTalus']),textvec(side['sourceSubtalarInTalus'])),textvec(side['sourceSubtalarInCalcaneus']))
            target_origin=vec(side['nativeFootGeometry']['anchorPosition'])
        coordinates=[dot(sub(p,source_origin),axis)*scale for axis in sb]
        value=tuple(target_origin[i]+sum(coordinates[j]*tb[j][i] for j in range(3)) for i in range(3))
        return value

def encoded(s):
    value=s.encode('utf-8'); length=len(value); prefix=bytearray()
    while length>=128: prefix.append((length&127)|128); length>>=7
    prefix.append(length); return bytes(prefix)+value

def path_digest(path):
    data=b''.join(encoded(path[k]) for k in ('Version','MuscleId','BodyGeometryFingerprint','Provenance'))
    data+=struct.pack('<i',len(path['Anchors']))
    for anchor in path['Anchors']:
        data+=encoded(anchor['AttachmentId'])+encoded(anchor['BodyId'])
        for axis in ('X','Y','Z'):
            value=anchor['LocalPositionMeters'][axis]; data+=struct.pack('<d',0.0 if value==0 else value)
    return hashlib.sha256(data).hexdigest().upper()

def compare(package):
    fixture=read(package/'anatomical-input/binding-input.json')
    ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}; rows=[]
    for output in ET.parse(package/'test-results/focused.trx').findall('.//t:UnitTestResult/t:Output/t:StdOut',ns):
        for line in (output.text or '').splitlines():
            if line.strip().startswith('ANATOMICAL_PATH_JSON '): rows.append(json.loads(line.strip().removeprefix('ANATOMICAL_PATH_JSON ')))
    originals={m['name']:(s,m) for s in fixture['sides'] for m in s['muscles']}
    assert len(rows)==len(originals)==10 and {r['path']['MuscleId'] for r in rows}==set(originals), 'All ten anatomical captures required'
    reports=[]; retained=[]
    for row in rows:
        path=row['path']; side,muscle=originals[path['MuscleId']]
        assert row['side']==side['side'] and len(path['Anchors'])==len(muscle['points'])
        assert path['BodyGeometryFingerprint']==fixture['originalNativeGeometrySha256']
        assert path['Provenance']==row['shinMap']['Fingerprint']+':'+row['footMap']['Fingerprint']
        # Version is a C# constant and is therefore not emitted as a property.
        assert 'Version' not in path
        assert path_digest(dict(path,Version='fixed-anchor-musculotendon-polyline-v1'))==path['Fingerprint']
        for name in ('shinMap','footMap'):
            mapping=row[name]
            assert mapping['SourceDefinitionSha256']==fixture['sourceSha256']
            assert mapping['TargetGeometrySha256']==fixture['originalNativeGeometrySha256']
            assert mapping['OrientationParity']==-1 and mapping['Scale']>0
        assert row['footMap']['Scale']==row['shinMap']['Scale']
        assert row['footMap']['ScaleProvenance']=='declared-shin-ratio-for-foot-not-measured-foot-scale'
        assert row['shinMap']['ScaleProvenance']=='paired-knee-ankle-landmark-ratio'
        for original,native in zip(muscle['points'],path['Anchors'],strict=True):
            assert native['AttachmentId']==original['name']
            shin=original['parentFrame']==side['sourceKneeInTibia']['parent']
            assert native['BodyId']==side['nativeShinGeometry' if shin else 'nativeFootGeometry']['id']
            a=mapped(side,original,80); b=mapped(side,original,110)
            candidate=tuple(number(native['LocalPositionMeters'][k]) for k in ('X','Y','Z'))
            agreement=max(abs(x-y) for x,y in zip(a,b,strict=True))
            error=max(abs(x-y) for x,y in zip(candidate,b,strict=True))
            reports.append(dict(muscle=muscle['name'],attachment=original['name'],body=native['BodyId'],
                sourceMeters=original['locationMeters'],candidateMeters=[float(x) for x in candidate],
                reference80Meters=[str(x) for x in a],reference110Meters=[str(x) for x in b],
                referenceAgreementMeters=str(agreement),maximumCoordinateErrorMeters=str(error),
                passed=agreement<=REFERENCE_BOUND_METERS and error<=POINT_BOUND_METERS))
        retained.append(row)
    assert len(reports)==44
    result=dict(completed=True,passed=all(r['passed'] for r in reports),muscles=10,points=44,
        originalPointOrderAndBodyIdentityPreserved=True,pathFingerprintsIndependentlyVerified=True,
        originalSourceSha256=fixture['sourceSha256'],originalBodySha256=fixture['originalNativeGeometrySha256'],
        pointAllowanceMeters=str(POINT_BOUND_METERS),referenceAllowanceMeters=str(REFERENCE_BOUND_METERS),
        referencePrecisionDigits=[80,110],footScaleIsDeclaredShinRatio=True,footDimensionsMeasured=False,
        wrappedMusclesImported=False,sourceMuscleDynamicsImported=False,nativeBodyExecuted=False,
        anatomicalCalibrationQualified=False,nativeMuscleDeliveryQualified=False,avatarStandingQualified=False,rows=reports)
    for name,value in [('frame-analysis.json',result),('mapped-lower-leg-paths.json',dict(version='declared-lower-leg-path-candidate-v1',
            runtimeSelected=False,anatomyQualified=False,footScaleIsDeclaredShinRatio=True,paths=retained))]:
        with (package/name).open('x',encoding='utf-8') as stream: json.dump(value,stream,indent=2,allow_nan=False); stream.write('\n')
    assert result['passed'], 'Declared coordinate mapping failed; preserve all points and original limits.'
    print('Ten named lower-leg paths and all 44 original attachment points reconciled at 80/110 digits. Foot scale is declared, not anatomically measured; no native body run.',flush=True)
    return result

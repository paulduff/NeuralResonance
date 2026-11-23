"""Copy protected source definitions and decode patellar data; no curve evaluation."""
import argparse
import importlib.util
import json
from pathlib import Path
import re
import shutil
import xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parents[1]
def module(name,path):
    s=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m
def decode(xml):
    raw=xml.read_bytes();assert b'<!DOCTYPE' not in raw and b'<!ENTITY' not in raw
    model=ET.fromstring(raw).find('Model');sides=[]
    def nums(text):return [float(s) for s in text.split()]
    for side in ('l','r'):
        name='patellofemoral_'+side;joint=model.find("JointSet/objects/CustomJoint[@name='"+name+"']");assert joint is not None
        frames={e.attrib['name']:e for e in joint.findall('frames/PhysicalOffsetFrame')}
        parent=frames[joint.findtext('socket_parent_frame')];child=frames[joint.findtext('socket_child_frame')]
        assert parent.findtext('socket_parent')=='/bodyset/femur_'+side and child.findtext('socket_parent')=='/bodyset/patella_'+side
        assert nums(parent.findtext('orientation'))==nums(child.findtext('orientation'))==nums(child.findtext('translation'))==[0,0,0]
        axes={e.attrib['name']:e for e in joint.findall('SpatialTransform/TransformAxis')};assert len(axes)==6
        curves={}
        for axis,key,vector in [('rotation1','angle',[0,0,1]),('translation1','x',[1,0,0]),('translation2','y',[0,1,0])]:
            a=axes[axis];assert nums(a.findtext('axis'))==vector and a.findtext('coordinates')=='knee_angle_'+side+'_beta'
            f=a.find('SimmSpline');assert f is not None
            curves[key]=dict(knots=nums(f.findtext('x')),values=nums(f.findtext('y')),originalKnots=f.findtext('x'),originalValues=f.findtext('y'))
        assert curves['angle']['knots']==curves['x']['knots']==curves['y']['knots'] and len(curves['angle']['knots'])==13
        for name in ('rotation2','rotation3'):assert float(axes[name].findtext('Constant/value'))==0 and not (axes[name].findtext('coordinates') or '').strip()
        assert nums(axes['translation3'].findtext('axis'))==[0,0,1] and not (axes['translation3'].findtext('coordinates') or '').strip()
        z=float(axes['translation3'].findtext('Constant/value'))
        c=model.find("ConstraintSet/objects/CoordinateCouplerConstraint[@name='patellofemoral_knee_angle_"+side+"_con']")
        assert c is not None and c.findtext('isEnforced')=='true' and c.findtext('independent_coordinate_names')=='knee_angle_'+side
        assert c.findtext('dependent_coordinate_name')=='knee_angle_'+side+'_beta' and float(c.findtext('scale_factor'))==1
        assert nums(c.findtext('coupled_coordinates_function/LinearFunction/coefficients'))==[1,0]
        knee=next(j for j in model.findall('JointSet/objects/*') if j.find("coordinates/Coordinate[@name='knee_angle_"+side+"']") is not None)
        coord=knee.find("coordinates/Coordinate[@name='knee_angle_"+side+"']");bounds=nums(coord.findtext('range'))
        assert coord.findtext('clamped')=='true' and bounds==[curves['angle']['knots'][0],curves['angle']['knots'][-1]]
        body=model.find("BodySet/objects/Body[@name='patella_"+side+"']");points=[]
        for muscle in model.findall('ForceSet/objects/*'):
            if muscle.attrib['name'] not in [n+'_'+side for n in ('recfem','vaslat','vasint','vasmed')]:continue
            for p in muscle.findall('GeometryPath/PathPointSet/objects/*'):
                if p.findtext('socket_parent_frame')=='/bodyset/patella_'+side:
                    assert p.tag=='PathPoint';points.append(dict(muscle=muscle.attrib['name'],id=p.attrib['name'],localMeters=nums(p.findtext('location')),originalLocation=p.findtext('location')))
        assert points and len({p['id'] for p in points})==len(points)
        sides.append(dict(side=side,coordinate='knee_angle_'+side,parent='femur_'+side,patella='patella_'+side,
            parentOffsetMeters=nums(parent.findtext('translation')),zTranslationMeters=z,curves=curves,points=points,
            supportedRange=bounds,sourceMassKg=float(body.findtext('mass')),sourceMassCenter=nums(body.findtext('mass_center')),sourceInertia=nums(body.findtext('inertia'))))
    return dict(version='retained-patellar-source-data-v1',sourceSha256='3F5C5F23E486073F2AD2AA4A4967FFE2FCDD582B1E355512BC54F70C36376BF4',
        identityCoordinateCoupling=True,sourceDynamicsImported=False,curvesEvaluated=False,sides=sides)
def capture(output):
    assert not output.exists();output.mkdir(parents=True)
    protect=module('patellar_source_preservation',ROOT/'tools/native_articulated_head_study.py').protected_copy
    for source,target in [('muscle-anatomy-source-20261123','anatomy'),('patellar-function-source-20261123','function')]:
        folder=ROOT/'artifacts'/source;manifest=json.loads((folder/'backup-manifest.json').read_text(encoding='utf-8-sig'))
        protect(folder,output/target,[e['Path'].replace('\\','/') for e in manifest['Files']])
    value=decode(output/'anatomy/Rajagopal2016.osim')
    with (output/'patellar-source.json').open('x',encoding='utf-8') as f:json.dump(value,f,indent=2,allow_nan=False);f.write('\n')
    source=(ROOT/'src/NRE.SimAvatar/AvatarArticulatedBody.cs').read_text(encoding='utf-8-sig')
    maximum=float(re.search(r'public const double MaximumKneeAngleRadians = ([\d.]+);',source)[1])
    shutil.copyfile(ROOT/'src/NRE.SimAvatar/AvatarArticulatedBody.cs',output/'AvatarArticulatedBody.cs')
    with (output/'capture.json').open('x',encoding='utf-8') as f:json.dump(dict(sourceSupportRadians=[0,2.0944],existingAvatarMaximumRadians=maximum,
        sourcePointsPerSide={s['side']:len(s['points']) for s in value['sides']},sourceMassAndInertiaRetained=True,
        curveOrModelEvaluated=False,testsExecuted=False,nativeMappingSelected=False),f,indent=2)
    print('Retained patellar knots, offsets, all quadriceps attachments, source mass/inertia and coordinate constraints. No curve or model evaluated.',flush=True)
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--output',type=Path,required=True);a=p.parse_args();capture(a.output.resolve())

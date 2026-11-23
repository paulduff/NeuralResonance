"""Audit saved anatomical topology only; no coordinates, forces or models evaluated."""
import argparse
from datetime import datetime,timezone
import hashlib
import importlib.util
import json
from pathlib import Path
import shutil
import subprocess
import xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parents[1]


def module(name,path):
    s=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m


def read(path):return json.loads(path.read_text(encoding='utf-8-sig'))


def write(path,value):
    with path.open('x',encoding='utf-8') as f:json.dump(value,f,indent=2,allow_nan=False);f.write('\n')


def crossed_joints(bodies,parents):
    def ancestors(body):
        result=[];seen=set()
        while body:
            assert body in parents and body not in seen,'Unknown or cyclic body tree.'
            seen.add(body);result.append(body);body=parents[body][0]
        return result
    found=set()
    for a,b in zip(bodies,bodies[1:]):
        left,right=ancestors(a),ancestors(b);common=next((v for v in left if v in right),None)
        assert common is not None,'Disconnected source bodies.'
        found.update(parents[v][1] for v in left[:left.index(common)]+right[:right.index(common)])
    return sorted(found)


def capture(output):
    assert not output.exists();output.mkdir(parents=True)
    helper=module('coverage_preservation',ROOT/'tools/native_articulated_head_study.py')
    source=module('coverage_source',ROOT/'tools/capture_muscle_anatomy_source.py')
    def copy(folder,target,names):helper.protected_copy(folder,output/target,names)
    copy(ROOT/'artifacts/muscle-anatomy-source-20261123','source', ['Rajagopal2016.osim','anatomy-catalog.json','provenance.json'])
    copy(ROOT/'artifacts/lower-leg-anatomy-input-20261123','binding',['binding-input.json'])
    copy(ROOT/'artifacts/muscle-frames-20261123-581e1f-review','mapping',['review.json','mapped-lower-leg-paths.json'])
    copy(ROOT/'artifacts/named-muscle-native-20261123-998900-review','native-review',['review.json'])
    proof=read(output/'native-review/review.json');assert proof['operatorReportedCompletion'] and proof['namedNativeForceAccountingPassed'] and proof['namedMuscles']==10
    catalog=read(output/'source/anatomy-catalog.json');raw=(output/'source/Rajagopal2016.osim').read_bytes()
    assert hashlib.sha256(raw).hexdigest().upper()==source.MODEL_SHA256==catalog['sourceSha256']
    assert b'<!DOCTYPE' not in raw and b'<!ENTITY' not in raw
    model=ET.fromstring(raw).find('Model');source_bodies={b.attrib['name'] for b in model.findall('BodySet/objects/*')}
    parents={'ground':('',None)};joints=[]
    for joint in model.findall('JointSet/objects/*'):
        frames={f.attrib['name']:f for f in joint.findall('frames/PhysicalOffsetFrame')}
        def body(socket):
            name=joint.findtext(socket)
            path=frames[name].findtext('socket_parent') if name in frames else name
            assert path=='/ground' or path.startswith('/bodyset/')
            result=path.split('/')[-1];assert result=='ground' or result in source_bodies
            return result
        parent,child=body('socket_parent_frame'),body('socket_child_frame');name=joint.attrib['name']
        assert child not in parents;parents[child]=(parent,name)
        joints.append(dict(name=name,type=joint.tag,parent=parent,child=child,
            coordinates=[c.attrib['name'] for c in joint.findall('coordinates/Coordinate')],
            transformFunctionTypes=sorted({c.tag for c in joint.iter() if 'Function' in c.tag or 'Spline' in c.tag}),
            originalDefinition=source.node(joint)))
    assert set(parents)==source_bodies|{'ground'}
    binding=read(output/'binding/binding-input.json');endpoint=json.loads(binding['originalNativeEndpointJson']);geometry=json.loads(endpoint['geometryJson'])
    assert hashlib.sha256(endpoint['geometryJson'].encode()).hexdigest().upper()==endpoint['bodyFingerprint']
    links={b['id']:b for b in geometry['links']};native_parents={k:(v['parent'],k) for k,v in links.items()}
    mapped=read(output/'mapping/mapped-lower-leg-paths.json')['paths'];mapped_by_name={r['path']['MuscleId']:r['path'] for r in mapped}
    assert len(mapped_by_name)==10 and sum(len(p['Anchors']) for p in mapped_by_name.values())==44
    current=[]
    for name,path in sorted(mapped_by_name.items()):
        bodies=[p['BodyId'] for p in path['Anchors']]
        current.append(dict(muscle=name,nativeAttachmentBodies=bodies,
            nativeCrossedChildJoints=crossed_joints(bodies,native_parents),originalAttachments=len(bodies),pathFingerprint=path['Fingerprint']))
    muscles=[]
    for m in catalog['muscles']:
        bodies=[p['parentFrame'].split('/')[-1] for p in m['points']]
        muscles.append(dict(name=m['name'],sourceAttachmentBodies=bodies,
            sourceAnchorTreeJointCrossings=crossed_joints(bodies,parents),
            mappedToCurrentNativePaths=m['name'] in mapped_by_name,requiresSourceWrapping=m['requiresWrapping'],
            sourcePatellaAttachments=sorted({b for b in bodies if b.startswith('patella_')}),
            originalPathPointCount=len(m['points']),originalWrapRouting=m['wrapping']))
    by_name={m['name']:m for m in muscles};groups=[]
    for g in catalog['anatomicalNameAssociations']:
        chosen=[by_name[n] for n in g['sourceMuscles']]
        groups.append(dict(group=g['avatarGroup'],side=g['side'],sourceMuscles=g['sourceMuscles'],
            mapped=[m['name'] for m in chosen if m['mappedToCurrentNativePaths']],
            wrappingRequired=[m['name'] for m in chosen if m['requiresSourceWrapping']],
            patellaRequired=[m['name'] for m in chosen if m['sourcePatellaAttachments']]))
    native_crossed=sorted({j for c in current for j in c['nativeCrossedChildJoints']})
    assert native_crossed==['left_foot','right_foot']
    quadriceps=[g for g in groups if g['group']=='Quadriceps'];assert len(quadriceps)==2
    assert all(len(g['sourceMuscles'])==len(g['wrappingRequired'])==len(g['patellaRequired'])==4 and not g['mapped'] for g in quadriceps)
    constraints=[source.node(c) for c in model.findall('ConstraintSet/objects/*')]
    assert len(constraints)==2 and all(c['type']=='CoordinateCouplerConstraint' for c in constraints)
    report=dict(version='saved-standing-muscle-topology-coverage-v1',createdUtc=datetime.now(timezone.utc).isoformat(),
        repositoryRevision=subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip(),
        sourceRevision=source.REVISION,sourceSha256=source.MODEL_SHA256,originalNativeGeometrySha256=endpoint['bodyFingerprint'],
        sourceMuscles=len(muscles),sourceBodies=len(source_bodies),nativeBodies=len(links),mappedMuscles=10,mappedAttachments=44,
        nativeCrossedChildJoints=native_crossed,nativeHipOrKneeCrossings=False,nativePatellaBodies=[],
        sourceMuscleInventory=muscles,currentNativePaths=current,anatomicalNameGroups=groups,
        sourceJointDefinitions=joints,sourceCoordinateConstraints=constraints,
        interpretation='Body-tree connectivity only; source wrapping and coupled joints can add pose-dependent effects. No moment-arm sign, strength or actuation rank inferred.',
        fixedPointPathCapability='Current AvatarMusclePath retains fixed material attachment/via-point polylines; patella coupling and source wrap routing are not selected by this catalog.',
        geometryEvaluated=False,momentArmsEvaluated=False,forceCapacityEstimated=False,modelsExecuted=False,testsExecuted=False,
        runtimeSelected=False,biologicalParametersChanged=False,avatarStandingQualified=False)
    write(output/'coverage.json',report);shutil.copyfile(__file__,output/Path(__file__).name)
    print(json.dumps(dict(output=str(output),sourceMuscles=len(muscles),mappedMuscles=10,nativeCrossedChildJoints=native_crossed,
        unmappedQuadricepsWithWrappingAndPatella=8,modelsExecuted=False,testsExecuted=False),indent=2),flush=True)


if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions required.')
    parser=argparse.ArgumentParser();parser.add_argument('output',type=Path);capture(parser.parse_args().output.resolve())

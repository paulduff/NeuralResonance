"""Decode pinned anatomical source data only; never execute or install a model.

OpenSim coordinate frames, path wrapping and constitutive conventions remain
explicit. This is reference input for Avatar's anatomical work, not a runtime
conversion or a source of activation commands.
"""
import argparse
from collections import Counter
import hashlib
import json
import math
from pathlib import Path
import xml.etree.ElementTree as ET

REVISION = 'e0cee6cbcf56f74731e841668ef668556d140ce9'
MODEL_SHA256 = '3F5C5F23E486073F2AD2AA4A4967FFE2FCDD582B1E355512BC54F70C36376BF4'
MODEL_BLOB = 'f0bbab84ebbd652fece7c36928e916618f0b9fff'
MODEL_URL = f'https://github.com/opensim-org/opensim-models/blob/{REVISION}/Models/Rajagopal/Rajagopal2016.osim'

# Names associate source muscles with existing Avatar groups for review only.
# Multiarticular muscles remain single source muscles: no duplicated actuator,
# summed force capacity, averaged length or new attachment is manufactured.
GROUPS = {
    'Iliopsoas': ['iliacus', 'psoas'],
    'GluteusMaximus': ['glmax1', 'glmax2', 'glmax3'],
    'GluteusMedius': ['glmed1', 'glmed2', 'glmed3'],
    'AdductorGroup': ['addbrev', 'addlong', 'addmagDist', 'addmagIsch', 'addmagMid', 'addmagProx'],
    'Hamstrings': ['bflh', 'bfsh', 'semimem', 'semiten'],
    'Quadriceps': ['recfem', 'vasint', 'vaslat', 'vasmed'],
    'TibialisAnterior': ['tibant'],
    'GastrocnemiusSoleus': ['gaslat', 'gasmed', 'soleus'],
    'TibialisPosterior': ['tibpost'],
    'FibularisLongusBrevis': ['perlong', 'perbrev'],
}


def node(element):
    """Retain exact scalar text and XML hierarchy, without filling defaults."""
    return dict(type=element.tag, attributes=dict(element.attrib),
                text=(element.text or '').strip(), children=[node(c) for c in element])


def number(element, name):
    literal = element.findtext(name)
    if literal is None: raise ValueError('Missing explicit source property: '+name)
    value = float(literal)
    if not math.isfinite(value): raise ValueError('Nonfinite source property: '+name)
    return dict(literal=literal.strip(), value=value)


def capture(package):
    original = package/'Rajagopal2016.osim'
    data = original.read_bytes()
    digest = hashlib.sha256(data).hexdigest().upper()
    blob = hashlib.sha1(b'blob '+str(len(data)).encode()+b'\0'+data).hexdigest()
    if digest != MODEL_SHA256 or blob != MODEL_BLOB: raise ValueError('Pinned source bytes changed.')
    if b'<!DOCTYPE' in data or b'<!ENTITY' in data: raise ValueError('External XML declarations are not source data.')
    root = ET.fromstring(data); model = root.find('Model')
    if root.attrib != {'Version': '40000'} or model is None or model.attrib != {'name': 'Rajagopal2016'}:
        raise ValueError('Unexpected pinned source identity.')
    if model.findtext('length_units') != 'meters' or model.findtext('force_units') != 'N':
        raise ValueError('Do not silently convert source units.')
    bodies = list(model.find('BodySet/objects'))
    body_names = [b.attrib['name'] for b in bodies]
    if len(body_names) != len(set(body_names)): raise ValueError('Duplicate source body identity.')
    wraps = []
    for body in bodies:
        for wrap in body.findall('WrapObjectSet/objects/*'):
            wraps.append(dict(body=body.attrib['name'], definition=node(wrap)))
    wrap_names = [w['definition']['attributes']['name'] for w in wraps]
    if len(wrap_names) != len(set(wrap_names)): raise ValueError('Ambiguous wrap identity.')
    muscles = []; actuators = []
    for force in model.find('ForceSet/objects'):
        if force.tag == 'CoordinateActuator':
            actuators.append(node(force)); continue
        if force.tag != 'Millard2012EquilibriumMuscle':
            raise ValueError('Unreviewed force class: '+force.tag)
        points = []
        for point in force.findall('GeometryPath/PathPointSet/objects/*'):
            if point.tag != 'PathPoint': raise ValueError('Do not flatten a conditional or moving path point.')
            parent = point.findtext('socket_parent_frame')
            if parent not in {'/bodyset/'+name for name in body_names}: raise ValueError('Unresolved source path frame.')
            location = point.findtext('location'); values = [float(x) for x in location.split()]
            if len(values) != 3 or not all(math.isfinite(v) for v in values): raise ValueError('Invalid source point.')
            points.append(dict(name=point.attrib['name'], parentFrame=parent,
                               locationMeters=values, originalLocationText=location.strip()))
        routing = [node(w) for w in force.findall('GeometryPath/PathWrapSet/objects/*')]
        for wrap in force.findall('GeometryPath/PathWrapSet/objects/*'):
            if wrap.tag != 'PathWrap' or wrap.findtext('wrap_object') not in wrap_names:
                raise ValueError('Unresolved source wrapping surface.')
        parameters = {name: number(force, name) for name in ('max_isometric_force', 'optimal_fiber_length',
            'tendon_slack_length', 'pennation_angle_at_optimal', 'fiber_damping', 'minimum_activation', 'default_activation')}
        muscles.append(dict(name=force.attrib['name'], sourceType=force.tag, parameters=parameters,
            points=points, wrapping=routing, requiresWrapping=bool(routing),
            originalDefinition=node(force), coordinateConversionApplied=False, avatarBindingQualified=False))
    names = [m['name'] for m in muscles]
    if len(names) != len(set(names)): raise ValueError('Duplicate source muscle identity.')
    groups = []
    for side in ('l', 'r'):
        for group, stems in GROUPS.items():
            selected = [stem+'_'+side for stem in stems]
            if not set(selected) <= set(names): raise ValueError('Missing source muscle in named association.')
            groups.append(dict(avatarGroup=group, side=side, sourceMuscles=selected,
                               relationship='anatomical names only; no aggregation or runtime motor mapping'))
    coordinates = model.findall('JointSet/objects/*/coordinates/Coordinate')
    report = dict(version='pinned-muscle-anatomy-catalog-v1', sourceUrl=MODEL_URL, sourceRevision=REVISION,
        sourceSha256=digest, sourceGitBlob=blob, sourceBytes=len(data),
        credits=model.findtext('credits').strip(), publications=model.findtext('publications').strip(),
        lengthUnits='meters', forceUnits='N', numericPolicy='original decimal text plus binary64 decoding; no inferred defaults',
        muscles=muscles, sourceBodies=[node(b) for b in bodies], sourceJoints=node(model.find('JointSet')),
        sourceConstraints=node(model.find('ConstraintSet')), sourceDefaults=node(model.find('defaults')),
        wrappingSurfaces=wraps, excludedCoordinateActuators=actuators, anatomicalNameAssociations=groups,
        inventory=dict(muscles=len(muscles), sourceBodies=len(bodies), serializedCoordinates=len(coordinates),
            pathPoints=sum(len(m['points']) for m in muscles), wrappingSurfaces=len(wraps),
            wrappedMuscles=sum(m['requiresWrapping'] for m in muscles),
            pathPointTypes=dict(Counter(p.tag for m in model.findall('ForceSet/objects/Millard2012EquilibriumMuscle')
                                       for p in m.findall('GeometryPath/PathPointSet/objects/*'))),
            excludedCoordinateActuators=len(actuators)),
        modelsExecuted=False, testsExecuted=False, runtimeSelected=False, avatarAnatomyQualified=False,
        muscleDynamicsImported=False, activationFloorImported=False, torqueControllersImported=False)
    target = package/'anatomy-catalog.json'
    with target.open('x', encoding='utf-8') as stream: json.dump(report, stream, indent=2, allow_nan=False); stream.write('\n')
    print(json.dumps(dict(output=str(target), inventory=report['inventory'], modelsExecuted=False,
                         testsExecuted=False, runtimeSelected=False), indent=2), flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('package', type=Path)
    args = parser.parse_args()
    capture(args.package.resolve())

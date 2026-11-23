"""Preserve paired source/native lower-leg landmarks; no mapping/model execution."""
import argparse
from datetime import datetime, timezone
import hashlib
import importlib.util
import json
from pathlib import Path
import shutil
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT/'tools'))
from capture_muscle_anatomy_source import MODEL_SHA256, MODEL_URL, node
from native_articulated_head_study import protected_copy

SOURCE = ROOT/'artifacts/muscle-anatomy-source-20261123'
NATIVE = Path('D:/DNNE-build-work/declared-body-mechanics-20261123-203560')
SELECTED = ('tibant', 'soleus', 'tibpost', 'perlong', 'perbrev')


def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest().upper()
def read(path): return json.loads(path.read_text(encoding='utf-8-sig'))


def frame(joint, socket):
    name = joint.findtext(socket)
    found = [f for f in joint.findall('frames/PhysicalOffsetFrame') if f.attrib['name'] == name]
    assert len(found) == 1
    f = found[0]
    return dict(name=name, parent=f.findtext('socket_parent'),
        translationText=f.findtext('translation').strip(), orientationText=f.findtext('orientation').strip(),
        originalDefinition=node(f))


def capture(output):
    assert not output.exists(); output.mkdir(parents=True)
    protected_copy(SOURCE, output/'source-evidence', ['Rajagopal2016.osim', 'anatomy-catalog.json', 'provenance.json'])
    protected_copy(NATIVE, output/'native-evidence', ['conditions/no-joint-friction/native/quiet/acknowledged.jsonl', 'configuration.json'])
    review = ROOT/'artifacts/declared-body-mechanics-20261123-203560-review'
    protected_copy(review, output/'native-review', ['review.json'])
    proof = read(output/'native-review/review.json')
    assert proof['operatorReportedCompletion'] and proof['exactOriginalQuietReplays'] and proof['passedComponentCases'] == 341
    original = output/'source-evidence/Rajagopal2016.osim'; assert sha(original) == MODEL_SHA256
    data = original.read_bytes(); assert b'<!DOCTYPE' not in data and b'<!ENTITY' not in data
    model = ET.fromstring(data).find('Model')
    joints = {j.attrib['name']:j for j in model.find('JointSet/objects')}
    muscles = {m['name']:m for m in read(output/'source-evidence/anatomy-catalog.json')['muscles']}
    native_path = output/'native-evidence/conditions/no-joint-friction/native/quiet/acknowledged.jsonl'
    with native_path.open(encoding='utf-8-sig') as stream: original_endpoint_json = next(stream).rstrip('\r\n')
    endpoint = json.loads(original_endpoint_json); geometry = json.loads(endpoint['geometryJson']); state = json.loads(endpoint['stateJson'])
    assert endpoint['physicsStep'] == 0 and endpoint['paused'] and not endpoint['connected'] and endpoint['isolated']
    assert hashlib.sha256(endpoint['geometryJson'].encode()).hexdigest().upper() == endpoint['bodyFingerprint']
    assert hashlib.sha256(endpoint['stateJson'].encode()).hexdigest().upper() == endpoint['stateHash']
    assert geometry['version'] == 'declared-full-avatar-tendon-paths-v2'
    links = {r['id']:r for r in geometry['links']}; poses = {r['id']:r for r in state['links']}
    sides = []
    for side, label in [('l','left'), ('r','right')]:
        knee, ankle, subtalar = [joints[prefix+side] for prefix in ('walker_knee_', 'ankle_', 'subtalar_')]
        assert ankle.tag == subtalar.tag == 'PinJoint'
        coordinates = []
        for joint in (knee, ankle, subtalar):
            for coordinate in joint.findall('coordinates/Coordinate'):
                assert coordinate.findtext('default_value').strip() == '0'
                coordinates.append(node(coordinate))
        assert subtalar.findtext('coordinates/Coordinate/locked').strip() == 'true'
        kp = frame(knee, 'socket_child_frame'); ap = frame(ankle, 'socket_parent_frame'); ac = frame(ankle, 'socket_child_frame')
        sp = frame(subtalar, 'socket_parent_frame'); sc = frame(subtalar, 'socket_child_frame')
        assert kp['parent'] == ap['parent'] == '/bodyset/tibia_'+side
        assert ac['parent'] == sp['parent'] == '/bodyset/talus_'+side and sc['parent'] == '/bodyset/calcn_'+side
        assert ap['orientationText'] == ac['orientationText'] and sp['orientationText'] == sc['orientationText']
        shin, foot = links[label+'_shin'], links[label+'_foot']
        assert foot['parent'] == shin['id'] and shin['parent'] == label+'_thigh'
        selected = [muscles[name+'_'+side] for name in SELECTED]
        for muscle in selected:
            assert not muscle['requiresWrapping'] and not muscle['wrapping']
            assert {p['parentFrame'] for p in muscle['points']} == {'/bodyset/tibia_'+side, '/bodyset/calcn_'+side}
        sides.append(dict(side=side, targetSide=label, sourceKneeInTibia=kp, sourceAnkleInTibia=ap,
            sourceAnkleInTalus=ac, sourceSubtalarInTalus=sp, sourceSubtalarInCalcaneus=sc,
            originalSourceCoordinates=coordinates, nativeShinGeometry=shin, nativeFootGeometry=foot,
            nativeShinInitialPose=poses[shin['id']], nativeFootInitialPose=poses[foot['id']], muscles=selected))
    report = dict(version='paired-lower-leg-anatomical-input-v1', createdUtc=datetime.now(timezone.utc).isoformat(),
        sourceUrl=MODEL_URL, sourceSha256=MODEL_SHA256, originalNativeGeometrySha256=endpoint['bodyFingerprint'],
        originalNativeStateSha256=endpoint['stateHash'], originalNativeEndpointJson=original_endpoint_json,
        sides=sides, selectedMuscles=10, selectedPathPoints=sum(len(m['points']) for s in sides for m in s['muscles']),
        sourceAxisReference='https://opensimconfluence.atlassian.net/wiki/spaces/OpenSim/pages/53089978/Coordinate+Systems',
        sourceAxisDeclaration=dict(anterior=[1,0,0], superior=[0,1,0], right=[0,0,1]),
        nativeBindAxisDeclaration=dict(anterior=[0,0,1], superior=[0,1,0], right=[1,0,0]),
        proposedShinMapping='Knee/ankle material landmarks, anterior hint and explicit handedness; uniform measured segment-length ratio.',
        proposedFootMapping='Preserve neutral talus/calcaneus offset; native ankle origin and body-local axes. Foot scale requires an explicit declaration; no foot-size calibration claimed.',
        sourceJointReduction='Subtalar locked neutral in source; Avatar combined ankle/roll is a reduced mechanical model, not the source joint anatomy.',
        excludedWrappedMusclesRetained=True, gastrocnemiusReplacedBySoleus=False,
        sourceNumericValuesChanged=False, sourceDefaultsInferred=False, mappedCoordinatesProduced=False,
        muscleDynamicsImported=False, runtimeSelected=False, modelsExecuted=False, testsExecuted=False,
        anatomyQualified=False, nativeForceDeliveryQualified=False, avatarStandingQualified=False)
    with (output/'binding-input.json').open('x', encoding='utf-8') as stream:
        json.dump(report, stream, indent=2, allow_nan=False); stream.write('\n')
    shutil.copyfile(__file__, output/Path(__file__).name)
    print(json.dumps(dict(output=str(output), selectedMuscles=10, selectedPathPoints=report['selectedPathPoints'],
                         mappedCoordinatesProduced=False, modelsExecuted=False, testsExecuted=False), indent=2), flush=True)


if __name__ == '__main__':
    p = argparse.ArgumentParser(); p.add_argument('output', type=Path); args = p.parse_args()
    if not __debug__: raise RuntimeError('Assertions must remain enabled.')
    capture(args.output.resolve())

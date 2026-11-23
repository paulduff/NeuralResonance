"""Positive replay and corruption controls for saved geometry identity only."""
import copy
import json
from pathlib import Path
import unittest
from native_geometry_identity import CrossSessionGeometry, geometry_identity, sha, text_bytes

CAPTURE = Path('D:/DNNE-build-work/native-canal-20261123-184323')


def compact(value):
    return json.dumps(value, separators=(',', ':'), ensure_ascii=False)


def refresh(endpoint):
    _, _, body, paths = geometry_identity(endpoint['geometryJson'])
    endpoint['bodyFingerprint'] = body
    for p in endpoint['paths']:
        p['pathFingerprint'] = paths[p['muscleId']]


def change_scene(endpoint, mutation):
    geometry = json.loads(endpoint['geometryJson'])
    scene = json.loads(geometry['sceneMechanicsJson']); mutation(scene)
    geometry['sceneMechanicsJson'] = compact(scene)
    endpoint['geometryJson'] = compact(geometry)
    refresh(endpoint)


class NativeGeometryIdentityControls(unittest.TestCase):
    def setUp(self):
        def first(path):
            with path.open(encoding='utf-8-sig') as stream:
                return json.loads(next(stream))
        self.actual = first(CAPTURE/'native/quiet-n8192/acknowledged.jsonl')
        self.original = first(CAPTURE/'native-evidence/conditions/no-joint-friction/native/quiet/acknowledged.jsonl')
        self.a = copy.deepcopy(self.original)
        change_scene(self.a, lambda scene: None)
        self.b = copy.deepcopy(self.a)

    def rejected_scene(self, mutation):
        with self.assertRaises(AssertionError):
            change_scene(self.a, mutation)
            CrossSessionGeometry(self.a, self.b)

    def test_saved_failure_and_derived_path_hashes(self):
        self.assertNotEqual(self.actual['geometryJson'], self.original['geometryJson'])
        self.assertNotEqual(self.actual['paths'], self.original['paths'])
        report = CrossSessionGeometry(self.actual, self.original).report()
        self.assertEqual(report['independentlyVerifiedPathsPerSession'], 38)
        self.assertEqual(sum(r['current'] != r['original'] for r in report['nativeInstanceMapping']), 30)
        self.assertFalse(report['originalRawGeometryIdentical'])

    def test_identical_session(self):
        self.assertTrue(CrossSessionGeometry(self.original, self.original).report()['originalRawGeometryIdentical'])

    def test_valid_process_renumbering(self):
        def renumber(scene):
            for collection in ('shapes', 'bodies'):
                for row in scene[collection]:
                    row['instance'] += 100000
        change_scene(self.a, renumber)
        CrossSessionGeometry(self.a, self.b)

    def test_duplicate_object_identity_rejected(self):
        self.rejected_scene(lambda s: s['bodies'][0].update(instance=s['shapes'][0]['instance']))

    def test_duplicate_name_rejected(self):
        self.rejected_scene(lambda s: s['shapes'][1].update(name=s['shapes'][0]['name']))

    def test_zero_identity_rejected(self):
        self.rejected_scene(lambda s: s['bodies'][0].update(instance=0))

    def test_noninteger_identity_rejected(self):
        self.rejected_scene(lambda s: s['bodies'][0].update(instance=1.5))

    def test_new_field_rejected(self):
        self.rejected_scene(lambda s: s['bodies'][0].update(unreviewed=1))

    def test_missing_field_rejected(self):
        self.rejected_scene(lambda s: s['shapes'][0].pop('material'))

    def test_body_mass_rejected(self):
        self.rejected_scene(lambda s: s['bodies'][0]['parameters'].__setitem__(0, '3FF0000000000000'))

    def test_friction_rejected(self):
        self.rejected_scene(lambda s: s['bodies'][0]['parameters'].__setitem__(3, '3FF0000000000000'))

    def test_shape_dimension_rejected(self):
        self.rejected_scene(lambda s: s['shapes'][0]['dimensions'].__setitem__(0, '3FF0000000000000'))

    def test_material_rejected(self):
        self.rejected_scene(lambda s: s['shapes'][0]['material'].__setitem__(0, 'changed-material'))

    def test_layer_filter_rejected(self):
        self.rejected_scene(lambda s: s['layerIgnored'].__setitem__(0, not s['layerIgnored'][0]))

    def test_collision_pair_rejected(self):
        self.rejected_scene(lambda s: s['ignoredPairs'].__setitem__(0, not s['ignoredPairs'][0]))

    def test_attachment_rejected(self):
        g = json.loads(self.a['geometryJson']); d = json.loads(g['declarationsJson'])
        d['paths'][0]['anchors'][0]['xBits'] = '3FF0000000000000'
        g['declarationsJson'] = compact(d); self.a['geometryJson'] = compact(g); refresh(self.a)
        with self.assertRaises(AssertionError): CrossSessionGeometry(self.a, self.b)

    def test_wrong_body_hash_rejected(self):
        self.a['bodyFingerprint'] = '0'*64
        with self.assertRaises(AssertionError): CrossSessionGeometry(self.a, self.b)

    def test_wrong_path_hash_rejected(self):
        self.a['paths'][0]['pathFingerprint'] = '0'*64
        with self.assertRaises(AssertionError): CrossSessionGeometry(self.a, self.b)

    def test_path_measurement_rejected(self):
        self.a['paths'][0]['lengthMeters']['bits'] = '3FF0000000000000'
        with self.assertRaises(AssertionError): CrossSessionGeometry(self.a, self.b)

    def test_state_rejected(self):
        self.a['stateJson'] += ' '
        with self.assertRaises(AssertionError): CrossSessionGeometry(self.a, self.b)

    def test_force_rejected(self):
        self.a['applicationsJson'] += ' '
        with self.assertRaises(AssertionError): CrossSessionGeometry(self.a, self.b)

    def test_path_reorder_rejected(self):
        self.a['paths'].reverse()
        with self.assertRaises(AssertionError): CrossSessionGeometry(self.a, self.b)

    def test_identity_change_within_session_rejected(self):
        identity = CrossSessionGeometry(self.a, self.b)
        change_scene(self.a, lambda s: s['bodies'][0].update(instance=99999))
        with self.assertRaises(AssertionError): identity.check(self.a, self.b)

    def test_duplicate_json_key_rejected(self):
        self.a['geometryJson'] = self.a['geometryJson'].replace('{', '{"unity":"duplicate",', 1)
        with self.assertRaises(AssertionError): CrossSessionGeometry(self.a, self.b)

    def test_utf8_length_prefix(self):
        self.assertEqual(text_bytes('é'*65), b'\x82\x01'+('é'*65).encode('utf-8'))


if __name__ == '__main__': unittest.main()

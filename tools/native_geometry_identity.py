"""Saved cross-session geometry identity. Never changes live protocol identity.

Only ArticulatedMechanicsIdentity's two explicit instance-ID fields may vary.
All remaining raw geometry text, path measurements and physical states stay
exact. Each original body/path digest is independently reconstructed first.
"""
import hashlib
import json
import math
import re
import struct


def sha(text):
    return hashlib.sha256(text.encode('utf-8')).hexdigest().upper()


def unique(pairs):
    result = {}
    for key, value in pairs:
        assert key not in result, ('Duplicate JSON property', key)
        result[key] = value
    return result


def decode(text):
    def invalid(value):
        raise AssertionError(('Nonfinite JSON', value))
    return json.loads(text, object_pairs_hook=unique, parse_constant=invalid)


def text_bytes(value):
    data = value.encode('utf-8'); size = len(data); prefix = bytearray()
    while size >= 128:
        prefix.append((size & 127) | 128); size >>= 7
    prefix.append(size)
    return bytes(prefix)+data


def path_digest(declaration, body_hash):
    assert set(declaration) == {'id', 'provenance', 'anchors'}
    anchors = declaration['anchors']
    assert len(anchors) >= 2 and len({a['id'] for a in anchors}) == len(anchors)
    data = b''.join(text_bytes(s) for s in ('fixed-anchor-musculotendon-polyline-v1',
        declaration['id'], body_hash, declaration['provenance'])) + struct.pack('<i', len(anchors))
    for anchor in anchors:
        assert set(anchor) == {'id', 'bodyId', 'xBits', 'yBits', 'zBits'}
        data += text_bytes(anchor['id'])+text_bytes(anchor['bodyId'])
        for axis in ('xBits', 'yBits', 'zBits'):
            bits = anchor[axis]; assert re.fullmatch('[0-9A-F]{16}', bits)
            value = struct.unpack('>d', bytes.fromhex(bits))[0]
            assert math.isfinite(value)
            data += struct.pack('<d', 0. if value == 0 else value)
    return hashlib.sha256(data).hexdigest().upper()


def geometry_identity(raw):
    geometry = decode(raw)
    assert set(geometry) == {'version', 'unity', 'declarationsJson', 'sceneMechanicsJson',
                             'stepSeconds', 'gravity', 'links'}
    assert geometry['version'] == 'declared-full-avatar-tendon-paths-v2'
    scene_raw = geometry['sceneMechanicsJson']; scene = decode(scene_raw)
    assert set(scene) == {'physics', 'layerIgnored', 'shapes', 'ignoredPairs', 'bodies'}
    objects = []
    schemas = {'shapes': {'name', 'kind', 'body', 'instance', 'layer', 'priority', 'include',
                         'exclude', 'enabled', 'active', 'trigger', 'dimensions', 'material',
                         'transforms', 'contactOffset'},
               'bodies': {'name', 'instance', 'parameters'}}
    for collection, keys in schemas.items():
        records = scene[collection]
        assert records and len({r['name'] for r in records}) == len(records)
        for record in records:
            assert set(record) == keys, (collection, 'Unknown/missing fields')
            identity = record['instance']
            assert type(identity) is int and -(2**31) <= identity < 2**31 and identity != 0
            objects.append(dict(collection=collection, name=record['name'], instance=identity))
    assert len({r['instance'] for r in objects}) == len(objects), 'Aliased native objects'
    # Raw-token substitution is deliberately narrower than object normalization:
    # spelling, order, all scalar bits and every other character must still match.
    pattern = r'"instance":(-?\d+)(?=[,}])'
    tokens = [int(m.group(1)) for m in re.finditer(pattern, scene_raw)]
    assert tokens == [r['instance'] for r in objects], 'Unexpected instance field location'
    canonical_scene = re.sub(pattern, '"instance":0', scene_raw)
    quoted = json.dumps(scene_raw, ensure_ascii=False)
    assert raw.count(quoted) == 1, 'Unexpected nested scene encoding'
    canonical = raw.replace(quoted, json.dumps(canonical_scene, ensure_ascii=False), 1)
    declarations = decode(geometry['declarationsJson'])
    assert set(declarations) == {'paths'}
    paths = declarations['paths']
    assert paths and len({p['id'] for p in paths}) == len(paths)
    body_hash = sha(raw)
    hashes = {p['id']: path_digest(p, body_hash) for p in paths}
    return canonical, objects, body_hash, hashes


class CrossSessionGeometry:
    def __init__(self, current, original):
        a = geometry_identity(current['geometryJson']); b = geometry_identity(original['geometryJson'])
        assert a[0] == b[0], 'Mechanical geometry differs beyond process-local instance IDs'
        assert [(r['collection'], r['name']) for r in a[1]] == [(r['collection'], r['name']) for r in b[1]]
        self.current_raw = current['geometryJson']; self.original_raw = original['geometryJson']
        self.current_hash, self.current_paths = a[2:]; self.original_hash, self.original_paths = b[2:]
        self.mapping = [dict(collection=x['collection'], name=x['name'], current=x['instance'], original=y['instance'])
                        for x, y in zip(a[1], b[1], strict=True)]
        self.check(current, original)

    def check(self, current, original):
        for endpoint, raw, body_hash, hashes in (
                (current, self.current_raw, self.current_hash, self.current_paths),
                (original, self.original_raw, self.original_hash, self.original_paths)):
            assert endpoint['geometryJson'] == raw and endpoint['bodyFingerprint'] == body_hash
            paths = endpoint['paths']
            assert [p['muscleId'] for p in paths] == list(hashes), 'Missing/reordered paths'
            for path in paths:
                assert set(path) == {'muscleId', 'pathFingerprint', 'lengthMeters', 'velocityMetersPerSecond'}
                assert path['pathFingerprint'] == hashes[path['muscleId']], 'Path digest is not derived from its own original geometry'
        for a, b in zip(current['paths'], original['paths'], strict=True):
            assert all(a[k] == b[k] for k in ('muscleId', 'lengthMeters', 'velocityMetersPerSecond')), 'Path measurement changed'
        assert current['stateJson'] == original['stateJson'], 'Original physical state changed'
        assert current['applicationsJson'] == original['applicationsJson'], 'Original point forces changed'

    def report(self):
        return dict(mechanicalGeometryExactApartFromInstanceIds=True,
                    currentBodySha256=self.current_hash, originalBodySha256=self.original_hash,
                    originalRawGeometryIdentical=self.current_raw == self.original_raw,
                    nativeInstanceMapping=self.mapping, independentlyVerifiedPathsPerSession=len(self.current_paths),
                    liveIdentityRulesChanged=False, numericalToleranceIntroduced=False)

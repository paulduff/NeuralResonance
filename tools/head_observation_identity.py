"""Compare the two declared .NET JSON spellings without changing any values."""
import json
import math

SCALARS = ('PhysicsStep', 'SampleId', 'HeadBodyId', 'SensorId')
VECTORS = ('LocalPointMeters', 'OriginWorldMeters', 'RightWorld', 'UpWorld', 'ForwardWorld',
    'PointWorldMeters', 'PointVelocityWorldMetersPerSecond', 'AngularVelocityWorldRadiansPerSecond',
    'GravityWorldMetersPerSecondSquared')


def original_observation(raw_json, expected):
    def unique(pairs):
        result = {}
        for key, value in pairs:
            if key in result: raise ValueError('Duplicate original observation property: '+key)
            result[key] = value
        return result
    actual = json.loads(raw_json, object_pairs_hook=unique)
    camel = lambda name: name[0].lower()+name[1:]
    if not isinstance(actual, dict) or set(actual) != set(SCALARS+VECTORS):
        raise ValueError('Original observation must retain its complete PascalCase schema.')
    if not isinstance(expected, dict) or set(expected) != {camel(n) for n in SCALARS+VECTORS}:
        raise ValueError('Captured head must retain its complete camelCase schema.')
    for name in SCALARS:
        value, other = actual[name], expected[camel(name)]
        kind = int if name == 'PhysicsStep' else str
        if type(value) is not kind or type(other) is not kind or value != other:
            raise ValueError('Original observation identity changed: '+name)
    for name in VECTORS:
        value, other = actual[name], expected[camel(name)]
        if not isinstance(value, dict) or set(value) != {'X','Y','Z'} or not isinstance(other, dict) or set(other) != {'x','y','z'}:
            raise ValueError('Original vector schema changed: '+name)
        for axis in ('X','Y','Z'):
            a, b = value[axis], other[axis.lower()]
            if type(a) not in (int,float) or type(b) not in (int,float) or not math.isfinite(a) or not math.isfinite(b) or a != b:
                raise ValueError('Original observation value changed: '+name+'.'+axis)
    return True

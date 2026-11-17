"""Frozen native skull evidence checks; no physics, membrane or ODE integration."""
import argparse
from hashlib import sha256
import importlib.util
import json
import math
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec); spec.loader.exec_module(result); return result


h = module('native_skull_helpers', ROOT / 'tools/prepare-causal-motor.py')
gate = module('native_skull_inventory', ROOT / 'tools/receptor_membrane_gate.py')
COUNTS = dict(PhysicalSampleProducerTests=16, PhysicalSampleTransportTests=22, SolutionManifestTests=1,
              SkullKinematicsTests=12, ClockedVestibularMechanicsTests=16, VestibularLabyrinthTransducerTests=7)
POINTS = dict(centre=[0, 0, 0], left=[-.0625, 0, 0], right=[.0625, 0, 0])
VECTORS = ('localPointMeters', 'originWorldMeters', 'rightWorld', 'upWorld', 'forwardWorld',
           'pointWorldMeters', 'pointVelocityWorldMetersPerSecond', 'angularVelocityWorldRadiansPerSecond',
           'gravityWorldMetersPerSecondSquared', 'centerOfMassWorldMeters', 'centerOfMassVelocityWorldMetersPerSecond', 'scale')


def protocol():
    return dict(version='native-skull-sampling-study-v1', points=POINTS, physicalEndpoints=201,
                actualPhysicalSteps=201, finalDeliveredStep=200, actualNativeGridEndpoints=202,
                sidecars=207, pointReceipts=603, heldInspections=4,
                basisAbsoluteBound=1e-5, positionAbsoluteBoundMeters=1e-5,
                pointVelocityAbsoluteBoundMetersPerSecond=1e-5, pointVelocityRelativeBound=1e-6,
                derivedAbsoluteBound=1e-8, derivedRelativeBound=1e-10,
                minimumHeadRotationRadiansPerSecond=.01, minimumOffsetSpeedDifferenceMetersPerSecond=.0001,
                initialAccelerationUnavailable=True, nativeSensorLocationsAnatomicallyCalibrated=False,
                continuousMotionAccuracyQualified=False, physiologyQualified=False, avatarRecoveryAssessed=False)


def prerequisite():
    folder = ROOT / 'artifacts/skull-kinematics-review-20261117'
    h.verify(folder, ['review.json']); review = h.read(folder / 'review.json')
    assert review['operatorReportedCompletion'] and review['savedAnalysisExactlyReproduced'] and review['passedTests'] == 74
    assert review['analysis']['componentChecksPassed'] and review['analysis']['defaultNativeFilesPreserved'] == 61
    baseline = Path(review['capture'])
    assert h.digest(baseline / 'backup-manifest.json') == review['manifestSha256']
    names = ['src/NRE.SimAvatar/AvatarSkullKinematics.cs', 'src/NRE.SimAvatar/AvatarClockedVestibularMechanics.cs']
    h.verify(baseline, ['source/' + name for name in names] + ['managed-runtime-before.json'])
    for name in names:
        assert h.io(ROOT / name).read_text(encoding='utf-8-sig') == h.io(baseline / 'source' / name).read_text(encoding='utf-8-sig'), name
    return review


def digest(text): return sha256(text.encode('utf-8')).hexdigest().upper()
def subtract(a, b): return [x-y for x, y in zip(a, b, strict=True)]
def dot(a, b): return sum(x*y for x, y in zip(a, b, strict=True))
def cross(a, b): return [a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0]]
def maximum(a): return max(abs(x) for x in a)


def vector(value, axes=('x', 'y', 'z')):
    assert isinstance(value, dict) and set(value) == set(axes)
    result = [value[k] for k in axes]
    assert all(type(x) in (int, float) and math.isfinite(x) for x in result)
    return result


def runtime(package):
    # The original motor probe loads ASP.NET as well as CoreCLR. This physical
    # probe uses only CoreCLR. Require the exact shared core, not artificial
    # installation of an otherwise unused framework to equalize file lists.
    baseline = h.read(package / 'qualified-managed-runtime.json')
    actual = h.read(package / 'managed-runtime-before.json')
    assert {k:v for k,v in baseline.items() if k != 'directories'} == {k:v for k,v in actual.items() if k != 'directories'}
    assert len(actual['directories']) == 1
    current = actual['directories'][0]
    assert Path(current['path']).parent.name == 'Microsoft.NETCore.App'
    assert current in baseline['directories']
    # Also preserve the original ASP.NET files used by the focused test project.
    for directory in baseline['directories']:
        root = h.io(directory['path'])
        assert {str(p) for p in root.rglob('*') if p.is_file()} == {str(h.io(e['path'])) for e in directory['files']}
        for entry in directory['files']: assert h.digest(entry['path']) == entry['sha256']
    return dict(exactCoreRuntimeSubset=True, originalManagedFilesUnchanged=True, modelsExecuted=False, testsExecuted=False)


def compare(package):
    bounds = h.read(package / 'skull-protocol.json'); assert bounds == protocol()
    tests = gate.check_tests(package, COUNTS)
    config = h.read(package / 'configuration.json'); assert config['NativeSkullCapture'] and config['NoLiveMonitoring']
    managed = runtime(package)
    study = package / 'study'; clock = h.read(study / 'clock.json')
    result = h.read(study / 'study-result.json')
    assert result['completed'] and result['exitCode'] == 0 and result['skullSampleEndpoints'] == 201
    assert result['actualPhysicsSteps'] == 201 and result['completeThroughStep'] == 200
    session = config['SessionId']; assert clock['sessionId'].replace('-', '') == session
    assert clock['initialPhysicsStep'] == 0 and clock['physicalEpochMs'] == 0 and clock['neuralEpochMs'] == 1000
    seconds = clock['physicsStepMs'] / 1000
    consumed = h.read(study / 'consumed.json'); assert len(consumed) == 201
    assert [s['physicsStep'] for s in consumed] == list(range(201))
    grid = h.read(study / 'source-grid.json'); assert [s['step'] for s in grid] == list(range(202))
    assert len(list(h.io(study).glob('skull-receipts-*.json'))) == 201
    paths = sorted(h.io(study / 'bridge').glob('skull-*.json')); assert len(paths) == bounds['sidecars']
    prior = {}; identities = {}; held = 0; sidecars = {}; receipts = 0
    max_velocity_error = max_position_error = max_basis_error = max_rotation = max_offset_difference = max_derived_error = 0.
    previous_step = 0
    for sequence, path in enumerate(paths):
        assert path.name == f'skull-{sequence:06}.json'
        raw = path.read_text(encoding='utf-8-sig'); source = json.loads(raw)
        assert set(source) == {'version', 'sessionId', 'operation', 'sequence', 'physicsStep', 'samples'}
        reply_name = 'hello.json' if sequence == 0 else f'reply-{sequence:06}.json'
        body = h.read(study / 'bridge' / reply_name)
        assert source['version'] == 'native-skull-sidecar-v1' and source['sessionId'] == body['sessionId'] == session
        assert source['sequence'] == body['sequence'] == sequence
        assert source['physicsStep'] == body['physicsStep'] and source['operation'] == body['operation']
        assert body['paused'] and body['isolated'] and not body['connected'] and body['finite'] and body['musclesEnabled']
        assert body['directStepRejected'] and body['wrongOwnerRejected'] and body['links'] == 15
        assert body['physicsStepMs'] == clock['physicsStepMs']
        assert body['bodyFramesAccepted'] == body['retinalFramesAccepted'] == body['contactFramesAccepted'] == 0
        operation = source['operation']; step = source['physicsStep']
        if sequence == 0: assert operation == 'hello' and step == 0
        else:
            request = h.read(study / 'bridge' / f'request-{sequence:06}.json')
            assert request == dict(version=1, sessionId=session, operation=operation, sequence=sequence, expectedStep=previous_step)
            assert operation in ('step', 'inspect', 'stop')
            assert step == previous_step + (operation == 'step')
        previous_step = step
        assert [s['sensorId'] for s in source['samples']] == list(POINTS)
        if operation in ('inspect', 'stop'):
            assert source['samples'] == sidecars[step][0]['samples'], 'Head changed without a physical step.'
            held += operation == 'inspect'
            if operation == 'stop': assert sequence == 206 and step == 201
        else:
            assert step not in sidecars; sidecars[step] = (source, raw, reply_name)
        point_vectors = {}
        for snapshot in source['samples']:
            assert set(snapshot) == set(VECTORS) | {'version', 'headBodyId', 'sensorId', 'physicsStep', 'rotation'}
            assert snapshot['version'] == 'native-head-material-point-v1' and snapshot['headBodyId'] == 'head'
            assert snapshot['physicsStep'] == step
            v = {k: vector(snapshot[k]) for k in VECTORS}; name = snapshot['sensorId']; point_vectors[name] = v
            assert v['localPointMeters'] == POINTS[name] and sum((x-1)**2 for x in v['scale']) <= 1e-12
            x, y, z, w = vector(snapshot['rotation'], ('x', 'y', 'z', 'w'))
            axes = [[1-2*(y*y+z*z), 2*(x*y+z*w), 2*(x*z-y*w)],
                    [2*(x*y-z*w), 1-2*(x*x+z*z), 2*(y*z+x*w)],
                    [2*(x*z+y*w), 2*(y*z-x*w), 1-2*(x*x+y*y)]]
            right, up, forward = [v[k] for k in ('rightWorld', 'upWorld', 'forwardWorld')]
            basis_error = max(abs(x*x+y*y+z*z+w*w-1),
                              *(maximum(subtract(a, b)) for a, b in zip(axes, (right, up, forward), strict=True)))
            assert basis_error <= bounds['basisAbsoluteBound']; max_basis_error = max(max_basis_error, basis_error)
            position = [v['originWorldMeters'][i] + sum(axes[j][i]*POINTS[name][j] for j in range(3)) for i in range(3)]
            position_error = maximum(subtract(position, v['pointWorldMeters']))
            assert position_error <= bounds['positionAbsoluteBoundMeters']; max_position_error = max(max_position_error, position_error)
            rotational = cross(v['angularVelocityWorldRadiansPerSecond'], subtract(v['pointWorldMeters'], v['centerOfMassWorldMeters']))
            expected = [a+b for a, b in zip(v['centerOfMassVelocityWorldMetersPerSecond'], rotational, strict=True)]
            velocity_error = maximum(subtract(expected, v['pointVelocityWorldMetersPerSecond']))
            assert velocity_error <= bounds['pointVelocityAbsoluteBoundMetersPerSecond'] + bounds['pointVelocityRelativeBound']*maximum(expected)
            max_velocity_error = max(max_velocity_error, velocity_error)
            max_rotation = max(max_rotation, maximum(v['angularVelocityWorldRadiansPerSecond']))
        for name in ('left', 'right'):
            for field in VECTORS:
                if field not in ('localPointMeters', 'pointWorldMeters', 'pointVelocityWorldMetersPerSecond'):
                    assert point_vectors[name][field] == point_vectors['centre'][field], 'Inconsistent same-step head state.'
            max_offset_difference = max(max_offset_difference, maximum(subtract(point_vectors[name]['pointVelocityWorldMetersPerSecond'],
                                                                                point_vectors['centre']['pointVelocityWorldMetersPerSecond'])))
    assert held == 4 and set(sidecars) == set(range(202)) and previous_step == 201
    for step in range(201):
        source, raw, reply_name = sidecars[step]
        assert grid[step] == dict(step=step, replyFile='bridge/'+reply_name)
        rows = h.read(study / f'skull-receipts-{step:06}.json')
        assert len(rows) == 3 and [r['sensorId'] for r in rows] == list(POINTS)
        for snapshot, row in zip(source['samples'], rows, strict=True):
            name = row['sensorId']; receipt = row['receipt']; receipts += 1
            assert row['sourceFile'] == f'bridge/skull-{source["sequence"]:06}.json' and row['sourceSha256'] == digest(raw)
            assert receipt['physicsStep'] == step
            assert receipt['physicalTimeMs'] == consumed[step]['physicalTimeMs'] == step*clock['physicsStepMs']
            assert receipt['neuralTimeMs'] == consumed[step]['neuralTimeMs'] == 1000+step*clock['physicsStepMs']
            original = json.loads(receipt['rawObservationJson'])
            assert receipt['rawObservationSha256'] == digest(receipt['rawObservationJson'])
            # Only the observation fields, with the C# record's exact names/casing.
            expected = {k[0].upper()+k[1:]: {axis.upper(): value for axis, value in snapshot[k].items()}
                        for k in VECTORS[:9]}
            expected.update(GravityWorldMetersPerSecondSquared={k.upper(): v for k, v in snapshot['gravityWorldMetersPerSecondSquared'].items()},
                            PhysicsStep=step, SampleId=consumed[step]['sampleId'], HeadBodyId='head', SensorId=name)
            assert original == expected
            if name in identities: assert identities[name] == row['adapterFingerprint']
            identities[name] = row['adapterFingerprint']
            if step == 0:
                assert all(receipt[k] is None for k in ('previousObservationSha256', 'accelerationFromStep',
                           'averageAccelerationWorldMetersPerSecondSquared', 'vestibularInput'))
            else:
                before, before_hash = prior[name]
                assert receipt['previousObservationSha256'] == before_hash and receipt['accelerationFromStep'] == step-1
                acceleration = [d/seconds for d in subtract(vector(snapshot['pointVelocityWorldMetersPerSecond']), vector(before['pointVelocityWorldMetersPerSecond']))]
                current = vector(receipt['averageAccelerationWorldMetersPerSecondSquared'])
                gi = subtract(vector(snapshot['gravityWorldMetersPerSecondSquared']), acceleration)
                omega = vector(snapshot['angularVelocityWorldRadiansPerSecond'])
                axes = [vector(snapshot[k]) for k in ('rightWorld', 'upWorld', 'forwardWorld')]
                projected = [dot(omega, a) for a in axes] + [dot(gi, axes[0]), -dot(gi, axes[1]), dot(gi, axes[2])]
                inp = receipt['vestibularInput']
                assert inp['physicsStep'] == step and inp['physicalSampleId'] == consumed[step]['sampleId']
                provenance = dict(Version='isolated-head-world-kinematics-v1', Fingerprint=row['adapterFingerprint'],
                                  Previous=before_hash, Current=receipt['rawObservationSha256'])
                assert inp['physicalObservationSha256'] == digest(json.dumps(provenance, separators=(',', ':')))
                fields = ('pitchVelocityRadiansPerSecond', 'yawVelocityRadiansPerSecond', 'rollVelocityRadiansPerSecond',
                          'gravitoInertialRightMetersPerSecondSquared', 'gravitoInertialDownMetersPerSecondSquared', 'gravitoInertialForwardMetersPerSecondSquared')
                actual = current + [inp[k] for k in fields]; expected_values = acceleration + projected
                assert all(math.isfinite(x) for x in actual+expected_values)
                error = maximum(subtract(actual, expected_values))
                assert error <= bounds['derivedAbsoluteBound'] + bounds['derivedRelativeBound']*maximum(expected_values)
                max_derived_error = max(max_derived_error, error)
            prior[name] = snapshot, receipt['rawObservationSha256']
    assert len(set(identities.values())) == 3 and receipts == 603
    assert max_rotation > bounds['minimumHeadRotationRadiansPerSecond'], 'Rotation was not exercised.'
    assert max_offset_difference > bounds['minimumOffsetSpeedDifferenceMetersPerSecond'], 'Off-centre motion was not exercised.'
    analysis = dict(completed=True, nativeHeadSamplingChecksPassed=True, tests=tests, sidecars=len(paths), pointReceipts=receipts,
                    managedRuntime=managed,
                    completeThroughStep=200, heldInspections=held, nativeGridEndpoints=202,
                    maximumPointVelocityErrorMetersPerSecond=max_velocity_error, maximumPointGeometryErrorMeters=max_position_error,
                    maximumBasisError=max_basis_error, maximumDerivedError=max_derived_error,
                    maximumHeadAngularComponentRadiansPerSecond=max_rotation, maximumOffsetVelocityDifferenceMetersPerSecond=max_offset_difference,
                    continuousMotionAccuracyQualified=False, nativeSensorLocationsAnatomicallyCalibrated=False,
                    vestibularMechanicsExecuted=False, neuronalModelExecuted=False, liveBrainAccessed=False,
                    physiologyQualified=False, avatarRecoveryAssessed=False)
    return analysis


if __name__ == '__main__':
    if not __debug__: raise RuntimeError('Assertions must remain enabled.')
    parser = argparse.ArgumentParser(); parser.add_argument('command', choices=['check-tests', 'compare', 'runtime'])
    parser.add_argument('package', type=Path); args = parser.parse_args(); package = args.package.resolve()
    if args.command == 'check-tests': result = gate.check_tests(package, COUNTS)
    elif args.command == 'runtime': result = runtime(package)
    else:
        result = compare(package)
        with (package / 'skull-analysis.json').open('x', encoding='utf-8') as stream: json.dump(result, stream, indent=2, allow_nan=False)
    print(json.dumps(result, indent=2), flush=True)

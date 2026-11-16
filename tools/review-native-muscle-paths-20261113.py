"""Audit completed native point-force evidence; no tests or physics execution."""
from collections import Counter
from datetime import datetime, timezone
from hashlib import sha256
import importlib.util
import json
import math
from pathlib import Path
import re
import struct
import subprocess
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
vec = lambda v: tuple(v[k] for k in ('x', 'y', 'z'))
add = lambda a, b: tuple(x + y for x, y in zip(a, b))
sub = lambda a, b: tuple(x - y for x, y in zip(a, b))
mul = lambda a, t: tuple(x * t for x in a)
dot = lambda a, b: sum(x * y for x, y in zip(a, b))
norm = lambda a: math.sqrt(dot(a, a))
err = lambda a, b: norm(sub(a, b))
f32 = lambda x: struct.unpack('<f', struct.pack('<f', x))[0]


def cross(a, b):
    return (a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0])


def text_bytes(value):
    data = value.encode('utf-8'); count = len(data); prefix = bytearray()
    while count >= 128:
        prefix.append((count & 127) | 128); count >>= 7
    return bytes(prefix + bytes([count])) + data


def fingerprint(row):
    data = b''.join(text_bytes(s) for s in ('fixed-anchor-musculotendon-polyline-v1', 'native-fixture', row['key'],
        'Synthetic free articulation roots; no human anatomy')) + struct.pack('<i', len(row['before']))
    for i, body in enumerate(row['before']):
        data += text_bytes('anchor-' + str(i)) + text_bytes(body['id'])
        data += struct.pack('<ddd', *(0.0 if x == 0 else x for x in vec(body['localAnchor'])))
    return sha256(data).hexdigest().upper()


def audit_case(row):
    bodies = row['before']; forces = [(0., 0., 0.) for _ in bodies]
    length = speed = 0.
    for i in range(len(bodies)-1):
        delta = sub(vec(bodies[i+1]['point']), vec(bodies[i]['point'])); segment = norm(delta)
        unit = mul(delta, 1/segment); length += segment
        speed += dot(unit, sub(vec(bodies[i+1]['pointVelocity']), vec(bodies[i]['pointVelocity'])))
        tension = mul(unit, row['tensionNewtons']); forces[i] = add(forces[i], tension); forces[i+1] = sub(forces[i+1], tension)
    metrics = {name: 0. for name in ('maximumPositionError', 'maximumPointVelocityError', 'maximumForceRoundingError',
        'maximumLinearVelocityError', 'maximumAngularVelocityError', 'maximumClearedForceVelocityError')}
    power = native_power = 0.
    for i, body in enumerate(bodies):
        assert body['id'] == 'path-body-' + str(i) and body['mass'] == 2+i
        assert vec(body['inertia']) == (f32(f32(.15)*(i+1)),)*3
        for stage in ('before', 'afterForce', 'afterClear'):
            state = row[stage][i]; assert state['id'] == body['id']
            for field in ('position', 'center', 'velocity', 'angularVelocity', 'inertia', 'point', 'pointVelocity'):
                assert all(math.isfinite(x) for x in vec(state[field]))
            assert all(math.isfinite(x) for x in state['rotation'].values())
        q = body['rotation']; local = vec(body['localAnchor']); qv = vec(q)
        rotated = add(local, mul(cross(qv, add(cross(qv, local), mul(local, q['w']))), 2))
        world = add(vec(body['position']), rotated)
        velocity = add(vec(body['velocity']), cross(vec(body['angularVelocity']), sub(vec(body['point']), vec(body['center']))))
        actual_force = vec(body['appliedForce'])
        assert tuple(map(f32, forces[i])) == actual_force
        torque = cross(sub(vec(body['point']), vec(body['center'])), actual_force)
        expected_v = add(vec(body['velocity']), mul(actual_force, row['stepSeconds']/body['mass']))
        expected_w = add(vec(body['angularVelocity']), mul(torque, row['stepSeconds']/body['inertia']['x']))
        values = dict(maximumPositionError=err(world, vec(body['point'])), maximumPointVelocityError=err(velocity, vec(body['pointVelocity'])),
            maximumForceRoundingError=err(forces[i], actual_force), maximumLinearVelocityError=err(expected_v, vec(row['afterForce'][i]['velocity'])),
            maximumAngularVelocityError=err(expected_w, vec(row['afterForce'][i]['angularVelocity'])),
            maximumClearedForceVelocityError=max(err(vec(row['afterForce'][i]['velocity']), vec(row['afterClear'][i]['velocity'])),
                err(vec(row['afterForce'][i]['angularVelocity']), vec(row['afterClear'][i]['angularVelocity']))))
        for name, value in values.items(): metrics[name] = max(metrics[name], value)
        power += dot(forces[i], vec(body['pointVelocity'])); native_power += dot(actual_force, velocity)
    metrics.update(nativePowerError=abs(native_power-power), powerResidualWatts=power+row['tensionNewtons']*speed,
                   powerWatts=power, lengthMeters=length, pathVelocityMetersPerSecond=speed)
    assert row['pathFingerprint'] == fingerprint(row)
    for name, value in metrics.items():
        assert math.isfinite(value) and abs(value-row[name]) <= 1e-12, (row['key'], name, value, row[name])
        if name.startswith('maximum'):
            assert value <= (3e-6 if name == 'maximumForceRoundingError' else 2e-6)
    assert metrics['nativePowerError'] <= 2e-5 and abs(metrics['powerResidualWatts']) <= 1e-11
    return metrics


def main():
    spec = importlib.util.spec_from_file_location('capture_audit', ROOT / 'tools/review-olive-and-motor-20261109.py')
    common = importlib.util.module_from_spec(spec); spec.loader.exec_module(common)
    package = ROOT / 'artifacts/native-muscle-paths-20261113-d9097a'
    revision = 'c9eff12ace1bc35f7bfa2cfc7777f3a377a64cf5'
    review = common.verify(package, ['test-binaries.json'], revision, 'FAILED', 1)
    archive = common.module('source_archive_audit', ROOT / 'tools/review-spindle-root-and-causal-motor-20261113.py')
    review['verifiedGitFiles'] = archive.source_archive(common, package, revision)
    for entry in common.read(package / 'player-files.json'):
        Path(entry['Path']).relative_to(package / 'player'); assert common.digest(entry['Path']) == entry['Sha256']
    source = common.io(package / 'source/UnityWorld'); compiled = 0
    for path in source.rglob('*.cs'):
        assert path.read_bytes() == common.io(package / 'built-project' / path.relative_to(source)).read_bytes(); compiled += 1
    assert common.read(package / 'player/build-result.json') == dict(result='Succeeded', errors=0, warnings=0)
    config = common.read(package / 'configuration.json'); status = common.read(package / 'window-status.json')
    assert status['NativeExitCode'] == 0 and status['PassedFocusedTests'] == 75
    assert common.digest(config['UnityExecutable']) == config['UnityExecutableSha256']
    counts = dict(MusclePathGeometryTests=31, CausalMotorPlantTests=27, DynamicMuscleTests=16, SolutionManifestTests=1)
    assert config['ExpectedClassCounts'] == counts and config['ExpectedTestCases'] == 75
    expected = Counter()
    for cls, count in counts.items():
        code = common.io(package / 'source/tests/NeuralResonanceEngine.DNNE.Tests' / (cls + '.cs')).read_text()
        found = re.findall(r'((?:\s*\[[^\n]+\])+\s*)public\s+(?:async\s+)?(?:void|Task)\s+(\w+)\(', code)
        methods = {name: (1 if '[Fact]' in attrs else attrs.count('[InlineData(')) for attrs, name in found if '[Fact]' in attrs or '[Theory]' in attrs}
        assert sum(methods.values()) == count
        expected.update({'NeuralResonanceEngine.DNNE.Tests.' + cls + '.' + name: n for name, n in methods.items()})
    assert dict(expected) == config['ExpectedTestMethods']
    ns = {'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
    tests = ET.parse(package / 'test-results/focused.trx').findall('.//t:UnitTestResult', ns)
    assert len(tests) == len({t.attrib['testName'] for t in tests}) == 75 and all(t.attrib['outcome']=='Passed' for t in tests)
    assert Counter(t.attrib['testName'].split('(', 1)[0] for t in tests) == expected
    print('Full C:/D: capture, Git/compiled source, binaries and 75 cases verified.', flush=True)
    native = common.read(package / 'native/results.json')
    assert native['completed'] and native['passed'] and native['isolated'] and not native['error']
    assert not any(native[k] for k in ('neuronalModelExecuted', 'avatarLoaded', 'anatomicalCalibrationQualified'))
    assert native['unity'] == config['ExpectedUnityVersion'] and native['sessionId'] == config['SessionId']
    assert native['actualPhysicsSteps'] == 48 and len(native['cases']) == 24
    assert [native[k] for k in ('positionBound', 'velocityBound', 'forceRoundingBound', 'powerBound')] == [2e-6, 2e-6, 3e-6, 2e-5]
    matrix = [(b, r, f32(h), t) for b in (False, True) for r in (False, True) for h in (.01, .001, .000125) for t in (0, 10)]
    recomputed = []
    for i, (row, parameters) in enumerate(zip(native['cases'], matrix)):
        assert (row['bent'], row['rotated'], row['stepSeconds'], row['tensionNewtons']) == parameters
        assert row['key'] == 'case-' + str(i).zfill(2) and row['completed'] and row['passed'] and not row['error']
        assert row['startStep'] == 2*i and row['endStep'] == 2*(i+1)
        assert all(len(row[s]) == (3 if row['bent'] else 2) for s in ('before', 'afterForce', 'afterClear'))
        assert common.read(package / 'native' / (row['key']+'.json')) == row
        initial = common.read(package / 'native' / (row['key']+'-initial.json'))
        forced = common.read(package / 'native' / (row['key']+'-forced.json'))
        assert initial['before'] == forced['before'] == row['before'] and forced['afterForce'] == row['afterForce']
        recomputed.append(audit_case(row))
    output = ROOT / 'artifacts/native-muscle-path-review-20261113-verified'; output.mkdir()
    # Replay only saved-data checks. PowerShell 5 parses these JSON fractions as
    # Decimal, so compare the recorded float-derived step as Double on BOTH sides.
    # Neither invocation builds, launches or steps anything.
    runner = 'tools/start-native-muscle-paths-window.ps1'
    frozen = common.io(package / 'source' / runner).read_text(); current = (ROOT / runner).read_text()
    bad = '@([single].01,[single].001,[single].000125)'; good = '@([single]0.01,[single]0.001,[single]0.000125)'
    old_comparison = '$row.stepSeconds -ne [double]$step'
    new_comparison = '[double]$row.stepSeconds -ne [double]$step'
    assert frozen.count(bad) == frozen.count(old_comparison) == 1
    assert frozen.replace(bad, good).replace(old_comparison, new_comparison) == current
    begin = frozen.index(' $native=Get-Content'); end = frozen.index(' Assert-Package', begin)
    block = frozen[begin:end]
    original_block = output / 'frozen-analysis.ps1'; repaired_block = output / 'repaired-analysis.ps1'
    original_block.write_text(block); repaired_block.write_text(block.replace(bad, good).replace(old_comparison, new_comparison))
    script = '''param([string]$Package,[string]$Output)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$configuration=Get-Content -LiteralPath (Join-Path $Package 'configuration.json') -Raw | ConvertFrom-Json
$nativeOutput=Join-Path $Package 'native'
$failure=$null
try { & (Join-Path $Output 'frozen-analysis.ps1') } catch { $failure=[ordered]@{Message=$_.Exception.Message;Position=$_.InvocationInfo.PositionMessage;Stack=$_.ScriptStackTrace} }
if(-not $failure -or $failure.Message -notlike "*property '1'*"){throw 'Original parser failure not reproduced.'}
$row=(Get-Content -LiteralPath (Join-Path $nativeOutput 'results.json') -Raw | ConvertFrom-Json).cases[0]
$step=[single]0.01
$promotion=[ordered]@{SavedType=$row.stepSeconds.GetType().FullName;SavedText=$row.stepSeconds.ToString();ExpectedDouble=([double]$step).ToString('R');MixedTypesEqual=$row.stepSeconds -eq [double]$step;BothDoublesEqual=[double]$row.stepSeconds -eq [double]$step}
if($promotion.SavedType -ne 'System.Decimal' -or $promotion.MixedTypesEqual -or -not $promotion.BothDoublesEqual){throw 'Original JSON numeric type mismatch not reproduced.'}
& (Join-Path $Output 'repaired-analysis.ps1')
[ordered]@{OriginalFailure=$failure;NumericTypeMismatch=$promotion;RepairedSavedDataChecksPassed=$true;ModelsExecuted=$false;TestsExecuted=$false} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $Output 'analysis-replay.json') -Encoding UTF8
'''
    replay = output / 'replay.ps1'; replay.write_text(script)
    import os
    shell = Path(os.environ['SystemRoot']) / 'System32/WindowsPowerShell/v1.0/powershell.exe'
    subprocess.run([str(shell), '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', str(replay), '-Package', str(package), '-Output', str(output)], check=True)
    replay_result = common.read(output / 'analysis-replay.json'); assert replay_result['RepairedSavedDataChecksPassed']
    for name in ('UnityWorld/Assets/NRE/Runtime/MusclePathNativeQualification.cs', 'UnityWorld/Assets/NRE/Runtime/MusclePathGeometry.cs', 'UnityWorld/Assets/NRE/Editor/MusclePathBuild.cs'):
        assert (ROOT / name).read_text() == common.io(package / 'source' / name).read_text()
    maxima = {k:max(abs(r[k]) for r in recomputed) for k in recomputed[0] if k.startswith('maximum') or k in ('nativePowerError','powerResidualWatts')}
    review.update(reviewedUtc=datetime.now(timezone.utc).isoformat(), operatorReportedCompletion=True,
        originalFailedCapturePreserved=True, originalAnalysisFailureReproduced=True,
        analysisOnlyRepairs=['Add leading zero to three PowerShell single-precision decimal literals.', 'Compare saved and declared physical steps as Double, avoiding mixed Decimal/Double equality.'],
        savedDataCriteriaPassed=True, independentlyRecomputedNativeCases=24, actualPhysicsSteps=48, passedTests=75,
        compiledUnitySourceFilesVerified=compiled, maximumRecomputedErrors=maxima, originalBoundsChanged=False,
        nativePointForceChecksPassed=True, constrainedJointsQualified=False, anatomicalCalibrationQualified=False,
        physiologyQualified=False, avatarRecoveryAssessed=False, testsExecutedDuringReview=False, modelsSolvedDuringReview=False)
    (output / 'review.json').write_text(json.dumps(review, indent=2, allow_nan=False)+'\n')
    print(json.dumps(review, indent=2), flush=True)


if __name__ == '__main__':
    main()

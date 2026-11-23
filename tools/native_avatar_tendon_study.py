"""Freeze/audit a prescribed full-Avatar mechanical study; no neural model here."""
import argparse
from collections import Counter
from datetime import datetime, timezone
import hashlib
import importlib.util
import json
import math
from pathlib import Path, PurePosixPath
import re
import shutil
import struct
import subprocess
import sys
import uuid
import xml.etree.ElementTree as ET
from zipfile import ZipFile

ROOT = Path(__file__).resolve().parents[1]
BASE = '68f985a4d1a91243ed304fc83b9608c29f0878f3'
COUNTS = dict(MusclePathGeometryTests=31, CausalMotorPlantTests=27, DynamicMuscleTests=16,
              StandingBalanceEvidenceTests=12, UnityStandingTrialTests=9, UnityBodySensorTests=16, SolutionManifestTests=1)
GUARDS = ['step', 'state', 'plant', 'repeat', 'missing', 'negative', 'nonfinite', 'shape', 'gravity', 'material', 'filter']

def digest(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest().upper()

def read(path):
    return json.loads(Path(path).read_text(encoding='utf-8-sig'))

def write(path, value):
    with Path(path).open('x', encoding='utf-8') as stream:
        json.dump(value, stream, indent=2, allow_nan=False)
        stream.write('\n')

def git(*args):
    return subprocess.check_output(['git', *args], cwd=ROOT, text=True, encoding='utf-8').strip()

def inventory():
    result = {}
    for cls, count in COUNTS.items():
        code = (ROOT/'tests/NeuralResonanceEngine.DNNE.Tests'/f'{cls}.cs').read_text(encoding='utf-8-sig')
        methods = re.findall(r'((?:\s*\[[^\n]+\])+\s*)public\s+(?:async\s+)?(?:void|Task)\s+(\w+)\(', code)
        found = {name: (1 if '[Fact]' in attrs else attrs.count('[InlineData(')) for attrs, name in methods if '[Fact]' in attrs or '[Theory]' in attrs}
        assert sum(found.values()) == count and all(found.values()), (cls, found)
        result.update({'NeuralResonanceEngine.DNNE.Tests.'+cls+'.'+name: n for name, n in found.items()})
    return result

def python_runtime():
    import _hashlib, _struct
    base=Path(sys.base_prefix)
    files={Path(sys.executable)}
    for module in (math,_hashlib,_struct):
        if getattr(module,'__file__',None): files.add(Path(module.__file__))
    files.update(base.glob('python*.dll')); files.update((base/'DLLs').glob('*.dll'))
    for module in (struct,hashlib,json):
        files.add(Path(module.__file__))
    files.update(Path(json.__file__).parent.glob('*.py'))
    return dict(version=sys.version,basePrefix=str(base),files=[dict(Path=str(p),Sha256=digest(p)) for p in sorted(files)])

def prepare():
    assert not git('status', '--porcelain'), 'Commit clean source first.'
    revision = git('rev-parse', 'HEAD'); assert revision == git('rev-parse', 'origin/main'), 'Push first.'
    assert not git('diff', '--name-only', BASE, revision, '--', 'src', 'tests'), 'Canonical Avatar/neuronal kernels and regressions must stay unchanged.'
    code = (ROOT/'UnityWorld/Assets/NRE/Runtime/MuscleCore.cs').read_text(encoding='utf-8-sig')
    profiles = re.findall(r'new MuscleProfile\("([^"]+)"', code)
    assert len(profiles) == len(set(profiles)) == 19
    cases = [dict(name='quiet', condition='quiet', muscle=''), dict(name='freefall', condition='freefall', muscle='')]
    cases += [dict(name=p+'-'+end, condition='pulse', muscle=p+'/'+end) for p in profiles for end in ('flexor', 'extensor')]
    cases += [dict(name='reject-'+g, condition='reject-'+g, muscle='') for g in GUARDS]
    methods = inventory(); assert sum(methods.values()) == 112 and len(cases) == 51
    name = 'avatar-tendons-'+datetime.now(timezone.utc).strftime('%Y%m%d')+'-'+uuid.uuid4().hex[:6]
    package = Path('D:/DNNE-build-work')/name; backup = Path('D:/DNNE-desktop-runs/UnityWorld')/name
    assert not package.exists() and not backup.exists(); package.mkdir()
    subprocess.run(['git', 'archive', '--format=zip', '--output='+str(package/'source.zip'), revision], cwd=ROOT, check=True)
    with ZipFile(package/'source.zip') as archive:
        for item in archive.infolist():
            p = PurePosixPath(item.filename)
            assert not p.is_absolute() and '..' not in p.parts and ':' not in item.filename and '\\' not in item.filename
            assert ((item.external_attr >> 16) & 0o170000) != 0o120000
            target = package/'source'/p
            if item.is_dir(): target.mkdir(parents=True, exist_ok=True)
            else:
                target.parent.mkdir(parents=True, exist_ok=True)
                with target.open('xb') as f: f.write(archive.read(item))
    # The already installed licensed rig is intentionally outside Git. Freeze its
    # exact attributed assets; no download or anatomy substitution is performed.
    attribution=read(ROOT/'UnityWorld/Assets/Avatar/provenance.json')
    for entry in attribution['files']:
        source=ROOT/'UnityWorld/Assets/Avatar'/entry['file']
        assert source.name in ('MikeAlger.fbx','MikeAlger_Texture.jpeg') and digest(source)==entry['sha256'].upper()
        shutil.copyfile(source,package/'source/UnityWorld/Assets/Avatar'/source.name)
    spec=importlib.util.spec_from_file_location('metadata',ROOT/'tools/unity_metadata.py')
    metadata=importlib.util.module_from_spec(spec);spec.loader.exec_module(metadata)
    write(package/'unity-metadata.json',metadata.validate(package/'source/UnityWorld'))
    shutil.copyfile(package/'source/tools/backup-unity-run.ps1', package/'backup-run.ps1')
    files = [dict(Path=p.relative_to(package).as_posix(), Sha256=digest(p)) for p in sorted(package.rglob('*')) if p.is_file()]
    unity = Path('C:/Program Files/Unity/Hub/Editor/6000.3.19f1/Editor/Unity.exe')
    dotnet = Path(shutil.which('dotnet')); python = Path(sys.executable)
    config = dict(Revision=revision, BaselineRevision=BASE, PackageFiles=files, BackupDestination=str(backup),
        UnityBuildWorkspace=str(package.parent/(name+'-unity')), UnityExecutable=str(unity), UnityExecutableSha256=digest(unity),
        PythonExecutable=str(python), PythonExecutableSha256=digest(python), DotnetExecutable=str(dotnet), DotnetExecutableSha256=digest(dotnet),
        NoLiveMonitoring=True, IsolatedFullAvatarTendons=True, ExpectedTestCases=112, ExpectedTestMethods=methods,
        TestFilter='|'.join('FullyQualifiedName~'+c for c in COUNTS), ExpectedNativeCases=51, ExpectedNativeSteps=413,
        Cases=cases, PythonRuntime=python_runtime(), Limits=dict(forceAbsoluteNewtons=1e-9, initialMomentArmMeters=2e-6, responseObservationMeters=1e-8),
        SyntheticAttachments=True, NeuronalSourceExecuted=False, AnatomicalCalibrationQualified=False, AvatarBalanceQualified=False)
    write(package/'configuration.json', config)
    preflight(package)
    print(json.dumps(dict(Package=str(package), Backup=str(backup), Revision=revision,
        Launcher=str(package/'source/tools/start-avatar-tendons-window.ps1')), indent=2), flush=True)

def preflight(package):
    c = read(package/'configuration.json')
    assert c['NoLiveMonitoring'] and c['IsolatedFullAvatarTendons'] and c['SyntheticAttachments']
    assert c['ExpectedNativeCases'] == len(c['Cases']) == 51 and c['ExpectedNativeSteps'] == 413
    assert c['ExpectedTestCases'] == 112 and not any(c[k] for k in ('NeuronalSourceExecuted', 'AnatomicalCalibrationQualified', 'AvatarBalanceQualified'))
    for f in c['PackageFiles']: assert digest(package/f['Path']) == f['Sha256'], f['Path']
    for name in ('Unity', 'Dotnet', 'Python'): assert digest(c[name+'Executable']) == c[name+'ExecutableSha256'], name
    assert c['PythonRuntime']==python_runtime(), 'Python arithmetic/serialization runtime changed.'
    print('Frozen source and executable identities verified; no model/test executed by preflight.', flush=True)

def check_tests(package):
    c = read(package/'configuration.json'); ns = {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
    tree = ET.parse(package/'test-results/focused.trx')
    definitions = {d.attrib['id']: d.find('t:TestMethod', ns).attrib for d in tree.findall('.//t:UnitTest', ns)}
    actual = Counter()
    results = tree.findall('.//t:UnitTestResult', ns)
    for r in results:
        assert r.attrib['outcome'] == 'Passed', r.attrib
        method = definitions[r.attrib['testId']]
        actual[method['className'].split(',')[0]+'.'+method['name']] += 1
    assert len(results) == 112 and dict(actual) == c['ExpectedTestMethods'], dict(actual)
    print('All 112 exact component/regression cases passed.', flush=True)

def real(bits):
    assert isinstance(bits, str) and re.fullmatch('[0-9A-F]{16}', bits), bits
    value = struct.unpack('>d', bytes.fromhex(bits))[0]; assert math.isfinite(value)
    return value

def vec(bits):
    assert len(bits) == 3
    return tuple(map(real, bits))

def sub(a, b): return tuple(x-y for x,y in zip(a,b,strict=True))
def dot(a, b): return sum(x*y for x,y in zip(a,b,strict=True))
def cross(a,b): return (a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0])

def analyse(package):
    c = read(package/'configuration.json'); summaries = []; endpoints = {}; steps = 0; max_force_error = 0.0
    for case in c['Cases']:
        folder = package/'native'/case['name']; result = read(folder/'result.json')
        assert result['completed'] and result['passed'] and not result.get('error'), case['name']
        assert result['condition'] == case['condition'] and result['muscle'] == case['muscle'] and result['unity'] == '6000.3.19f1'
        assert result['links'] == 15 and result['paths'] == 38
        assert not any(result[k] for k in ('neuronalSourceExecuted', 'anatomicalCalibrationQualified', 'avatarBalanceQualified'))
        fixtures = {x['muscleId']:x for x in result['fixtures']}; assert len(fixtures) == 38
        records = [json.loads(line) for line in (folder/'endpoints.jsonl').read_text(encoding='utf-8-sig').splitlines()]
        expected = 100 if case['condition']=='freefall' else 1 if case['condition']=='reject-repeat' else 0 if case['condition'].startswith('reject-') else 8
        assert result['finalStep'] == expected and len(records) == expected+1
        dt = real(result['stepMsBits']); assert dt == float(struct.unpack('<f',struct.pack('<f',.005))[0])*1000
        for n, record in enumerate(records):
            assert record['step'] == n and real(record['physicalTimeMsBits']) == n*dt
            assert hashlib.sha256(record['stateJson'].encode()).hexdigest().upper() == record['stateHash']
            state = json.loads(record['stateJson']); assert state['physicsStep'] == n and len(state['links']) == 15
            for link in state['links']:
                for key in ('positionBits','rotationBits','velocityBits','angularVelocityBits','jointPositionBits','jointVelocityBits','jointForceBits'):
                    list(map(real,link[key]))
            assert {p['muscleId'] for p in record['paths']} == fixtures.keys()
            for path in record['paths']:
                pts=path['points']; assert len(pts)==2
                d=sub(vec(pts[1]['positionBits']),vec(pts[0]['positionBits'])); length=math.sqrt(dot(d,d))
                assert length>0 and abs(length-real(path['lengthBits'])) <= 2e-12
                velocity=dot(sub(vec(pts[1]['velocityBits']),vec(pts[0]['velocityBits'])),d)/length
                assert abs(velocity-real(path['velocityBits']))<=2e-10
                if n==0:
                    fixture=fixtures[path['muscleId']]; moment=dot(cross(sub(vec(pts[1]['positionBits']),vec(fixture['pivotBits'])),tuple(-v/length for v in d)),vec(fixture['worldAxisBits']))
                    assert abs(moment+fixture['sign']*real(fixture['radiusBits'])) <= c['Limits']['initialMomentArmMeters']
            if n:
                points=json.loads(record['applicationsJson'])['points']; assert len(points)==76
                grouped={}
                for point in points: grouped.setdefault(point['muscleId'],[]).append(point)
                assert grouped.keys()==fixtures.keys()
                for path in records[n-1]['paths']:
                    a,b=path['points']; d=sub(vec(b['positionBits']),vec(a['positionBits'])); length=math.sqrt(dot(d,d))
                    tension=40 if case['condition']=='pulse' and path['muscleId']==case['muscle'] else 0
                    applied=grouped[path['muscleId']]; assert len(applied)==2
                    for index,p in enumerate(applied):
                        original=path['points'][index]
                        assert p['attachmentId']==original['attachmentId'] and p['bodyId']==original['bodyId'] and p['pointBits']==original['positionBits']
                        requested=tuple(real(p[k]) for k in ('requestedXBits','requestedYBits','requestedZBits'))
                        expected_force=tuple((1 if index==0 else -1)*tension*v/length for v in d)
                        error=max(abs(x-y) for x,y in zip(requested,expected_force,strict=True)); max_force_error=max(max_force_error,error)
                        assert error <= c['Limits']['forceAbsoluteNewtons']
                        assert vec(p['appliedBits']) == tuple(struct.unpack('<f',struct.pack('<f',x))[0] for x in requested)
        if case['condition'].startswith('reject-'):
            assert result['rejectedWithoutStep'] and result['faultLatchPassed'] and result['rejectedError']
            assert result['beforeRejectedState']==result['afterRejectedState']
        steps += expected; endpoints[case['name']]=records
        summaries.append(dict(name=case['name'],steps=expected,passed=True))
    quiet=endpoints['quiet']; responses=[]
    for case in c['Cases']:
        if case['condition']!='pulse': continue
        observed=endpoints[case['name']]
        assert observed[0]['stateHash']==quiet[0]['stateHash'], 'Initial physical states differ; do not attribute response.'
        end=json.loads(observed[-1]['stateJson'])['links']; base=json.loads(quiet[-1]['stateJson'])['links']
        difference=max(abs(x-y) for a,b in zip(end,base,strict=True) for x,y in zip(vec(a['positionBits']),vec(b['positionBits']),strict=True))
        responses.append(dict(muscle=case['muscle'],maximumPositionDifferenceMeters=difference,observed=difference>c['Limits']['responseObservationMeters']))
    assert steps == 413 and any(r['observed'] for r in responses), 'No measurable native response.'
    write(package/'analysis.json', dict(completed=True,mechanicalCriteriaPassed=True,cases=summaries,actualPhysicsSteps=steps,
        maximumForceErrorNewtons=max_force_error,comparisonToQuiet=responses,syntheticAttachments=True,
        independentCoupledTrajectoryQualified=False,neuronalSourceExecuted=False,physiologyQualified=False,avatarBalanceQualified=False))
    print('51 native cases and 413 physical steps reconciled. Force accounting is separate from anatomy, neuronal authority and standing.',flush=True)

if __name__=='__main__':
    if not __debug__: raise RuntimeError('Assertions must remain enabled.')
    parser=argparse.ArgumentParser(); parser.add_argument('stage',choices=('prepare','preflight','check-tests','analyse')); parser.add_argument('package',nargs='?',type=Path)
    args=parser.parse_args()
    if args.stage=='prepare': prepare()
    else:
        assert args.package is not None
        {'preflight':preflight,'check-tests':check_tests,'analyse':analyse}[args.stage](args.package.resolve())

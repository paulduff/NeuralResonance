"""Review the completed exact-wire run and retained physical prefix; no model runs."""
from collections import Counter
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path
import re
import xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parents[1]


def main():
    spec=importlib.util.spec_from_file_location('capture_audit',ROOT/'tools/review-olive-and-motor-20261109.py')
    common=importlib.util.module_from_spec(spec);spec.loader.exec_module(common)
    package=ROOT/'artifacts/native-motor-bridge-20261116-bd7079';revision='46eaa518c56764bd0ac8491ac9d5c4209162f534'
    review=common.verify(package,['test-binaries.json','probe-binaries.json'],revision,'FAILED',1)
    archive=common.module('source_archive',ROOT/'tools/review-spindle-root-and-causal-motor-20261113.py')
    review['verifiedGitFiles']=archive.source_archive(common,package,revision)
    for entry in common.read(package/'player-files.json'):
        Path(entry['Path']).relative_to(package/'player');assert common.digest(entry['Path'])==entry['Sha256']
    source=common.io(package/'source/UnityWorld');compiled=0
    for path in source.rglob('*.cs'):
        assert path.read_bytes()==common.io(package/'built-project'/path.relative_to(source)).read_bytes();compiled+=1
    build=common.read(package/'player/build-result.json');assert build['result']=='Succeeded' and build['errors']==0
    config=common.read(package/'configuration.json');status=common.read(package/'window-status.json')
    assert status['PassedFocusedTests']==75 and status['UnityBuildSucceeded'] and not status['NativeBridgeChecksPassed']
    expected=Counter()
    for cls,count in dict(MusclePathGeometryTests=31,CausalMotorPlantTests=27,DynamicMuscleTests=16,SolutionManifestTests=1).items():
        code=common.io(package/'source/tests/NeuralResonanceEngine.DNNE.Tests'/(cls+'.cs')).read_text()
        found=re.findall(r'((?:\s*\[[^\n]+\])+\s*)public\s+(?:async\s+)?(?:void|Task)\s+(\w+)\(',code)
        methods={name:(1 if '[Fact]' in attrs else attrs.count('[InlineData(')) for attrs,name in found if '[Fact]' in attrs or '[Theory]' in attrs}
        assert sum(methods.values())==count
        expected.update({'NeuralResonanceEngine.DNNE.Tests.'+cls+'.'+name:n for name,n in methods.items()})
    ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
    tests=ET.parse(package/'test-results/focused.trx').findall('.//t:UnitTestResult',ns)
    assert len(tests)==len({t.attrib['testName'] for t in tests})==75 and all(t.attrib['outcome']=='Passed' for t in tests)
    assert Counter(t.attrib['testName'].split('(',1)[0] for t in tests)==expected==Counter(config['ExpectedTestMethods'])
    audit=common.module('frozen_bridge_audit',package/'source/tools/analyse-native-motor-bridge.py')
    wire=audit.audit_wire(package);print('Capture, binaries and native exact-wire evidence verified.',flush=True)
    folder=package/'native-bridge';assert sorted(p.name for p in folder.iterdir() if p.is_dir())==['left','quiet','right']
    summaries=[]
    for name,expected_steps,expected_traces in [('quiet',1024,1024),('left',1024,1024),('right',240,239)]:
        bridge=folder/name/'bridge';previous=audit.read(bridge/'hello.json');audit.state(previous);steps=0;efforts=set()
        requests=sorted(bridge.glob('request-*.json'));replies=sorted(bridge.glob('reply-*.json'));assert len(requests)==len(replies)
        for seq,path in enumerate(replies,1):
            request=audit.read(bridge/f'request-{seq:06d}.json');reply=audit.read(path)
            audit.state(reply)
            assert request['sequence']==reply['sequence']==seq and request['operation']==reply['operation']
            assert request['startTimeMs']==steps*audit.MS and request['expectedStep']==steps and request['stateHash']==previous['stateHash']
            assert request['sessionId']==reply['sessionId']==previous['sessionId']
            assert request['bodyFingerprint']==reply['bodyFingerprint']==previous['bodyFingerprint']
            if request['operation']=='step':
                steps+=1;assert request['effortId'] not in efforts;efforts.add(request['effortId'])
                assert request['endTimeMs']==steps*audit.MS and reply['lastAppliedEffortId']==request['effortId']
            else: assert reply['stateJson']==previous['stateJson'] and reply['paths']==previous['paths']
            assert reply['physicsStep']==steps and reply['physicalTimeMs']==steps*audit.MS
            previous=reply
        lines=(folder/name/'steps.jsonl').read_text().splitlines();assert steps==expected_steps and len(lines)==expected_traces
        delivered=0
        for tick,line in enumerate(lines,1):
            entry=audit.decode_wire(json.loads(line));audit.finite_tree(entry)
            assert entry['step']['after']['physicsStep']==tick
            for event in entry['step']['arrivals']:assert (tick-1)*audit.MS<event['arrivalAtBodyMs']<=tick*audit.MS
            delivered+=len(entry['step']['arrivals'])
        if name!='right':
            final=audit.read(folder/name/'final.json');assert final['plant']['completedPhysicsStep']==1024 and not final['plant']['faulted']
            assert final['arrivals']==delivered=={'quiet':0,'left':2}[name] and previous['operation']=='stop'
        else:
            assert len(replies)==724 and previous['operation']=='inspect' and not (folder/name/'final.json').exists()
            failure=common.read(folder/name/'driver-failure.json')['error']
            assert 'System.IO.IOException' in failure and 'reply-000724.json' in failure and 'being used by another process' in failure
            assert delivered==1
        summaries.append(dict(case=name,completedNativeSteps=steps,completedPlantSteps=len(lines),replies=len(replies),deliveredFixtureEvents=delivered,
            lastAngleRadians=json.loads(previous['stateJson'])['angle']))
    assert not (folder/'results.json').exists() and not (package/'bridge-analysis.json').exists()
    review.update(reviewedUtc=datetime.now(timezone.utc).isoformat(),operatorReportedCompletion=True,passedTests=75,
        compiledUnitySourceFilesVerified=compiled,wire=wire,cases=summaries,nativeSteps=2288,completedPlantSteps=2287,
        nativeReplyPreserved=True,physicalStepMustNotBeRepeated=True,lockingProcessIdentified=False,
        testsExecutedDuringReview=False,modelsSolvedDuringReview=False,originalBoundsChanged=False,
        nativeBridgeQualified=False,physiologyQualified=False,avatarRecoveryAssessed=False)
    output=ROOT/'artifacts/native-bridge-read-failure-review-20261116';output.mkdir()
    (output/'review.json').write_text(json.dumps(review,indent=2,allow_nan=False)+'\n')
    print(json.dumps(review,indent=2),flush=True)


if __name__=='__main__':main()

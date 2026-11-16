"""Review the operator-reported failed native bridge without rerunning a model."""
from collections import Counter
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path
import re
import struct
import uuid
import xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parents[1]


def main():
    spec=importlib.util.spec_from_file_location('capture_audit',ROOT/'tools/review-olive-and-motor-20261109.py')
    common=importlib.util.module_from_spec(spec);spec.loader.exec_module(common)
    package=ROOT/'artifacts/native-motor-bridge-20261116-223098';revision='3d87882a8dc2b4d83fbac76350e2f0e4cf3d20a4'
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
    metadata=common.module('frozen_metadata',package/'source/tools/unity_metadata.py').validate(package/'source/UnityWorld')
    assert metadata==common.read(package/'unity-metadata.json')
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
    bridge=package/'native-bridge/quiet/bridge';read=common.read
    assert [p.name for p in (package/'native-bridge').iterdir()]==['quiet']
    requests=sorted(bridge.glob('request-*.json'));replies=sorted(bridge.glob('reply-*.json'))
    assert len(requests)==33 and len(replies)==32
    previous=read(bridge/'hello.json');step=0;seen=set();step_ms=struct.unpack('<f',struct.pack('<f',.000125))[0]*1000
    for sequence,path in enumerate(replies,1):
        command=read(bridge/f'request-{sequence:06d}.json');reply=read(path)
        assert command['sequence']==reply['sequence']==sequence and command['operation']==reply['operation']
        assert command['expectedStep']==step and command['stateHash']==previous['stateHash']
        assert command['startTimeMs']==step*step_ms
        if command['operation']=='step':
            assert uuid.UUID(command['effortId']).int and command['effortId'] not in seen;seen.add(command['effortId']);step+=1
            assert command['endTimeMs']==step*step_ms and reply['lastAppliedEffortId']==command['effortId']
        assert reply['physicsStep']==step and reply['physicalTimeMs']==step*step_ms
        assert reply['stateJson']==previous['stateJson'] # Symmetric quiet fixture remained stationary.
        previous=reply
    failed=read(bridge/'request-000033.json');failure=read(bridge/'failure.json')
    assert step==9 and failure['physicsStep']==9 and not failure['physicsAttempted']
    assert failure['observed']['stateJson']==previous['stateJson'] and failure['sequence']==32
    assert 'Effort identity or physical endpoint differs.' in failure['error']
    assert failed['startTimeMs']==9*step_ms and failed['endTimeMs']==10*step_ms
    assert uuid.UUID(failed['effortId']).int and failed['effortId'] not in seen
    lines=(package/'native-bridge/quiet/steps.jsonl').read_text().splitlines()
    assert len(lines)==9 and all(not json.loads(line)['step']['arrivals'] for line in lines)
    assert not (package/'native-bridge/results.json').exists() and not (package/'bridge-analysis.json').exists()
    review.update(reviewedUtc=datetime.now(timezone.utc).isoformat(),operatorReportedCompletion=True,
        passedTests=75,compiledUnitySourceFilesVerified=compiled,completedQuietSteps=9,rejectedRequestSequence=33,
        rejectedBeforePhysics=True,quietFixtureStationary=True,savedRequestClockExactlyCorrect=True,
        expectedEndTimeMs=10*step_ms,expectedEndTimeBits=struct.pack('>d',10*step_ms).hex().upper(),
        freshValidEffortId=failed['effortId'],nativeDecodedRequestNotRecorded=True,
        decimalDecoderDifferenceSuspected=True,decimalDecoderCauseConfirmed=False,
        testsExecutedDuringReview=False,modelsSolvedDuringReview=False,originalBoundsChanged=False,
        nativeBridgeQualified=False,physiologyQualified=False,avatarRecoveryAssessed=False)
    output=ROOT/'artifacts/native-motor-bridge-failure-review-20261116';output.mkdir()
    (output/'review.json').write_text(json.dumps(review,indent=2,allow_nan=False)+'\n')
    print(json.dumps(review,indent=2),flush=True)


if __name__=='__main__':main()

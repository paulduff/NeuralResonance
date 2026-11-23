"""Review completed native named-path evidence; do not run physics or references."""
from collections import Counter
from datetime import datetime, timezone
from hashlib import sha256
import importlib.util
import json
from pathlib import Path
import shutil
import sys
import xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parents[1]
PACKAGE=Path('D:/DNNE-build-work/named-muscle-native-20261123-998900')
REVISION='250fb111aeef3da1195b4decf2f03144310acbd8'


def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path); value=importlib.util.module_from_spec(spec)
    spec.loader.exec_module(value); return value


def main():
    assert '--operator-reported-complete' in sys.argv and __debug__
    output=ROOT/'artifacts'/(PACKAGE.name+'-review'); assert not output.exists()
    common=module('native_named_review',ROOT/'tools/review-olive-and-motor-20261109.py')
    archive=module('native_named_archive',ROOT/'tools/review-spindle-root-and-causal-motor-20261113.py')
    result=common.verify(PACKAGE,['test-binaries.json','probe-binaries.json'],REVISION,progress_every=1000)
    result['verifiedGitFiles']=archive.source_archive(common,PACKAGE,REVISION)
    cfg=common.read(PACKAGE/'configuration.json'); status=common.read(PACKAGE/'window-status.json')
    for key in ('NativeUnityExecuted','UnityBuildSucceeded','NamedNativeAccountingPassed','AllNamedPathsResponded',
                'FrozenInputsPreserved','ManagedRuntimePreserved'):
        assert status[key],key
    assert not any(status[k] for k in ('LiveBrainAccessed','OldSavesLoaded','NeuronalMotorAuthority',
                                      'AnatomicalCalibrationQualified','AvatarBalanceQualified'))
    assert status['AvatarBuildInputChecksPassed']==cfg['AvatarBuildInputChecks']==8
    controls=(PACKAGE/'avatar-input-controls.log').read_text(encoding='utf-8-sig')
    assert 'Ran 8 tests in' in controls and controls.rstrip().endswith('OK') and 'FAILED' not in controls
    ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}; tree=ET.parse(PACKAGE/'test-results/focused.trx')
    methods={r.attrib['id']:r.find('t:TestMethod',ns).attrib for r in tree.findall('.//t:UnitTest',ns)}; actual=Counter()
    for row in tree.findall('.//t:UnitTestResult',ns):
        assert row.attrib['outcome']=='Passed'; m=methods[row.attrib['testId']]
        actual[m['className'].split(',')[0]+'.'+m['name']]+=1
    assert sum(actual.values())==status['PassedFocusedTests']==cfg['ExpectedTestCases']==359
    assert dict(actual)==cfg['ExpectedTestMethods']
    assert common.read(PACKAGE/'managed-runtime-before.json')==common.read(PACKAGE/'managed-runtime-after.json')
    build=common.read(PACKAGE/'player/build-result.json'); assert build['result']=='Succeeded' and build['errors']==0
    player_files=common.read(PACKAGE/'player-files.json')
    for entry in player_files:
        path=Path(entry['Path']); path.relative_to(PACKAGE/'player'); assert common.digest(path)==entry['Sha256']
    unity=PACKAGE/'source/UnityWorld'
    for path in unity.rglob('*.cs'):
        assert common.digest(path)==common.digest(PACKAGE/'built-project'/path.relative_to(unity))
    a=common.read(PACKAGE/'named-native-analysis.json'); force=common.read(PACKAGE/'named-native-force-checks.json')
    assert a['completed'] and a['nativeAccountingPassed'] and a['allNamedPathsResponded']
    limits=dict(forceNewtons=1e-10,pathLengthMeters=2e-12,nativePointMeters=2e-6,responseRadians=1e-6)
    assert a['limits']==cfg['NamedNativeLimits']==limits
    assert a['namedMuscles']==10 and a['points']==44 and a['physicsSteps']==96 and a['headReceipts']==324
    assert a['originalQuietMotionExact'] and a['inspectionMotionExact'] and a['footScaleIsDeclaredShinRatio']
    assert not any(a[k] for k in ('anatomicalFitQualified','independentCoupledTrajectoryQualified',
                                'neuronalSourceExecuted','muscleCellExecuted','avatarStandingQualified'))
    assert a['maximumPointErrorMeters']<=limits['nativePointMeters'] and a['maximumLengthErrorMeters']<=limits['pathLengthMeters']
    assert len(force)==3696 and max(r['errorNewtons'] for r in force)==a['maximumForceErrorNewtons']<=limits['forceNewtons']
    assert len(a['responses'])==10 and all(r['observed'] and r['maximumJointResponseRadians']>limits['responseRadians'] for r in a['responses'])
    protocol=common.read(PACKAGE/'native/protocol.json'); native=common.read(PACKAGE/'native/result.json')
    assert protocol['prescribedTensionNewtons']==40 and protocol['passivePreludeSteps']==1
    assert native['completed'] and [native[k] for k in ('nativeCases','physicsSteps','physicalEndpoints','headReceipts','extraInspections')]==[12,96,108,324,18]
    assert {r['name'] for r in a['cases']}=={r['name'] for r in protocol['cases']} and len(a['cases'])==12
    rows=lambda p:[json.loads(line) for line in p.read_text(encoding='utf-8-sig').splitlines()]
    quiet=rows(PACKAGE/'native/quiet/acknowledged.jsonl')
    old=rows(PACKAGE/'anatomical-input/native-evidence/conditions/no-joint-friction/native/quiet/acknowledged.jsonl')
    assert [r['stateJson'] for r in quiet]==[r['stateJson'] for r in old]
    for case in a['cases']:
        name=case['name']; folder=PACKAGE/'native'/name
        ack=rows(folder/'acknowledged.jsonl'); heads=rows(folder/'heads.jsonl')
        assert len(ack)==len(heads)==case['endpoints']==9 and case['physicsSteps']==8 and case['headReceipts']==27
        assert len(rows(folder/'inspections.jsonl'))==case['inspections']==(18 if name=='quiet-inspected' else 0)
        assert Counter(r['case'] for r in force)[name]==308
        for step,(endpoint,head) in enumerate(zip(ack,heads,strict=True)):
            assert endpoint['physicsStep']==head['physicsStep']==step
            assert sha256(endpoint['stateJson'].encode()).hexdigest().upper()==endpoint['stateHash']==head['bodyStateSha256']
            assert head['originalObservationsJson']==endpoint['observationsJson'] and len(head['samples'])==3
        if name=='quiet-inspected':
            assert [(r['stateJson'],r['applicationsJson']) for r in ack]==[(r['stateJson'],r['applicationsJson']) for r in quiet]
    result.update(reviewedUtc=datetime.now(timezone.utc).isoformat(),operatorReportedCompletion=True,
        passedManagedTests=359,passedBuildInputTests=8,verifiedPlayerFiles=len(player_files),nativeCases=12,
        physicsSteps=96,physicalEndpoints=108,headReceipts=324,namedMuscles=10,originalAttachments=44,pointForces=3696,
        minimumObservedResponseRadians=min(r['maximumJointResponseRadians'] for r in a['responses']),
        maximumObservedResponseRadians=max(r['maximumJointResponseRadians'] for r in a['responses']),
        namedNativeForceAccountingPassed=True,originalQuietMotionExact=True,footScaleIsDeclaredShinRatio=True,
        numericalLimitsChanged=False,modelsExecutedDuringReview=False,testsExecutedDuringReview=False,
        independentCoupledTrajectoryQualified=False,anatomicalFitQualified=False,neuronalMotorAuthority=False,avatarStandingQualified=False)
    output.mkdir();(output/'review.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
    for source,target in [('named-native-analysis.json','named-native-analysis.json'),('window-status.json','window-status.json'),
                          ('native/result.json','native-result.json'),('unity-avatar-inputs.json','unity-avatar-inputs.json'),
                          ('player/build-result.json','build-result.json')]:
        shutil.copyfile(PACKAGE/source,output/target)
    shutil.copyfile(__file__,output/Path(__file__).name)
    print(json.dumps(result,indent=2),flush=True)


if __name__=='__main__': main()

"""Read-only completed scheduling/bridge reviews. No model or test execution."""
import argparse
from collections import Counter
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path
import shutil
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
RUNS = {
    'motor-closure-20261123-ab2502': ('188aef7d6fd443c92aee351cdb6bbf8e1a9ff69c', 243, 14, 0, 229, 0, 0),
    'tendon-wire-20261123-7ce921': ('d4069ebb59b38683913554779ff2adff2d8fd591', 295, 0, 34, 261, 0, 0),
    'motor-attachment-20261123-526071': ('bd0099e9682c07f9c552420f9f937eeeaddc940f', 315, 0, 0, 295, 20, 0),
    'articulated-head-20261123-7bc04c': ('712e3251e877ccb46073d6535dda60ca9617e1cc', 331, 0, 0, 315, 0, 16),
}

def module(name, path):
    spec=importlib.util.spec_from_file_location(name,path)
    result=importlib.util.module_from_spec(spec);spec.loader.exec_module(result);return result

def review(package):
    assert package.parent == Path('D:/DNNE-build-work') and package.name in RUNS
    revision, total, schedule, bridge, previous, attachment, head = RUNS[package.name]
    output=ROOT/'artifacts'/(package.name+'-review');assert not output.exists()
    common=module('component_saved_files',ROOT/'tools/review-olive-and-motor-20261109.py')
    archive=module('component_saved_archive',ROOT/'tools/review-spindle-root-and-causal-motor-20261113.py')
    gate=module('component_saved_gate',package/'source/tools/fresh_motor_epoch_study.py')
    result=common.verify(package,['test-binaries.json','probe-binaries.json'],revision,progress_every=1000)
    result['verifiedGitFiles']=archive.source_archive(common,package,revision)
    gate.preflight(package);gate.runtime(package)  # Frozen declarations/hashes only.
    cfg=common.read(package/'configuration.json');status=common.read(package/'window-status.json')
    for key in ('NoLiveMonitoring','TestExecutionAttempted','ClockChecksPassed','FrozenInputsPreserved','ManagedRuntimePreserved','SplitScheduleChecksPassed'):
        assert status[key] is True,key
    for key in ('NativeUnityExecuted','LiveBrainAccessed','OldSavesLoaded','IndependentCoupledTrajectoryQualified','PhysiologicalFiringQualified','AvatarBalanceQualified'):
        assert status[key] is False,key
    assert status['PassedFocusedTests']==total
    if bridge or attachment or head: assert status['HostBridgeChecksPassed'] and not status['NativeFullBodyBridgeQualified']
    if attachment or head: assert status['PhysicalInitializationChecksPassed'] and not status['NativePassiveInitializationQualified']
    if head: assert status['OriginalHeadInputChecksPassed'] and not status['NativeHeadDeliveryQualified']
    assert common.digest(package/'managed-runtime-before.json')==common.digest(package/'managed-runtime-after.json')
    runtime=common.read(package/'managed-runtime-before.json')
    assert len(runtime['directories'])==2 and sum(len(d['files']) for d in runtime['directories'])==335
    ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
    tree=ET.parse(package/'test-results/focused.trx')
    definitions={r.attrib['id']:r.find('t:TestMethod',ns).attrib for r in tree.findall('.//t:UnitTest',ns)}
    actual=Counter();rows=tree.findall('.//t:UnitTestResult',ns)
    for row in rows:
        assert row.attrib['outcome']=='Passed',row.attrib
        method=definitions[row.attrib['testId']]
        actual[method['className'].split(',')[0]+'.'+method['name']]+=1
    assert len(rows)==total and dict(actual)==cfg['ExpectedTestMethods']==gate.helper.inventory()
    expected=dict(completed=True,clockComponentChecksPassed=True,passedCases=total,newClockCases=0,existingRegressionCases=previous,
        exactMethodMultiplicity=True,prescribedInputFixtures=True,oldCapturesRetimed=False,nativeUnityExecuted=False,
        independentCoupledTrajectoryQualified=False,physiologicalFiringQualified=False,avatarBalanceQualified=False,
        newScheduleCases=schedule,splitScheduleComponentChecksPassed=True,activeSensoryFeedbackQualified=False)
    if bridge or attachment or head: expected.update(newBridgeCases=bridge,hostProtocolChecksPassed=True,nativeFullBodyBridgeQualified=False)
    if attachment or head: expected.update(newAttachmentCases=attachment,physicalInitializationChecksPassed=True,nativePassiveInitializationQualified=False)
    if head: expected.update(newHeadCases=head,originalHeadInputChecksPassed=True,nativeHeadDeliveryQualified=False)
    assert common.read(package/'analysis.json')==expected
    result.update(reviewedUtc=datetime.now(timezone.utc).isoformat(),operatorReportedCompletion=True,passedTests=total,
        newScheduleCases=schedule,newBridgeCases=bridge,newAttachmentCases=attachment,newHeadCases=head,existingRegressionCases=previous,exactTestInventory=True,
        selectedManagedFiles=335,selectedManagedDirectories=2,runtimeBeforeAfterIdentical=True,
        externalRuntimeRehashedDuringReview=False,analysisSha256=common.digest(package/'analysis.json'),
        componentChecksPassed=True,testsExecutedDuringReview=False,modelsExecutedDuringReview=False,
        nativeFullBodyBridgeQualified=False,nativePassiveInitializationQualified=False,nativeHeadDeliveryQualified=False,activeSensoryFeedbackQualified=False,
        independentCoupledTrajectoryQualified=False,physiologicalFiringQualified=False,avatarBalanceQualified=False)
    output.mkdir();(output/'review.json').write_text(json.dumps(result,indent=2,allow_nan=False)+'\n',encoding='utf-8')
    for name in ('analysis.json','window-status.json'):shutil.copyfile(package/name,output/name)
    shutil.copyfile(__file__,output/Path(__file__).name)
    print(json.dumps(dict(output=str(output),verifiedFiles=result['verifiedFilesPerCopy'],passedTests=total),indent=2),flush=True)

if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    parser=argparse.ArgumentParser();parser.add_argument('package',type=Path);parser.add_argument('--operator-reported-complete',action='store_true');args=parser.parse_args()
    assert args.operator_reported_complete;review(args.package.resolve())

"""Read-only review of operator-completed actual head delivery; no model rerun."""
import argparse
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path
import shutil

ROOT=Path(__file__).resolve().parents[1]
CAPTURE=Path('D:/DNNE-build-work/native-articulated-head-20261123-e19c4a')
REVISION='619b0b38bf49735f1f21b3fa6224c65f56cd8fd1'

def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path);r=importlib.util.module_from_spec(spec);spec.loader.exec_module(r);return r

def review(package):
    assert package==CAPTURE
    output=ROOT/'artifacts'/(package.name+'-review');assert not output.exists()
    common=module('native_head_saved_files',ROOT/'tools/review-olive-and-motor-20261109.py')
    archive=module('native_head_saved_archive',ROOT/'tools/review-spindle-root-and-causal-motor-20261113.py')
    gate=module('native_head_saved_gate',package/'source/tools/native_articulated_head_study.py')
    result=common.verify(package,['probe-binaries.json'],REVISION,progress_every=1000)
    result['verifiedGitFiles']=archive.source_archive(common,package,REVISION)
    gate.preflight(package);gate.runtime(package)  # Hash/declaration review only.
    status=common.read(package/'window-status.json');analysis=common.read(package/'analysis.json')
    for key in ('NoLiveMonitoring','NativeExecutionAttempted','NativeHeadDeliveryChecksPassed','FrozenInputsPreserved','ManagedRuntimePreserved'):
        assert status[key] is True,key
    for key in ('LiveBrainAccessed','OldSavesLoaded','NeuronalSourceExecuted','MuscleCellExecuted','PhysiologicalFiringQualified','AvatarBalanceQualified'):
        assert status[key] is False,key
    assert status['ComponentCasesReused']==331
    assert common.digest(package/'managed-runtime-before.json')==common.digest(package/'managed-runtime-after.json')
    runtime=common.read(package/'managed-runtime-before.json');assert len(runtime['directories'])==2 and sum(len(d['files']) for d in runtime['directories'])==335
    for key in ('completed','nativeHeadDeliveryChecksPassed','originalQuietEndpointsIdentical','nonmovingInspectionPreserved','derivedChecksPassed'):
        assert analysis[key] is True,key
    for key in ('neuronalSourceExecuted','muscleCellExecuted','physiologicalFiringQualified','avatarBalanceQualified'):assert analysis[key] is False,key
    assert [analysis[k] for k in ('actualPhysicsSteps','physicalEndpoints','headReceipts','extraInspections','derivedChecks')]==[48,54,162,54,1296]
    ledger=common.read(package/'derived-observations.json')
    assert len(ledger)==len({r['label'] for r in ledger})==1296
    assert all(r['passed'] and 0<=r['error']<=r['allowance'] for r in ledger)
    assert analysis['maximumDerivedAllowanceFraction']==max(r['error']/r['allowance'] for r in ledger)
    acknowledgements={};head_frames={};observations=0;inspections=0
    for (name,muscle,repeat),saved in zip(gate.CASES,analysis['cases'],strict=True):
        folder=package/'native'/name;native=common.read(folder/'result.json')
        assert native['completed'] and native['physicsSteps']==8 and native['headReceipts']==27
        ack=gate.rows(folder/'acknowledged.jsonl');heads=gate.rows(folder/'heads.jsonl');inspected=gate.rows(folder/'inspections.jsonl')
        assert len(ack)==len(heads)==9 and len(inspected)==repeat*9
        assert saved==dict(name=name,physicalSteps=8,endpoints=9,headReceipts=27,inspections=repeat*9)
        wire=folder/'tendon-bridge';assert common.read(wire/'hello.json')==ack[0]
        replies={0:ack[0]}
        for path in sorted(wire.glob('reply-*.json')):
            reply=common.read(path);assert reply['sequence'] not in replies;replies[reply['sequence']]=reply
        assert len(replies)==11+repeat*9 and sorted(replies)==list(range(len(replies)))
        assert replies[len(replies)-1]['operation']=='stop' and replies[len(replies)-1]['physicsStep']==8
        assert len(list(wire.glob('request-*.json')))==len(replies)-1
        for step,(a,frame) in enumerate(zip(ack,heads,strict=True)):
            assert a==replies[a['sequence']] and a['physicsStep']==frame['physicsStep']==step
            assert gate.sha(a['stateJson'])==a['stateHash']==frame['bodyStateSha256']
            assert gate.sha(a['observationsJson'])==a['observationsHash']==frame['observationsSha256'] and frame['originalObservationsJson']==a['observationsJson']
            raw=json.loads(a['observationsJson']);assert len(raw['contactsJson'])==15 and len(frame['samples'])==len(raw['skullJson'])==3
            for sample,source in zip(frame['samples'],raw['skullJson'],strict=True):
                assert sample['originalNativeJson']==source and sample['nativeSha256']==gate.sha(source)
                receipt=sample['kinematics'];assert receipt['rawObservationSha256']==gate.sha(receipt['rawObservationJson'])
                assert receipt['physicsStep']==step and (receipt['vestibularInput'] is None)==(step==0)
                observations+=1
        for a in inspected:
            assert a==replies[a['sequence']] and a['operation']=='inspect'
            endpoint=ack[a['physicsStep']]
            assert all(a[k]==endpoint[k] for k in ('stateJson','observationsJson','applicationsJson','paths'))
            inspections+=1
        acknowledgements[name]=ack;head_frames[name]=heads
    original=gate.rows(package/'original-evidence/native/quiet-observed/endpoints.jsonl')
    assert all(a['stateJson']==b['stateJson'] for a,b in zip(acknowledgements['quiet'],original,strict=True))
    for name in ('quiet','hip','shoulder'):
        for a,b in zip(acknowledgements[name],acknowledgements[name+'-inspected'],strict=True):
            assert all(a[k]==b[k] for k in ('geometryJson','stateJson','paths','applicationsJson'))
            assert json.loads(a['observationsJson'])['skullJson']==json.loads(b['observationsJson'])['skullJson']
    assert observations==162 and inspections==54
    result.update(reviewedUtc=datetime.now(timezone.utc).isoformat(),operatorReportedCompletion=True,componentCasesReused=331,
        nativeCases=6,physicalSteps=48,physicalEndpoints=54,headReceipts=162,extraInspections=54,derivedChecks=1296,
        exactOriginalQuietReplay=True,inspectionPreserved=True,nativeHeadDeliveryChecksPassed=True,
        maximumDerivedAllowanceFraction=analysis['maximumDerivedAllowanceFraction'],selectedManagedFiles=335,
        runtimeBeforeAfterIdentical=True,externalRuntimeRehashedDuringReview=False,
        fullDerivedAnalysisRecomputedDuringReview=False,testsExecutedDuringReview=False,modelsExecutedDuringReview=False,
        physiologicalFiringQualified=False,neuronalAuthorityQualified=False,avatarBalanceQualified=False)
    output.mkdir();(output/'review.json').write_text(json.dumps(result,indent=2,allow_nan=False)+'\n',encoding='utf-8')
    for name in ('analysis.json','window-status.json'):shutil.copyfile(package/name,output/name)
    shutil.copyfile(__file__,output/Path(__file__).name)
    print(json.dumps(dict(output=str(output),verifiedFiles=result['verifiedFilesPerCopy'],nativeCases=6,headReceipts=162,derivedChecks=1296),indent=2),flush=True)

if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    p=argparse.ArgumentParser();p.add_argument('package',type=Path);p.add_argument('--operator-reported-complete',action='store_true');a=p.parse_args()
    assert a.operator_reported_complete;review(a.package.resolve())

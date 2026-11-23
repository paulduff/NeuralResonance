"""Read completed paired captures/ledgers; no force analysis or physics rerun."""
import argparse
from datetime import datetime, timezone
import hashlib
import importlib.util
import json
from pathlib import Path
import shutil
import subprocess

ROOT=Path(__file__).resolve().parents[1]
CAPTURE=Path('D:/DNNE-build-work/tendon-force-20261123-e764e3')
REVISION='740fa633dcc92a1c5c537c44ebd27e08c8ba20b2'

def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path)
    result=importlib.util.module_from_spec(spec);spec.loader.exec_module(result);return result

def review(package):
    assert package==CAPTURE
    output=ROOT/'artifacts'/(package.name+'-review');assert not output.exists()
    common=module('force_saved_files',ROOT/'tools/review-olive-and-motor-20261109.py')
    archive=module('force_saved_archive',ROOT/'tools/review-spindle-root-and-causal-motor-20261113.py')
    gate=module('force_saved_gate',package/'source/tools/native_tendon_force_study.py')
    result=common.verify(package,['test-binaries.json'],REVISION,progress_every=1000)
    result['verifiedGitFiles']=archive.source_archive(common,package,REVISION)
    gate.preflight(package);gate.h.check_tests(package)  # Read saved TRX; no test execution.
    cfg=common.read(package/'configuration.json');status=common.read(package/'window-status.json')
    assert not subprocess.check_output(['git','diff','--name-only',gate.BASE,REVISION,'--','src','Structures','tests','Shared.Contracts','tools/NativeMotorBridgeProbe'],cwd=ROOT).strip()
    assert status['PassedFocusedTests']==112 and cfg['ExpectedTestMethods']==gate.h.inventory()
    for key in ('NoLiveMonitoring','NativePhysicsAttempted','MechanicalChecksPassed','UnityBuildSucceeded'):assert status[key] is True,key
    for key in ('NeuronalSourceExecuted','AnatomicalCalibrationQualified','AvatarBalanceQualified'):assert status[key] is False,key
    runtime=common.read(package/'managed-runtime-files.json');assert len(runtime)==len({r['Path'] for r in runtime})==3099
    player=common.read(package/'player-files.json');names=set()
    for entry in player:
        path=Path(entry['Path']);relative=path.relative_to(package/'player');assert relative not in names;names.add(relative)
        assert common.digest(path)==entry['Sha256']
    assert names=={p.relative_to(package/'player') for p in (package/'player').rglob('*') if p.is_file()}
    analysis=common.read(package/'analysis.json')
    for key in ('completed','mechanicalCriteriaPassed','originalEndpointsIdentical','observationLeavesMotionUnchanged','accumulatedForceAndTorquePassed'):assert analysis[key] is True,key
    for key in ('independentCoupledTrajectoryQualified','neuronalSourceExecuted','physiologyQualified','avatarBalanceQualified'):assert analysis[key] is False,key
    endpoints=0;observed_steps=0
    for case,summary in zip(cfg['Cases'],analysis['cases'],strict=True):
        folder=package/'native'/case['name'];report=common.read(folder/'result.json');values=gate.records(folder/'endpoints.jsonl')
        old=gate.records(package/'original/native'/case['original']/'endpoints.jsonl')
        paired=gate.records(package/'native'/(case['original']+'-plain')/'endpoints.jsonl')
        assert report['completed'] and report['passed'] and not report['error'] and report['finalStep']==8
        assert report['links']==15 and report['paths']==38 and report['unity']=='6000.3.19f1'
        assert report['condition']==case['condition'] and report['muscle']==case['muscle'] and report['nativeForceObservationEnabled']==case['observe']
        assert not any(report[k] for k in ('neuronalSourceExecuted','anatomicalCalibrationQualified','avatarBalanceQualified'))
        assert summary['name']==case['name'] and summary['steps']==8 and summary['originalEndpointsIdentical'] and summary['pairedEndpointsIdentical']
        assert len(values)==len(old)==len(paired)==9
        for n,(current,prior,control) in enumerate(zip(values,old,paired,strict=True)):
            assert current['step']==n and current['physicalTimeMsBits']==prior['physicalTimeMsBits']
            assert hashlib.sha256(current['stateJson'].encode()).hexdigest().upper()==current['stateHash']
            assert json.loads(current['stateJson'])['physicsStep']==n and len(current['paths'])==38
            for key in ('stateJson','stateHash','applicationsJson','paths','leftFootForce','rightFootForce'):
                assert current[key]==prior[key]==control[key],(case['name'],n,key)
            if case['observe'] and n:
                trace=json.loads(current['nativeForcesJson']);assert trace['observationOnly'] and trace['startStep']==n-1
                assert trace['observations'][0]['phase']=='before-applications' and trace['observations'][-1]['phase']=='after-physics'
                assert all(len(s['bodies'])==15 and len(s['reducedCounteractingForceBits'])==s['nativeDofCount'] for s in trace['observations'])
                observed_steps+=1
            else:assert not current['nativeForcesJson']
        endpoints+=len(values)
    assert len(cfg['Cases'])==78 and analysis['actualPhysicsSteps']==624 and endpoints==702 and observed_steps==312
    ledger=common.read(package/'force-observations.json')
    assert len(ledger)==analysis['accumulatorChecks']==46320
    assert all(row['passed'] and 0<=row['error']<=row['allowance'] for row in ledger)
    assert analysis['failedAccumulatorChecks']==0 and analysis['maximumAllowanceFraction']==max(r['error']/r['allowance'] for r in ledger)
    responses=[dict(name=s['name'],**s['jointResponse']) for s in analysis['cases'] if s['name'].endswith('-plain') and s['jointResponse'] is not None]
    assert len(responses)==38 and all(not r['practicalControlQualified'] for r in responses)
    result.update(reviewedUtc=datetime.now(timezone.utc).isoformat(),operatorReportedCompletion=True,passedTests=112,
        exactTestInventory=True,verifiedPlayerFiles=len(player),savedRuntimeFiles=len(runtime),externalRuntimeRehashedDuringReview=False,
        originalCanonicalSourceUnchanged=True,nativeCases=78,physicalSteps=624,endpointRecords=702,observedSteps=312,
        originalEndpointsIdentical=True,observingPreservesEndpoints=True,accumulatorChecks=46320,accumulatorChecksPassed=True,
        fullForceAnalysisRecomputedDuringReview=False,maximumAllowanceFraction=analysis['maximumAllowanceFraction'],jointResponses=responses,
        analysisSha256=common.digest(package/'analysis.json'),syntheticAttachments=True,testsExecutedDuringReview=False,modelsExecutedDuringReview=False,
        usefulAllAxisControlQualified=False,anatomicalCalibrationQualified=False,neuronalAuthorityQualified=False,avatarBalanceQualified=False)
    output.mkdir();(output/'review.json').write_text(json.dumps(result,indent=2,allow_nan=False)+'\n',encoding='utf-8')
    for name in ('analysis.json','window-status.json'):shutil.copyfile(package/name,output/name)
    shutil.copyfile(__file__,output/Path(__file__).name)
    print(json.dumps(dict(output=str(output),verifiedFiles=result['verifiedFilesPerCopy'],tests=112,cases=78,steps=624,
        largestSavedJointResponses=sorted(responses,key=lambda r:r['maximumAbsoluteAngleDifferenceRadians'],reverse=True)[:4]),indent=2),flush=True)

if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    parser=argparse.ArgumentParser();parser.add_argument('package',type=Path);parser.add_argument('--operator-reported-complete',action='store_true');args=parser.parse_args()
    assert args.operator_reported_complete;review(args.package.resolve())

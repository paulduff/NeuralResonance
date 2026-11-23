"""Verify operator-completed mechanical intervention evidence; no rerun."""
import argparse
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path
import shutil

ROOT=Path(__file__).resolve().parents[1]
RUNS={'tendon-mechanics-20261123-9d73a7':('dc493d1e6483614d27f05afa3a4e60c88be87c10',False,54,432,61920),
      'tendon-friction-20261123-9d34cc':('712e3251e877ccb46073d6535dda60ca9617e1cc',True,78,624,92640)}

def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path)
    result=importlib.util.module_from_spec(spec);spec.loader.exec_module(result);return result

def review(package):
    assert package.parent==Path('D:/DNNE-build-work') and package.name in RUNS
    revision,full,case_count,step_count,force_count=RUNS[package.name]
    output=ROOT/'artifacts'/(package.name+'-review');assert not output.exists()
    common=module('mechanical_saved_files',ROOT/'tools/review-olive-and-motor-20261109.py')
    archive=module('mechanical_saved_archive',ROOT/'tools/review-spindle-root-and-causal-motor-20261113.py')
    gate=module('mechanical_saved_gate',package/'source/tools/native_tendon_mechanics_study.py')
    result=common.verify(package,['test-binaries.json'],revision,progress_every=1000)
    result['verifiedGitFiles']=archive.source_archive(common,package,revision)
    gate.preflight(package);gate.h.check_tests(package)  # Frozen files and saved TRX only.
    cfg=common.read(package/'configuration.json');status=common.read(package/'window-status.json');analysis=common.read(package/'analysis.json')
    assert cfg['Cases']==(gate.cases(True) if full else gate.cases()) and status['PassedFocusedTests']==112
    for key in ('NoLiveMonitoring','NativePhysicsAttempted','MechanicalChecksPassed','UnityBuildSucceeded'):assert status[key] is True,key
    for key in ('NeuronalSourceExecuted','AnatomicalCalibrationQualified','AvatarBalanceQualified'):assert status[key] is False,key
    for key in ('completed','mechanicalCriteriaPassed','originalEndpointsIdentical','settingsInterventionsVerified','initialStatesPreserved','accumulatedForceAndTorquePassed'):
        assert analysis[key] is True,key
    for key in ('usefulAllAxisControlQualified','neuronalSourceExecuted','physiologyQualified','avatarBalanceQualified'):assert analysis[key] is False,key
    player=common.read(package/'player-files.json');names=set()
    for entry in player:
        path=Path(entry['Path']);relative=path.relative_to(package/'player');assert relative not in names;names.add(relative)
        assert common.digest(path)==entry['Sha256']
    assert names=={p.relative_to(package/'player') for p in (package/'player').rglob('*') if p.is_file()}
    runtime=common.read(package/'managed-runtime-files.json');assert len(runtime)==len({r['Path'] for r in runtime})==3099
    total=0;endpoint_count=0;responses=[];frictions=None
    for case,summary in zip(cfg['Cases'],analysis['cases'],strict=True):
        folder=package/'native'/case['name'];report=common.read(folder/'result.json');values=gate.records(folder/'endpoints.jsonl')
        old=gate.records(package/'original/native'/case['original']/'endpoints.jsonl')
        assert report['completed'] and report['passed'] and not report['error'] and report['finalStep']==8
        assert report['links']==15 and report['paths']==38 and report['unity']=='6000.3.19f1' and report['nativeForceObservationEnabled']
        assert report['condition']==case['condition'] and report['muscle']==case['muscle']
        evidence=json.loads(report['mechanicsDiagnosticJson']);changed=gate.settings(evidence,case['mode'])
        assert summary['changedSettings']==changed and summary['mode']==case['mode'] and summary['name']==case['name']
        if frictions is None:frictions={b['id']:gate.h.real(b['friction']) for b in evidence['original']['bodies']}
        assert len(values)==len(old)==9 and summary['steps']==8 and not summary['practicalControlQualified']
        keys=('stateJson','stateHash','applicationsJson','paths','leftFootForce','rightFootForce')
        for n,(current,prior) in enumerate(zip(values,old,strict=True)):
            assert current['step']==n and current['physicalTimeMsBits']==prior['physicalTimeMsBits']
            assert gate.h.hashlib.sha256(current['stateJson'].encode()).hexdigest().upper()==current['stateHash']
            if case['mode']=='original' or n==0:assert all(current[k]==prior[k] for k in keys),(case['name'],n)
            if n:
                trace=json.loads(current['nativeForcesJson']);assert trace['observationOnly'] and trace['startStep']==n-1
                assert trace['observations'][0]['phase']=='before-applications' and trace['observations'][-1]['phase']=='after-physics'
            else:assert not current['nativeForcesJson']
        if summary['jointResponse']:responses.append(dict(mode=case['mode'],muscle=case['muscle'],**summary['jointResponse']))
        total+=8;endpoint_count+=len(values)
    ledger=common.read(package/'force-observations.json')
    assert total==analysis['actualPhysicsSteps']==step_count and endpoint_count==case_count*9 and len(responses)==case_count-(2 if full else 6)
    assert len(ledger)==analysis['accumulatorChecks']==force_count and all(r['passed'] and 0<=r['error']<=r['allowance'] for r in ledger)
    assert analysis['maximumAllowanceFraction']==max(r['error']/r['allowance'] for r in ledger)
    if full:
        limits=common.read(package/'joint-limit-observations.json')
        assert analysis['fullFrictionMatrix'] and analysis['existingJointLimitGuardPassed'] and len(limits)==analysis['jointLimitObservations']==13338
        assert all(r['passed'] and 0<=r['violationRadians']<cfg['MaximumLimitViolationRadians'] for r in limits)
        assert analysis['maximumJointLimitViolationRadians']==max(r['violationRadians'] for r in limits)
        assert len({(r['case'],r['step'],r['joint'],r['axis']) for r in limits})==len(limits)
    result.update(reviewedUtc=datetime.now(timezone.utc).isoformat(),operatorReportedCompletion=True,passedTests=112,
        verifiedPlayerFiles=len(player),savedRuntimeFiles=len(runtime),externalRuntimeRehashedDuringReview=False,
        nativeCases=case_count,physicsSteps=step_count,endpointRecords=endpoint_count,originalEndpointsIdentical=True,initialStatesPreserved=True,
        declaredInterventionsVerified=True,accumulatorChecks=force_count,accumulatorChecksPassed=True,
        fullFrictionMatrix=full,jointLimitObservations=13338 if full else None,maximumJointLimitViolationRadians=analysis.get('maximumJointLimitViolationRadians'),
        originalJointFriction=frictions,jointResponses=responses,analysisSha256=common.digest(package/'analysis.json'),
        fullForceAnalysisRecomputedDuringReview=False,testsExecutedDuringReview=False,modelsExecutedDuringReview=False,
        usefulAllAxisControlQualified=False,anatomicalCalibrationQualified=False,neuronalAuthorityQualified=False,avatarBalanceQualified=False)
    output.mkdir();(output/'review.json').write_text(json.dumps(result,indent=2,allow_nan=False)+'\n',encoding='utf-8')
    for name in ('analysis.json','window-status.json'):shutil.copyfile(package/name,output/name)
    shutil.copyfile(__file__,output/Path(__file__).name)
    print(json.dumps(dict(output=str(output),verifiedFiles=result['verifiedFilesPerCopy'],tests=112,nativeCases=case_count,steps=step_count,
        originalJointFriction=frictions,largestResponsesByMode={m:max(r['maximumAbsoluteAngleDifferenceRadians'] for r in responses if r['mode']==m) for m in (gate.MODES[:2] if full else gate.MODES)}),indent=2),flush=True)

if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    parser=argparse.ArgumentParser();parser.add_argument('package',type=Path);parser.add_argument('--operator-reported-complete',action='store_true');args=parser.parse_args()
    assert args.operator_reported_complete;review(args.package.resolve())

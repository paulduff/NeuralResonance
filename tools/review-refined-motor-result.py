"""Review completed full-matrix evidence; reuse its archived causal audit."""
import argparse
import ast
from datetime import datetime,timezone
import importlib.util
import json
import math
from pathlib import Path
import shutil
import sys

ROOT=Path(__file__).resolve().parents[1]
KEYS=['quiet','unit-left','strong-left','strong-right','strong-cut','strong-fast','strong-retry']


def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path);value=importlib.util.module_from_spec(spec);spec.loader.exec_module(value);return value


def review(package,reported):
    assert reported,'Require the operator final completion report.'
    assert package==Path('D:/DNNE-build-work/refined-motor-20261118-5bdd81')
    output=ROOT/'artifacts'/(package.name+'-review');assert not output.exists()
    files=module('refined_files',ROOT/'tools/review-olive-and-motor-20261109.py')
    archive=module('refined_archive',ROOT/'tools/review-spindle-root-and-causal-motor-20261113.py')
    cfg=files.read(package/'configuration.json');status=files.read(package/'window-status.json')
    assert cfg['Revision']=='feb510652eb26d8a3216271a5847178180b1fed4'
    result=files.verify(package,['probe-binaries.json'],cfg['Revision'],progress_every=1000)
    result['verifiedGitFiles']=archive.source_archive(files,package,cfg['Revision'])
    for row in files.read(package/'player-files.json'):
        Path(row['Path']).relative_to(package/'player');assert files.digest(row['Path'])==row['Sha256']
    for key in ('StorageControlsPassed','ModelExecutionAttempted','NativePhysicsAttempted','NativeDriverSucceeded',
                'NumericalCriteriaPassed','SavedReferenceControlsVerified','SavedReferencesReused','UnityBuildSucceeded',
                'ArchiveAccountingPassed','ReferenceEnvironmentPreserved','ManagedRuntimePreserved'):
        assert status[key] is True,key
    for key in ('LiveBrainAccessed','OldSavesLoaded','IndependentReferencesAttempted','LiveUnityIntegrated','TimingMeasured',
                'UpstreamNumericalAccuracyQualified','PhysiologyQualified','AvatarRecoveryAssessed','FullUnitSuitePassed'):
        assert status[key] is False,key
    assert status['ActualPhysicsSteps']==223972 and status['PriorFocusedTestsReused']==287 and status['PassedStorageControls']==22
    assert files.digest(package/'managed-runtime-before.json')==files.digest(package/'managed-runtime-after.json')
    sys.path.insert(0,str(package/'source/tools'))
    try:
        gate=module('refined_original_gate',package/'source/tools/refined_motor_gate.py');gate.preflight(package)
    finally:sys.path.pop(0)
    controls=files.read(package/'storage-controls/controls.json')
    tree=ast.parse((package/'source/tools/check-native-capture-store.py').read_text(encoding='utf-8-sig'))
    names=[n.args[0].value for n in ast.walk(tree) if isinstance(n,ast.Call) and isinstance(n.func,ast.Name)
        and n.func.id=='check' and n.args and isinstance(n.args[0],ast.Constant)]
    assert len(names)==len(set(names))==controls['expected']==controls['passed']==len(controls['cases'])==22
    assert {r['key'] for r in controls['cases']}==set(names) and all(r['passed'] for r in controls['cases'])
    assert controls['completed'] and controls['existingCaptureFilesRemoved']==0
    source=files.read(package/'native-source-analysis.json');spec=files.read(package/'rubral-hinge-input.json')
    prior=files.read(package/'baseline/rubral-hinge-input.json');mapping=files.read(package/'event-epoch-mappings.json')
    analysis=files.read(package/'analysis.json');owner=files.read(package/'capture-storage/ownership.json')
    view=files.read(package/'capture-storage/view.json');backup=Path(result['backup'])
    assert source['completed'] and source['sourceBodyAccountingPassed'] and source['observedNeuronalMuscleMovement']
    assert source['actualPhysicsSteps']==analysis['actualPhysicsSteps']==223972 and source['packetAndRetryStateBitwiseEqual']
    assert [c['case'] for c in source['cases']]==[c['key'] for c in spec['cases']]==[c['case'] for c in analysis['cases']]==KEYS
    assert [c['key'] for c in prior['cases']]==[c['case'] for c in mapping['cases']]==owner['conditions']==KEYS
    assert owner['rawRootAbsentAtCreation'] and not owner['existingCapturesMayBeAdopted']
    assert view['completed'] and view['allSevenConditionsArchived'] and view['actualPhysicsSteps']==223972
    assert [c['key'] for c in view['archives']]==KEYS and view['owner']==owner['owner']
    expected_events=[0,1,12,12,0,12,12];expected_selected=[0,50,230,230,230,230,230]
    assert mapping['completed'] and mapping['fullAuditsPassed']
    reports=[];members=0
    for i,(key,case,old,audit,record,epoch,archived) in enumerate(zip(KEYS,spec['cases'],prior['cases'],source['cases'],analysis['cases'],mapping['cases'],view['archives'],strict=True)):
        assert audit['selectedEvents']==expected_selected[i] and audit['motorEvents']==audit['received']==expected_events[i]
        assert audit['sourceFrames']==12000 and audit['motorRows']==15001
        assert case['physicalSteps']==4*old['physicalSteps']==31996 and old['physicalStepMs']==4*case['physicalStepMs']
        for field in ('durationMs','initialAngleRadians','initialSpeedRadiansPerSecond'):assert case[field]==old[field]
        assert epoch['physicalInputExactlyEqual'] and epoch['independentCompleteSourceAuditsRequired'] and not epoch['arrivalToleranceUsed']
        assert epoch['originalAndCurrentIdentitiesRetained']==[dict(original=a,current=b) for a,b in zip(old['events'],case['events'],strict=True)]
        for a,b in zip(old['events'],case['events'],strict=True):
            for field in ('muscleIndex','arrivalAtMs','originalSourceNeuronId'):assert a[field]==b[field]
            for field in ('originalMessageId','originalContactId'):assert a[field]!=b[field]
        assert [r['reference'] for r in record['comparisons']]==[s['key'] for s in prior['referenceSettings']]
        normalized={}
        for metric,bound in spec['candidateBounds'].items():
            assert bound==prior['candidateBounds'][metric]>0
            errors=[r['errors'][metric] for r in record['comparisons']]
            assert all(math.isfinite(v) and 0<=v<=bound for v in errors)
            normalized[metric]=max(errors)/bound
        assert normalized==record['maximumNormalizedByMetric'] and record['passed'] and all(r['passed'] for r in record['comparisons'])
        assert record['allNativeEndpointsReconciled'] and record['commonSamples']==8000
        published=files.read(package/f'capture-storage/{key}.published.json');released=files.read(package/f'capture-storage/{key}.released.json')
        closed=files.read(package/f'capture-storage/{key}.closed.json')
        for suffix,value in [('published',published),('released',released),('closed',closed)]:
            assert value==files.read(backup/f'capture-storage/{key}.{suffix}.json') and value['owner']==owner['owner'] and value['key']==key
        assert published['bothArchivesAndMembersVerified'] and released['bothArchivesAndMembersVerified']
        assert released['completed'] and released['rawInventoryExactlyVerified'] and released['originalFileBytesRetainedInBothArchives']
        assert not released['existingCapturesTouched'] and released['rawFilesRemoved']==published['files']
        assert closed['physicsSteps']==31996 and closed['nativeProcessExitedSuccessfully'] and closed['recordingScopesDisposed']
        assert closed['completionSha256']==published['completionSha256']
        assert released['archiveSha256']==published['archiveSha256']==archived['sha256']
        assert not (package/'native-bridge'/key).exists()
        # Whole archive hashes were checked in both copies above. Reuse the
        # completed member/full-causal audit; do not repeat 1.35M member decodes.
        manifest=files.read(package/'backup-manifest.json')
        entry=next(e for e in manifest['Files'] if e['Path'].replace('\\','/')==f'capture-storage/{key}.zip')
        assert entry['Sha256']==published['archiveSha256'] and entry['Bytes']==published['archiveBytes']
        members+=published['files'];worst=max(normalized,key=normalized.get)
        reports.append(dict(case=key,motorEvents=audit['motorEvents'],selectedEvents=audit['selectedEvents'],
            worstMetric=worst,maximumAllowedFraction=normalized[worst],passed=True,archivedMembers=published['files']))
    assert analysis['completed'] and analysis['numericalCriteriaPassed'] and analysis['fullOriginalConditionMatrixAssessed']
    assert analysis['sourceFrames']==84000 and analysis['independentReferencesReused']==15 and analysis['newReferenceSolves']==0
    for key in ('originalBoundsPreserved','allNativeEndpointsReconciled','commonGridAccuracyOnly','packetAndRetryStateBitwiseEqual','newCaptureArchiveAccountingPassed'):assert analysis[key]
    for key in ('finestIntermediateReferenceSamplesAssessed','upstreamNumericalAccuracyQualified','liveBrainAuthorityQualified','physiologyQualified','avatarRecoveryAssessed'):assert not analysis[key]
    result.update(reviewedUtc=datetime.now(timezone.utc).isoformat(),operatorReportedCompletion=True,
        numericalCriteriaPassed=True,fullOriginalConditionMatrixAssessed=True,actualPhysicsSteps=223972,sourceFrames=84000,
        commonSamplesPerCase=8000,independentReferencesReused=15,storageControlsPassed=22,priorComponentTestsReused=287,
        analysisSha256=files.digest(package/'analysis.json'),accountingSha256=files.digest(package/'native-source-analysis.json'),
        eventMappingsSha256=files.digest(package/'event-epoch-mappings.json'),archivedMembers=members,cases=reports,
        savedComparisonArithmeticReproduced=True,originalFullCausalAuditReused=True,archiveMemberDecodeRepeatedDuringReview=False,
        wholeArchivesVerifiedInBothCopies=True,newCapturePublicationAndOwnedReleaseChecked=True,archiveCopiesShareDrive=True,
        modelsExecutedDuringReview=False,testsExecutedDuringReview=False,referenceSolvesExecutedDuringReview=False,
        upstreamNumericalAccuracyQualified=False,liveBrainAuthorityQualified=False,physiologyQualified=False,avatarRecoveryAssessed=False)
    output.mkdir();(output/'review.json').write_text(json.dumps(result,indent=2,allow_nan=False)+'\n',encoding='utf-8')
    for name in ('analysis.json','native-source-analysis.json','event-epoch-mappings.json'):shutil.copyfile(package/name,output/name)
    shutil.copyfile(__file__,output/Path(__file__).name)
    print(json.dumps(dict(output=str(output),files=result['verifiedFilesPerCopy'],archivedMembers=members,cases=reports),indent=2),flush=True)


if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    p=argparse.ArgumentParser();p.add_argument('package',type=Path);p.add_argument('--operator-reported-complete',action='store_true')
    a=p.parse_args();review(a.package.resolve(),a.operator_reported_complete)

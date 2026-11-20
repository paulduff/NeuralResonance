"""Review reported midpoint trajectories and reconcile unchanged saved criteria.

No ODE, derivative, native model or test runs here. Full trajectory arrays are
byte-verified; their saved error envelopes are reconciled, not recomputed.
"""
import argparse
from datetime import datetime,timezone
import importlib.util
import json
import math
from pathlib import Path
import shutil
import sys

ROOT=Path(__file__).resolve().parents[1]
CAPTURE=Path('D:/DNNE-build-work/canal-midpoint-trajectory-20261119-e503fd')
REVISION='4488f9da1590efbed84c7316c03b5ec941e5a66e'


def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path)
    result=importlib.util.module_from_spec(spec);spec.loader.exec_module(result);return result


def review(package,reported):
    if not reported:raise ValueError('Require the operator final footer before reading the capture.')
    assert package==CAPTURE
    output=ROOT/'artifacts'/(package.name+'-review');assert not output.exists(),'Preserve earlier reviews.'
    common=module('canal_files',ROOT/'tools/review-olive-and-motor-20261109.py')
    archive=module('canal_git',ROOT/'tools/review-spindle-root-and-causal-motor-20261113.py')
    result=common.verify(package,['test-binaries.json','probe-binaries.json'],REVISION,progress_every=500)
    result['verifiedGitFiles']=archive.source_archive(common,package,REVISION)
    status=common.read(package/'window-status.json')
    for key in ('SampledContinuousInputAccuracyPassed','TestExecutionAttempted','FocusedTestsPassed','AnalyticControlsPassed',
                'NativeCaptureAttempted','ConnectedAccountingPassed','MatchesQualifiedManagedRuntime',
                'QualifiedDependenciesPreserved','ManagedRuntimePreserved','FrozenPackageUnchanged'):
        assert status[key] is True,key
    for key in ('ReferenceOdesRerun','OriginalBoundsChanged','EventTimeAccuracyQualified','LiveBrainAccessed','OldSavesLoaded',
                'NativeBodyExecuted','PhysiologyQualified','AvatarRecoveryAssessed','FullUnitSuitePassed'):
        assert status[key] is False,key
    assert status['IndependentReferencesReused']==6 and status['PassedFocusedTests']==172
    assert common.digest(package/'managed-runtime-before.json')==common.digest(package/'managed-runtime-after.json')
    sys.path.insert(0,str(package/'source/tools'))
    try:
        gate=module('canal_frozen_gate',package/'source/tools/canal_midpoint_trajectory_gate.py');gate.runtime(package)
        gate.original.verify_originals(common.read(package/'provenance.json'))
        tests=gate.component.test_evidence(package);partition=gate.component.partition_audit(package)
        assert tests['passedCases']==172 and tests['exactMethodMultiplicity']
        assert all(partition['checks'][k] for k in ('ReticularEqual','SpinalEqual','CanonicalDeliveriesEqual','CanonicalWaitingEqual'))
        assert common.read(package/'test-analysis.json')==dict(tests=tests,partitionComparison=partition)
        assert common.read(package/'analytic-controls.json')==dict(completed=True,passed=True,cases=24,
            methods=gate.controls_inventory(package/'source'),failures=[],errors=[],skipped=[])
        from canal_midpoint_comparison import inputs
        inputs(package) # Read-only protocol identity; never call accounting/compare here.
        from afferent_trajectory_protocol import group
        p=common.read(package/'continuous-reference-input.json');analysis=common.read(package/'continuous-comparison/analysis.json')
        assert analysis['completed'] and analysis['sampledContinuousInputAccuracyPassed']
        assert not analysis['originalBoundsChanged'] and analysis['comparedNativeSteps']==2293760
        assert not (package/'continuous-comparison/failure.json').exists()
        assert common.read(package/'analysis.json')==dict(analysis,candidateForcingPolicy='MidpointHeld',
            originalFailurePreserved=True,independentReferencesReused=6,referenceOdesRerun=False,connectedAccountingPassed=True)
        for flag in ('identicalHeldInputAccuracyQualified','eventTimeAccuracyQualified','physiologicalCalibrationQualified',
                     'liveBrainAuthorityQualified','avatarBalanceAssessed'):
            assert analysis[flag] is False,flag
        accounting=common.read(package/'accounting.json');captured=common.read(package/'study/results.json')
        expected=[(c,n) for c in p['conditions'] for n in p['connected']['sourceStepsPerInterval']]
        assert accounting['completed'] and accounting['connectedAccountingPassed'] and accounting['sourceSteps']==2293760
        assert [(r['condition'],r['sourceStepsPerInterval']) for r in accounting['runs']]==expected
        assert all(r['accountingPassed'] and r['conservationPassed'] and r['steps']==10*r['sourceStepsPerInterval']
                   and r['heldInputArithmeticMaximumFraction']<=1 for r in accounting['runs'])
        assert sum(r['steps'] for r in accounting['runs'])==2293760
        assert captured['completed'] and len(captured['runs'])==6
        for (condition,count),row in zip(expected,captured['runs'],strict=True):
            assert common.read(package/'study'/f'{condition}-n{count}'/'final.json')==row
        prior=common.read(package/'original-failed-analysis.json')
        assert not prior['sampledContinuousInputAccuracyPassed']
        assert common.read(package/'original-failed-status.json')['Status']=='FAILED'
        assert common.read(package/'continuous-reference/results.json')==dict(completed=True,attempted=6,failedRuns=[],
            comparisonsExecuted=False,coupledAccuracyQualified=False,physiologicalCalibrationQualified=False,avatarBalanceAssessed=False)
        def envelope(row,bounds,samples):
            assert row['samples']==samples and row['numericValues']==113*samples
            worst=row['worst'];absolute=bounds['absolute'][group(worst['component'])]
            measured=abs(worst['actual']-worst['expected'])/(absolute+bounds['relative']*abs(worst['expected']))
            assert measured==row['maximumAllowedFraction'] and math.isfinite(measured)
            assert row['passed']==(measured<=1)
            assert set(row['maximumAbsoluteByGroup'])==set(bounds['absolute'])
            assert all(math.isfinite(v) and v>=0 for v in row['maximumAbsoluteByGroup'].values())
        def conserved(row):
            residual=row['independentLedgerResiduals']
            assert set(residual)==set(p['ledgerResidualBounds'])
            expected=all(math.isfinite(residual[k]) and 0<=residual[k]<=v for k,v in p['ledgerResidualBounds'].items())
            assert row['conservationPassed']==expected
            return expected
        assert [c['condition'] for c in analysis['cases']]==['recorded-head','stationary-head']
        summaries=[]
        for case in analysis['cases']:
            assert [r['setting'] for r in case['references']]==['radau-standard','radau-tight','bdf-tight']
            for reference in case['references']:
                assert reference['uniqueSamples']==655361 and reference['storedSamples']==655370
                envelope(reference['denseEndpointErrors'],p['referenceComparison'],10)
                assert reference['passed']==bool(conserved(reference) and reference['precisionPassed'] and reference['denseEndpointErrors']['passed'])
            assert set(case['referenceErrors'])=={'radau-standard','bdf-tight'}
            for error in case['referenceErrors'].values():envelope(error,p['referenceComparison'],655361)
            refpass=all(r['passed'] for r in case['references']) and all(e['passed'] for e in case['referenceErrors'].values())
            assert case['referenceCriteriaPassed']==refpass
            assert [n['sourceStepsPerInterval'] for n in case['candidates']]==[16384,32768,65536]
            for candidate in case['candidates']:
                envelope(candidate['allEndpoints'],p['candidateComparison'],10*candidate['sourceStepsPerInterval'])
                envelope(candidate['commonCoarseGrid'],p['candidateComparison'],163840);conserved(candidate)
            errors=[n['commonCoarseGrid']['maximumAllowedFraction'] for n in case['candidates']]
            assert len({n['commonCoarseGrid']['gridSha256'] for n in case['candidates']})==1
            checks=[dict(coarser=a,finer=b,allowed=p['refinement']['maximumRemainingFraction']*a+p['refinement']['normalizedErrorFloor'],
                passed=b<=p['refinement']['maximumRemainingFraction']*a+p['refinement']['normalizedErrorFloor']) for a,b in zip(errors,errors[1:])]
            assert case['commonTimeRefinement']==dict(checks=checks,passed=all(c['passed'] for c in checks))
            finest=case['candidates'][-1]['allEndpoints'];assert case['finestAllEndpointCriteriaPassed']==finest['passed']
            assert case['conservationPassed']==all(n['conservationPassed'] for n in case['candidates'])
            assert case['passed']==bool(refpass and finest['passed'] and case['commonTimeRefinement']['passed'] and case['conservationPassed'])
            # Reconcile the final cumulative checkpoint with the final report.
            checkpoint=common.read(package/'continuous-comparison'/(case['condition']+'-interval-10.json'))
            assert checkpoint['completed'] and checkpoint['heldPhysicalStep']==10 and checkpoint['referenceErrors']==case['referenceErrors']
            assert checkpoint['nativeErrors']==[{k:n[k] for k in ('sourceStepsPerInterval','allEndpoints','commonCoarseGrid')} for n in case['candidates']]
            summaries.append(dict(condition=case['condition'],referenceCriteriaPassed=refpass,conservationPassed=case['conservationPassed'],
                commonGridAllowedFractions=errors,remainingErrorFractions=[b/a if a else None for a,b in zip(errors,errors[1:])],
                finestAllEndpointFraction=finest['maximumAllowedFraction'],finestWorst=finest['worst'],
                finestMaximumAbsoluteByGroup=finest['maximumAbsoluteByGroup'],passed=case['passed']))
        assert all(c['passed'] for c in analysis['cases'])
        for original,current in zip(prior['cases'],analysis['cases'],strict=True):
            assert original['condition']==current['condition']
            assert original['references']==current['references'] and original['referenceErrors']==current['referenceErrors']
        for row,original in zip(summaries,prior['cases'],strict=True):
            row['originalFinestAllEndpointFraction']=original['candidates'][-1]['allEndpoints']['maximumAllowedFraction']
            row['originalCommonGridAllowedFractions']=[n['commonCoarseGrid']['maximumAllowedFraction'] for n in original['candidates']]
    finally:sys.path.pop(0)
    result.update(reviewedUtc=datetime.now(timezone.utc).isoformat(),operatorReportedCompletion=True,originalFailurePreserved=True,
        savedReportCriteriaReconciled=True,fullTrajectoryErrorEnvelopesRecomputed=False,independentReferencesReused=6,
        connectedAccountingPassed=True,fullAccountingRecomputedDuringReview=False,passedTests=172,passedAnalyticControls=24,
        eventsByRun=[dict(condition=r['condition'],sourceStepsPerInterval=r['sourceStepsPerInterval'],events=r['events']) for r in accounting['runs']],
        comparedNativeSteps=2293760,summaries=summaries,analysisSha256=common.digest(package/'analysis.json'),continuousAnalysisSha256=common.digest(package/'continuous-comparison/analysis.json'),
        originalBoundsChanged=False,modelsExecutedDuringReview=False,testsExecutedDuringReview=False,referenceSolvesExecutedDuringReview=False,
        sampledContinuousInputAccuracyPassed=True,causalAttributionEstablished=False,eventTimeAccuracyQualified=False,
        physiologicalFiringQualified=False,liveBrainAuthorityQualified=False,physiologyQualified=False,avatarBalanceAssessed=False)
    output.mkdir();(output/'review.json').write_text(json.dumps(result,indent=2,allow_nan=False)+'\n',encoding='utf-8')
    shutil.copyfile(package/'analysis.json',output/'analysis.json')
    shutil.copyfile(package/'window-status.json',output/'window-status.json');shutil.copyfile(__file__,output/Path(__file__).name)
    print(json.dumps(dict(output=str(output),verifiedFiles=result['verifiedFilesPerCopy'],summaries=summaries),indent=2),flush=True)


if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    parser=argparse.ArgumentParser();parser.add_argument('package',type=Path);parser.add_argument('--operator-reported-complete',action='store_true');args=parser.parse_args()
    if not args.operator_reported_complete:parser.error('Wait for the final operator report; no capture opened without it.')
    review(args.package.resolve(),args.operator_reported_complete)

"""Review reported-completed sensory/motor traces; never integrate or rerun tests."""
import argparse
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path
import sys

ROOT=Path(__file__).resolve().parents[1]
def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m


def main(which,reported):
    assert reported,'An operator completion report is required.'
    names=dict(sensory=('cleft-trajectories-20261118-87562a','22148e322fe862e8b69c2f42388aa26534b8539a'),
               finer=('cleft-refinement-20261118-884934','fb61de2fd0af6b2ea01ec9b136c5e96fa0873415'),
               motor=('rubral-comparison-20261118-f5e01c','40a78fd9f2cf30d29c4d67452fc3b84f5491d935'))
    name,revision=names[which];capture=ROOT/'artifacts'/name
    out=ROOT/'artifacts'/('coupled-'+which+'-review-20261118');assert not out.exists()
    files=module('completed_files',ROOT/'tools/review-olive-and-motor-20261109.py')
    archive=module('completed_git',ROOT/'tools/review-spindle-root-and-causal-motor-20261113.py')
    result=files.verify(capture,['test-binaries.json','probe-binaries.json'] if which!='motor' else [],revision,
        'DIAGNOSTICS_COMPLETE' if which=='finer' else 'FAILED',0 if which=='finer' else 1)
    result['verifiedGitFiles']=archive.source_archive(files,capture,revision)
    sys.path.insert(0,str(capture/'source/tools'))
    import numpy as np
    out.mkdir()
    if which=='finer':
        import cleft_refinement_gate as gate
        gate.runtime(capture);tests=gate.component.inventory.check_tests(capture,gate.COUNTS)
        assert tests['passedCases']==132 and tests['passedClasses']['CleftStationarySearchTests']==12
        assert files.read(capture/'managed-runtime-before.json')==files.read(capture/'managed-runtime-after.json')
        assert files.read(capture/'reference-preflight.json')['environment']==files.read(capture/'reference-postflight.json')['environment']
        class RedirectOutput:
            def __truediv__(self,name):return (out if name=='trajectory-analysis.json' else capture)/name
        gate.compare(RedirectOutput())
        saved=files.read(capture/'trajectory-analysis.json');assert files.read(out/'trajectory-analysis.json')==saved
        assert saved['numericalTrajectoryCriteriaPassed'] and saved['newReferenceSolves']==0 and saved['reusedReferences']==21
        assert all(c['passed'] and c['overlappingMetricsExactlyReproduced'] for c in saved['cases'])
        status=files.read(capture/'window-status.json')
        assert status['PassedFocusedTests']==132 and status['StationarySearchComponentChecksPassed'] and status['SavedReferencesReused']
        assert not status['IndependentReferencesAttempted'] and not status['StationaryInitializationQualified']
        worst=max(saved['cases'],key=lambda c:c['candidates'][-1]['error']['maximumAllowedFraction'])
        result.update(passedTests=132,stationaryComponentCases=12,savedAnalysisExactlyReproduced=True,numericalCriteriaPassed=True,
            referenceCriteriaPassed=True,allSampledRefinementsPassed=True,allLedgerCriteriaPassed=True,analysis=saved,
            worst=dict(case=worst['key'],allowedFraction=worst['candidates'][-1]['error']['maximumAllowedFraction']))
    elif which=='sensory':
        import cleft_trajectory_gate as gate
        import cleft_trajectory_analysis as analysis
        gate.runtime(capture);protocol=gate.prerequisites(capture)
        tests=gate.component.inventory.check_tests(capture,gate.COUNTS);assert tests['passedCases']==120
        assert files.read(capture/'managed-runtime-before.json')==files.read(capture/'managed-runtime-after.json')
        assert files.read(capture/'reference-preflight.json')['environment']==files.read(capture/'reference-postflight.json')['environment']
        class RedirectOutput:
            def __truediv__(self,name):
                return (out if name=='trajectory-analysis.json' else capture)/name
        try:analysis.analyse(RedirectOutput(),protocol)
        except RuntimeError as error:assert str(error)=='Coupled trajectory criteria failed; retain the original bounds and all traces.'
        else:raise AssertionError('Expected preserved numerical failure.')
        saved=files.read(capture/'trajectory-analysis.json');assert files.read(out/'trajectory-analysis.json')==saved
        assert all(c['referenceCriteriaPassed'] and c['sampledRefinementPassed'] and c['ledgerCriteriaPassed'] for c in saved['cases'])
        assert [c['key'] for c in saved['cases'] if not c['passed']]==['bundle-eight']
        c=next(c for c in protocol['cases'] if c['key']=='bundle-eight');ticks,values,_=analysis.native(capture,protocol,c,1)
        with np.load(capture/'reference-trajectories/bundle-eight-radau-tight/samples.npz',allow_pickle=False) as f:reference=f['values']
        from cleft_trajectory_protocol import labels,group
        states,ledgers=labels(c['fixture']);labels_all=states+ledgers;b=protocol['candidateComparison']
        bounds=np.array([b['absolute'][group(n)] for n in labels_all])+b['relative']*np.abs(reference)
        error=np.abs(values-reference);r,col=np.unravel_index(np.argmax(error/bounds),error.shape)
        result.update(passedTests=120,savedAnalysisExactlyReproduced=True,numericalCriteriaPassed=False,
            referenceCriteriaPassed=True,failedCases=['bundle-eight'],allSampledRefinementsPassed=True,allLedgerCriteriaPassed=True,
            worst=dict(label=labels_all[col],timeMs=float(ticks[r]/protocol['ticksPerMs']),candidate=float(values[r,col]),
                       reference=float(reference[r,col]),absoluteError=float(error[r,col]),bound=float(bounds[r,col]),allowedFraction=float((error/bounds)[r,col])),
            analysis=saved)
    else:
        base=module('saved_motor_arrays',capture/'source/tools/analyse-native-hinge-comparison.py')
        helper=module('selected_motor_files',capture/'source/tools/prepare-causal-motor.py')
        prior=files.read(capture/'input-postflight.json');assert prior==files.read(capture/'input-review.json')
        assert prior['fullCaptureVerified'] and prior['verifiedFilesPerCopy']==360428 and prior['passedTests']==281
        native=Path(prior['capture'])
        assert files.digest(native/'backup-manifest.json')==files.digest(Path(prior['backup'])/'backup-manifest.json')==prior['manifestSha256']
        spec=files.read(capture/'rubral-hinge-input.json');saved=files.read(capture/'analysis.json');rows=[];worst=[]
        assert not saved['numericalCriteriaPassed'] and saved['originalBoundsPreserved'] and saved['independentReferences']==15
        selected=['native-bridge/'+c['key']+'/'+n for c in spec['cases'] for n in ('initial.json','final.json','steps.jsonl')]
        helper.verify(native,selected)
        for case in spec['cases']:
            key=case['key'];data=base.candidate_data(native/'native-bridge'/key,case,allowed_steps=(7999,));comparisons=[]
            reference_key='strong-left' if key in ('strong-fast','strong-retry') else key
            for setting in spec['referenceSettings']:
                with np.load(capture/'rubral-reference'/(reference_key+'-'+setting['key']+'.npz'),allow_pickle=False) as f:
                    assert np.array_equal(f['timeSeconds'],data['timeMs']/1000)
                    y=f['state'];errors=base.errors(data['state'],y,data['forces'],f['forces'])
                failed={k:dict(error=v,bound=spec['candidateBounds'][k]) for k,v in errors.items() if v>spec['candidateBounds'][k]}
                comparisons.append(dict(reference=setting['key'],errors=errors,failedMetrics=failed))
                if failed:
                    assert set(failed)=={'yieldFraction'}
                    d=np.abs(data['state'][:,[6,17]]-y[:,[6,17]]);r,col=np.unravel_index(np.argmax(d),d.shape)
                    worst.append(dict(case=key,reference=setting['key'],timeMs=float(data['timeMs'][r]),muscle=int(col),
                        absoluteError=float(d[r,col]),bound=spec['candidateBounds']['yieldFraction']))
            rows.append(dict(case=key,comparisons=comparisons,passed=all(base.within(c['errors'],spec['candidateBounds']) for c in comparisons)))
        assert rows==saved['cases']
        result.update(savedComparisonErrorsExactlyReproduced=True,numericalCriteriaPassed=False,analysis=saved,worst=worst,
            originalNativeFullAuditRetained=prior,originalNativeFullAuditRerun=False,selectedNativeFilesReverifiedPerCopy=len(selected),
            nativeCausalityReview=files.read(capture/'native-source-analysis.json'))
    result.update(reviewedUtc=datetime.now(timezone.utc).isoformat(),operatorReportedCompletion=True,
        originalBoundsChanged=False,modelsExecutedDuringReview=False,testsExecutedDuringReview=False,
        stationaryInitializationQualified=False,physiologyQualified=False,avatarRecoveryAssessed=False)
    (out/'review.json').write_text(json.dumps(result,indent=2,allow_nan=False)+'\n',encoding='utf-8')
    print(json.dumps(dict(review=str(out),verifiedFilesPerCopy=result['verifiedFilesPerCopy'],worst=result['worst']),indent=2),flush=True)


if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    p=argparse.ArgumentParser();p.add_argument('which',choices=['sensory','finer','motor']);p.add_argument('--operator-reported-complete',action='store_true')
    a=p.parse_args();main(a.which,a.operator_reported_complete)

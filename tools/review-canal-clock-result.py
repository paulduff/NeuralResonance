"""Review completed clock failure and localize saved trajectory errors; no integration."""
import argparse
from datetime import datetime,timezone
import gzip
import importlib.util
import json
from pathlib import Path
import shutil
import sys
import numpy as np

ROOT=Path(__file__).resolve().parents[1]


def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path);value=importlib.util.module_from_spec(spec);spec.loader.exec_module(value);return value


def review(package,reported):
    assert reported,'Wait for the operator final completion report.'
    assert package==Path('D:/DNNE-build-work/canal-clock-20261119-274f31')
    output=ROOT/'artifacts'/(package.name+'-review');assert not output.exists()
    files=module('clock_files',ROOT/'tools/review-olive-and-motor-20261109.py')
    archive=module('clock_archive',ROOT/'tools/review-spindle-root-and-causal-motor-20261113.py')
    cfg=files.read(package/'configuration.json');status=files.read(package/'window-status.json')
    assert cfg['Revision']=='d2d14605520ee2706d6a196096fc618a302be857'
    result=files.verify(package,['test-binaries.json','probe-binaries.json'],cfg['Revision'],expected_status='FAILED',expected_exit=1,progress_every=1000)
    result['verifiedGitFiles']=archive.source_archive(files,package,cfg['Revision'])
    for key in ('FocusedTestsPassed','MatchesQualifiedManagedRuntime','QualifiedDependenciesPreserved','ManagedRuntimePreserved','FrozenPackageUnchanged'):assert status[key] is True,key
    assert status['PassedFocusedTests']==121 and status['NumericalCriteriaPassed'] is False
    assert files.digest(package/'managed-runtime-before.json')==files.digest(package/'managed-runtime-after.json')
    sys.path.insert(0,str(package/'source/tools'))
    try:
        gate=module('clock_original_gate',package/'source/tools/canal_clock_gate.py');gate.runtime(package)
        tests=gate.inventory.check_tests(package,gate.COUNTS);assert tests==files.read(package/'test-analysis.json') and tests['passedCases']==121
        saved=module('clock_saved_analysis',package/'source/tools/canal_clock_analysis.py')
        analysis=saved.analyse(package);assert analysis==files.read(package/'analysis.json')
        assert not analysis['numericalCriteriaPassed'] and analysis['referenceCriteriaPassed'] and analysis['originalClockAndInputsPreserved']
        assert all(r['accountingPassed'] for r in analysis['runs'])
        spec=files.read(package/'clock-input.json');fine=spec['subdivisions'][-1];coarse=spec['subdivisions'][0]
        with np.load(package/'reference-trajectories/samples.npz',allow_pickle=False) as f:reference=f['decimal90'].copy()
        reports=[]
        for n,original in zip(spec['subdivisions'],analysis['runs'],strict=True):
            with gzip.open(package/'study'/('n'+str(n))/'trajectory.csv.gz','rt',encoding='utf-8') as stream:rows=np.loadtxt(stream,delimiter=',',skiprows=1,ndmin=2)
            states=rows[:,4:16];target=reference[::fine//n][1:]
            shared=saved.ratio(states[n//coarse-1::n//coarse],reference[::fine//coarse][1:],spec['candidateBounds'])
            endpoints=saved.ratio(states[n-1::n],reference[fine::fine],spec['candidateBounds'])
            worst=original['error']['row'];closed_step=int(rows[worst,3]);held_step=closed_step-1
            first=saved.ratio(states[::n],target[::n],spec['candidateBounds'])
            offset_windows=[]
            for start in (0,1,2,4,8,16,32,64,128):
                if start>=n:continue
                mask=np.arange(len(states))%n>=start
                offset_windows.append(dict(firstKeptSubstep=start+1,error=saved.ratio(states[mask],target[mask],spec['candidateBounds'])))
            previous=files.read(package/'study'/('n'+str(n))/f'endpoint-{held_step-1:03}.json')['input']['projection']['components']
            held=files.read(package/'study'/('n'+str(n))/f'endpoint-{held_step:03}.json')['input']['projection']['components']
            report=dict(subdivisions=n,allSampleError=original['error'],commonCoarseGridError=shared,physicalEndpointError=endpoints,
                firstSubstepError=first,worstClosingPhysicalStep=closed_step,worstHeldInputStep=held_step,
                worstSubstepWithinInterval=int(rows[worst,0])-held_step*n,
                microsecondsAfterInputAvailable=(spec['frame']['clock']['physicsStepMs']*1000/n)*(int(rows[worst,0])-held_step*n),
                previousInput=previous,heldInput=held,tailWindows=offset_windows)
            reports.append(report)
    finally:sys.path.pop(0)
    diagnosis=dict(completed=True,savedSamplesOnly=True,originalFailurePreserved=True,originalBoundsChanged=False,
        commonGridIsDiagnosticOnly=True,allSampleAccuracyStillRequired=True,runs=reports,
        modelsExecuted=False,testsExecuted=False,referenceSolvesExecuted=False)
    result.update(reviewedUtc=datetime.now(timezone.utc).isoformat(),operatorReportedCompletion=True,originalFailurePreserved=True,
        savedAnalysisExactlyReproduced=True,analysisSha256=files.digest(package/'analysis.json'),passedTests=121,
        nativeSteps=356608,referenceSamples=203777,referenceCriteriaPassed=True,allClockAndAccountingChecksPassed=True,
        numericalCriteriaPassed=False,sampledRefinementPassed=False,originalBoundsChanged=False,
        modelsExecutedDuringReview=False,testsExecutedDuringReview=False,referenceSolvesExecutedDuringReview=False,
        archiveCopiesShareDrive=True,continuousMotionAccuracyQualified=False,anatomicalCalibrationQualified=False,
        afferentFiringQualified=False,avatarBalanceAssessed=False)
    output.mkdir()
    for name,value in (('review.json',result),('analysis.json',analysis),('transient-diagnosis.json',diagnosis)):
        (output/name).write_text(json.dumps(value,indent=2,allow_nan=False)+'\n',encoding='utf-8')
    shutil.copyfile(__file__,output/Path(__file__).name)
    print(json.dumps(dict(output=str(output),verifiedFiles=result['verifiedFilesPerCopy'],runs=[{k:r[k] for k in
        ('subdivisions','allSampleError','commonCoarseGridError','physicalEndpointError','worstHeldInputStep','worstSubstepWithinInterval','microsecondsAfterInputAvailable')} for r in reports]),indent=2),flush=True)


if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    p=argparse.ArgumentParser();p.add_argument('package',type=Path);p.add_argument('--operator-reported-complete',action='store_true');a=p.parse_args()
    review(a.package.resolve(),a.operator_reported_complete)

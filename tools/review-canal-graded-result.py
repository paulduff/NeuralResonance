"""Completed graded clock evidence only; no model, test or reference solve."""
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
    assert package==Path('D:/DNNE-build-work/canal-graded-20261119-7880d2')
    output=ROOT/'artifacts'/(package.name+'-review');assert not output.exists()
    files=module('graded_files',ROOT/'tools/review-olive-and-motor-20261109.py')
    archive=module('graded_archive',ROOT/'tools/review-spindle-root-and-causal-motor-20261113.py')
    cfg=files.read(package/'configuration.json');status=files.read(package/'window-status.json')
    assert cfg['Revision']=='b06a811b57d11a5d94adac2c73ae8883889525a6'
    result=files.verify(package,['test-binaries.json','probe-binaries.json'],cfg['Revision'],progress_every=1000)
    result['verifiedGitFiles']=archive.source_archive(files,package,cfg['Revision'])
    for key in ('FocusedTestsPassed','MatchesQualifiedManagedRuntime','QualifiedDependenciesPreserved','ManagedRuntimePreserved',
        'FrozenPackageUnchanged','NumericalCriteriaPassed','ClockedCanalChecksPassed'):assert status[key] is True,key
    for key in ('MechanicalCalibrationQualified','StationaryInitializationQualified','SpatialGeometryCalibrated','AfferentFiringQualified',
        'LiveBrainAccessed','OldSavesLoaded','NativeBodyExecuted','PhysiologyQualified','AvatarRecoveryAssessed','FullUnitSuitePassed'):assert status[key] is False,key
    assert status['PassedFocusedTests']==156
    assert files.digest(package/'managed-runtime-before.json')==files.digest(package/'managed-runtime-after.json')
    sys.path.insert(0,str(package/'source/tools'))
    try:
        gate=module('graded_original_gate',package/'source/tools/canal_graded_gate.py');gate.runtime(package)
        tests=gate.inventory.check_tests(package,gate.COUNTS);assert tests==files.read(package/'test-analysis.json') and tests['passedCases']==156
        saved=module('graded_saved_analysis',package/'source/tools/canal_graded_analysis.py')
        analysis=saved.analyse(package);assert analysis==files.read(package/'analysis.json')
        assert analysis['numericalCriteriaPassed'] and analysis['referenceCriteriaPassed'] and analysis['sampledRefinementPassed']
        assert analysis['originalClockAndInputsPreserved'] and analysis['allNativeEndpointsCompared'] and all(r['accountingPassed'] for r in analysis['runs'])
        spec=files.read(package/'graded-input.json')
        with np.load(package/'reference-trajectories/samples.npz',allow_pickle=False) as f:
            reference=f['decimal90'].copy();ref_ticks=f['ticks'].copy()
        reports=[]
        for r in analysis['runs']:
            refinement=r['refinement'];n=r['substepsPerInterval'];interval_ticks=r['ticksPerInterval']
            with gzip.open(package/'study'/('r'+str(refinement))/'trajectory.csv.gz','rt',encoding='utf-8') as stream:
                rows=np.loadtxt(stream,delimiter=',',skiprows=1,ndmin=2)
            target_ticks=(rows[:,0].astype(np.int64)-interval_ticks)*(4//refinement)
            indices=np.searchsorted(ref_ticks,target_ticks);assert np.array_equal(ref_ticks[indices],target_ticks)
            target=reference[indices];states=rows[:,4:16]
            reports.append(dict(refinement=refinement,substepsPerInterval=n,allSampleError=r['error'],
                firstSubstepError=saved.ratio(states[::n],target[::n],spec['candidateBounds']),
                physicalEndpointError=saved.ratio(states[n-1::n],target[n-1::n],spec['candidateBounds']),
                firstSubstepMicroseconds=spec['frame']['clock']['physicsStepMs']*1000/interval_ticks))
    finally:sys.path.pop(0)
    result.update(reviewedUtc=datetime.now(timezone.utc).isoformat(),operatorReportedCompletion=True,
        savedAnalysisExactlyReproduced=True,analysisSha256=files.digest(package/'analysis.json'),passedTests=156,
        nativeSteps=491729,referenceSamples=459293,referenceCriteriaPassed=True,allClockAndAccountingChecksPassed=True,
        numericalCriteriaPassed=True,sampledRefinementPassed=True,originalBoundsChanged=False,
        originalUniformFailurePreserved=True,modelsExecutedDuringReview=False,testsExecutedDuringReview=False,referenceSolvesExecutedDuringReview=False,
        archiveCopiesShareDrive=True,continuousMotionAccuracyQualified=False,anatomicalCalibrationQualified=False,
        afferentFiringQualified=False,avatarBalanceAssessed=False)
    output.mkdir()
    for name,value in (('review.json',result),('analysis.json',analysis),('boundary-review.json',dict(runs=reports,savedSamplesOnly=True,modelsExecuted=False))):
        (output/name).write_text(json.dumps(value,indent=2,allow_nan=False)+'\n',encoding='utf-8')
    shutil.copyfile(__file__,output/Path(__file__).name)
    print(json.dumps(dict(output=str(output),verifiedFiles=result['verifiedFilesPerCopy'],runs=reports),indent=2),flush=True)


if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    p=argparse.ArgumentParser();p.add_argument('package',type=Path);p.add_argument('--operator-reported-complete',action='store_true');a=p.parse_args()
    review(a.package.resolve(),a.operator_reported_complete)

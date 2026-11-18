"""Review the reported loaded-rest capture using saved states only."""
import argparse
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path
import shutil
import sys

ROOT=Path(__file__).resolve().parents[1]


def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path);value=importlib.util.module_from_spec(spec);spec.loader.exec_module(value);return value


def review(package,reported):
    assert reported,'Wait for the final operator report.'
    assert package==Path('D:/DNNE-build-work/afferent-loaded-20261118-cd9fba')
    output=ROOT/'artifacts'/(package.name+'-review');assert not output.exists()
    files=module('loaded_files',ROOT/'tools/review-olive-and-motor-20261109.py')
    archive=module('loaded_archive',ROOT/'tools/review-spindle-root-and-causal-motor-20261113.py')
    cfg=files.read(package/'configuration.json');status=files.read(package/'window-status.json')
    result=files.verify(package,['probe-binaries.json'],cfg['Revision'],progress_every=1000)
    result['verifiedGitFiles']=archive.source_archive(files,package,cfg['Revision'])
    assert cfg['Revision']=='8c9b28cce57660f9402c2dcea53ee9da33a648a3'
    for key in ('NumericalCriteriaPassed','LoadedRestTrajectoryChecksPassed','MatchesQualifiedManagedRuntime',
                'QualifiedDependenciesPreserved','ManagedRuntimePreserved','FrozenPackageUnchanged'):
        assert status[key] is True,key
    for key in ('MechanicalCalibrationQualified','StationaryInitializationQualified','SpatialGeometryCalibrated',
                'AfferentFiringQualified','LiveBrainAccessed','OldSavesLoaded','NativeBodyExecuted',
                'PhysiologyQualified','AvatarRecoveryAssessed','FullUnitSuitePassed'):
        assert status[key] is False,key
    assert status['ComponentTestsReused']==333
    assert files.digest(package/'managed-runtime-before.json')==files.digest(package/'managed-runtime-after.json')
    sys.path.insert(0,str(package/'source/tools'))
    try:
        gate=module('loaded_original_gate',package/'source/tools/afferent_loaded_gate.py');gate.runtime(package)
        analysis_module=module('loaded_saved_analysis',package/'source/tools/afferent_loaded_analysis.py')
        spec=files.read(package/'trajectory-input.json');source=files.read(package/'source-decisions.json')
        assert files.read(package/'study/activation-continuation.json')==dict(version=gate.previous.MODEL,policy='EndpointHold',
            originalDefaultChanged=False,physiologicalCalibrationQualified=False)
        analysis=analysis_module.analyse(package,spec,source)
        observations=analysis_module.observations(package,spec,source)
        assert analysis==files.read(package/'trajectory-analysis.json')
        assert observations==files.read(package/'rest-observations.json')
    finally:
        sys.path.pop(0)
    assert analysis['numericalTrajectoryCriteriaPassed'] and observations['quietDriftPassed']
    assert len(analysis['cases'])==4 and analysis['nativeRuns']==analysis['references']==12
    # Each trajectory retains its initial state as well as every advanced state.
    retained_rows=sum(v['rows'] for c in analysis['cases'] for v in c['candidates'])
    steps=retained_rows-analysis['nativeRuns'];assert steps==114688 and retained_rows==114700
    cases=[dict(key=c['key'],passed=c['passed'],errorFractions=[v['error']['maximumAllowedFraction'] for v in c['candidates']],
        referenceCriteriaPassed=c['referenceCriteriaPassed'],sampledRefinementPassed=c['sampledRefinementPassed'],
        ledgerCriteriaPassed=c['ledgerCriteriaPassed']) for c in analysis['cases']]
    quiet_max={k:max(row['maximumDistanceFromQuietRoot'][k] for row in observations['quietRuns'])
        for k in observations['quietDriftBounds']}
    result.update(reviewedUtc=datetime.now(timezone.utc).isoformat(),operatorReportedCompletion=True,
        savedAnalysisExactlyReproduced=True,savedObservationsExactlyReproduced=True,numericalTrajectoryCriteriaPassed=True,
        analysisSha256=files.digest(package/'trajectory-analysis.json'),observationsSha256=files.digest(package/'rest-observations.json'),
        componentTestsReused=333,nativeRuns=12,references=12,nativeSteps=steps,retainedNativeRows=retained_rows,durationMs=4.,quietDriftPassed=True,
        maximumQuietDrift=quiet_max,cases=cases,modelsExecutedDuringReview=False,testsExecutedDuringReview=False,
        referenceSolvesExecutedDuringReview=False,originalBoundsChanged=False,stationaryInitializationQualified=False,
        physiologicalRestQualified=False,afferentFiringQualified=False,avatarRecoveryAssessed=False,archiveCopiesShareDrive=True)
    output.mkdir()
    for name,value in (('review.json',result),('analysis.json',analysis),('observations.json',observations)):
        (output/name).write_text(json.dumps(value,indent=2,allow_nan=False)+'\n',encoding='utf-8')
    shutil.copyfile(__file__,output/Path(__file__).name)
    print(json.dumps(dict(output=str(output),verifiedFiles=result['verifiedFilesPerCopy'],cases=cases,nativeSteps=steps,
        maximumQuietDrift=quiet_max,quietDriftPassed=True),indent=2),flush=True)


if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    parser=argparse.ArgumentParser();parser.add_argument('package',type=Path);parser.add_argument('--operator-reported-complete',action='store_true')
    args=parser.parse_args();review(args.package.resolve(),args.operator_reported_complete)

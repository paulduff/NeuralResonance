"""Review completed canal evidence; no native, reference or control rerun."""
import argparse
from datetime import datetime, timezone
from decimal import Decimal
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
    assert package.parent==Path('D:/DNNE-build-work') and package.name.startswith('canal-trajectories-')
    output=ROOT/'artifacts'/(package.name+'-review');assert not output.exists()
    files=module('published_canal_files',ROOT/'tools/review-olive-and-motor-20261109.py')
    archive=module('published_canal_source',ROOT/'tools/review-spindle-root-and-causal-motor-20261113.py')
    cfg=files.read(package/'configuration.json');status=files.read(package/'window-status.json')
    result=files.verify(package,['probe-binaries.json'],cfg['Revision'],progress_every=1000)
    result['verifiedGitFiles']=archive.source_archive(files,package,cfg['Revision'])
    for key in ('NumericalCriteriaPassed','ReferenceControlsAttempted','PublishedCanalTrajectoryChecksPassed',
                'MatchesQualifiedManagedRuntime','QualifiedDependenciesPreserved','ManagedRuntimePreserved','FrozenPackageUnchanged'):
        assert status[key] is True,key
    for key in ('SourceToSkullFrameQualified','LocalBundleMapQualified','MechanicalCalibrationQualified',
                'StationaryInitializationQualified','SpatialGeometryCalibrated','AfferentFiringQualified','LiveBrainAccessed',
                'OldSavesLoaded','NativeBodyExecuted','PhysiologyQualified','AvatarRecoveryAssessed','FullUnitSuitePassed'):
        assert status[key] is False,key
    assert status['ComponentTestsReused']==76 and status['PassedReferenceControls']==6
    assert files.digest(package/'managed-runtime-before.json')==files.digest(package/'managed-runtime-after.json')
    controls=files.read(package/'reference-controls/controls.json')
    assert controls['completed'] and controls['expected']==controls['passed']==6 and not controls['physicalModelExecuted']
    assert [row['key'] for row in controls['cases']]==['zero','diagonal','nilpotent','damped-jordan','stiff-decay','constant-forcing']
    assert all(row['passed'] and all(Decimal(error)<=Decimal('1e-45') for error in row['absoluteErrors']) for row in controls['cases'])
    sys.path.insert(0,str(package/'source/tools'))
    try:
        gate=module('published_canal_gate',package/'source/tools/canal_trajectory_gate.py');gate.runtime(package)
        analysis_module=module('published_canal_saved_analysis',package/'source/tools/coupled_canal_analysis.py')
        analysis=analysis_module.analyse(package,gate.protocol())
        assert analysis==files.read(package/'trajectory-analysis.json')
        assert analysis['completed'] and analysis['numericalCriteriaPassed'] and len(analysis['cases'])==15
    finally:
        sys.path.pop(0)
    cases=[dict(key=row['key'],passed=row['passed'],errorFractions=[v['comparison']['maximum'] for v in row['candidates']],
        referenceCriteriaPassed=row['reference']['passed'],refinementPassed=row['refinementPassed']) for row in analysis['cases']]
    steps=sum(value['accounting']['rows'] for row in analysis['cases'] for value in row['candidates'])
    reference_points=sum(row['reference']['samples'] for row in analysis['cases'])
    assert steps==1020600 and reference_points==583215
    result.update(reviewedUtc=datetime.now(timezone.utc).isoformat(),operatorReportedCompletion=True,
        savedAnalysisExactlyReproduced=True,numericalCriteriaPassed=True,passedReferenceControls=6,priorComponentTestsReused=76,
        nativeRuns=45,referenceCases=15,referenceCalculations=45,nativeSteps=steps,referencePointsPerMethod=reference_points,
        allNativeEndpointsCompared=True,sourceSpecimenOnly=True,sourceToSkullFrameQualified=False,localBundleMapQualified=False,
        physiologyQualified=False,avatarRecoveryAssessed=False,modelsExecutedDuringReview=False,testsExecutedDuringReview=False,
        archiveCopiesShareDrive=True,analysisSha256=files.digest(package/'trajectory-analysis.json'),cases=cases)
    output.mkdir()
    for name,data in (('review.json',result),('analysis.json',analysis)):
        (output/name).write_text(json.dumps(data,indent=2,allow_nan=False)+'\n',encoding='utf-8')
    shutil.copyfile(__file__,output/Path(__file__).name)
    print(json.dumps(dict(output=str(output),verifiedFiles=result['verifiedFilesPerCopy'],casesPassed=len(cases),nativeSteps=steps,
        maximumFinestErrorFraction=max(row['errorFractions'][-1] for row in cases),savedAnalysisExactlyReproduced=True),indent=2),flush=True)


if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    parser=argparse.ArgumentParser();parser.add_argument('package',type=Path);parser.add_argument('--operator-reported-complete',action='store_true')
    args=parser.parse_args();review(args.package.resolve(),args.operator_reported_complete)

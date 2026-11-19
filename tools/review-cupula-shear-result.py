"""Review completed spatial component controls, never a live study."""
import argparse
from datetime import datetime,timezone
import importlib.util
import json
from pathlib import Path
import shutil
import sys

ROOT=Path(__file__).resolve().parents[1]


def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path);value=importlib.util.module_from_spec(spec);spec.loader.exec_module(value);return value


def review(package,reported):
    assert reported,'Wait for the operator final footer.'
    assert package==Path('D:/DNNE-build-work/cupula-shear-20261119-8ec244')
    output=ROOT/'artifacts'/(package.name+'-review');assert not output.exists()
    files=module('shear_files',ROOT/'tools/review-olive-and-motor-20261109.py')
    archive=module('shear_archive',ROOT/'tools/review-spindle-root-and-causal-motor-20261113.py')
    cfg=files.read(package/'configuration.json');status=files.read(package/'window-status.json')
    assert cfg['Revision']=='ed357966062a5d96de4519face2a8060ba0aab23'
    result=files.verify(package,['test-binaries.json','probe-binaries.json'],cfg['Revision'],progress_every=1000)
    result['verifiedGitFiles']=archive.source_archive(files,package,cfg['Revision'])
    for key in ('FocusedTestsPassed','CupulaShearComponentChecksPassed','MatchesQualifiedManagedRuntime',
        'QualifiedDependenciesPreserved','ManagedRuntimePreserved','FrozenPackageUnchanged'):assert status[key] is True,key
    for key in ('IndependentTrajectoryCriteriaPassed','MechanicalCalibrationQualified','StationaryInitializationQualified',
        'SpatialGeometryCalibrated','AfferentFiringQualified','LiveBrainAccessed','OldSavesLoaded','NativeBodyExecuted',
        'PhysiologyQualified','AvatarRecoveryAssessed','FullUnitSuitePassed'):assert status[key] is False,key
    assert status['PassedFocusedTests']==179
    assert files.digest(package/'managed-runtime-before.json')==files.digest(package/'managed-runtime-after.json')
    sys.path.insert(0,str(package/'source/tools'))
    try:
        gate=module('shear_original_gate',package/'source/tools/cupula_shear_gate.py');gate.runtime(package)
        tests=gate.inventory.check_tests(package,gate.COUNTS);assert tests==files.read(package/'test-analysis.json') and tests['passedCases']==179
    finally:sys.path.pop(0)
    analysis=files.read(package/'analysis.json')
    assert analysis==dict(completed=True,componentCriteriaPassed=True,passedCases=179,
        independentTrajectoryComparisonExecuted=False,coupledSensoryAccuracyQualified=False,anatomicalCalibrationQualified=False,
        physiologicalBundleMapQualified=False,afferentFiringQualified=False,avatarBalanceAssessed=False)
    result.update(reviewedUtc=datetime.now(timezone.utc).isoformat(),operatorReportedCompletion=True,componentCriteriaPassed=True,
        passedTests=179,newSpatialComponentTests=23,priorComponentTests=156,exactTestInventory=True,
        canonicalAndExportedCataloguesPreserved=True,modelsExecutedDuringReview=False,testsExecutedDuringReview=False,
        independentTrajectoryComparisonExecuted=False,anatomicalCalibrationQualified=False,physiologicalBundleMapQualified=False,
        coupledSensoryAccuracyQualified=False,afferentFiringQualified=False,avatarBalanceAssessed=False,archiveCopiesShareDrive=True)
    output.mkdir()
    for name,value in (('review.json',result),('analysis.json',analysis)):
        (output/name).write_text(json.dumps(value,indent=2,allow_nan=False)+'\n',encoding='utf-8')
    shutil.copyfile(__file__,output/Path(__file__).name)
    print(json.dumps(dict(output=str(output),verifiedFiles=result['verifiedFilesPerCopy'],passedCases=179),indent=2),flush=True)


if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    p=argparse.ArgumentParser();p.add_argument('package',type=Path);p.add_argument('--operator-reported-complete',action='store_true');a=p.parse_args()
    review(a.package.resolve(),a.operator_reported_complete)

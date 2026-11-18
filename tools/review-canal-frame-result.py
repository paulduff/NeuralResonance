"""Review completed frame evidence with scalar indexing repaired; no model/solve/test rerun."""
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
    assert package==Path('D:/DNNE-build-work/canal-frame-20261118-a80ee0')
    output=ROOT/'artifacts'/(package.name+'-review');assert not output.exists()
    files=module('frame_files',ROOT/'tools/review-olive-and-motor-20261109.py')
    archive=module('frame_archive',ROOT/'tools/review-spindle-root-and-causal-motor-20261113.py')
    cfg=files.read(package/'configuration.json');status=files.read(package/'window-status.json')
    result=files.verify(package,['test-binaries.json','probe-binaries.json'],cfg['Revision'],
        expected_status='FAILED',expected_exit=1,progress_every=1000)
    result['verifiedGitFiles']=archive.source_archive(files,package,cfg['Revision'])
    assert cfg['Revision']=='ddf0efb1c49b7cf38e6118711020a5bb52b0704c'
    for key in ('FocusedTestsPassed','MatchesQualifiedManagedRuntime','QualifiedDependenciesPreserved',
                'ManagedRuntimePreserved','FrozenPackageUnchanged'):
        assert status[key] is True,key
    assert status['PassedFocusedTests']==100 and status['AngularInputChecksPassed'] is False
    assert 'ValueError: sign must be an integer with the value 0 or 1' in (package/'reference-compare.log').read_text(encoding='utf-8-sig')
    assert not (package/'analysis.json').exists()
    assert files.digest(package/'managed-runtime-before.json')==files.digest(package/'managed-runtime-after.json')

    # Prove the comparator change is only per-component indexing plus a separate
    # output destination. Never rewrite the failed package or its saved status.
    old=(package/'source/tools/canal_frame_reference.py').read_text(encoding='utf-8-sig')
    expected=old.replace('def compare(package):','def compare(package, *, output_path=None):')
    assert expected.count(",expected,'1e-")==2
    expected=expected.replace(",expected,'1e-",",expected[j],'1e-")
    expected=expected.replace("save(package/'analysis.json',output);return output",
        "save(package/'analysis.json' if output_path is None else output_path,output);return output")
    corrected=ROOT/'tools/canal_frame_reference.py'
    assert corrected.read_text(encoding='utf-8-sig')==expected
    preserved_status=files.digest(package/'window-status.json')
    sys.path.insert(0,str(package/'source/tools'))
    try:
        gate=module('frame_original_gate',package/'source/tools/canal_frame_gate.py')
        gate.runtime(package)
        tests=gate.inventory.check_tests(package,gate.COUNTS)
        assert tests==files.read(package/'test-analysis.json') and tests['passedCases']==100
    finally:
        sys.path.pop(0)
    repaired=module('frame_corrected_comparison',corrected)
    output.mkdir()
    analysis=repaired.compare(package,output_path=output/'analysis.json')
    assert analysis['angularInputChecksPassed'] and analysis['independentAngularSamples']==600
    assert files.digest(package/'window-status.json')==preserved_status and not (package/'analysis.json').exists()
    result.update(reviewedUtc=datetime.now(timezone.utc).isoformat(),operatorReportedCompletion=True,
        originalStatus='FAILED',originalStatusPreserved=True,originalStatusSha256=preserved_status,
        comparatorDefect='three-component reference list passed to Decimal instead of indexed scalar',
        onlyScalarIndexingAndOutputDestinationChanged=True,accuracyBoundsUnchanged=True,
        repairedSavedComparisonPassed=True,focusedTestsPassed=100,referenceControlsPassed=6,
        originalReceiptReplayCount=603,independentAngularSamples=600,
        maximumAllowedFractions=analysis['maximumAllowedFractions'],maximumNativeBackwardError=analysis['maximumNativeBackwardError'],
        originalComparatorSha256=files.digest(package/'source/tools/canal_frame_reference.py'),correctedComparatorSha256=files.digest(corrected),
        modelsExecutedDuringReview=False,testsExecutedDuringReview=False,referenceSolvesExecutedDuringReview=False,
        mechanicalClockCouplingQualified=False,anatomicalCalibrationQualified=False,afferentFiringQualified=False,avatarBalanceAssessed=False,
        archiveCopiesShareDrive=True)
    (output/'review.json').write_text(json.dumps(result,indent=2,allow_nan=False)+'\n',encoding='utf-8')
    shutil.copyfile(corrected,output/corrected.name);shutil.copyfile(__file__,output/Path(__file__).name)
    print(json.dumps(dict(output=str(output),verifiedFiles=result['verifiedFilesPerCopy'],tests=100,angularSamples=600,
        comparisonPassed=True,maximumAllowedFractions=analysis['maximumAllowedFractions']),indent=2),flush=True)


if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    parser=argparse.ArgumentParser();parser.add_argument('package',type=Path);parser.add_argument('--operator-reported-complete',action='store_true')
    args=parser.parse_args();review(args.package.resolve(),args.operator_reported_complete)

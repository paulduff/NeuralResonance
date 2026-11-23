"""Read-only review of the operator-reported fresh motor epoch regressions."""
import argparse
from collections import Counter
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path
import shutil
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
CAPTURE = Path('D:/DNNE-build-work/motor-epoch-20261123-6889f0')
REVISION = '07294c30fce30ea0ef0a2b465aa91964a3519f88'


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec); spec.loader.exec_module(result)
    return result


def review(package):
    assert package == CAPTURE
    output = ROOT/'artifacts'/(package.name+'-review'); assert not output.exists()
    common = module('clock_saved_files', ROOT/'tools/review-olive-and-motor-20261109.py')
    archive = module('clock_saved_git', ROOT/'tools/review-spindle-root-and-causal-motor-20261113.py')
    gate = module('clock_saved_gate', package/'source/tools/fresh_motor_epoch_study.py')
    result = common.verify(package, ['test-binaries.json', 'probe-binaries.json'], REVISION, progress_every=1000)
    result['verifiedGitFiles'] = archive.source_archive(common, package, REVISION)
    gate.preflight(package); gate.runtime(package)  # Hashes/declarations only.
    cfg = common.read(package/'configuration.json'); status = common.read(package/'window-status.json')
    assert status['PassedFocusedTests'] == 155
    for key in ('NoLiveMonitoring','TestExecutionAttempted','ClockChecksPassed','FrozenInputsPreserved','ManagedRuntimePreserved'):
        assert status[key] is True, key
    for key in ('NativeUnityExecuted','LiveBrainAccessed','OldSavesLoaded','IndependentCoupledTrajectoryQualified','PhysiologicalFiringQualified','AvatarBalanceQualified'):
        assert status[key] is False, key
    assert common.digest(package/'managed-runtime-before.json') == common.digest(package/'managed-runtime-after.json')
    runtime = common.read(package/'managed-runtime-before.json')
    assert len(runtime['directories']) == 2 and sum(len(d['files']) for d in runtime['directories']) == 335
    ns = {'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
    tree = ET.parse(package/'test-results/focused.trx')
    definitions = {r.attrib['id']:r.find('t:TestMethod', ns).attrib for r in tree.findall('.//t:UnitTest', ns)}
    rows = tree.findall('.//t:UnitTestResult', ns); actual = Counter()
    for row in rows:
        assert row.attrib['outcome'] == 'Passed', row.attrib
        method = definitions[row.attrib['testId']]
        actual[method['className'].split(',')[0]+'.'+method['name']] += 1
    assert len(rows) == 155 and dict(actual) == cfg['ExpectedTestMethods'] == gate.helper.inventory()
    analysis = common.read(package/'analysis.json')
    assert analysis == dict(completed=True, clockComponentChecksPassed=True, passedCases=155,
        newClockCases=13, existingRegressionCases=142, exactMethodMultiplicity=True,
        prescribedInputFixtures=True, oldCapturesRetimed=False, nativeUnityExecuted=False,
        independentCoupledTrajectoryQualified=False, physiologicalFiringQualified=False, avatarBalanceQualified=False)
    result.update(reviewedUtc=datetime.now(timezone.utc).isoformat(), operatorReportedCompletion=True,
        passedTests=155, newClockCases=13, existingRegressionCases=142, exactTestInventory=True,
        selectedManagedFiles=335, selectedManagedDirectories=2, runtimeBeforeAfterIdentical=True,
        externalRuntimeRehashedDuringReview=False, clockComponentChecksPassed=True,
        analysisSha256=common.digest(package/'analysis.json'), testsExecutedDuringReview=False,
        modelsExecutedDuringReview=False, independentCoupledTrajectoryQualified=False,
        fullLoopClockJoinQualified=False, physiologicalFiringQualified=False, avatarBalanceQualified=False)
    output.mkdir()
    (output/'review.json').write_text(json.dumps(result, indent=2, allow_nan=False)+'\n', encoding='utf-8')
    for name in ('analysis.json','window-status.json'): shutil.copyfile(package/name, output/name)
    shutil.copyfile(__file__, output/Path(__file__).name)
    print(json.dumps(dict(output=str(output), verifiedFiles=result['verifiedFilesPerCopy'], passedTests=155,
        newClockCases=13, existingRegressionCases=142), indent=2), flush=True)


if __name__ == '__main__':
    if not __debug__: raise RuntimeError('Assertions must remain enabled.')
    parser = argparse.ArgumentParser(); parser.add_argument('package', type=Path)
    parser.add_argument('--operator-reported-complete', action='store_true'); args = parser.parse_args()
    assert args.operator_reported_complete
    review(args.package.resolve())

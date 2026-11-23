"""Read completed equilibrium evidence without rerunning tests or references."""
from collections import Counter
from datetime import datetime, timezone
from decimal import Decimal
import importlib.util
import json
from pathlib import Path
import shutil
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
PACKAGE = Path('D:/DNNE-build-work/muscle-initial-20261123-4c4377')
REVISION = '22f7ed894ff131f9a67b5eea5d7f3030a15e1591'

def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec); spec.loader.exec_module(result); return result

def review():
    output = ROOT/'artifacts'/(PACKAGE.name+'-review'); assert not output.exists()
    common = module('muscle_initial_review_files', ROOT/'tools/review-olive-and-motor-20261109.py')
    archive = module('muscle_initial_review_archive', ROOT/'tools/review-spindle-root-and-causal-motor-20261113.py')
    result = common.verify(PACKAGE, ['test-binaries.json', 'probe-binaries.json'], REVISION, progress_every=1000)
    result['verifiedGitFiles'] = archive.source_archive(common, PACKAGE, REVISION)
    cfg = common.read(PACKAGE/'configuration.json'); status = common.read(PACKAGE/'window-status.json')
    analysis = common.read(PACKAGE/'analysis.json'); independent = common.read(PACKAGE/'initial-balance-analysis.json')
    for key in ('NoLiveMonitoring','ClockChecksPassed','InitialMuscleBalanceChecksPassed','IndependentInitialBalancePassed','FrozenInputsPreserved','ManagedRuntimePreserved'):
        assert status[key] is True
    for key in ('NativeUnityExecuted','LiveBrainAccessed','OldSavesLoaded','IndependentCoupledTrajectoryQualified','PhysiologicalFiringQualified','AvatarBalanceQualified','LiveMuscleGeometrySelected'):
        assert status[key] is False
    assert cfg['ExpectedTestCases'] == status['PassedFocusedTests'] == analysis['passedCases'] == 263
    assert analysis['newInitialBalanceCases'] == 20 and analysis['existingRegressionCases'] == 243
    ns = {'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
    tree = ET.parse(PACKAGE/'test-results/focused.trx')
    definitions = {r.attrib['id']:r.find('t:TestMethod',ns).attrib for r in tree.findall('.//t:UnitTest',ns)}
    actual = Counter(); native = {}
    for row in tree.findall('.//t:UnitTestResult',ns):
        assert row.attrib['outcome'] == 'Passed'
        method = definitions[row.attrib['testId']]; actual[method['className'].split(',')[0]+'.'+method['name']] += 1
        for item in row.findall('t:Output/t:StdOut',ns):
            for line in (item.text or '').splitlines():
                if line.strip().startswith('INITIAL_BALANCE_JSON '):
                    record = json.loads(line.strip().removeprefix('INITIAL_BALANCE_JSON '))
                    assert record['key'] not in native; native[record['key']] = record
    assert sum(actual.values()) == 263 and dict(actual) == cfg['ExpectedTestMethods']
    assert independent['completed'] and independent['passed'] and independent['cases'] == len(native) == len(independent['rows']) == 8
    assert independent['referencePrecisionDigits'] == [80,110] and independent['originalForceAllowancesRetained']
    assert {r['key'] for r in independent['rows']} == set(native)
    for row in independent['rows']:
        assert row['passed'] and row['lengthAllowanceMeters'] == 1e-11
        assert Decimal(row['referenceAgreementMeters']) <= Decimal('1e-60')
        assert Decimal(row['candidateLengthErrorMeters']) <= Decimal.from_float(1e-11)
        assert abs(Decimal(row['independentForceResidual110Newtons'])) <= Decimal.from_float(row['forceAllowanceNewtons'])
        assert row['forceAllowanceNewtons'] == native[row['key']]['result']['forceAllowanceNewtons']
    before = common.read(PACKAGE/'managed-runtime-before.json'); after = common.read(PACKAGE/'managed-runtime-after.json')
    assert before == after
    result.update(reviewedUtc=datetime.now(timezone.utc).isoformat(), operatorReportedCompletion=True,
        passedTests=263,newInitialBalanceCases=20,existingRegressions=243,independentEquilibria=8,
        maximumFiberErrorMeters=str(max(Decimal(r['candidateLengthErrorMeters']) for r in independent['rows'])),
        maximumForceAllowanceFraction=max(abs(float(r['independentForceResidual110Newtons']))/r['forceAllowanceNewtons'] for r in independent['rows']),
        sourceAndRuntimePreserved=True,testsExecutedDuringReview=False,modelsExecutedDuringReview=False,
        referencesRecomputedDuringReview=False,anatomyQualified=False,coupledTrajectoriesQualified=False,
        neuronalMotorAuthority=False,avatarStandingQualified=False)
    output.mkdir(); (output/'review.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
    for name in ('analysis.json','initial-balance-analysis.json','window-status.json'): shutil.copyfile(PACKAGE/name,output/name)
    shutil.copyfile(__file__,output/Path(__file__).name)
    print(json.dumps(result,indent=2),flush=True)

if __name__ == '__main__':
    assert '--operator-reported-complete' in sys.argv
    if not __debug__: raise RuntimeError('Assertions must remain enabled.')
    review()

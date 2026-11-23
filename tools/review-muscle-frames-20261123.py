"""Review completed mapping evidence; no coordinate/model/test execution."""
from collections import Counter
from decimal import Decimal
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path
import shutil
import sys
import xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parents[1]
PACKAGE=Path('D:/DNNE-build-work/muscle-frames-20261123-581e1f')
REVISION='a3c2bb9997c0d6fbbbc7babf714ae384c7671eb3'
def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path); result=importlib.util.module_from_spec(spec);spec.loader.exec_module(result);return result

def review():
    output=ROOT/'artifacts'/(PACKAGE.name+'-review');assert not output.exists()
    common=module('frame_review_files',ROOT/'tools/review-olive-and-motor-20261109.py')
    archive=module('frame_review_archive',ROOT/'tools/review-spindle-root-and-causal-motor-20261113.py')
    result=common.verify(PACKAGE,['test-binaries.json','probe-binaries.json'],REVISION,progress_every=1000)
    result['verifiedGitFiles']=archive.source_archive(common,PACKAGE,REVISION)
    cfg=common.read(PACKAGE/'configuration.json'); status=common.read(PACKAGE/'window-status.json')
    assert status['AnatomicalFrameArithmeticPassed'] and status['IndependentInitialBalancePassed'] and status['FrozenInputsPreserved'] and status['ManagedRuntimePreserved']
    assert not status['NativeUnityExecuted'] and not status['LiveBrainAccessed'] and not status['LiveMuscleGeometrySelected'] and not status['AvatarBalanceQualified']
    ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'};tree=ET.parse(PACKAGE/'test-results/focused.trx')
    methods={r.attrib['id']:r.find('t:TestMethod',ns).attrib for r in tree.findall('.//t:UnitTest',ns)}; actual=Counter()
    for row in tree.findall('.//t:UnitTestResult',ns):
        assert row.attrib['outcome']=='Passed';m=methods[row.attrib['testId']];actual[m['className'].split(',')[0]+'.'+m['name']]+=1
    assert sum(actual.values())==cfg['ExpectedTestCases']==status['PassedFocusedTests']==288 and dict(actual)==cfg['ExpectedTestMethods']
    f=common.read(PACKAGE/'frame-analysis.json');mapped=common.read(PACKAGE/'mapped-lower-leg-paths.json')
    assert f['completed'] and f['passed'] and f['points']==len(f['rows'])==44 and f['muscles']==len(mapped['paths'])==10
    assert f['originalPointOrderAndBodyIdentityPreserved'] and f['pathFingerprintsIndependentlyVerified'] and f['referencePrecisionDigits']==[80,110]
    assert f['footScaleIsDeclaredShinRatio'] and not f['footDimensionsMeasured'] and not f['anatomicalCalibrationQualified']
    assert not mapped['runtimeSelected'] and not mapped['anatomyQualified']
    for row in f['rows']:
        assert row['passed'] and Decimal(row['maximumCoordinateErrorMeters'])<=Decimal('1e-12') and Decimal(row['referenceAgreementMeters'])<=Decimal('1e-60')
    equilibrium=common.read(PACKAGE/'initial-balance-analysis.json')
    assert equilibrium['passed'] and equilibrium['cases']==len(equilibrium['rows'])==8
    for row in equilibrium['rows']:
        assert row['passed'] and Decimal(row['candidateLengthErrorMeters'])<=Decimal.from_float(1e-11)
        assert Decimal(row['referenceAgreementMeters'])<=Decimal('1e-60')
        assert abs(Decimal(row['independentForceResidual110Newtons']))<=Decimal.from_float(row['forceAllowanceNewtons'])
    assert common.read(PACKAGE/'managed-runtime-before.json')==common.read(PACKAGE/'managed-runtime-after.json')
    result.update(reviewedUtc=datetime.now(timezone.utc).isoformat(),operatorReportedCompletion=True,passedTests=288,
        originalPoints=44,namedMuscles=10,independentEquilibria=8,frameArithmeticPassed=True,
        maximumCoordinateErrorMeters=str(max(Decimal(r['maximumCoordinateErrorMeters']) for r in f['rows'])),
        footScaleIsDeclaredShinRatio=True,anatomicalFitQualified=False,nativeNamedMuscleDeliveryQualified=False,
        modelsExecutedDuringReview=False,testsExecutedDuringReview=False,referencesRecomputedDuringReview=False,avatarStandingQualified=False)
    output.mkdir();(output/'review.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
    for name in ('frame-analysis.json','mapped-lower-leg-paths.json','window-status.json'):shutil.copyfile(PACKAGE/name,output/name)
    shutil.copyfile(__file__,output/Path(__file__).name);print(json.dumps(result,indent=2),flush=True)

if __name__=='__main__':
    assert '--operator-reported-complete' in sys.argv and __debug__
    review()

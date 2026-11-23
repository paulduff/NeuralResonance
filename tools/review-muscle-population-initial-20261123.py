"""Review the completed full slow-population initial conditions, without integration."""
from collections import Counter
from datetime import datetime,timezone
from decimal import Decimal
import importlib.util
import json
from pathlib import Path
import shutil
import sys
import xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parents[1]
PACKAGE=Path('D:/DNNE-build-work/muscle-population-initial-20261123-370cb9')
REVISION='917be280abd608f3b33910028cabb0d180de4234'


def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m


def main():
    assert '--operator-reported-complete' in sys.argv and __debug__
    output=ROOT/'artifacts'/(PACKAGE.name+'-review');assert not output.exists()
    common=module('population_review_files',ROOT/'tools/review-olive-and-motor-20261109.py')
    archive=module('population_review_archive',ROOT/'tools/review-spindle-root-and-causal-motor-20261113.py')
    result=common.verify(PACKAGE,['test-binaries.json','probe-binaries.json'],REVISION,progress_every=1000)
    result['verifiedGitFiles']=archive.source_archive(common,PACKAGE,REVISION)
    cfg=common.read(PACKAGE/'configuration.json');status=common.read(PACKAGE/'window-status.json')
    assert status['FullSlowPopulationInitialBalancePassed'] and status['PopulationInitialCases']==4 and status['UnitsPerMuscle']==147
    assert status['FrozenInputsPreserved'] and status['ManagedRuntimePreserved'] and status['AvatarBuildInputChecksPassed']==8
    assert not any(status[k] for k in ('NativeUnityExecuted','LiveBrainAccessed','OldSavesLoaded','LiveMuscleGeometrySelected','AvatarBalanceQualified'))
    assert common.read(PACKAGE/'managed-runtime-before.json')==common.read(PACKAGE/'managed-runtime-after.json')
    ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'};tree=ET.parse(PACKAGE/'test-results/focused.trx')
    methods={r.attrib['id']:r.find('t:TestMethod',ns).attrib for r in tree.findall('.//t:UnitTest',ns)};actual=Counter()
    for row in tree.findall('.//t:UnitTestResult',ns):
        assert row.attrib['outcome']=='Passed';m=methods[row.attrib['testId']];actual[m['className'].split(',')[0]+'.'+m['name']]+=1
    assert sum(actual.values())==cfg['ExpectedTestCases']==status['PassedFocusedTests']==379 and dict(actual)==cfg['ExpectedTestMethods']
    baseline=common.read(PACKAGE/'initial-balance-analysis.json');assert baseline['passed'] and baseline['cases']==8
    for row in baseline['rows']:
        assert row['passed'] and Decimal(row['candidateLengthErrorMeters'])<=Decimal.from_float(1e-11)
        assert Decimal(row['referenceAgreementMeters'])<=Decimal('1e-60')
    proof=common.read(PACKAGE/'population-analysis.json');native=common.read(PACKAGE/'population-study/result.json')
    spec=common.read(PACKAGE/'population-input.json')
    assert proof['completed'] and proof['passed'] and proof['unitsPerMuscle']==147 and proof['originalLimitsRetained']
    assert len(proof['cases'])==len(native['cases'])==len(spec['cases'])==4 and native['completed'] and native['passed']
    summaries=[]
    for row,saved,declared in zip(proof['cases'],native['cases'],spec['cases'],strict=True):
        assert row['key']==saved['key']==declared['key'] and row['passed'] and saved['passed']
        assert Decimal(row['referenceAgreementMeters'])<=Decimal('1e-60') and Decimal(row['candidateErrorMeters'])<=Decimal.from_float(1e-11)
        assert abs(Decimal(row['independentForceResidualNewtons']))<=Decimal.from_float(row['forceAllowanceNewtons'])
        assert saved['geometry']==declared['geometry'] and saved['quietOriginalState'] and saved['originalDefinitionsIntact']
        r=saved['result'];assert r['balanced'] and r['definition'] and r['candidate']['timeMs']==declared['physicalTimeMs']
        assert len(r['candidate']['units'])==len(r['definition']['units'])==147
        for unit,definition,source in zip(r['candidate']['units'],r['definition']['units'],spec['parameterRows'],strict=True):
            assert definition['calcium']==source['calcium'] and definition['lengthGainAboveOptimal']==source['lengthGainAboveOptimal'] and definition['lengthGainBelowOptimal']==source['lengthGainBelowOptimal']
            assert unit['motorUnitId']==definition['contact']['motorUnitId']
            assert all(unit[k]==0 for k in ('freeCalcium','boundCalcium','activation','releaseDrive','arrivals'))
        summaries.append(dict(key=row['key'],normalizedFiberLength=row['normalizedFiberLength'],axialTendonForceNewtons=row['axialTendonForceNewtons'],candidateErrorMeters=row['candidateErrorMeters']))
    result.update(reviewedUtc=datetime.now(timezone.utc).isoformat(),operatorReportedCompletion=True,passedManagedTests=379,
        passedPackagingControls=8,originalEquilibria=8,populationEquilibria=4,unitsPerMuscle=147,
        originalSourceParametersPreserved=True,originalPhysicalTimestampPreserved=True,initialBalancePassed=True,cases=summaries,
        modelsExecutedDuringReview=False,testsExecutedDuringReview=False,anatomicalFitQualified=False,
        activePopulationQualified=False,neuronalAuthority=False,avatarStandingQualified=False)
    output.mkdir();(output/'review.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
    for name in ('population-analysis.json','population-input.json','window-status.json'):
        shutil.copyfile(PACKAGE/name,output/name)
    shutil.copyfile(PACKAGE/'population-study/result.json',output/'population-result.json')
    shutil.copyfile(__file__,output/Path(__file__).name)
    print(json.dumps(result,indent=2),flush=True)


if __name__=='__main__':main()

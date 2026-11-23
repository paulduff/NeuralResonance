"""Read-only reconciliation of two operator-reported completed studies."""
from collections import Counter
from datetime import datetime,timezone
import gzip
import importlib.util
import json
from pathlib import Path
import shutil
import struct
import sys
import xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parents[1]


def module(name,path):
    s=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m


def check_tests(package,common,count):
    cfg=common.read(package/'configuration.json');ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
    tree=ET.parse(package/'test-results/focused.trx');methods={r.attrib['id']:r.find('t:TestMethod',ns).attrib for r in tree.findall('.//t:UnitTest',ns)}
    actual=Counter()
    for row in tree.findall('.//t:UnitTestResult',ns):
        assert row.attrib['outcome']=='Passed';m=methods[row.attrib['testId']];actual[m['className'].split(',')[0]+'.'+m['name']]+=1
    assert sum(actual.values())==cfg['ExpectedTestCases']==count and dict(actual)==cfg['ExpectedTestMethods']
    return cfg


def review(kind):
    assert '--operator-reported-complete' in sys.argv and __debug__
    active=kind=='active';name='muscle-population-active-20261123-a212b5' if active else 'shared-motor-anatomy-20261123-5001cd'
    revision='3ac6ecb089eff59ee96b1e7edb2c4a2d41b0eb36' if active else 'bc7898940d43cf83093e0c4368671e8bd22c01d5'
    package=Path('D:/DNNE-build-work')/name;output=ROOT/'artifacts'/(name+'-review');assert not output.exists()
    common=module('completed_population_files',ROOT/'tools/review-olive-and-motor-20261109.py')
    archive=module('completed_population_source',ROOT/'tools/review-spindle-root-and-causal-motor-20261113.py')
    report=common.verify(package,['test-binaries.json','probe-binaries.json'],revision,progress_every=1000)
    report['verifiedGitFiles']=archive.source_archive(common,package,revision)
    count=379 if active else 164;cfg=check_tests(package,common,count);status=common.read(package/'window-status.json')
    assert status['PassedFocusedTests']==count and status['FrozenInputsPreserved'] and status['ManagedRuntimePreserved']
    assert common.read(package/'managed-runtime-before.json')==common.read(package/'managed-runtime-after.json')
    assert all(not status[k] for k in ('NativeUnityExecuted','LiveBrainAccessed','OldSavesLoaded','AvatarBalanceQualified'))
    copies=['analysis.json','window-status.json'];details={}
    if active:
        assert status['PopulationNumericalCriteriaPassed'] and status['PopulationDynamicCases']==status['PopulationReferences']==24
        assert status['UnitsPerMuscle']==147 and status['ScalarVectorFieldControlsPassed']==108 and status['BoundedJacobianControlsPassed']==6
        spec=common.read(package/'population-dynamics-input.json');analysis=common.read(package/'population-dynamics-analysis.json')
        bounds=module('retained_population_bounds',package/'source/tools/dynamic_muscle_protocol.py')
        assert spec['referenceBounds']==bounds.REFERENCE_BOUNDS and spec['candidateBounds']==bounds.CANDIDATE_BOUNDS and spec['referenceSettings']==bounds.SETTINGS
        assert spec['stepsMs']==[.125,.0625,.03125] and spec['sampleMs']==.25 and len(spec['parameterRows'])==147
        assert analysis['completed'] and analysis['numericalCriteriaPassed'] and not analysis['originalBoundsChanged']
        assert len(analysis['cases'])==len(spec['cases'])==8
        control=common.read(package/'population-reference-controls.json')
        assert control['passed'] and len(control['fieldCases'])==108 and len(control['jacobianCases'])==6
        assert all(x['passed'] and x['maximumAllowanceFraction']<=1 for x in control['fieldCases']+control['jacobianCases'])
        execution=common.read(package/'population-reference/execution.json');assert execution['completed'] and len(execution['references'])==24
        native=common.read(package/'population-dynamics/result.json');assert native['completed'] and native['successfulCases']==24 and len(native['cases'])==24
        assert common.read(package/'population-dynamics/input.json')==spec
        peak_fraction=reference_fraction=0.;cases=[]
        for row,declared in zip(analysis['cases'],spec['cases'],strict=True):
            assert row['case']==declared['key'] and row['passed'] and row['referenceDomainPassed'] and all(row['refinementPassed'].values())
            assert [(c['left'],c['right']) for c in row['referenceContrasts']]==[('dop853','dop853-tight'),('dop853-tight','radau')]
            for c in row['referenceContrasts']:
                assert c['passed'] and c['errors'].keys()==spec['referenceBounds'].keys()
                ratios=[c['errors'][k]/v for k,v in spec['referenceBounds'].items()];assert all(0<=x<=1 for x in ratios)
                reference_fraction=max(reference_fraction,*ratios)
            assert [c['stepMs'] for c in row['candidates']]==spec['stepsMs']
            for candidate in row['candidates']:
                assert candidate['physicalDomainPassed'] and candidate['accountingPassed']
                assert candidate['releaseWaveformError']<=spec['releaseWaveformAbsolute'] and candidate['calciumLedgerError']<=spec['calciumLedgerAbsolute']
                assert candidate['momentumLedgerError']<=spec['momentumLedgerNewtonSeconds']
                key=row['case']+'-h'+str(candidate['stepMs']);folder=package/'population-dynamics'/key
                saved=next(r for r in native['cases'] if r['key']==key)
                assert saved['completed'] and saved['samples']==801 and saved['delivered']==len(declared['events'])
                assert common.read(folder/'declared.json')['scenario']==declared
                # Decode only: no reference function or model evaluation.
                with gzip.open(folder/'trace.f64.gz','rb') as f:data=f.read()
                assert data[:8]==b'NRMUSC01' and struct.unpack('<iq',data[8:20])==(1339,801) and len(data)==20+801*1339*8
                for i in range(801):assert struct.unpack_from('<d',data,20+i*1339*8)[0]==declared['initialTimeMs']+i*.25
            finest=row['candidates'][-1];assert finest['kineticLedgerError']<=spec['finestKineticLedgerJoules']
            ratios=[errors[k]/bound for errors in finest['errorsAgainst'].values() for k,bound in spec['candidateBounds'].items()]
            assert len(ratios)==20 and all(0<=x<=1 for x in ratios);peak_fraction=max(peak_fraction,*ratios)
            cases.append(dict(case=row['case'],finestMaximumAllowanceFraction=max(ratios),
                peakActiveForceNewtons=finest['peakActiveForceNewtons'],peakTendonForceNewtons=finest['peakTendonForceNewtons'],
                minimumNormalizedFiberLength=finest['minimumNormalizedFiberLength']))
        for ref in execution['references']:
            assert ref['completed'] and ref['acceptedInternalDomainPassed'] and ref['sampleDomainPassed']
            assert common.read(package/'population-reference'/(ref['key']+'-status.json'))==ref
        details=dict(activePopulationNumericalCriteriaPassed=True,unitsPerMuscle=147,candidateTraces=24,independentReferences=24,
            sampleCountPerTrace=801,referenceMaximumAllowanceFraction=reference_fraction,finestMaximumAllowanceFraction=peak_fraction,
            prescribedArrivals=True,fullPopulationPhysiologyQualified=False,cases=cases)
        copies+=['population-dynamics-analysis.json','population-dynamics-input.json','population-reference-controls.json']
    else:
        assert cfg['SharedMotorAnatomyRegression'] and cfg['NewSharedMotorCases']==status['NewSharedMotorCases']==9
        assert status['SharedMotorAnatomyChecksPassed']
        result=common.read(package/'analysis.json');assert result['sharedMotorAnatomyChecksPassed'] and result['fullPopulationPerSide']==147
        assert cfg['SharedMotorChangeDeclaration']['changedFiles']==['tests/NeuralResonanceEngine.DNNE.Tests/SharedMotorAnatomyTests.cs','tools/NativeMotorBridgeProbe/DescendingMotorPool.cs']
        details=dict(sharedMotorAnatomyChecksPassed=True,newSharedMotorCases=9,fullPopulationPerSide=147,
            wholeBodyCompletenessRetained=True,neuronalRecruitmentQualified=False)
    report.update(reviewedUtc=datetime.now(timezone.utc).isoformat(),operatorReportedCompletion=True,passedManagedTests=count,
        modelsExecutedDuringReview=False,testsExecutedDuringReview=False,anatomicalFitQualified=False,
        neuronalAuthorityQualified=False,avatarStandingQualified=False,**details)
    output.mkdir();(output/'review.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    for name in copies:shutil.copyfile(package/name,output/name)
    shutil.copyfile(__file__,output/Path(__file__).name)
    print(json.dumps(report,indent=2),flush=True)


if __name__=='__main__':
    assert len(sys.argv)==3 and sys.argv[1] in ('active','shared')
    review(sys.argv[1])

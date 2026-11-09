"""Review the reported complete-but-failed coupled Golgi capture, without solving."""
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path
import xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parents[1]
def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path)
    result=importlib.util.module_from_spec(spec); spec.loader.exec_module(result); return result

def main():
    common=module('completed_capture_review',ROOT/'tools/review-olive-and-motor-20261109.py')
    capture=ROOT/'artifacts/golgi-sdirk-20261109-440bb8'
    report=common.verify(capture,['test-binaries.json','probe-binaries.json'],
        'fb6c6e1028748d0b965d544ac9858cc0f269db25',expected_status='FAILED',expected_exit=1)
    ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
    cases=ET.parse(capture/'test-results/focused.trx').findall('.//t:UnitTestResult',ns)
    assert len(cases)==32 and all(c.attrib['outcome']=='Passed' for c in cases)
    classes=['GolgiSdirkTests','GolgiReferenceCellTests','GolgiChannelKineticsTests','SolutionManifestTests']
    counts={name:sum(c.attrib['testName'].startswith('NeuralResonanceEngine.DNNE.Tests.'+name+'.') for c in cases) for name in classes}
    assert list(counts.values())==[7,8,16,1]
    audit=module('reported_golgi_audit',ROOT/'tools/analyse-golgi-sdirk.py')
    analysis=audit.audit(capture)
    assert analysis==common.read(capture/'sdirk-analysis.json') and analysis['completedCases']==30
    assert not analysis['numericalCriteriaPassed'] and all(r['physicalPassed'] for r in analysis['cases'])
    results=common.read(capture/'study/results.json'); summaries=[]
    for assessment in analysis['assessment']:
        selected=[r for r in analysis['cases'] if r['protocol']==assessment['protocol'] and r['nonlinearToleranceScale']==1]
        selected.sort(key=lambda r:r['stepMs'],reverse=True)
        item=dict(protocol=assessment['protocol'],passed=assessment['passed'],
            refinementPassed=all(r['passed'] for r in assessment['refinement']))
        if assessment['protocol'].startswith('passive-'):
            item['finestVoltageErrorMv']=selected[-1]['passiveVoltageErrorMv']
        else:
            comparisons=[r['comparisons']['bdf-tighter'] for r in selected]
            item.update(finestAbsolutePassed=assessment['finestAbsolutePassed'],
                nonlinearSensitivityPassed=assessment['nonlinearSensitivityPassed'],
                nonlinearSensitivity=assessment['nonlinearSensitivity'],
                ordinaryComparisons=comparisons,finestFailedFields=[f for f in audit.FIELDS if comparisons[-1][f]>analysis['criteria'][f]],
                crossingErrorRatios=[a['crossingMs']/b['crossingMs'] if b['crossingMs'] else None for a,b in zip(comparisons,comparisons[1:])])
            with audit.np.load(capture/'reference-baseline/retained-reference'/(assessment['protocol']+'-bdf-tighter.npz'),allow_pickle=False) as ref:
                crossing=ref['spikes'].copy()
            errors=[]
            for row in selected:
                original=next(r for r in results['cases'] if r['key']==row['key'])
                delta=audit.np.asarray(original['crossingsMs'])-crossing
                errors.append(dict(stepMs=row['stepMs'],count=len(delta),errorsMs=delta.tolist(),
                    firstErrorMs=float(delta[0]) if len(delta) else None,
                    lastErrorMs=float(delta[-1]) if len(delta) else None))
            item['ordinalCrossingErrors']=errors
        summaries.append(item)
    report.update(reviewedUtc=datetime.now(timezone.utc).isoformat(),operatorReportedCompletion=True,
        passedFocusedTests=32,testCounts=counts,all30ConditionsCompleted=True,allPhysicalAccountingPassed=True,
        originalAnalysisReproduced=True,originalStatus='FAILED',originalBoundsChanged=False,
        protocolSummaries=summaries,numericalCriteriaPassed=False,odeSolved=False,
        physiologyQualified=False,avatarRecoveryAssessed=False)
    output=ROOT/'artifacts/golgi-sdirk-review-20261109'; output.mkdir()
    (output/'review.json').write_text(json.dumps(report,indent=2,allow_nan=False)+'\n',encoding='utf-8')
    print(json.dumps({k:v for k,v in report.items() if k!='protocolSummaries'},indent=2))
    for row in summaries:
        if 'ordinaryComparisons' in row:
            final=row['ordinaryComparisons'][-1]
            print(row['protocol'],'voltage',final['voltageMv'],'timing',final['crossingMs'],'timing ratios',row['crossingErrorRatios'],
                'first/last',row['ordinalCrossingErrors'][-1]['firstErrorMs'],row['ordinalCrossingErrors'][-1]['lastErrorMs'])
        else: print(row['protocol'],'voltage',row['finestVoltageErrorMv'])

if __name__=='__main__': main()

"""Audit the operator-reported failed cortical window; no model execution."""
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path
import xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parents[1]

def main():
    spec=importlib.util.spec_from_file_location('completed_review',ROOT/'tools/review-olive-and-motor-20261109.py')
    common=importlib.util.module_from_spec(spec);spec.loader.exec_module(common)
    capture=ROOT/'artifacts/cortical-precision-20261109-67008c'
    report=common.verify(capture,['test-binaries.json'],'4fefd12c27c027fb86b048d37b25481f9fc00d1b',
        expected_status='FAILED',expected_exit=1)
    ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
    cases=ET.parse(capture/'test-results/focused.trx').findall('.//t:UnitTestResult',ns)
    assert len(cases)==68 and sum(c.attrib['outcome']=='Passed' for c in cases)==67
    failed=[c for c in cases if c.attrib['outcome']!='Passed'];assert len(failed)==1
    failure=failed[0];name='NeuralResonanceEngine.DNNE.Tests.CorticalPrecisionIntegrationTests.InhibitoryAndSyntheticContactsRetainTheirOwnRulesAndStorage'
    assert failure.attrib['testName']==name and failure.attrib['outcome']=='Failed'
    message=failure.find('t:Output/t:ErrorInfo/t:Message',ns).text
    assert 'collection did not contain any matching items' in message and 'Collection: []' in message
    counts={name:sum(c.attrib['testName'].split('(')[0].rsplit('.',1)[0].split('.')[-1]==name for c in cases)
        for name in common.read(capture/'configuration.json')['TestClassCounts']}
    assert counts==common.read(capture/'configuration.json')['TestClassCounts']
    assert all(not (capture/name).exists() for name in ('study','legacy-study','probe-binaries.json','precision-analysis.json'))
    report.update(reviewedUtc=datetime.now(timezone.utc).isoformat(),operatorReportedCompletion=True,
        savedBytesReverified=True,focusedTests=68,passedTests=67,failedTests=1,testCounts=counts,
        failedTest=name,failureMessage=message,longerStudiesStarted=False,modelExecutedDuringReview=False,
        originalBoundsChanged=False,integrationQualified=False,biologicalConsolidationQualified=False,
        physiologyQualified=False,avatarRecoveryAssessed=False,
        sourceDiagnosis='The test inspected contacts after 1 ms; its Entorhinal tract requires at least 3 ms. Confirm the corrected before/after delivery assertions in a fresh window.')
    output=ROOT/'artifacts/cortical-precision-first-review-20261109';output.mkdir()
    (output/'review.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(report,indent=2))

if __name__=='__main__':main()

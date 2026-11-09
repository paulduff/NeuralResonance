"""Audit the operator-reported completed CUDA capture. No model/GPU execution."""
from datetime import datetime, timezone
from pathlib import Path
import importlib.util
import json
import xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parents[1]
def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path)
    result=importlib.util.module_from_spec(spec); spec.loader.exec_module(result); return result

def main():
    common=module('completed_review',ROOT/'tools/review-olive-and-motor-20261109.py')
    capture=ROOT/'artifacts/olive-cuda-20261109-cd1fee'
    review=common.verify(capture,['test-binaries.json','interop-binaries.json','probe-binaries.json'],
        'f8de323a5ef989219d10be8906f9f46aa13cbdbf')
    config=common.read(capture/'configuration.json'); status=common.read(capture/'window-status.json')
    reports=list((capture/'test-results').glob('*.trx')); assert len(reports)==1
    ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
    cases=ET.parse(reports[0]).findall('.//t:UnitTestResult',ns)
    assert len(cases)==config['ExpectedTestCases']==11 and all(c.attrib['outcome']=='Passed' for c in cases)
    assert {c.attrib['testName'].rsplit('.',1)[0].rsplit('.',1)[-1] for c in cases}==set(config['TestClasses'])
    interop=module('interop_review',ROOT/'tools/analyse-cuda-interop.py').audit(capture/'interop-study')
    assert interop==common.read(capture/'interop-study/analysis.json') and interop['exactTransferAndArithmeticCases']==6
    analysis=module('membrane_review',ROOT/'tools/analyse-olive-cuda.py').audit(capture)
    assert analysis==common.read(capture/'gpu-analysis.json') and analysis['numericalAgreementPassed']
    assert status['GpuExecuted'] and status['GpuCpuAgreementPassed'] and not status['SpeedupMeasured']
    maxima=[max(r['maximumAbsoluteErrors'][i] for r in analysis['cases']) for i in range(17)]
    review.update(reviewedUtc=datetime.now(timezone.utc).isoformat(),operatorReportedCompletion=True,
        passedFocusedTests=len(cases),savedBytesReverified=True,interop=interop,analysis=analysis,
        comparedCellFrames=sum(r['comparedCellFrames'] for r in analysis['cases']),
        maximumVoltageErrorMv=max(maxima[:3]),maximumGateError=max(maxima[3:11]+maxima[12:14]),
        maximumCalciumErrorMicromolar=maxima[11],speedupMeasured=False,
        independentSolverQualified=False,physiologyQualified=False,avatarRecoveryAssessed=False)
    output=ROOT/'artifacts/olive-cuda-review-20261109'; output.mkdir()
    (output/'review.json').write_text(json.dumps(review,indent=2)+'\n',encoding='utf-8')
    print(json.dumps({k:v for k,v in review.items() if k not in ('interop','analysis')},indent=2))

if __name__=='__main__': main()

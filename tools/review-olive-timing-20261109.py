"""Review operator-reported batch timing, including all saved accuracy evidence."""
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path
import xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parents[1]
def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path); result=importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result); return result

def main():
    common=module('completed_capture_review',ROOT/'tools/review-olive-and-motor-20261109.py')
    capture=ROOT/'artifacts/olive-cuda-timing-20261109-eb7469'
    report=common.verify(capture,['test-binaries.json','probe-binaries.json'],'c4f80f22c6cd16b94f188cc558cba65002d1e07e')
    ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
    cases=ET.parse(capture/'test-results/focused.trx').findall('.//t:UnitTestResult',ns)
    assert len(cases)==11 and all(c.attrib['outcome']=='Passed' for c in cases)
    config=common.read(capture/'configuration.json')
    assert {c.attrib['testName'].rsplit('.',1)[0].rsplit('.',1)[-1] for c in cases}==set(config['TestClasses'])
    analysis=module('timing_saved_byte_audit',ROOT/'tools/analyse-olive-cuda-timing.py').audit(capture)
    assert analysis==common.read(capture/'timing-analysis.json') and analysis['accuracyPassed']
    summaries=[]
    for row in analysis['cases']:
        cpu=row['backends'][row['bestCpuMode']]; gpu=row['backends']['cuda']
        summaries.append(dict(key=row['key'],bestCpuMode=row['bestCpuMode'],cpuMedianMs=cpu['medianMs'],
            gpuMedianMs=gpu['medianMs'],ratio=row['speedupVersusBestCpuMedian'],
            pairedMinimum=min(row['pairedSpeedupVersusBestCpu']),pairedMaximum=max(row['pairedSpeedupVersusBestCpu']),
            deviceBufferBytes=row['deviceBufferBytes'],minimumObservedFreeBytes=row['minimumObservedFreeBytes']))
    report.update(reviewedUtc=datetime.now(timezone.utc).isoformat(),operatorReportedCompletion=True,passedFocusedTests=11,
        completeAnalysisReproduced=True,accuracyPassed=True,measuredTrials=240,warmupTrials=48,summary=summaries,
        speedupMeasured=True,wholeBrainThroughputMeasured=False,physiologyQualified=False,avatarRecoveryAssessed=False,
        recommendedDefaultForCurrent192CellFixture='cpu',productionBackendChanged=False)
    output=ROOT/'artifacts/olive-timing-review-20261109'; output.mkdir()
    (output/'review.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(report,indent=2))

if __name__=='__main__': main()

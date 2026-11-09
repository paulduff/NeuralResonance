"""Review the completed actual-cell cortical integration capture without running it."""
from collections import Counter
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path
import xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parents[1]

def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path)
    result=importlib.util.module_from_spec(spec);spec.loader.exec_module(result);return result

def main():
    common=module('capture_review',ROOT/'tools/review-olive-and-motor-20261109.py')
    capture=ROOT/'artifacts/cortical-precision-20261109-bae439'
    report=common.verify(capture,['test-binaries.json','probe-binaries.json'],'5e2c3a55c021b25c5745190ebce4dc0e88f9db25')
    ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
    cases=ET.parse(capture/'test-results/focused.trx').findall('.//t:UnitTestResult',ns)
    assert len(cases)==68 and all(c.attrib['outcome']=='Passed' for c in cases)
    counts=Counter(c.attrib['testName'].split('(')[0].rsplit('.',1)[0] for c in cases)
    assert counts==common.read(capture/'configuration.json')['TestClassCounts']
    audit=module('cortical_analysis',ROOT/'tools/analyse-cortical-precision-integration.py')
    analysis=audit.audit(capture);assert analysis==common.read(capture/'precision-analysis.json')
    assert analysis['integrationCriteriaPassed'] and analysis['legacySevenControlsReproduced']
    summaries=[]
    for item in analysis['cases']:
        folder=capture/'study'/item['scenario'];final=common.read(folder/'final.json')
        events=audit.lines(folder/'learning.jsonl')
        cue=[c for c in final['precise'] if c['sourceInstance']=='isolated-precision-source']
        changes={kind:sum(e['after']['state']['weight']-e['before']['state']['weight'] for e in events
            if e['kind']==kind and e['after']['sourceInstance']=='isolated-precision-source') for kind in ('pre','post','modulation')}
        small=Counter(e['kind'] for e in events if e['before']['state']['weight']!=e['after']['state']['weight']
            and audit.f32(e['before']['state']['weight'])==audit.f32(e['after']['state']['weight']))
        summaries.append(dict(item,preciseContacts=len(final['precise']),
            recurrentPreciseContacts=sum(c['sourceInstance']=='isolated-cortical-precision-study' for c in final['precise']),
            externalCueFinalState=cue[0]['state'] if cue else None,externalCueSignedWeightChanges=changes,
            subFloatUpdatesByEventKind=dict(small)))
    report.update(reviewedUtc=datetime.now(timezone.utc).isoformat(),operatorReportedCompletion=True,
        savedBytesReverified=True,passedTests=68,testCounts=dict(counts),originalAnalysisReproduced=True,
        sevenLegacyControlsReproduced=True,precisionScenarioCount=6,warmStartsChecked=6,
        precisionDeliveries=sum(c['precisionDeliveries'] for c in analysis['cases']),
        preciseLearningEvents=sum(c['preciseLearningEvents'] for c in analysis['cases']),
        subFloatWeightUpdates=sum(c['subFloatWeightUpdates'] for c in analysis['cases']),
        cases=summaries,integrationCriteriaPassed=True,modelExecutedDuringReview=False,
        originalNumericalBoundsChanged=False,liveCandidateSelected=False,fullUnitSuitePassed=False,
        biochemicalConsolidationQualified=False,physiologyQualified=False,avatarRecoveryAssessed=False)
    output=ROOT/'artifacts/cortical-precision-review-20261109';output.mkdir()
    (output/'review.json').write_text(json.dumps(report,indent=2,allow_nan=False)+'\n',encoding='utf-8')
    print(json.dumps({k:v for k,v in report.items() if k!='cases'},indent=2))
    for item in summaries:
        print(item['scenario'], 'contacts',item['preciseContacts'],'recurrent',item['recurrentPreciseContacts'],
            'cueWeight',item['externalCueFinalState']['weight'] if item['externalCueFinalState'] else None,
            'signedCueChanges',item['externalCueSignedWeightChanges'],'smallUpdates',item['subFloatUpdatesByEventKind'])

if __name__=='__main__':main()

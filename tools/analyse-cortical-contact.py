"""Audit saved fixed-history replays; no neural integration or fitted criteria."""
from collections import Counter
import json
import math
import struct
from pathlib import Path
import sys

read=lambda path:json.loads(path.read_text(encoding='utf-8-sig'))
lines=lambda path:[json.loads(line) for line in path.read_text(encoding='utf-8-sig').splitlines()]
f32=lambda value:struct.unpack('<f',struct.pack('<f',value))[0]

def main(package):
    spec=read(package/'input/input.json');result=read(package/'study/results.json')
    assert result['completed'] and not result['sourceLearningRuleChanged'] and not result['biologicalConsolidationQualified']
    assert len(result['replayCases'])==16 and len(result['cadenceCases'])==27
    replays={};summaries=[]
    for case in spec['cases']:
        original=lines(package/'input'/case['file'])
        assert len(original)==case['events']
        for mode in spec['interventions']:
            summary=next(x for x in result['replayCases'] if x['key']==case['key'] and x['intervention']==mode)
            rows=lines(package/'study'/(case['key']+'-'+mode+'.jsonl'))
            assert len(rows)==len(original)==summary['eventCount']
            previous=original[0]['before'];changes=Counter();lost=0;spent_lost=0
            for index,(row,recorded) in enumerate(zip(rows,original,strict=True)):
                assert row['index']==index and row['intervention']==mode and row['before']==previous
                for field in ('kind','messageId','timestampMs','elapsedMs','activity','teaching'):
                    assert row[field]==recorded[field]
                assert row['recordedDopamine']==recorded['dopamine'] and row['recordedAcetylcholine']==recorded['acetylcholine']
                if mode=='Recorded':assert row['before']==recorded['before'] and row['after']==recorded['after']
                before=row['before'];after=row['after']
                for field in ('contactId','projectionId','sourceInstance','source','sourceNeuron','target','neurotransmitter','synthetic','baseline'):
                    assert before[field]==after[field]==recorded['before'][field]
                for field in ('preTrace','postTrace','eligibility','tag','theta','timestampMs'):
                    assert after[field]==recorded['after'][field]
                assert all(math.isfinite(v) for v in after.values() if isinstance(v,(int,float)))
                change=f32(after['weight'])-f32(before['weight']);changes[row['kind']]+=change
                spent=after['absoluteChange']-before['absoluteChange']
                if spent>0 and change==0:lost+=1;spent_lost+=spent
                previous=after
            assert previous==summary['final'] and summary['initial']==original[0]['before']
            # Reconstruct the serialized float states before promoting to double,
            # exactly matching subtraction of the actual stored C# weights.
            assert all(changes[k]==summary['weightChangeByKind'][k] for k in ('pre','post','modulation'))
            assert lost==summary['lostUpdates'] and math.isclose(spent_lost,summary['spentWithNoStoredChange'],abs_tol=1e-14)
            assert summary['preEvents']==sum(e['kind']=='pre' for e in original)
            assert summary['postEvents']==sum(e['kind']=='post' for e in original)
            assert summary['modulationEvents']==sum(e['kind']=='modulation' for e in original)
            replays[case['key'],mode]=summary
        recorded=replays[case['key'],'Recorded'];no_capture=replays[case['key'],'NoTagCapture']
        no_ach=replays[case['key'],'NoAcetylcholine']
        assert no_capture['final']==no_ach['final']
        if recorded['modulationEvents']==0:
            assert all(replays[case['key'],m]['final']==recorded['final'] for m in spec['interventions'])
        summaries.append(dict(case=case['key'],originalFinalWeight=recorded['final']['weight'],
            noCaptureFinalWeight=no_capture['final']['weight'],noDopamineFinalWeight=replays[case['key'],'NoDopamine']['final']['weight'],
            directInterventionFinalWeightDifference=recorded['final']['weight']-no_capture['final']['weight'],
            includesSubsequentWeightAndBudgetInteractions=True,spikesAndActivityHeldFixed=True,
            recordedModulationWeightChange=recorded['weightChangeByKind']['modulation'],
            lostUpdates=recorded['lostUpdates'],spentWithNoStoredChange=recorded['spentWithNoStoredChange']))
    steps=[4,2,1,.5,.25,.125,.0625,.03125,.015625]
    expected={(p,h) for p in ('pre-post','post-pre','unpaired') for h in steps}
    for row in result['cadenceCases']:
        key=row['pairing'],row['stepMs'];assert key in expected;expected.remove(key)
        assert row['durationMs']==1000 and row['final']['timestampMs']==1020
        assert math.isfinite(row['continuousLimit']) and math.isfinite(row['absoluteWeightDifference'])
        assert abs(f32(row['final']['weight'])-row['continuousLimit'])==row['absoluteWeightDifference']
        integral=f32(.35)*f32(.02)*f32(row['initial']['tag'])*8*(1-math.exp(-1/8))/5
        limit=5-(5-f32(row['initial']['weight']))*math.exp(-integral) if row['initial']['tag']>=0 else f32(row['initial']['weight'])*math.exp(integral)
        assert abs(limit-row['continuousLimit'])<=8*math.ulp(limit)
        if row['pairing']=='unpaired':assert row['final']['weight']==1 and row['final']['tag']==0
    assert not expected
    output=package/'study/analysis.json';assert not output.exists()
    report=dict(completed=True,exactRecordedReplays=4,replayCases=16,cadenceCases=27,
        cases=summaries,cadence=result['cadenceCases'],biologicalConsolidationQualified=False,
        originalConsolidationAssertionRepaired=False,numericalCadenceQualified=False,liveBrainAccessed=False)
    output.write_text(json.dumps(report,indent=2,allow_nan=False)+'\n')
    print(json.dumps({k:v for k,v in report.items() if k!='cadence'},indent=2),flush=True)

if __name__=='__main__':main(Path(sys.argv[1]).resolve())

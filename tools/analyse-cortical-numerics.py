"""Audit candidate evidence and closed-form accuracy; never execute neural steps."""
import json
import math
from pathlib import Path
import struct
import sys

read=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
f32=lambda x:struct.unpack('<f',struct.pack('<f',x))[0]
lines=lambda p:[json.loads(s) for s in p.read_text(encoding='utf-8-sig').splitlines()]

def main(package):
    assert read(package/'study/results.json')==read(package/'input/completed-replay-results.json'), 'Original complete baseline changed.'
    protocol=read(package/'numerical-study/protocol.json');result=read(package/'numerical-study/results.json')
    assert protocol['version']==result['version']=='isolated-cortical-contact-double-v1'
    assert protocol['maximumAbsoluteWeightError']==3e-8 and protocol['finestAbsoluteWeightError']==1e-9
    assert protocol['refinementRatio']==1.1 and protocol['refinementRoundoffFloor']==1e-11
    assert result['completed'] and result['invalidInputAtomicityPassed'] and len(result['replayCases'])==16 and len(result['cadenceCases'])==27
    expected={(p,h) for p in ('pre-post','post-pre','unpaired') for h in protocol['stepsMs']};previous={};all_pass=True
    for row in result['cadenceCases']:
        key=row['pairing'],row['stepMs'];assert key in expected;expected.remove(key)
        a=row['initial'];b=row['final'];assert b['timeMs']-a['timeMs']==1000 and a['weight']==1
        gain=f32(.35)*(f32(.35)+f32(.65))*f32(.02)
        integral=gain*a['tag']*8*(1-math.exp(-1/8))/5
        limit=5-(5-a['weight'])*math.exp(-integral) if a['tag']>=0 else a['weight']*math.exp(integral)
        assert abs(limit-row['continuousLimit'])<=8*math.ulp(limit)
        error=abs(b['weight']-row['continuousLimit']);assert error==row['absoluteError']
        direction=b['weight']>1 if key[0]=='pre-post' else b['weight']<1 if key[0]=='post-pre' else b['weight']==1
        passed=(error<=3e-8 and (key[1]!=.015625 or error<=1e-9) and
            error<=max(1e-11,1.1*previous.get(key[0],math.inf)) and direction and row['maximumChargedWithoutWeight']==0
            and b['budget']>0 and .05<b['weight']<5)
        assert passed==row['passed'] and direction==row['directionPassed'];all_pass &= passed;previous[key[0]]=error
        assert all(math.isfinite(v) for v in b.values() if isinstance(v,(int,float)))
    assert not expected and all_pass==result['numericalCriteriaPassed']
    groups={}
    for summary in result['replayCases']:
        name,mode=summary['key'],summary['intervention'];assert (name,mode) not in groups
        original=lines(package/'input'/(name+'.jsonl'));rows=lines(package/'numerical-study'/(name+'-'+mode+'.jsonl'))
        assert len(rows)==len(original)==summary['eventCount']
        state=summary['initial'];change=dict(pre=0.,post=0.,modulation=0.);lost=0;charge=0
        captured=original[0]['before']
        for target,source in (('weight','weight'),('baseline','baseline'),('preTrace','preTrace'),('postTrace','postTrace'),
            ('eligibility','eligibility'),('tag','tag'),('theta','theta'),('budget','budget')):
            assert state[target]==f32(captured[source])
        for target,source in (('contactId','contactId'),('timeMs','timestampMs'),('updates','updates'),
            ('budgetTimeMs','budgetTimestampMs'),('chargedPlasticity','absoluteChange')):
            assert state[target]==captured[source]
        for i,(row,event) in enumerate(zip(rows,original,strict=True)):
            assert row['index']==i and row['intervention']==mode and row['before']==state
            for field in ('kind','messageId','timestampMs','elapsedMs','activity','teaching'):assert row[field]==event[field]
            assert row['recordedDopamine']==event['dopamine'] and row['recordedAcetylcholine']==event['acetylcholine']
            assert row['after']['contactId']==row['before']['contactId']==event['before']['contactId']
            assert row['after']['timeMs']==event['timestampMs']
            assert all(math.isfinite(v) for v in row['after'].values() if isinstance(v,(int,float)))
            delta=row['after']['weight']-state['weight'];change[row['kind']]+=delta
            spent=row['after']['chargedPlasticity']-state['chargedPlasticity']
            if spent>0 and delta==0:lost+=1;charge+=spent
            state=row['after']
        assert state==summary['final'] and change==summary['weightChangeByKind'] and lost==summary['lostUpdates'] and charge==summary['lostCharge']
        groups[name,mode]=summary
    cases=[]
    for name in ('continuous-colocated','continuous-neighbor','continuous-simultaneous','continuous-unmodulated'):
        base=groups[name,'RecordedSignals'];no_capture=groups[name,'NoTagCapture']
        assert no_capture['final']==groups[name,'NoAcetylcholine']['final']
        for mode in ('NoTagCapture','NoDopamine','NoAcetylcholine'):
            for field in ('preTrace','postTrace','eligibility','tag','theta','timeMs'):
                assert groups[name,mode]['final'][field]==base['final'][field]
        if name!='continuous-colocated':
            assert all(groups[name,m]['final']==base['final'] for m in ('NoTagCapture','NoDopamine','NoAcetylcholine'))
        cases.append(dict(case=name,recordedSignalsFinalWeight=base['final']['weight'],noCaptureFinalWeight=no_capture['final']['weight'],
            directFixedHistoryDifference=base['final']['weight']-no_capture['final']['weight'],lostUpdates=base['lostUpdates'],lostCharge=base['lostCharge']))
    report=dict(completed=True,originalCompleteBaselineReproduced=True,isolatedNumericalCriteriaPassed=all_pass,
        replayCases=16,cadenceCases=27,cases=cases,cadence=result['cadenceCases'],biologicalConsolidationQualified=False,
        liveBrainAccessed=False,liveIntegrationQualified=False,avatarRecoveryAssessed=False)
    destination=package/'numerical-study/analysis.json';assert not destination.exists()
    destination.write_text(json.dumps(report,indent=2,allow_nan=False)+'\n')
    print(json.dumps({k:v for k,v in report.items() if k!='cadence'},indent=2),flush=True)
    if not all_pass:raise RuntimeError('Candidate numerical gates failed; do not change bounds after the result.')

if __name__=='__main__':main(Path(sys.argv[1]).resolve())

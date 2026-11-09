"""Audit actual-cell precision events, delivery and warm-start storage; no simulation."""
from collections import Counter
import json
import math
from pathlib import Path
import struct
import sys

read=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
lines=lambda p:[json.loads(s) for s in p.read_text(encoding='utf-8-sig').splitlines()]
f32=lambda x:struct.unpack('<f',struct.pack('<f',x))[0]
clip=lambda x,a,b:min(b,max(a,x))
NAMES=['legacy-paired','precise-paired','precise-neighbor','precise-no-modulation','precise-stdp','precise-capture','precise-synthetic']

def transition(event,rule):
    old=event['before']['state']; s=old.copy(); time=event['timestampMs']; dt=time-old['timeMs']
    assert dt>=0
    s['timeMs']=time
    for field,tau in [('preTrace',20),('postTrace',35),('eligibility',900),('tag',8000)]: s[field]*=math.exp(-dt/tau)
    def local(tag):
        tag=clip(tag,-1,1); saturation=1-s['weight']/5 if tag>=0 else s['weight']/5
        return clip(tag*clip(saturation,f32(.05),1),-f32(.05),f32(.05))
    raw=0
    if event['kind'] in ('pre','post'):
        pre=event['kind']=='pre'; pair=-f32(.014)*s['postTrace'] if pre else f32(.018)*s['preTrace']
        field='preTrace' if pre else 'postTrace'; s[field]=min(8,s[field]+1)
        s['eligibility']=clip(s['eligibility']+pair,-1,1); s['tag']=clip(s['tag']+pair,-1,1)
        s['updates']=min(2147483646,s['updates'])+1
        if rule in ('STDP','STDP+SynapticTaggingCapture'): raw=local(pair)
    else:
        assert event['kind']=='modulation'
        interval=min(event['elapsedMs'],dt); activity=clip(event['activity'],0,1)
        s['theta']=clip(s['theta']+(activity*activity-s['theta'])*(1-math.exp(-interval/2500)),f32(.001),10)
        if 'SynapticTag' in rule and event['acetylcholine']>f32(.001):
            rate=local(s['tag'])*f32(.35)*clip(event['acetylcholine'],0,1)*(f32(.35)+f32(.65)*clip(event['dopamine'],0,1))
            raw=rate*(interval/1000)
        if raw!=0:s['updates']=min(2147483646,s['updates'])+1
    if raw!=0:
        budget=s['budget']
        if s['budgetTimeMs']>=0 and time>s['budgetTimeMs']:
            budget=min(f32(.04),budget+(time-s['budgetTimeMs'])/1000*f32(.01))
        scaled=raw*f32(.02); magnitude=min(abs(scaled),budget)
        s['weight']=clip(s['weight']+math.copysign(magnitude,scaled),f32(.05),5)
        s['budget']=budget-magnitude;s['budgetTimeMs']=time;s['chargedPlasticity']+=magnitude
    return s

def scrub(value):
    # Only per-run generated UUIDs are excluded. Connected identities, event
    # ordering, all numerical fields and biological clocks remain compared.
    if isinstance(value,dict):return {k:scrub(v) for k,v in value.items() if k!='messageId'}
    if isinstance(value,list):return [scrub(v) for v in value]
    return value

def audit(package):
    baseline=package/'legacy-baseline'; current=package/'legacy-study'
    previous=read(baseline/'protocol.json'); assert read(current/'protocol.json')==previous
    assert read(current/'results.json')['completed'] and len(previous['scenarios'])==7
    for scenario in previous['scenarios']:
        name=scenario['key']
        for filename in ('ticks.jsonl','inputs.jsonl','learning.jsonl','inbound.jsonl'):
            assert scrub(lines(baseline/name/filename))==scrub(lines(current/name/filename)),f'Legacy control changed: {name}/{filename}'
        assert read(baseline/name/'declared-targets.json')==read(current/name/'declared-targets.json')
    root=package/'study'; p=read(root/'protocol.json'); result=read(root/'results.json')
    assert p['version']=='cortical-precision-integration-v1' and p['numericalVersion']=='isolated-cortical-contact-double-v1'
    assert p['candidateVersion']=='isolated-cortical-receptor-persistence-double-v1'
    assert [s['key'] for s in p['scenarios']]==NAMES and result['completed'] and len(result['cases'])==7
    assert (p['ticks'],p['tickMs'],p['cycles'],p['cuesPerCycle'])==(550,1,10,12)
    assert (p['stateAbsoluteTolerance'],p['budgetAbsoluteTolerance'],p['receptorGainAbsoluteTolerance'])==(2e-12,2e-13,1e-11)
    summaries=[]
    for spec,row in zip(p['scenarios'],result['cases'],strict=True):
        name=spec['key']; folder=root/name; assert row==read(folder/'result.json') and row['key']==name and row['completed']
        events=lines(folder/'learning.jsonl'); receptors=lines(folder/'receptors.jsonl'); ticks=lines(folder/'ticks.jsonl')
        inputs=lines(folder/'inputs.jsonl'); final=read(folder/'final.json'); saved=read(folder/'trained-state.json')
        assert len(ticks)==550 and [t['tick'] for t in ticks]==list(range(1,551))
        assert len(inputs)==10 and [i['tick'] for i in inputs]==list(range(1,551,55))
        arrivals=[m for item in inputs for m in item['arrivals']]; assert len({m['messageId'] for m in arrivals})==len(arrivals)
        assert len(arrivals)==(120 if spec['modulationTarget'] is None else 140)
        firing_events=lines(folder/'firings.jsonl')
        firing_keys={(e['instance'],e['cell'],e['timestampMs']) for e in firing_events}
        assert len(firing_keys)==len(firing_events)
        firings=sum(e['cell']==166 for e in firing_events);assert firings==row['targetFirings'] and firings>0
        assert saved['SchemaVersion']==(5 if spec['precise'] else 4)
        explicit_cue=spec['precise'] and not spec['synthetic']
        cue_contacts=[c for c in final['precise'] if c['sourceInstance']=='isolated-precision-source']
        assert len(cue_contacts)==(1 if explicit_cue else 0)
        assert len(receptors)==row['deliveries']
        kinds=Counter(e['kind'] for e in events);assert [row[k] for k in ('pre','post','modulation')]==[kinds[k] for k in ('pre','post','modulation')]
        if spec['precise']:
            assert events and receptors and len(final['precise'])>0
            input_by_id={m['messageId']:m for m in arrivals}; last={}; maximum_error=0; small=0
            cue_events=[e for e in events if e['after']['sourceInstance']=='isolated-precision-source']
            cue_kinds=Counter(e['kind'] for e in cue_events)
            assert cue_kinds['pre']==(120 if explicit_cue else 0) and cue_kinds['post']==(firings if explicit_cue else 0)
            for e in events:
                before,after=e['before'],e['after']; a,b=before['state'],after['state']
                assert before['target']==after['target'] and before['targetInstance']==after['targetInstance']=='isolated-cortical-precision-study'
                assert {k:v for k,v in before.items() if k!='state'}=={k:v for k,v in after.items() if k!='state'}
                identity=a['contactId']
                if identity in last:assert a==last[identity]
                predicted=transition(e,spec['rule'])
                for field,value in predicted.items():
                    if isinstance(value,(str,int)):assert b[field]==value
                    else:
                        assert math.isfinite(b[field]);error=abs(b[field]-value);maximum_error=max(maximum_error,error)
                        assert error<=(p['budgetAbsoluteTolerance'] if field=='budget' else p['stateAbsoluteTolerance']),(name,e['kind'],field,error)
                if e['kind']=='pre' and e['messageId'] in input_by_id:
                    source=input_by_id[e['messageId']];assert source['timestampMs']<=e['timestampMs']
                elif e['kind']=='post':assert (after['targetInstance'],after['target'],e['timestampMs']) in firing_keys
                if spec['modulationTarget'] in (None,165) and after['sourceInstance']=='isolated-precision-source':
                    assert e['dopamine']==e['acetylcholine']==0
                small+=a['weight']!=b['weight'] and f32(a['weight'])==f32(b['weight']);last[identity]=b
            assert last=={c['state']['contactId']:c['state'] for c in final['precise']} and small==row['smallChanges']
            if spec['modulationTarget'] in (None,165):assert cue_kinds['modulation']==0
            elif explicit_cue:assert cue_kinds['modulation']>0 and any(e['dopamine']>0 and e['acetylcholine']>0 for e in cue_events)
            if name=='precise-paired':assert small>0,'No sub-float learned increments were exercised.'
            gains={};maximum_gain_error=0
            pre_by_id={(e['messageId'],e['after']['state']['contactId']):e for e in events if e['kind']=='pre'}
            assert len(pre_by_id)==kinds['pre']==len(receptors) and {(r['messageId'],r['contactId']) for r in receptors}==set(pre_by_id)
            for r in receptors:
                e=pre_by_id[r['messageId'],r['contactId']];assert r['target']==e['after']['target']
                assert r['arrivalMs']<=r['appliedMs']==e['timestampMs'] and r['weight']==e['before']['state']['weight']
                release=r['release'];assert release==f32(release)
                if r['messageId'] in input_by_id:assert release==f32(input_by_id[r['messageId']]['vesicleQuanta'])
                assert r['effectiveQuanta']==clip(clip(release,f32(.05),5)*r['weight'],f32(.05),5)
                actual=(r['after']['ampa']-r['before']['ampa'])/r['effectiveQuanta'];assert actual>0
                gain=gains.setdefault(r['contactId'],actual)
                maximum_gain_error=max(maximum_gain_error,abs(actual-gain));assert abs(actual-gain)<=p['receptorGainAbsoluteTolerance']
            stored_precise={c['SynapseId']:c for c in saved['Inbound'] if 'CorticalPrecisionState' in c}
            assert set(stored_precise)==set(last)
            for identity,state in last.items():
                entry=stored_precise[identity];stored=entry['CorticalPrecisionState']
                assert {k[0].lower()+k[1:]:v for k,v in stored.items()}==state
                assert f32(entry['VesicleQuanta'])==f32(state['weight'])
        else:
            assert not events and not receptors and not final['precise'];maximum_error=maximum_gain_error=0;small=0
        if spec['precise']:
            warm=read(folder/'warm.json');assert not warm['membraneRestored'] and not warm['queuedEventsRestored']
            assert len(warm['before'])==len(final['precise'])
            after_by_id={c['state']['contactId']:c for c in warm['after']}
            for before,trained in zip(warm['before'],final['precise'],strict=True):
                reset=dict(trained['state'],timeMs=0,preTrace=0,postTrace=0,eligibility=0,tag=0,budgetTimeMs=-1)
                assert before==dict(trained,state=reset)
                after=after_by_id[reset['contactId']]
                assert 1000<=after['state']['timeMs']<=1001 and 1000<=after['state']['budgetTimeMs']<=1001
                if before['sourceInstance']=='isolated-precision-source':
                    assert after['state']['weight']==reset['weight'] and after['state']['budget']==reset['budget']
            assert read(folder/'warm-state.json')['SchemaVersion']==5
        summaries.append(dict(scenario=name,neuronGeneratedTargetFirings=firings,preciseLearningEvents=len(events),
            subFloatWeightUpdates=small,precisionDeliveries=len(receptors),maximumStateTransitionError=maximum_error,
            maximumReceptorGainError=maximum_gain_error,warmStartChecked=spec['precise']))
    return dict(completed=True,legacySevenControlsReproduced=True,integrationCriteriaPassed=True,cases=summaries,
        fullUnitSuitePassed=False,biologicalConsolidationQualified=False,physiologyQualified=False,avatarRecoveryAssessed=False)

if __name__=='__main__':
    package=Path(sys.argv[1]).resolve(); result=audit(package)
    with (package/'precision-analysis.json').open('x',encoding='utf-8') as stream: stream.write(json.dumps(result,indent=2)+'\n')
    print('Seven legacy controls reproduced; actual-cell precision, local delivery and explicit warm-start criteria passed.')
    print('The reduced learning rule is not biochemical consolidation or Avatar recovery qualification.')

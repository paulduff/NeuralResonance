"""Analyse completed diagnostic captures; no learning-rule fitting or simulation."""
from collections import Counter
import json
import math
from pathlib import Path
import sys

read=lambda path:json.loads(path.read_text(encoding='utf-8-sig'))
lines=lambda path:[json.loads(s) for s in path.read_text(encoding='utf-8-sig').splitlines()]


def main(root):
    output=root/'analysis.json';assert not output.exists()
    report=dict(completed=False,biologicalConsolidationQualified=False,originalTestChanged=False,cases=[])
    try:
        protocol=read(root/'protocol.json');results=read(root/'results.json')
        assert protocol['version']=='cortical-memory-diagnostic-v2' and results['completed'] and len(results['cases'])==7
        names=['legacy-observed','legacy-unobserved','continuous-simultaneous','continuous-spaced','continuous-unmodulated',
               'continuous-colocated','continuous-neighbor']
        assert [s['key'] for s in protocol['scenarios']]==names and [s['key'] for s in results['cases']]==names
        captures={}
        for scenario,result in zip(protocol['scenarios'],results['cases']):
            folder=root/scenario['key'];ticks=lines(folder/'ticks.jsonl');inputs=lines(folder/'inputs.jsonl')
            evidence=lines(folder/'learning.jsonl');inbound=lines(folder/'inbound.jsonl')
            assert result['completed'] and len(ticks)==len(inputs)==result['ticks']==18
            assert len(evidence)==result['learningEvents'] and len(inbound)==result['inboundEvents']
            assert [t['signal']['tick'] for t in ticks]==list(range(1,19))
            assert [t['signal']['timestampMs'] for t in ticks]==[55*r+offset for r in range(9) for offset in (25,55)]
            assert [t['signal']['tickDurationMs'] for t in ticks]==([25,30]*9 if scenario['continuous'] else [10]*18)
            assert all(0<=t['ack']['synapticLearningDiagnostics']['contactSampleTimestampMs']<=t['signal']['timestampMs'] for t in ticks)
            for index,(input_row,tick) in enumerate(zip(inputs,ticks)):
                assert input_row['repetition']==tick['repetition']==index//2
                assert input_row['phase']==tick['phase']==('cue' if index%2 else 'modulation')
                assert len(input_row['spikes'])==(12 if index%2 else 14 if scenario['modulation'] else 0)
                if index%2:
                    assert [s['timestampMs'] for s in input_row['spikes']]==[
                        55*(index//2)+30+scenario['spacingMs']*n for n in range(12)]
            injected=[s for row in inputs for s in row['spikes']]
            assert len({s['messageId'] for s in injected})==len(injected)
            cues=[t for t in ticks if t['phase']=='cue'];assert len(cues)==9
            # Follow the original assertion's ensemble selection as well as the
            # actual cue contact; a recurrent ensemble may be strongest.
            fixture_ensemble=max(cues[0]['ack']['synapticMemoryDiagnostics']['ensembles'],key=lambda x:x['engramStrength'])['ensembleIndex']
            fixture_scores=[t['ack']['synapticMemoryDiagnostics']['ensembles'][fixture_ensemble]['consolidation'] for t in cues]
            summary=[];glutamate=None;maximum_aggregate_error=0.
            for t in cues:
                contacts=t['contacts'];feature=[x for x in contacts if x['sourceNeuron']=='memory-feature-140']
                assert len(feature)==1
                contact=feature[0];glutamate=contact['neurotransmitter'];ensemble=contact['target']//4%8
                selected=t['ack']['synapticMemoryDiagnostics']['ensembles'][ensemble]
                supported=[x for x in contacts if x['neurotransmitter']==glutamate and x['updates']>0 and x['target']//4%8==ensemble]
                assert selected['supportingSynapses']==len(supported)
                def score(x):
                    change=max(0,x['weight']-x['baseline'])/max(.05,5-x['baseline'])
                    repetition=min(1,max(0,math.log2(x['updates']+1)/6))
                    return min(1,max(0,change*(.3+.7*repetition)))
                reconstructed=sum(score(x) for x in supported)/max(1,len(supported))
                maximum_aggregate_error=max(maximum_aggregate_error,abs(reconstructed-selected['consolidation']))
                summary.append(dict(repetition=t['repetition'],timeMs=t['signal']['timestampMs'],contactId=contact['contactId'],
                    weight=contact['weight'],baseline=contact['baseline'],tag=contact['tag'],eligibility=contact['eligibility'],
                    budget=contact['budget'],updates=contact['updates'],supportingSynapses=len(supported),
                    consolidationScore=selected['consolidation'],reconstructedConsolidationScore=reconstructed))
            assert maximum_aggregate_error<=1e-6, 'Aggregate diverges from stored contacts; inspect the preserved capture.'
            contributions={kind:dict(positive=0.,negative=0.,events=0) for kind in ('pre','post','modulation')}
            contact_id=summary[0]['contactId'];net={};last={}
            for event in evidence:
                before=event['before'];after=event['after'];identity=after['contactId']
                assert before['contactId']==identity and after['timestampMs']==event['timestampMs']
                assert event['timestampMs']>=before['timestampMs']-1e-6
                delta=after['weight']-before['weight'];net[identity]=net.get(identity,0.)+delta;last[identity]=after
                if identity==contact_id:
                    row=contributions[event['kind']];row['events']+=1
                    row['positive']+=max(0,delta);row['negative']+=min(0,delta)
            if scenario['observe']:
                external_sources={s['sourceStructure'] for s in injected}
                external_inbound=[x for x in inbound if x['source'] in external_sources]
                # Local cortical feedback is retained and counted separately;
                # it must not be mistaken for duplicate injected events.
                expected=Counter((s['sourceStructure'],s['neurotransmitter']) for s in injected)
                delivered=Counter((x['source'],x['neurotransmitter']) for x in external_inbound)
                assert expected==delivered, 'Injected event delivery coverage changed.'
                external_pre=[x for x in evidence if x['kind']=='pre' and x['before']['source'] in external_sources]
                assert Counter(x['messageId'] for x in external_pre)==Counter(s['messageId'] for s in injected)
                input_by_id={s['messageId']:s for s in injected}
                for event,arrival in zip(external_pre,external_inbound,strict=True):
                    source=input_by_id[event['messageId']]
                    assert event['before']['source']==arrival['source']==source['sourceStructure']
                    assert event['before']['neurotransmitter']==arrival['neurotransmitter']==source['neurotransmitter']
                    assert event['timestampMs']==arrival['processingTimestampMs']
                    assert event['before']['target']==arrival['targetNeuronIndex']
                    assert source['timestampMs']<=arrival['scheduledDeliveryTimestampMs']<=event['timestampMs']
                assert all(x['processingTimestampMs']>=x['scheduledDeliveryTimestampMs'] for x in inbound)
                for contact in ticks[-1]['contacts']:
                    assert abs(net[contact['contactId']]-(contact['weight']-contact['baseline']))<=1e-7
                    assert last[contact['contactId']]==contact
            else:
                assert not evidence and not inbound
                external_inbound=[]
            delays=[x['processingTimestampMs']-x['scheduledDeliveryTimestampMs'] for x in inbound]
            declared=sum(t['signal']['tickDurationMs'] for t in ticks)
            feature_events=[e for e in evidence if e['after']['contactId']==contact_id]
            local_da=max((e['dopamine'] for e in feature_events),default=None)
            local_ach=max((e['acetylcholine'] for e in feature_events),default=None)
            target_map=read(folder/'declared-targets.json')
            if scenario['modulatorTarget'] is not None:
                assert target_map['fingerprint'] and len(target_map['entries'])==126
                assert {e['targetCell'] for e in target_map['entries']}=={scenario['modulatorTarget']}
                mod_contacts=[c for c in ticks[-1]['contacts'] if c['neurotransmitter'] in (3,5,6)]
                assert len(mod_contacts)==126 and {c['target'] for c in mod_contacts}=={scenario['modulatorTarget']}
                assert {e['after']['target'] for e in feature_events}=={166}
                if scenario['modulatorTarget']==166:
                    assert local_da>0 and local_ach>0 and contributions['modulation']['events']>0
                else:
                    assert local_da==local_ach==0 and contributions['modulation']['events']==0
            else:assert target_map['fingerprint'] is None and target_map['entries'] is None
            row=dict(scenario=scenario['key'],declaredIntegratedMilliseconds=declared,outerTimestampSpanMs=495,
                learningEvents=len(evidence),inboundEvents=len(inbound),maximumDeliveryLatenessMs=max(delays,default=None),
                injectedEvents=len(injected),observedInjectedEvents=len(external_inbound),
                otherInboundEvents=len(inbound)-len(external_inbound),
                fixtureSelectedEnsemble=fixture_ensemble,fixtureConsolidationScores=fixture_scores,
                fixtureScoreIncreased=fixture_scores[-1]>fixture_scores[0],
                consolidationScoreIncreased=summary[-1]['consolidationScore']>summary[0]['consolidationScore'],
                featureWeightIncreased=summary[-1]['weight']>summary[0]['weight'],
                featureWeightChange=summary[-1]['weight']-summary[0]['weight'],
                maximumAggregateReconstructionError=maximum_aggregate_error,featureLearningContributions=contributions,cues=summary)
            row.update(declaredModulatorTarget=scenario['modulatorTarget'],targetMapFingerprint=target_map['fingerprint'],
                       maximumCueDopamine=local_da,maximumCueAcetylcholine=local_ach)
            report['cases'].append(row);captures[scenario['key']]=ticks
        observed=captures['legacy-observed'];unobserved=captures['legacy-unobserved']
        for a,b in zip(observed,unobserved):
            assert a['signal']==b['signal'] and a['contacts']==b['contacts'] and a['ack']==b['ack'], 'Observation changed retained state.'
            # Original output event IDs are intentionally generated afresh in
            # each fresh run; preserve them on disk, compare the full remaining
            # event payload and order, not just aggregate firing counts.
            scrub=lambda items:[{k:v for k,v in item.items() if k!='messageId'} for item in items]
            assert scrub(a['emitted'])==scrub(b['emitted']), 'Observation changed outbound events.'
        baseline=read(root.parent/'cortical-baseline/analysis.json')
        assert baseline['completed'] and len(baseline['cases'])==5
        for previous,current in zip(baseline['cases'],report['cases'][:5],strict=True):
            assert previous=={key:current[key] for key in previous}, 'An unchanged diagnostic control diverged from its retained baseline.'
        report.update(completed=True,observationInvariancePassed=True,captureIntegrityPassed=True,
            declaredRecipientChecksPassed=True,previousFiveControlsReproduced=True,
            diagnosticLegacyFixtureScoreIncreased=report['cases'][0]['fixtureScoreIncreased'])
        output.write_text(json.dumps(report,indent=2,allow_nan=False)+'\n')
        for case in report['cases']:
            first,last=case['cues'][0],case['cues'][-1]
            print(f"{case['scenario']}: weight {first['weight']:.8f} -> {last['weight']:.8f}; score {first['consolidationScore']:.8f} -> {last['consolidationScore']:.8f}; integrated {case['declaredIntegratedMilliseconds']} ms",flush=True)
        print('Diagnostic capture complete. Score changes do not qualify biological consolidation.',flush=True)
    except Exception as error:
        report['error']=repr(error);output.write_text(json.dumps(report,indent=2,allow_nan=False)+'\n');raise


if __name__=='__main__':main(Path(sys.argv[1]).resolve())

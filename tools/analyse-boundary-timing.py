"""Validate boundary-only controls and measure the causal scheduling contrast."""
import csv
from hashlib import sha256
import json
import math
from pathlib import Path
import struct
import sys

read=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
def digest(p):
    h=sha256()
    with p.open('rb') as f:
        for b in iter(lambda:f.read(8*1024*1024),b''):h.update(b)
    return h.hexdigest().upper()

def rows(path):
    with path.open(newline='') as f:yield from csv.DictReader(f)

def analyse(root,write_report=True,cell_local=False):
    root=Path(root);p=read(root/'protocol.json');bundle=read(root/'inputs.json');results=read(root/'results.json')
    baseline=read(root.parent/'baseline.json');duration=8000 if p['smoke'] else 60000
    assert p['study']=='boundary-timing' and p['durationMs']==duration and p['caseCount']==24 and p['hostStepMs']==.125
    assert p['conditions']==['connected','disconnected','markers'] and p['steps']==[.125,.0625,.03125,.015625]
    assert p.get('cellLocalIntegration',False) is cell_local
    if cell_local:assert p['integrationVersion']=='isolated-cell-local-arrivals-v1'
    assert digest(root/'inputs.json')==p['bundleSha256'] and results['completed'] and not results['physiologicallyQualified']
    assert bundle['durationMs']==60000 and len(bundle['replayCases'])==2
    expected={(s['id'],h,k) for s in bundle['replayCases'] for h in p['steps'] for k in p['conditions']}
    assert len(results['cases'])==len(expected)==24
    lookup={};states={};baseline_matches=0;event_count=0;trace_count=0;contact_count=0;state_count=0
    max_energy_error=0.;max_calcium_error=0.
    for c in results['cases']:
        k=(c['sourceId'],c['step'],c['boundaryCondition']);assert k in expected;expected.remove(k);lookup[k]=c
        source=next(s for s in bundle['replayCases'] if s['id']==c['sourceId']);condition=c['boundaryCondition']
        assert c['kind']=='replay' and c['duration']==duration and c['variant']==('calcium' if condition=='connected' else 'disconnected')
        assert c.get('cellLocalIntegration',False) is cell_local
        directory=root/c['key'];design=read(directory/'design.json');assert read(directory/'summary.json')==c
        for field in ('key','kind','sourceId','duration','variant','step','boundaryCondition'):assert design[field]==c[field]
        assert design.get('cellLocalIntegration',False) is cell_local
        inputs=[x for x in source['inputs'] if x['arrivalMs']<duration and (condition=='connected' or x['role']!='climbing')]
        boundaries=sorted({x['arrivalMs'] for x in source['inputs'] if x['role']=='climbing' and x['arrivalMs']<duration}) if condition=='markers' else None
        contacts=[{name:x[name] for name in ('role','contactId','target')} for x in source['contacts']]
        assert design['inputs']==inputs and design['noOpBoundaries']==boundaries and design['contacts']==contacts
        assert c['inputs']==len(inputs) and c['boundariesProcessed']==len(boundaries or [])
        declared={x['message']['messageId']:x for x in inputs};assert len(declared)==len(inputs)
        seen=set();observed_boundaries=[];spikes=[[],[]];spike_ids=set();contact_ids=set()
        expected_roles={x['role']:x for x in source['contacts']}
        for line in (directory/'events.jsonl').open():
            e=json.loads(line);x=e['value'];event_count+=1
            if e['kind']=='boundary':
                assert condition=='markers' and x['scheduledMs']==x['processedMs']
                observed_boundaries.append(x['scheduledMs'])
            elif e['kind']=='learning':
                mid=x['messageId'];assert mid in declared and mid not in seen;seen.add(mid);item=declared[mid];message=item['message']
                assert x['scheduledMs']==x['processedMs']==item['arrivalMs']
                before={v['contactId']:v for v in x['before']};after={v['contactId']:v for v in x['after']}
                assert before.keys()==after.keys() and x['contactId'] in before;contact_ids.add(x['contactId'])
                owner=before[x['contactId']]
                assert owner['sourceInstance']==message['sourceInstanceKey'] and owner['sourceNeuron']==message['sourceNeuronId'] and owner['source']==message['sourceStructure']==x['source']
                target=source['taughtCell'] if item['role']=='climbing' else expected_roles[item['role']]['target']
                assert x['target']==owner['target']==target
                if item['role']!='climbing':assert x['contactId']==expected_roles[item['role']]['contactId']
                for cid,a in after.items():
                    b=before[cid];assert a['weight']==b['weight']
                    for field in ('sourceInstance','source','sourceNeuron','target','synthetic'):assert a[field]==b[field]
                    if a['target']!=target:assert a==b
            elif e['kind']=='spike':
                assert x['messageId'] not in spike_ids;spike_ids.add(x['messageId'])
                assert x['sourceStructure']=='PurkinjeCellLayer' and x['neurotransmitter']=='GABA' and x['sourceNeuronId'] in ('n-000','n-001')
                spikes[int(x['sourceNeuronId'][2:])].append(x['timestampMs'])
            else:raise AssertionError(e['kind'])
        assert seen==declared.keys() and observed_boundaries==(boundaries or []) and spikes==c['spikes']
        for train in spikes:assert all(0<t<=duration for t in train) and all(a<b for a,b in zip(train,train[1:]))
        assert {x['contactId'] for x in c['contacts']}==contact_ids
        if condition!='connected':assert contact_ids=={x['contactId'] for x in source['contacts']}
        count=0
        for raw in rows(directory/'trace.csv'):
            x={k:float(v) for k,v in raw.items()};count+=1;trace_count+=1
            assert x['timeMs']==count*2 and all(math.isfinite(v) for v in x.values())
            for j in (0,1):
                assert 0<=x[f'p{j}Reserve']<=1 and abs(x[f'p{j}BalanceError'])<1e-7 and x[f'p{j}Unmet']==0
                max_energy_error=max(max_energy_error,abs(x[f'p{j}BalanceError']))
        assert count==duration//2
        previous={};final={};first={role:min(x['arrivalMs'] for x in inputs if x['role']==role) for role in expected_roles}
        for raw in rows(directory/'contacts.csv'):
            role=raw.pop('role');assert role in expected_roles;x={k:float(v) for k,v in raw.items()};contact_count+=1
            assert all(math.isfinite(v) for v in x.values()) and x['timeMs']==previous.get(role,math.ceil(first[role]/2)*2-2)+2
            previous[role]=x['timeMs'];assert .628-1e-7<=x['weight']<=1
            if role!='nonparallel':
                error=abs(x['calcium']+x['store']-(5.05+x['entry']-x['extruded']));max_calcium_error=max(max_calcium_error,error,x['massError'])
                assert error<1e-7 and x['massError']<1e-7 and x['calcium']>=.05-1e-10 and 0<=x['store']<=5+1e-10 and 0<=x['inducedDepression']<=.372
            else:assert x['weight']==1 and x['calcium']==0
            final[role]=x
        assert previous==dict.fromkeys(expected_roles,duration)
        for role,contact in expected_roles.items():
            weight=next(x['state']['weight'] for x in c['contacts'] if x['contactId']==contact['contactId'])
            assert final[role]['weight']==struct.unpack('f',struct.pack('f',weight))[0]
        cell_rows=[[],[]];headers=None
        for i,raw in enumerate(rows(directory/'cell-states.csv')):
            x={k:float(v) for k,v in raw.items()};state_count+=1
            assert all(math.isfinite(v) for v in x.values()) and x['timeMs']==2*(i//2+1) and x['cell']==i%2
            assert 0<=x['reserve']<=1 and x['unmet']==0 and abs(x['balanceError'])<1e-7
            assert abs(x['reserve']-(1+x['produced']-x['consumed']-x['overflow']))<1e-7
            assert abs(x['requested']-x['consumed']-x['unmet'])<1e-7
            assert all(-1e-8<=x[g]<=1+1e-8 for g in ('m','h','n','q','p','l'))
            headers=list(x) if headers is None else headers;assert headers==list(x)
            cell_rows[i%2].append(tuple(x.values()))
        assert all(len(x)==duration//2 for x in cell_rows)
        states[c['key']]=(headers,cell_rows)
        if condition!='markers' and not cell_local:
            old=next(x for x in baseline['cases'] if (x['sourceId'],x['step'],x['condition'])==k)
            assert spikes==[[t for t in train if t<=duration] for train in old['spikes']]
            for name in ('trace.csv','contacts.csv'):
                assert digest(directory/name)==old['files'][name][str(duration)]['sha256'],(c['key'],name,'archived baseline changed')
            baseline_matches+=1
    assert not expected and baseline_matches==(0 if cell_local else 16)
    comparisons=[]
    for s in bundle['replayCases']:
        j=1-s['taughtCell']
        for h in p['steps']:
            connected=lookup[(s['id'],h,'connected')];disconnected=lookup[(s['id'],h,'disconnected')];markers=lookup[(s['id'],h,'markers')]
            def compare(a,b,cell):
                header,aa=states[a['key']];other,bb=states[b['key']];assert header==other
                pairs=list(zip(aa[cell],bb[cell]));different=next(((x[0],{field:[x[i],y[i]] for i,field in enumerate(header) if x[i]!=y[i]}) for x,y in pairs if x!=y),None)
                ta,tb=a['spikes'][cell],b['spikes'][cell]
                return dict(firstCount=len(ta),secondCount=len(tb),spikesIdentical=ta==tb,
                    maximumOrdinalDifferenceMs=max((abs(x-y) for x,y in zip(ta,tb)),default=0) if len(ta)==len(tb) else None,
                    sampledStatesIdentical=different is None,firstDifferentSample=None if different is None else dict(timeMs=different[0],fields=different[1]),
                    maximumSampledVoltageDifferenceMv=max(abs(x[2]-y[2]) for x,y in pairs))
            comparisons.append(dict(sourceId=s['id'],step=h,untaughtCell=j,
                connectedVersusDisconnected=compare(connected,disconnected,j),connectedVersusMarkers=compare(connected,markers,j),
                disconnectedVersusMarkers=[compare(disconnected,markers,i) for i in (0,1)],
                taughtConnectedVersusMarkers=compare(connected,markers,s['taughtCell'])))
    report=dict(completed=True,accountingValidated=True,caseCount=24,baselineMatches=baseline_matches,traceRows=trace_count,contactRows=contact_count,stateRows=state_count,eventRows=event_count,
        maximumEnergyBalanceError=max_energy_error,maximumCalciumMassError=max_calcium_error,
        boundaryHypothesisSupported=all(c['connectedVersusMarkers']['spikesIdentical'] and c['connectedVersusMarkers']['sampledStatesIdentical'] for c in comparisons) and any(not c['connectedVersusDisconnected']['sampledStatesIdentical'] for c in comparisons),
        solverConvergenceQualified=False,physiologicalCalibrationComplete=False,physicalRecoveryAssessed=False,comparisons=comparisons)
    if cell_local:
        def identical(x):return x['spikesIdentical'] and x['sampledStatesIdentical']
        locality=all(identical(c['connectedVersusDisconnected']) and identical(c['connectedVersusMarkers']) and
            all(identical(x) for x in c['disconnectedVersusMarkers']) for c in comparisons)
        # Compare complete recorded contact trajectories, including the untouched
        # cell in the connected contrast. Do not substitute similar final weights.
        contact_checks=[]
        for s in bundle['replayCases']:
            for h in p['steps']:
                cases={k:lookup[(s['id'],h,k)] for k in p['conditions']}
                def contact_digest(case,roles):
                    digest=sha256()
                    for row in rows(root/case['key']/'contacts.csv'):
                        if row['role'] in roles:digest.update(json.dumps(row,sort_keys=True).encode())
                    return digest.hexdigest()
                all_roles={c['role'] for c in s['contacts']}
                untouched={c['role'] for c in s['contacts'] if c['target']!=s['taughtCell']}
                assert untouched
                marker_equal=contact_digest(cases['disconnected'],all_roles)==contact_digest(cases['markers'],all_roles)
                untouched_equal=contact_digest(cases['connected'],untouched)==contact_digest(cases['disconnected'],untouched)
                contact_checks.append(dict(source=s['id'],step=h,markersIdentical=marker_equal,untouchedIdentical=untouched_equal))
        report.update(cellLocalIntegration=True,cellLocalityPassed=locality and all(x['markersIdentical'] and x['untouchedIdentical'] for x in contact_checks),contactLocality=contact_checks)
    if write_report:(root/'cell-analysis.json').write_text(json.dumps(report,indent=2,allow_nan=False)+'\n',encoding='utf-8')
    print(f'Validated 24 cases, {baseline_matches} exact archived baselines, {event_count} events. Boundary hypothesis supported: {report["boundaryHypothesisSupported"]}; physiological and solver qualification remain open.')
    return report

if __name__=='__main__':analyse(Path(sys.argv[1]))

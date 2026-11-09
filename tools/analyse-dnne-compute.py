"""Audit saved performance evidence, without executing any neural calculation."""
from hashlib import sha256
import json
import math
from pathlib import Path
from statistics import median
import sys

root = Path(sys.argv[1]).resolve()
read = lambda p: json.loads(p.read_text(encoding='utf-8-sig'))
data = read(root/'results.json')
protocol = read(root/'protocol.json')
assert data['completed'] and protocol['version'] == 'isolated-cpu-costs-v1'
assert not any(data[k] for k in ('gpuMeasured','networkTransportMeasured','unityPhysicsMeasured','physiologyQualified','avatarRecoveryAssessed'))
assert protocol['durationMs'] == 1000 and protocol['pairs'] == 3
expected = ['temporal-live-rule','temporal-tag-candidate','granule-live-rule','olive-live-rule','purkinje-live-rule','spinal-live-rule']
assert [c['Key'] for c in protocol['cases']] == expected
assert len(data['neuralRuns']) == 42 and len(data['body']['runs']) == 21

def finite_nonnegative(values):
    assert all(math.isfinite(x) and x >= 0 for x in values)

def verified(path, expected_hash):
    assert sha256(path.read_bytes()).hexdigest().upper() == expected_hash, str(path)
    return read(path)

def strip_message_ids(obj):
    if isinstance(obj, dict): return {k:strip_message_ids(v) for k,v in obj.items() if k != 'MessageId'}
    if isinstance(obj, list): return [strip_message_ids(x) for x in obj]
    return obj

neural = []
for key in expected:
    rows = [r for r in data['neuralRuns'] if r['Key'] == key]
    assert len(rows) == 7 and sum(r['Pair'] == -1 for r in rows) == 1
    saved = {}
    for r in rows:
        path = root/key/r['Run']
        assert read(path/'timing.json') == r
        semantic = verified(path/'semantic-outputs.json', r['OutputsSha256'])
        state = verified(path/'state-evidence.json', r['StateSha256'])
        raw = read(path/'raw-outputs.json')
        assert strip_message_ids(raw) == semantic
        assert len(raw) == 50 and len(state['_neurons']) == r['Cells'] > 0
        assert len(state['_inboundSynapses']) == r['Contacts'] > 0
        assert len(state['_cellPostTrace']) == len(state['_cellPostTimestamp']) == r['Cells']
        assert set(state) == {'_neurons','_inboundSynapses','_outboundSynapses','_cellPostTrace','_cellPostTimestamp',
                              '_lastBiologicalTimestampMs','_lastProcessedTick','_feedForward','_feedback'}
        assert all(raw[i]['tick']['TimestampMs'] == (i+1)*20 for i in range(50))
        outgoing = [event for frame in raw for event in frame['outgoing']]
        assert len(outgoing) == r['Emitted']
        ids = [e['MessageId'] for e in outgoing]
        assert len(ids) == len(set(ids)) and '00000000-0000-0000-0000-000000000000' not in ids
        finite_nonnegative([r[k] for k in ('EnqueueMs','StepMs','DrainMs','CompleteWithEvidenceAndShutdownMs','ProcessCpuMs','TimedCallsAllocatedBytes')])
        assert r['StepMs'] > 0
        phases = {p['Phase']:p for p in r['Phases']}
        if r['Instrumented']:
            assert set(phases) == {'TickTotal','DueInputs','CellDynamics','ContactLearning','SpikeRouting','Diagnostics'}
            assert phases['TickTotal']['Calls'] == 50
            assert phases['CellDynamics']['Calls'] == phases['ContactLearning']['Calls'] == phases['SpikeRouting']['Calls'] > 0
            assert phases['Diagnostics']['Calls'] == 50 * (r['Cells'] + 1)
            for phase in phases.values():
                finite_nonnegative([phase['Calls'],phase['StopwatchTicks'],phase['Milliseconds']])
                assert math.isclose(phase['Milliseconds'],phase['StopwatchTicks']*1000/protocol['stopwatchFrequency'],rel_tol=1e-12,abs_tol=1e-9)
            assert sum(p['StopwatchTicks'] for name,p in phases.items() if name != 'TickTotal') <= phases['TickTotal']['StopwatchTicks']
        else: assert not phases
        saved[r['Run']] = (semantic, state)
    for pair in range(3):
        contrast = [r for r in rows if r['Pair'] == pair]
        assert len(contrast) == 2 and {r['Instrumented'] for r in contrast} == {False,True}
        assert saved[contrast[0]['Run']] == saved[contrast[1]['Run']], (key,pair)
    plain = [r for r in rows if r['Pair'] >= 0 and not r['Instrumented']]
    timed = [r for r in rows if r['Pair'] >= 0 and r['Instrumented']]
    phase_medians = {name:median(next(p['Milliseconds'] for p in r['Phases'] if p['Phase']==name) for r in timed)
                     for name in ('DueInputs','CellDynamics','ContactLearning','SpikeRouting','Diagnostics')}
    neural.append(dict(key=key,cells=plain[0]['Cells'],contacts=[r['Contacts'] for r in plain],
        plainTickMilliseconds=[r['StepMs'] for r in plain],instrumentedTickMilliseconds=[r['StepMs'] for r in timed],
        medianPlainTickMs=median(r['StepMs'] for r in plain),
        medianPlainEnqueueAndDrainMs=median(r['EnqueueMs']+r['DrainMs'] for r in plain),
        instrumentedPhaseMedianMs=phase_medians,
        medianTimingOverheadRatio=median(r['StepMs'] for r in timed)/median(r['StepMs'] for r in plain)))

body = []
for count in (1,32,256):
    rows = [r for r in data['body']['runs'] if r['Units']==count]
    assert len(rows) == 7 and sum(r['Pair']==-1 for r in rows)==1
    saved = {}
    for r in rows:
        path=root/'motor-units'/(str(count)+'-'+r['Run'])
        assert read(path/'timing.json')==r
        frames=verified(path/'frames.json',r['OutputsSha256'])
        state=verified(path/'state-evidence.json',r['StateSha256'])
        assert len(frames)==50 and frames[-1]['Frame']['BodyTimeMs']==1000
        last=frames[-1]['Frame']; assert last['AcceptedEvents']==last['DeliveredEvents']==count*25
        assert last['PendingEvents']==0 and not last['Faulted']
        assert len(last['Units'])==count and all(u['Arrivals']==25 for u in last['Units'])
        finite_nonnegative([r[k] for k in ('PublishMs','SerializationMs','ReceiveMs','AdvanceMs','SerializedBytes')])
        assert (r['SerializedBytes']>0)==r['JsonRoundTrip']
        saved[r['Run']]=(frames,state)
    for pair in range(3):
        contrast=[r for r in rows if r['Pair']==pair]
        assert len(contrast)==2 and {r['JsonRoundTrip'] for r in contrast}=={False,True}
        assert saved[contrast[0]['Run']]==saved[contrast[1]['Run']],(count,pair)
    measured=[r for r in rows if r['Pair']>=0 and r['JsonRoundTrip']]
    body.append(dict(units=count,medianMilliseconds={k:median(r[k] for r in measured)
        for k in ('PublishMs','SerializationMs','ReceiveMs','AdvanceMs')},serializedBytes=[r['SerializedBytes'] for r in measured]))

assert data['exactNeuralObservationPairs']==18 and data['body']['exactSerializationPairs']==9
report=dict(completed=True,neuralObservationPairs=18,motorSerializationPairs=9,neural=neural,motor=body,
    scope='isolated in-process current-model costs; no live service network, Unity physics or CUDA measurement',
    generatedNeuralMessageIdsExcluded=True,fullBrainCheckpointCompared=False,
    physiologicalQualification=False,avatarRecoveryAssessed=False)
(root/'analysis.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
print('Saved CPU cost evidence audited: 18 neural observation pairs and 9 motor serialization pairs.')
for r in neural: print(r['key'], 'plain ticks median ms:', round(r['medianPlainTickMs'],3),'timing overhead ratio:',round(r['medianTimingOverheadRatio'],3))
print('These measurements do not qualify physiology or establish a CUDA speedup.')

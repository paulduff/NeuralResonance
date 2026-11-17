"""Read-only audit of saved receptor/motor trajectories; never advances a model."""
from hashlib import sha256
import json
import math
from pathlib import Path
import re
import sys
import uuid
import numpy as np
from receptor_membrane_protocol import protocol, crossing_mode, cell_version

read = lambda p: json.loads(p.read_text(encoding='utf-8-sig'))
GATES = np.r_[np.arange(5, 14), np.arange(15, 27)]


def digest(path):
    h = sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(4 * 1024 * 1024), b''): h.update(block)
    return h.hexdigest().upper()


def stable(*parts):
    values = [('True' if p else 'False') if isinstance(p, bool) else str(p) for p in parts]
    encoded = ''.join(str(len(s.encode('utf-16-le')) // 2) + ':' + s for s in values).encode('utf-8')
    return str(uuid.UUID(bytes_le=sha256(encoded).digest()[:16]))


def enums(path):
    text = re.sub(r'//[^\n]*', '', path.read_text())
    body = text.split('{', 1)[1].split('}', 1)[0]; result = {}; next_value = 0
    for item in body.split(','):
        item = item.strip()
        if not item: continue
        match = re.fullmatch(r'(\w+)(?:\s*=\s*(\d+))?', item); assert match, item
        name, explicit = match.groups()
        result[name] = int(explicit) if explicit is not None else next_value; next_value = result[name] + 1
    return result


def json_hash(value):
    # This protocol uses ASCII identifiers and finite integer/.125/.375 event
    # fields. They have identical minimal JSON number spellings in .NET/Python.
    return sha256(json.dumps(value, ensure_ascii=True, separators=(',', ':'), allow_nan=False).encode()).hexdigest().upper()


def units(time):
    p, q = float(time).as_integer_ratio(); scale = 1 << 80
    assert p >= 0 and scale % q == 0, 'Time cannot be represented in declared binary audit units.'
    return p * (scale // q)


def audit(package):
    if not __debug__: raise RuntimeError('Audit assertions must remain enabled.')
    spec = read(package / 'receptor-membrane-input.json'); mode = crossing_mode(spec)
    predictions = None
    if mode == 'CubicHermite':
        config = read(package / 'configuration.json')
        assert config['IsolatedReceptorCubicComparison'] and config['CrossingLocalizationToleranceMs'] == 1e-9
        assert read(package / 'legacy-state-analysis.json')['passed'], 'Require exact original endpoint trajectories first.'
        predictions = read(package / 'reference/saved-endpoint-crossings.json')['rows']
        assert len(predictions) == 27
        legacy_hashes = {r['key']:r['trajectorySha256'] for r in read(package / 'reference/legacy-trajectories.json')}
        assert len(legacy_hashes) == 18
    study = package / 'membrane-study'; meta = read(study / 'protocol.json'); results = read(study / 'results.json')
    assert meta['inputSha256'] == digest(package / 'receptor-membrane-input.json')
    assert meta['specification'] == {key: spec[key] for key in meta['specification']}
    version = cell_version(spec)
    assert meta['candidateVersion'] == version and meta['contactVersion'] == 'isolated-motor-receptor-two-state-v1'
    assert results['completed'] and results['expectedConditions'] == len(results['cases']) == spec['candidateConditions'] == 18
    assert results['numericalAccuracyQualified'] is False
    assert meta['postsynapticNeuronExecutionRequested'] and results['postsynapticNeuronExecuted']
    for container in (meta, results):
        assert container['presynapticEventsAreFixtures'] and not container['presynapticNeuronsExecuted']
        assert not container['nativePhysicsExecuted'] and not container['physiologicalCalibrationComplete'] and not container['avatarRecoveryAssessed']
    expected = [(case['key'], step) for case in spec['cases'] for step in spec['stepsMs']]
    assert [(c['scenario'], c['maximumStepMs']) for c in results['cases']] == expected
    assert all(c['completed'] for c in results['cases'])
    structures = enums(package / 'source/Protocol/StructureId.cs'); transmitters = enums(package / 'source/Protocol/NTEnum.cs')
    reports = []; cases = {}; total_points = total_deliveries = 0
    for scenario in spec['cases']:
        conditions = []; ordered_contacts = None; fingerprints = None; original_ids = None
        for maximum in spec['stepsMs']:
            outcome = next(c for c in results['cases'] if c['scenario'] == scenario['key'] and c['maximumStepMs'] == maximum)
            assert outcome['key'] == scenario['key'] + '-h' + str(maximum)
            folder = study / outcome['key']; inputs = read(folder / 'inputs.json'); final = read(folder / 'final.json'); state = final['state']
            assert inputs['scenario'] == scenario and inputs['maximumStepMs'] == maximum
            session = stable(spec['version'], meta['inputSha256'], scenario['key']); assert inputs['session'] == state['sessionId'] == session
            assert len(inputs['admissions']) == len(scenario['contacts'])
            by_id = {}; event_sets = {}; transitions = {0., spec['durationMs']}; all_originals = {}
            for contact, admission in zip(scenario['contacts'], inputs['admissions']):
                assert admission['site'] == contact['site']; d = admission['definition']; source = 'fixture-' + contact['kind']
                synapse = stable(spec['version'], 'synapse', source); projection = stable(spec['version'], 'projection', source)
                transmitter = transmitters['GLUTAMATE' if contact['kind'] == 'AMPA' else 'GABA']
                contact_base = stable('contact-events-v1', source, structures['PremotorCortex'], source + '-cell',
                    'isolated-spinal', structures['SpinalCordMotor'], 0, projection, transmitter, False, False)
                contact_id = stable('motor-receptor-contact-v1', contact_base, synapse, spec['cell'], contact['site'])
                expected_definition = dict(sourceInstance=source, sourceStructure=structures['PremotorCortex'], sourceNeuron=source+'-cell',
                    sourceSynapse=synapse, projection=projection, targetInstance='isolated-spinal', targetNeuron=spec['cell'],
                    targetNeuronIndex=0, targetSite=contact['site'], electricalCompartment=contact['compartment'],
                    kind=0 if contact['kind']=='AMPA' else 1, maximumConductanceNs=contact['maximumConductanceNs'],
                    axonalDelayMs=contact['axonalDelayMs'], isFeedback=False)
                assert d == expected_definition and contact_id not in by_id
                by_id[contact_id] = contact; event_sets[contact_id] = []
                assert len(admission['originalEvents']) == len(contact['sourceTimesMs'])
                hashes = []
                for at, event in zip(contact['sourceTimesMs'], admission['originalEvents']):
                    event_id = stable(spec['version'], session, contact['kind'], str(at))
                    expected_event = dict(messageId=event_id, timestampMs=at, sourceStructure=structures['PremotorCortex'],
                        targetStructure=structures['SpinalCordMotor'], sourceNeuronId=source+'-cell', targetNeuronId=spec['cell'],
                        synapseId=synapse, neurotransmitter=transmitter, vesicleQuanta=1, reuptakeRate=0, spikeType=0,
                        isFeedback=False, modulationContext=None, isRightingCircuitSpike=False, sourceInstanceKey=source,
                        projectionId=projection, isSyntheticBackground=False)
                    assert event == expected_event
                    # Preserve declared property order for default C# message hashing.
                    original_hash = json_hash({key[0].upper()+key[1:]: value for key, value in expected_event.items()})
                    if event_id in all_originals: assert all_originals[event_id] == original_hash
                    all_originals[event_id] = original_hash; hashes.append(original_hash); event_sets[contact_id].append(event_id)
                    arrival = at + contact['axonalDelayMs']; transitions.update([arrival, arrival+spec['pulseDurationMs']])
                receipt = admission['receipt']
                assert receipt == dict(sessionId=session, contactId=contact_id, sequence=1, completeThroughSourceMs=spec['durationMs'],
                    events=len(hashes), sha256=json_hash(dict(SessionId=session, ContactId=contact_id, Sequence=1,
                        CompleteThroughSourceMs=spec['durationMs'], OriginalEventHashes=hashes)))
            order = final['contactOrder']; assert len(order) == len(set(order)) == len(by_id) and set(order) == set(by_id)
            contacts = [by_id[c] for c in order]
            if ordered_contacts is None: ordered_contacts = contacts
            else: assert contacts == ordered_contacts
            if original_ids is None: original_ids = event_sets
            else: assert event_sets == original_ids
            path = folder / 'trajectory.csv'; assert digest(path) == outcome['trajectorySha256']
            if mode == 'CubicHermite': assert outcome['trajectorySha256'] == legacy_hashes[outcome['key']]
            with path.open() as stream: header = stream.readline().strip().split(',')
            assert header == ['timeMs','dtMs'] + ['state'+str(i) for i in range(30)] + ['receptor_'+c for c in order]
            data = np.loadtxt(path, delimiter=',', skiprows=1, ndmin=2)
            assert data.shape == (outcome['rows'], 32+len(order)) and np.isfinite(data).all()
            time, dt, trajectory = data[:,0], data[:,1], data[:,2:]
            assert time[0] == dt[0] == 0 and time[-1] == spec['durationMs'] and np.all(np.diff(time) > 0)
            assert np.array_equal(np.diff(time),dt[1:]) and np.all((dt[1:]>0)&(dt[1:]<=maximum))
            supplied = 0
            for at, interval in zip(time,dt):
                supplied += units(interval); assert supplied == units(at), 'Lost or fabricated elapsed time.'
            assert str(supplied) == final['suppliedUnits'] and final['scalePower'] == 80
            observation = np.arange(15001)*spec['sampleMs']; indices = np.searchsorted(time,observation)
            assert np.array_equal(time[indices],observation)
            edges = np.array(sorted(transitions)); indices = np.searchsorted(time,edges); assert np.array_equal(time[indices],edges)
            assert np.all((trajectory[:,GATES]>=0)&(trajectory[:,GATES]<=1)) and np.all(trajectory[:,14]>=0)
            assert np.all((trajectory[:,30:]>=0)&(trajectory[:,30:]<=1)) and np.all(trajectory[0,:5]==spec['initialVoltageMv'])
            assert np.all(trajectory[0,30:]==0) and np.array_equal(trajectory[-1,:30],state['membraneState'])
            crossing_rows=np.flatnonzero((trajectory[:-1,0]<spec['thresholdMv'])&(trajectory[1:,0]>=spec['thresholdMv']))
            if mode == 'Linear':
                crossings=np.array([float(time[i])+float(dt[i+1])*(spec['thresholdMv']-float(trajectory[i,0]))/
                    (float(trajectory[i+1,0])-float(trajectory[i,0])) for i in crossing_rows])
                assert np.array_equal(crossings,state['crossings'])
            else:
                crossings=np.asarray(state['crossings'],dtype=float)
                expected=sorted((r for r in predictions if r['scenario']==scenario['key'] and r['maximumStepMs']==maximum),key=lambda r:r['ordinal'])
                assert len(expected)==len(crossings)==len(crossing_rows)
                assert [r['ordinal'] for r in expected]==list(range(len(expected)))
                assert np.isfinite(crossings).all() and np.all(np.diff(crossings)>0)
                for at,index,prior in zip(crossings,crossing_rows,expected):
                    assert time[index]<=at<=time[index+1], 'A published event must lie in its completed threshold bracket.'
                    assert abs(at-prior['estimates']['candidate']['timeMs'])<=1e-9, 'Native event differs from the reviewed endpoint cubic.'
            assert len(crossings)==outcome['crossings']
            delivered=sum(len(c['sourceTimesMs']) for c in contacts)
            assert not state['faulted'] and state['timeMs']==spec['durationMs'] and state['integrationVersion']==version
            assert state['integrationSteps']==len(time)-1 and final['rows']==len(time)
            assert state['admittedEvents']==state['deliveredEvents']==outcome['deliveredEvents']==delivered
            assert [c['contactId'] for c in state['contacts']]==order and [l['contactId'] for l in state['inputLanes']]==order
            for j,(c,actual,lane) in enumerate(zip(contacts,state['contacts'],state['inputLanes'])):
                assert actual['sessionId']==session and actual['acceptedEvents']==len(c['sourceTimesMs']) and not actual['faulted']
                assert lane==dict(contactId=order[j],batches=1,completeThroughSourceMs=spec['durationMs'],
                    admittedEvents=len(c['sourceTimesMs']),deliveredEvents=len(c['sourceTimesMs']))
                last=c['sourceTimesMs'][-1]+c['axonalDelayMs'] if c['sourceTimesMs'] else None
                assert actual['lastReleaseMs']==last and actual['pulseEndMs']==(last+1 if last is not None else None)
                assert actual['value']==dict(timeMs=spec['durationMs'],openFraction=trajectory[-1,30+j],
                    conductanceNs=c['maximumConductanceNs']*trajectory[-1,30+j],reversalMv=0 if c['kind']=='AMPA' else -80,
                    transmitterMm=0,electricalCompartment=c['compartment'])
            current_fingerprints=[c['fingerprint'] for c in state['contacts']]
            if fingerprints is None: fingerprints=current_fingerprints
            else: assert current_fingerprints==fingerprints
            conditions.append(dict(maximumStepMs=maximum,time=time,state=trajectory,crossings=crossings))
            reports.append(dict(key=outcome['key'],acceptedEndpoints=len(time),deliveredContactEvents=delivered,
                crossings=len(crossings),originalEventsPreserved=True,exactElapsedTime=True))
            total_points+=len(time);total_deliveries+=delivered
        cases[scenario['key']]=dict(contacts=ordered_contacts,conditions=conditions)
    return dict(cases=cases,summary=dict(completed=True,conditions=18,acceptedEndpoints=total_points,
        deliveredContactEvents=total_deliveries,cases=reports,modelsExecuted=False,
        numericalAccuracyQualified=False,physiologyQualified=False,avatarRecoveryAssessed=False))


if __name__=='__main__':
    result=audit(Path(sys.argv[1]).resolve())
    print(json.dumps(result['summary'],indent=2),flush=True)

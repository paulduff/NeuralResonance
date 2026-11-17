"""Freeze and audit the isolated actual-DNNE-source experiment.

Preparation reads reported evidence and source metadata only. All tests, models
and this experiment's audit execute in its independent visible PowerShell window.
Later review requires the operator's completion report; never poll this capture.
"""
import argparse
from collections import Counter
import csv
from datetime import datetime, timezone
import gzip
from hashlib import sha256
import importlib.util
import json
import math
from pathlib import Path, PurePosixPath
import shutil
import subprocess
import uuid
from zipfile import ZipFile

ROOT = Path(__file__).resolve().parents[1]


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec); spec.loader.exec_module(result)
    return result


prior = module('rubral_prior_gate', ROOT / 'tools/native_crossing_gate.py')
gate = prior.gate
h = gate.helper()
COUNTS = dict(prior.COUNTS, IsolatedRubralSourceTests=12)
CASES = {'no-evoked-drive': 0, 'drive-quarter': .25, 'drive-1': 1, 'drive-4': 4}
BRANCHES = {'packets1': 1, 'packets17': 17, 'contacts-disabled': 17}
FALSE_CLAIMS = ['upstreamNumericalAccuracyQualified', 'upstreamPhysiologyQualified',
                'learnedContactEfficacyTransferred', 'avatarRecoveryAssessed']


def save(path, value):
    h.io(path).write_text(json.dumps(value, indent=2, allow_nan=False) + '\n', encoding='utf-8')


def text_hash(value):
    return sha256(value.encode('utf-8')).hexdigest().upper()


def prepare():
    assert not h.git('status', '--porcelain'), 'Commit clean source first.'
    revision = h.git('rev-parse', 'HEAD')
    assert revision == h.git('rev-parse', 'origin/main'), 'Push frozen source first.'
    folder = ROOT / 'artifacts/receptor-cubic-review-20261117'
    h.verify(folder, ['review.json']); review = h.read(folder / 'review.json')
    assert review['operatorReportedCompletion'] and review['passedTests'] == 124
    assert review['savedNumericalContrastsReproduced'] and not review['originalBoundsChanged']
    assert review['originalAcceptedStateTrajectoriesPreserved'] == 18
    failed_folder = ROOT / 'artifacts/rubral-output-failure-review-20261117'
    h.verify(failed_folder, ['review.json']); failed_review = h.read(failed_folder / 'review.json')
    assert failed_review['operatorReportedCompletion'] and failed_review['originalStatus'] == 'FAILED'
    assert failed_review['passedTests'] == 136 and failed_review['activeConditionsExecuted'] == 0
    assert failed_review['savedQuietTrajectoriesExactlyEqual'] and not failed_review['fullExperimentComplete']
    assert h.digest(Path(failed_review['capture']) / 'backup-manifest.json') == failed_review['manifestSha256']
    baseline = Path(review['capture'])
    assert h.digest(baseline / 'backup-manifest.json') == review['manifestSha256']
    # Preserve every existing shared-runtime implementation, including the
    # qualified motor core and the actual upstream DNNE model. Only the added
    # ownership/observation file differs. This is source preservation, not a
    # claim that all those existing models are physiologically qualified.
    preserved = [p.relative_to(h.io(baseline / 'source')).as_posix()
                 for p in sorted(h.io(baseline / 'source/Structures/_SharedRuntime').rglob('*.cs'))]
    preserved += ['Structures/RedNucleus/Program.cs', 'Protocol/MotorAxonProtocol.cs',
        'Protocol/MotorAxonPublisher.cs', 'Protocol/MotorAxonReplayBuffer.cs',
        'src/NRE.SimAvatar/AvatarMotorAxonReceiver.cs']
    h.verify(baseline, ['source/' + n for n in preserved] + ['managed-runtime-before.json'])
    for name in preserved:
        assert h.io(ROOT / name).read_text(encoding='utf-8-sig') == h.io(baseline / 'source' / name).read_text(encoding='utf-8-sig'), name
    methods = gate.inventory(ROOT, COUNTS)
    assert sum(COUNTS.values()) == 136
    package = ROOT / 'artifacts' / ('rubral-motor-' + datetime.now(timezone.utc).strftime('%Y%m%d') + '-' + uuid.uuid4().hex[:6])
    backup = Path('D:/DNNE-desktop-runs/UnityWorld') / package.name
    assert not package.exists() and not backup.exists(); package.mkdir()
    subprocess.run(['git', 'archive', '--format=zip', '--output=' + str(package / 'source.zip'), revision], cwd=ROOT, check=True)
    with ZipFile(package / 'source.zip') as archive:
        for entry in archive.infolist():
            name = PurePosixPath(entry.filename)
            assert not name.is_absolute() and '..' not in name.parts and ':' not in entry.filename and '\\' not in entry.filename
            assert ((entry.external_attr >> 16) & 0o170000) != 0o120000
            target = h.io(package / 'source' / name)
            if entry.is_dir(): target.mkdir(parents=True, exist_ok=True)
            else:
                target.parent.mkdir(parents=True, exist_ok=True)
                with target.open('xb') as stream: stream.write(archive.read(entry))
    reference = package / 'reference'; reference.mkdir()
    shutil.copyfile(h.io(baseline / 'managed-runtime-before.json'), reference / 'qualified-managed-runtime.json')
    save(package / 'provenance.json', dict(review=review, preservedFailedCaptureReview=failed_review, preservedSourceFiles=preserved,
        testsExecutedDuringPreparation=False, modelsExecutedDuringPreparation=False,
        liveSelectionChanged=False, originalCubicCapturePreserved=True))
    shutil.copyfile(package / 'source/tools/backup-unity-run.ps1', package / 'backup-run.ps1')
    files = [dict(Path=p.relative_to(h.io(package)).as_posix(), Sha256=h.digest(p))
             for p in sorted(h.io(package).rglob('*')) if p.is_file()]
    dotnet = shutil.which('dotnet')
    python = Path('C:/Users/duff_/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe')
    assert dotnet and python.is_file()
    save(package / 'configuration.json', dict(Revision=revision, PreparedUtc=datetime.now(timezone.utc).isoformat(),
        PackageFiles=files, DotnetExecutable=dotnet, DotnetExecutableSha256=h.digest(dotnet),
        PythonExecutable=str(python), PythonExecutableSha256=h.digest(python), BackupDestination=str(backup),
        NoLiveMonitoring=True, IsolatedRubralMotor=True, ExpectedConditions=4, ExpectedBranches=12, ExpectedTestCases=136,
        ExpectedClassCounts=COUNTS, ExpectedTestMethods=methods,
        TestFilter='|'.join('FullyQualifiedName~' + c for c in COUNTS), NativePhysicsRequested=False,
        UpstreamNumericalAccuracyQualified=False, PhysiologyQualified=False, AvatarRecoveryAssessed=False))
    pointer = dict(Package=str(package), Backup=str(backup), Revision=revision, PreparedOnly=True, NoLiveMonitoring=True,
        Launcher=str(package / 'source/tools/start-rubral-motor-window.ps1'))
    save(package.parent / (package.name + '-prepared.json'), pointer)
    print(json.dumps(pointer, indent=2), flush=True)


def records(path):
    with gzip.open(h.io(path), 'rt', encoding='utf-8') as stream:
        for line in stream:
            yield json.loads(line)


def audit(package):
    tests = gate.check_tests(package, COUNTS)
    study = package / 'study'; results = h.read(study / 'results.json')
    protocol = h.read(study / 'protocol.json')
    assert protocol['version'] == 'isolated-rubral-receptor-motor-study-v1'
    assert protocol['sourceSampleMs'] == .125 and protocol['sourceCells'] == 256 and protocol['durationMs'] == 1500
    assert protocol['sourceModel'] == 'Izhikevich' and protocol['sourcePlasticity'] == 'STDP'
    assert results['completed'] and results['componentCausalityChecksPassed'] and results['presynapticNeuronsExecuted']
    assert results['presynapticInputIsFixture'] and len(results['cases']) == 4
    for key in FALSE_CLAIMS + ['liveBrainAccessed', 'nativePhysicsExecuted', 'muscleDynamicsExecuted']:
        assert results[key] is False, key
    outcomes = []; active_fixture = None; previous_routes = None
    for key, quanta in CASES.items():
        print('Auditing saved original source and receiver records: ' + key, flush=True)
        folder = study / key; result = h.read(folder / 'result.json')
        assert result == results['cases'][len(outcomes)] and result['key'] == key and result['completed']
        for name in FALSE_CLAIMS + ['movingBodyCouplingQualified']: assert result[name] is False
        topology = h.read(folder / 'topology.json'); fixture = h.read(folder / 'input.json')
        assert fixture['quanta'] == quanta and len(fixture['originalEvents']) == (5120 if quanta else 0)
        assert len({e['MessageId'] for e in fixture['originalEvents']}) == len(fixture['originalEvents'])
        for n, event in enumerate(fixture['originalEvents']):
            assert event['TimestampMs'] == 1000.125 + 10 * (n // 256) and event['VesicleQuanta'] == quanta
            assert event['TargetNeuronId'] == f'n-{n % 256:03d}' and not event['IsSyntheticBackground']
        normalized_fixture = [{k:v for k,v in event.items() if k != 'VesicleQuanta'} for event in fixture['originalEvents']]
        if quanta:
            if active_fixture is None: active_fixture = normalized_fixture
            else: assert active_fixture == normalized_fixture
        routes = {r['SourceNeuron']: r for r in topology['allRoutes']}
        assert len(routes) == len(topology['allRoutes']) == 256
        if previous_routes is None: previous_routes = routes
        else: assert routes == previous_routes
        target = min(r['TargetNeuron'] for r in routes.values())
        selected = [r for r in routes.values() if r['TargetNeuron'] == target]
        assert selected == topology['selected'] and topology['identity']['Neuron'] == target
        contacts = dict(zip(topology['contactIds'],topology['contacts'],strict=True))
        assert len(contacts) == len(selected) > 0
        assert {c['SourceNeuron'] for c in contacts.values()} == {r['SourceNeuron'] for r in selected}
        for contact in contacts.values():
            route = routes[contact['SourceNeuron']]
            assert contact['SourceInstance'] == route['SourceInstance'] and contact['SourceSynapse'] == route['Synapse']
            assert contact['Projection'] == route['Projection'] and contact['TargetNeuron'] == target
            assert contact['MaximumConductanceNs'] == 50 and contact['AxonalDelayMs'] == .125
            assert contact['ElectricalCompartment'] == 1 + route['SourceIndex'] % 4
        originals = {}; selected_ids = []; frames = {}; admitted = 0
        for n, frame in enumerate(records(folder / 'source-frames.jsonl.gz'), 1):
            assert frame['Sequence'] == n and frame['Session'] == fixture['session'] and frame['CompleteThroughMs'] == n * .125
            assert text_hash(frame['CanonicalJson']) == frame['Sha256']
            body = json.loads(frame['CanonicalJson']); observation = body['Observation']
            assert body['Sequence'] == n and body['Session'] == frame['Session'] and body['CompleteThroughMs'] == frame['CompleteThroughMs']
            assert body['TimingConvention'] == 'existing-DNNE-completed-integration-substep'
            assert observation['CompletedTick'] == n and observation['TickTimestampMs'] == n * .125
            assert {c['Neuron'] for c in observation['Cells']} == set(routes) and len(observation['Cells']) == 256
            assert all(math.isfinite(c[field]) for c in observation['Cells'] for field in ['VoltageMv', 'NetCurrent', 'FiringRateHz', 'ActivityTrace'])
            while admitted < len(fixture['originalEvents']) and fixture['originalEvents'][admitted]['TimestampMs'] <= n * .125: admitted += 1
            assert body['AdmittedFixtureEvents'] == admitted and body['FiredCount'] == len(body['OriginalEvents'])
            frame_ids = []
            for event in body['OriginalEvents']:
                route = routes[event['SourceNeuronId']]
                assert event['SourceInstanceKey'] == route['SourceInstance'] == fixture['instance']
                for event_key, route_key in [('TargetStructure', 'TargetStructure'), ('TargetNeuronId', 'TargetNeuron'),
                    ('ProjectionId', 'Projection'), ('SynapseId', 'Synapse'), ('Neurotransmitter', 'Transmitter')]:
                    assert event[event_key] == route[route_key]
                assert event['SourceStructure'] == protocol['sourceStructure'] and not event['IsFeedback'] and not event['IsSyntheticBackground']
                assert (n-1)*.125 < event['TimestampMs'] <= n*.125 and event['MessageId'] not in originals
                originals[event['MessageId']] = event; frame_ids.append(event['MessageId'])
                if event['TargetNeuronId'] == target: selected_ids.append(event['MessageId'])
            frames[n] = frame_ids
        assert len(frames) == result['sourceFrames'] == 12000 and len(originals) == result['sourceEvents']
        assert len(selected_ids) == result['selectedEvents'] and admitted == len(fixture['originalEvents'])
        status = result['source']
        assert status['CompletedSamples'] == status['EngineCompletedTick'] == 12000
        assert status['ActualCompletedThroughMs'] == status['EngineTickTimestampMs'] == 1500
        assert status['ProducedEvents'] == len(originals) and status['AdmittedFixtureEvents'] == admitted
        assert not status['Faulted'] and not status['HasPendingFrame']
        assert result['sourceBackpressureChecks'] == 12000//113 + 1
        finals = {}; branch_audits = []
        for branch, cadence in BRANCHES.items():
            root = folder / branch; disabled = branch == 'contacts-disabled'
            initial = h.read(root / 'initial.json'); final = h.read(root / 'final.json'); finals[branch] = final
            assert initial['cadence'] == cadence and initial['disabled'] == disabled
            state = final['source']; cell = state['Cell']
            assert state['SessionId'] == fixture['session'] and state['CompletedSamples'] == 15000 and cell['TimeMs'] == 1500
            assert state['SampleMs'] == .1 and state['MaximumStepMs'] == .003125 and not state['Faulted'] and not cell['Faulted']
            seen = Counter(); seen_events = []; contact_sequences = Counter()
            for item in records(root / 'receptor-admissions.jsonl.gz'):
                receipt = item['receipt']; contact = contacts[receipt['ContactId']]
                seq = receipt['Sequence']; contact_sequences[receipt['ContactId']] += 1
                assert seq == contact_sequences[receipt['ContactId']] and receipt['SessionId'] == fixture['session']
                sequences = list(range((seq-1)*cadence+1, min(seq*cadence,12000)+1))
                assert item['sourceSequences'] == sequences and receipt['CompleteThroughSourceMs'] == sequences[-1]*.125
                expected = [eid for s in sequences for eid in frames[s] if originals[eid]['SourceNeuronId'] == contact['SourceNeuron']]
                assert item['selectedOriginalIds'] == expected and item['contactDisabled'] == disabled
                assert [json.loads(s) for s in item['selectedOriginalJson']] == [originals[eid] for eid in expected]
                delivered = [] if disabled else expected
                assert item['deliveredOriginalIds'] == delivered and receipt['Events'] == len(delivered)
                canonical = json.loads(item['canonicalInputJson'])
                assert text_hash(item['canonicalInputJson']) == receipt['Sha256']
                assert canonical == dict(SessionId=fixture['session'], ContactId=receipt['ContactId'], Sequence=seq,
                    CompleteThroughSourceMs=receipt['CompleteThroughSourceMs'],
                    OriginalEventHashes=[] if disabled else [text_hash(s) for s in item['selectedOriginalJson']])
                assert item['replayed'] == (seq%37 == 0 or sequences[-1] == 12000)
                seen.update(sequences); seen_events.extend(delivered)
            batches = math.ceil(12000/cadence)
            assert contact_sequences == Counter({c: batches for c in contacts}) and final['inputBatches'] == batches
            assert seen == Counter({n: len(contacts) for n in frames})
            assert Counter(seen_events) == Counter([] if disabled else selected_ids)
            assert cell['AdmittedEvents'] == len(seen_events)
            eligible_inputs = sum(originals[eid]['TimestampMs']+.125 <= 1500 for eid in seen_events)
            assert cell['DeliveredEvents'] == eligible_inputs
            for lane in cell['InputLanes']:
                expected = [eid for eid in seen_events if originals[eid]['SourceNeuronId'] == contacts[lane['ContactId']]['SourceNeuron']]
                assert lane['AdmittedEvents'] == len(expected) and lane['Batches'] == batches and lane['CompleteThroughSourceMs'] == 1500
            emissions = state['Emissions']; assert [e['FiredAtNeuralMs'] for e in emissions] == cell['Crossings']
            assert len(emissions) == len({e['MessageId'] for e in emissions})
            assert [e['Ordinal'] for e in emissions] == list(range(1,len(emissions)+1))
            produced = []; arrived = []; prior_time = 0; retries = blocked = 0; count = 0
            for count, item in enumerate(records(root / 'motor-transport.jsonl.gz'), 1):
                batch = item['batch']; receipt = item['receipt']
                assert json.loads(item['canonicalBatchJson']) == batch and text_hash(item['canonicalBatchJson']) == receipt['BatchSha256']
                assert batch['Sequence'] == receipt['Sequence'] == count and batch['SessionId'] == fixture['session']
                assert batch['AnatomyFingerprint'] == receipt['AnatomyFingerprint'] == initial['anatomyFingerprint']
                assert batch['SourceInstanceKey'] == receipt['SourceInstanceKey'] == topology['identity']['Instance']
                assert batch['CompleteThroughNeuralMs'] == min(count*cadence,15000)*.1
                for event in batch['Spikes']:
                    original = emissions[len(produced)]
                    for name in ['MessageId','Ordinal','SourceNeuronId','ProjectionId','SourceSynapseId','FiredAtNeuralMs']:
                        assert event[name] == original[name]
                    assert prior_time < event['FiredAtNeuralMs'] <= batch['CompleteThroughNeuralMs']
                    produced.append(event['MessageId'])
                prior_time = batch['CompleteThroughNeuralMs']
                assert item['replayed'] == (count%37 == 0 or prior_time == 1500)
                assert item['blocked'] == (count%113 == 0 or prior_time == 1500)
                retries += item['replayed']; blocked += item['blocked']; arrived.extend(item['arrivals'])
                assert all(a['ArrivalAtBodyMs'] <= prior_time for a in item['arrivals'])
            assert count == final['outputBatches'] == math.ceil(15000/cadence) and prior_time == 1500
            assert retries == final['retries'] and blocked == final['backpressureChecks']
            assert produced == [e['MessageId'] for e in emissions]
            arrived += final['tail']; expected_arrivals = [e for e in emissions if e['FiredAtNeuralMs']+2.0625 <= 1500]
            assert len(arrived) == final['received'] == final['receiverDelivered'] == len(expected_arrivals)
            anatomy = initial['anatomy'][0]
            for arrival, emission in zip(arrived, expected_arrivals, strict=True):
                for name in ['MessageId','Ordinal','SourceInstanceKey','SourceNeuronId','ProjectionId','SourceSynapseId','FiredAtNeuralMs']:
                    assert arrival[name] == emission[name]
                assert arrival['SessionId'] == fixture['session'] and arrival['ContactId'] == anatomy['ContactId']
                assert arrival['MotorUnitId'] == anatomy['MotorUnitId'] and arrival['ArrivalAtBodyMs'] == emission['FiredAtNeuralMs']+2.0625
            pending = [dict(emission=e,arrivalAtBodyMs=e['FiredAtNeuralMs']+2.0625) for e in emissions if e['FiredAtNeuralMs']+2.0625>1500]
            assert pending == final['pendingBeyondHorizon'] and len(pending) == final['receiverPending']
            assert final['receiverCompleteThroughMs'] == 1500 and len(pending)+len(arrived) == len(emissions)
            with h.io(root / 'trajectory.csv').open(newline='',encoding='utf-8-sig') as stream:
                reader = csv.reader(stream); header = next(reader); rows = 0
                assert len(header) == 34+len(contacts)
                for rows, row in enumerate(reader, 1):
                    values = [float(x) for x in row]
                    assert len(values) == len(header) and all(math.isfinite(x) for x in values)
                    assert values[0] == (rows-1)*.1
                assert rows == 15001
                assert values[1:31] == cell['MembraneState']
                assert values[-3:] == [cell['IntegrationSteps'],cell['DeliveredEvents'],len(cell['Crossings'])]
            if disabled: assert not emissions and not seen_events
            branch_audits.append(dict(branch=branch,admittedEvents=len(seen_events),deliveredInputs=eligible_inputs,
                motorEvents=len(emissions),received=len(arrived),retainedBeyondHorizon=len(pending),samples=15001))
        fast = finals['packets1']['source']; slow = finals['packets17']['source']
        assert fast['Emissions'] == slow['Emissions'] and fast['Cell']['MembraneState'] == slow['Cell']['MembraneState']
        assert h.digest(folder / 'packets1/trajectory.csv') == h.digest(folder / 'packets17/trajectory.csv') == result['trajectoryHash']
        assert result['packetCadenceExact'] and result['receiverDeliveryExact']
        response = bool(selected_ids and fast['Emissions'])
        assert result['observedNeuronalMotorResponse'] == response and result['motorEvents'] == len(fast['Emissions'])
        outcomes.append(dict(key=key,sourceEvents=len(originals),selectedEvents=len(selected_ids),
            observedNeuronalMotorResponse=response,branches=branch_audits))
    assert results['observedNeuronalMotorResponse'] == any(c['observedNeuronalMotorResponse'] for c in outcomes)
    return dict(completed=True,tests=tests,cases=outcomes,originalFrames=48000,branches=12,
        componentCausalityChecksPassed=True,observedNeuronalMotorResponse=results['observedNeuronalMotorResponse'],
        upstreamNumericalAccuracyQualified=False,upstreamPhysiologyQualified=False,learnedContactEfficacyTransferred=False,
        liveBrainAccessed=False,nativePhysicsExecuted=False,muscleDynamicsExecuted=False,avatarRecoveryAssessed=False)


if __name__ == '__main__':
    if not __debug__: raise RuntimeError('Assertions must remain enabled.')
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command',choices=['prepare','check-tests','audit'])
    parser.add_argument('package',type=Path,nargs='?'); args = parser.parse_args()
    if args.command == 'prepare': prepare()
    else:
        assert args.package is not None; package = args.package.resolve()
        if args.command == 'check-tests': print(json.dumps(gate.check_tests(package,COUNTS),indent=2),flush=True)
        else:
            result = audit(package); assert not (package / 'analysis.json').exists()
            save(package / 'analysis.json',result); print(json.dumps(result,indent=2),flush=True)

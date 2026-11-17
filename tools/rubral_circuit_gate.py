"""Freeze/reconcile the body-epoch circuit owner; execute models only in its visible window."""
import argparse
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path, PurePosixPath
import shutil
import subprocess
import sys
import uuid
from zipfile import ZipFile

ROOT=Path(__file__).resolve().parents[1]


def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path); result=importlib.util.module_from_spec(spec);spec.loader.exec_module(result);return result


old=module('circuit_prior_gate',ROOT/'tools/rubral_motor_gate.py');h=old.h;gate=old.gate
COUNTS=dict(old.TEN_COUNTS,RubralMotorCircuitTests=6)
CASES=[(key,quanta,cut,cadence) for key,quanta in old.CASES.items()
       for cut in ([False,True] if key=='drive-4' else [False]) for cadence in (1,17)]


def save(path,value):
    with h.io(path).open('x',encoding='utf-8') as f:json.dump(value,f,indent=2,allow_nan=False);f.write('\n')


def stable(*parts):
    return str(uuid.UUID(bytes_le=__import__('hashlib').sha256(''.join(str(len(str(p)))+':'+str(p) for p in parts).encode()).digest()[:16]))


def prepare():
    assert not h.git('status','--porcelain'),'Commit first.'
    revision=h.git('rev-parse','HEAD');assert revision==h.git('rev-parse','origin/main'),'Push first.'
    folder=ROOT/'artifacts/rubral-ten-review-20261117-v2';h.verify(folder,['review.json']);review=h.read(folder/'review.json')
    assert review['operatorReportedCompletion'] and review['savedAnalysisExactlyReproduced'] and review['passedTests']==155
    assert review['analysis']['tenDistinctOriginalSources'] and review['analysis']['observedNeuronalMotorResponse']
    baseline=Path(review['capture']);assert h.digest(baseline/'backup-manifest.json')==review['manifestSha256']
    preserved=[p.relative_to(h.io(baseline/'source')).as_posix() for p in h.io(baseline/'source/Structures/_SharedRuntime').rglob('*.cs')]
    preserved+=['Protocol/'+n+'.cs' for n in ('MotorAxonProtocol','MotorAxonPublisher','MotorAxonReplayBuffer')]
    preserved+=['src/NRE.SimAvatar/AvatarMotorAxonReceiver.cs']
    selected=['study/'+key+'/'+name for key in old.CASES for name in
              ('input.json','topology.json','source-frames.jsonl.gz','packets1/trajectory.csv','packets1/final.json')]
    selected+=['study/drive-4/contacts-disabled/'+name for name in ('trajectory.csv','final.json')]
    h.verify(baseline,selected+['managed-runtime-before.json']+['source/'+name for name in preserved])
    for name in preserved:assert h.io(ROOT/name).read_text(encoding='utf-8-sig')==h.io(baseline/'source'/name).read_text(encoding='utf-8-sig'),name
    methods=gate.inventory(ROOT,COUNTS);assert sum(methods.values())==161
    package=ROOT/'artifacts'/('rubral-circuit-'+datetime.now(timezone.utc).strftime('%Y%m%d')+'-'+uuid.uuid4().hex[:6])
    backup=Path('D:/DNNE-desktop-runs/UnityWorld')/package.name;assert not package.exists() and not backup.exists();package.mkdir()
    subprocess.run(['git','archive','--format=zip','--output='+str(package/'source.zip'),revision],cwd=ROOT,check=True)
    with ZipFile(package/'source.zip') as archive:
        for entry in archive.infolist():
            name=PurePosixPath(entry.filename);assert not name.is_absolute() and '..' not in name.parts and ':' not in entry.filename and '\\' not in entry.filename
            assert ((entry.external_attr>>16)&0o170000)!=0o120000
            target=h.io(package/'source'/name)
            if entry.is_dir():target.mkdir(parents=True,exist_ok=True)
            else:
                target.parent.mkdir(parents=True,exist_ok=True)
                with target.open('xb') as f:f.write(archive.read(entry))
    for name in selected:
        target=h.io(package/'reference'/name);target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(h.io(baseline/name),target)
    shutil.copyfile(h.io(baseline/'managed-runtime-before.json'),package/'reference/qualified-managed-runtime.json')
    save(package/'provenance.json',dict(review=review,preservedSources=preserved,referenceFiles=selected,
        testsExecutedDuringPreparation=False,modelsExecutedDuringPreparation=False))
    shutil.copyfile(package/'source/tools/backup-unity-run.ps1',package/'backup-run.ps1')
    files=[dict(Path=p.relative_to(h.io(package)).as_posix(),Sha256=h.digest(p)) for p in sorted(h.io(package).rglob('*')) if p.is_file()]
    dotnet=shutil.which('dotnet');assert dotnet
    save(package/'configuration.json',dict(Revision=revision,PackageFiles=files,BackupDestination=str(backup),
        DotnetExecutable=dotnet,DotnetExecutableSha256=h.digest(dotnet),PythonExecutable=sys.executable,PythonExecutableSha256=h.digest(sys.executable),
        NoLiveMonitoring=True,IsolatedRubralBodyEpoch=True,ExpectedTestCases=161,ExpectedClassCounts=COUNTS,ExpectedTestMethods=methods,
        TestFilter='|'.join('FullyQualifiedName~'+name for name in COUNTS),ExpectedConditions=10,ExpectedSourceFrames=120000,
        NativePhysicsRequested=False,PhysiologyQualified=False,AvatarRecoveryAssessed=False))
    pointer=dict(Package=str(package),Backup=str(backup),Revision=revision,Launcher=str(package/'source/tools/start-rubral-circuit-window.ps1'))
    save(package.parent/(package.name+'-prepared.json'),pointer);print(json.dumps(pointer,indent=2),flush=True)


def audit(package):
    tests=gate.check_tests(package,COUNTS);protocol=h.read(package/'study/protocol.json');result=h.read(package/'study/results.json')
    assert protocol['version']=='rubral-body-epoch-source-gate-v1' and protocol['bodyNeuralEpochMs']==1000 and protocol['bodyEpochMs']==0
    assert protocol['publicationEverySourceFrames']==[1,17] and protocol['sourceFrames']==12000 and protocol['motorSamples']==15000
    assert protocol['warmFrames']==8000 and not protocol['oldRecordsDriveModel'] and not protocol['nativePhysicsExecuted']
    assert protocol['circuitVersion']=='owned-rubral-motor-circuit-v1' and protocol['fixtureInputRetained'] and protocol['neuronalModelExecuted']
    assert not protocol['liveBrainAccessed'] and not protocol['avatarRecoveryAssessed']
    assert result['completed'] and result['bodyEpochSourceGatePassed'] and len(result['cases'])==10
    assert [c['name'] for c in result['cases']]==[key+('-cut' if cut else '')+'-packets'+str(cadence) for key,_,cut,cadence in CASES]
    assert result['sourceFrames']==120000 and result['motorTrajectoryRows']==150010
    outcomes=[]
    for key,quanta,cut,cadence in CASES:
        name=key+('-cut' if cut else '')+'-packets'+str(cadence);print('Reconciling owned circuit: '+name,flush=True)
        root=package/'study'/name;baseline=package/'reference/study'/key
        initial=h.read(root/'initial.json');final=h.read(root/'final.json');primed=h.read(root/'primed.json')
        fixture=h.read(root/'input.json');original=h.read(baseline/'input.json');session=fixture['session']
        assert fixture['quanta']==quanta and fixture['instance']==original['instance'] and fixture['originalEvents']==original['originalEvents']
        assert initial['name']==name and initial['key']==key and initial['cadence']==cadence and initial['disconnected']==cut and initial['session']==session
        topology=h.read(root/'topology.json');prior=h.read(baseline/'topology.json');identity=topology['identity']
        for field in ('allRoutes','selected','contacts','contactIds'):assert topology[field]==prior[field],field
        assert [r['SourceIndex'] for r in topology['selected']]==list(range(81,91))
        assert identity['Instance']=='isolated-rubral-motor' and identity['Neuron']=='auto-SpinalCordMotor-002'
        assert identity['Projection']==stable('isolated-rubral-receptor-motor-study-v1',session,'motor-projection')
        assert identity['Synapse']==stable('isolated-rubral-receptor-motor-study-v1',session,'motor-synapse')
        assert initial['clock']==primed['clock']==dict(SessionId=session,NeuralEpochMs=1000,BodyEpochMs=0)
        contacts=dict(zip(topology['contactIds'],topology['contacts'],strict=True));selected_count=delivered_count=0;input_ids=set()
        routes={r['SourceNeuron']:r for r in topology['allRoutes']};source_ids=set();receiving_contacts={}
        assert len(routes)==256 and len(contacts)==10
        fixture_by_id={e['MessageId']:e for e in fixture['originalEvents']}
        frames=old.records(root/'source-frames.jsonl.gz');previous=old.records(baseline/'source-frames.jsonl.gz');admissions=old.records(root/'admissions.jsonl.gz')
        for n,(frame,base,entry) in enumerate(zip(frames,previous,admissions,strict=True),1):
            assert frame['Sequence']==n and frame['Session']==session and frame['CompleteThroughMs']==n*.125
            assert old.text_hash(frame['CanonicalJson'])==frame['Sha256']
            payload=json.loads(frame['CanonicalJson']);original_payload=json.loads(base['CanonicalJson'])
            assert payload['Observation']==original_payload['Observation'] and payload['Energy']==original_payload['Energy']
            assert [(e['SourceNeuronId'],e['TimestampMs']) for e in payload['OriginalEvents']]==[(e['SourceNeuronId'],e['TimestampMs']) for e in original_payload['OriginalEvents']]
            assert payload['Session']==session and payload['Sequence']==n and payload['CompleteThroughMs']==n*.125
            assert payload['TimingConvention']==original_payload['TimingConvention'] and payload['FiredCount']==len(payload['OriginalEvents'])
            for event in payload['OriginalEvents']:
                route=routes[event['SourceNeuronId']]
                assert event['SourceInstanceKey']==route['SourceInstance']==fixture['instance']
                for field,target in [('TargetStructure','TargetStructure'),('TargetNeuronId','TargetNeuron'),('ProjectionId','Projection'),('SynapseId','Synapse'),('Neurotransmitter','Transmitter')]:
                    assert event[field]==route[target]
                assert not event['IsFeedback'] and not event['IsSyntheticBackground'] and (n-1)*.125<event['TimestampMs']<=n*.125
                assert event['MessageId'] not in source_ids;source_ids.add(event['MessageId'])
            assert entry['sourceSequence']==n and entry['SourceAcknowledgement']==dict(Session=session,Sequence=n,Sha256=frame['Sha256'])
            for delivery in payload['InputDeliveries']:
                message=fixture_by_id[delivery['OriginalMessageId']];assert message['MessageId'] not in input_ids;input_ids.add(message['MessageId'])
                assert json.loads(delivery['OriginalJson'])==message and delivery['NamedTarget']==delivery['ActualTarget']==message['TargetNeuronId']
                assert delivery['ActualTargetIndex']==int(message['TargetNeuronId'][2:])
                assert message['TimestampMs']<=delivery['ArrivalAtMs']==delivery['AppliedAtMs']<=n*.125 and delivery['AppliedAtMs']>(n-1)*.125
                target=delivery['ActualTargetIndex'];contact_id=delivery['ReceivingContactId']
                assert contact_id!='00000000-0000-0000-0000-000000000000'
                if target in receiving_contacts:assert receiving_contacts[target]==contact_id
                receiving_contacts[target]=contact_id
            assert len(entry['Admissions'])==10 and {a['Receipt']['ContactId'] for a in entry['Admissions']}==set(contacts)
            for a in entry['Admissions']:
                receipt=a['Receipt'];contact=contacts[receipt['ContactId']]
                chosen=[e for e in payload['OriginalEvents'] if e['SourceNeuronId']==contact['SourceNeuron'] and e['TargetNeuronId']==contact['TargetNeuron']]
                assert [json.loads(s) for s in a['SelectedOriginalJson']]==chosen
                delivered=[] if cut else chosen;assert [json.loads(s) for s in a['DeliveredOriginalJson']]==delivered
                assert receipt['SessionId']==session and receipt['Sequence']==n and receipt['CompleteThroughSourceMs']==n*.125 and receipt['Events']==len(delivered)
                canonical=json.loads(a['CanonicalInputJson']);assert old.text_hash(a['CanonicalInputJson'])==receipt['Sha256']
                assert canonical==dict(SessionId=session,ContactId=receipt['ContactId'],Sequence=n,CompleteThroughSourceMs=n*.125,
                    OriginalEventHashes=[old.text_hash(s) for s in a['DeliveredOriginalJson']])
                selected_count+=len(chosen);delivered_count+=len(delivered)
        assert n==12000 and input_ids==set(fixture_by_id)
        assert len(receiving_contacts)==len(set(receiving_contacts.values()))==(256 if quanta else 0)
        branch='contacts-disabled' if cut else 'packets1';baseline_final=h.read(baseline/branch/'final.json')
        assert h.digest(root/'trajectory.csv')==h.digest(baseline/branch/'trajectory.csv')
        motor=final['motor'];cell=motor['Cell'];primed_motor=primed['motor'];emissions=motor['Emissions']
        assert motor['CompletedSamples']==15000 and cell['TimeMs']==1500 and not motor['Faulted'] and not cell['Faulted']
        assert cell['AdmittedEvents']==cell['DeliveredEvents']==delivered_count
        assert cell['MembraneState']==baseline_final['source']['Cell']['MembraneState'] and cell['Crossings']==baseline_final['source']['Cell']['Crossings']
        assert primed_motor['CompletedSamples']==10000 and primed_motor['Cell']['TimeMs']==1000 and not primed_motor['Emissions'] and not primed_motor['Cell']['Crossings']
        assert primed['source']['CompletedSamples']==8000 and not primed['source']['HasPendingFrame']
        for ordinal,e in enumerate(emissions,1):
            assert e['Ordinal']==ordinal and e['SourceInstanceKey']==identity['Instance'] and e['SourceNeuronId']==identity['Neuron']
            assert e['ProjectionId']==identity['Projection'] and e['SourceSynapseId']==identity['Synapse']
            assert e['MessageId']==stable('isolated-receptor-motor-source-cubic-v2',session,identity['Instance'],identity['Neuron'],identity['Projection'],identity['Synapse'],ordinal)
        assert [e['FiredAtNeuralMs'] for e in emissions]==cell['Crossings']
        boundaries=[]
        for source_step in range(8001,12001):
            if (source_step-8000)%cadence==0 or source_step==12000:
                # Count the original fixed motor endpoints, without rebasing or
                # multiplying a rounded reciprocal of the clock period.
                sample=min(15000,int(source_step*.125/.1)+1)
                while sample*.1>source_step*.125:sample-=1
                boundaries.append(sample*.1)
        produced=[];arrived=[];blocked=retries=0;last=1000
        for n,p in enumerate(old.records(root/'packets.jsonl.gz'),1):
            batch=p['batch'];receipt=p['receipt'];through=boundaries[n-1]
            assert batch==json.loads(p['canonicalBatchJson']) and old.text_hash(p['canonicalBatchJson'])==receipt['BatchSha256']
            assert batch['SessionId']==receipt['SessionId']==session and batch['Sequence']==receipt['Sequence']==n
            assert batch['SourceInstanceKey']==receipt['SourceInstanceKey']==identity['Instance']
            assert batch['AnatomyFingerprint']==receipt['AnatomyFingerprint']==initial['anatomyFingerprint'] and batch['CompleteThroughNeuralMs']==through
            expected=[e for e in emissions if last<e['FiredAtNeuralMs']<=through]
            assert len(batch['Spikes'])==len(expected)
            for actual,e in zip(batch['Spikes'],expected,strict=True):
                for field in ('MessageId','Ordinal','SourceNeuronId','ProjectionId','SourceSynapseId','FiredAtNeuralMs'):assert actual[field]==e[field]
                assert not actual['IsSyntheticBackground'] and not actual['IsFeedback']
                produced.append(e['MessageId'])
            assert p['replayed']==(n%37==0 or through==1500) and p['blocked']==(n%113==0 or through==1500)
            retries+=p['replayed'];blocked+=p['blocked'];arrived+=p['arrivals'];last=through
            assert all(a['ArrivalAtBodyMs']<=through-1000 for a in p['arrivals'])
        assert n==len(boundaries)==final['batches'] and last==1500 and retries==final['retries'] and blocked==final['blocked']
        assert produced==[e['MessageId'] for e in emissions]
        expected=[e for e in emissions if e['FiredAtNeuralMs']-1000+2.0625<=500];anatomy=initial['anatomy'][0]
        assert len(arrived)==final['received']==len(expected) and final['receiverPending']==len(emissions)-len(expected)
        for a,e in zip(arrived,expected,strict=True):
            for field in ('MessageId','Ordinal','SourceInstanceKey','SourceNeuronId','ProjectionId','SourceSynapseId','FiredAtNeuralMs'):assert a[field]==e[field]
            assert a['SessionId']==session and a['ContactId']==anatomy['ContactId'] and a['MotorUnitId']==anatomy['MotorUnitId']
            assert a['ArrivalAtBodyMs']==e['FiredAtNeuralMs']-1000+2.0625
        assert final['receiverCompleteThroughMs']==500 and final['source']['CompletedSamples']==12000 and not final['source']['Faulted']
        assert not final['source']['HasPendingFrame'] and final['source']['ProducedEvents']==len(source_ids)
        assert final['motorTrajectoryExactlyPreserved'] and final['sourceObservationsExactlyPreserved']
        row=next(c for c in result['cases'] if c['name']==name)
        assert row==dict(name=name,key=key,cadence=cadence,disconnected=cut,completed=True,sourceFrames=12000,motorSamples=15000,
            motorEvents=len(emissions),received=len(arrived),batches=n,blocked=blocked,retries=retries)
        outcomes.append(dict(row,selectedEvents=selected_count,deliveredReceptorEvents=delivered_count,originalTrajectoryExactlyPreserved=True))
    assert h.digest(package/'managed-runtime-before.json')==h.digest(package/'reference/qualified-managed-runtime.json')
    return dict(completed=True,bodyEpochSourceGatePassed=True,tests=tests,cases=outcomes,sourceFrames=120000,motorTrajectoryRows=150010,
        nativePhysicsExecuted=False,muscleDynamicsExecuted=False,upstreamNumericalAccuracyQualified=False,physiologyQualified=False,avatarRecoveryAssessed=False)


if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    parser=argparse.ArgumentParser();parser.add_argument('command',choices=['prepare','check-tests','audit']);parser.add_argument('package',type=Path,nargs='?');args=parser.parse_args()
    if args.command=='prepare':prepare()
    elif args.command=='check-tests':print(json.dumps(gate.check_tests(args.package.resolve(),COUNTS),indent=2))
    else:
        result=audit(args.package.resolve());save(args.package.resolve()/'analysis.json',result);print(json.dumps(result,indent=2),flush=True)

"""Read only the operator-reported failed capture; do not run its model/tests."""
import argparse
from collections import Counter
import csv
from datetime import datetime, timezone
import gzip
from hashlib import sha256
import importlib.util
import json
import math
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path)
    result=importlib.util.module_from_spec(spec);spec.loader.exec_module(result);return result


def records(path):
    with gzip.open(path,'rt',encoding='utf-8') as stream:
        for line in stream: yield json.loads(line)


def main(reported):
    if not reported: raise RuntimeError('Wait for the operator completion report.')
    common=module('rubral_failure_integrity',ROOT/'tools/review-olive-and-motor-20261109.py')
    archive=module('rubral_failure_archive',ROOT/'tools/review-spindle-root-and-causal-motor-20261113.py')
    package=ROOT/'artifacts/rubral-motor-20261117-e42efc'
    output=ROOT/'artifacts/rubral-output-failure-review-20261117'
    assert not output.exists(),'Preserve prior review.'
    revision='4cffbcc2a05e8e8233131c01c7c22b7e0975fee9'
    review=common.verify(package,['test-binaries.json','probe-binaries.json'],revision,'FAILED',1)
    review['verifiedGitFiles']=archive.source_archive(common,package,revision)
    frozen=module('rubral_frozen_gate',package/'source/tools/rubral_motor_gate.py')
    tests=frozen.gate.check_tests(package,frozen.COUNTS);assert tests['passedCases']==136
    status=common.read(package/'window-status.json')
    assert status['FocusedTestsPassed'] and status['StudyExecutionAttempted']
    assert not status['ComponentCausalityChecksPassed'] and not status['ObservedNeuronalMotorResponse']
    assert common.digest(package/'managed-runtime-before.json')==common.digest(package/'reference/qualified-managed-runtime.json')
    assert not (package/'managed-runtime-after.json').exists()
    study=package/'study';folder=study/'no-evoked-drive'
    assert {p.name for p in study.iterdir() if p.is_dir()}=={'no-evoked-drive'}
    assert not (study/'results.json').exists() and not (folder/'result.json').exists()
    failure=common.read(folder/'failure.json')
    assert failure['error'].startswith('System.IO.IOException:') and 'used by another process' in failure['error']
    assert 'RubralMotorStudy.Hash' in failure['error'] and failure['pending'] is None
    source=failure['source'];assert source['CompletedSamples']==source['EngineCompletedTick']==12000
    assert source['ActualCompletedThroughMs']==source['EngineTickTimestampMs']==1500
    assert source['ProducedEvents']==source['AdmittedFixtureEvents']==0 and not source['Faulted'] and not source['HasPendingFrame']
    fixture=common.read(folder/'input.json');assert fixture['quanta']==0 and fixture['originalEvents']==[]
    topology=common.read(folder/'topology.json');assert len(topology['allRoutes'])==256 and len(topology['contacts'])==1
    contact=topology['contactIds'][0]
    frame_count=0
    for frame_count,frame in enumerate(records(common.io(folder/'source-frames.jsonl.gz')),1):
        assert frame['Sequence']==frame_count and frame['CompleteThroughMs']==frame_count*.125 and frame['Session']==fixture['session']
        assert sha256(frame['CanonicalJson'].encode()).hexdigest().upper()==frame['Sha256']
        body=json.loads(frame['CanonicalJson']);obs=body['Observation']
        assert body['OriginalEvents']==[] and body['FiredCount']==body['AdmittedFixtureEvents']==0
        assert body['Sequence']==frame_count and body['CompleteThroughMs']==frame_count*.125 and body['Session']==fixture['session']
        assert obs['CompletedTick']==frame_count and obs['TickTimestampMs']==frame_count*.125 and len(obs['Cells'])==256
    assert frame_count==12000
    branches=[];trajectory_hashes=[];last_states=[]
    for key,cadence in [('packets1',1),('packets17',17),('contacts-disabled',17)]:
        root=folder/key;initial=common.read(root/'initial.json');final=common.read(root/'final.json')
        state=final['source'];cell=state['Cell']
        assert state['CompletedSamples']==15000 and cell['TimeMs']==1500 and not state['Faulted'] and not cell['Faulted']
        assert cell['AdmittedEvents']==cell['DeliveredEvents']==final['received']==final['receiverPending']==0
        assert state['Emissions']==cell['Crossings']==final['tail']==final['pendingBeyondHorizon']==[]
        assert final['receiverCompleteThroughMs']==1500 and initial['cadence']==cadence
        coverage=Counter();count=0
        for count,item in enumerate(records(common.io(root/'receptor-admissions.jsonl.gz')),1):
            receipt=item['receipt'];assert receipt['Sequence']==count and receipt['ContactId']==contact and receipt['Events']==0
            expected=list(range((count-1)*cadence+1,min(count*cadence,12000)+1))
            assert item['sourceSequences']==expected and receipt['CompleteThroughSourceMs']==expected[-1]*.125
            assert item['selectedOriginalIds']==item['deliveredOriginalIds']==item['selectedOriginalJson']==[]
            assert sha256(item['canonicalInputJson'].encode()).hexdigest().upper()==receipt['Sha256']
            coverage.update(expected)
        assert coverage==Counter(range(1,12001)) and count==final['inputBatches']==math.ceil(12000/cadence)
        count=0
        for count,item in enumerate(records(common.io(root/'motor-transport.jsonl.gz')),1):
            batch=item['batch'];receipt=item['receipt']
            assert batch['Spikes']==item['arrivals']==[] and batch['Sequence']==receipt['Sequence']==count
            assert batch['CompleteThroughNeuralMs']==min(count*cadence,15000)*.1
            assert json.loads(item['canonicalBatchJson'])==batch
            assert sha256(item['canonicalBatchJson'].encode()).hexdigest().upper()==receipt['BatchSha256']
        assert count==final['outputBatches']==math.ceil(15000/cadence)
        with common.io(root/'trajectory.csv').open(newline='',encoding='utf-8-sig') as stream:
            reader=csv.reader(stream);header=next(reader);assert len(header)==35
            for rows,row in enumerate(reader,1):
                values=list(map(float,row));assert len(values)==35 and all(map(math.isfinite,values))
                assert values[0]==(rows-1)*.1 and values[-2:]==[0,0]
            assert rows==15001 and values[1:31]==cell['MembraneState']
        trajectory_hashes.append(common.digest(root/'trajectory.csv'));last_states.append(cell['MembraneState'])
        branches.append(dict(branch=key,sampledStates=rows,inputBatches=final['inputBatches'],outputBatches=count,
            sourceInputs=0,motorEvents=0,received=0,pending=0))
    assert len(set(trajectory_hashes))==1 and all(s==last_states[0] for s in last_states)
    review.update(reviewedUtc=datetime.now(timezone.utc).isoformat(),operatorReportedCompletion=True,
        originalStatus='FAILED',failureKind='checksum attempted before writer handles closed',
        testInventory=tests,passedTests=136,completedSourceFrames=frame_count,completedPrefixBranches=branches,
        savedQuietTrajectoriesExactlyEqual=True,activeConditionsExecuted=0,initialManagedRuntimeMatched=True,
        postflightManagedRuntimeRecorded=False,fullExperimentComplete=False,observedNeuronalMotorResponse=False,
        modelOrTestExecutedDuringReview=False,liveBrainAccessed=False,physiologyQualified=False,avatarRecoveryAssessed=False)
    output.mkdir();(output/'review.json').write_text(json.dumps(review,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(review,indent=2),flush=True)


if __name__=='__main__':
    if not __debug__: raise RuntimeError('Assertions must remain enabled.')
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--operator-reported-complete',action='store_true')
    main(parser.parse_args().operator_reported_complete)

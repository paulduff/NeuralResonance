"""Prescribed head/canal/nerve accounting; not an independent trajectory solve."""
import csv
import gzip
from itertools import islice
import json
import math
import numpy as np
from afferent_precision_gate import read
from afferent_trajectory_protocol import labels,group
from afferent_trajectory_analysis import balance,RESIDUALS
from coupled_canal_analysis import vector
from canal_midpoint_protocol import forcing_ticks
from head_turn_afferent_protocol import protocol
from head_turn_input_evidence import audit as head_audit
from head_observation_identity import original_observation


def analyse(package):
    p=read(package/'connected-input.json');assert p==protocol(read(package/'qualified-connected-input.json'));forcing='MidpointHeld'
    result=read(package/'study/results.json');assert result['completed'] and len(result['runs'])==6
    assert read(package/'study/input.json')==p
    fixture=p['fixture'];states,ledgers=labels(fixture);ns,nl=len(states),len(ledgers)
    saved_initial=read(package/'sensory/study/quiet-ticks64/initial.json')
    saved_geometry=read(package/'baseline/study/geometry.json')
    assert saved_initial['stateLabels']==states and saved_initial['ledgerLabels']==ledgers
    diagnostics=list(RESIDUALS.values())+['nonlinearEvaluations','maximumNewtonIterations','maximumAcceptedScaledResidual']
    header=['step','timeMs','heldBundleNm','mechanicalStartTick','mechanicalEndTick','mechanicalEndNeuralMs','representationDifferenceMs',
        'startMv','endMv','startSlopeMvPerMs','endSlopeMvPerMs']+states+['stateError/'+n for n in states]+ledgers+['ledgerError/'+n for n in ledgers]+diagnostics
    v_slice=slice(11,11+ns);e_slice=slice(11+ns,11+2*ns);l_slice=slice(11+2*ns,11+2*ns+nl);le_slice=slice(11+2*ns+nl,11+2*ns+2*nl)
    reports=[];endpoints={};initial_combined=np.array(saved_initial['values']+saved_initial['ledgers'])
    for expected,run in zip([(k,n) for k in p['conditions'] for n in p['sourceStepsPerInterval']],result['runs'],strict=True):
        condition,count=expected;folder=package/'study'/f'{condition}-n{count}'
        initial=read(folder/'initial.json');final=read(folder/'final.json');source=initial['source'];clock=initial['binding']['physicalClock']
        if forcing=='MidpointHeld':
            assert read(folder/'sampling.json')==dict(policy=forcing,version='canal-to-observed-afferent-midpoint-owner-v2',
                ownerFingerprint=initial['fingerprint'],mechanicalTicksPerSourceStep=262144//count,offsetTicks=262144//count//2)
        assert initial['field']==saved_geometry['field'] and initial['attachment']==saved_geometry['attachment']
        n=initial['neuronal'];assert final==run and final['completed'] and final['failure'] is None
        assert (final['condition'],final['count'])==expected and final['steps']==p['physicalIntervals']*count and final['intervals']==p['physicalIntervals'] and final['packets']==p['physicalIntervals']*count//512
        for name in ('values','initialValues','stateLabels','ledgerLabels','ledgers','additionResiduals','ledgerAdditionResiduals','perilymphMv','initialStateFingerprint'):
            assert n[name]==saved_initial[name],name
        assert n['failure'] is None and n['completedSteps']==0 and n['nonlinearEvaluations']==0
        assert n['settings']==dict(stepMs=clock['physicsStepMs']/count,**p['nonlinearSettings'])
        assert n['sessionId']==source['sessionId']==clock['sessionId'] and n['cellId']==source['cellId']==initial['attachment']['hairCellId']
        assert n['epochMs']==n['timeMs']==source['epochMs']==clock['neuralEpochMs']+clock['physicsStepMs']
        assert source['stepMs']==clock['physicsStepMs']/count and source['cableNodeId']==p['observationNode']
        head_input=read(folder/'head-input.json');head_audit(head_input,p,condition,clock)
        # The source frame's serialized clock may carry its computed fingerprint.
        field=initial['field'];attachment=initial['attachment'];radius=field['radiusMeters'];area=math.pi*radius*radius
        depth=math.sqrt(1-attachment['rootXOverRadius']**2);height=attachment['bundleLengthMeters']/radius
        gain=attachment['excitatoryPolarity']*2*height*(2*depth-height)/area*1e9
        stride=262144//count;max_input_error=0.;max_rounding=0.;maximum={key:0. for key in p['ledgerResidualBounds']};history=[]
        ranges={name:[float(n['values'][i]),float(n['values'][i])] for i,name in enumerate(states) if name.endswith('Mv')};bundle_range=[0.,0.]
        last_values=np.array(n['values']);node=states.index('axon/'+p['observationNode']+'/potentialMv');bath=n['perilymphMv']
        with gzip.open(folder/'trajectory.csv.gz','rt',encoding='utf-8',newline='') as stream:
            reader=csv.reader(stream);assert next(reader)==header
            for interval in range(p['physicalIntervals']):
                head=interval+2;mechanical=read(folder/f'mechanical-{head:02}.json');receipt=mechanical['mechanics'];released=receipt['completedInterval']
                assert released['heldInput']['skull']['physicsStep']==head-1 and released['closingInput']['skull']['physicsStep']==head
                original_observation(released['heldInput']['skull']['rawObservationJson'],head_input['observations'][head-1])
                original_observation(released['closingInput']['skull']['rawObservationJson'],head_input['observations'][head])
                substeps=released['substeps'];assert len(substeps)==1412
                ends=np.array([s['clockSubstepIndex'] for s in substeps],dtype=np.int64)
                first_tick=(head-1)*262144;starts=np.concatenate(([first_tick],ends[:-1]));widths=ends-starts
                before=np.array([vector(s['transition']['before']) for s in substeps]);after=np.array([vector(s['transition']['after']) for s in substeps])
                stage1=np.array([vector(s['transition']['firstStage']['point'])+list(s['transition']['firstStage']['accelerationMeters3PerSecond2'].values()) for s in substeps])
                stage2=np.array([vector(s['transition']['lastStage']['point'])+list(s['transition']['lastStage']['accelerationMeters3PerSecond2'].values()) for s in substeps])
                if condition=='quiet':assert not np.any(before) and not np.any(after) and not np.any(stage1) and not np.any(stage2)
                rows=np.array(list(islice(reader,count)),dtype=float);assert rows.shape==(count,len(header)) and np.isfinite(rows).all()
                steps=np.arange(interval*count+1,(interval+1)*count+1,dtype=np.int64);start_ticks=262144+(steps-1)*stride;end_ticks=start_ticks+stride
                assert np.array_equal(rows[:,0],steps) and np.array_equal(rows[:,3],start_ticks) and np.array_equal(rows[:,4],end_ticks)
                times=source['epochMs']+steps*source['stepMs'];mechanical_times=clock['neuralEpochMs']+end_ticks*(clock['physicsStepMs']/262144)
                assert np.array_equal(rows[:,1],times) and np.array_equal(rows[:,5],mechanical_times) and np.array_equal(rows[:,6],times-mechanical_times)
                max_rounding=max(max_rounding,float(np.max(abs(rows[:,6]))))
                sampled_ticks=forcing_ticks(start_ticks,stride,forcing)
                idx=np.searchsorted(ends,sampled_ticks,side='left');theta=(sampled_ticks-starts[idx])/widths[idx]
                dt=np.array([s['transition']['stepSeconds'] for s in substeps])[idx]
                q=before[idx,3]+dt*(theta*(1.5-.75*theta)*stage1[idx,9]+theta*(.75*theta-.5)*stage2[idx,9])
                q=np.where(theta==0,before[idx,3],np.where(theta==1,after[idx,3],q));held=gain*q
                scale=p['bundle']['denseProjectionArithmeticAbsolute']+p['bundle']['denseProjectionArithmeticRelative']*abs(held)
                max_input_error=max(max_input_error,float(np.max(abs(rows[:,2]-held)/scale)))
                values=rows[:,v_slice];integrals=rows[:,l_slice]
                bundle_range=[min(bundle_range[0],float(rows[:,2].min())),max(bundle_range[1],float(rows[:,2].max()))]
                for i,name in enumerate(states):
                    if name in ranges:ranges[name]=[min(ranges[name][0],float(values[:,i].min())),max(ranges[name][1],float(values[:,i].max()))]
                for index,name in enumerate(states):
                    if group(name)=='gate':assert ((values[:,index]>=0)&(values[:,index]<=1)).all(),name
                    if group(name)=='ion':assert (values[:,index]>0).all(),name
                assert np.array_equal(rows[:,8],values[:,node]-bath)
                assert np.array_equal(rows[:,7],np.concatenate(([last_values[node]-bath],values[:-1,node]-bath)))
                last_values=values[-1].copy()
                measured=balance(fixture,np.vstack((initial_combined,np.column_stack((values,integrals)))))
                for key in maximum:maximum[key]=max(maximum[key],measured[key])
                for i,key in enumerate(RESIDUALS):assert np.max(rows[:,-7+i])<=p['ledgerResidualBounds'][key]
                assert np.max(rows[:,-2])<=p['nonlinearSettings']['maximumNewtonIterations'] and np.max(rows[:,-1])<=1
                history.append(values[-1].tolist())
            assert next(reader,None) is None,'Unexpected additional source steps.'
        state=final['state'];assert state['failure'] is None and state['pendingPacket'] is None and state['mechanical']['pendingTrace'] is None
        assert state['completedSteps']==state['acknowledgedSteps']==p['physicalIntervals']*count and state['observer']['observedThroughStep']==p['physicalIntervals']*count
        for subset,name in ((v_slice,'values'),(e_slice,'additionResiduals'),(l_slice,'ledgers'),(le_slice,'ledgerAdditionResiduals')):
            assert rows[-1,subset].tolist()==state['neuronal'][name]
        event_receipts=[json.loads(line) for line in (folder/'events.jsonl').read_text(encoding='utf-8').splitlines()]
        assert len(event_receipts)==final['events']==state['observer']['eventCount']
        captured={r['completedStep']:r for r in event_receipts};assert len(captured)==len(event_receipts)
        with (folder/'packets.jsonl').open(encoding='utf-8') as stream:packets=[json.loads(line) for line in stream]
        through=0;ordinal=0
        for packet in packets:
            assert packet['source']==source and packet['startingStep']==through and packet['completedStep']==through+512
            assert packet['startMs']==source['epochMs']+through*source['stepMs'] and packet['endMs']==source['epochMs']+(through+512)*source['stepMs']
            for event in packet['events']:
                ordinal+=1;assert event['ordinal']==ordinal and through<event['completedStep']<=through+512
                receipt=captured[event['completedStep']];original=receipt['event'];crossing=original['crossing'];bracket=receipt['interval']
                assert event['messageId']==original['messageId'] and event['crossingTimeMs']==crossing['timeMs'] and original['ordinal']==ordinal
                assert original['sessionId']==source['sessionId'] and original['cellId']==source['cellId'] and original['cableNodeId']==source['cableNodeId']
                assert bracket['startMs']<=crossing['timeMs']<=bracket['endMs'] and bracket['startMv']<p['thresholdMv']<=bracket['endMv']
                assert abs(crossing['residualMv'])<=1e-8 and crossing['minimumScaledSlope']>0
            through=packet['completedStep']
        assert through==p['physicalIntervals']*count and ordinal==len(captured) and len(packets)==final['packets']
        conservation=all(maximum[key]<=bound for key,bound in p['ledgerResidualBounds'].items())
        passed=max_input_error<=1 and conservation
        reports.append(dict(condition=condition,sourceStepsPerInterval=count,steps=through,packets=len(packets),events=ordinal,
            heldInputArithmeticMaximumFraction=max_input_error,maximumClockRepresentationDifferenceMs=max_rounding,
            independentLedgerResiduals=maximum,conservationPassed=conservation,accountingPassed=bool(passed),
            bundleRangeNm=bundle_range,voltageRangesMv=ranges))
        endpoints[(condition,count)]=np.array(history)
    responses=[dict(condition=condition,sourceStepsPerInterval=n,maximumEndpointVoltageDifferenceMv=float(np.max(abs(
        endpoints[(condition,n)][:,[i for i,name in enumerate(states) if name.endswith('Mv')]]-
        endpoints[('quiet',n)][:,[i for i,name in enumerate(states) if name.endswith('Mv')]]))))
        for condition in ('yaw-positive','yaw-negative') for n in p['sourceStepsPerInterval']]
    assert sum(r['steps'] for r in reports)==p['expectedSourceSteps']
    return dict(completed=True,connectedAccountingPassed=all(r['accountingPassed'] for r in reports),
        prescribedMotionAccountingPassed=True,runs=reports,sourceSteps=sum(r['steps'] for r in reports),responses=responses,
        independentConnectedAccuracyQualified=False,physiologicalCalibrationQualified=False,afferentFiringQualified=False,avatarBalanceAssessed=False)

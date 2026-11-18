"""Saved joint sensory/nerve trajectories and ledgers; no equation evaluations."""
import csv
import re
from decimal import localcontext
import numpy as np
from afferent_trajectory_protocol import labels,group
from afferent_trajectory_reference import Equation,input_record
from afferent_numerical_protocol import protocol as derivative_protocol
from afferent_numerical_analysis import agreement,joint_conservation,read
from cleft_rest_analysis import comparison as record_comparison
from cleft_trajectory_analysis import balance as sensory_balance,comparison
from cleft_trajectory_protocol import labels as sensory_labels

DIAGNOSTICS=('maximumChargeResidualFc','maximumIonResidualAttomoles','maximumCableChargeResidualFc',
    'maximumInterfaceChargeResidualFc','nonlinearEvaluations','maximumNewtonIterations','maximumAcceptedScaledResidual')
RESIDUALS=dict(chargeFc='maximumChargeResidualFc',ionAttomoles='maximumIonResidualAttomoles',
    cableChargeFc='maximumCableChargeResidualFc',interfaceChargeFc='maximumInterfaceChargeResidualFc')


def balance(fixture,values):
    """Independent capacitor/incidence matrices using saved state and charges."""
    states,ledgers=labels(fixture);names=states+ledgers;lookup={n:i for i,n in enumerate(names)}
    old_states,old_ledgers=sensory_labels(fixture['sensory'])
    result=sensory_balance(fixture['sensory'],values[:,[lookup[n] for n in old_states+old_ledgers]])
    topology=fixture['cableTopology'];nodes=sorted(topology['nodes'],key=lambda n:n['id'])
    index={n['id']:i for i,n in enumerate(nodes)};incidence=np.zeros((len(nodes),len(ledgers)))
    ledger_index={n:i for i,n in enumerate(ledgers)}
    for n in nodes:incidence[index[n['id']],ledger_index['axon-membrane/'+n['id']+'/outwardChargeFc']]-=1
    for edge in topology['edges']:
        k=ledger_index['axon-edge/'+edge['id']+'/leftToRightChargeFc']
        incidence[index[edge['leftId']],k]-=1;incidence[index[edge['rightId']],k]+=1
    clefts=sorted(fixture['sensory']['states'],key=lambda n:n['id']);patch_index={n['id']:i for i,n in enumerate(clefts)}
    interface=np.zeros((len(clefts),len(ledgers)))
    for n in clefts:interface[patch_index[n['id']],ledger_index['membrane/'+n['id']+'/calyxToAxonChargeFc']]=1
    attachments={a['terminalId']:a['cleftCompartmentId'] for a in fixture['attachments']}
    for terminal in topology['terminals']:
        k=ledger_index['axon-terminal/'+terminal['id']+'/nodeToCalyxChargeFc'];incidence[index[terminal['nodeId']],k]-=1
        if terminal['id'] in attachments:interface[patch_index[attachments[terminal['id']]],k]+=1
        else:assert terminal['conductanceNs']==0 and (values[:,len(states)+k]==0).all(),'Unattached terminal carried charge.'
    voltage=np.array([lookup['axon/'+n['id']+'/potentialMv'] for n in nodes])
    capacitance=np.array([2*np.pi*n['radiusMicrometers']*n['lengthMicrometers']*n['membrane']['specificCapacitancePfPerSquareMicrometer'] for n in nodes])
    charge=(values[:,voltage]-values[0,voltage])*capacitance;integrals=values[:,len(states):]
    result['cableChargeFc']=float(np.max(np.abs(charge-integrals@incidence.T)))
    result['interfaceChargeFc']=float(np.max(np.abs(integrals@interface.T)))
    return result


def native(package,protocol,case,ticks,source):
    folder=package/'study'/f"{case['key']}-ticks{ticks}";initial=read(folder/'initial.json');final=read(folder/'final.json')
    equation=Equation(case['fixture'],source,protocol['referenceDecimalPrecision']);state_names,ledger_names=labels(case['fixture'])
    for s in (initial,final):
        assert s['stateLabels']==state_names and s['ledgerLabels']==ledger_names and s['failure'] is None
        assert s['sessionId']==protocol['sessionId'] and s['cellId']==case['cellId'] and s['version']==protocol['kernelVersion']
        assert s['settings']==dict(stepMs=ticks/protocol['ticksPerMs'],**protocol['nonlinearSettings'])
        assert s['epochMs']==0 and s['perilymphMv']==case['fixture']['sensory']['perilymphMv']
        assert s['initialValues']==equation.initial[:equation.physical_count].tolist()
    assert initial['values']==initial['initialValues'] and not any(initial['ledgers']) and initial['completedSteps']==0
    assert not any(initial['additionResiduals']) and not any(initial['ledgerAdditionResiduals']) and initial['mostRecentHeldInput'] is None
    assert all(initial[k]==0 for k in DIAGNOSTICS) and initial['timeMs']==0
    for k in ('fingerprint','initialStateFingerprint'):
        assert initial[k]==final[k] and re.fullmatch('[0-9A-Fa-f]{64}',initial[k])
    step_count=case['totalTicks']//ticks
    assert final['completedSteps']==step_count
    assert final['mostRecentHeldInput']==dict(startingStep=step_count-1,requestedEndStep=step_count,
        bundleDisplacementNanometers=case['segments'][-1]['bundleDisplacementNanometers'])
    header=['tick','step','timeMs']+state_names+['stateError/'+n for n in state_names]+ledger_names+['ledgerError/'+n for n in ledger_names]
    samples=[];sample_ticks=[];accepted=[];count=0;previous={k:0 for k in DIAGNOSTICS}
    with (folder/'trajectory.csv').open(newline='') as stream:
        reader=csv.DictReader(stream);assert reader.fieldnames==header+list(DIAGNOSTICS)
        for i,row in enumerate(reader):
            assert len(row)==len(reader.fieldnames) and None not in row
            assert int(row['tick'])==i*ticks and int(row['step'])==i and float(row['timeMs'])==i*(ticks/protocol['ticksPerMs'])
            state=np.array([float(row[n]) for n in state_names]);ledger=np.array([float(row[n]) for n in ledger_names])
            physical=np.concatenate((state,ledger));equation.validate_samples(physical[:,None]);accepted.append(physical.tolist())
            assert all(np.isfinite(float(v)) for v in row.values())
            for name in DIAGNOSTICS:
                v=float(row[name]);assert v>=previous[name];previous[name]=v
            assert previous['maximumNewtonIterations']<=protocol['nonlinearSettings']['maximumNewtonIterations']
            assert previous['maximumAcceptedScaledResidual']<=1
            if i==0:
                assert physical.tolist()==equation.initial.tolist()
                assert all(float(row['stateError/'+n])==0 for n in state_names)
                assert all(float(row['ledgerError/'+n])==0 for n in ledger_names)
            if i*ticks%protocol['sampleTicks']==0:sample_ticks.append(i*ticks);samples.append(physical.tolist())
            last=row;count+=1
    assert count==step_count+1 and int(last['tick'])==case['totalTicks']
    for names,field,prefix in [(state_names,'values',''),(state_names,'additionResiduals','stateError/'),
                               (ledger_names,'ledgers',''),(ledger_names,'ledgerAdditionResiduals','ledgerError/')]:
        assert [float(last[prefix+n]) for n in names]==final[field]
    for name in DIAGNOSTICS:assert float(last[name])==final[name]
    assert final['timeMs']==float(last['timeMs'])
    return np.array(sample_ticks),np.array(samples),dict(rows=count,final=final,
        independentAllEndpointLedgerResiduals=balance(case['fixture'],np.array(accepted)))


def reference_saved(folder,protocol,case,setting,source):
    eq=Equation(case['fixture'],source,protocol['referenceDecimalPrecision'])
    assert read(folder/'input.json')==input_record(eq,case,setting,protocol)
    status=read(folder/'results.json');assert status['completed'] and len(status['segments'])==4
    assert status['allAcceptedNodesPhysicallyAdmissible'] and status['fastTransientOnly']
    for k in ('candidateValuesUsedForIntegration','stationaryInitializationQualified','afferentFiringQualified','physiologyQualified','avatarRecoveryAssessed'):assert not status[k]
    previous=eq.initial;start=0;maximum={k:0. for k in protocol['ledgerResidualBounds']};all_ticks=[];all_values=[]
    for i,segment in enumerate(case['segments']):
        meta=read(folder/f'segment-{i}.json');assert meta==status['segments'][i] and meta['success']
        assert meta['segment']==i and meta['startTick']==start and meta['endTick']==segment['endTick']
        with np.load(folder/f'segment-{i}-nodes.npz',allow_pickle=False) as saved:
            t=saved['timeMs'];y=saved['values'];eq.validate_samples(y)
            assert t.shape==(y.shape[1],) and len(t)==meta['acceptedNodes'] and np.isfinite(t).all() and (np.diff(t)>0).all()
            assert t[0]==start/protocol['ticksPerMs'] and t[-1]==segment['endTick']/protocol['ticksPerMs']
            assert np.array_equal(y[:,0],previous);previous=y[:,-1].copy()
            ledger=balance(case['fixture'],np.vstack((eq.initial,y.T)))
            maximum={k:max(maximum[k],ledger[k]) for k in maximum}
        witnesses=read(folder/f'segment-{i}-precision.json');assert len(witnesses)==2
        for j,witness in enumerate(witnesses):
            assert witness['tick']==(start if j==0 else segment['endTick']) and witness['state']==y[:,0 if j==0 else -1].tolist()
            a,b=witness['references'];p=record_comparison(a,b,dict(absolute='1e-40',relative='1e-40'))
            w=record_comparison(witness['reference40'],b,dict(absolute='1e-25',relative='1e-25'))
            assert p==witness['precision'] and w==witness['workingPrecision'] and p['passed'] and w['passed']
        with np.load(folder/f'segment-{i}-samples.npz',allow_pickle=False) as saved:
            ticks=saved['ticks'];samples=saved['values'];eq.validate_samples(samples)
            assert np.array_equal(ticks,np.arange(start,segment['endTick']+1,protocol['sampleTicks'],dtype=np.int64))
            assert samples.shape==(eq.size,len(ticks))
            assert comparison(samples[:,[0,-1]].T,y[:,[0,-1]].T,eq.labels,protocol['referenceComparison'])['maximumAllowedFraction']<=1
            if i:ticks=ticks[1:];samples=samples[:,1:]
            all_ticks.extend(ticks.tolist());all_values.extend(samples.T.tolist())
        start=segment['endTick']
    with np.load(folder/'samples.npz',allow_pickle=False) as saved:
        assert np.array_equal(saved['ticks'],np.array(all_ticks,dtype=np.int64))
        assert np.array_equal(saved['values'],np.array(all_values,dtype=np.float64))
        values=saved['values'].copy()
    assert status['samples']==len(all_ticks)
    ledger=balance(case['fixture'],np.vstack((eq.initial,values)))
    return np.array(all_ticks),values,{k:max(maximum[k],ledger[k]) for k in maximum}


def initial_checks(package,protocol):
    records=read(package/'initial-reference-checks.json');spec=derivative_protocol();results=[]
    assert len(records)==len(protocol['cases'])
    with localcontext() as context:
        context.prec=100
        for case,row in zip(protocol['cases'],records,strict=True):
            assert row['key']==case['key'] and row['input']==case['fixture'] and row['precisions']==[60,90]
            a,b=row['references'];precision=record_comparison(a,b,spec['referenceComparison'])
            native_values=[read(package/'study'/f"{case['key']}-ticks{ticks}"/'initial-derivative.json') for ticks in protocol['stepTicks']]
            assert all(v==native_values[0] for v in native_values)
            candidate=agreement('joints',native_values[0],b,spec)
            accounting=[joint_conservation(case['fixture'],v,spec['conservation']).result() for v in [native_values[0],a,b]]
            result=dict(key=case['key'],precision=precision,candidate=candidate,accounting=accounting)
            result['passed']=precision['passed'] and candidate['passed'] and all(c['passed'] for c in accounting)
            assert row['checks']==result;results.append(result)
    return results


def analyse(package,protocol,source):
    assert read(package/'study/input.json')=={k:protocol[k] for k in ('version','kernelVersion','sessionId','ticksPerMs','sampleTicks',
        'stepTicks','expectedCases','expectedNativeRuns','expectedReferences','cases','nonlinearSettings','fastTransientOnly')}
    assert read(package/'study/results.json')==dict(completed=True,nativeRuns=21,failedRuns=[],fastTransientOnly=True,
        independentCoupledTrajectoryQualified=False,stationaryInitializationQualified=False,physiologyQualified=False,avatarRecoveryAssessed=False)
    assert read(package/'reference-trajectories/results.json')==dict(completed=True,attempted=21,failedRuns=[],fastTransientOnly=True)
    initial=initial_checks(package,protocol);rows=[];all_ok=all(r['passed'] for r in initial)
    for case in protocol['cases']:
        names=sum(labels(case['fixture']),[]);expected_ticks=np.arange(0,case['totalTicks']+1,protocol['sampleTicks'],dtype=np.int64)
        references=[];reference_ledgers=[]
        for setting in protocol['referenceSettings']:
            folder=package/'reference-trajectories'/(case['key']+'-'+setting['key'])
            ticks,values,ledger=reference_saved(folder,protocol,case,setting,source)
            assert np.array_equal(ticks,expected_ticks) and values.shape==(len(expected_ticks),len(names))
            references.append(values);reference_ledgers.append(ledger)
        errors=[comparison(references[i],references[1],names,protocol['referenceComparison']) for i in (0,2)]
        reference_pass=all(e['maximumAllowedFraction']<=1 for e in errors)
        reference_pass &= all(all(l[k]<=protocol['ledgerResidualBounds'][k] for k in l) for l in reference_ledgers)
        runs=[];initial_fingerprint=None
        for ticks in protocol['stepTicks']:
            grid,values,metadata=native(package,protocol,case,ticks,source);assert np.array_equal(grid,expected_ticks)
            if initial_fingerprint is None:initial_fingerprint=metadata['final']['initialStateFingerprint']
            assert metadata['final']['initialStateFingerprint']==initial_fingerprint
            error=comparison(values,references[1],names,protocol['candidateComparison']);ledger=balance(case['fixture'],values)
            for k,field in RESIDUALS.items():ledger[k]=max(ledger[k],metadata['final'][field],metadata['independentAllEndpointLedgerResiduals'][k])
            runs.append(dict(stepTicks=ticks,stepMs=ticks/protocol['ticksPerMs'],rows=metadata['rows'],error=error,ledger=ledger,
                fingerprint=metadata['final']['fingerprint'],nonlinearEvaluations=metadata['final']['nonlinearEvaluations']))
        ratios=[r['error']['maximumAllowedFraction'] for r in runs]
        refinement=all(b<=protocol['refinement']['maximumRemainingFraction']*a+protocol['refinement']['normalizedErrorFloor'] for a,b in zip(ratios,ratios[1:]))
        ledger_pass=all(all(r['ledger'][k]<=protocol['ledgerResidualBounds'][k] for k in RESIDUALS) for r in runs)
        passed=reference_pass and ratios[-1]<=1 and refinement and ledger_pass;all_ok &= passed
        rows.append(dict(key=case['key'],referenceCriteriaPassed=bool(reference_pass),referenceErrors=errors,referenceLedgers=reference_ledgers,
            candidates=runs,finestCriteriaPassed=ratios[-1]<=1,sampledRefinementPassed=refinement,ledgerCriteriaPassed=ledger_pass,passed=bool(passed)))
    return dict(completed=True,numericalTrajectoryCriteriaPassed=bool(all_ok),nativeRuns=21,references=21,cases=rows,initialDerivativeChecks=initial,
        fastTransientOnly=True,originalBoundsChanged=False,stationaryInitializationQualified=False,spatialGeometryCalibrated=False,
        afferentFiringQualified=False,physiologyQualified=False,avatarRecoveryAssessed=False)

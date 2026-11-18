"""Saved full-trajectory validation and prospective numerical criteria."""
import csv
from decimal import Decimal,localcontext
import json
from pathlib import Path
import numpy as np
from cleft_trajectory_protocol import labels,group
from cleft_trajectory_reference import Equation,base

read=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))


def balance(fixture,values):
    """Independent capacitor/incidence ledgers; no membrane equation evaluation."""
    state_names,ledger_names=labels(fixture);n=len(fixture['topology']['compartments']);count=len(state_names)
    t=fixture['topology'];nodes=sorted(t['compartments'],key=lambda x:x['id']);idx={p['id']:i for i,p in enumerate(nodes)}
    lookup={name:i for i,name in enumerate(ledger_names)};size=2*n+3;endo=size-2;bath=size-1
    c=np.zeros((size,count));m=np.zeros((size,len(ledger_names)));species=np.zeros((2*n,len(ledger_names)))
    def capacitor(a,b,state_a,state_b,cap):
        if state_a is not None:c[a,state_a]+=cap;c[b,state_a]-=cap
        if state_b is not None:c[a,state_b]-=cap;c[b,state_b]+=cap
    def current(name,a,b):
        k=lookup[name]
        if a is not None:m[a,k]-=1
        if b is not None:m[b,k]+=1
    capacitor(0,endo,0,None,t['hairApicalPf']);capacitor(0,bath,0,None,t['hairPerilymphPf'])
    current('metOutwardChargeFc',0,endo);current('endolymphHoldingChargeFc',None,endo);current('perilymphHoldingChargeFc',None,bath)
    for i,node in enumerate(nodes):
        s=1+2*i;cy=s+1;o=1+11*i;prefix='membrane/'+node['id']+'/'
        capacitor(0,s,0,o,node['hairCleftPf']);capacitor(cy,s,o+1,o,node['calyxInnerPf']);capacitor(cy,bath,o+1,None,node['calyxOuterPf'])
        current(prefix+'hairToCleftChargeFc',0,s);current(prefix+'calyxToCleftChargeFc',cy,s)
        current(prefix+'calyxToPerilymphChargeFc',cy,bath);current(prefix+'calyxToAxonChargeFc',cy,None)
        for ion,offset in [('Potassium',0),('Sodium',1)]:
            for source in ('hair','calyx'):species[2*i+offset,lookup[prefix+source+ion+'Attomoles']]+=1
    for edge in t['connections']:
        a=idx[edge['leftId']];b=idx[edge['rightId']];prefix='edge/'+edge['id']+'/'
        current(prefix+'cleftLeftToRightChargeFc',1+2*a,1+2*b);current(prefix+'calyxLeftToRightChargeFc',2+2*a,2+2*b)
        for ion,offset in [('potassium',0),('sodium',1)]:
            k=lookup[prefix+ion+'LeftToRightAttomoles'];species[2*a+offset,k]-=1;species[2*b+offset,k]+=1
    for face in t['faces']:
        i=idx[face['compartmentId']];prefix='apex/'+face['id']+'/'
        current(prefix+'chargeToBathFc',1+2*i,bath)
        for ion,offset in [('potassium',0),('sodium',1)]:species[2*i+offset,lookup[prefix+ion+'ToBathAttomoles']]-=1
    delta=values[:,:count]-values[0,:count];ledgers=values[:,count:]
    charge=delta@c.T-ledgers@m.T
    amounts=np.column_stack([delta[:,1+11*i+2+j]*node['volumeCubicMicrometers'] for i,node in enumerate(nodes) for j in (0,1)])
    ions=amounts-ledgers@species.T
    return dict(chargeFc=float(np.max(np.abs(charge))),ionAttomoles=float(np.max(np.abs(ions))))


def native(package,protocol,case,ticks):
    folder=package/'study'/f"{case['key']}-ticks{ticks}";initial=read(folder/'initial.json');final=read(folder/'final.json')
    equation=Equation(case['fixture'],protocol['referenceDecimalPrecision']);state_names,ledger_names=labels(case['fixture'])
    for s in (initial,final):
        assert s['stateLabels']==state_names and s['ledgerLabels']==ledger_names and s['failure'] is None
        assert s['sessionId']==protocol['sessionId'] and s['cellId']==case['cellId'] and s['version']==protocol['kernelVersion']
        assert s['settings']==dict(stepMs=ticks/protocol['ticksPerMs'],**protocol['nonlinearSettings'])
        assert s['epochMs']==0 and s['perilymphMv']==case['fixture']['perilymphMv']
        assert s['initialValues']==equation.initial[:equation.physical_count].tolist()
    assert initial['values']==initial['initialValues'] and not any(initial['ledgers']) and initial['completedSteps']==0
    assert not any(initial['additionResiduals']) and not any(initial['ledgerAdditionResiduals']) and initial['nonlinearEvaluations']==0
    assert initial['fingerprint']==final['fingerprint'] and initial['initialStateFingerprint']==final['initialStateFingerprint']
    assert len(initial['fingerprint'])==len(initial['initialStateFingerprint'])==64
    assert initial['mostRecentHeldInput'] is None
    segment=case['segments'][-1];step_count=case['totalTicks']//ticks
    assert final['completedSteps']==step_count
    assert final['mostRecentHeldInput']==dict(startingStep=step_count-1,requestedEndStep=step_count,
        bundleDisplacementNanometers=segment['bundleDisplacementNanometers'],axonCurrents=sorted(segment['axonCurrents'],key=lambda a:a['id']))
    expected_header=['tick','step','timeMs']+state_names+['stateError/'+n for n in state_names]+ledger_names+['ledgerError/'+n for n in ledger_names]
    diagnostics=['maximumChargeResidualFc','maximumIonResidualAttomoles','nonlinearEvaluations','maximumNewtonIterations','maximumAcceptedScaledResidual']
    sampled=[];sample_ticks=[];accepted=[];count=0;previous={k:0 for k in diagnostics}
    with (folder/'trajectory.csv').open(newline='') as stream:
        reader=csv.DictReader(stream);assert reader.fieldnames==expected_header+diagnostics
        for i,row in enumerate(reader):
            assert len(row)==len(reader.fieldnames) and None not in row
            assert int(row['tick'])==i*ticks and int(row['step'])==i and float(row['timeMs'])==i*(ticks/protocol['ticksPerMs'])
            state=np.array([float(row[n]) for n in state_names]);ledger=np.array([float(row[n]) for n in ledger_names])
            physical=np.concatenate((state,ledger));equation.validate_samples(physical[:,None])
            accepted.append(physical.tolist())
            assert all(np.isfinite(float(v)) for v in row.values())
            for name in diagnostics:
                value=float(row[name]);assert value>=previous[name];previous[name]=value
            assert previous['maximumNewtonIterations']<=protocol['nonlinearSettings']['maximumNewtonIterations'] and previous['maximumAcceptedScaledResidual']<=1
            if i==0:
                assert state.tolist()==initial['values'] and ledger.tolist()==initial['ledgers']
            if i*ticks%protocol['sampleTicks']==0:sample_ticks.append(i*ticks);sampled.append(physical.tolist())
            last=row;count+=1
    assert count==step_count+1 and int(last['tick'])==case['totalTicks']
    for names,field,prefix in [(state_names,'values',''),(state_names,'additionResiduals','stateError/'),
                               (ledger_names,'ledgers',''),(ledger_names,'ledgerAdditionResiduals','ledgerError/')]:
        assert [float(last[prefix+n]) for n in names]==final[field]
    for name in diagnostics:assert float(last[name])==final[name]
    assert final['timeMs']==float(last['timeMs'])
    return np.array(sample_ticks),np.array(sampled),dict(rows=count,final=final,
        independentAllEndpointLedgerResiduals=balance(case['fixture'],np.array(accepted)))


def restore(value,key=''):
    if isinstance(value,dict):return {k:restore(v,k) for k,v in value.items()}
    if isinstance(value,list):return [restore(v,key) for v in value]
    return value if key in ('id','leftId','rightId','compartmentId','reservoirId') else Decimal(value)


def reference_saved(folder,protocol,case,setting):
    eq=Equation(case['fixture'],protocol['referenceDecimalPrecision']);record=read(folder/'input.json')
    assert record==dict(case=case,setting=setting,labels=eq.labels,initial=eq.initial.tolist(),decimalPrecision=eq.precision,
        explicitJacobian=True,ledgerJacobianColumnsExactlyZero=True,internalTrialGatesMayLeaveProbabilityDomain=True,
        acceptedNodesAndSamplesRequirePhysicalDomain=True)
    status=read(folder/'results.json');assert status['completed'] and len(status['segments'])==4
    previous=eq.initial;start=0;maximum_ledger=dict(chargeFc=0.,ionAttomoles=0.)
    for i,segment in enumerate(case['segments']):
        meta=read(folder/f'segment-{i}.json');assert meta==status['segments'][i] and meta['success']
        assert meta['segment']==i and meta['startTick']==start and meta['endTick']==segment['endTick']
        with np.load(folder/f'segment-{i}-nodes.npz',allow_pickle=False) as saved:
            t=saved['timeMs'];y=saved['values'];eq.validate_samples(y)
            assert t.shape==(y.shape[1],) and len(t)==meta['acceptedNodes'] and np.isfinite(t).all() and (np.diff(t)>0).all()
            assert t[0]==start/protocol['ticksPerMs'] and t[-1]==segment['endTick']/protocol['ticksPerMs']
            assert np.array_equal(y[:,0],previous);previous=y[:,-1].copy()
            # Include the true common initial condition when auditing later
            # segments: ledgers are cumulative, not reset at each transition.
            ledger=balance(case['fixture'],np.vstack((eq.initial,y.T)))
            maximum_ledger={k:max(maximum_ledger[k],ledger[k]) for k in ledger}
        witnesses=read(folder/f'segment-{i}-precision.json');assert len(witnesses)==2
        for j,witness in enumerate(witnesses):
            assert witness['tick']==(start if j==0 else segment['endTick'])
            assert witness['state']==y[:,0 if j==0 else -1].tolist()
            a,b=[restore(v) for v in witness['references']]
            with localcontext() as context:
                context.prec=90;base.compare_values(a,b,Decimal('1e-40'),Decimal('1e-40'))
                base.compare_values(restore(witness['reference40']),b,Decimal('1e-25'),Decimal('1e-25'))
        start=segment['endTick']
    return maximum_ledger


def comparison(actual,expected,names,bounds):
    absolute=np.array([bounds['absolute'][group(name)] for name in names]);delta=np.abs(actual-expected)
    scaled=delta/(absolute+bounds['relative']*np.abs(expected))
    return dict(maximumAllowedFraction=float(scaled.max()),maximumAbsoluteByGroup={k:float(delta[:,[i for i,n in enumerate(names) if group(n)==k]].max()) for k in bounds['absolute']})


def analyse(package,protocol):
    typed=read(package/'study/input.json');assert typed=={k:protocol[k] for k in ('version','kernelVersion','sessionId','ticksPerMs','sampleTicks','stepTicks',
        'expectedCases','expectedNativeRuns','expectedReferences','cases','nonlinearSettings')}
    assert read(package/'study/results.json')==dict(completed=True,nativeRuns=21,failedRuns=[],independentCoupledTrajectoryQualified=False,
        stationaryInitializationQualified=False,physiologyQualified=False,avatarRecoveryAssessed=False)
    rows=[];all_ok=True
    for case in protocol['cases']:
        state_names,ledger_names=labels(case['fixture']);names=state_names+ledger_names
        expected_ticks=np.arange(0,case['totalTicks']+1,protocol['sampleTicks'],dtype=np.int64)
        references=[];reference_ledgers=[]
        for setting in protocol['referenceSettings']:
            folder=package/'reference-trajectories'/(case['key']+'-'+setting['key']);status=read(folder/'results.json')
            assert status['completed'] and status['samples']==len(expected_ticks) and status['allAcceptedNodesPhysicallyAdmissible']
            assert not status['candidateValuesUsedForIntegration']
            accepted_ledger=reference_saved(folder,protocol,case,setting)
            with np.load(folder/'samples.npz',allow_pickle=False) as data:
                assert np.array_equal(data['ticks'],expected_ticks);values=data['values'].copy()
            assert values.shape==(len(expected_ticks),len(names)) and np.isfinite(values).all()
            Equation(case['fixture'],protocol['referenceDecimalPrecision']).validate_samples(values.T)
            references.append(values);ledger=balance(case['fixture'],values)
            reference_ledgers.append({k:max(ledger[k],accepted_ledger[k]) for k in ledger})
        reference_errors=[comparison(references[i],references[1],names,protocol['referenceComparison']) for i in (0,2)]
        reference_pass=all(r['maximumAllowedFraction']<=1 for r in reference_errors)
        reference_pass &= all(all(b[k]<=protocol['ledgerResidualBounds'][k] for k in b) for b in reference_ledgers)
        runs=[]
        for ticks in protocol['stepTicks']:
            grid,values,metadata=native(package,protocol,case,ticks);assert np.array_equal(grid,expected_ticks)
            error=comparison(values,references[1],names,protocol['candidateComparison']);ledger=balance(case['fixture'],values)
            for k,field in [('chargeFc','maximumChargeResidualFc'),('ionAttomoles','maximumIonResidualAttomoles')]:
                ledger[k]=max(ledger[k],metadata['final'][field],metadata['independentAllEndpointLedgerResiduals'][k])
            runs.append(dict(stepTicks=ticks,stepMs=ticks/protocol['ticksPerMs'],rows=metadata['rows'],error=error,ledger=ledger,
                fingerprint=metadata['final']['fingerprint'],nonlinearEvaluations=metadata['final']['nonlinearEvaluations']))
        ratios=[r['error']['maximumAllowedFraction'] for r in runs]
        refinement=all(b<=protocol['refinement']['maximumRemainingFraction']*a+protocol['refinement']['normalizedErrorFloor'] for a,b in zip(ratios,ratios[1:]))
        ledger_pass=all(all(r['ledger'][k]<=protocol['ledgerResidualBounds'][k] for k in protocol['ledgerResidualBounds']) for r in runs)
        passed=reference_pass and ratios[-1]<=1 and refinement and ledger_pass;all_ok &= passed
        rows.append(dict(key=case['key'],referenceCriteriaPassed=bool(reference_pass),referenceErrors=reference_errors,
            referenceLedgers=reference_ledgers,candidates=runs,finestCriteriaPassed=ratios[-1]<=1,
            sampledRefinementPassed=refinement,ledgerCriteriaPassed=ledger_pass,passed=bool(passed)))
    result=dict(completed=True,numericalTrajectoryCriteriaPassed=bool(all_ok),nativeRuns=21,references=21,cases=rows,
        originalBoundsChanged=False,stationaryInitializationQualified=False,spatialGeometryCalibrated=False,
        afferentFiringQualified=False,physiologyQualified=False,avatarRecoveryAssessed=False)
    with (package/'trajectory-analysis.json').open('x',encoding='utf-8') as stream:json.dump(result,stream,indent=2,allow_nan=False)
    print(json.dumps({k:result[k] for k in ('completed','numericalTrajectoryCriteriaPassed','nativeRuns','references')},indent=2),flush=True)
    if not all_ok:raise RuntimeError('Coupled trajectory criteria failed; retain the original bounds and all traces.')

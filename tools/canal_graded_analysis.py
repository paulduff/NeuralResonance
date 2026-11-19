"""Saved trajectory/clock/energy reconciliation; no model equations executed."""
import csv
from decimal import Decimal as D,localcontext
import gzip
import numpy as np
from coupled_canal_reference import read,source_matrices
from coupled_canal_analysis import ratio,decimal_agreement,vector
from canal_graded_protocol import widths,ticks,reference_ticks


def analyse(package):
    spec=read(package/'graded-input.json');clock=spec['frame']['clock'];fine=spec['referenceTicksPerInterval']
    grid=np.array(reference_ticks(),dtype=np.int64);count=spec['expectedReferenceSamples']
    ref_ticks=np.concatenate((np.array([0],dtype=np.int64),np.concatenate([j*fine+grid for j in range(199)])))
    assert spec['referenceEndpointTicks']==grid.tolist() and len(ref_ticks)==count
    assert read(package/'study/input.json')==spec
    results=read(package/'study/results.json');assert results['completed'] and len(results['runs'])==3
    controls=read(package/'reference-controls/controls.json');assert controls['completed'] and controls['passed']==controls['expected']==6
    assert [c['key'] for c in controls['cases']]==['zero','diagonal','nilpotent','damped-jordan','stiff-decay','constant-forcing']
    assert all(c['passed'] and all(D(v)<=D('1e-45') for v in c['absoluteErrors']) for c in controls['cases'])
    rs=read(package/'reference-trajectories/results.json');assert rs['completed'] and rs['samples']==count and rs['intervals']==199
    assert rs['ticksPerInterval']==fine and rs['endpointTicks']==grid.tolist()
    assert rs['heldSteps']==list(range(1,200)) and rs['initialAtStep']==1 and not rs['nativeValuesUsedForIntegration']
    with np.load(package/'reference-trajectories/samples.npz',allow_pickle=False) as f:
        assert np.array_equal(f['ticks'],ref_ticks);refs={k:f[k].copy() for k in ('decimal60','decimal90','scipy')}
    assert all(a.shape==(count,12) and np.isfinite(a).all() and np.array_equal(a[0],np.zeros(12)) for a in refs.values())
    precision=D(0)
    with localcontext() as context:
        context.prec=100
        with gzip.open(package/'reference-trajectories/decimal60.csv.gz','rt',encoding='utf-8') as a,gzip.open(package/'reference-trajectories/decimal90.csv.gz','rt',encoding='utf-8') as b:
            for i,(low,high) in enumerate(zip(csv.reader(a),csv.reader(b),strict=True)):
                assert len(low)==len(high)==13 and int(low[0])==int(high[0])==int(ref_ticks[i])
                assert list(map(float,low[1:]))==refs['decimal60'][i].tolist() and list(map(float,high[1:]))==refs['decimal90'][i].tolist()
                precision=max(precision,decimal_agreement(low[1:],high[1:],D(spec['matrixPrecisionAbsolute']),D(spec['matrixPrecisionRelative'])))
        assert i+1==count
        operators=read(package/'reference-trajectories/operators.json')
        gaps=sorted(set(np.diff(np.concatenate(([0],grid))).tolist()))
        assert [(r['precision'],r['gapTicks']) for r in operators]==[(p,g) for p in (60,90) for g in gaps]
        operator_precision=D(0)
        for low,high in zip(operators[:len(gaps)],operators[len(gaps):],strict=True):
            assert D(low['stepSeconds'])==D.from_float(clock['physicsStepMs'])/D(1000)/D(fine)*D(low['gapTicks'])
            assert low['stepSeconds']==high['stepSeconds']
            operator_precision=max(operator_precision,decimal_agreement(sum(low['matrix'],[]),sum(high['matrix'],[]),D(spec['matrixPrecisionAbsolute']),D(spec['matrixPrecisionRelative'])))
    ref_error=ratio(refs['scipy'],refs['decimal90'],spec['referenceBounds'])
    # Retain the independent uniform reference as an additional anchor, not a replacement.
    anchor_ticks=np.arange(199*1024+1,dtype=np.int64)*(fine//1024)
    anchor_indices=np.searchsorted(ref_ticks,anchor_ticks);assert np.array_equal(ref_ticks[anchor_indices],anchor_ticks)
    with np.load(package/'baseline/reference-trajectories/samples.npz',allow_pickle=False) as saved:
        assert np.array_equal(saved['ticks'],np.arange(199*1024+1))
        anchors={name:ratio(refs[name][anchor_indices],saved[name],spec['referenceBounds']) for name in refs}
    reference_pass=precision<=1 and operator_precision<=1 and ref_error['maximum']<=1 and all(r['maximum']<=1 for r in anchors.values())
    catalog=read(package/'source/docs/reference-data/vestibular-ifediba-2007.json');m,c,k,f=[np.array(x,dtype=float) for x in source_matrices(catalog)]
    params=read(package/'study/parameters.json')
    for name,a in zip(('massKgPerMeter4','dampingPascalSecondsPerMeter3','stiffnessPascalPerMeter3','pressurePascalPerRadianPerSecondSquared'),(m,c,k,f)):
        assert params[name]==a.flatten().tolist()
    original=[r for r in spec['frame']['records'] if r['sensorId']=='centre'];assert len(original)==201
    angular=read(package/'baseline/baseline/reference-90.json')['rows'];reports=[]
    for refinement,run in zip(spec['refinements'],results['runs'],strict=True):
        schedule=widths(refinement);n=len(schedule);interval_ticks=65536*refinement
        folder=package/'study'/('r'+str(refinement));initial=read(folder/'initial.json');final=read(folder/'final.json')
        assert final==run and final['completed'] and final['error'] is None and final['receipts']==201 and final['intervals']==199 and final['steps']==199*n
        assert initial['refinement']==final['refinement']==refinement and initial['clock']==clock and not initial['initial']['faulted']
        declared=initial['schedule'];assert declared['ticksPerInterval']==interval_ticks and declared['count']==n and declared['stepWidthsTicks']==schedule
        assert declared['fingerprint'] and initial['initial']['mechanics']['version']==spec['ownerVersion']
        assert initial['initial']['mechanics']['completedThroughStep'] is None and vector(initial['initial']['mechanics']['point'])==spec['initial']
        inputs=[];endpoint_states=[];endpoint_records=[]
        for j,row in enumerate(original):
            endpoint=read(folder/f'endpoint-{j:03}.json');inp=endpoint['input'];mech=endpoint['mechanics'];endpoint_records.append(mech)
            assert endpoint['sourceFile']==row['sourceFile'] and endpoint['sourceSha256']==row['sourceSha256'] and inp['skull']==row['receipt']
            assert mech['fingerprint']==initial['fingerprint'] and mech['integratedIntervals']==max(0,j-1) and mech['integratedSubsteps']==max(0,j-1)*n
            assert mech['completedThroughStep']==(None if j==0 else j)
            if j<2:
                assert endpoint['completedInterval'] is None and not endpoint['acknowledged'] and vector(mech['point'])==spec['initial']
            else:
                interval=endpoint['completedInterval'];assert endpoint['acknowledged'] and interval['heldStep']==j-1 and interval['closingStep']==j and interval['substeps']==n
                assert interval['fingerprint']
            if j:
                assert inp['differenceFromStep']==j-1
                values=[inp['projection']['components'][name] for name in ('hc','ac','pc')]
                expected=angular[3*j]['projection']['prime']
                assert all(abs(a-float(b))<=1e-9+1e-12*abs(float(b)) for a,b in zip(values,expected,strict=True))
                inputs.append(values);endpoint_states.append(vector(mech['point']))
                assert mech['physicalTimeMs']==clock['physicalEpochMs']+j*clock['physicsStepMs'] and mech['neuralTimeMs']==clock['neuralEpochMs']+j*clock['physicsStepMs']
        with gzip.open(folder/'trajectory.csv.gz','rt',encoding='utf-8') as stream:
            header=stream.readline().strip().split(',');rows=np.loadtxt(stream,delimiter=',',ndmin=2)
        assert header==['index','physicalMs','neuralMs','intervalStep']+[f'state{i}' for i in range(12)]+[f'stage1_{i}' for i in range(18)]+[f'stage2_{i}' for i in range(18)]+['energyBefore','energyAfter','inputWork','physicalDamping','numericalDamping','energyResidual','backwardError']
        assert rows.shape==(199*n,59) and np.isfinite(rows).all()
        indices=np.concatenate([j*interval_ticks+np.array(ticks(refinement),dtype=np.int64) for j in range(1,200)])
        assert np.array_equal(rows[:,0],indices) and np.array_equal(rows[:,3],np.repeat(np.arange(2,201),n))
        elapsed=indices*(clock['physicsStepMs']/interval_ticks)
        assert np.array_equal(rows[:,1],clock['physicalEpochMs']+elapsed) and np.array_equal(rows[:,2],clock['neuralEpochMs']+elapsed)
        states=rows[:,4:16];assert np.array_equal(states[n-1::n],np.array(endpoint_states[1:]))
        assert vector(final['state']['mechanics']['point'])==states[-1].tolist() and final['state']['pendingInterval'] is None and not final['state']['faulted']
        before=np.vstack((np.array(spec['initial']),states[:-1]));q0,v0=before[:,:6],before[:,6:]
        q1,v1,a1=rows[:,16:22],rows[:,22:28],rows[:,28:34];q2,v2,a2=rows[:,34:40],rows[:,40:46],rows[:,46:52]
        assert np.array_equal(states,np.column_stack((q2,v2)))
        force=np.repeat(np.array(inputs[:-1])@f.T,n,axis=0);dt=((clock['physicsStepMs']/1000)/interval_ticks)*np.tile(schedule,199)
        quadratic=lambda x,a:np.einsum('ni,ij,nj->n',x,a,x)
        e0=.5*(quadratic(v0,m)+quadratic(q0,k));e2=.5*(quadratic(v2,m)+quadratic(q2,k))
        work=dt*np.sum(force*(.75*v1+.25*v2),axis=1);physical=dt*(.75*quadratic(v1,c)+.25*quadratic(v2,c))
        numerical=dt*dt/32*(quadratic(v1-v2,k)+quadratic(a1-a2,m));residual=e0+work-physical-numerical-e2
        scale=spec['energyAbsoluteJoules']+spec['energyRelative']*(abs(e0)+abs(e2)+abs(work)+abs(physical)+abs(numerical))
        energy=float(np.max(abs(np.column_stack((e0,e2,work,physical,numerical,residual))-rows[:,52:58])/scale[:,None]))
        identity=float(np.max(abs(residual)/scale));stage_error=0.
        for q,v,a in ((q1,v1,a1),(q2,v2,a2)):
            total=a@m.T+v@c.T+q@k.T-force;denom=abs(a)@abs(m.T)+abs(v)@abs(c.T)+abs(q)@abs(k.T)+abs(force)
            assert (total[denom==0]==0).all();stage_error=max(stage_error,float(np.max(np.divide(abs(total),denom,out=np.zeros_like(total),where=denom!=0))))
        collocation_error=0.
        for actual,start,x1,x2,w1,w2 in ((q1,q0,v1,v2,5/12,-1/12),(q2,q0,v1,v2,.75,.25),
                (v1,v0,a1,a2,5/12,-1/12),(v2,v0,a1,a2,.75,.25)):
            first=dt[:,None]*w1*x1;second=dt[:,None]*w2*x2
            residual=actual-start-first-second;scale=abs(actual)+abs(start)+abs(first)+abs(second)
            assert (residual[scale==0]==0).all()
            collocation_error=max(collocation_error,float(np.max(np.divide(abs(residual),scale,out=np.zeros_like(residual),where=scale!=0))))
        assert (rows[:,[52,53,55,56,58]]>=0).all() and (rows[:,58]<=1e-12).all()
        # Independently sum saved substep work/losses at each physical endpoint.
        sums=np.cumsum(rows[:,54:57],axis=0)[n-1::n];saved=np.array([[r[name] for name in ('inputWorkJoules','physicalDampingJoules','numericalDampingJoules')] for r in endpoint_records[2:]])
        cumulative_scale=spec['energyAbsoluteJoules']+spec['energyRelative']*(abs(sums)+np.cumsum(abs(rows[:,54:57]),axis=0)[n-1::n])
        cumulative=float(np.max(abs(sums-saved)/cumulative_scale))
        endpoint_energy=np.array([r['energyJoules'] for r in endpoint_records[2:]])
        assert np.array_equal(endpoint_energy,rows[n-1::n,53]) and all(r['initialEnergyJoules']==0 for r in endpoint_records)
        endpoint_residual=saved[:,0]-saved[:,1]-saved[:,2]-endpoint_energy
        endpoint_scale=spec['energyAbsoluteJoules']+spec['energyRelative']*(np.sum(abs(saved),axis=1)+abs(endpoint_energy))
        recorded_residual=np.array([r['energyIdentityResidualJoules'] for r in endpoint_records[2:]])
        cumulative_identity=float(max(np.max(abs(endpoint_residual)/endpoint_scale),np.max(abs(recorded_residual-endpoint_residual)/endpoint_scale)))
        target_ticks=(indices-interval_ticks)*(4//refinement);target_indices=np.searchsorted(ref_ticks,target_ticks)
        assert np.array_equal(ref_ticks[target_indices],target_ticks)
        target=refs['decimal90'][target_indices];error=ratio(states,target,spec['candidateBounds'])
        accounting=energy<=1 and identity<=1 and stage_error<=5e-10 and collocation_error<=5e-10 and cumulative<=1 and cumulative_identity<=1
        reports.append(dict(refinement=refinement,substepsPerInterval=n,ticksPerInterval=interval_ticks,rows=len(rows),error=error,energyRecordMaximumFraction=energy,energyIdentityMaximumFraction=identity,
            maximumPhysicalStageBackwardError=stage_error,maximumCollocationBackwardError=collocation_error,cumulativeLedgerMaximumFraction=cumulative,cumulativeEnergyIdentityMaximumFraction=cumulative_identity,
            accountingPassed=accounting,originalClockAndInputsPreserved=True))
    fractions=[r['error']['maximum'] for r in reports]
    refined=all(b<=spec['refinementMaximumRemainingFraction']*a+spec['refinementNormalizedFloor'] for a,b in zip(fractions,fractions[1:]))
    passed=reference_pass and all(r['accountingPassed'] for r in reports) and fractions[-1]<=1 and refined
    return dict(completed=True,numericalCriteriaPassed=bool(passed),referenceCriteriaPassed=bool(reference_pass),referencePrecisionMaximumFraction=str(precision),
        operatorPrecisionMaximumFraction=str(operator_precision),referenceComparison=ref_error,originalUniformAnchorComparisons=anchors,sampledRefinementPassed=refined,runs=reports,
        originalClockAndInputsPreserved=True,allNativeEndpointsCompared=True,nativeSteps=sum(r['rows'] for r in reports),referenceSamples=count,
        continuousMotionAccuracyQualified=False,anatomicalCalibrationQualified=False,afferentFiringQualified=False,avatarBalanceAssessed=False)

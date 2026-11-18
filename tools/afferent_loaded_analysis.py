"""Saved loaded-rest trajectories; no model or eigen solve."""
from decimal import localcontext
import numpy as np
from afferent_trajectory_analysis import native,balance,RESIDUALS
from afferent_loaded_reference import Equation,input_record
from afferent_trajectory_protocol import labels,group
from afferent_numerical_protocol import protocol as derivative_protocol
from afferent_numerical_analysis import read,agreement,joint_conservation
from cleft_rest_analysis import comparison as record_comparison
from cleft_trajectory_analysis import comparison

def reference_saved(folder,protocol,case,setting,source):
    eq=Equation(case['fixture'],source,protocol['referenceDecimalPrecision'])
    assert read(folder/'input.json')==input_record(eq,case,setting,protocol)
    status=read(folder/'results.json');assert status['completed'] and len(status['segments'])==4
    assert status['allAcceptedNodesPhysicallyAdmissible'] and status['restInitialized']
    assert status['savedNativeStationaryInitialStateUsed']
    for k in ('nativeTrajectoryValuesUsedForIntegration','stationaryInitializationQualified','afferentFiringQualified','physiologyQualified','avatarRecoveryAssessed'):assert not status[k]
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
        'stepTicks','expectedCases','expectedNativeRuns','expectedReferences','cases','nonlinearSettings','restInitialized')}
    assert read(package/'study/results.json')==dict(completed=True,nativeRuns=12,failedRuns=[],restInitialized=True,
        independentCoupledTrajectoryQualified=False,stationaryInitializationQualified=False,physiologyQualified=False,avatarRecoveryAssessed=False)
    assert read(package/'reference-trajectories/results.json')==dict(completed=True,attempted=12,failedRuns=[],restInitialized=True)
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
    return dict(completed=True,numericalTrajectoryCriteriaPassed=bool(all_ok),nativeRuns=12,references=12,cases=rows,initialDerivativeChecks=initial,
        restInitialized=True,originalBoundsChanged=False,stationaryInitializationQualified=False,spatialGeometryCalibrated=False,
        afferentFiringQualified=False,physiologyQualified=False,avatarRecoveryAssessed=False)


def observations(package,protocol,source):
    """Report finite-time behavior separately from agreement and physiology."""
    quiet=protocol['cases'][0];assert quiet['key']=='quiet'
    states,_=labels(quiet['fixture']);eq=Equation(quiet['fixture'],source,protocol['referenceDecimalPrecision'])
    root=eq.initial[:len(states)]
    limits=protocol['restObservation']['quietDriftBounds']
    def distances(values):
        return {g:float(np.max(np.abs(values[:,[i for i,n in enumerate(states) if group(n)==g]]-
            root[[i for i,n in enumerate(states) if group(n)==g]]))) for g in limits}
    quiet_rows=[];responses=[]
    for case in protocol['cases']:
        for ticks in protocol['stepTicks'] if case['key']=='quiet' else protocol['stepTicks'][-1:]:
            _,values,_=native(package,protocol,case,ticks,source)
            peak=distances(values[:,:len(states)]);final=distances(values[-1:,:len(states)])
            row=dict(case=case['key'],method='native',stepTicks=ticks,maximumDistanceFromQuietRoot=peak,finalDistanceFromQuietRoot=final)
            responses.append(row)
            if case['key']=='quiet':quiet_rows.append(dict(**row,passed=all(peak[g]<=limits[g] for g in limits)))
        for setting in protocol['referenceSettings']:
            _,values,_=reference_saved(package/'reference-trajectories'/(case['key']+'-'+setting['key']),protocol,case,setting,source)
            peak=distances(values[:,:len(states)]);final=distances(values[-1:,:len(states)])
            row=dict(case=case['key'],method=setting['key'],maximumDistanceFromQuietRoot=peak,finalDistanceFromQuietRoot=final)
            responses.append(row)
            if case['key']=='quiet':quiet_rows.append(dict(**row,passed=all(peak[g]<=limits[g] for g in limits)))
    return dict(completed=True,durationMs=4.,quietDriftBounds=limits,quietRuns=quiet_rows,
        quietDriftPassed=all(r['passed'] for r in quiet_rows),responses=responses,
        localStabilityReusedForEndpointHold=False,nonlinearStabilityProven=False,
        physiologicalRestQualified=False,afferentFiringQualified=False,avatarRecoveryAssessed=False)

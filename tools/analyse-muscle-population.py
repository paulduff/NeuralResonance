"""Independent complete slow-population comparison, executed in PowerShell only."""
import gzip
import importlib.util
import json
import os
from pathlib import Path
import struct
import traceback

for name in ('OMP_NUM_THREADS','OPENBLAS_NUM_THREADS','MKL_NUM_THREADS','NUMEXPR_NUM_THREADS'): os.environ[name]='1'
import numpy as np


def module(name,file):
    spec=importlib.util.spec_from_file_location(name,Path(__file__).with_name(file)); value=importlib.util.module_from_spec(spec)
    spec.loader.exec_module(value); return value


workflow=module('population_protocol','muscle_population_dynamics.py')
reference=module('population_equations','population_muscle_reference.py')
read,write=workflow.read,workflow.write


def controls(package):
    spec=read(package/'population-dynamics-input.json'); protocols=[]
    for side,case in zip(('left','right'),spec['cases'][:2],strict=True):
        protocol,_=workflow.reference_input(spec,case)
        protocol.update(side=side,initialMusculotendonLengthMeters=case['initialMusculotendonLengthMeters']); protocols.append(protocol)
    report=reference.controls(protocols); write(package/'population-reference-controls.json',report)
    if not report['passed']: raise RuntimeError('Full population reference controls failed; retain original bounds.')
    print('108 scalar/vector field and six bounded-Jacobian controls passed. No trajectories executed by this stage.',flush=True)


def solve(package):
    spec=read(package/'population-dynamics-input.json'); control=read(package/'population-reference-controls.json')
    assert control['passed'] and len(control['fieldCases'])==108 and len(control['jacobianCases'])==6
    output=package/'population-reference'; output.mkdir(); results=[]
    grid=np.arange(801)*spec['sampleMs']/1000
    for case in spec['cases']:
        protocol,scenario=workflow.reference_input(spec,case)
        for settings in spec['referenceSettings']:
            key=case['key']+'-'+settings['key']; print('FULL POPULATION REFERENCE '+key,flush=True)
            ref=reference.PopulationReference(protocol,scenario)
            try:
                states,forces,stats=ref.solve(settings,grid)
                np.savez_compressed(output/(key+'.npz'),timeSeconds=grid,state=states,forces=forces)
                record=dict(key=key,completed=True,**stats)
            except Exception:
                partial=ref.reference_partial
                np.savez_compressed(output/(key+'-partial.npz'),timeSeconds=grid,partialSamples=partial['samples'],
                    internalTimes=partial['internalTimes'],internalStates=partial['internalStates'])
                record=dict(key=key,completed=False,completedBoundarySeconds=partial['completedBoundarySeconds'],error=traceback.format_exc())
                print(record['error'],flush=True)
            write(output/(key+'-status.json'),record); results.append(record)
    write(output/'execution.json',dict(completed=True,references=results,expectedReferences=24))
    if len(results)!=24 or not all(r['completed'] for r in results):
        raise RuntimeError('Population reference incomplete; every successful trace and failure retained.')


def errors(a,b,af,bf):
    d=np.max(abs(a-b),axis=0); f=np.max(abs(af-bf),axis=0); unit=d[3:-2].reshape(147,6)
    return dict(fiberLengthMeters=float(d[0]),fiberVelocityMetersPerSecond=float(d[1]),yieldFraction=float(d[2]),
        calcium=float(unit[:,:2].max()),activation=float(unit[:,2].max()),fluxIntegral=float(unit[:,3:].max()),
        forceNewtons=float(f[:6].max()),accelerationMetersPerSecondSquared=float(f[6]),
        impulseNewtonSeconds=float(d[-2]),workJoules=float(d[-1]))


def within(error,bounds):
    assert error.keys()==bounds.keys()
    return all(error[k]<=bounds[k] for k in error)


def load_native(folder,case,h,spec):
    declared=read(folder/'declared.json'); assert declared['scenario']==case and declared['stepMs']==h
    assert declared['prescribedArrivals'] and not declared['neuronalModelExecuted'] and not declared['liveBodyAccessed']
    clock=declared['clock']; assert clock['bodyEpochMs']==case['initialTimeMs'] and clock['neuralEpochMs']==0
    units=declared['definitions']; assert len(units)==147
    contacts=[u['contact'] for u in units]
    for key in ('motorUnitId','contactId','sourceNeuronId','projectionId'): assert len({c[key] for c in contacts})==147
    assert [c['motorUnitId'] for c in contacts]==['unit-'+str(i+1).zfill(3) for i in range(147)]
    for i,(unit,row) in enumerate(zip(units,spec['parameterRows'],strict=True)):
        assert row['row']==i and row['sourceRow']==i+1
        assert all(unit[k]==row[k] for k in ('calcium','lengthGainAboveOptimal','lengthGainBelowOptimal'))
    arrivals=[json.loads(line) for line in (folder/'arrivals.jsonl').read_text().splitlines()]
    assert arrivals==declared['arrivals'] and len(arrivals)==len(case['events'])
    ordinals=[0]*147; messages=set()
    for event,arrival in zip(case['events'],arrivals,strict=True):
        i=event['unitIndex']; contact=contacts[i]; ordinals[i]+=1
        assert all(arrival[k]==contact[k] for k in ('motorUnitId','contactId','sourceNeuronId','projectionId','sourceInstanceKey'))
        assert arrival['sessionId']==clock['sessionId'] and arrival['ordinal']==ordinals[i]
        assert arrival['firedAtNeuralMs']==event['offsetMs'] and contact['conductionDelayMs']==0
        assert arrival['arrivalAtBodyMs']==clock['bodyEpochMs']+event['offsetMs']
        assert arrival['messageId'] not in messages; messages.add(arrival['messageId'])
    fmt=read(folder/'trace-format.json')
    core=['timeMs','totalLengthMeters','fiberLengthMeters','fiberVelocityMetersPerSecond','yieldFraction',
        'activeFiberForceNewtons','passiveFiberForceNewtons','tendonAxialForceNewtons','projectedTendonForceNewtons',
        'projectedFiberForceNewtons','geometricForceNewtons','accelerationMetersPerSecondSquared',
        'impulseNewtonSeconds','workJoules','momentumLedger','kineticLedger']
    unit_names=['releaseDrive','freeCalcium','boundCalcium','activation','released','recovered','netBound','calciumLedger','arrivals']
    columns=core+[f'u{i}_{k}' for i in range(147) for k in unit_names]
    assert fmt==dict(version='little-endian-f64-muscle-population-v1',magic='NRMUSC01',columns=columns,
        expectedSamples=801,initialTimeMs=case['initialTimeMs'],sampleMs=spec['sampleMs'])
    with gzip.open(folder/'trace.f64.gz','rb') as f: raw=f.read()
    assert raw[:8]==b'NRMUSC01' and struct.unpack('<iq',raw[8:20])==(1339,801)
    assert len(raw)==20+801*1339*8
    data=np.frombuffer(raw,offset=20,dtype='<f8').reshape(801,1339)
    assert np.isfinite(data).all()
    grid_ms=np.arange(801)*spec['sampleMs']; assert np.array_equal(data[:,0],case['initialTimeMs']+grid_ms)
    assert data[0,1]==case['initialMusculotendonLengthMeters'] and data[0,2]==case['initialFiberLengthMeters']
    assert data[0,3]==0 and data[0,4]==1 and np.all(data[0,16:]==0)
    assert np.all(data[0,12:16]==0)
    final=read(folder/'final.json'); assert not final['faulted'] and len(final['units'])==147
    assert final['timeMs']==data[-1,0] and final['fiberLengthMeters']==data[-1,2] and final['fiberVelocityMetersPerSecond']==data[-1,3]
    assert final['boundary']==case['boundary'] and final['musculotendonLengthMeters']==data[-1,1] and final['yield']==data[-1,4]
    assert np.array_equal(data[-1,5:12],[final['forces'][k] for k in core[5:12]])
    assert np.array_equal(data[-1,12:16],[final[k] for k in ('integratedNetImpulseNewtonSeconds',
        'integratedNetFiberWorkJoules','maximumMomentumLedgerError','maximumKineticLedgerError')])
    for i,unit in enumerate(final['units']):
        assert unit['motorUnitId']==contacts[i]['motorUnitId'] and unit['arrivals']==ordinals[i]
        names=['releaseDrive','freeCalcium','boundCalcium','activation','releasedCalcium','recoveredCalcium',
               'netBoundCalcium','maximumCalciumLedgerError','arrivals']
        assert np.array_equal(data[-1,16+9*i:25+9*i],[unit[k] for k in names])
    return data


def compare(package):
    spec=read(package/'population-dynamics-input.json'); native=read(package/'population-dynamics/result.json')
    assert read(package/'population-dynamics/input.json')==spec
    assert native['completed'] and native['successfulCases']==24 and len(native['cases'])==24
    assert all(not native[k] for k in ('neuronalSourceExecuted','nativeUnityExecuted','physiologyQualified','avatarStandingQualified'))
    assert {r['key'] for r in native['cases']}=={c['key']+'-h'+str(h) for c in spec['cases'] for h in spec['stepsMs']}
    execution=read(package/'population-reference/execution.json')
    assert execution['completed'] and execution['expectedReferences']==24 and len(execution['references'])==24
    assert {r['key'] for r in execution['references']}=={c['key']+'-'+s['key'] for c in spec['cases'] for s in spec['referenceSettings']}
    control=read(package/'population-reference-controls.json'); assert control['completed'] and control['passed']
    grid_ms=np.arange(801)*spec['sampleMs']; grid=grid_ms/1000; records=[]
    for case in spec['cases']:
        protocol,scenario=workflow.reference_input(spec,case); ref=reference.PopulationReference(protocol,scenario)
        refs={}; domain=True
        for settings in spec['referenceSettings']:
            key=case['key']+'-'+settings['key']; folder=package/'population-reference'
            status=read(folder/(key+'-status.json'))
            assert status==next(r for r in execution['references'] if r['key']==key) and status['completed']
            with np.load(folder/(key+'.npz'),allow_pickle=False) as data:
                assert np.array_equal(data['timeSeconds'],grid)
                state,forces=data['state'].copy(),data['forces'].copy()
            assert state.shape==(801,887) and forces.shape==(801,7) and np.isfinite(state).all() and np.isfinite(forces).all()
            domain &= status['acceptedInternalDomainPassed'] and status['sampleDomainPassed'] and ref.domain(state)
            assert np.array_equal(state[0],ref.initial())
            refs[settings['key']]=(state,forces)
        contrasts=[]
        for a,b in (('dop853','dop853-tight'),('dop853-tight','radau')):
            error=errors(refs[a][0],refs[b][0],refs[a][1],refs[b][1])
            contrasts.append(dict(left=a,right=b,errors=error,passed=within(error,spec['referenceBounds'])))
        candidates=[]
        for h in spec['stepsMs']:
            key=case['key']+'-h'+str(h); row=next(r for r in native['cases'] if r['key']==key)
            assert row['completed'] and row['delivered']==len(case['events']) and row['samples']==801
            data=load_native(package/'population-dynamics'/key,case,h,spec)
            assert np.max(abs(data[:,1]-np.array([ref.length(t) for t in grid])))<=1e-15
            state=np.concatenate([data[:,2:5]]+[data[:,17+9*i:23+9*i] for i in range(147)]+[data[:,12:14]],axis=1)
            force=data[:,5:12]; wave=ledger=0.
            for i in range(147):
                j=16+9*i
                wave=max(wave,float(np.max(abs(data[:,j]-np.array([ref.release(t,i) for t in grid])))))
                ledger=max(ledger,float(np.max(data[:,j+7])),float(np.max(abs(data[:,j+1]-data[:,j+4]+data[:,j+5]+data[:,j+6]))),
                    float(np.max(abs(data[:,j+2]-data[:,j+6]))))
                times=[e['offsetMs'] for e in case['events'] if e['unitIndex']==i]
                assert np.array_equal(data[:,j+8],np.searchsorted(times,grid_ms,side='right'))
            mass=case['geometry']['fiberMassKg']
            momentum=max(float(np.max(data[:,14])),float(np.max(abs(mass*state[:,1]-state[:,-2]))))
            kinetic=max(float(np.max(data[:,15])),float(np.max(abs(.5*mass*state[:,1]**2-state[:,-1]))))
            comparisons={k:errors(state,s,force,f) for k,(s,f) in refs.items() if k!='dop853'}
            accounting=wave<=spec['releaseWaveformAbsolute'] and ledger<=spec['calciumLedgerAbsolute'] and momentum<=spec['momentumLedgerNewtonSeconds']
            candidates.append(dict(stepMs=h,errorsAgainst=comparisons,physicalDomainPassed=ref.domain(state,0),
                releaseWaveformError=wave,calciumLedgerError=ledger,momentumLedgerError=momentum,kineticLedgerError=kinetic,
                accountingPassed=accounting,peakActiveForceNewtons=float(force[:,0].max()),
                peakTendonForceNewtons=float(force[:,2].max()),minimumNormalizedFiberLength=float(state[:,0].min()/case['geometry']['optimalFiberLengthMeters'])))
        finest=candidates[-1]; refinement={}
        for key in ('dop853-tight','radau'):
            coarse=candidates[0]['errorsAgainst'][key]; fine=finest['errorsAgainst'][key]
            refinement[key]=all(fine[k]<=spec['refinementFraction']*coarse[k]+spec['refinementFloorFraction']*spec['candidateBounds'][k] for k in fine)
        passed=bool(domain and all(c['passed'] for c in contrasts) and all(refinement.values()) and
            all(c['physicalDomainPassed'] and c['accountingPassed'] for c in candidates) and
            all(within(e,spec['candidateBounds']) for e in finest['errorsAgainst'].values()) and finest['kineticLedgerError']<=spec['finestKineticLedgerJoules'])
        records.append(dict(case=case['key'],referenceDomainPassed=bool(domain),referenceContrasts=contrasts,
            candidates=candidates,refinementPassed=refinement,passed=passed))
        print(case['key'],'numerical criteria',passed,flush=True)
    report=dict(completed=True,numericalCriteriaPassed=all(r['passed'] for r in records),cases=records,
        candidateConditions=24,independentReferences=24,unitsPerMuscle=147,originalBoundsChanged=False,
        prescribedArrivals=True,neuronalAuthorityQualified=False,nativeUnityExecuted=False,
        anatomicalCalibrationQualified=False,physiologicalCalibrationComplete=False,avatarStandingQualified=False)
    write(package/'population-dynamics-analysis.json',report)
    if not report['numericalCriteriaPassed']: raise RuntimeError('Active population numerical criteria failed; preserve all traces and original bounds.')
    print('24 complete 147-unit trajectories and 24 independent references reconciled. Physiology and standing remain separate.',flush=True)

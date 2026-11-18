"""Saved-data-only comparison of declared mechanical trajectories and energy."""
import csv
from decimal import Decimal, localcontext, ROUND_HALF_EVEN
import gzip
import json
import numpy as np
from coupled_canal_reference import read, source_matrices


def ratio(a, b, bounds):
    absolute = np.array([bounds['volumeAbsolute']] * 6 + [bounds['flowAbsolute']] * 6)
    scaled = np.abs(a-b) / (absolute + bounds['relative'] * np.maximum(np.abs(a), np.abs(b)))
    index = np.unravel_index(np.argmax(scaled), scaled.shape)
    return dict(maximum=float(scaled[index]), row=int(index[0]), component=int(index[1]),
                candidate=float(a[index]), reference=float(b[index]))


def grid(case, refinement):
    ticks = []; phases = []; steps = []; previous = 0
    for index, phase in enumerate(case['phases']):
        step = phase['baseStepTicks'] // refinement
        values = list(range(previous + step, phase['endTick'] + 1, step))
        ticks.extend(values); phases.extend([index]*len(values)); steps.extend([step]*len(values)); previous = phase['endTick']
    return np.array(ticks, dtype=np.int64), np.array(phases), np.array(steps)


def decimal_agreement(a, b, absolute, relative):
    maximum = Decimal(0)
    for x, y in zip(a, b, strict=True):
        x, y = Decimal(x), Decimal(y)
        if not x.is_finite() or not y.is_finite(): raise ArithmeticError('Nonfinite reference witness.')
        maximum = max(maximum, abs(x-y)/(absolute + relative*max(abs(x), abs(y))))
    return maximum


def reference(package, case, spec):
    folder = package/'reference-trajectories'/case['key']; status = read(folder/'results.json')
    assert status['completed'] and status['independentSourceDecimals'] and not status['nativeValuesUsedForIntegration']
    expected = np.r_[0, grid(case, 4)[0]]
    with np.load(folder/'samples.npz', allow_pickle=False) as archive:
        ticks = archive['ticks']; values = {key: archive[key].copy() for key in ('decimal60','decimal90','scipy')}
    assert np.array_equal(ticks, expected) and len(ticks) == status['samples'] and ticks[-1] == status['endTick'] == case['durationTicks']
    assert all(v.shape == (len(ticks),12) and np.isfinite(v).all() and v[0].tolist() == case['initial'] for v in values.values())
    maximum = Decimal(0)
    with localcontext() as context:
        context.prec = 100; context.rounding = ROUND_HALF_EVEN
        with gzip.open(folder/'decimal60.csv.gz','rt',encoding='utf-8',newline='') as sixty, gzip.open(folder/'decimal90.csv.gz','rt',encoding='utf-8',newline='') as ninety:
            for index,(a,b) in enumerate(zip(csv.reader(sixty),csv.reader(ninety),strict=True)):
                assert len(a) == len(b) == 13 and int(a[0]) == int(b[0]) == ticks[index]
                assert list(map(float,a[1:])) == values['decimal60'][index].tolist()
                assert list(map(float,b[1:])) == values['decimal90'][index].tolist()
                maximum = max(maximum, decimal_agreement(a[1:],b[1:],Decimal(spec['matrixPrecisionAbsolute']),Decimal(spec['matrixPrecisionRelative'])))
            assert index+1 == len(ticks)
    comparison = ratio(values['scipy'],values['decimal90'],case['referenceBounds'])
    return ticks,values['decimal90'],dict(samples=len(ticks),maximumDecimalPrecisionFraction=str(maximum),scipyComparison=comparison,
        passed=maximum<=1 and comparison['maximum']<=1)


def vector(point):
    names=('endolymphHC','endolymphAC','endolymphPC','cupulaHC','cupulaAC','cupulaPC')
    return [point[k][name] for k in ('volumeMeters3','flowMeters3PerSecond') for name in names]


def native(package, case, refinement, spec, matrices):
    folder=package/'study'/(case['key']+'-r'+str(refinement));initial=read(folder/'initial.json');final=read(folder/'final.json')
    assert initial['key']==final['key']==case['key']+'-r'+str(refinement) and initial['refinement']==final['refinement']==refinement
    assert final['completed'] and final['error'] is None and vector(initial['state'])==case['initial']
    assert initial['tick']==0 and initial['condition']==dict(key=case['key'],regime=case['regime'],conditionName=case['condition'],
        initial=case['initial'],phases=case['phases'],durationTicks=case['durationTicks'])
    expected_header=['tick','step','phase','stepTicks']+[f'state{i}' for i in range(12)]+[f'stage1_{i}' for i in range(18)]+[f'stage2_{i}' for i in range(18)]+[
        'energyBefore','energyAfter','inputWork','physicalDamping','numericalDamping','energyResidual','backwardError']
    with (folder/'trajectory.csv').open(encoding='utf-8') as stream:
        assert stream.readline().strip().split(',')==expected_header
        rows=np.loadtxt(stream,delimiter=',',ndmin=2)
    ticks,phases,steps=grid(case,refinement)
    assert rows.shape==(len(ticks),59) and np.isfinite(rows).all()
    assert np.array_equal(rows[:,0],ticks) and np.array_equal(rows[:,1],np.arange(1,len(ticks)+1))
    assert np.array_equal(rows[:,2],phases) and np.array_equal(rows[:,3],steps)
    assert final['tick']==ticks[-1]==case['durationTicks'] and final['steps']==len(ticks)
    states=rows[:,4:16];assert states[-1].tolist()==vector(final['state'])
    assert np.array_equal(states,rows[:,34:46])
    initial_solvers={r['stepTicks']:r for r in initial['solvers']}
    assert set(initial_solvers)==set(steps.tolist()) and all(initial_solvers[t]['stepSeconds']==t/spec['ticksPerSecond'] for t in initial_solvers)
    assert initial['fingerprint']==read(package/'study/parameters.json')['fingerprint']
    m,c,k,f=matrices;dt=steps/spec['ticksPerSecond'];drive=np.array([case['phases'][p]['input'] for p in phases]);force=drive@f.T
    before=np.vstack((np.array(case['initial']),states[:-1]));q0,v0=before[:,:6],before[:,6:]
    q1,v1,a1=rows[:,16:22],rows[:,22:28],rows[:,28:34]
    q2,v2,a2=rows[:,34:40],rows[:,40:46],rows[:,46:52]
    quadratic=lambda values,matrix:np.einsum('ni,ij,nj->n',values,matrix,values)
    e0=.5*(quadratic(v0,m)+quadratic(q0,k));e2=.5*(quadratic(v2,m)+quadratic(q2,k))
    work=dt*np.sum(force*(.75*v1+.25*v2),axis=1)
    physical=dt*(.75*quadratic(v1,c)+.25*quadratic(v2,c))
    numerical=dt*dt/32*(quadratic(v1-v2,k)+quadratic(a1-a2,m))
    residual=e0+work-physical-numerical-e2
    scale=spec['energyAbsoluteJoules']+spec['energyRelative']*(np.abs(e0)+np.abs(e2)+np.abs(work)+np.abs(physical)+np.abs(numerical))
    recomputed=np.column_stack((e0,e2,work,physical,numerical,residual))
    reported=rows[:,52:58]
    energy_error=float(np.max(np.abs(recomputed-reported)/scale[:,None]))
    identity_error=float(np.max(np.abs(residual)/scale))
    assert (rows[:,[52,53,55,56,58]]>=0).all() and (rows[:,58]<=1e-12).all()
    quiet=np.all(drive==0,axis=1)
    passive=bool(np.all(e2[quiet]<=e0[quiet]+scale[quiet]))
    stage_error=0.
    for q,v,a in ((q1,v1,a1),(q2,v2,a2)):
        total=a@m.T+v@c.T+q@k.T-force
        denominator=np.abs(a)@np.abs(m.T)+np.abs(v)@np.abs(c.T)+np.abs(q)@np.abs(k.T)+np.abs(force)
        assert (total[denominator==0]==0).all()
        fractions=np.divide(np.abs(total),denominator,out=np.zeros_like(total),where=denominator!=0)
        stage_error=max(stage_error,float(np.max(fractions)))
    return ticks,states,dict(rows=len(ticks),energyRecordMaximumFraction=energy_error,energyIdentityMaximumFraction=identity_error,
        maximumPhysicalStageBackwardError=stage_error,undrivenEnergyNonincreasing=passive,
        passed=energy_error<=1 and identity_error<=1 and stage_error<=5e-10 and passive)


def analyse(package,spec):
    capture=read(package/'study/results.json');reference_status=read(package/'reference-trajectories/results.json')
    assert capture['completed'] and len(capture['runs'])==45 and all(r['completed'] for r in capture['runs'])
    assert reference_status['completed'] and len(reference_status['cases'])==15 and all(r['completed'] for r in reference_status['cases'])
    assert {r['key'] for r in capture['runs']}=={c['key']+'-r'+str(r) for c in spec['cases'] for r in spec['refinements']}
    assert {r['key'] for r in reference_status['cases']}=={c['key'] for c in spec['cases']}
    assert read(package/'study/input.json')==spec
    catalog=read(package/'source/docs/reference-data/vestibular-ifediba-2007.json')
    matrices=[np.array(a,dtype=float) for a in source_matrices(catalog)]
    p=read(package/'study/parameters.json')
    for name,matrix in zip(('massKgPerMeter4','dampingPascalSecondsPerMeter3','stiffnessPascalPerMeter3','pressurePascalPerRadianPerSecondSquared'),matrices):
        assert p[name]==matrix.flatten().tolist()
    assert p['sourceSha256']==catalog['retainedHtmlSha256'].upper()
    operators=read(package/'reference-trajectories/operators.json');operator_precision=[]
    expected_steps={phase['baseStepTicks']//4 for case in spec['cases'] for phase in case['phases']}
    assert len(operators)==2*len(expected_steps) and {r['stepTicks'] for r in operators}==expected_steps
    with np.load(package/'reference-trajectories/scipy-operators.npz',allow_pickle=False) as saved:
        assert set(saved.files)=={str(step) for step in expected_steps}
        assert all(saved[key].shape==(15,15) and np.isfinite(saved[key]).all() for key in saved.files)
    with localcontext() as context:
        context.prec=100; context.rounding=ROUND_HALF_EVEN
        for step in sorted({r['stepTicks'] for r in operators}):
            rows=[r for r in operators if r['stepTicks']==step];assert [r['precision'] for r in rows]==[60,90]
            for row in rows:
                assert len(row['matrix'])==15 and all(len(values)==15 for values in row['matrix'])
                metadata=row['metadata'];assert metadata['precision']==row['precision'] and metadata['tailIsNotAGlobalRoundoffBound']
                assert Decimal(metadata['localSeriesTailBound'])<=Decimal(metadata['localSeriesGoal'])
            difference=decimal_agreement([x for row in rows[0]['matrix'] for x in row],[x for row in rows[1]['matrix'] for x in row],
                Decimal(spec['matrixPrecisionAbsolute']),Decimal(spec['matrixPrecisionRelative']))
            operator_precision.append(dict(stepTicks=step,maximumFraction=str(difference),passed=difference<=1))
    reports=[]
    for case in spec['cases']:
        ticks,values,ref=reference(package,case,spec);runs=[]
        for refinement in spec['refinements']:
            native_ticks,states,accounting=native(package,case,refinement,spec,matrices)
            indices=np.searchsorted(ticks,native_ticks);assert np.array_equal(ticks[indices],native_ticks)
            comparison=ratio(states,values[indices],case['candidateBounds'])
            runs.append(dict(refinement=refinement,comparison=comparison,accounting=accounting))
        errors=[r['comparison']['maximum'] for r in runs]
        refining=all(b<=spec['refinementMaximumRemainingFraction']*a+spec['refinementNormalizedFloor'] for a,b in zip(errors,errors[1:]))
        passed=ref['passed'] and errors[-1]<=1 and refining and all(r['accounting']['passed'] for r in runs)
        reports.append(dict(key=case['key'],reference=ref,candidates=runs,finestAccuracyPassed=errors[-1]<=1,refinementPassed=refining,passed=passed))
        print(case['key'],passed,errors,flush=True)
    return dict(completed=True,numericalCriteriaPassed=all(r['passed'] for r in reports) and all(r['passed'] for r in operator_precision),
        cases=reports,operatorPrecision=operator_precision,allNativeEndpointsCompared=True,sourceSpecimenOnly=True,
        sourceToSkullFrameQualified=False,localBundleMapQualified=False,physiologyQualified=False,avatarRecoveryAssessed=False)

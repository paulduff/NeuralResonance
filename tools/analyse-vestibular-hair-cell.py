"""Independent fixed-bath hair-cell equations; execute only in visible PowerShell.

No native implementation is imported. All accepted ODE nodes and charge states
are retained. The physical input is prescribed bundle displacement, not Avatar's
reduced angular-velocity filter. This does not produce afferent action potentials.
"""
import argparse
from datetime import datetime, timezone
import importlib.util
import json
import os
from pathlib import Path
import sys

for key in ('OMP_NUM_THREADS', 'OPENBLAS_NUM_THREADS', 'MKL_NUM_THREADS', 'NUMEXPR_NUM_THREADS'):
    os.environ[key] = '1'
sys.path.insert(0, str(Path(__file__).resolve().parent))
from vestibular_hair_cell_protocol import validate, COUNTS, PARAMETERS, BATH
import numpy as np
from scipy.integrate import solve_ivp

ROOT = Path(__file__).resolve().parents[1]
read = lambda p: json.loads(p.read_text(encoding='utf-8-sig'))
CURRENT_NAMES = ('metPa', 'potassiumLowPa', 'calciumPa', 'hcnPotassiumPa',
                 'hcnSodiumPa', 'pumpPotassiumPa', 'pumpSodiumPa')
CSV_NAMES = ('timeMs,bundleDisplacementNm,previousIntervalBundleDisplacementNm,voltageMv,kLow,ca,hcn1,'
             'metChargeFc,kLowChargeFc,calciumChargeFc,hcnKChargeFc,hcnNaChargeFc,pumpKChargeFc,pumpNaChargeFc,'
             'metPa,kLowPa,calciumPa,hcnKPa,hcnNaPa,pumpKPa,pumpNaPa').split(',')


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec); spec.loader.exec_module(result); return result


def save(path, value):
    with path.open('x', encoding='utf-8') as stream:
        json.dump(value, stream, indent=2, allow_nan=False); stream.write('\n')


def arrays(path, **values):
    with path.open('xb') as stream: np.savez_compressed(stream, **values)


def environment():
    return module('hair_cell_environment', ROOT / 'tools/golgi-reference-resume.py').environment()


def preflight(package):
    validate(read(package / 'hair-cell-input.json'))
    current = environment()
    assert current == read(package / 'preparation-environment.json'), 'Numerical dependencies changed before this study.'
    save(package / 'reference-preflight.json', dict(environment=current, modelsExecuted=False,
        testsExecuted=False, completedUtc=datetime.now(timezone.utc).isoformat()))


def postflight(package):
    current = environment(); unchanged = current == read(package / 'reference-preflight.json')['environment']
    save(package / 'reference-postflight.json', dict(environment=current, unchanged=unchanged))
    assert unchanged, 'Numerical dependencies changed during this study.'


def spec_for(package):
    spec = validate(read(package / 'hair-cell-input.json'))
    assert environment() == read(package / 'reference-preflight.json')['environment']
    return spec


def rates(v):
    # Hair-cell HCN1, not the disputed calyx HCN2 equation. Complex arguments
    # are retained for a Jacobian without finite-difference perturbation growth.
    inf = 1 / (1 + np.exp(np.array([-(v+80)/2.84, -(v+44)/5.8, (v+90)/6.8])))
    tau = np.array([10 + 429.7*np.exp(-.2826*(v+80)/2.84), .6,
        209.479 + (np.exp((v+80.646)/6.916)+np.exp((v+80.646)/14.881))/2551.988])
    return inf, tau


def constants(p, bath):
    cap = p['apicalCapacitancePf'] + p['specificCapacitancePfPerSquareMicrometer']*p['basolateralAreaSquareMicrometers']
    reversals = np.array([26*np.log(bath['potassiumOutsideMm']/bath['potassiumInsideMm']),
        26*np.log(bath['sodiumOutsideMm']/bath['sodiumInsideMm']),
        13*np.log(bath['calciumOutsideMm']/bath['calciumInsideMm'])])
    pump = (96.485 / 602000) * .1 * p['pumpDensityPerSquareMicrometer'] * p['cleftFacingAreaSquareMicrometers'] * \
        (bath['potassiumOutsideMm']/(bath['potassiumOutsideMm']+1.5))**2
    return cap, reversals, pump


def met_open(displacement):
    x = displacement*.001 + .2
    return 1 / ((1 + np.exp(-4.05*(x-.39))) * (1 + np.exp(-14.5*(x-.25))))


def currents(y, displacement, p, bath):
    _, (ek, ena, eca), pump = constants(p, bath)
    voltage = y[0] - bath['basolateralOutsidePotentialMv']
    return np.array([p['metMaximumNs']*met_open(displacement)*(y[0]-p['endolymphPotentialMv']-p['metReversalMv']),
        p['potassiumLowMaximumNs']*y[1]*(voltage-ek), p['calciumMaximumNs']*y[2]*(voltage-eca),
        p['hcn1MaximumNs']*.8*y[3]*(voltage-ek), p['hcn1MaximumNs']*.2*y[3]*(voltage-ena), -2*pump, 3*pump])


def derivative(displacement, p=PARAMETERS, bath=BATH):
    cap, _, _ = constants(p, bath)
    def rhs(_time, y):
        inf, tau = rates(y[0]-bath['basolateralOutsidePotentialMv'])
        ionic = currents(y, displacement, p, bath)
        return np.concatenate((np.atleast_1d(-np.sum(ionic)/cap), (inf-y[1:4])/tau, ionic))
    return rhs


def jacobian(rhs):
    def jac(time, y):
        result = np.zeros((11, 11))
        for column in range(4):
            z = np.asarray(y, dtype=complex).copy(); z[column] += 1e-20j
            result[:, column] = rhs(time, z).imag / 1e-20
        return result
    return jac


def initial(p, bath, voltage=-75.):
    return np.r_[voltage, rates(voltage-bath['basolateralOutsidePotentialMv'])[0], np.zeros(7)]


def ode(rhs, interval, state, setting, spec):
    options = dict(method=setting['method'], rtol=setting['relativeTolerance'],
        atol=spec['referenceAbsoluteTolerance'], max_step=setting['maxStepMs'], dense_output=True)
    if setting['method'] == 'Radau': options['jac'] = jacobian(rhs)
    return solve_ivp(rhs, interval, state, **options)


def controls(package):
    spec = spec_for(package); folder = package / 'reference-controls'; folder.mkdir(); rows = []
    bounds = spec['controlBounds']; cap = constants(PARAMETERS, BATH)[0]
    # Three algorithms/settings solve an analytically known passive RC cell.
    passive = dict(PARAMETERS, potassiumLowMaximumNs=0., calciumMaximumNs=0., hcn1MaximumNs=0., pumpDensityPerSquareMicrometer=0.)
    zero = dict(passive, metMaximumNs=0.)
    for setting in spec['referenceSettings']:
        for label, p in [('passive', passive), ('zero', zero)]:
            y0 = initial(p, BATH); rhs = derivative(300., p); sol = ode(rhs, (0., 2.), y0, setting, spec)
            arrays(folder / (label+'-'+setting['key']+'.npz'), times=sol.t, states=sol.y.T)
            assert sol.success and sol.t[-1] == 2. and np.isfinite(sol.y).all()
            if label == 'passive':
                conductance = p['metMaximumNs']*met_open(300.)
                reversal = p['endolymphPotentialMv']+p['metReversalMv']
                expected_v = reversal + (y0[0]-reversal)*np.exp(-sol.t*conductance/cap)
                expected_q = -cap*(expected_v-y0[0])
                ev = float(np.max(np.abs(sol.y[0]-expected_v))); eq = float(np.max(np.abs(sol.y[4]-expected_q)))
                assert np.array_equal(sol.y[5:], np.zeros_like(sol.y[5:]))
                rows.append(dict(key=label+'-'+setting['key'], voltageError=ev, chargeError=eq,
                    passed=ev <= bounds['passiveVoltageMv'] and eq <= bounds['passiveChargeFc']))
            else:
                error = float(np.max(np.abs(sol.y-y0[:, None])))
                rows.append(dict(key=label+'-'+setting['key'], maximumError=error, passed=error <= bounds['zeroDerivative']))
    for voltage in [-100., -75., -40.]:
        y = initial(PARAMETERS, BATH, voltage); y[1:4] = [.2, .3, .4]
        rhs = derivative(300.); actual = jacobian(rhs)(0., y); numerical = np.zeros((11,11))
        for j in range(11):
            e = np.zeros(11); e[j] = 1e-4
            numerical[:, j] = (-rhs(0,y+2*e)+8*rhs(0,y+e)-8*rhs(0,y-e)+rhs(0,y-2*e))/(12e-4)
        error = float(np.max(np.abs(actual-numerical)))
        arrays(folder / ('jacobian-'+str(voltage)+'.npz'), complexStep=actual, symmetricDifference=numerical, state=y)
        rows.append(dict(key='jacobian-'+str(voltage), maximumError=error, passed=error <= bounds['jacobian']))
        dy = rhs(0.,y); charge = abs(cap*dy[0]+sum(dy[4:])); stoich=abs(3*dy[9]+2*dy[10])
        rows.append(dict(key='instant-charge-'+str(voltage), chargeError=float(charge), pumpError=float(stoich),
            passed=bool(max(charge,stoich) <= bounds['instantaneousChargePa'])))
    assert len(rows) == spec['expectedReferenceControls'] == 12
    report = dict(completed=True, controls=rows, passed=all(r['passed'] for r in rows))
    save(package / 'controls.json', report)
    assert report['passed'], 'Independent reference controls failed; retain original bounds.'


def reference(package):
    spec = spec_for(package); assert read(package / 'controls.json')['passed']
    folder = package / 'reference'; folder.mkdir(); rows = []
    grid = np.arange(spec['expectedSamplesPerCapture'])*spec['samplePeriodMs']
    for case in spec['cases']:
        for setting in spec['referenceSettings']:
            key = case['key']+'-'+setting['key']; print('REFERENCE '+key, flush=True)
            y = initial(spec['parameters'], spec['bath'], spec['initialVoltageMv']); sampled = np.full((len(grid), 11), np.nan)
            sampled[0] = y; times = []; states = []; segments = []; stats = []; error = None
            try:
                for index, change in enumerate(case['changes']):
                    start = change['timeMs']; end = case['changes'][index+1]['timeMs'] if index+1 < len(case['changes']) else spec['durationMs']
                    rhs = derivative(change['displacementNanometers'], spec['parameters'], spec['bath'])
                    sol = ode(rhs, (start, end), y, setting, spec)
                    times.extend(sol.t); states.extend(sol.y.T); segments.extend([index]*len(sol.t))
                    stats.append(dict(index=index, startMs=start, endMs=end, displacementNanometers=change['displacementNanometers'],
                        success=bool(sol.success), message=sol.message, acceptedNodes=len(sol.t),
                        acceptedEndMs=float(sol.t[-1]), nfev=sol.nfev, njev=sol.njev, nlu=sol.nlu))
                    assert sol.success and sol.t[-1] == end and np.isfinite(sol.y).all()
                    mask = (grid > start) & (grid <= end); sampled[mask] = sol.sol(grid[mask]).T
                    # Preserve the accepted endpoint exactly, avoiding interpolation roundoff at joins.
                    sampled[grid == end] = sol.y[:, -1]; y = sol.y[:, -1].copy()
            except Exception as exception:
                error = repr(exception); print(key+': '+error, flush=True)
            arrays(folder / (key+'-accepted.npz'), times=np.asarray(times), states=np.asarray(states).reshape(-1,11), segments=np.asarray(segments))
            arrays(folder / (key+'.npz'), times=grid, states=sampled)
            row = dict(key=key, case=case['key'], setting=setting, completed=error is None,
                error=error, segments=stats, acceptedNodes=len(times))
            save(folder / (key+'.json'), row); rows.append(row)
    save(package / 'reference-results.json', dict(completed=all(r['completed'] for r in rows), cases=rows))
    assert len(rows) == spec['expectedReferences'] and all(r['completed'] for r in rows), 'Preserve failed reference prefixes.'


def compare_values(a, b, bounds):
    errors = np.max(np.abs(a-b), axis=0)
    values = dict(voltageMv=float(errors[0]), gates=float(max(errors[1:4])), chargeFc=float(max(errors[4:])))
    return dict(errors=values, bounds=bounds, passed=all(values[k] <= bounds[k] for k in bounds))


def physical_checks(states, spec):
    assert states.ndim == 2 and states.shape[1] == 11 and np.isfinite(states).all()
    cap = constants(spec['parameters'],spec['bath'])[0]
    charge = float(np.max(np.abs(cap*(states[:,0]-spec['initialVoltageMv'])+states[:,4:].sum(axis=1))))
    stoich = float(np.max(np.abs(3*states[:,9]+2*states[:,10])))
    low, high = float(states[:,1:4].min()), float(states[:,1:4].max())
    slack = spec['referenceGateDomainSlack']
    return dict(chargeResidualFc=charge, pumpStoichiometryResidualFc=stoich, gateMinimum=low, gateMaximum=high,
        passed=charge <= spec['chargeLedgerAbsoluteFc'] and stoich <= spec['chargeLedgerAbsoluteFc'] and low >= -slack and high <= 1+slack)


def channel_audit(package, spec):
    baths = [BATH, dict(BATH,potassiumOutsideMm=20.), dict(BATH,basolateralOutsidePotentialMv=9.)]
    rows = [json.loads(line) for line in (package / 'study/channel-arithmetic.jsonl').read_text().splitlines()]
    count = 0; maximum = 0.
    def near(actual, expected):
        nonlocal maximum
        a,b=np.asarray(actual),np.asarray(expected); assert a.shape==b.shape and np.isfinite(a).all()
        e=np.abs(a-b); maximum=max(maximum,float(np.max(e)))
        assert np.all(e <= spec['arithmeticAbsoluteTolerance']+spec['arithmeticRelativeTolerance']*np.abs(b))
    for bi,bath in enumerate(baths):
        for voltage in np.arange(121)*1.5-120:
            inf,tau=rates(voltage-bath['basolateralOutsidePotentialMv'])
            for x in [-1000.,-300.,0.,300.,1000.]:
                for gi,gates in enumerate([[0.,0.,0.],[1.,1.,1.],[.2,.3,.4],inf]):
                    row=rows[count]; assert row['index']==count and row['bathIndex']==bi and row['gatePreset']==gi
                    assert row['voltageMv']==voltage and row['displacementNanometers']==x
                    near(row['gates'],gates); near(row['metOpenProbability'],met_open(x))
                    for i,key in enumerate(['potassiumLow','calcium','hcn1']):
                        near([row['rates'][key]['equilibrium'],row['rates'][key]['tauMs']],[inf[i],tau[i]])
                    cap,revs,pump=constants(PARAMETERS,bath)
                    near([row['capacitancePf'],row['potassiumReversalMv'],row['sodiumReversalMv'],row['calciumReversalMv'],row['pumpTurnoverChargePa']],np.r_[cap,revs,pump])
                    expected=currents(np.r_[voltage,gates],x,PARAMETERS,bath)
                    near([row['currents'][key] for key in CURRENT_NAMES],expected)
                    near([row['currents']['totalOutwardPa'],row['currents']['basolateralPotassiumPa'],row['currents']['basolateralSodiumPa']],
                         [sum(expected),expected[1]+expected[3]+expected[5],expected[4]+expected[6]])
                    count+=1
    assert count==len(rows)==spec['expectedArithmeticRows']
    return dict(rows=count, maximumAbsoluteError=maximum, passed=True)


def compare(package):
    spec=spec_for(package); tests=module('hair_cell_tests',ROOT/'tools/receptor_membrane_gate.py').check_tests(package,COUNTS)
    controls_report=read(package/'controls.json'); assert controls_report['passed'] and len(controls_report['controls'])==12
    arithmetic=channel_audit(package,spec); native=read(package/'study/results.json'); ref_result=read(package/'reference-results.json')
    assert native['completed'] and ref_result['completed'] and len(native['cases'])==18 and len(ref_result['cases'])==18
    grid=np.arange(8001)*.125; references=[]; candidates=[]; refinement=[]; ledgers=[]; ids=set(); fingerprints=set(); nodes=0
    for case in spec['cases']:
        refs=[]
        for setting in spec['referenceSettings']:
            key=case['key']+'-'+setting['key']; meta=read(package/'reference'/(key+'.json'))
            assert meta in ref_result['cases'] and meta['completed'] and meta['case']==case['key'] and meta['setting']==setting
            with np.load(package/'reference'/(key+'.npz'),allow_pickle=False) as data:
                assert np.array_equal(data['times'],grid); y=data['states'].copy(); assert y.shape==(8001,11)
            with np.load(package/'reference'/(key+'-accepted.npz'),allow_pickle=False) as data:
                t,z,s=data['times'],data['states'],data['segments']
                assert len(t)==len(z)==len(s)==meta['acceptedNodes'] and len(meta['segments'])==len(case['changes'])
                assert set(s)==set(range(len(meta['segments'])))
                for i,row in enumerate(meta['segments']):
                    change=case['changes'][i]; mask=s==i; ts,ys=t[mask],z[mask]
                    end=case['changes'][i+1]['timeMs'] if i+1<len(case['changes']) else 1000.
                    assert row['index']==i and row['startMs']==change['timeMs'] and row['endMs']==end and row['displacementNanometers']==change['displacementNanometers']
                    assert row['success'] and len(ts)==row['acceptedNodes'] and ts[0]==change['timeMs'] and ts[-1]==end and np.all(np.diff(ts)>0)
                    assert np.array_equal(ys[0],y[int(ts[0]/.125)]) and np.array_equal(ys[-1],y[int(end/.125)])
                ledgers.append(dict(key=key+'-accepted',**physical_checks(z,spec))); nodes+=len(t)
            ledgers.append(dict(key=key,**physical_checks(y,spec))); refs.append(y)
        for i,j in [(0,1),(0,2),(1,2)]:
            references.append(dict(case=case['key'],pair=[i,j],**compare_values(refs[i],refs[j],spec['referenceBounds'])))
        previous_errors=None
        for step in spec['stepsMs']:
            key=case['key']+'-h'+str(step); folder=package/'study'/key; row=read(folder/'result.json')
            assert row in native['cases'] and row['completed'] and row['samples']==8001 and row['stepMs']==step and row['condition']==case
            first=read(folder/'initial.json'); final=row['finalState']
            assert first['sessionId']==final['sessionId']==spec['sessionId'] and first['cellId']==final['cellId'] and first['cellId'] not in ids
            ids.add(first['cellId']); assert first['version']==final['version']==spec['candidateVersion']
            assert first['fingerprint']==final['fingerprint'] and len(first['fingerprint'])==64
            fingerprints.add(first['fingerprint']); assert not first['faulted'] and not final['faulted']
            assert first['completedSteps']==0 and first['timeMs']==0 and final['completedSteps']==int(1000/step) and final['timeMs']==1000
            assert first['stepMs']==final['stepMs']==step and len(final['additionResiduals'])==11 and np.isfinite(final['additionResiduals']).all()
            data=np.genfromtxt(folder/'trajectory.csv',delimiter=',',names=True); assert list(data.dtype.names)==CSV_NAMES and len(data)==8001
            matrix=np.column_stack([data[n] for n in CSV_NAMES]); assert np.isfinite(matrix).all() and np.array_equal(matrix[:,0],grid)
            available=np.array([next(c['displacementNanometers'] for c in reversed(case['changes']) if c['timeMs']<=t) for t in grid])
            assert np.array_equal(matrix[:,1],available) and np.array_equal(matrix[:,2],np.r_[available[0],available[:-1]])
            y=matrix[:,3:14]; assert np.array_equal(y[0],first['values']) and np.array_equal(y[-1],final['values'])
            assert np.allclose(y[0],initial(PARAMETERS,BATH),rtol=0,atol=1e-14)
            current_expected=np.array([currents(v,x,PARAMETERS,BATH) for v,x in zip(y,available)])
            assert np.all(np.abs(matrix[:,14:]-current_expected)<=spec['arithmeticAbsoluteTolerance']+spec['arithmeticRelativeTolerance']*np.abs(current_expected))
            ledger=dict(key=key,internalMaximumChargeResidualFc=final['maximumChargeResidualFc'],**physical_checks(y,spec))
            ledger['passed'] &= final['maximumChargeResidualFc']<=spec['chargeLedgerAbsoluteFc']; ledgers.append(ledger)
            for i,ref in enumerate(refs):
                bounds=spec['finestBounds'] if step==spec['stepsMs'][-1] else spec['candidateBounds']
                candidates.append(dict(case=case['key'],stepMs=step,reference=i,**compare_values(y,ref,bounds)))
            errors=compare_values(y,refs[1],spec['candidateBounds'])['errors']
            if previous_errors is not None:
                allowed={k:max(spec['refinementRoundoffFloor'][k],previous_errors[k]*spec['refinementMaximumRatio']) for k in errors}
                refinement.append(dict(case=case['key'],stepMs=step,errors=errors,allowed=allowed,passed=all(errors[k]<=allowed[k] for k in errors)))
            previous_errors=errors
    assert len(ids)==18 and len(fingerprints)==3 and len(references)==18 and len(candidates)==54 and len(refinement)==12 and len(ledgers)==54
    passed=all(r['passed'] for r in references+candidates+refinement+ledgers)
    result=dict(completed=True,numericalCriteriaPassed=passed,testInventory=tests,controls=12,arithmetic=arithmetic,
        nativeCaptures=18,referenceTrajectories=18,acceptedReferenceNodes=nodes,referenceContrasts=references,
        candidateContrasts=candidates,refinementContrasts=refinement,chargeAndGateChecks=ledgers,
        hairCellMembraneIntegrated=True,afferentMembraneIntegrated=False,liveBrainAccessed=False,
        nativeBodyExecuted=False,physiologyQualified=False,avatarRecoveryAssessed=False,originalBoundsChanged=False)
    save(package/'analysis.json',result)
    if not passed: raise RuntimeError('Hair-cell numerical criteria failed; preserve evidence and original bounds.')
    print('18 native hair-cell captures and 18 independent references reconciled. No afferent or live body qualification.',flush=True)


if __name__=='__main__':
    if not __debug__: raise RuntimeError('Assertions must remain enabled.')
    parser=argparse.ArgumentParser(); parser.add_argument('package',type=Path)
    parser.add_argument('--stage',required=True,choices=['preflight','controls','reference','compare','postflight','check-tests'])
    args=parser.parse_args(); package=args.package.resolve()
    with np.errstate(over='raise',invalid='raise',divide='raise'):
        if args.stage=='check-tests': print(module('hair_cell_gate',ROOT/'tools/receptor_membrane_gate.py').check_tests(package,COUNTS))
        else: globals()[args.stage](package)

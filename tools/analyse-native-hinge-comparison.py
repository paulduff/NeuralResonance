"""Compare reported-completed native traces; reference solves run only in PowerShell."""
import argparse
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import traceback

for name in ('OMP_NUM_THREADS', 'OPENBLAS_NUM_THREADS', 'MKL_NUM_THREADS', 'NUMEXPR_NUM_THREADS'):
    os.environ[name] = '1'
import numpy as np
from causal_motor_reference import tendon_energy_change
from native_hinge_protocol import make_protocol, read, decode
from native_hinge_reference import HingeGeometry, HingeCoupledReference
from native_hinge_reference_controls import run_controls

OBSERVED = np.r_[0:2, 4:26]
FORCES = ('activeFiberForceNewtons', 'passiveFiberForceNewtons', 'tendonAxialForceNewtons',
          'projectedTendonForceNewtons', 'projectedFiberForceNewtons', 'geometricForceNewtons',
          'accelerationMetersPerSecondSquared')


def save(path, value):
    with path.open('x', encoding='utf-8') as stream:
        json.dump(value, stream, indent=2, allow_nan=False); stream.write('\n')


def environment(package):
    spec = importlib.util.spec_from_file_location('dependencies', package/'source/tools/golgi-reference-resume.py')
    module = importlib.util.module_from_spec(spec); spec.loader.exec_module(module)
    return module.environment()


def validate(package):
    spec = read(package/'hinge-input.json')
    assert spec == make_protocol(package/'native-bridge', package/'reference/muscle-input.json')
    review = read(package/'reference/native-review.json')
    assert review['operatorReportedCompletion'] and review['passedTests'] == 81
    assert review['savedAnalysisExactlyReproduced'] and review['analysis']['originalCriteriaPassed']
    assert review['analysis']['actualPhysicsSteps'] == 4096
    return spec


def preflight(package):
    spec = validate(package); current = environment(package)
    assert current == read(package/'preparation-environment.json')
    assert current == read(package/'reference/qualified-numerical-preflight.json')['environment']
    # Parsing retained endpoints is not a new native simulation or numerical solve.
    for case in spec['cases']:
        candidate_data(package/'native-bridge'/case['key'], case)
    save(package/'reference-preflight.json', dict(environment=current, completed=True, protocol=spec, modelExecuted=False))


def postflight(package):
    current = environment(package)
    unchanged = current == read(package/'reference-preflight.json')['environment'] == read(package/'preparation-environment.json')
    save(package/'reference-postflight.json', dict(environment=current, unchanged=unchanged, modelExecuted=False))
    if not unchanged: raise RuntimeError('Numerical dependencies changed during the study.')


def candidate_data(folder, case, *, allowed_steps=(512,1024,2048)):
    initial = read(folder/'initial.json'); final = read(folder/'final.json')
    rows = [decode(json.loads(line)) for line in (folder/'steps.jsonl').read_text().splitlines()]
    assert len(rows) == case['physicalSteps'] and case['physicalSteps'] in allowed_steps
    entries = [(initial['initial'], initial['muscles'], initial['native'])]
    events = []; previous = initial['initial']
    for index, row in enumerate(rows, 1):
        step = row['step']; assert step['before'] == previous
        after = step['after']; assert after['physicsStep'] == index
        assert after['lastAppliedEffortId'] == step['effort']['effortId']
        entries.append((after, step['muscles'], row['native'])); events.extend(step['arrivals']); previous = after
    assert final['muscles'] == entries[-1][1] and final['native'] == entries[-1][2]
    assert final['plant']['deliveredEvents'] == len(events) == len(case['events'])
    expected = sorted((e['originalMessageId'], e['originalSourceNeuronId'], e['originalContactId'], e['arrivalAtMs']) for e in case['events'])
    actual = sorted((e['messageId'], e['sourceNeuronId'], e['contactId'], e['arrivalAtBodyMs']) for e in events)
    assert actual == expected and len({e['messageId'] for e in events}) == len(events)
    # Native capture has no integrated joint impulse/work or tendon-work columns.
    # Keep these unobserved columns NaN, never fabricated zeros or accuracy claims.
    y = np.full((len(entries), 28), np.nan); f = np.empty((len(entries), 2, 7))
    path = np.empty((len(entries), 2)); times = []; release = np.empty_like(path); arrivals = np.empty_like(path)
    recorded = dict(calciumLedger=0., fiberMomentumLedger=0., fiberKineticLedger=0.)
    for index, (body, muscles, native) in enumerate(entries):
        time = index*case['physicalStepMs']; times.append(time)
        assert body['physicalTimeMs'] == native['physicalTimeMs'] == time
        assert body['physicsStep'] == native['physicsStep'] == index
        assert native['physicsStepMs'] == case['physicalStepMs'] and native['isolated'] and native['paused']
        assert body['sessionId'].replace('-', '') == native['sessionId']
        assert native['geometryJson'] == initial['native']['geometryJson']
        assert hashlib.sha256(native['stateJson'].encode()).hexdigest().upper() == native['stateHash']
        state = json.loads(native['stateJson'])
        # These coordinates originate as Unity binary32 articulation values.
        y[index, :2] = np.asarray([state['angle'], state['speed']], dtype=np.float32).astype(float)
        for i in range(2):
            key = 'm'+str(i); m = muscles[key]; unit = m['units'][0]
            assert len(m['units']) == 1 and m['boundary'] == 0 and not m['faulted'] and m['timeMs'] == time
            assert body['paths'][i]['muscleId'] == native['paths'][i]['muscleId'] == key
            path[index, i] = m['musculotendonLengthMeters']
            assert path[index, i] == body['paths'][i]['lengthMeters'] == native['paths'][i]['lengthMeters']
            y[index, 4+11*i:15+11*i] = [m['fiberLengthMeters'], m['fiberVelocityMetersPerSecond'], m['yield'],
                unit['freeCalcium'], unit['boundCalcium'], unit['activation'], unit['releasedCalcium'],
                unit['recoveredCalcium'], unit['netBoundCalcium'], m['integratedNetImpulseNewtonSeconds'], m['integratedNetFiberWorkJoules']]
            f[index, i] = [m['forces'][name] for name in FORCES]
            release[index, i] = unit['releaseDrive']; arrivals[index, i] = unit['arrivals']
            recorded['calciumLedger'] = max(recorded['calciumLedger'], unit['maximumCalciumLedgerError'])
            recorded['fiberMomentumLedger'] = max(recorded['fiberMomentumLedger'], m['maximumMomentumLedgerError'])
            recorded['fiberKineticLedger'] = max(recorded['fiberKineticLedger'], m['maximumKineticLedgerError'])
            occurred = [e for e in case['events'] if e['muscleIndex']==i and e['arrivalAtMs']<=time]
            assert unit['arrivals'] == len(occurred)
            assert unit['lastMessageId'] == (occurred[-1]['originalMessageId'] if occurred else '00000000-0000-0000-0000-000000000000')
    assert np.isfinite(y[:, OBSERVED]).all() and all(np.isfinite(a).all() for a in (f,path,release,arrivals))
    return dict(timeMs=np.asarray(times), state=y, forces=f, path=path, release=release, arrivals=arrivals, recordedLedgers=recorded)


def errors(a, b, af, bf):
    difference = np.zeros(28); difference[OBSERVED] = np.max(abs(a[:, OBSERVED]-b[:, OBSERVED]), axis=0)
    result = dict(jointAngleRadians=float(difference[0]), jointSpeedRadiansPerSecond=float(difference[1]))
    for i in range(2):
        d = difference[4+11*i:15+11*i]; f = np.max(abs(af[:, i]-bf[:, i]), axis=0)
        local = dict(fiberLengthMeters=d[0], fiberVelocityMetersPerSecond=d[1], yieldFraction=d[2],
            calcium=max(d[3:5]), activation=d[5], fluxIntegral=max(d[6:9]), forceNewtons=max(f[:6]),
            accelerationMetersPerSecondSquared=f[6], impulseNewtonSeconds=d[9], workJoules=d[10])
        for name, value in local.items(): result[name] = max(result.get(name, 0.), float(value))
    return result


def within(error, bounds):
    assert error.keys() == bounds.keys()
    return all(np.isfinite(error[name]) and error[name] <= bound for name, bound in bounds.items())


def accounting(spec, case, y, inertia=None):
    g = spec['geometry']; result = dict(calciumLedger=0., fiberMomentumLedger=0., fiberKineticLedger=0.)
    bounds = dict(calciumLedger=spec['calciumLedgerAbsolute'], fiberMomentumLedger=spec['fiberMomentumLedgerNewtonSeconds'],
                  fiberKineticLedger=spec['fiberKineticLedgerJoules'])
    for i in range(2):
        p = y[:, 4+11*i:15+11*i]
        result['calciumLedger'] = max(result['calciumLedger'], float(np.max(abs(p[:,3]-p[:,6]+p[:,7]+p[:,8]))), float(np.max(abs(p[:,4]-p[:,8]))))
        result['fiberMomentumLedger'] = max(result['fiberMomentumLedger'], float(np.max(abs(g['fiberMassKg']*p[:,1]-p[:,9]))))
        result['fiberKineticLedger'] = max(result['fiberKineticLedger'], float(np.max(abs(.5*g['fiberMassKg']*p[:,1]**2-p[:,10]))))
    if inertia is not None:
        w0 = case['initialSpeedRadiansPerSecond']
        result['jointMomentumLedger'] = float(np.max(abs(inertia*(y[:,1]-w0)-y[:,2])))
        result['jointKineticLedger'] = float(np.max(abs(.5*inertia*(y[:,1]**2-w0*w0)-y[:,3])))
        bounds.update(jointMomentumLedger=spec['referenceJointMomentumLedgerNewtonMeterSeconds'], jointKineticLedger=spec['referenceJointKineticLedgerJoules'])
    result['passed'] = all(np.isfinite(result[k]) and result[k]<=v for k,v in bounds.items())
    return result


def reference_energy(spec, geometry, case, y):
    cosine = np.cos(spec['geometry']['pennationRadians']); paths = geometry.measure(y[:,0])['length']
    start_paths = geometry.measure(case['initialAngleRadians'])['length']; quadrature = ledger = 0.
    for i in range(2):
        start = start_paths[i]-cosine*spec['initialFiberLengthMeters']; end = paths[:,i]-cosine*y[:,4+11*i]
        low = tendon_energy_change(spec['geometry'], start, end, 64); high = tendon_energy_change(spec['geometry'], start, end, 128)
        quadrature = max(quadrature, float(np.max(abs(high-low)))); ledger = max(ledger, float(np.max(abs(high-y[:,26+i]))))
    return dict(quadratureErrorJoules=quadrature, tendonWorkLedgerJoules=ledger,
        passed=quadrature<=spec['tendonQuadratureAgreementJoules'] and ledger<=spec['referenceTendonEnergyLedgerJoules'])


def solve(package):
    spec = validate(package); assert environment(package) == read(package/'reference-preflight.json')['environment']
    controls = read(package/'reference-controls/controls.json'); assert controls['completed'] and controls['passed']
    output = package/'hinge-reference'; output.mkdir(); records = []
    geometry = HingeGeometry(spec['nativeGeometry'])
    for case in spec['cases']:
        data = candidate_data(package/'native-bridge'/case['key'], case); grid = data['timeMs']/1000
        for settings in spec['referenceSettings']:
            key = case['key']+'-'+settings['key']; print('NATIVE HINGE REFERENCE', key, flush=True)
            ref = HingeCoupledReference(spec, case, geometry)
            try:
                y, forces, stats = ref.solve(settings, grid)
                np.savez_compressed(output/(key+'.npz'), timeSeconds=grid, state=y, forces=forces)
                stats = dict(completed=True, **stats)
            except Exception:
                partial = getattr(ref, 'partial', {})
                np.savez_compressed(output/(key+'-partial.npz'), **partial)
                stats = dict(completed=False, error=traceback.format_exc()); print(stats['error'], flush=True)
            save(output/(key+'-statistics.json'), stats); records.append(dict(key=key, **stats))
    save(output/'execution.json', dict(completed=len(records)==12, successfulReferences=sum(r['completed'] for r in records), references=records))


def audit(package):
    spec = validate(package); assert environment(package) == read(package/'reference-preflight.json')['environment']
    controls = read(package/'reference-controls/controls.json')
    assert controls['completed'] and controls['passed'] and not controls['biologicalDynamicsIntegrated']
    assert len(controls['geometryCases'])==49 and len(controls['mechanicalMethodChecks'])==6 and len(controls['crossMethodChecks'])==3
    assert all(r['passed'] for group in ('geometryCases','mechanicalMethodChecks','crossMethodChecks') for r in controls[group])
    executed = read(package/'hinge-reference/execution.json'); assert executed['completed'] and len(executed['references'])==12
    geometry = HingeGeometry(spec['nativeGeometry']); results = []
    for case in spec['cases']:
        result = dict(case=case['key'], references=[], referenceContrasts=[], nativeContrasts=[], passed=False); results.append(result)
        model = HingeCoupledReference(spec, case, geometry); saved = {}
        candidate = candidate_data(package/'native-bridge'/case['key'], case); grid = candidate['timeMs']/1000
        y = candidate['state']; path = geometry.measure(y[:,0])['length']; cosine = np.cos(spec['geometry']['pennationRadians'])
        domain = all(m.domain(y[:,sl]) and np.min(candidate['path'][:,i]-cosine*y[:,sl.start])>0 for i,(m,sl) in enumerate(zip(model.muscles,model.slices)))
        release_error = max(float(np.max(abs(np.asarray([m.release(t,0) for t in grid])-candidate['release'][:,i]))) for i,m in enumerate(model.muscles))
        path_error = float(np.max(abs(path-candidate['path'])))
        ledgers = accounting(spec, case, y)
        bounds = dict(calciumLedger=spec['calciumLedgerAbsolute'], fiberMomentumLedger=spec['fiberMomentumLedgerNewtonSeconds'], fiberKineticLedger=spec['fiberKineticLedgerJoules'])
        recorded_ok = all(candidate['recordedLedgers'][k]<=v for k,v in bounds.items())
        result['nativeChecks'] = dict(sampleDomainPassed=bool(domain), ledgers=ledgers, recordedLedgers=candidate['recordedLedgers'],
            maximumPathGeometryErrorMeters=path_error, maximumReleaseDriveError=release_error,
            passed=bool(domain and ledgers['passed'] and recorded_ok and path_error<=spec['pathGeometryMeters'] and release_error<=spec['releaseWaveformAbsolute']))
        for settings in spec['referenceSettings']:
            key = case['key']+'-'+settings['key']; stats = read(package/'hinge-reference'/(key+'-statistics.json'))
            assert stats == {k:v for k,v in next(r for r in executed['references'] if r['key']==key).items() if k!='key'}
            if not stats['completed']:
                result['references'].append(dict(key=settings['key'], completed=False, passed=False)); continue
            with np.load(package/'hinge-reference'/(key+'.npz')) as data:
                assert np.array_equal(grid,data['timeSeconds']); state=data['state']; forces=data['forces']
            assert state.shape==(1025,28) and forces.shape==(1025,2,7)
            accounts = accounting(spec,case,state,geometry.inertia); energy=reference_energy(spec,geometry,case,state)
            passed = bool(stats['acceptedInternalDomainPassed'] and stats['sampleDomainPassed'] and model.domain(state) and np.isfinite(forces).all() and accounts['passed'] and energy['passed'])
            result['references'].append(dict(key=settings['key'], completed=True, ledgers=accounts, energy=energy, passed=passed))
            saved[settings['key']]=(state,forces)
        if len(saved)!=3: continue
        for a,b in (('dop853','dop853-tight'),('dop853-tight','radau')):
            error = errors(saved[a][0],saved[b][0],saved[a][1],saved[b][1])
            result['referenceContrasts'].append(dict(left=a,right=b,errors=error,passed=within(error,spec['referenceBounds'])))
        for key,(state,forces) in saved.items():
            error=errors(y,state,candidate['forces'],forces)
            result['nativeContrasts'].append(dict(reference=key,errors=error,passed=within(error,spec['candidateBounds'])))
        result['passed']=result['nativeChecks']['passed'] and all(r['passed'] for group in ('references','referenceContrasts','nativeContrasts') for r in result[group])
    return dict(completed=len(results)==4, capturedTrajectoryAgreementPassed=all(r['passed'] for r in results), cases=results,
        capturedNativeCases=4,capturedNativeSteps=4096,independentReferences=12,successfulReferences=executed['successfulReferences'],
        nativePhysicsReexecuted=False,neuronalModelExecuted=False,nativeJointIntegralAccuracyAssessed=False,
        physicalStepRefinementAssessed=False,muscleStepRefinementAssessed=False,fullCoupledTrajectoryQualified=False,
        physiologyQualified=False,avatarRecoveryAssessed=False)


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('package',type=Path)
    parser.add_argument('--stage',choices=('preflight','controls','solve','audit','postflight'),required=True)
    args=parser.parse_args();package=args.package.resolve()
    if args.stage=='preflight':preflight(package)
    elif args.stage=='postflight':postflight(package)
    elif args.stage=='controls':
        spec=validate(package);assert environment(package)==read(package/'reference-preflight.json')['environment']
        run_controls(spec,spec['nativeGeometry'],package/'reference-controls')
    elif args.stage=='solve':solve(package)
    else:
        result=audit(package);save(package/'hinge-analysis.json',result)
        print(json.dumps(result,indent=2),flush=True)
        if not result['capturedTrajectoryAgreementPassed']:raise RuntimeError('Captured native trajectory criteria failed; preserve evidence and original bounds.')

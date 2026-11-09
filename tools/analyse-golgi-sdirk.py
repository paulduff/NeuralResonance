"""Audit complete physical traces against frozen, checked Golgi references."""
from hashlib import sha256
import importlib.util
import json
import os
from pathlib import Path
import sys
for name in ('OPENBLAS_NUM_THREADS', 'OMP_NUM_THREADS', 'MKL_NUM_THREADS'):
    os.environ[name] = '1'
import numpy as np
import scipy
from golgi_sdirk_protocol import protocol

read = lambda p: json.loads(p.read_text(encoding='utf-8-sig'))
FIELDS = ('voltageMv', 'gate', 'sk', 'calciumMm', 'crossingMs')


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result)
    return result


def reference_array(path):
    with np.load(path, allow_pickle=False) as data:
        assert set(data.files) == {'times', 'states', 'spikes'}
        t, y, s = (data[k].copy() for k in ('times', 'states', 'spikes'))
    assert np.array_equal(t, np.arange(24001)/4) and y.shape == (24001, 159)
    assert np.isfinite(y).all() and np.isfinite(s).all() and np.all(np.diff(s)>0)
    assert np.all((s>0)&(s<=6000)) and y[:,133:157].min()>-1e-8 and y[:,133:157].max()<1+1e-8
    assert y[:,-2:].min()>0 and np.max(np.abs(y[:,151:157].sum(axis=1)-1))<1e-8
    return dict(times=t, states=y, spikes=s)


def compare(a, b):
    assert np.array_equal(a['times'],b['times']) and a['states'].shape == b['states'].shape
    error = np.abs(a['states']-b['states'])
    same = len(a['spikes']) == len(b['spikes'])
    return dict(voltageMv=float(error[:,:133].max()), somaMv=float(error[:,0].max()),
        gate=float(error[:,133:151].max()), sk=float(error[:,151:157].max()), calciumMm=float(error[:,-2:].max()/1000),
        equalCrossingCounts=same, counts=[len(a['spikes']),len(b['spikes'])],
        crossingMs=float(np.abs(a['spikes']-b['spikes']).max(initial=0)) if same else None)


def within(error, criteria, fraction=1):
    return error['equalCrossingCounts'] and all(error[f] is not None and error[f] <= fraction*criteria[f] for f in FIELDS)


def load_references(package, spec):
    base = package/'reference-baseline'
    audit = module('preserved_reference_comparison',base/'reference-source/golgi-reference-refinement.py')
    analysis = read(base/'study/analysis.json')
    assert analysis['completed'] and analysis['allOriginalReferenceCriteriaPassed'] and len(analysis['comparisons'])==12
    names = list(dict.fromkeys(c['protocol'] for c in spec['cases'] if c['block']!='Passive'))
    refs, uncertainties = {}, {}
    for name in names:
        old = reference_array(base/f'golgi-input/study/active-reference/{name}-strict.npz')
        bdf = reference_array(base/f'retained-reference/{name}-bdf-tighter.npz')
        folder = 'study' if name=='no-sk' else 'retained-reference'
        radau = reference_array(base/f'{folder}/{name}-radau-strict.npz')
        for pair,a,b in (('old-strict-vs-bdf-tighter',old,bdf),('bdf-tighter-vs-radau-strict',bdf,radau)):
            saved = next(c for c in analysis['comparisons'] if c['protocol']==name and c['pair']==pair)
            assert dict(protocol=name,pair=pair,**audit.compare(a,b))==saved and saved['passedOriginalCriteria']
        uncertainty = compare(bdf,radau)
        if not within(uncertainty,spec['criteria'],spec['criteria']['referenceFraction']):
            raise RuntimeError('Reference uncertainty exceeds the prospective full-state budget: '+name)
        refs[name] = {'bdf-tighter':bdf,'radau-strict':radau}
        uncertainties[name] = uncertainty
    return refs, uncertainties


def passive_references(package):
    # The original active capture retained passive candidate CSVs but not the
    # continuous passive arrays. Recompute and SAVE both original BDF settings.
    base = package/'reference-baseline'
    source = module('preserved_passive_reference',base/'reference-source/analyse-golgi-cell.py')
    nodes = source.morphology(base/'golgi-input/reference')
    cap, stiffness = source.matrix(nodes)
    output = package/'passive-reference'
    output.mkdir()  # A new package is required; never overwrite a solve.
    times = np.arange(401)/4
    refs, reports = {}, []
    for name,electrode in (('passive-soma',False),('passive-electrode',True)):
        loose,n1 = source.continuous_passive(cap,stiffness,electrode,times,1e-8,1e-10)
        strict,n2 = source.continuous_passive(cap,stiffness,electrode,times,1e-10,1e-12)
        assert loose.shape==strict.shape==(401,135) and np.isfinite(loose).all() and np.isfinite(strict).all()
        difference = float(np.abs(loose-strict).max())
        np.savez_compressed(output/(name+'.npz'),times=times,loose=loose,strict=strict)
        reports.append(dict(protocol=name,toleranceDifferenceMv=difference,evaluations=[n1,n2],passed=difference<2e-6))
        refs[name] = strict
    (output/'analysis.json').write_text(json.dumps(reports,indent=2)+'\n',encoding='utf-8')
    if not all(r['passed'] for r in reports):
        raise RuntimeError('Original passive reference agreement bound failed; arrays retained.')
    return refs,cap,reports


def validate_package(package):
    config=read(package/'configuration.json')
    assert config['ComponentsReviewed'] and config['ExpectedConditions']==30 and config['NoLiveMonitoring']
    for entry in config['PackageFiles']:
        assert sha256((package/entry['Path']).read_bytes()).hexdigest().upper()==entry['Sha256'],entry['Path']
    spec=read(package/'sdirk-input.json'); assert spec==protocol()
    assert np.__version__=='2.2.6' and scipy.__version__=='1.16.3'
    return spec


def preflight(package):
    spec=validate_package(package)
    environment=module('preserved_environment_capture',package/'source/tools/golgi-reference-resume.py').environment()
    _,uncertainty=load_references(package,spec)
    _,_,checks=passive_references(package)
    files=[dict(path=p.relative_to(package).as_posix(),sha256=sha256(p.read_bytes()).hexdigest())
           for p in sorted((package/'passive-reference').iterdir()) if p.is_file()]
    with (package/'reference-preflight.json').open('x',encoding='utf-8') as stream:
        stream.write(json.dumps(dict(completed=True,referenceUncertainty=uncertainty,passiveChecks=checks,files=files,environment=environment),indent=2)+'\n')
    print('Reference preflight complete; unchanged active arrays and newly saved passive references pass.',flush=True)


def load_passive(package):
    pre=read(package/'reference-preflight.json'); assert pre['completed']
    for entry in pre['files']:
        assert sha256((package/entry['path']).read_bytes()).hexdigest()==entry['sha256']
    reports=read(package/'passive-reference/analysis.json'); assert reports==pre['passiveChecks']
    refs={}
    for row in reports:
        with np.load(package/'passive-reference'/(row['protocol']+'.npz'),allow_pickle=False) as data:
            assert np.array_equal(data['times'],np.arange(401)/4)
            assert data['strict'].shape==data['loose'].shape==(401,135)
            assert np.isfinite(data['strict']).all() and np.isfinite(data['loose']).all()
            difference=float(np.abs(data['strict']-data['loose']).max())
            assert difference==row['toleranceDifferenceMv'] and difference<2e-6 and row['passed']
            refs[row['protocol']]=data['strict'].copy()
    assert set(refs)=={'passive-soma','passive-electrode'}
    source=module('preserved_passive_geometry',package/'reference-baseline/reference-source/analyse-golgi-cell.py')
    nodes=source.morphology(package/'reference-baseline/golgi-input/reference')
    cap,_=source.matrix(nodes)
    return refs,cap,reports


def candidate(package, row, condition, cap, criteria):
    key = condition['key']
    assert row['condition']==condition and row['completed'] and row['error'] is None and not row['faulted']
    count = int(condition['durationMs']*4)+1
    assert row['samplesSaved']==count and row['timeMs']==condition['durationMs']
    data = np.fromfile(package/'study'/(key+'.bin'),dtype='<f8')
    assert data.size==count*162
    data = data.reshape(count,162); times=data[:,0]; physical=data[:,1:]
    assert np.array_equal(times,np.arange(count)/4) and np.isfinite(physical).all()
    assert physical[:,135:159].min()>=0 and physical[:,135:159].max()<=1
    assert physical[:,159:].min()>0 and physical[:,159:].max()<=100
    assert np.max(np.abs(physical[:,153:159].sum(axis=1)-1))<=criteria['skMassError']
    assert np.array_equal(physical[0,:135],np.full(135,-5.))
    spikes = np.asarray(row['crossingsMs'])
    assert np.isfinite(spikes).all() and np.all(np.diff(spikes)>0) and np.all((spikes>0)&(spikes<=condition['durationMs']))
    ledger = np.genfromtxt(package/'study'/(key+'-accounting.csv'),delimiter=',',names=True)
    assert len(ledger)==count and np.array_equal(ledger['timeMs'],times)
    assert all(np.isfinite(ledger[n]).all() for n in ledger.dtype.names)
    mappings = [('maxChargeErrorFc','maximumChargeBalanceErrorFc','chargeLedgerFc'),
        ('maxCalciumErrorMm','maximumCalciumBalanceErrorMm','calciumLedgerMm'),
        ('maxKirchhoffPa','maximumKirchhoffResidualPa','kirchhoffPa'),
        ('maxGateStagePerMs','maximumGateStageResidualPerMs','gateStagePerMs'),
        ('maxCalciumStageMmPerMs','maximumCalciumStageResidualMmPerMs','calciumStageMmPerMs'),
        ('maxSkMassError','maximumSkMassError','skMassError'),
        ('maxScaledNonlinearResidual','maximumScaledNonlinearResidual','nonlinearResidual')]
    checks = {}
    for column,field,bound in mappings:
        assert ledger[column][-1]==row[field] and np.all(np.diff(ledger[column])>=0)
        checks[bound] = bool(row[field]<=criteria[bound])
    for column,field in (('newtonEvaluations','nonlinearEvaluations'),('maxNewtonIterations','maximumNewtonIterations')):
        assert ledger[column][-1]==row[field] and np.all(np.diff(ledger[column])>=0)
    assert row['maximumNewtonIterations']<=12
    charge_error = float(np.abs((physical[:,:135]+5)@cap-ledger['chargeFc']).max())
    high_error = float(np.abs(physical[:,159]/1000-.00005-ledger['highEnteredMm']+ledger['highRemovedMm']).max())
    low_error = float(np.abs(physical[:,160]/1000-.00005-ledger['lowEnteredMm']+ledger['lowRemovedMm']).max())
    checks.update(sampledCharge=charge_error<=criteria['chargeLedgerFc'],sampledCalcium=max(high_error,low_error)<=criteria['calciumLedgerMm'])
    state = np.concatenate((physical[:,[0]+list(range(3,135))],physical[:,135:]),axis=1)
    assert state.shape==(count,159)
    return dict(times=times,states=state,spikes=spikes), physical[:,:135]-55, dict(checks=checks,
        passed=all(checks.values()),sampledChargeLedgerErrorFc=charge_error,sampledCalciumLedgerErrorMm=max(high_error,low_error))


def audit(package):
    spec=validate_package(package)
    criteria = spec['criteria']
    refs, uncertainty = load_references(package,spec)
    assert uncertainty==read(package/'reference-preflight.json')['referenceUncertainty']
    passive,cap,passive_checks = load_passive(package)
    metadata = read(package/'study/protocol.json'); outcomes = read(package/'study/results.json')
    assert metadata['inputSha256']==sha256((package/'sdirk-input.json').read_bytes()).hexdigest().upper()
    assert metadata['specification']=={k:spec[k] for k in metadata['specification']}
    assert metadata['rowDoubles']==162 and not metadata['exactCheckpoint'] and not metadata['liveBrainAccessed']
    assert outcomes['completed'] and len(outcomes['cases'])==30
    assert outcomes['successfulCases']==sum(c['completed'] for c in outcomes['cases'])
    assert outcomes['allConditionsCompleted']==(outcomes['successfulCases']==30)
    assert [c['key'] for c in outcomes['cases']]==[c['key'] for c in spec['cases']]
    rows,states = [],{}
    for row,condition in zip(outcomes['cases'],spec['cases']):
        item = dict(key=condition['key'],protocol=condition['protocol'],stepMs=condition['stepMs'],
            nonlinearToleranceScale=condition['nonlinearToleranceScale'],completed=False,physicalPassed=False)
        try:
            state,voltage,physical = candidate(package,row,condition,cap,criteria)
            item.update(completed=True,physicalPassed=physical['passed'],physical=physical)
            if condition['block']=='Passive':
                item.update(passiveVoltageErrorMv=float(np.abs(voltage-passive[condition['protocol']]).max()),
                    noSpikes=len(state['spikes'])==0)
            else:
                item['comparisons']={name:compare(state,target) for name,target in refs[condition['protocol']].items()}
                states[condition['key']] = state
        except Exception as error:
            item.update(error=repr(error),recordedError=row.get('error'),samplesSaved=row.get('samplesSaved'),timeMs=row.get('timeMs'))
        rows.append(item)
        print('Audited '+condition['key']+': '+str(item['completed']),flush=True)
    assessment = []
    for name in dict.fromkeys(c['protocol'] for c in spec['cases']):
        selected=[r for r in rows if r['protocol']==name]
        ordinary=sorted([r for r in selected if r['nonlinearToleranceScale']==1],key=lambda r:r['stepMs'],reverse=True)
        result=dict(protocol=name,passed=False,refinement=[])
        try:
            assert len(ordinary)==3 and all(r['completed'] and r['physicalPassed'] for r in selected)
            if name.startswith('passive-'):
                result['passed']=all(r['noSpikes'] for r in ordinary) and ordinary[-1]['passiveVoltageErrorMv']<=criteria['passiveVoltageMv']
                for coarse,fine in zip(ordinary,ordinary[1:]):
                    limit=max(criteria['refinementRatio']*coarse['passiveVoltageErrorMv'],2*next(r['toleranceDifferenceMv'] for r in passive_checks if r['protocol']==name),criteria['refinementFloorFraction']*criteria['passiveVoltageMv'])
                    passed=fine['passiveVoltageErrorMv']<=limit
                    result['refinement'].append(dict(coarse=coarse['key'],fine=fine['key'],limitMv=limit,passed=passed))
                    result['passed'] &= passed
            else:
                tighter=next(r for r in selected if r['nonlinearToleranceScale']==.125)
                primary=spec['primaryReference']
                result['finestAbsolutePassed']=all(within(r['comparisons'][method],criteria)
                    for r in (ordinary[-1],tighter) for method in (primary,spec['crossMethodReference']))
                result['passed']=result['finestAbsolutePassed'] and all(r['comparisons'][primary]['equalCrossingCounts'] for r in selected)
                for coarse,fine in zip(ordinary,ordinary[1:]):
                    if not coarse['comparisons'][primary]['equalCrossingCounts'] or not fine['comparisons'][primary]['equalCrossingCounts']:
                        result['refinement'].append(dict(coarse=coarse['key'],fine=fine['key'],passed=False,error='Crossing counts differ.'))
                        result['passed']=False
                        continue
                    limits={f:max(criteria['refinementRatio']*coarse['comparisons'][primary][f],2*uncertainty[name][f],criteria['refinementFloorFraction']*criteria[f]) for f in FIELDS}
                    passed=within(fine['comparisons'][primary],limits)
                    result['refinement'].append(dict(coarse=coarse['key'],fine=fine['key'],limits=limits,passed=passed))
                    result['passed'] &= passed
                sensitivity=compare(states[ordinary[-1]['key']],states[tighter['key']])
                result['nonlinearSensitivity']=sensitivity
                result['nonlinearSensitivityPassed']=within(sensitivity,criteria,criteria['nonlinearFraction'])
                result['passed'] &= result['nonlinearSensitivityPassed']
        except Exception as error:
            result.update(passed=False,error=repr(error))
        assessment.append(result)
    environment=module('preserved_environment_audit',package/'source/tools/golgi-reference-resume.py').environment()
    assert environment==read(package/'reference-preflight.json')['environment'],'Numerical dependencies changed during the study.'
    report=dict(completed=True,caseCount=30,completedCases=sum(r['completed'] for r in rows),cases=rows,
        criteria=criteria,referenceUncertainty=uncertainty,passiveReferenceComparisons=passive_checks,assessment=assessment,
        numericalCriteriaPassed=all(r['passed'] for r in assessment),physiologyQualified=False,activeAxonQualified=False,avatarRecoveryAssessed=False)
    return report


def main(package):
    report=audit(package)
    with (package/'sdirk-analysis.json').open('x',encoding='utf-8') as stream:
        stream.write(json.dumps(report,indent=2,allow_nan=False)+'\n')
    if not report['numericalCriteriaPassed']:
        raise RuntimeError('Coupled solver criteria not all met; every condition and original bound retained.')
    print('Prospective numerical checks passed. Physiological and circuit qualifications remain separate.',flush=True)


if __name__=='__main__':
    import argparse
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('package',type=Path)
    parser.add_argument('--preflight',action='store_true')
    args=parser.parse_args()
    (preflight if args.preflight else main)(args.package.resolve())

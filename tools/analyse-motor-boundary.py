"""Reuse saved adaptive references and unchanged gates; never solve a new ODE."""
import argparse
from copy import deepcopy
from hashlib import sha256
import importlib.util
import json
from pathlib import Path
import sys
import numpy as np
import scipy
from motor_boundary_protocol import protocol,original_protocol

read=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
digest=lambda p:sha256(p.read_bytes()).hexdigest().upper()

def load_reference_module(folder):
    spec=importlib.util.spec_from_file_location('preserved_motor_reference',folder/'motoneuron-active-reference.py')
    result=importlib.util.module_from_spec(spec);spec.loader.exec_module(result);return result

def reference_case(folder,name,saved,cap,criteria,reference):
    grid=np.arange(60001)*.1;arrays={}
    assert [r['method'] for r in saved['references']]==['dop853-tight','dop853-tighter','radau']
    for row in saved['references']:
        assert row['completed']
        with np.load(folder/(name+'-'+row['method']+'.npz'),allow_pickle=False) as data:
            assert np.array_equal(data['timeMs'],grid)
            state,crossings=data['state'].copy(),data['crossingsMs'].copy()
        metrics=reference.metrics(grid,state,crossings,cap)
        assert metrics==row['metrics'] and reference.physical(metrics,criteria)==row['physicalAndAccountingPassed']
        arrays[row['method']]=(state,crossings)
    assert [(c['left'],c['right']) for c in saved['referenceContrasts']]==[('dop853-tight','dop853-tighter'),('dop853-tighter','radau')]
    for contrast in saved['referenceContrasts']:
        a,sa=arrays[contrast['left']];b,sb=arrays[contrast['right']]
        error=reference.compare(a,b,sa,sb)
        assert error==contrast['maximumAbsolute'] and reference.meets(error,criteria,'reference')==contrast['passed']
    return arrays

def numerical_cases(study,reference_folder,specification,saved,cap,reference):
    """Original gates, with preserved reference rows. Also usable for a saved-control regression."""
    outcomes=read(study/'results.json');meta=read(study/'protocol.json');criteria=specification['criteria'];grid=np.arange(60001)*.1
    assert outcomes['completed'] and outcomes['expectedCases']==len(outcomes['cases'])==30
    assert outcomes['successfulCases']==sum(c['completed'] for c in outcomes['cases'])
    assert [(c['scenario'],c['stepMs']) for c in outcomes['cases']]==[(s['key'],h) for s in specification['cases'] for h in specification['stepsMs']]
    with np.load(reference_folder/'quiet-dop853-tighter.npz',allow_pickle=False) as data:initial=data['state'][0].copy()
    rows=[]
    for scenario,old in zip(specification['cases'],saved['cases']):
        assert scenario['key']==old['scenario'];name=scenario['key']
        arrays=reference_case(reference_folder,name,old,cap,criteria,reference)
        target,events=arrays['dop853-tighter']
        row=dict(scenario=name,references=deepcopy(old['references']),referenceContrasts=deepcopy(old['referenceContrasts']),candidates=[],passed=False)
        ref_ok=all(r['physicalAndAccountingPassed'] for r in row['references']) and all(c['passed'] for c in row['referenceContrasts'])
        for candidate in (c for c in outcomes['cases'] if c['scenario']==name):
            item=dict(key=candidate['key'],stepMs=candidate['stepMs'],completed=candidate['completed'],passed=False);row['candidates'].append(item)
            if not candidate['completed']:item['error']=candidate['error'];continue
            try:
                trace=np.loadtxt(study/(candidate['key']+'.csv'),delimiter=',',skiprows=1)
                assert trace.shape==(60001,31) and np.array_equal(trace[:,0],grid)
                state=trace[:,1:];crossings=np.asarray(candidate['crossingsMs'])
                assert np.max(np.abs(state[0]-initial))<=1e-12
                error=reference.compare(state,target,crossings,events);metrics=reference.metrics(grid,state,crossings,cap)
                accounting=reference.physical(metrics,criteria) and candidate['maximumCalciumLedgerErrorMm']<=criteria['calciumLedgerMm'] and candidate['maximumChargeLedgerErrorFc']<=criteria['chargeLedgerFc']
                item.update(maximumAbsolute=error,metrics=metrics,physicalAndAccountingPassed=bool(accounting),passed=bool(reference.meets(error,criteria,'candidate') and accounting))
            except Exception as error:item['error']=repr(error)
        ordered=sorted(row['candidates'],key=lambda c:c['stepMs'],reverse=True);refinement=[]
        for coarse,fine in zip(ordered,ordered[1:]):
            passed=False
            if ref_ok and 'maximumAbsolute' in coarse and 'maximumAbsolute' in fine:
                ec,ef=coarse['maximumAbsolute'],fine['maximumAbsolute'];passed=ec['equalCrossingCounts'] and ef['equalCrossingCounts']
                for field in ('voltageMv','gate','calciumMm','crossingMs'):
                    uncertainty=max(c['maximumAbsolute'][field] for c in row['referenceContrasts'])
                    if ec[field] is None or ef[field] is None or ef[field]>max(2*uncertainty,criteria['refinementRatio']*ec[field]):passed=False
            refinement.append(dict(coarseStepMs=coarse['stepMs'],fineStepMs=fine['stepMs'],passed=bool(passed)))
        row['refinement']=refinement
        row['passed']=bool(ref_ok and len(ordered)==3 and ordered[-1]['passed'] and len(refinement)==2 and all(r['passed'] for r in refinement) and all(r.get('physicalAndAccountingPassed',False) for r in ordered))
        rows.append(row)
    return rows

def preflight(package):
    assert np.__version__=='2.2.6' and scipy.__version__=='1.16.3'
    old=read(package/'original-input.json');new=read(package/'boundary-input.json')
    assert old==original_protocol() and new==protocol()
    assert {k:v for k,v in old.items() if k!='version'}=={k:v for k,v in new.items() if k!='version'}
    saved=read(package/'baseline/comparison.json');meta=read(package/'baseline/protocol.json')
    assert saved['completed'] and not saved['numericalCriteriaPassed'] and saved['criteria']==old['criteria']
    assert len(saved['cases'])==len(old['cases'])==10
    reference=load_reference_module(package/'reference-source');cap=np.asarray(meta['capacitancesPf'])
    for scenario,row in zip(old['cases'],saved['cases']):
        assert scenario['key']==row['scenario'];reference_case(package/'references',scenario['key'],row,cap,old['criteria'],reference)
    return dict(completed=True,referenceConditions=30,criteriaUnchanged=True,referencesReused=True,odeSolved=False,
        pythonVersion=sys.version,numpyVersion=np.__version__,scipyVersion=scipy.__version__)

def audit(package):
    check=preflight(package);assert check==read(package/'reference-preflight.json')
    original=read(package/'original-input.json');boundary=read(package/'boundary-input.json')
    saved=read(package/'baseline/comparison.json');old_meta=read(package/'baseline/protocol.json');old_results=read(package/'baseline/results.json')
    reference=load_reference_module(package/'reference-source');cap=np.asarray(old_meta['capacitancesPf'])
    for folder,mode,inputs in [('original-control','original',original),('boundary-study','endpoint-difference',boundary)]:
        meta=read(package/folder/'protocol.json')
        assert meta['clockScheduling']==mode and meta['exactTimeUnitPower']==80
        assert meta['inputSha256']==digest(package/('original-input.json' if folder=='original-control' else 'boundary-input.json'))
        assert meta['specification']=={k:inputs[k] for k in meta['specification']}
        for key in ('version','stateAccumulation','parameters','areasUm2','capacitancesPf','couplingNs','nativeNeuronExecuted','liveBrainAccessed'):
            assert meta[key]==old_meta[key],key
    control_results=read(package/'original-control/results.json')
    hashes=read(package/'baseline/control-trace-hashes.json')
    assert len(hashes)==30 and control_results['successfulCases']==30
    for original_row,current in zip(old_results['cases'],control_results['cases']):
        assert all(current[k]==v for k,v in original_row.items()),'Original control result changed.'
        assert digest(package/'original-control'/(current['key']+'.csv'))==hashes[current['key']],'Original control state bytes changed.'
    controls=numerical_cases(package/'original-control',package/'references',original,saved,cap,reference)
    assert controls==saved['cases'],'Original failed analysis changed.'
    outcomes=read(package/'boundary-study/results.json');clock=read(package/'provenance.json')['clockReview']['analysis']
    for study,mode in [(control_results,'original'),(outcomes,'endpoint-difference')]:
        for row in study['cases']:
            if not row['completed']:continue
            expected=next(c for c in clock['cases'] if c['mode']==mode and c['maximumStepMs']==row['stepMs'])
            assert row['calls']==expected['calls'] and row['suppliedUnits']==expected['finalSuppliedUnits'] and row['elapsedTimeErrorUnits']==expected['finalErrorUnits']
    cases=numerical_cases(package/'boundary-study',package/'references',boundary,saved,cap,reference)
    strong=next(c for c in cases if c['scenario']=='soma-6000')
    exercise=all(r['completed'] and sum(r['metrics']['windowCrossings'][1:4])>0 and r['metrics']['maximumSomaVoltageMv']>0 and r['metrics']['maximumCalciumMm']>.0001 for r in strong['references'])
    changes=[dict(scenario=new['scenario'],originalPassed=old['passed'],candidatePassed=new['passed'],
        originalFinest=old['candidates'][-1].get('maximumAbsolute'),candidateFinest=new['candidates'][-1].get('maximumAbsolute')) for old,new in zip(controls,cases)]
    return dict(completed=True,originalControlBytesReproduced=True,originalAnalysisReproduced=True,referencesReused=True,
        originalBoundsChanged=False,odeReferenceSolved=False,actionPotentialAndCalciumExercisePassed=bool(exercise),
        numericalCriteriaPassed=bool(exercise and all(c['passed'] for c in cases)),cases=cases,changes=changes,
        expectedCandidateConditions=30,expectedControlConditions=30,physiologyQualified=False,activeMotorSourceQualified=False,avatarRecoveryAssessed=False)

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('package',type=Path);parser.add_argument('--preflight',action='store_true')
    args=parser.parse_args();package=args.package.resolve();result=preflight(package) if args.preflight else audit(package)
    name='reference-preflight.json' if args.preflight else 'boundary-analysis.json'
    with (package/name).open('x',encoding='utf-8') as stream:stream.write(json.dumps(result,indent=2,allow_nan=False)+'\n')
    if not args.preflight:
        for row in result['changes']:print(f"{row['scenario']}: original {row['originalPassed']}; endpoint candidate {row['candidatePassed']}",flush=True)
        if not result['numericalCriteriaPassed']:raise RuntimeError('Original numerical gates not all met; preserve evidence and bounds.')
        print('Scheduling comparison passed its original numerical gates. Physiology, motor authority and Avatar recovery remain unqualified.')

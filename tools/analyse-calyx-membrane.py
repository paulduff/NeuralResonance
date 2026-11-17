"""Independent deposited-convention calyx arithmetic; visible study only.

No native implementation is imported. This is not a free-membrane solver.
"""
import argparse
import importlib.util
import json
import os
from pathlib import Path
import sys

for key in ('OMP_NUM_THREADS','OPENBLAS_NUM_THREADS','MKL_NUM_THREADS','NUMEXPR_NUM_THREADS'):os.environ[key]='1'
sys.path.insert(0,str(Path(__file__).resolve().parent))
from calyx_membrane_protocol import validate,COUNTS
import numpy as np

ROOT=Path(__file__).resolve().parents[1]
read=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
GATE_NAMES=['innerKv74','innerHcn2','outerKv74','outerHcn2']


def module(name,path):
    s=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m


def save(path,value):
    with path.open('x',encoding='utf-8') as f:json.dump(value,f,indent=2,allow_nan=False);f.write('\n')


def environment():return module('calyx_environment',ROOT/'tools/golgi-reference-resume.py').environment()


def preflight(package):
    validate(read(package/'calyx-input.json'));current=environment()
    assert current==read(package/'preparation-environment.json')
    save(package/'reference-preflight.json',dict(environment=current,modelsExecuted=False,testsExecuted=False))


def postflight(package):
    current=environment();unchanged=current==read(package/'reference-preflight.json')['environment']
    save(package/'reference-postflight.json',dict(environment=current,unchanged=unchanged))
    assert unchanged,'Numerical dependencies changed during the study.'


def rate(v):
    # Explicitly select the deposited HCN2 sign convention; it is not the
    # different reciprocal sign printed in the supplement's HCN2 equation.
    return dict(kv74=dict(equilibrium=float(1/(1+np.exp(-(v+52)/16))),
        tauMs=float(max(1.,1/(.0002488*np.exp(-.04401*v)+.4506*np.exp(.05437*v))))),
        hcn2=dict(equilibrium=float(1/(1+np.exp((v+95)/11.7))),
        tauMs=float(209.479+(1/np.exp(-(v+80.646)/6.916)+np.exp((v+80.646)/14.881))/2551.988)))


def gate_values(preset,v,cleft):
    if preset=='zero':return [0.,0.,0.,0.]
    if preset=='one':return [1.,1.,1.,1.]
    if preset=='mixed':return [.2,.3,.4,.5]
    assert preset=='conditioned'
    return [rate(w)[channel]['equilibrium'] for w in [v-cleft,v] for channel in ['kv74','hcn2']]


def face(v,k,sodium,gates,p,inner):
    area=p['innerAreaSquareMicrometers'] if inner else p['outerAreaSquareMicrometers']
    ek=float(26*np.log(k/150)) if inner else -88.4311
    ena=float(26*np.log(sodium/12)) if inner else 63.8751
    kv=p['kv74InnerFraction'] if inner else 1-p['kv74InnerFraction']
    hcn=p['hcn2InnerFraction'] if inner else 1-p['hcn2InnerFraction']
    density=p['innerPumpDensityPerSquareMicrometer'] if inner else p['outerPumpDensityPerSquareMicrometer']
    pump=96.485*density*.1/602000*(k/(k+1.5))**2*area
    kcc=p['innerKccMaximumPaPerSquareMicrometer']/(1+(17.5/k)**2)*area if inner else 0.
    row=dict(kv74PotassiumPa=p['kv74MaximumNs']*kv*gates[0]*(v-ek),
        hcnPotassiumPa=.8*p['hcn2MaximumNs']*hcn*gates[1]*(v-ek),
        hcnSodiumPa=.2*p['hcn2MaximumNs']*hcn*gates[1]*(v-ena),
        pumpPotassiumPa=-2*pump,pumpSodiumPa=3*pump,kccPotassiumPa=-kcc,kccChloridePa=kcc,
        unassignedLeakPa=0. if inner else p['outerLeakNs']*v)
    # Sum the actual ionic terms independently; do not discard KCC from the
    # potassium ledger just because it cancels electrically with chloride.
    row['totalOutwardPa']=sum(row.values())
    row['potassiumOutwardPa']=row['kv74PotassiumPa']+row['hcnPotassiumPa']+row['pumpPotassiumPa']+row['kccPotassiumPa']
    row['sodiumOutwardPa']=row['hcnSodiumPa']+row['pumpSodiumPa']
    return row


def evaluation(v,cleft,k,sodium,gates,p):
    inside=face(v-cleft,k,sodium,gates[:2],p,True);outside=face(v,5.,140.,gates[2:],p,False)
    return dict(innerVoltageMv=v-cleft,outerVoltageMv=v,
        innerPotassiumReversalMv=float(26*np.log(k/150)),innerSodiumReversalMv=float(26*np.log(sodium/12)),
        rates=dict(inner=rate(v-cleft),outer=rate(v)),inner=inside,outer=outside,
        totalOutwardPa=inside['totalOutwardPa']+outside['totalOutwardPa'])


def compare(package):
    spec=validate(read(package/'calyx-input.json'));assert environment()==read(package/'reference-preflight.json')['environment']
    tests=module('calyx_test_inventory',ROOT/'tools/receptor_membrane_gate.py').check_tests(package,COUNTS)
    result=read(package/'study/results.json');assert result['completed']
    for flag in ['freeMembraneIntegrated','cleftDynamicsIntegrated','afferentFiringQualified','liveAvatarSelected','physiologyQualified','avatarRecoveryAssessed']:
        assert result[flag] is False
    count=0;maximum=0.;fingerprints={}
    def near(actual,expected,absolute=None):
        nonlocal maximum
        if isinstance(expected,dict):
            assert set(actual)==set(expected)
            for name,value in expected.items():near(actual[name],value,absolute)
            return
        a,b=np.asarray(actual),np.asarray(expected);assert a.shape==b.shape and np.isfinite(a).all()
        error=np.abs(a-b);maximum=max(maximum,float(np.max(error)))
        allowed=spec['arithmeticAbsoluteTolerance']+spec['arithmeticRelativeTolerance']*np.abs(b) if absolute is None else absolute
        assert np.all(error<=allowed),('Independent calyx arithmetic differs',actual,expected)
    with (package/'study/arithmetic.jsonl').open() as source,(package/'independent-arithmetic.jsonl').open('x') as target:
        for variant in spec['variants']:
            p=variant['parameters']
            for v in spec['voltagesMv']:
                for cleft in spec['cleftPotentialsMv']:
                    for k in spec['cleftPotassiumMm']:
                        for sodium in spec['cleftSodiumMm']:
                            for preset in spec['gatePresets']:
                                row=json.loads(next(source));assert row['index']==count and row['variant']==variant['key']
                                assert [row['voltageMv'],row['cleftPotentialMv'],row['cleftPotassiumMm'],row['cleftSodiumMm'],row['preset']]==[v,cleft,k,sodium,preset]
                                gates=gate_values(preset,v,cleft);near(row['gates'],dict(zip(GATE_NAMES,gates)))
                                expected=evaluation(v,cleft,k,sodium,gates,p);near(row['evaluation'],expected)
                                near(row['innerCapacitancePf'],p['innerAreaSquareMicrometers']*p['specificCapacitancePfPerSquareMicrometer'])
                                near(row['outerCapacitancePf'],p['outerAreaSquareMicrometers']*p['specificCapacitancePfPerSquareMicrometer'])
                                if variant['key'] in fingerprints:assert row['fingerprint']==fingerprints[variant['key']]
                                else:fingerprints[variant['key']]=row['fingerprint'];assert len(row['fingerprint'])==64
                                target.write(json.dumps(dict(index=count,evaluation=expected),allow_nan=False)+'\n');count+=1
        assert not source.read().strip()
    assert count==result['arithmeticRows']==spec['expectedArithmeticRows']==15984 and len(set(fingerprints.values()))==6
    arithmetic_max=maximum;maximum=0.;clamps=0
    with (package/'study/clamps.jsonl').open() as source,(package/'independent-clamps.jsonl').open('x') as target:
        for v,cleft in spec['clampVoltagesMv']:
            rates=[rate(w)[c] for w in [v-cleft,v] for c in ['kv74','hcn2']]
            for preset in spec['gatePresets']:
                initial=gate_values(preset,v,cleft)
                for elapsed in spec['clampDurationsMs']:
                    row=json.loads(next(source));assert row['index']==clamps
                    assert [row['voltageMv'],row['cleftPotentialMv'],row['preset'],row['elapsedMs']]==[v,cleft,preset,elapsed]
                    assert row['subdivisionSteps']*.125==elapsed
                    near(row['initial'],dict(zip(GATE_NAMES,initial)),spec['clampAbsoluteTolerance'])
                    values=[float(r['equilibrium']+(x-r['equilibrium'])*np.exp(-elapsed/r['tauMs'])) for x,r in zip(initial,rates)]
                    expected=dict(zip(GATE_NAMES,values));near(row['actual'],expected,spec['clampAbsoluteTolerance']);near(row['subdivided'],expected,spec['clampAbsoluteTolerance'])
                    target.write(json.dumps(dict(index=clamps,expected=expected),allow_nan=False)+'\n');clamps+=1
        assert not source.read().strip()
    assert clamps==result['clampRows']==spec['expectedClampRows']==224
    save(package/'analysis.json',dict(completed=True,numericalCriteriaPassed=True,testInventory=tests,
        arithmeticRows=count,clampRows=clamps,variants=6,maximumArithmeticAbsoluteError=arithmetic_max,
        maximumClampAbsoluteError=maximum,sourceConvention='deposited model subset, not printed supplement equivalence',
        freeMembraneIntegrated=False,cleftDynamicsIntegrated=False,afferentFiringQualified=False,
        liveAvatarSelected=False,physiologyQualified=False,avatarRecoveryAssessed=False,originalBoundsChanged=False))
    print('15984 current/gate cases and 224 analytic voltage clamps reconciled. No free sensory cell or body integration.',flush=True)


if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    parser=argparse.ArgumentParser();parser.add_argument('package',type=Path)
    parser.add_argument('--stage',required=True,choices=['preflight','compare','postflight','check-tests']);args=parser.parse_args()
    with np.errstate(over='raise',invalid='raise',divide='raise'):
        if args.stage=='check-tests':print(module('calyx_test_gate',ROOT/'tools/receptor_membrane_gate.py').check_tests(args.package.resolve(),COUNTS))
        else:globals()[args.stage](args.package.resolve())

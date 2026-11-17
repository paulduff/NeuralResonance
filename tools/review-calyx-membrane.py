"""Reconcile an operator-completed calyx capture using saved arithmetic only."""
import argparse
from datetime import datetime, timezone
import importlib.util
import json
import math
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]


def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m


def review(capture,output,reported):
    if not reported: raise ValueError('Wait for the operator completion report before reading the study.')
    assert not output.exists(),'Preserve previous reviews.'
    common=module('calyx_completed_integrity',ROOT/'tools/review-olive-and-motor-20261109.py')
    config=common.read(capture/'configuration.json');status=common.read(capture/'window-status.json')
    assert config['IsolatedCalyxMembrane'] and config['NoLiveMonitoring'] and config['ExpectedTestCases']==23
    revision='635508a1d6d8dcaba156004d50d3e67e620b9acd'
    result=common.verify(capture,['test-binaries.json','probe-binaries.json'],revision)
    archive=module('calyx_completed_archive',ROOT/'tools/review-spindle-root-and-causal-motor-20261113.py')
    result['verifiedGitFiles']=archive.source_archive(common,capture,revision)
    protocol=module('calyx_completed_inputs',capture/'source/tools/calyx_membrane_protocol.py')
    spec=protocol.validate(common.read(capture/'calyx-input.json'))
    assert common.read(capture/'study/input.json')=={k:spec[k] for k in ('version','kernelVersion','parameters','variants',
        'voltagesMv','cleftPotentialsMv','cleftPotassiumMm','cleftSodiumMm','gatePresets','clampVoltagesMv','clampDurationsMs','clampSubdivisionMs')}
    inventory=module('calyx_completed_tests',ROOT/'tools/receptor_membrane_gate.py')
    tests=inventory.check_tests(capture,protocol.COUNTS)
    assert common.digest(capture/'managed-runtime-before.json')==common.digest(capture/'managed-runtime-after.json')
    before=common.read(capture/'reference-preflight.json')['environment'];after=common.read(capture/'reference-postflight.json')
    assert after['unchanged'] and before==after['environment']==common.read(capture/'preparation-environment.json')
    for key in ('FocusedTestsPassed','NumericalCriteriaPassed','ModelExecutionAttempted','ComponentStudyExecutionAttempted',
                'MembraneArithmeticEvaluated','NumericalEnvironmentUnchanged','ManagedRuntimePreserved','FrozenPackageUnchanged'):
        assert status[key] is True,key
    for key in ('FreeMembraneIntegrated','CleftDynamicsIntegrated','AfferentFiringQualified','NativeBodyExecuted',
                'LiveBrainAccessed','OldSavesLoaded','PhysiologyQualified','AvatarRecoveryAssessed','FullUnitSuitePassed'):
        assert status[key] is False,key
    saved=common.read(capture/'study/results.json')
    assert saved['completed'] and saved['arithmeticRows']==15984 and saved['clampRows']==224
    for key in ('freeMembraneIntegrated','cleftDynamicsIntegrated','afferentFiringQualified','liveAvatarSelected','physiologyQualified','avatarRecoveryAssessed'):
        assert saved[key] is False,key
    maximum=0.;fingerprints={};gates_by_input={};count=0
    gate_names=['innerKv74','innerHcn2','outerKv74','outerHcn2']
    def near(actual,expected,absolute=None):
        nonlocal maximum
        if isinstance(expected,dict):
            assert actual.keys()==expected.keys()
            for key,value in expected.items(): near(actual[key],value,absolute)
            return
        assert math.isfinite(actual) and math.isfinite(expected)
        error=abs(actual-expected);maximum=max(maximum,error)
        allowed=spec['arithmeticAbsoluteTolerance']+spec['arithmeticRelativeTolerance']*abs(expected) if absolute is None else absolute
        assert error<=allowed,(actual,expected,error,allowed)
    # Read the preserved independent values. Do not evaluate channels, run a
    # clamp transition or import the model/reference evaluator during review.
    with (capture/'study/arithmetic.jsonl').open() as native,(capture/'independent-arithmetic.jsonl').open() as reference:
        for variant in spec['variants']:
            p=variant['parameters']
            for v in spec['voltagesMv']:
                for cleft in spec['cleftPotentialsMv']:
                    for k in spec['cleftPotassiumMm']:
                        for sodium in spec['cleftSodiumMm']:
                            for preset in spec['gatePresets']:
                                row=json.loads(next(native));independent=json.loads(next(reference));expected=independent['evaluation']
                                assert row['index']==independent['index']==count and row['variant']==variant['key']
                                assert [row['voltageMv'],row['cleftPotentialMv'],row['cleftPotassiumMm'],row['cleftSodiumMm'],row['preset']]==[v,cleft,k,sodium,preset]
                                if preset=='conditioned':
                                    initial=[expected['rates'][face][channel]['equilibrium'] for face in ('inner','outer') for channel in ('kv74','hcn2')]
                                else: initial={'zero':[0.,0.,0.,0.],'one':[1.,1.,1.,1.],'mixed':[.2,.3,.4,.5]}[preset]
                                gates=dict(zip(gate_names,initial,strict=True))
                                assert gates_by_input.setdefault((v,cleft,preset),gates)==gates
                                near(row['gates'],gates);near(row['evaluation'],expected)
                                near(row['innerCapacitancePf'],p['innerAreaSquareMicrometers']*p['specificCapacitancePfPerSquareMicrometer'])
                                near(row['outerCapacitancePf'],p['outerAreaSquareMicrometers']*p['specificCapacitancePfPerSquareMicrometer'])
                                assert fingerprints.setdefault(variant['key'],row['fingerprint'])==row['fingerprint'] and len(row['fingerprint'])==64
                                count+=1
        assert not native.read().strip() and not reference.read().strip()
    assert count==15984 and len(set(fingerprints.values()))==6
    arithmetic_max=maximum;maximum=0.;clamps=0
    with (capture/'study/clamps.jsonl').open() as native,(capture/'independent-clamps.jsonl').open() as reference:
        for v,cleft in spec['clampVoltagesMv']:
            for preset in spec['gatePresets']:
                for elapsed in spec['clampDurationsMs']:
                    row=json.loads(next(native));independent=json.loads(next(reference))
                    assert row['index']==independent['index']==clamps
                    assert [row['voltageMv'],row['cleftPotentialMv'],row['preset'],row['elapsedMs']]==[v,cleft,preset,elapsed]
                    assert row['subdivisionSteps']*spec['clampSubdivisionMs']==elapsed
                    near(row['initial'],gates_by_input[(v,cleft,preset)],spec['clampAbsoluteTolerance'])
                    near(row['actual'],independent['expected'],spec['clampAbsoluteTolerance'])
                    near(row['subdivided'],independent['expected'],spec['clampAbsoluteTolerance']);clamps+=1
        assert not native.read().strip() and not reference.read().strip()
    assert clamps==224
    analysis=dict(completed=True,numericalCriteriaPassed=True,testInventory=tests,arithmeticRows=count,clampRows=clamps,variants=6,
        maximumArithmeticAbsoluteError=arithmetic_max,maximumClampAbsoluteError=maximum,
        sourceConvention='deposited model subset, not printed supplement equivalence',freeMembraneIntegrated=False,
        cleftDynamicsIntegrated=False,afferentFiringQualified=False,liveAvatarSelected=False,physiologyQualified=False,
        avatarRecoveryAssessed=False,originalBoundsChanged=False)
    assert analysis==common.read(capture/'analysis.json'),'Saved independent comparisons did not reproduce the original summary.'
    result.update(reviewedUtc=datetime.now(timezone.utc).isoformat(),operatorReportedCompletion=True,
        savedAnalysisExactlyReproduced=True,numericalCriteriaPassed=True,passedTests=23,analysis=analysis,
        independentSavedComparisonsRechecked=True,testsExecutedDuringReview=False,modelsEvaluatedDuringReview=False,
        freeMembraneIntegrated=False,cleftDynamicsIntegrated=False,afferentFiringQualified=False,physiologyQualified=False,avatarRecoveryAssessed=False)
    output.mkdir(parents=True)
    with (output/'review.json').open('x',encoding='utf-8') as stream: json.dump(result,stream,indent=2,allow_nan=False);stream.write('\n')
    print(json.dumps({k:v for k,v in result.items() if k!='analysis'},indent=2),flush=True)


if __name__=='__main__':
    if not __debug__: raise RuntimeError('Assertions must remain enabled.')
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('capture',type=Path);parser.add_argument('output',type=Path)
    parser.add_argument('--operator-reported-complete',action='store_true',required=True);args=parser.parse_args()
    review(args.capture.resolve(),args.output.resolve(),args.operator_reported_complete)

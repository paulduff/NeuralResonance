"""Audit an operator-reported capture using saved states; never integrate a cell."""
import argparse
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]


def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path)
    result=importlib.util.module_from_spec(spec);spec.loader.exec_module(result);return result


def review(capture,output,reported):
    if not reported: raise ValueError('Wait for the operator completion report before reading the study.')
    assert not output.exists(), 'Preserve earlier reviews.'
    common=module('hair_cell_integrity',ROOT/'tools/review-olive-and-motor-20261109.py')
    config=common.read(capture/'configuration.json');status=common.read(capture/'window-status.json')
    revision='59b586bdb85a190797b077a9644b2e88496012f0'
    assert config['IsolatedVestibularHairCell'] and config['NoLiveMonitoring'] and config['ExpectedTestCases']==47
    result=common.verify(capture,['test-binaries.json','probe-binaries.json'],revision)
    archive=module('hair_cell_archive',ROOT/'tools/review-spindle-root-and-causal-motor-20261113.py')
    result['verifiedGitFiles']=archive.source_archive(common,capture,revision)
    frozen=module('completed_hair_cell_audit',capture/'source/tools/analyse-vestibular-hair-cell.py')
    # Reproduce only the saved-array/current arithmetic audit. Intercept its
    # sole report write into memory; the original capture stays byte-for-byte.
    writes=[]
    def preserve(path,value):
        assert path==capture/'analysis.json' and not writes
        writes.append(value)
    def forbidden(*args,**kwargs): raise RuntimeError('No solver, model integration or new control execution during review.')
    frozen.save=preserve;frozen.solve_ivp=forbidden;frozen.ode=forbidden
    frozen.controls=forbidden;frozen.reference=forbidden
    frozen.compare(capture)
    assert len(writes)==1 and writes[0]==common.read(capture/'analysis.json')
    analysis=writes[0]; assert analysis['numericalCriteriaPassed'] and analysis['testInventory']['passedCases']==47
    assert analysis['arithmetic']['rows']==7260 and analysis['nativeCaptures']==analysis['referenceTrajectories']==18
    assert common.digest(capture/'managed-runtime-before.json')==common.digest(capture/'managed-runtime-after.json')
    before=common.read(capture/'reference-preflight.json')['environment']
    after=common.read(capture/'reference-postflight.json')
    assert after['unchanged'] and before==after['environment']==common.read(capture/'preparation-environment.json')
    for key in ['FocusedTestsPassed','NumericalCriteriaPassed','HairCellMembraneIntegrated','HairCellStudyExecutionAttempted',
                'NumericalEnvironmentUnchanged','ManagedRuntimePreserved','FrozenPackageUnchanged']:
        assert status[key] is True,key
    for key in ['AfferentMembraneIntegrated','NativeBodyExecuted','LiveBrainAccessed','OldSavesLoaded',
                'PhysiologyQualified','AvatarRecoveryAssessed','FullUnitSuitePassed']:
        assert status[key] is False,key
    # Verify control summaries from their retained numerical output, without
    # rerunning either ODE solver or a Jacobian perturbation experiment.
    np=frozen.np; spec=common.read(capture/'hair-cell-input.json'); control=common.read(capture/'controls.json')
    assert control['passed'] and len(control['controls'])==12
    rows={r['key']:r for r in control['controls']}; assert len(rows)==12
    cap=frozen.constants(spec['parameters'],spec['bath'])[0]
    for setting in spec['referenceSettings']:
        for label in ['passive','zero']:
            key=label+'-'+setting['key']
            with np.load(capture/'reference-controls'/(key+'.npz'),allow_pickle=False) as data:
                t,y=data['times'],data['states']
                assert t[0]==0 and t[-1]==2 and np.all(np.diff(t)>0) and y.shape==(len(t),11) and np.isfinite(y).all()
                if label=='zero': assert float(np.max(np.abs(y-y[0])))==rows[key]['maximumError']==0
                else:
                    reversal=spec['parameters']['endolymphPotentialMv']+spec['parameters']['metReversalMv']
                    conductance=spec['parameters']['metMaximumNs']*frozen.met_open(300.)
                    v=reversal+(y[0,0]-reversal)*np.exp(-t*conductance/cap);q=-cap*(v-y[0,0])
                    assert float(np.max(np.abs(y[:,0]-v)))==rows[key]['voltageError']
                    assert float(np.max(np.abs(y[:,4]-q)))==rows[key]['chargeError']
                    assert np.array_equal(y[:,5:],np.zeros_like(y[:,5:]))
                    assert rows[key]['voltageError']<=spec['controlBounds']['passiveVoltageMv']
                    assert rows[key]['chargeError']<=spec['controlBounds']['passiveChargeFc']
    for v in [-100.,-75.,-40.]:
        key='jacobian-'+str(v)
        with np.load(capture/'reference-controls'/(key+'.npz'),allow_pickle=False) as data:
            error=float(np.max(np.abs(data['complexStep']-data['symmetricDifference'])))
            assert error==rows[key]['maximumError'] and error<=spec['controlBounds']['jacobian']
            assert np.array_equal(data['complexStep'][:,4:],np.zeros((11,7)))
    fine=[r for r in analysis['candidateContrasts'] if r['stepMs']==spec['stepsMs'][-1]]
    metrics=dict(maximumReferenceErrors={k:max(r['errors'][k] for r in analysis['referenceContrasts']) for k in spec['referenceBounds']},
        maximumFinestCandidateErrors={k:max(r['errors'][k] for r in fine) for k in spec['finestBounds']},
        maximumChargeLedgerFc=max(max(r['chargeResidualFc'],r.get('internalMaximumChargeResidualFc',0)) for r in analysis['chargeAndGateChecks']),
        maximumPumpStoichiometryResidualFc=max(r['pumpStoichiometryResidualFc'] for r in analysis['chargeAndGateChecks']),
        acceptedReferenceNodes=analysis['acceptedReferenceNodes'])
    quiet=np.genfromtxt(capture/'study/quiet-h0.0078125/trajectory.csv',delimiter=',',names=True)
    responses=[]
    for case in spec['cases']:
        data=np.genfromtxt(capture/'study'/(case['key']+'-h0.0078125')/'trajectory.csv',delimiter=',',names=True)
        responses.append(dict(case=case['key'],minimumVoltageMv=float(data['voltageMv'].min()),maximumVoltageMv=float(data['voltageMv'].max()),
            finalVoltageMv=float(data['voltageMv'][-1]),differenceFromQuietAt750Ms=float(data['voltageMv'][6000]-quiet['voltageMv'][6000])))
    result.update(reviewedUtc=datetime.now(timezone.utc).isoformat(),operatorReportedCompletion=True,
        savedAnalysisExactlyReproduced=True,numericalCriteriaPassed=True,passedTests=47,analysis=analysis,
        savedControlArraysRechecked=True,metrics=metrics,prescribedDisplacementResponses=responses,
        testsExecutedDuringReview=False,modelsIntegratedDuringReview=False,afferentFiringQualified=False,
        liveBodyCoupled=False,physiologyQualified=False,avatarRecoveryAssessed=False)
    output.mkdir(parents=True)
    with (output/'review.json').open('x',encoding='utf-8') as stream:json.dump(result,stream,indent=2,allow_nan=False);stream.write('\n')
    print(json.dumps({k:v for k,v in result.items() if k!='analysis'},indent=2),flush=True)


if __name__=='__main__':
    if not __debug__: raise RuntimeError('Assertions must remain enabled.')
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('capture',type=Path);parser.add_argument('output',type=Path)
    parser.add_argument('--operator-reported-complete',action='store_true',required=True);args=parser.parse_args()
    review(args.capture.resolve(),args.output.resolve(),args.operator_reported_complete)

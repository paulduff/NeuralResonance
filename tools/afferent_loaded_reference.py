"""Four-ms loaded-rest references. Run only in the visible qualification window.
Reuses the reviewed independent derivative, not native stages or currents.
"""
from afferent_trajectory_reference import Equation,save,precision_witness
from afferent_trajectory_protocol import group
import numpy as np
from scipy.integrate import solve_ivp
import traceback

def input_record(equation,case,setting,protocol):
    return dict(case=case,setting=setting,labels=equation.labels,initial=equation.initial.tolist(),decimalPrecision=equation.precision,
        explicitJacobian=True,ledgerJacobianColumnsExactlyZero=True,internalTrialGatesMayLeaveProbabilityDomain=True,
        acceptedNodesAndSamplesRequirePhysicalDomain=True,maximumStepMs=protocol['referenceMaximumStepMs'],restInitialized=True)


def run_case(protocol,case,setting,source,output):
    output.mkdir();equation=Equation(case['fixture'],source,protocol['referenceDecimalPrecision']);y=equation.initial.copy()
    save(output/'input.json',input_record(equation,case,setting,protocol))
    atol=np.array([protocol['referenceAtol'][group(n)]*setting['atolScale'] for n in equation.labels])
    all_ticks=[];all_values=[];metadata=[];start=0
    try:
        equation.validate_samples(y[:,None])
        for index,forcing in enumerate(case['segments']):
            end=forcing['endTick'];left=start/protocol['ticksPerMs'];right=end/protocol['ticksPerMs']
            result=solve_ivp(lambda t,state:equation.rates(state,forcing),(left,right),y,method=setting['method'],
                rtol=setting['rtol'],atol=atol,jac=lambda t,state:equation.jacobian(state,forcing),dense_output=True,
                max_step=protocol['referenceMaximumStepMs'])
            np.savez(output/f'segment-{index}-nodes.npz',timeMs=result.t,values=result.y)
            item=dict(segment=index,startTick=start,endTick=end,success=bool(result.success),message=result.message,
                nfev=result.nfev,njev=result.njev,nlu=result.nlu,acceptedNodes=len(result.t))
            save(output/f'segment-{index}.json',item);metadata.append(item)
            assert result.success and result.t[-1]==right and result.sol is not None
            equation.validate_samples(result.y)
            ticks=np.arange(start,end+1,protocol['sampleTicks'],dtype=np.int64);samples=result.sol(ticks/protocol['ticksPerMs'])
            np.savez(output/f'segment-{index}-samples.npz',ticks=ticks,values=samples)
            equation.validate_samples(samples)
            witnesses=[precision_witness(equation,result.y[:,j],forcing,start if j==0 else end,protocol) for j in (0,-1)]
            save(output/f'segment-{index}-precision.json',witnesses)
            assert all(w['precision']['passed'] and w['workingPrecision']['passed'] for w in witnesses),'Reference precision criteria failed.'
            if index:ticks=ticks[1:];samples=samples[:,1:]
            all_ticks.extend(ticks.tolist());all_values.extend(samples.T.tolist());y=result.y[:,-1].copy();start=end
        np.savez(output/'samples.npz',ticks=np.array(all_ticks,dtype=np.int64),values=np.array(all_values,dtype=np.float64))
        save(output/'results.json',dict(completed=True,segments=metadata,samples=len(all_ticks),allAcceptedNodesPhysicallyAdmissible=True,
            nativeTrajectoryValuesUsedForIntegration=False,savedNativeStationaryInitialStateUsed=True,restInitialized=True,stationaryInitializationQualified=False,
            afferentFiringQualified=False,physiologyQualified=False,avatarRecoveryAssessed=False))
    except Exception:
        save(output/'failure.json',dict(error=traceback.format_exc(),lastIndependentInput=equation.last_input,completedSegments=metadata))
        raise


def run(package,protocol,source):
    root=package/'reference-trajectories';root.mkdir();failures=[]
    for case in protocol['cases']:
        for setting in protocol['referenceSettings']:
            name=case['key']+'-'+setting['key'];print('INDEPENDENT SENSORY NERVE TRAJECTORY '+name,flush=True)
            try:run_case(protocol,case,setting,source,root/name)
            except Exception:
                failures.append(name);print('REFERENCE FAILED '+name+'; prefix retained.',flush=True)
    save(root/'results.json',dict(completed=not failures,attempted=12,failedRuns=failures,restInitialized=True))
    if failures:raise RuntimeError('Independent sensory nerve reference incomplete; all available prefixes retained.')

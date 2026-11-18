"""Independent full-graph Decimal RHS with Radau/BDF time integration.

Execute only in the independent visible study. No candidate stages, derivatives,
equilibrium initializers or solver coefficients are used to integrate references.
"""
from copy import deepcopy
from decimal import Decimal,localcontext
import importlib.util
import json
from pathlib import Path
import traceback
import numpy as np
from scipy.integrate import solve_ivp
from cleft_membrane_reference import evaluate
from cleft_trajectory_protocol import labels,group

ROOT=Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('trajectory_constitutive_reference',ROOT/'tools/analyse-cleft-coupling.py')
base=importlib.util.module_from_spec(spec);spec.loader.exec_module(base)


class Equation:
    def __init__(self,fixture,precision):
        self.fixture=deepcopy(fixture);self.precision=precision
        self.nodes=sorted(fixture['topology']['compartments'],key=lambda n:n['id'])
        self.state_labels,self.ledger_labels=labels(fixture);self.labels=self.state_labels+self.ledger_labels
        self.physical_count=len(self.state_labels);self.size=len(self.labels)
        states={s['id']:s for s in fixture['states']};inputs={g['id']:g for g in fixture['inputs']}
        initial=[fixture['hairMv']]
        for n in self.nodes:
            s=states[n['id']];g=inputs[n['id']]
            initial.extend(s[k] for k in ('cleftMv','calyxMv','potassiumMm','sodiumMm'))
            initial.extend(g['hairGates'][k] for k in ('potassiumLow','calcium','hcn1'))
            initial.extend(g['calyxGates'][k] for k in ('innerKv74','innerHcn2','outerKv74','outerHcn2'))
        self.initial=np.array(initial+[0.]*len(self.ledger_labels),dtype=np.float64)
        self.last_input=None

    def evaluation(self,y,forcing,precision=None):
        assert len(y)==self.size and np.isfinite(y).all()
        row=dict(self.fixture,hairMv=float(y[0]),bundleDisplacementNanometers=forcing['bundleDisplacementNanometers'],states=[],inputs=[])
        currents={p['id']:p['outgoingPa'] for p in forcing['axonCurrents']}
        assert set(currents)=={n['id'] for n in self.nodes}
        for i,n in enumerate(self.nodes):
            o=1+11*i;values=[float(v) for v in y[o:o+11]]
            assert values[2]>0 and values[3]>0,'Reference attempted nonpositive ion concentration; no clipping.'
            row['states'].append(dict(id=n['id'],**dict(zip(('cleftMv','calyxMv','potassiumMm','sodiumMm'),values[:4],strict=True))))
            row['inputs'].append(dict(id=n['id'],hairGates=dict(zip(('potassiumLow','calcium','hcn1'),values[4:7],strict=True)),
                calyxGates=dict(zip(('innerKv74','innerHcn2','outerKv74','outerHcn2'),values[7:11],strict=True)),axonOutgoingPa=currents[n['id']]))
        # A reference Newton trial may extend the smooth gate equations outside
        # the probability interval. No trial is clipped; all retained accepted
        # nodes and comparison samples must be physically admissible.
        self.last_input=row
        with localcontext() as ctx:
            ctx.prec=precision or self.precision
            return evaluate(row,base.edge,base.dense_solve)

    def rates(self,y,forcing):
        with localcontext() as ctx:
            ctx.prec=self.precision
            r=self.evaluation(y,forcing);spatial=r['coupling']['spatial']
            physical=[spatial['hairMvPerMs']]
            for m,s in zip(r['membranes'],spatial['compartments'],strict=True):
                assert m['id']==s['id']
                physical.extend(s[k] for k in ('cleftMvPerMs','calyxMvPerMs','potassiumMmPerMs','sodiumMmPerMs'))
                physical.extend(m['hairGateDerivatives'][k] for k in ('potassiumLowPerMs','calciumPerMs','hcn1PerMs'))
                physical.extend(m['calyxGateDerivatives'][k] for k in ('innerKv74PerMs','innerHcn2PerMs','outerKv74PerMs','outerHcn2PerMs'))
            ledgers=[r['metCurrentPa'],r['coupling']['endolymphHoldingCurrentPa'],r['coupling']['perilymph']['holdingCurrentPa']]
            faraday=Decimal.from_float(96.485)
            for m in r['membranes']:
                p=m['port'];ledgers.extend(p[k] for k in ('hairToCleftPa','calyxToCleftPa','calyxToPerilymphPa','calyxToAxonPa'))
                ledgers.extend(p[k]/faraday for k in ('hairPotassiumPa','hairSodiumPa','calyxPotassiumPa','calyxSodiumPa'))
            for e in spatial['transfers']:
                ledgers.extend(e['cleft'][k] for k in ('potassiumAttomolesPerMs','sodiumAttomolesPerMs','totalCurrentPa'))
                ledgers.append(e['calyxCurrentLeftToRightPa'])
            for f in r['coupling']['apexTransfers']:
                ledgers.extend(f['outgoing'][k] for k in ('potassiumAttomolesPerMs','sodiumAttomolesPerMs','totalCurrentPa'))
            result=np.array([float(v) for v in physical+ledgers],dtype=np.float64)
            assert result.shape==(self.size,) and np.isfinite(result).all();return result

    def jacobian(self,y,forcing):
        # Integral ledgers have no outgoing influence: their Jacobian columns
        # are exactly zero, rather than numerically perturbed zero columns.
        jac=np.zeros((self.size,self.size),dtype=np.float64)
        for column in range(self.physical_count):
            delta=np.ldexp(max(1.,abs(float(y[column]))),-20);a=y.copy();b=y.copy();a[column]+=delta;b[column]-=delta
            is_ion=column>0 and (column-1)%11 in (2,3)
            if is_ion and b[column]<=0:b=y.copy()
            jac[:,column]=(self.rates(a,forcing)-self.rates(b,forcing))/(a[column]-b[column])
        assert np.isfinite(jac).all();return jac

    def validate_samples(self,y):
        assert y.ndim==2 and y.shape[0]==self.size and np.isfinite(y).all()
        for i in range(len(self.nodes)):
            o=1+11*i
            assert (y[o+2:o+4]>0).all(),'Accepted reference ions are nonpositive.'
            assert ((y[o+4:o+11]>=0)&(y[o+4:o+11]<=1)).all(),'Accepted reference gate is outside [0,1].'


def save(path,value):
    with path.open('x',encoding='utf-8') as f:json.dump(value,f,indent=2,allow_nan=False)


def run_case(protocol,case,setting,output):
    output.mkdir();equation=Equation(case['fixture'],protocol['referenceDecimalPrecision']);y=equation.initial.copy()
    save(output/'input.json',dict(case=case,setting=setting,labels=equation.labels,initial=y.tolist(),
        decimalPrecision=equation.precision,explicitJacobian=True,ledgerJacobianColumnsExactlyZero=True,
        internalTrialGatesMayLeaveProbabilityDomain=True,acceptedNodesAndSamplesRequirePhysicalDomain=True))
    atol=np.array([protocol['referenceAtol'][group(label)]*setting['atolScale'] for label in equation.labels])
    all_ticks=[];all_values=[];metadata=[];start=0
    try:
        for index,forcing in enumerate(case['segments']):
            end=forcing['endTick'];left=start/protocol['ticksPerMs'];right=end/protocol['ticksPerMs']
            result=solve_ivp(lambda t,state:equation.rates(state,forcing),(left,right),y,method=setting['method'],
                rtol=setting['rtol'],atol=atol,jac=lambda t,state:equation.jacobian(state,forcing),dense_output=True,max_step=.05)
            # Retain unsuccessful solves too, before interpreting the result.
            np.savez(output/f'segment-{index}-nodes.npz',timeMs=result.t,values=result.y)
            item=dict(segment=index,startTick=start,endTick=end,success=bool(result.success),message=result.message,
                nfev=result.nfev,njev=result.njev,nlu=result.nlu,acceptedNodes=len(result.t))
            save(output/f'segment-{index}.json',item);metadata.append(item)
            assert result.success and result.t[-1]==right and result.sol is not None
            equation.validate_samples(result.y)
            ticks=np.arange(start,end+1,protocol['sampleTicks'],dtype=np.int64);samples=result.sol(ticks/protocol['ticksPerMs'])
            equation.validate_samples(samples)
            # Preserve separate high-precision RHS witnesses at both ends of
            # every reference segment; these are not candidate-derived inputs.
            witnesses=[]
            for endpoint in (0,-1):
                accepted_state=result.y[:,endpoint]
                values=[equation.evaluation(accepted_state,forcing,precision) for precision in protocol['referenceCheckPrecisions']]
                forty=equation.evaluation(accepted_state,forcing,protocol['referenceDecimalPrecision'])
                with localcontext() as context:
                    context.prec=90
                    base.compare_values(values[0],values[1],Decimal('1e-40'),Decimal('1e-40'))
                    base.compare_values(forty,values[1],Decimal('1e-25'),Decimal('1e-25'))
                witnesses.append(dict(tick=int(ticks[endpoint]),state=accepted_state.tolist(),
                    reference40=base.decimal_strings(forty),references=[base.decimal_strings(v) for v in values]))
            save(output/f'segment-{index}-precision.json',witnesses)
            if index:ticks=ticks[1:];samples=samples[:,1:]
            all_ticks.extend(ticks.tolist());all_values.extend(samples.T.tolist())
            y=result.y[:,-1].copy();start=end
        np.savez(output/'samples.npz',ticks=np.array(all_ticks,dtype=np.int64),values=np.array(all_values,dtype=np.float64))
        save(output/'results.json',dict(completed=True,segments=metadata,samples=len(all_ticks),allAcceptedNodesPhysicallyAdmissible=True,
            candidateValuesUsedForIntegration=False,stationaryInitializationQualified=False,physiologyQualified=False,avatarRecoveryAssessed=False))
    except Exception:
        save(output/'failure.json',dict(error=traceback.format_exc(),lastIndependentInput=equation.last_input,completedSegments=metadata))
        raise


def run(package,protocol):
    root=package/'reference-trajectories';root.mkdir()
    for case in protocol['cases']:
        for setting in protocol['referenceSettings']:
            name=case['key']+'-'+setting['key'];print('INDEPENDENT JOINT TRAJECTORY '+name,flush=True)
            run_case(protocol,case,setting,root/name)

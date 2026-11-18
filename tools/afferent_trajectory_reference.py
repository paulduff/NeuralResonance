"""Independent joint sensory/nerve ODE. Execute only in a visible study.

Uses the separately written Decimal derivative and Radau/BDF, never candidate
stages, saved candidate currents, equilibrium gates or native solver coefficients.
"""
from copy import deepcopy
from decimal import Decimal,localcontext
import json
import traceback
import numpy as np
from scipy.integrate import solve_ivp
from afferent_numerical_reference import joint
from afferent_numerical_analysis import strings
from afferent_trajectory_protocol import labels,group,GATES
from cleft_trajectory_reference import Equation as SensoryEquation
from cleft_rest_analysis import comparison,rates_from_saved_evaluation


def save(path,value):
    with path.open('x',encoding='utf-8') as stream:json.dump(value,stream,indent=2,allow_nan=False)


class Equation:
    def __init__(self,fixture,source,precision):
        self.fixture=deepcopy(fixture);self.source=deepcopy(source);self.precision=precision
        self.sensory=SensoryEquation(fixture['sensory'],precision)
        self.nodes=sorted(fixture['cableTopology']['nodes'],key=lambda n:n['id'])
        self.state_labels,self.ledger_labels=labels(fixture);self.labels=self.state_labels+self.ledger_labels
        self.physical_count=len(self.state_labels);self.size=len(self.labels)
        self.cable_start=self.sensory.physical_count
        states={s['id']:s for s in fixture['cableStates']}
        initial=self.sensory.initial[:self.cable_start].tolist()
        for n in self.nodes:
            s=states[n['id']];initial+=[s['potentialMv']]+[s['gates'][k] for k in GATES]
        self.initial=np.array(initial+[0.]*len(self.ledger_labels),dtype=np.float64)
        self.ion_columns=[i for i,n in enumerate(self.state_labels) if group(n)=='ion']
        self.gate_columns=[i for i,n in enumerate(self.state_labels) if group(n)=='gate']
        assert len(self.initial)==self.size and self.physical_count==self.cable_start+7*len(self.nodes)
        self.last_input=None

    def input_at(self,y,forcing):
        assert len(y)==self.size and np.isfinite(y).all()
        row=deepcopy(self.fixture);f=row['sensory'];f['hairMv']=float(y[0])
        f['bundleDisplacementNanometers']=forcing['bundleDisplacementNanometers'];f['states']=[];f['inputs']=[]
        for i,n in enumerate(self.sensory.nodes):
            o=1+11*i;v=[float(x) for x in y[o:o+11]]
            assert v[2]>0 and v[3]>0,'Reference trial has nonpositive ions; no clipping.'
            f['states'].append(dict(id=n['id'],**dict(zip(('cleftMv','calyxMv','potassiumMm','sodiumMm'),v[:4],strict=True))))
            f['inputs'].append(dict(id=n['id'],hairGates=dict(zip(('potassiumLow','calcium','hcn1'),v[4:7],strict=True)),
                calyxGates=dict(zip(('innerKv74','innerHcn2','outerKv74','outerHcn2'),v[7:11],strict=True)),axonOutgoingPa=0.))
        # The joint equation replaces this zero placeholder with its reciprocal
        # current from the actual same-state calyx and cable voltages.
        row['cableStates']=[]
        for i,n in enumerate(self.nodes):
            o=self.cable_start+7*i
            row['cableStates'].append(dict(id=n['id'],potentialMv=float(y[o]),gates=dict(zip(GATES,map(float,y[o+1:o+7]),strict=True))))
        # State-vector order is canonical; retain each fixture's declared list
        # order at its typed boundary. Identity, not list position, owns values.
        for target,original,key in ((f,self.fixture['sensory'],'states'),(f,self.fixture['sensory'],'inputs'),(row,self.fixture,'cableStates')):
            indexed={entry['id']:entry for entry in target[key]}
            target[key]=[indexed[entry['id']] for entry in original[key]]
        return row

    def evaluation(self,y,forcing,precision=None):
        row=self.input_at(y,forcing);self.last_input=row
        with localcontext() as context:
            context.prec=precision or self.precision
            return joint(row,self.source,allow_trial_gates=True)

    def rates(self,y,forcing):
        with localcontext() as context:
            context.prec=self.precision;r=self.evaluation(y,forcing);s=r['sensory'];c=r['cable']
            physical=rates_from_saved_evaluation(s)
            for expected,n in zip(self.nodes,c['nodes'],strict=True):
                assert n['id']==expected['id']
                physical+=[n['potentialMvPerMs']]+[n['membrane']['gateDerivatives'][k+'PerMs'] for k in GATES]
            ledgers=[s['metCurrentPa'],s['coupling']['endolymphHoldingCurrentPa'],s['coupling']['perilymph']['holdingCurrentPa']]
            faraday=Decimal.from_float(96.485)
            for m in s['membranes']:
                port=m['port'];ledgers.extend(port[k] for k in ('hairToCleftPa','calyxToCleftPa','calyxToPerilymphPa','calyxToAxonPa'))
                ledgers.extend(port[k]/faraday for k in ('hairPotassiumPa','hairSodiumPa','calyxPotassiumPa','calyxSodiumPa'))
            for edge in s['coupling']['spatial']['transfers']:
                ledgers.extend(edge['cleft'][k] for k in ('potassiumAttomolesPerMs','sodiumAttomolesPerMs','totalCurrentPa'))
                ledgers.append(edge['calyxCurrentLeftToRightPa'])
            for face in s['coupling']['apexTransfers']:
                ledgers.extend(face['outgoing'][k] for k in ('potassiumAttomolesPerMs','sodiumAttomolesPerMs','totalCurrentPa'))
            ledgers.extend(n['membraneOutwardPa'] for n in c['nodes'])
            ledgers.extend(e['leftToRightPa'] for e in c['edges'])
            ledgers.extend(t['nodeToTerminalPa'] for t in c['terminals'])
            result=np.array([float(x) for x in physical+ledgers],dtype=np.float64)
            assert result.shape==(self.size,) and np.isfinite(result).all();return result

    def jacobian(self,y,forcing):
        # Ledger columns are structurally zero. Perturbing zero columns via
        # SciPy's adaptive numerical Jacobian previously overflowed its factors.
        jac=np.zeros((self.size,self.size),dtype=np.float64)
        for column in range(self.physical_count):
            delta=np.ldexp(max(1.,abs(float(y[column]))),-20)
            a=y.copy();b=y.copy();a[column]+=delta;b[column]-=delta
            if column in self.ion_columns and b[column]<=0:b=y.copy()
            jac[:,column]=(self.rates(a,forcing)-self.rates(b,forcing))/(a[column]-b[column])
        assert np.isfinite(jac).all();return jac

    def validate_samples(self,y):
        assert y.ndim==2 and y.shape[0]==self.size and np.isfinite(y).all()
        assert (y[self.ion_columns]>0).all(),'Accepted reference ions are nonpositive.'
        assert ((y[self.gate_columns]>=0)&(y[self.gate_columns]<=1)).all(),'Accepted reference gates outside [0,1].'


def input_record(equation,case,setting,protocol):
    return dict(case=case,setting=setting,labels=equation.labels,initial=equation.initial.tolist(),decimalPrecision=equation.precision,
        explicitJacobian=True,ledgerJacobianColumnsExactlyZero=True,internalTrialGatesMayLeaveProbabilityDomain=True,
        acceptedNodesAndSamplesRequirePhysicalDomain=True,maximumStepMs=protocol['referenceMaximumStepMs'],fastTransientOnly=True)


def precision_witness(equation,state,forcing,tick,protocol):
    values=[equation.evaluation(state,forcing,p) for p in protocol['referenceCheckPrecisions']]
    working=equation.evaluation(state,forcing,protocol['referenceDecimalPrecision'])
    return dict(tick=int(tick),state=state.tolist(),reference40=strings(working),references=[strings(v) for v in values],
        precision=comparison(values[0],values[1],dict(absolute='1e-40',relative='1e-40')),
        workingPrecision=comparison(working,values[1],dict(absolute='1e-25',relative='1e-25')))


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
            candidateValuesUsedForIntegration=False,fastTransientOnly=True,stationaryInitializationQualified=False,
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
    save(root/'results.json',dict(completed=not failures,attempted=21,failedRuns=failures,fastTransientOnly=True))
    if failures:raise RuntimeError('Independent sensory nerve reference incomplete; all available prefixes retained.')

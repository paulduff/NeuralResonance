"""Full-population reference evaluation; original scalar equations remain the control.

Vectorize independent calcium operations and store the bounded central Jacobian
sparsely. No biological equation, trial extension, tolerance or ledger changes.
All execution belongs in the independent PowerShell study.
"""
import importlib.util
from pathlib import Path
import numpy as np
from scipy.integrate import solve_ivp
from scipy.sparse import csc_matrix

spec=importlib.util.spec_from_file_location('original_muscle_reference',Path(__file__).with_name('dynamic_muscle_reference.py'))
original=importlib.util.module_from_spec(spec);spec.loader.exec_module(original)


class PopulationReference(original.Reference):
    def __init__(self,protocol,case):
        super().__init__(protocol,case)
        self.p={key:np.array([r['calcium'][key] for r in self.rows]) for key in self.rows[0]['calcium']}
        self.phi_above=np.array([r['lengthGainAboveOptimal'] for r in self.rows])
        self.phi_below=np.array([r['lengthGainBelowOptimal'] for r in self.rows])
        self._active_owner=object();self._event_matrix=None

    def release_vector(self,t,active=None):
        owner=self.events if active is None else active
        if owner is not self._active_owner:
            matrix=np.full((self.n,max(map(len,owner),default=0)),np.inf)
            for i,times in enumerate(owner):matrix[i,:len(times)]=times
            self._event_matrix=matrix;self._active_owner=owner
        if self._event_matrix.shape[1]==0:return np.zeros(self.n)
        age=t-self._event_matrix
        # Negative ages are future/absent events. Evaluating their release at
        # zero avoids overflow; causal masking makes their contribution zero.
        causal=age>=0;age=np.maximum(age,0)
        return np.sum(np.where(causal,-np.expm1(-age/self.p['releaseRiseSeconds'][:,None])*
            np.exp(-age/self.p['releaseDecaySeconds'][:,None]),0),axis=1)

    def rhs(self,t,y,active=None):
        g=self.g;p=self.p;result=np.zeros_like(y);force=self.mechanics(t,y)
        if self.free:result[0],result[1]=y[1],force[-1]
        velocity=y[1]/g['optimalFiberLengthMeters']
        result[2]=(1-.35*(1-np.exp(-abs(velocity)/.1))-y[2])/.2
        length=y[0]/g['optimalFiberLengthMeters']
        gain=(length-1)*(self.phi_above if length>=1 else self.phi_below)
        if np.any(abs(gain)>=1):raise RuntimeError('Reference length parameters left their original domain.')
        c,b,a=y[3:-2].reshape(self.n,6)[:,:3].T
        release=p['releaseRatePerSecond']*(p['totalCalcium']-c-b)*self.release_vector(t,active)
        pump=p['pumpRatePerSecond']*c*(p['storeSites']-p['totalCalcium']+c+b)
        binding=(1-b)*(p['bindingRatePerSecond']*(1+gain)*c-
            p['unbindingRatePerSecond']*b/(1+p['cooperativity']*(1+gain)*a))
        power=abs(b*y[2]/(p['halfActivationBinding']*(1-gain)))**(p['hillExponent']*(1-gain))
        rates=result[3:-2].reshape(self.n,6)
        rates[:,0]=release-pump-binding;rates[:,1]=binding
        rates[:,2]=(power/(1+power)-a)/p['activationSeconds']
        rates[:,3]=release;rates[:,4]=pump;rates[:,5]=binding
        result[-2]=g['fiberMassKg']*result[1];result[-1]=result[-2]*y[1]
        return result

    def jacobian(self,t,y,active=None):
        # The cumulative ledgers never feed an equation. Their exact zero
        # columns need no evaluations; no nonzero value is thresholded away.
        result=np.zeros((len(y),len(y)));scales=np.ones(len(y))
        scales[0]=self.g['optimalFiberLengthMeters'];scales[1]=.1
        for col in [0,1,2]+[3+6*i+j for i in range(self.n) for j in range(3)]:
            h=1e-7*max(abs(y[col]),scales[col]);plus=y.copy();minus=y.copy()
            plus[col]+=h;minus[col]-=h
            result[:,col]=(self.rhs(t,plus,active)-self.rhs(t,minus,active))/(2*h)
        return csc_matrix(result)

    def solve(self,settings,grid):
        state=self.initial();trace=np.full((len(grid),len(state)),np.nan)
        self.reference_partial=dict(samples=trace,completedBoundarySeconds=float(grid[0]),
            internalTimes=np.array([grid[0]]),internalStates=state[None,:].copy())
        boundaries=np.unique(np.r_[grid[0],grid[-1],self.knots[:,0],*self.events])
        stats=dict(nfev=0,njev=0,nlu=0,acceptedInternalPoints=0,acceptedInternalDomainPassed=True,
            boundedCentralJacobian=True,sparseStorage=True,exactZeroLedgerColumns=True)
        for index,(left,right) in enumerate(zip(boundaries[:-1],boundaries[1:]),1):
            print(f"  reference interval {index}/{len(boundaries)-1}",flush=True)
            active=[times[times<=left] for times in self.events]
            rhs=lambda t,y:self.rhs(t,y,active)
            extra=dict(jac=lambda t,y:self.jacobian(t,y,active)) if settings['method']=='Radau' else {}
            answer=solve_ivp(rhs,(left,right),state,method=settings['method'],rtol=settings['rtol'],atol=settings['atol'],
                max_step=self.protocol['maximumReferenceStepSeconds'],dense_output=True,**extra)
            self.reference_partial['internalTimes']=answer.t;self.reference_partial['internalStates']=answer.y.T
            if not answer.success:raise RuntimeError(answer.message)
            chosen=(grid>=left)&((grid<right) if right<grid[-1] else (grid<=right))
            if np.any(chosen):trace[chosen]=answer.sol(grid[chosen]).T
            state=answer.y[:,-1];self.reference_partial['completedBoundarySeconds']=float(right)
            stats['acceptedInternalPoints']+=len(answer.t)
            stats['acceptedInternalDomainPassed']&=self.domain(answer.y.T)
            tendon=(np.array([self.length(t) for t in answer.t])-answer.y[0]*np.cos(self.g['pennationRadians']))/self.g['referenceTendonLengthMeters']
            stats['acceptedInternalDomainPassed']&=bool(np.min(tendon)>0)
            for key in ('nfev','njev','nlu'):stats[key]+=getattr(answer,key)
        if not np.isfinite(trace).all():raise RuntimeError('Reference samples missing or nonfinite.')
        stats['sampleDomainPassed']=self.domain(trace)
        return trace,np.array([self.mechanics(t,y) for t,y in zip(grid,trace)]),stats


def controls(protocols):
    """108 derivative and six bounded-Jacobian controls; no ODE integration."""
    field_rows=[];jacobian_rows=[]
    for protocol in protocols:
        for n in (1,2,147):
            case=dict(unitRows=list(range(n)),boundary='Free',initialFiberLengthMeters=protocol['geometry']['optimalFiberLengthMeters'],
                initialMusculotendonLengthMeters=protocol['initialMusculotendonLengthMeters'],
                lengthKnots=[dict(timeMs=0.,offsetMeters=0.),dict(timeMs=200.,offsetMeters=0.)],
                events=[dict(unitIndex=i,arrivalAtMs=t+.015625*(i%3)) for i in range(n) for t in (5.,15.,30.)])
            old=original.Reference(protocol,case);new=PopulationReference(protocol,case)
            for length in (.9,1.,1.1):
                for velocity in (-.1,0.,.1):
                    for yielding in (.8,1.):
                        y=old.initial();y[0]*=length;y[1]=velocity*protocol['geometry']['optimalFiberLengthMeters'];y[2]=yielding
                        for i in range(n):y[3+6*i:6+6*i]=(.02+.03*i/n,.05+.1*(i%7)/7,.1+.2*(i%11)/11)
                        a=old.rhs(.02,y);b=new.rhs(.02,y)
                        allowed=2e-12+2e-13*np.abs(a);ratio=float(np.max(abs(a-b)/allowed))
                        field_rows.append(dict(side=protocol['side'],units=n,length=length,velocity=velocity,yielding=yielding,maximumAllowanceFraction=ratio,passed=ratio<=1))
                        if (length,velocity,yielding)!=(1.,0.,1.):continue
                        jac=new.jacobian(.02,y).toarray();scales=np.ones(len(y));scales[0]=protocol['geometry']['optimalFiberLengthMeters'];scales[1]=.1;scales[-2:]=[.01,.001]
                        selected=sorted(set([0,1,2,len(y)-2,len(y)-1]+[3+6*i+j for i in sorted({0,n//2,n-1}) for j in range(6)]))
                        maximum=0.
                        for col in selected:
                            h=1e-7*max(abs(y[col]),scales[col]);plus=y.copy();minus=y.copy();plus[col]+=h;minus[col]-=h
                            independent=(old.rhs(.02,plus)-old.rhs(.02,minus))/(2*h)
                            maximum=max(maximum,float(np.max(abs(jac[:,col]-independent)/(1e-7+2e-8*abs(independent)))))
                        jacobian_rows.append(dict(side=protocol['side'],units=n,checkedColumns=selected,maximumAllowanceFraction=maximum,passed=maximum<=1))
    assert len(field_rows)==108 and len(jacobian_rows)==6
    return dict(completed=True,passed=all(r['passed'] for r in field_rows+jacobian_rows),fieldCases=field_rows,jacobianCases=jacobian_rows,
        fieldAbsoluteBound=2e-12,fieldRelativeBound=2e-13,jacobianAbsoluteBound=1e-7,jacobianRelativeBound=2e-8,odeExecuted=False)

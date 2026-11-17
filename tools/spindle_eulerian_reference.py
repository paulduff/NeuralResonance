# SPDX-License-Identifier: AGPL-3.0-only
"""Independent continuous-motion finite-volume intrafusal reference candidate.

Pinned kinetics: spindleModelTutorial 721ba03cb775261548766ed1349c4bf2e0e35385.
No retained-interval candidate code is imported. Upwind transport is diffusive:
spatial refinement is required before any reference-accuracy qualification.
No construction, derivative evaluation or integration occurs at import.
"""
import math
import numpy as np
from scipy.integrate import solve_ivp

VERSION = 'spindle-continuous-eulerian-reference-v1'
SUBDIVISIONS = (8, 16, 32, 64)
SETTINGS = (
    dict(label='dop853-strict',method='DOP853',rtol=1e-10,densityAtol=1e-12,scalarAtol=1e-13,maxStep=.000125,courantCap=.5),
    dict(label='dop853-tighter',method='DOP853',rtol=1e-12,densityAtol=1e-14,scalarAtol=1e-14,maxStep=.0000625,courantCap=.25),
    dict(label='rk45-tighter',method='RK45',rtol=1e-11,densityAtol=1e-13,scalarAtol=1e-14,maxStep=.0000625,courantCap=.25),
)
LIMITS = dict(analyticStateAbsolute=1e-9,conservationAbsolute=1e-10,domainAbsolute=1e-8,
    fixedReferenceStateAbsolute=1e-8,fixedReferenceStressAbsolutePa=.01,
    temporalPopulationL1=1e-8,temporalScalarAbsolute=1e-8,temporalStressAbsolutePa=.01,
    spatialPopulationL1=1e-5,spatialScalarAbsolute=1e-6,spatialStressAbsolutePa=.01,
    rangePopulationL1=1e-8,rangeScalarAbsolute=1e-8,rangeStressAbsolutePa=.01)


def protocol():
    return dict(version=VERSION,subdivisions=list(SUBDIVISIONS),settings=list(SETTINGS),criteria=LIMITS,
        sourceRateWidthNm=.5,halfRangesNm=[20.25,40.25],durationSeconds=.04,sampleIntervalSeconds=.00025,
        analyticControls=12,fixedCases=6,fixedReferenceContrasts=18,movingSolves=42,
        temporalContrasts=12,spatialContrasts=18,rangeContrasts=6,candidateContrasts=18,
        motionProtocols=['stretch','stretch-return','shorten-return'],fibers=['bag','chain'],
        equation='dn/dt + v dn/dx = f(x) D (A-B) - g(x) n; B=integral(n dx)',
        rateRefinement='repeat original 0.5 nm piecewise-constant rates; do not resample biology',
        advection='first-order upwind face flux, integrated simultaneously with chemistry',
        boundary='zero inflow; outward population flux transfers to detached myosin',
        candidateRerun=False,referenceAccuracyMustBeAssessed=True,
        slackAssessed=False,sensoryFiringAssessed=False,physiologyQualified=False,avatarRecoveryAssessed=False)


def overlap(length):
    if not math.isfinite(length): raise ValueError('Nonfinite length.')
    covered=1120.-(length-815.)
    fraction=min(1.,max(0.,covered/735.))
    if 1120.-length-80.>0: fraction=(735.-(1120.-length-80.))/735.
    if not 0 < fraction <= 1: raise ValueError('Pinned kinetics do not define zero/invalid overlap.')
    return fraction


def movement(name):
    if name=='stretch': return ((0.,.04,1250.,100.,6.5),)
    if name=='stretch-return': return ((0.,.02,1250.,200.,6.5),(.02,.04,1254.,-200.,6.5))
    if name=='shorten-return': return ((0.,.02,1250.,-200.,6.5),(.02,.04,1246.,200.,6.5))
    raise ValueError('Unknown prescribed motion.')


class EulerianPopulation:
    """State: D, N cell masses, inactive actin, active actin, left/right outflow."""
    def __init__(self,fiber,subdivision,half_range=20.25):
        if fiber not in ('bag','chain') or type(subdivision) is not int or subdivision<1 or subdivision>128:
            raise ValueError('Declare a supported fiber and positive bounded subdivision.')
        if half_range not in (20.25,40.25): raise ValueError('Declare the pinned narrow or wide rate domain.')
        rate_cells=round(2*half_range/.5);self.count=rate_cells*subdivision;self.dx=.5/subdivision
        self.edges=np.linspace(-half_range,half_range,self.count+1)
        self.centres=(self.edges[:-1]+self.edges[1:])/2
        self.rate_centres=-half_range+.25+.5*np.arange(rate_cells)
        f0,g0,self.slack,self.passive,self.off=(600.,40.,1050.,90.,20.) if fiber=='bag' else (400.,300.,1200.,250.,200.)
        x=self.rate_centres
        self.original_attach=np.minimum(f0*np.exp(-.01*x*x/(1e18*1.381e-23*288.)),5000.)
        coefficient=.2 if fiber=='bag' else np.where(x < -5.,.2,.4)
        self.original_detach=np.minimum(g0+coefficient*np.abs(x+5.)**3+(.5 if fiber=='bag' else 10.),5000.)
        self.attach=np.repeat(self.original_attach,subdivision)
        self.detach=np.repeat(self.original_detach,subdivision)
        self.on=8e7;self.subdivision=subdivision;self.fiber=fiber;self.half_range=half_range

    def initial(self):
        y=np.zeros(self.count+5);y[0]=y[-4]=1.;return y

    def rhs(self,segment):
        start,end,length,speed,pca=segment
        if not all(math.isfinite(x) for x in segment) or end<=start: raise ValueError('Invalid physical segment.')
        overlap(length);overlap(length+speed*(end-start))
        try: calcium=self.on*10.**(-pca)
        except OverflowError as error: raise ValueError('Calcium drive exceeds the numerical domain.') from error
        if not math.isfinite(calcium): raise ValueError('Calcium drive exceeds the numerical domain.')
        velocity=.5*speed
        def evaluate(t,y):
            masses=y[1:-4];bound=np.sum(masses);free=y[-3]-bound
            reaction=self.attach*self.dx*y[0]*free-self.detach*masses
            flux=np.zeros(self.count+1)
            if velocity>0: flux[1:]=velocity*masses/self.dx
            elif velocity<0: flux[:-1]=velocity*masses/self.dx
            left=-flux[0];right=flux[-1]
            fraction=overlap(length+speed*(t-start))
            activation=calcium*(fraction-y[-3])*(1+y[-3]/fraction)
            deactivation=self.off*free*(2-y[-3]/fraction)
            dy=np.empty_like(y);dy[1:-4]=reaction-np.diff(flux)
            dy[0]=-np.sum(reaction)+left+right
            dy[-4]=deactivation-activation;dy[-3]=activation-deactivation
            dy[-2]=left;dy[-1]=right
            return dy
        return evaluate

    def stress(self,y,lengths):
        values=np.asarray(y);cross=69000.*(values[...,1:-4]@(self.centres+2.5))
        return cross,cross+self.passive*(np.asarray(lengths)-self.slack)

    def original_bins(self,y):
        values=np.asarray(y);masses=values[...,1:-4].reshape(*values.shape[:-1],-1,self.subdivision).sum(axis=-1)
        return np.concatenate((values[...,:1],masses,values[...,-4:-2]),axis=-1)

    def accounting(self,times,states,lengths):
        t=np.asarray(times);y=np.asarray(states)
        if y.shape!=(len(t),self.count+5) or not np.isfinite(y).all(): raise ValueError('Incomplete/nonfinite saved physical state.')
        b=y[:,1:-4].sum(axis=1);fractions=np.array([overlap(x) for x in lengths])
        mass=float(np.max(abs(y[:,0]+b-1)));actin=float(np.max(abs(y[:,-4]+y[:,-3]-1)))
        domain=float(max(0.,-y.min(),np.max(b-y[:,-3]),np.max(y[:,-3]-fractions)))
        return dict(myosinResidual=mass,actinResidual=actin,domainViolation=domain,
            passed=bool(max(mass,actin)<=LIMITS['conservationAbsolute'] and domain<=LIMITS['domainAbsolute']))


def integrate(model,segments,times,setting,output,initial=None):
    """Retain every accepted solver node and sampled complete state, including failures."""
    output.mkdir();times=np.asarray(times,dtype=float)
    if len(times)<2 or not np.all(np.diff(times)>0) or times[0]!=segments[0][0] or times[-1]!=segments[-1][1]:
        raise ValueError('Sample times must cover the entire declared physical interval.')
    y=model.initial() if initial is None else np.array(initial,dtype=float,copy=True)
    if y.shape!=(model.count+5,): raise ValueError('Initial state dimension differs.')
    sampled=np.full((len(times),len(y)),np.nan);sampled[0]=y;filled=np.zeros(len(times),dtype=bool);filled[0]=True
    lengths=np.full(len(times),np.nan);lengths[0]=segments[0][2]
    reports=[];node_accounting=[];previous_end=times[0];previous_length=segments[0][2]
    for index,segment in enumerate(segments):
        start,end,length,speed,pca=segment
        if start!=previous_end or length!=previous_length: raise ValueError('Physical segments have a gap or geometry reset.')
        velocity=abs(.5*speed)
        max_step=min(setting['maxStep'],setting['courantCap']*model.dx/velocity) if velocity else setting['maxStep']
        atol=np.full(len(y),setting['scalarAtol']);atol[1:-4]=setting['densityAtol']*model.dx
        solution=solve_ivp(model.rhs(segment),(start,end),y,method=setting['method'],rtol=setting['rtol'],
            atol=atol,max_step=max_step,dense_output=True)
        np.savez_compressed(output/f'segment-{index}.npz',times=solution.t,states=solution.y.T,
            lengths=length+speed*(solution.t-start),initial=y)
        report=dict(index=index,startSeconds=start,endSeconds=end,initialLengthNm=length,lengthSpeedNmPerSecond=speed,
            pCa=pca,success=bool(solution.success),message=solution.message,nfev=solution.nfev,
            acceptedNodes=len(solution.t),maximumStepSeconds=max_step)
        reports.append(report)
        with (output/f'segment-{index}.json').open('x',encoding='utf-8') as stream:
            import json
            json.dump(report,stream,indent=2,allow_nan=False);stream.write('\n')
        if not solution.success or solution.t[-1]!=end or not np.isfinite(solution.y).all():
            raise RuntimeError('Continuous reference solve failed; accepted prefix retained without retry.')
        account=model.accounting(solution.t,solution.y.T,length+speed*(solution.t-start));node_accounting.append(account)
        if not account['passed']: raise RuntimeError('Accepted reference node violates accounting/domain; no clipping or normalization.')
        selected=(times>start)&(times<=end)
        if filled[selected].any(): raise RuntimeError('Repeated physical sample.')
        sampled[selected]=solution.sol(times[selected]).T;lengths[selected]=length+speed*(times[selected]-start);filled[selected]=True
        sampled[times==end]=solution.y[:,-1];y=solution.y[:,-1].copy()
        previous_end=end;previous_length=length+speed*(end-start)
    if not filled.all(): raise RuntimeError('Unfilled sample; do not concatenate an incomplete trajectory.')
    cross,total=model.stress(sampled,lengths)
    np.savez_compressed(output/'trace.npz',times=times,states=sampled,lengths=lengths,edges=model.edges,
        crossBridgeStressPa=cross,stressPa=total,originalBins=model.original_bins(sampled),
        originalAttach=model.original_attach,originalDetach=model.original_detach)
    accounting=model.accounting(times,sampled,lengths)
    if not accounting['passed']: raise RuntimeError('Interpolated reference sample violates accounting/domain; preserve the trace.')
    return dict(completed=True,settings=setting,fiber=model.fiber,subdivision=model.subdivision,
        halfRangeNm=model.half_range,finiteVolumeCells=model.count,segments=reports,
        sampledAccounting=accounting,acceptedNodeAccounting=node_accounting,samples=len(times))

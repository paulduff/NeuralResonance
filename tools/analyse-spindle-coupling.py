# SPDX-License-Identifier: AGPL-3.0-only
"""Run ONLY in the independent visible PowerShell window; no work at import."""
import argparse
import importlib.util
import json
import os
from pathlib import Path
import time
import traceback

for key in ('OMP_NUM_THREADS', 'OPENBLAS_NUM_THREADS', 'MKL_NUM_THREADS', 'NUMEXPR_NUM_THREADS'):
    os.environ[key] = '1'
import numpy as np
from scipy.integrate import solve_ivp
from spindle_coupled_population import CoupledPopulation, LIMITS, overlap, protocol


def read(path): return json.loads(path.read_text(encoding='utf-8-sig'))


def save(path, value):
    def encode(x):
        if isinstance(x, np.ndarray): return x.tolist()
        if isinstance(x, np.generic): return x.item()
        raise TypeError(type(x).__name__)
    with path.open('x', encoding='utf-8') as stream:
        json.dump(value, stream, indent=2, default=encode, allow_nan=False); stream.write('\n')


def environment(package):
    spec = importlib.util.spec_from_file_location('coupling_dependencies', package/'source/tools/golgi-reference-resume.py')
    module = importlib.util.module_from_spec(spec); spec.loader.exec_module(module)
    return module.environment()


def preflight(package):
    assert read(package/'coupling-input.json') == protocol()
    assert read(package/'reference/fixed-review.json')['capturedFixedLengthAccuracyPassed']
    assert read(package/'reference/transport-review.json')['savedRecordsReconciled']
    current = environment(package)
    assert current == read(package/'preparation-environment.json')
    save(package/'reference-preflight.json', dict(environment=current, modelExecuted=False))


def postflight(package):
    current = environment(package)
    unchanged = current == read(package/'reference-preflight.json')['environment'] == read(package/'preparation-environment.json')
    save(package/'reference-postflight.json', dict(environment=current, unchanged=unchanged, modelExecuted=False))
    if not unchanged: raise RuntimeError('Numerical dependencies changed during the study.')


def seeded():
    c = CoupledPopulation('bag'); c.density[40] = .4
    c.detached = .8; c.active = .4; c.inactive = .6
    return c


def fingerprint(c):
    return (c.origin.tobytes(), c.density.tobytes(), json.dumps(c.summary(), sort_keys=True))


def shape_l1(first_faces,first_density,second_faces,second_density):
    faces=np.union1d(first_faces,second_faces)
    def sample(edges,density):
        indices=np.searchsorted(edges,faces[:-1],side='right')-1
        valid=(indices>=0)&(indices<len(density)); result=np.zeros(len(faces)-1)
        result[valid]=density[indices[valid]]
        return result
    return float(np.dot(abs(sample(first_faces,first_density)-sample(second_faces,second_density)),np.diff(faces)))


def saved_shape_difference(root,first_label,second_label,stride):
    # Compare the complete piecewise distributions, not just their bin totals.
    with np.load(root/(first_label+'.npz'),allow_pickle=False) as a, np.load(root/(second_label+'.npz'),allow_pickle=False) as b:
        first={k:a[k] for k in ('originFaces','densityPerNm','faceOffsets','densityOffsets')}
        second={k:b[k] for k in ('originFaces','densityPerNm','faceOffsets','densityOffsets')}
    def state(raw,i):
        return (raw['originFaces'][raw['faceOffsets'][i]:raw['faceOffsets'][i+1]],
                raw['densityPerNm'][raw['densityOffsets'][i]:raw['densityOffsets'][i+1]])
    count=len(first['faceOffsets'])-1
    assert (count-1)*stride==len(second['faceOffsets'])-2
    errors=[shape_l1(*state(first,i),*state(second,i*stride)) for i in range(count)]
    return dict(maximumPopulationL1=max(errors),endpointPopulationL1=errors)


def controls(package):
    root = package/'controls'; root.mkdir(); records = []
    def record(label, c, error=0., bound=LIMITS['analyticStateAbsolute'], details=None):
        report = dict(label=label, error=float(error), bound=bound, passed=bool(error<=bound), state=c.summary(), details=details)
        save(root/(label+'.json'), report)
        np.savez_compressed(root/(label+'.npz'), originNm=c.origin, densityPerNm=c.density, physicalState=c.physical_state())
        records.append(report)
        if not report['passed']: raise RuntimeError('Control failed: '+label)
    c=seeded(); before=fingerprint(c); c.react(0.,6.5)
    record('zero-duration',c,0 if fingerprint(c)==before else 1)
    c=CoupledPopulation('bag'); c.attach[:]=0.; c.detach[:]=0.; c.off=0.
    c.react(.01,6.5); wanted=overlap(1250.)*np.tanh(c.on*10**-6.5*.01)
    record('actin-only',c,max(abs(c.active-wanted),abs(c.inactive-(1-wanted))))
    c=seeded(); c.attach[:]=0.; c.on=c.off=0.; before=c.density.copy()
    c.react(.001,6.5); expected=before*np.exp(-c.detach*.001)
    record('detachment-only',c,max(np.max(abs(c.density-expected)),abs(c.detached-(1-np.dot(expected,np.diff(c.edges))))))
    c=CoupledPopulation('bag'); c.active=.4; c.inactive=.6; c.detach[:]=0.; c.on=c.off=0.
    total=np.dot(c.attach,np.diff(c.edges)); decay=np.exp(-total*.6*.001)
    bound=.4*(1-decay)/(1-.4*decay); expected=c.attach/total*bound
    c.react(.001,6.5)
    record('attachment-only',c,max(np.max(abs(c.density-expected)),abs(c.detached-(1-bound)),abs(c.active-.4)))
    c=seeded(); c.move_to_length(1250.25); c.attach[:]=0.; c.on=c.off=0.
    faces=np.union1d(c.origin,c.edges-c.position)
    # Independent interval intersection for the old shape, followed by exact
    # exponential survival at each piecewise-constant rate.
    old=np.array([sum(d*max(0.,min(b,r)-max(a,l)) for l,r,d in zip(c.origin[:-1],c.origin[1:],c.density))/(b-a)
                  for a,b in zip(faces[:-1],faces[1:])])
    indices=np.searchsorted(c.edges-c.position,faces[:-1],side='right')-1
    expected=old*np.exp(-c.detach[indices]*.001)
    c.react(.001,6.5)
    record('shifted-detachment',c,max(np.max(abs(c.density-expected)),np.max(abs(c.origin-faces))))
    c=seeded(); c.move_to_length(1250.25)
    faces=np.union1d(c.origin,c.edges-c.position); widths=np.diff(faces)
    initial_density=np.array([sum(d*max(0.,min(b,r)-max(a,l)) for l,r,d in zip(c.origin[:-1],c.origin[1:],c.density))/(b-a)
                              for a,b in zip(faces[:-1],faces[1:])])
    indices=np.searchsorted(c.edges-c.position,faces[:-1],side='right')-1
    f=c.attach[indices].copy(); g=c.detach[indices].copy(); fraction=overlap(c.length)
    def full_density_rhs(t,y):
        bound=np.dot(widths,y[1:-2]); free=y[-1]-bound
        flux=f*y[0]*free-g*y[1:-2]
        activation=8e7*10**-6.5*(fraction-y[-1])*(1+y[-1]/fraction)-20*free*(2-y[-1]/fraction)
        return np.concatenate(([-np.dot(widths,flux)],flux,[-activation,activation]))
    initial=np.concatenate(([c.detached],initial_density,[c.inactive,c.active]))
    reference=solve_ivp(full_density_rhs,(0.,.001),initial,method='Radau',rtol=1e-12,atol=1e-14,max_step=.000125)
    np.savez_compressed(root/'full-density-reference.npz',times=reference.t,states=reference.y.T,originNm=faces)
    assert reference.success and reference.t[-1]==.001
    c.react(.001,6.5); actual=np.concatenate(([c.detached],c.density,[c.inactive,c.active]))
    record('birth-preserves-old-shape',c,np.max(abs(actual-reference.y[:,-1])))
    c=CoupledPopulation('bag'); c.density[-1]=.4; c.detached=.8; c.active=.4; c.inactive=.6
    c.move_to_length(1250.5)
    record('boundary-transfer',c,max(abs(c.summary()['bound']-.1),abs(c.detached-.9),abs(c.right_outflow-.1)))
    c.move_to_length(1250.)
    record('boundary-return',c,max(abs(c.summary()['bound']-.1),abs(c.detached-.9),abs(c.right_outflow-.1)))
    before=fingerprint(c); observed=c.observe(np.linspace(-40.,40.,1001)); observed[:]=999.
    hidden_shape=shape_l1(np.array([0.,.25,.5]),np.array([.8,0.]),np.array([0.,.5]),np.array([.4]))
    record('observation-purity',c,max(0 if before==fingerprint(c) else 1,abs(hidden_shape-.2)))
    c=CoupledPopulation('chain'); c.move_to_length(1254.)
    record('passive-force',c,abs(c.summary()['stressPa']-13500.),LIMITS['analyticForceAbsolutePa'])
    c=seeded(); before=fingerprint(c); rejected=0
    for action in (lambda:c.move_to_length(2000.),lambda:c.react(-1.,6.5),lambda:c.observe([1.,1.])):
        try: action()
        except ValueError: rejected+=1
    record('invalid-input',c,0 if rejected==3 and fingerprint(c)==before else 1)
    c=CoupledPopulation('bag',maximum_intervals=82); c.move_to_length(1250.25); before=fingerprint(c)
    rejected=False
    try: c.react(.001,6.5)
    except RuntimeError as error: rejected='capacity' in str(error)
    record('capacity-rejection',c,0 if rejected and fingerprint(c)==before else 1)
    assert len(records)==12
    save(package/'controls-analysis.json',dict(completed=True,passed=True,cases=records))


class Recorder:
    def __init__(self,c):
        self.c=c; self.records=[]; self.states=[]; self.faces=[]; self.densities=[]
        self.offsets=[0]; self.density_offsets=[0]; self.internal=[]; self.internal_offsets=[0]; self.reactions=[]
        self.capture(0.)

    def capture(self,t):
        self.records.append(dict(timeSeconds=t,**self.c.summary())); self.states.append(self.c.physical_state())
        self.faces.append(self.c.origin.copy()); self.densities.append(self.c.density.copy())
        self.offsets.append(self.offsets[-1]+len(self.c.origin)); self.density_offsets.append(self.density_offsets[-1]+len(self.c.density))

    def reaction(self,dt,pca,owner_time,phase):
        self.c.react(dt,pca)
        r=self.c.last_reaction
        self.internal.append(np.column_stack((r['times'],r['physicalStates'])))
        self.internal_offsets.append(self.internal_offsets[-1]+len(r['times']))
        self.reactions.append(dict(ownerTimeSeconds=owner_time,phase=phase,lengthNm=r['lengthNm'],durationSeconds=dt,pCa=pca))

    def write(self,root,label):
        np.savez_compressed(root/(label+'.npz'), states=np.asarray(self.states),
            originFaces=np.concatenate(self.faces),densityPerNm=np.concatenate(self.densities),
            faceOffsets=self.offsets,densityOffsets=self.density_offsets,
            internal=np.concatenate(self.internal) if self.internal else np.empty((0,len(self.states[0])+1)),
            internalOffsets=self.internal_offsets)
        save(root/(label+'.json'),dict(endpoints=self.records,reactions=self.reactions))


def accounting(records):
    ledger=max(max(abs(r['myosinResidual']),abs(r['actinResidual'])) for r in records)
    domain=max(max(0.,-r['minimumDensity'],-r['detached'],-r['active'],-r['inactive'],r['bound']-r['active'],r['active']-r['overlap']) for r in records)
    return dict(maximumLedgerError=ledger,maximumDomainViolation=domain,
        passed=ledger<=LIMITS['conservationAbsolute'] and domain<=LIMITS['domainAbsolute'])


def run_case(root,label,fiber,half,step_us,duration,length_at,pca_at):
    c=CoupledPopulation(fiber,half_range=half); rec=Recorder(c); start=time.perf_counter(); failed=None
    try:
        count=round(duration*1e6/step_us); dt=step_us/1e6
        for i in range(count):
            pca=pca_at(i*step_us); next_time=(i+1)*dt
            rec.reaction(dt/2,pca,i*dt,'before-motion')
            c.move_to_length(length_at((i+1)*step_us))
            rec.reaction(dt/2,pca,next_time,'after-motion')
            rec.capture(next_time)
    except Exception:
        failed=traceback.format_exc()
        # Preserve the state after any already-completed operator and the failed
        # solver's internal prefix, without resetting the case or retrying it.
        np.savez_compressed(root/(label+'-failure-state.npz'),originFaces=c.origin,densityPerNm=c.density,physicalState=c.physical_state())
        save(root/(label+'-failure.json'),dict(error=failed,current=c.summary()))
        if c.last_reaction is not None:
            r=c.last_reaction
            np.savez_compressed(root/(label+'-last-reaction.npz'),times=r['times'],birthStates=r['birthStates'],physicalStates=r['physicalStates'],oldBinMasses=r['oldBinMasses'])
    finally:
        rec.write(root,label)
    result=dict(label=label,fiber=fiber,halfRangeNm=half,stepUs=step_us,completed=failed is None,
        endpoints=len(rec.records),elapsedSeconds=time.perf_counter()-start,peakIntervals=c.peak_intervals,
        rhsEvaluations=c.evaluations,accounting=accounting(rec.records),final=c.summary())
    save(root/(label+'-summary.json'),result)
    return result,rec


def fixed(package):
    assert read(package/'controls-analysis.json')['passed']
    root=package/'fixed'; root.mkdir(); cases=[]; comparisons=[]
    for fiber in ('bag','chain'):
        for name in ('quiet-hold','active-hold','drive-on-off'):
            for step in (1000,500,250):
                label=f'{fiber}-{name}-{step}us'; print('Fixed chemistry '+label,flush=True)
                pca=lambda us: 9. if name=='quiet-hold' or (name=='drive-on-off' and not 20000<=us<80000) else 6.5
                case,rec=run_case(root,label,fiber,20.25,step,.1,lambda us:1250.,pca); cases.append(case)
                if not case['completed']: continue
                times=np.array([r['timeSeconds'] for r in rec.records]); states=np.asarray(rec.states)
                for setting in ('dop853-strict','dop853-tighter','radau-strict'):
                    with np.load(package/'reference/fixed'/f'{fiber}-{name}-{setting}.npz',allow_pickle=False) as raw:
                        indices=np.searchsorted(raw['times'],times); assert np.array_equal(raw['times'][indices],times)
                        ref=raw['states'][indices]
                    error=float(np.max(abs(states-ref)))
                    reference_force=69000.*(ref[:,1:-2]@(np.arange(-20.,20.5,.5)+2.5))
                    actual=np.array([r['crossBridgeStressPa'] for r in rec.records]); force=float(np.max(abs(reference_force-actual)))
                    comparisons.append(dict(label=label,reference=setting,maximumStateDifference=error,maximumForceDifferencePa=force,
                        passed=error<=LIMITS['fixedStateAbsolute'] and force<=LIMITS['fixedForceAbsolutePa']))
    passed=len(cases)==18 and len(comparisons)==54 and all(c['completed'] and c['accounting']['passed'] for c in cases) and all(c['passed'] for c in comparisons)
    save(package/'fixed-analysis.json',dict(completed=len(cases)==18,passed=passed,cases=cases,comparisons=comparisons))
    if not passed: raise RuntimeError('Fixed chemistry comparison failed; preserve original bounds.')


def moving(package):
    assert read(package/'fixed-analysis.json')['passed']
    root=package/'moving'; root.mkdir(); cases=[]; saved={}
    for fiber in ('bag','chain'):
        for name in ('stretch','stretch-return','shorten-return'):
            for half in (20.25,40.25):
                for step in (1000,500,250):
                    label=f'{fiber}-{name}-range{half}-{step}us'; print('Moving chemistry '+label,flush=True)
                    def length(us):
                        excursion=4.*us/40000 if name=='stretch' else 4.*min(us,40000-us)/20000
                        return 1250.+(-excursion if name=='shorten-return' else excursion)
                    case,rec=run_case(root,label,fiber,half,step,.04,length,lambda us:6.5)
                    cases.append(case); saved[(fiber,name,half,step)]=(case,np.asarray(rec.states),rec.records)
    temporal=[]; ranges=[]
    for fiber in ('bag','chain'):
        for name in ('stretch','stretch-return','shorten-return'):
            for half in (20.25,40.25):
                fine,states,records=saved[(fiber,name,half,250)]
                for step in (1000,500):
                    coarse,old,rows=saved[(fiber,name,half,step)]
                    if not (fine['completed'] and coarse['completed']): continue
                    stride=step//250
                    assert [r['displacementNm'] for r in rows]==[r['displacementNm'] for r in records[::stride]]
                    temporal.append(dict(fiber=fiber,protocol=name,halfRangeNm=half,coarseStepUs=step,fineStepUs=250,
                        **saved_shape_difference(root,coarse['label'],fine['label'],stride),
                        maximumObservedBinL1=float(np.max(np.sum(abs(old[:,1:-2]-states[::stride,1:-2]),axis=1))),
                        maximumScalarDifference=float(np.max(abs(old[:,[0,-2,-1]]-states[::stride,[0,-2,-1]]))),
                        maximumStressDifferencePa=max(abs(a['stressPa']-b['stressPa']) for a,b in zip(rows,records[::stride]))))
            for step in (1000,500,250):
                small,a,ra=saved[(fiber,name,20.25,step)]; large,b,rb=saved[(fiber,name,40.25,step)]
                if not(small['completed'] and large['completed']): continue
                assert [r['displacementNm'] for r in ra]==[r['displacementNm'] for r in rb]
                ranges.append(dict(fiber=fiber,protocol=name,stepUs=step,
                    **saved_shape_difference(root,small['label'],large['label'],1),
                    maximumScalarDifference=float(np.max(abs(a[:,[0,-2,-1]]-b[:,[0,-2,-1]]))),
                    maximumStressDifferencePa=max(abs(x['stressPa']-y['stressPa']) for x,y in zip(ra,rb)),
                    narrowOutflow=ra[-1]['leftNumericalOutflow']+ra[-1]['rightNumericalOutflow'],
                    wideOutflow=rb[-1]['leftNumericalOutflow']+rb[-1]['rightNumericalOutflow']))
    complete=len(cases)==36 and len(temporal)==24 and len(ranges)==18 and all(c['completed'] for c in cases)
    passed=complete and all(c['accounting']['passed'] for c in cases)
    save(package/'moving-analysis.json',dict(completed=complete,accountingPassed=passed,cases=cases,
        temporalDifferences=temporal,rangeDifferences=ranges,movingIndependentAccuracyQualified=False,
        temporalOrSpatialConvergenceQualified=False,timingIsBenchmark=False))
    if not passed: raise RuntimeError('Moving chemistry incomplete or accounting failed; preserve the captured prefix.')


def summarise(package):
    a,b,c=(read(package/(name+'-analysis.json')) for name in ('controls','fixed','moving'))
    assert a['passed'] and b['passed'] and c['accountingPassed']
    save(package/'coupling-analysis.json',dict(completed=True,componentChecksPassed=True,
        analyticControls=len(a['cases']),fixedCases=len(b['cases']),fixedReferenceComparisons=len(b['comparisons']),
        movingCases=len(c['cases']),movingTemporalContrasts=len(c['temporalDifferences']),movingRangeContrasts=len(c['rangeDifferences']),
        movingIndependentAccuracyQualified=False,temporalOrSpatialConvergenceQualified=False,
        slackTrajectoryAssessed=False,sensoryFiringAssessed=False,physiologyQualified=False,avatarRecoveryAssessed=False))


if __name__=='__main__':
    if not __debug__: raise RuntimeError('Assertions must remain enabled.')
    p=argparse.ArgumentParser(); p.add_argument('package',type=Path)
    p.add_argument('--stage',required=True,choices=['preflight','controls','fixed','moving','summarise','postflight'])
    args=p.parse_args(); globals()[args.stage](args.package.resolve())

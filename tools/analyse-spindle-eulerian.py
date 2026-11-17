# SPDX-License-Identifier: AGPL-3.0-only
"""Execute only inside the separate visible PowerShell experiment."""
import argparse
import importlib.util
import json
import math
import os
from pathlib import Path
import traceback

for key in ('OMP_NUM_THREADS','OPENBLAS_NUM_THREADS','MKL_NUM_THREADS','NUMEXPR_NUM_THREADS'): os.environ[key]='1'
import numpy as np
from scipy.special import gammaln
from scipy.io import loadmat
from spindle_eulerian_reference import EulerianPopulation, SETTINGS, SUBDIVISIONS, LIMITS, protocol, movement, overlap, integrate


def read(path): return json.loads(path.read_text(encoding='utf-8-sig'))


def save(path,value):
    def convert(x):
        if isinstance(x,np.ndarray): return x.tolist()
        if isinstance(x,np.generic): return x.item()
        raise TypeError(type(x).__name__)
    with path.open('x',encoding='utf-8') as stream:
        json.dump(value,stream,indent=2,default=convert,allow_nan=False);stream.write('\n')


def environment(package):
    spec=importlib.util.spec_from_file_location('eulerian_dependencies',package/'source/tools/golgi-reference-resume.py')
    module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module);return module.environment()


def preflight(package):
    assert read(package/'eulerian-input.json')==protocol()
    review=read(package/'reference/coupling-review.json')
    assert review['operatorReportedCompletion'] and review['savedRecordsReconciled'] and review['movingCases']==36
    current=environment(package);assert current==read(package/'preparation-environment.json')
    save(package/'reference-preflight.json',dict(environment=current,modelsExecuted=False))


def postflight(package):
    current=environment(package)
    unchanged=current==read(package/'preparation-environment.json')==read(package/'reference-preflight.json')['environment']
    save(package/'reference-postflight.json',dict(environment=current,unchanged=unchanged,modelsExecuted=False))
    if not unchanged: raise RuntimeError('Numerical dependencies changed during the study.')


def solve_case(model,segments,times,setting,folder,initial=None):
    try:
        result=integrate(model,segments,times,setting,folder,initial)
        save(folder/'result.json',result);return result
    except Exception:
        folder.mkdir(parents=True,exist_ok=True)
        save(folder/'failure.json',dict(error=traceback.format_exc(),modelVersion=protocol()['version'],settings=setting))
        raise


def poisson_shift(masses,rate_seconds,direction):
    if rate_seconds==0: return masses.copy()
    k=np.arange(len(masses));probability=np.exp(-rate_seconds+k*np.log(rate_seconds)-gammaln(k+1))
    if direction>0: return np.convolve(masses,probability)[:len(masses)]
    return np.convolve(masses[::-1],probability)[:len(masses)][::-1]


def controls(package):
    root=package/'controls';root.mkdir();reports=[];times=np.linspace(0,.01,81)
    def record(label,error,details=None):
        row=dict(label=label,maximumError=float(error),bound=LIMITS['analyticStateAbsolute'],
                 passed=bool(error<=LIMITS['analyticStateAbsolute']),details=details)
        save(root/(label+'.json'),row);reports.append(row)
        if not row['passed']: raise RuntimeError('Eulerian reference control failed: '+label)
    for fiber in ('bag','chain'):
        for kind in ('detachment','actin'):
            model=EulerianPopulation(fiber,2);model.attach[:]=0;model.off=0
            y=model.initial();expected=np.tile(y,(len(times),1))
            if kind=='detachment':
                model.on=0;y[1:-4]=.2/model.count;y[0]=.8;y[-3]=.4;y[-4]=.6
                expected=np.tile(y,(len(times),1));expected[:,1:-4]=y[1:-4]*np.exp(-times[:,None]*model.detach)
                expected[:,0]=1-expected[:,1:-4].sum(axis=1)
            else:
                model.detach[:]=0
                expected[:,-3]=overlap(1250.)*np.tanh(8e7*10**-6.5*times);expected[:,-4]=1-expected[:,-3]
            folder=root/(fiber+'-'+kind)
            solve_case(model,((0.,.01,1250.,0.,6.5),),times,SETTINGS[1],folder,y)
            with np.load(folder/'trace.npz') as raw: actual=raw['states']
            np.savez_compressed(folder/'analytic.npz',times=times,states=expected)
            record(fiber+'-'+kind,np.max(abs(actual-expected)))
    for direction in (-1,1):
        model=EulerianPopulation('bag',2);model.attach[:]=model.detach[:]=0;model.on=model.off=0
        y=model.initial();index=3 if direction<0 else model.count-4
        y[1+index]=.2;y[0]=.8;y[-3]=.4;y[-4]=.6
        expected=[];continuum=[]
        for t in times:
            masses=poisson_shift(y[1:-4],20*t/model.dx,direction);row=y.copy();row[1:-4]=masses
            row[0]=1-masses.sum();row[-2 if direction<0 else -1]=.2-masses.sum();expected.append(row)
            left=model.edges[index]+direction*20*t;right=left+model.dx
            continuum.append(.2/model.dx*np.maximum(0.,np.minimum(model.edges[1:],right)-np.maximum(model.edges[:-1],left)))
        expected=np.asarray(expected);folder=root/('upwind-'+str(direction))
        solve_case(model,((0.,.01,1250.,direction*40.,6.5),),times,SETTINGS[1],folder,y)
        with np.load(folder/'trace.npz') as raw: actual=raw['states']
        np.savez_compressed(folder/'analytic.npz',times=times,states=expected,continuumCellMasses=continuum)
        record('upwind-'+str(direction),np.max(abs(actual-expected)),dict(
            maximumContinuumTransportBinL1=float(np.max(np.sum(abs(expected[:,1:-4]-continuum),axis=1))),
            exactForSemidiscreteUpwindOnly=True,continuumTransportExact=False))
    model=EulerianPopulation('bag',2);model.attach[:]=model.detach[:]=0;model.on=model.off=0
    y=model.initial();y[-5]=.2;y[0]=.8;y[-3]=.4;y[-4]=.6
    first=poisson_shift(y[1:-4],20*.005/model.dx,1)
    last=poisson_shift(first,20*.005/model.dx,-1);expected=y.copy();expected[1:-4]=last
    expected[0]=1-last.sum();expected[-1]=.2-first.sum();expected[-2]=first.sum()-last.sum()
    folder=root/'outflow-return'
    solve_case(model,((0.,.005,1250.,40.,6.5),(.005,.01,1250.2,-40.,6.5)),times,SETTINGS[1],folder,y)
    with np.load(folder/'trace.npz') as raw: actual=raw['states'][-1]
    np.savez_compressed(folder/'analytic.npz',finalState=expected)
    record('outflow-return',np.max(abs(actual-expected)))
    model=EulerianPopulation('bag',2);model.detach[:]=0;model.on=model.off=0
    y=model.initial();y[-3]=.4;y[-4]=.6
    total=np.sum(model.attach)*model.dx;decay=np.exp(-.6*total*times)
    bound=.4*(1-decay)/(1-.4*decay);expected=np.tile(y,(len(times),1))
    expected[:,1:-4]=bound[:,None]*model.attach*model.dx/total;expected[:,0]=1-bound
    folder=root/'attachment'
    solve_case(model,((0.,.01,1250.,0.,6.5),),times,SETTINGS[1],folder,y)
    with np.load(folder/'trace.npz') as raw: actual=raw['states']
    np.savez_compressed(folder/'analytic.npz',states=expected,times=times)
    record('attachment',np.max(abs(actual-expected)))
    for fiber in ('bag','chain'):
        original=loadmat(package/'reference/constructor'/f'{fiber}.mat',simplify_cells=True)['result']['parameters']
        discrepancies=[]
        for subdivision in SUBDIVISIONS:
            model=EulerianPopulation(fiber,subdivision)
            discrepancies += [np.max(abs(model.original_attach-np.asarray(original['forward_rates'])))/5000,
                np.max(abs(model.original_detach-np.asarray(original['reverse_rates'])))/5000,
                np.max(abs(model.attach.reshape(-1,subdivision)-model.original_attach[:,None])),
                np.max(abs(model.detach.reshape(-1,subdivision)-model.original_detach[:,None]))]
        record(fiber+'-rate-inheritance',max(discrepancies),dict(reference='pinned native MAT constructor parameters'))
    errors=[]
    for fiber in ('bag','chain'):
        model=EulerianPopulation(fiber,2);y=model.initial();y[1:-4]=.2/model.count;y[0]=.8;y[-3]=.4;y[-4]=.6
        for speed in (-200.,0.,200.):
            dy=model.rhs((0.,.01,1250.,speed,6.5))(.005,y)
            errors += [abs(dy[0]+dy[1:-4].sum()),abs(dy[-4]+dy[-3]),max(0.,-dy[-2],-dy[-1])]
    record('independent-ledgers',max(errors))
    rejected=0
    for call in (lambda:EulerianPopulation('other',8),lambda:EulerianPopulation('bag',0),
                 lambda:EulerianPopulation('bag',8,21.),lambda:overlap(2000.),
                 lambda:model.rhs((0.,0.,1250.,0.,6.5)),lambda:model.rhs((0.,.01,1250.,0.,-1000.))):
        try: call()
        except ValueError: rejected+=1
    record('invalid-input',0 if rejected==6 else 1)
    assert len(reports)==12
    save(package/'controls-analysis.json',dict(completed=True,passed=True,cases=reports))


def fixed(package):
    root=package/'fixed';root.mkdir();cases=[];comparisons=[]
    for fiber in ('bag','chain'):
        for name in ('quiet-hold','active-hold','drive-on-off'):
            intervals=((0.,.1,9.),) if name=='quiet-hold' else ((0.,.1,6.5),) if name=='active-hold' else ((0.,.02,9.),(.02,.08,6.5),(.08,.1,9.))
            segments=tuple((a,b,1250.,0.,pca) for a,b,pca in intervals)
            with np.load(package/'reference/fixed'/f'{fiber}-{name}-dop853-strict.npz') as raw: times=raw['times']
            model=EulerianPopulation(fiber,8);folder=root/(fiber+'-'+name)
            print('Fixed continuous reference '+folder.name,flush=True)
            case=solve_case(model,segments,times,SETTINGS[1],folder);cases.append(dict(label=folder.name,**case))
            with np.load(folder/'trace.npz') as raw: states=raw['originalBins'];stress=raw['stressPa']
            for setting in ('dop853-strict','dop853-tighter','radau-strict'):
                with np.load(package/'reference/fixed'/f'{fiber}-{name}-{setting}.npz') as raw:
                    assert np.array_equal(raw['times'],times);expected=raw['states']
                expected_stress=69000.*(expected[:,1:-2]@(np.arange(-20.,20.5,.5)+2.5))+model.passive*(1250-model.slack)
                state_error=float(np.max(abs(states-expected)));stress_error=float(np.max(abs(stress-expected_stress)))
                comparisons.append(dict(fiber=fiber,protocol=name,reference=setting,maximumStateDifference=state_error,
                    maximumStressDifferencePa=stress_error,passed=state_error<=LIMITS['fixedReferenceStateAbsolute'] and stress_error<=LIMITS['fixedReferenceStressAbsolutePa']))
    passed=len(cases)==6 and len(comparisons)==18 and all(c['passed'] for c in comparisons)
    save(package/'fixed-analysis.json',dict(completed=True,passed=passed,cases=cases,comparisons=comparisons))
    if not passed: raise RuntimeError('Fixed reduction does not match the preserved independent reference.')


def moving(package):
    assert read(package/'controls-analysis.json')['passed'] and read(package/'fixed-analysis.json')['passed']
    root=package/'moving';root.mkdir();cases=[];times=np.arange(161)*.00025
    for fiber in ('bag','chain'):
        for name in ('stretch','stretch-return','shorten-return'):
            configurations=[(n,20.25,SETTINGS[0]) for n in SUBDIVISIONS]
            configurations += [(64,20.25,s) for s in SETTINGS[1:]]+[(64,40.25,SETTINGS[0])]
            for subdivision,half,setting in configurations:
                label=f'{fiber}-{name}-range{half}-sub{subdivision}-{setting["label"]}'
                print('Continuous movement '+label,flush=True)
                result=solve_case(EulerianPopulation(fiber,subdivision,half),movement(name),times,setting,root/label)
                cases.append(dict(label=label,protocol=name,**result))
    assert len(cases)==42
    save(package/'moving-results.json',dict(completed=True,cases=cases))


def distribution_l1(a_edges,a_density,b_edges,b_density):
    faces=np.union1d(a_edges,b_edges);width=np.diff(faces)
    def values(edges,density):
        indices=np.searchsorted(edges,faces[:-1],side='right')-1;result=np.zeros(len(width))
        valid=(indices>=0)&(indices<len(density));result[valid]=density[indices[valid]];return result
    return float(np.dot(abs(values(a_edges,a_density)-values(b_edges,b_density)),width))


def compare(package):
    assert read(package/'moving-results.json')['completed']
    temporal=[];spatial=[];ranges=[];candidate=[]
    def path(fiber,name,n=64,half=20.25,setting='dop853-strict'):
        return package/'moving'/f'{fiber}-{name}-range{half}-sub{n}-{setting}'/'trace.npz'
    def contrast(first,second,kind):
        with np.load(first) as a,np.load(second) as b:
            assert np.array_equal(a['times'],b['times']) and np.array_equal(a['lengths'],b['lengths'])
            errors=[distribution_l1(a['edges'],x/np.diff(a['edges']),b['edges'],y/np.diff(b['edges'])) for x,y in zip(a['states'][:,1:-4],b['states'][:,1:-4])]
            scalar=float(np.max(abs(a['states'][:,[0,-4,-3]]-b['states'][:,[0,-4,-3]])))
            force=float(np.max(abs(a['stressPa']-b['stressPa'])))
        bound=LIMITS[kind+'PopulationL1'];scalar_bound=LIMITS[kind+'ScalarAbsolute'];force_bound=LIMITS[kind+'StressAbsolutePa']
        return dict(first=first.parent.name,second=second.parent.name,endpointPopulationL1=errors,
            maximumPopulationL1=max(errors),maximumScalarDifference=scalar,maximumStressDifferencePa=force,
            passed=max(errors)<=bound and scalar<=scalar_bound and force<=force_bound)
    for fiber in ('bag','chain'):
        for name in ('stretch','stretch-return','shorten-return'):
            for setting in SETTINGS[1:]: temporal.append(contrast(path(fiber,name),path(fiber,name,setting=setting['label']),'temporal'))
            for coarse,fine in zip(SUBDIVISIONS[:-1],SUBDIVISIONS[1:]):
                spatial.append(dict(coarseSubdivision=coarse,fineSubdivision=fine,**contrast(path(fiber,name,coarse),path(fiber,name,fine),'spatial')))
            ranges.append(contrast(path(fiber,name),path(fiber,name,half=40.25),'range'))
            with np.load(path(fiber,name,setting='dop853-tighter')) as raw:
                ref={k:raw[k] for k in ('times','states','lengths','edges','stressPa')}
            for step in (1000,500,250):
                label=f'{fiber}-{name}-range20.25-{step}us';root=package/'reference/moving'
                meta=read(root/(label+'.json'));rows=meta['endpoints'];stride=step//250
                with np.load(root/(label+'.npz')) as raw:
                    assert len(rows)==len(ref['times'][::stride]);shape=[]
                    for i,row in enumerate(rows):
                        assert abs(row['timeSeconds']-ref['times'][i*stride])<=1e-15 and abs(row['lengthNm']-ref['lengths'][i*stride])<=2e-12
                        faces=raw['originFaces'][raw['faceOffsets'][i]:raw['faceOffsets'][i+1]]+row['displacementNm']
                        density=raw['densityPerNm'][raw['densityOffsets'][i]:raw['densityOffsets'][i+1]]
                        shape.append(distribution_l1(faces,density,ref['edges'],ref['states'][i*stride,1:-4]/np.diff(ref['edges'])))
                    scalars=raw['states'][:,[0,-2,-1]]
                candidate.append(dict(label=label,candidateStepUs=step,endpointPopulationL1=shape,maximumPopulationL1=max(shape),
                    maximumScalarDifference=float(np.max(abs(scalars-ref['states'][::stride][:,[0,-4,-3]]))),
                    maximumStressDifferencePa=float(np.max(abs(np.array([r['stressPa'] for r in rows])-ref['stressPa'][::stride]))),
                    independentReferenceAccuracyRequiredBeforeQualification=True))
    assert (len(temporal),len(spatial),len(ranges),len(candidate))==(12,18,6,18)
    temporal_pass=all(c['passed'] for c in temporal);spatial_pass=all(c['passed'] for c in spatial if c['fineSubdivision']==64)
    range_pass=all(c['passed'] for c in ranges)
    save(package/'eulerian-analysis.json',dict(completed=True,componentChecksPassed=True,analyticControls=12,fixedCases=6,
        fixedReferenceContrasts=18,movingSolves=42,temporalContrasts=temporal,spatialContrasts=spatial,
        rangeContrasts=ranges,candidateContrasts=candidate,temporalCriteriaPassed=temporal_pass,
        finestSpatialCriteriaPassed=spatial_pass,rangeCriteriaPassed=range_pass,
        sampledReferenceDiscretizationCriteriaPassed=temporal_pass and spatial_pass and range_pass,
        referenceIsExact=False,candidateMovingAccuracyQualified=False,slackAssessed=False,sensoryFiringAssessed=False,
        physiologyQualified=False,avatarRecoveryAssessed=False,timingIsBenchmark=False))


if __name__=='__main__':
    if not __debug__: raise RuntimeError('Assertions must remain enabled.')
    p=argparse.ArgumentParser();p.add_argument('package',type=Path)
    p.add_argument('--stage',required=True,choices=['preflight','controls','fixed','moving','compare','postflight'])
    args=p.parse_args();globals()[args.stage](args.package.resolve())

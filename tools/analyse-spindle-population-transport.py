# SPDX-License-Identifier: AGPL-3.0-only
"""Run isolated translation controls/comparisons only in the visible study window."""
import argparse
import importlib.util
import json
import os
from pathlib import Path

for name in ('OMP_NUM_THREADS', 'OPENBLAS_NUM_THREADS', 'MKL_NUM_THREADS', 'NUMEXPR_NUM_THREADS'):
    os.environ[name] = '1'
import numpy as np
from scipy.io import loadmat
from spindle_population_transport import CRITERIA, SHIFTS_NM, protocol, grid, transport, compare


def read(path): return json.loads(path.read_text(encoding='utf-8-sig'))


def encode(value):
    if isinstance(value, np.ndarray): return value.tolist()
    if isinstance(value, np.generic): return value.item()
    raise TypeError(type(value).__name__)


def save(path, value):
    with path.open('x', encoding='utf-8') as stream:
        json.dump(value, stream, indent=2, default=encode, allow_nan=False); stream.write('\n')


def module(name, path):
    spec=importlib.util.spec_from_file_location(name,path)
    value=importlib.util.module_from_spec(spec);spec.loader.exec_module(value);return value


def environment(package):
    return module('population_environment',package/'source/tools/golgi-reference-resume.py').environment()


def inputs(package):
    assert read(package/'transport-input.json') == protocol()
    review=read(package/'reference/review.json')
    assert review['operatorReportedCompletion'] and review['forceLandscapeNumericsPassed']
    assert review['savedStateQueries']==2812 and review['analyticControlsPassed']==8 and review['noRootStates']==31
    rows=[]
    for fiber in ('bag','chain'):
        for name in ('stretch-shorten','imposed-below-slack'):
            for step in (1000,500,250):
                label=f'{fiber}-{name}-{step}us'; raw=loadmat(package/'native'/(label+'.mat'),simplify_cells=True)['result']
                assert raw['fiber']==fiber and raw['protocol']==name and raw['completed'] and raw['finite']
                assert raw['completedSteps']==100000//step and raw['dtSeconds']==step/1e6
                assert not raw['slackControllerUsed'] and raw['timeConvention']=='initial plus completed endpoints'
                p=raw['parameters']; assert p['compliance_factor']==.5 and p['bin_width']==.5
                assert p['cb_force_constant']==69000. and p['power_stroke']==2.5
                assert np.array_equal(p['x_bins'],np.arange(-20.,20.5,.5))
                snapshots=raw['snapshots']; assert len(snapshots)==1+100000//step
                states=np.asarray([s['y'] for s in snapshots]);assert states.shape==(len(snapshots),84) and np.isfinite(states).all()
                for i,s in enumerate(snapshots):
                    assert s['step']==i and s['timeSeconds']==i*(step/1e6)
                    assert np.array_equal(s['reportedBinPopulations'],states[i,1:-2])
                rows.append(dict(label=label,parameters=p,snapshots=snapshots,states=states))
    assert sum(len(r['states']) for r in rows)==2812
    return rows,review


def preflight(package):
    rows,_=inputs(package); env=environment(package)
    assert env==read(package/'preparation-environment.json')
    save(package/'reference-preflight.json',dict(environment=env,completed=True,
        capturedStates=sum(len(r['states']) for r in rows),modelExecuted=False,controlsExecuted=False))


def postflight(package):
    env=environment(package)
    same=env==read(package/'preparation-environment.json')==read(package/'reference-preflight.json')['environment']
    save(package/'reference-postflight.json',dict(environment=env,unchanged=same,modelExecuted=False))
    if not same: raise RuntimeError('Numerical environment changed; preserve evidence.')


def controls(package):
    out=package/'controls';out.mkdir(); records=[]
    cases=[('stationary-interior',40,.2,0.,{40:.2},0.,0.),
        ('half-cell-interior',40,.2,.25,{40:.1,41:.1},0.,0.),
        ('right-edge-half-outflow',80,.2,.25,{80:.1},0.,.1),
        ('left-edge-half-outflow',0,.2,-.25,{0:.1},.1,0.),
        ('right-edge-full-outflow',80,.2,.5,{},0.,.2),
        ('left-edge-full-outflow',0,.2,-.5,{},.2,0.),
        ('fractional-multicell-interior',40,.2,1.125,{42:.15,43:.05},0.,0.),
        ('entire-domain-outflow',40,.2,41.,{},0.,.2),
        ('empty-population',40,0.,.125,{},0.,0.)]
    centres=np.arange(-20.,20.5,.5)
    for label,index,mass,shift,expected,left,right in cases:
        source=np.zeros(81);source[index]=mass; result=compare(source,shift)
        desired=np.zeros(81)
        for i,m in expected.items(): desired[i]=m
        force=69000.*float((centres+2.5)@desired)
        passed=(result['passed'] and np.max(np.abs(result['candidate']['masses']-desired))<=CRITERIA['populationAbsolute']
            and abs(result['candidate']['leftOutflow']-left)<=CRITERIA['populationAbsolute']
            and abs(result['candidate']['rightOutflow']-right)<=CRITERIA['populationAbsolute']
            and abs(result['candidate']['forcePa']-force)<=CRITERIA['forceAbsolutePa'])
        record=dict(label=label,source=source,shiftNm=shift,expectedMasses=desired,expectedForcePa=force,result=result,passed=bool(passed))
        save(out/(label+'.json'),record);records.append(dict(label=label,passed=bool(passed)))
    # The pinned interpolation drops an occupied endpoint on one side of zero.
    source=np.zeros(81);source[-1]=.2;eps=protocol()['continuityEpsilonNm']
    original=lambda q:69000.*float((centres+2.5)@np.interp(centres-q,centres,source,left=0.,right=0.))
    legacy=abs(original(0.)-original(-eps)); points=[compare(source,q) for q in (-eps,0.,eps)]
    force_span=abs(points[-1]['candidate']['forcePa']-points[0]['candidate']['forcePa'])
    # L1 transport is Lipschitz with 2*|dq|/h per source mass; force uses a bounded weight.
    bound=69000.*max(abs(centres+2.5))*np.sum(abs(source))*4*eps/.5+2*CRITERIA['forceAbsolutePa']
    passed=all(r['passed'] for r in points) and legacy>100000. and force_span<=bound
    record=dict(label='occupied-endpoint-continuity',legacyJumpPa=legacy,candidateSpanPa=force_span,
        candidateSpanBoundPa=float(bound),points=points,passed=bool(passed))
    save(out/'occupied-endpoint-continuity.json',record);records.append(dict(label=record['label'],passed=bool(passed)))
    report=dict(completed=True,passed=all(r['passed'] for r in records),cases=records,physiologicalControls=False)
    save(out/'controls.json',report)
    if not report['passed']: raise RuntimeError('Population transport analytic controls failed.')


def matrix(package):
    assert read(package/'controls/controls.json')['passed']
    rows,review=inputs(package); out=package/'matrix';out.mkdir();summaries=[];total=0
    for row in rows:
        records=[]; candidate_arrays=[]; reference_arrays=[];passed=True
        with (out/(row['label']+'.jsonl')).open('x',encoding='utf-8') as stream:
            for s,y in zip(row['snapshots'],row['states']):
                for shift in SHIFTS_NM:
                    result=compare(y[1:-2],shift)
                    candidate_arrays.append(result['candidate']['masses']);reference_arrays.append(result['reference']['masses'])
                    record={k:v for k,v in result.items() if k not in ('candidate','reference')}
                    for side in ('candidate','reference'):
                        record[side]={k:v for k,v in result[side].items() if k!='masses'}
                    record.update(step=int(s['step']),timeSeconds=float(s['timeSeconds']),shiftNm=shift,
                        originalLengthNm=float(s['hsLengthNm']),originalStressPa=float(s['stressPa']),
                        sourceMinimumPopulation=float(np.min(y[1:-2])))
                    stream.write(json.dumps(record,default=encode,allow_nan=False)+'\n');stream.flush()
                    records.append(record);total+=1;passed &= result['passed']
                    if not result['passed']: break
                if not passed: break
        np.savez_compressed(out/(row['label']+'.npz'),candidateMasses=candidate_arrays,referenceMasses=reference_arrays)
        summary=dict(label=row['label'],queries=len(records),expectedQueries=len(row['states'])*9,passed=bool(passed),
            maximumPopulationDifference=max(r['maximumPopulationDifference'] for r in records),
            maximumForceDifferencePa=max(r['forceDifferencePa'] for r in records),
            maximumConservationResidual=max(r['conservationResidual'] for r in records),
            maximumGridVsContinuumForcePa=max(abs(r['gridVsContinuumForcePa']) for r in records))
        summaries.append(summary);print(json.dumps(summary),flush=True)
        if not passed: break
    report=dict(completed=total==25308,queries=total,trajectories=summaries,
        passed=bool(total==25308 and all(r['passed'] for r in summaries)),newKineticsExecuted=False)
    save(package/'matrix-analysis.json',report)
    if not report['passed']: raise RuntimeError('Saved-state transport comparison failed or incomplete.')


def continuity(package):
    rows,review=inputs(package);lookup={r['label']:r for r in rows};out=package/'continuity';out.mkdir();results=[]
    eps=protocol()['continuityEpsilonNm'];centres=np.arange(-20.,20.5,.5)
    for evidence in review['noRootEvidence']:
        source=lookup[evidence['trajectory']];s=source['snapshots'][evidence['step']];y=source['states'][evidence['step']]
        assert s['hsLengthNm']==evidence['originalLengthNm'] and s['stressPa']==evidence['originalStressPa']
        for boundary in evidence['discontinuities']:
            if not boundary['signChangeAcrossJump']: continue
            q=(boundary['lengthNm']-s['hsLengthNm'])*.5
            points=[compare(y[1:-2],q+d) for d in (-eps,0.,eps)]
            span=abs(points[-1]['candidate']['forcePa']-points[0]['candidate']['forcePa'])
            bound=69000.*max(abs(centres+2.5))*np.sum(abs(y[1:-2]))*4*eps/.5+2*CRITERIA['forceAbsolutePa']
            record=dict(trajectory=evidence['trajectory'],step=evidence['step'],oldBoundary=boundary,
                shiftNm=q,candidateForceSpanPa=span,candidateSpanBoundPa=float(bound),points=points,
                passed=bool(all(r['passed'] for r in points) and span<=bound))
            save(out/(str(len(results))+'.json'),record);results.append({k:v for k,v in record.items() if k!='points'})
    expected=sum(int(b['signChangeAcrossJump']) for e in review['noRootEvidence'] for b in e['discontinuities'])
    assert len(review['noRootEvidence'])==31 and len(results)==expected
    report=dict(completed=True,capturedNoRootStates=31,probes=len(results),cases=results,passed=all(r['passed'] for r in results),rootsRecomputed=False)
    save(package/'continuity-analysis.json',report)
    if not report['passed']: raise RuntimeError('Saved-boundary continuity comparison failed.')


def rectangle(edges,a,b,density):
    return density*np.maximum(0.,np.minimum(edges[1:],b)-np.maximum(edges[:-1],a)) if b>a else np.zeros(len(edges)-1)


def histories(package):
    out=package/'histories';out.mkdir();spec=protocol();summaries=[]
    profiles=[('interior',-4.25,3.75,1.25),('right-edge',19.25,20.25,.375),('left-edge',-20.25,-19.25,-.375)]
    for half_range in spec['refinementHalfRangesNm']:
        for width in spec['refinementWidthsNm']:
            count=round(2*half_range/width);assert count*width==2*half_range
            edges=-half_range+width*np.arange(count+1);centres=grid(-half_range,width,count)
            for name,a,b,excursion in profiles:
                density=.2/(b-a);initial=rectangle(edges,a,b,density)
                assert abs(float(np.sum(initial))-.2)<=CRITERIA['conservationAbsolute']
                for steps in spec['historySteps']:
                    for path in ('one-way','return'):
                        positions=list(np.linspace(0.,excursion,steps+1))
                        if path=='return':positions+=list(np.linspace(excursion,0.,steps+1)[1:])
                        state=initial.copy();left=right=0.;low=high=0.;previous=0.;records=[]
                        for index,position in enumerate(positions):
                            if index:
                                update=transport(state,float(position-previous),-half_range,width)
                                state=update['masses'];left+=update['leftOutflow'];right+=update['rightOutflow']
                            low=min(low,position);high=max(high,position);previous=position
                            # Exact characteristic solution for the original rectangle with absorbing faces.
                            start=max(a,-half_range-low);end=min(b,half_range-high)
                            exact=rectangle(edges,start+position,end+position,density)
                            mass=float(np.sum(state));exact_mass=max(0.,end-start)*density
                            exact_first=exact_mass*(start+end+2*position)*.5
                            exact_second=exact_mass*((start+position)**2+(start+position)*(end+position)+(end+position)**2)/3
                            first=float(centres@state);second=float((centres**2+width**2/12)@state)
                            ledger=abs(mass+left+right-.2);domain=max(0.,-float(np.min(state)),-left,-right)
                            records.append(dict(index=index,displacementNm=float(position),retained=mass,leftOutflow=left,rightOutflow=right,
                                exactRetained=exact_mass,populationL1=float(np.sum(abs(state-exact))),
                                firstMomentErrorNm=first-exact_first,secondMomentErrorNm2=second-exact_second,
                                forceErrorPa=69000.*(first+2.5*mass-exact_first-2.5*exact_mass),
                                conservationResidual=ledger,domainViolation=domain,
                                passed=ledger<=CRITERIA['conservationAbsolute'] and domain<=CRITERIA['domainAbsolute']))
                        label=f'{name}-{path}-range{half_range}-width{width}-steps{steps}'
                        np.savez_compressed(out/(label+'.npz'),centresNm=centres,initialMasses=initial,candidateFinalMasses=state,exactFinalMasses=exact)
                        save(out/(label+'.json'),records)
                        summaries.append(dict(label=label,profile=name,path=path,halfRangeNm=half_range,widthNm=width,stepsPerLeg=steps,
                            endpoints=len(records),maximumPopulationL1=max(r['populationL1'] for r in records),
                            maximumForceErrorPa=max(abs(r['forceErrorPa']) for r in records),
                            maximumSecondMomentErrorNm2=max(abs(r['secondMomentErrorNm2']) for r in records),
                            finalRetained=mass,exactFinalRetained=exact_mass,passed=all(r['passed'] for r in records)))
    refinements=[]
    for half_range in spec['refinementHalfRangesNm']:
        for name,_,_,_ in profiles:
            for path in ('one-way','return'):
                for steps in spec['historySteps']:
                    selected=[r for r in summaries if (r['halfRangeNm'],r['profile'],r['path'],r['stepsPerLeg'])==(half_range,name,path,steps)]
                    assert [r['widthNm'] for r in selected]==spec['refinementWidthsNm']
                    bound=max(spec['spatialRefinementFloor'],spec['spatialRefinementRatio']*selected[0]['maximumPopulationL1'])
                    refinements.append(dict(halfRangeNm=half_range,profile=name,path=path,stepsPerLeg=steps,
                        coarseError=selected[0]['maximumPopulationL1'],fineError=selected[-1]['maximumPopulationL1'],
                        allowedFineError=bound,passed=selected[-1]['maximumPopulationL1']<=bound))
    completed=len(summaries)==144 and len(refinements)==36
    report=dict(completed=completed,cases=summaries,spatialRefinement=refinements,
        passed=completed and all(r['passed'] for r in summaries+refinements),
        finiteRangePhysiologyQualified=False,timeRefinementClaimed=False,kineticsExecuted=False)
    save(package/'history-analysis.json',report)
    if not report['passed']:raise RuntimeError('Translation history accounting/spatial refinement criteria failed.')


def summarise(package):
    reports={name:read(package/(name+'-analysis.json')) for name in ('matrix','continuity','history')}
    controls=read(package/'controls/controls.json')
    assert controls['completed'] and controls['passed'] and len(controls['cases'])==10
    assert all(r['completed'] and r['passed'] for r in reports.values())
    save(package/'transport-analysis.json',dict(completed=True,transportNumericsPassed=True,
        analyticControls=10,savedStateQueries=reports['matrix']['queries'],
        noRootStatesProbed=reports['continuity']['capturedNoRootStates'],continuityProbes=reports['continuity']['probes'],
        historyCases=len(reports['history']['cases']),spatialRefinementComparisons=len(reports['history']['spatialRefinement']),
        criteria=CRITERIA,originalBoundsChanged=False,kineticsExecuted=False,rootSelected=False,
        slackTrajectoryAssessed=False,sensoryFiringAssessed=False,physiologyQualified=False,avatarRecoveryAssessed=False))


if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    parser=argparse.ArgumentParser();parser.add_argument('package',type=Path)
    parser.add_argument('--stage',required=True,choices=('preflight','controls','matrix','continuity','histories','summarise','postflight'))
    args=parser.parse_args();globals()[args.stage](args.package.resolve())

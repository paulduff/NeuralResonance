"""Review operator-reported saved states; never evolve chemistry or movement."""
import argparse
from datetime import datetime, timezone
import importlib.util
import json
import math
import os
from pathlib import Path
import sys

for key in ('OMP_NUM_THREADS','OPENBLAS_NUM_THREADS','MKL_NUM_THREADS','NUMEXPR_NUM_THREADS'):
    os.environ[key]='1'
import numpy as np

ROOT=Path(__file__).resolve().parents[1]


def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path)
    result=importlib.util.module_from_spec(spec); spec.loader.exec_module(result); return result


def main():
    capture=ROOT/'artifacts/spindle-coupling-20261117-85ae1d'
    output=ROOT/'artifacts/spindle-coupling-review-20261117'; assert not output.exists()
    common=module('coupling_integrity',ROOT/'tools/review-olive-and-motor-20261109.py')
    print('Verifying completed C:/D: inventory and frozen source...',flush=True)
    review=common.verify(capture,[],'0398904f04e58ec99e588cf05b841f799eea32ba')
    archive=module('coupling_archive',ROOT/'tools/review-spindle-root-and-causal-motor-20261113.py')
    review['verifiedGitFiles']=archive.source_archive(common,capture,review['sourceRevision'])
    sys.path.insert(0,str(capture/'source/tools'))
    audit=module('coupling_saved_analysis',capture/'source/tools/analyse-spindle-coupling.py')
    # Only protocol, saved-array comparisons and JSON helpers are called here.
    # Guard against accidentally executing any candidate/reference integration.
    def forbidden(*args,**kwargs): raise RuntimeError('Review cannot execute a model, control or trajectory.')
    audit.solve_ivp=forbidden
    audit.CoupledPopulation.__init__=forbidden
    env=audit.environment(capture)
    assert env==common.read(capture/'preparation-environment.json')==common.read(capture/'reference-preflight.json')['environment']
    assert common.read(capture/'reference-postflight.json')==dict(environment=env,unchanged=True,modelExecuted=False)
    config=common.read(capture/'configuration.json'); status=common.read(capture/'window-status.json')
    assert config['NoLiveMonitoring'] and config['IsolatedSpindleCoupling']
    assert status['ComponentChecksPassed'] and status['NumericalEnvironmentUnchanged'] and status['FrozenPackageUnchanged']
    spec=common.read(capture/'coupling-input.json'); assert spec==audit.protocol()
    report=common.read(capture/'coupling-analysis.json')
    assert report==dict(completed=True,componentChecksPassed=True,analyticControls=12,fixedCases=18,
        fixedReferenceComparisons=54,movingCases=36,movingTemporalContrasts=24,movingRangeContrasts=18,
        movingIndependentAccuracyQualified=False,temporalOrSpatialConvergenceQualified=False,
        slackTrajectoryAssessed=False,sensoryFiringAssessed=False,physiologyQualified=False,avatarRecoveryAssessed=False)
    helper=module('coupling_provenance',ROOT/'tools/prepare-causal-motor.py')
    for kind,folder in (('fixed','spindle-fixed-review-20261116'),('transport','spindle-characteristics-review-20261116')):
        prior=ROOT/'artifacts'/folder; helper.verify(prior,['review.json'])
        assert helper.digest(prior/'review.json')==helper.digest(capture/'reference'/(kind+'-review.json'))
        source=common.read(prior/'review.json'); assert source['operatorReportedCompletion']
        assert common.digest(Path(source['capture'])/'backup-manifest.json')==source['manifestSha256']
    provenance=common.read(capture/'provenance.json')
    assert not provenance['controlsExecutedDuringPreparation'] and not provenance['modelsExecutedDuringPreparation']
    for item in provenance['copied']:
        assert common.digest(capture/item['path'])==item['sha256']==common.digest(Path(item['capture'])/item['original'])
    bounds=spec['criteria']; controls=common.read(capture/'controls-analysis.json')
    expected=['zero-duration','actin-only','detachment-only','attachment-only','shifted-detachment',
        'birth-preserves-old-shape','boundary-transfer','boundary-return','observation-purity',
        'passive-force','invalid-input','capacity-rejection']
    assert controls['completed'] and controls['passed'] and [r['label'] for r in controls['cases']]==expected
    for control in controls['cases']:
        assert control==common.read(capture/'controls'/(control['label']+'.json'))
        assert control['passed'] and control['error']<=control['bound']
        assert control['bound']==(1e-7 if control['label']=='passive-force' else 1e-9)
        with np.load(capture/'controls'/(control['label']+'.npz'),allow_pickle=False) as raw:
            assert np.isfinite(raw['originNm']).all() and np.isfinite(raw['densityPerNm']).all() and np.isfinite(raw['physicalState']).all()
            assert len(raw['originNm'])==len(raw['densityPerNm'])+1 and (np.diff(raw['originNm'])>0).all()
    with np.load(capture/'controls/full-density-reference.npz',allow_pickle=False) as ref, np.load(capture/'controls/birth-preserves-old-shape.npz',allow_pickle=False) as candidate:
        assert ref['times'][0]==0 and ref['times'][-1]==.001 and (np.diff(ref['times'])>0).all()
        assert np.array_equal(ref['originNm'],candidate['originNm'])
        physical=candidate['physicalState']; full=np.concatenate(([physical[0]],candidate['densityPerNm'],physical[-2:]))
        assert float(np.max(abs(full-ref['states'][-1])))==controls['cases'][5]['error']
    fixed=common.read(capture/'fixed-analysis.json'); moving=common.read(capture/'moving-analysis.json')
    assert fixed['completed'] and fixed['passed'] and moving['completed'] and moving['accountingPassed']
    assert not moving['movingIndependentAccuracyQualified'] and not moving['temporalOrSpatialConvergenceQualified'] and not moving['timingIsBenchmark']
    groups={}; endpoints=0; reactions=0; accepted_nodes=0; max_internal_ledger=0.; max_internal_domain=0.
    print('Reconciling all completed physical distributions and accepted reaction states...',flush=True)
    for kind,analysis,count in (('fixed',fixed,18),('moving',moving,36)):
        cases=analysis['cases']; assert len(cases)==count and len({r['label'] for r in cases})==count
        expected_labels=({f'{f}-{p}-{s}us' for f in ('bag','chain') for p in ('quiet-hold','active-hold','drive-on-off') for s in (1000,500,250)}
            if kind=='fixed' else {f'{f}-{p}-range{h}-{s}us' for f in ('bag','chain') for p in ('stretch','stretch-return','shorten-return') for h in (20.25,40.25) for s in (1000,500,250)})
        assert {r['label'] for r in cases}==expected_labels
        for case in cases:
            assert case==common.read(capture/kind/(case['label']+'-summary.json')) and case['completed']
            stored=common.read(capture/kind/(case['label']+'.json')); rows=stored['endpoints']; ops=stored['reactions']
            with np.load(capture/kind/(case['label']+'.npz'),allow_pickle=False) as raw:
                arrays={key:raw[key] for key in raw.files}
            states=arrays['states']; faces=arrays['originFaces']; density=arrays['densityPerNm']
            fo=arrays['faceOffsets']; do=arrays['densityOffsets']; internal=arrays['internal']; io=arrays['internalOffsets']
            steps=(100000 if kind=='fixed' else 40000)//case['stepUs']; dt=case['stepUs']/1e6
            assert case['endpoints']==len(rows)==len(states)==steps+1
            assert len(ops)==len(io)-1==2*steps and len(fo)==len(do)==steps+2
            assert fo[0]==do[0]==io[0]==0 and fo[-1]==len(faces) and do[-1]==len(density) and io[-1]==len(internal)
            assert (np.diff(fo)>1).all() and np.array_equal(np.diff(fo),np.diff(do)+1) and (np.diff(io)>=2).all()
            assert all(np.isfinite(a).all() for a in arrays.values())
            half=case['halfRangeNm']; bins=round(4*half); edges=np.linspace(-half,half,bins+1)
            assert states.shape==(steps+1,bins+3) and internal.shape[1]==bins+4
            assert np.array_equal(states[0],np.concatenate(([1.],np.zeros(bins),[1.,0.])))
            for i,row in enumerate(rows):
                assert row['timeSeconds']==i*dt
                if kind=='fixed': wanted_length=1250.
                else:
                    elapsed_us=i*case['stepUs']
                    returning='return' in case['label']
                    excursion=4.*min(elapsed_us,40000-elapsed_us)/20000 if returning else 4.*elapsed_us/40000
                    wanted_length=1250.+(-excursion if 'shorten' in case['label'] else excursion)
                assert row['lengthNm']==wanted_length and row['displacementNm']==.5*(wanted_length-1250.)
                f=faces[fo[i]:fo[i+1]]; d=density[do[i]:do[i+1]]
                assert (np.diff(f)>0).all() and len(d)==row['intervals']<=bounds['maximumIntervals']
                assert f[0]==-half-row['displacementNm'] and f[-1]==half-row['displacementNm']
                mass=d*np.diff(f); a=f[:-1]+row['displacementNm']; b=f[1:]+row['displacementNm']
                total=math.fsum(mass); first=math.fsum(mass*(a+b)/2); second=math.fsum(mass*(a*a+a*b+b*b)/3)
                assert total==row['bound'] and first==row['firstMomentNm'] and second==row['secondMomentNm2']
                assert 69000.*(first+2.5*total)==row['crossBridgeStressPa']
                passive,slack=(90.,1050.) if case['fiber']=='bag' else (250.,1200.)
                assert row['stressPa']==row['crossBridgeStressPa']+passive*(row['lengthNm']-slack)
                assert row['detached']==states[i,0] and row['inactive']==states[i,-2] and row['active']==states[i,-1]
                assert row['myosinResidual']==row['detached']+total-1 and row['actinResidual']==row['active']+row['inactive']-1
                assert row['minimumDensity']==float(d.min())
                prefix=np.concatenate(([0.],np.cumsum(mass)))
                observed=np.diff(np.interp(edges-row['displacementNm'],f,prefix,left=0.,right=prefix[-1]))
                assert np.max(abs(observed-states[i,1:-2]))<=1e-12
                if i:
                    assert row['leftNumericalOutflow']>=rows[i-1]['leftNumericalOutflow']
                    assert row['rightNumericalOutflow']>=rows[i-1]['rightNumericalOutflow']
            assert audit.accounting(rows)==case['accounting'] and case['accounting']['passed']
            assert case['final']=={k:v for k,v in rows[-1].items() if k!='timeSeconds'}
            assert case['peakIntervals']==max(r['peakIntervals'] for r in rows)
            for i,op in enumerate(ops):
                nodes=internal[io[i]:io[i+1]]; y=nodes[:,1:]; step=i//2
                assert op['phase']==('before-motion' if i%2==0 else 'after-motion')
                assert op['ownerTimeSeconds']==(step if i%2==0 else step+1)*dt
                pca=9. if kind=='fixed' and ('quiet-hold' in case['label'] or ('drive-on-off' in case['label'] and not 20000<=step*case['stepUs']<80000)) else 6.5
                assert op['pCa']==pca
                assert op['durationSeconds']==dt/2 and nodes[0,0]==0 and nodes[-1,0]==dt/2 and (np.diff(nodes[:,0])>0).all()
                assert op['lengthNm']==rows[step if i%2==0 else step+1]['lengthNm']
                bound=y[:,1:-2].sum(axis=1); fraction=(1120.-(op['lengthNm']-815.))/735.
                ledger=max(float(np.max(abs(y[:,0]+bound-1))),float(np.max(abs(y[:,-2]+y[:,-1]-1))))
                domain=max(0.,-float(y.min()),float(np.max(bound-y[:,-1])),float(np.max(y[:,-1]-fraction)))
                assert ledger<=bounds['conservationAbsolute'] and domain<=bounds['domainAbsolute']
                if i%2==0: assert np.max(abs(y[0]-states[step]))<=1e-12
                else:
                    assert np.max(abs(y[-1]-states[step+1]))<=1e-12
                    assert np.max(abs(y[0,-2:]-internal[io[i]-1,-2:]))<=1e-12
                max_internal_ledger=max(max_internal_ledger,ledger); max_internal_domain=max(max_internal_domain,domain)
            groups[(kind,case['label'])]=(states,rows)
            endpoints+=len(rows); reactions+=len(ops); accepted_nodes+=len(internal)
    assert endpoints==7614 and reactions==15120
    assert len(fixed['comparisons'])==54
    assert len({(p['label'],p['reference']) for p in fixed['comparisons']})==54
    for pair in fixed['comparisons']:
        states,rows=groups[('fixed',pair['label'])]; name=pair['label'].rsplit('-',1)[0]
        times=np.array([r['timeSeconds'] for r in rows])
        with np.load(capture/'reference/fixed'/(name+'-'+pair['reference']+'.npz'),allow_pickle=False) as raw:
            indices=np.searchsorted(raw['times'],times); assert np.array_equal(raw['times'][indices],times)
            ref=raw['states'][indices]
        error=float(np.max(abs(states-ref)))
        stress=69000.*(ref[:,1:-2]@(np.arange(-20.,20.5,.5)+2.5))
        force=float(np.max(abs(stress-np.array([r['crossBridgeStressPa'] for r in rows]))))
        assert pair['passed'] and error==pair['maximumStateDifference']<=bounds['fixedStateAbsolute']
        assert force==pair['maximumForceDifferencePa']<=bounds['fixedForceAbsolutePa']
    assert len(moving['temporalDifferences'])==24 and len(moving['rangeDifferences'])==18
    for temporal in (True,False):
        for pair in moving['temporalDifferences' if temporal else 'rangeDifferences']:
            base=f"{pair['fiber']}-{pair['protocol']}-range"
            a=base+(str(pair['halfRangeNm'])+'-'+str(pair['coarseStepUs']) if temporal else '20.25-'+str(pair['stepUs']))+'us'
            b=base+(str(pair['halfRangeNm'])+'-250' if temporal else '40.25-'+str(pair['stepUs']))+'us'
            stride=pair['coarseStepUs']//250 if temporal else 1
            shape=audit.saved_shape_difference(capture/'moving',a,b,stride)
            assert shape=={k:pair[k] for k in shape}
            first,ra=groups[('moving',a)]; second,rb=groups[('moving',b)]; second=second[::stride]; rb=rb[::stride]
            assert [r['displacementNm'] for r in ra]==[r['displacementNm'] for r in rb]
            assert float(np.max(abs(first[:,[0,-2,-1]]-second[:,[0,-2,-1]])))==pair['maximumScalarDifference']
            assert max(abs(x['stressPa']-y['stressPa']) for x,y in zip(ra,rb))==pair['maximumStressDifferencePa']
            if temporal: assert float(np.max(np.sum(abs(first[:,1:-2]-second[:,1:-2]),axis=1)))==pair['maximumObservedBinL1']
            else:
                assert ra[-1]['leftNumericalOutflow']+ra[-1]['rightNumericalOutflow']==pair['narrowOutflow']
                assert rb[-1]['leftNumericalOutflow']+rb[-1]['rightNumericalOutflow']==pair['wideOutflow']
    temporal=moving['temporalDifferences']; ranges=moving['rangeDifferences']
    pairs=[]
    for coarse in temporal[::2]:
        fine=next(p for p in temporal if p['fiber']==coarse['fiber'] and p['protocol']==coarse['protocol'] and p['halfRangeNm']==coarse['halfRangeNm'] and p['coarseStepUs']==500)
        pairs.append(dict(fiber=coarse['fiber'],protocol=coarse['protocol'],halfRangeNm=coarse['halfRangeNm'],
            shapeDifferenceRatio=fine['maximumPopulationL1']/coarse['maximumPopulationL1'],
            forceDifferenceRatio=fine['maximumStressDifferencePa']/coarse['maximumStressDifferencePa']))
    review.update(reviewedUtc=datetime.now(timezone.utc).isoformat(),operatorReportedCompletion=True,savedRecordsReconciled=True,
        analyticControlsPassed=12,fixedCases=18,fixedReferenceComparisons=54,movingCases=36,
        physicalEndpoints=endpoints,reactionOperators=reactions,acceptedReactionNodes=accepted_nodes,
        maximumFixedStateError=max(p['maximumStateDifference'] for p in fixed['comparisons']),
        maximumFixedForceErrorPa=max(p['maximumForceDifferencePa'] for p in fixed['comparisons']),
        maximumInternalLedgerError=max_internal_ledger,maximumInternalDomainViolation=max_internal_domain,
        maximumEndpointLedgerError=max(c['accounting']['maximumLedgerError'] for c in fixed['cases']+moving['cases']),
        maximumMovingShapeDifference=max(p['maximumPopulationL1'] for p in temporal),
        maximumMovingForceDifferencePa=max(p['maximumStressDifferencePa'] for p in temporal),
        maximumRangeShapeDifference=max(p['maximumPopulationL1'] for p in ranges),
        maximumRangeForceDifferencePa=max(p['maximumStressDifferencePa'] for p in ranges),
        peakIntervals=max(c['peakIntervals'] for c in moving['cases']),stepDifferenceRatios=pairs,
        testsExecutedDuringReview=False,modelsSolvedDuringReview=False,originalBoundsChanged=False,
        movingIndependentAccuracyQualified=False,temporalOrSpatialConvergenceQualified=False,
        sensoryFiringAssessed=False,physiologyQualified=False,avatarRecoveryAssessed=False)
    output.mkdir(); audit.save(output/'review.json',review)
    print(json.dumps({k:v for k,v in review.items() if k!='stepDifferenceRatios'},indent=2),flush=True)


if __name__=='__main__':
    if not __debug__: raise RuntimeError('Assertions must remain enabled.')
    p=argparse.ArgumentParser(); p.add_argument('--operator-reported-complete',required=True,action='store_true'); p.parse_args()
    main()

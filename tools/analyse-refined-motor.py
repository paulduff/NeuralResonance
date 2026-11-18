"""Complete archived neuronal/native audit, then unchanged common-grid limits."""
import argparse
import json
from pathlib import Path
import numpy as np
import refined_motor_gate as gate
from native_capture_store import open_view, CONDITIONS
from native_hinge_protocol import read
from native_hinge_reference import HingeGeometry, HingeCoupledReference


def analyse(package):
    gate.preflight(package)
    controls=read(package/'storage-controls/controls.json')
    assert controls['completed'] and controls['expected']==controls['passed']==22
    prior=read(package/'baseline/rubral-hinge-input.json');prior_audit=read(package/'baseline/native-source-analysis.json')
    original=gate.module('refined_full_native_audit',package/'source/tools/analyse-rubral-native-body.py')
    arrays=gate.module('refined_native_arrays',package/'source/tools/analyse-native-hinge-comparison.py')
    wire=gate.module('refined_native_wire',package/'source/tools/analyse-native-motor-bridge.py')
    print('RECONCILE EVERY ORIGINAL SOURCE FRAME, PACKET, EFFORT AND NATIVE STEP FROM ARCHIVES',flush=True)
    with open_view(package) as capture:
        _,spec,data,evidence=original.audit_at(package,capture,arrays,wire,3,1,False)
    assert evidence['sourceBodyAccountingPassed'] and evidence['actualPhysicsSteps']==223972 and evidence['packetAndRetryStateBitwiseEqual']
    gate.save(package/'native-source-analysis.json',evidence);gate.save(package/'rubral-hinge-input.json',spec)
    assert [case['key'] for case in spec['cases']]==[case['key'] for case in prior['cases']]==list(CONDITIONS)
    for field in ('candidateVersion','muscleVersion','geometry','parameterRows','unitRows','initialFiberLengthMeters',
                  'referenceSettings','candidateBounds','referenceBounds','calciumLedgerAbsolute','fiberMomentumLedgerNewtonSeconds',
                  'fiberKineticLedgerJoules','releaseWaveformAbsolute','pathGeometryMeters'):
        assert spec[field]==prior[field],field
    assert {k:v for k,v in spec['nativeGeometry'].items() if k!='stepSeconds'}=={k:v for k,v in prior['nativeGeometry'].items() if k!='stepSeconds'}
    mappings=[]
    for old,new,old_source,current_source in zip(prior['cases'],spec['cases'],prior_audit['cases'],evidence['cases'],strict=True):
        assert old['key']==new['key']==old_source['case']==current_source['case']
        assert old['durationMs']==new['durationMs'] and new['physicalSteps']==4*old['physicalSteps']==31996
        assert old['physicalStepMs']==4*new['physicalStepMs']
        for field in ('initialAngleRadians','initialSpeedRadiansPerSecond'):assert new[field]==old[field]
        for field in ('selectedEvents','motorEvents','received','sourceFrames','motorRows'):assert current_source[field]==old_source[field]
        # Both epochs' complete source/packet audits establish their own IDs.
        # Numerical reference reuse requires the exact same physical input;
        # retain distinct IDs in the mapping instead of equating fresh epochs.
        physical=('muscleIndex','arrivalAtMs','originalSourceNeuronId')
        assert [{k:e[k] for k in physical} for e in new['events']]==[{k:e[k] for k in physical} for e in old['events']]
        pairs=[]
        for a,b in zip(old['events'],new['events'],strict=True):
            assert a.keys()==b.keys()=={'muscleIndex','arrivalAtMs','originalSourceNeuronId','originalMessageId','originalContactId'}
            assert a['originalMessageId']!=b['originalMessageId'] and a['originalContactId']!=b['originalContactId']
            pairs.append(dict(original=a,current=b))
        mappings.append(dict(case=new['key'],physicalInputExactlyEqual=True,originalAndCurrentIdentitiesRetained=pairs,
            independentCompleteSourceAuditsRequired=True,arrivalToleranceUsed=False))
    gate.save(package/'event-epoch-mappings.json',dict(completed=True,cases=mappings,fullAuditsPassed=True))

    geometry=HingeGeometry(spec['nativeGeometry']);references={};grid=None
    execution=read(package/'baseline/rubral-reference/execution.json')
    assert execution['completed'] and len(execution['references'])==15
    for case in prior['cases'][:5]:
        values={}
        for settings in prior['referenceSettings']:
            name=case['key']+'-'+settings['key'];stats=read(package/'baseline/rubral-reference'/(name+'-statistics.json'))
            assert stats=={k:v for k,v in next(r for r in execution['references'] if r['key']==name).items() if k!='key'}
            assert stats['completed'] and stats['acceptedInternalDomainPassed'] and stats['sampleDomainPassed']
            with np.load(package/'baseline/rubral-reference'/(name+'.npz'),allow_pickle=False) as saved:
                y,forces,times=saved['state'].copy(),saved['forces'].copy(),saved['timeSeconds'].copy()
            assert y.shape==(8000,28) and forces.shape==(8000,2,7) and np.isfinite(y).all() and np.isfinite(forces).all()
            if grid is None:grid=times
            else:assert np.array_equal(times,grid)
            assert arrays.accounting(prior,case,y,geometry.inertia)['passed'] and arrays.reference_energy(prior,geometry,case,y)['passed']
            for ry,rf in values.values():assert arrays.within(arrays.errors(y,ry,forces,rf),prior['referenceBounds'])
            values[settings['key']]=(y,forces)
        references[case['key']]=values

    reports=[]
    for case in spec['cases']:
        key=case['key'];candidate=data[key];state=candidate['state'];forces=candidate['forces']
        assert state.shape==(31997,28) and np.array_equal(candidate['timeMs'][::4]/1000,grid)
        model=HingeCoupledReference(spec,case,geometry);cosine=np.cos(spec['geometry']['pennationRadians'])
        domain=all(m.domain(state[:,sl]) and np.min(candidate['path'][:,i]-cosine*state[:,sl.start])>0
            for i,(m,sl) in enumerate(zip(model.muscles,model.slices)))
        release=max(float(np.max(abs(np.array([m.release(t/1000,0) for t in candidate['timeMs']])-candidate['release'][:,i]))) for i,m in enumerate(model.muscles))
        path=float(np.max(abs(geometry.measure(state[:,0])['length']-candidate['path'])))
        assert domain and release<=spec['releaseWaveformAbsolute'] and path<=spec['pathGeometryMeters']
        assert arrays.accounting(spec,case,state)['passed']
        assert all(candidate['recordedLedgers'][k]<=spec[v] for k,v in [('calciumLedger','calciumLedgerAbsolute'),
            ('fiberMomentumLedger','fiberMomentumLedgerNewtonSeconds'),('fiberKineticLedger','fiberKineticLedgerJoules')])
        refs=references['strong-left' if key in ('strong-fast','strong-retry') else key];comparisons=[]
        for name,(ry,rf) in refs.items():
            errors=arrays.errors(state[::4],ry,forces[::4],rf)
            comparisons.append(dict(reference=name,errors=errors,passed=arrays.within(errors,spec['candidateBounds'])))
        normalized={metric:max(r['errors'][metric] for r in comparisons)/bound for metric,bound in spec['candidateBounds'].items()}
        reports.append(dict(case=key,comparisons=comparisons,maximumNormalizedByMetric=normalized,
            passed=all(row['passed'] for row in comparisons),allNativeEndpointsReconciled=True,commonSamples=8000))
        print(key,reports[-1]['passed'],json.dumps(normalized),flush=True)
    result=dict(completed=True,numericalCriteriaPassed=all(r['passed'] for r in reports),cases=reports,
        fullOriginalConditionMatrixAssessed=True,actualPhysicsSteps=223972,sourceFrames=84000,
        independentReferencesReused=15,newReferenceSolves=0,originalBoundsPreserved=True,
        allNativeEndpointsReconciled=True,commonGridAccuracyOnly=True,finestIntermediateReferenceSamplesAssessed=False,
        packetAndRetryStateBitwiseEqual=True,newCaptureArchiveAccountingPassed=True,
        upstreamNumericalAccuracyQualified=False,liveBrainAuthorityQualified=False,physiologyQualified=False,avatarRecoveryAssessed=False)
    gate.save(package/'analysis.json',result)
    if not result['numericalCriteriaPassed']:raise RuntimeError('Full refined motor matrix failed original limits; preserve all recordings and bounds.')


if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    parser=argparse.ArgumentParser();parser.add_argument('package',type=Path);analyse(parser.parse_args().package.resolve())

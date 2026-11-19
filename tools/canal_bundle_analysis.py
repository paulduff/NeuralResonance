"""Compare saved native output with already reviewed independent trajectories."""
import gzip
import hashlib
import math
import numpy as np
from afferent_precision_gate import read
from coupled_canal_analysis import ratio
from canal_graded_protocol import ticks,reference_ticks


def expanded_hash(path):
    digest=hashlib.sha256()
    with gzip.open(path,'rb') as stream:
        for block in iter(lambda:stream.read(1024*1024),b''):digest.update(block)
    return digest.hexdigest()


def table(path,header):
    with gzip.open(path,'rt',encoding='utf-8') as stream:
        assert stream.readline().strip().split(',')==header,'Unexpected columns: '+str(path)
        data=np.loadtxt(stream,delimiter=',',ndmin=2)
    assert np.isfinite(data).all(),'Nonfinite saved output.'
    return data


def analyse(package):
    spec=read(package/'bundle-input.json');baseline=package/'baseline';clock=spec['frame']['clock']
    assert read(package/'study/input.json')==spec
    result=read(package/'study/results.json');assert result['completed'] and len(result['runs'])==3
    assert not result['newNativePhysicsExecuted'] and not result['connectedSensoryDynamicsExecuted']
    for name in ('parameters.json','basis.json'):
        assert read(package/'study'/name)==read(baseline/'study'/name),'Previously qualified mechanical parameters changed.'
    reference_review=read(package/'reviewed-clock-analysis.json')
    assert reference_review['referenceCriteriaPassed'] and reference_review['numericalCriteriaPassed']
    fine=262144;grid=np.array(reference_ticks(),dtype=np.int64)
    expected_ref_ticks=np.concatenate(([0],np.concatenate([i*fine+grid for i in range(199)])))
    with np.load(baseline/'reference-trajectories/samples.npz',allow_pickle=False) as f:
        assert np.array_equal(f['ticks'],expected_ref_ticks)
        references=f['decimal90'].copy()
    assert references.shape==(459293,12) and np.isfinite(references).all()
    geometry=read(package/'study/geometry.json');field=geometry['field'];attachment=geometry['attachment'];binding=geometry['stiffness']
    declared=spec['geometry'];placed=spec['attachment'];parameters=read(package/'study/parameters.json')
    assert field['organId']==declared['organId'] and field['canal']==declared['canal']==0
    assert field['geometrySource']==declared['source'] and field['sourceSha256'].lower()==declared['sourceSha256']
    assert field['shearModulusPascal']==declared['shearModulusPascal']
    assert attachment['hairCellId']==placed['hairCellId'] and attachment['organId']==field['organId'] and attachment['canal']==0
    for name in ('rootXOverRadius','bundleLengthMeters','excitatoryPolarity'):assert attachment[name]==placed[name]
    assert attachment['placementSource']==placed['source']
    stiffness=parameters['stiffnessPascalPerMeter3'][21]
    radius=(8*declared['shearModulusPascal']*declared['thicknessOverRadius']/(math.pi*stiffness))**(1/3)
    thickness=declared['thicknessOverRadius']*radius;area=math.pi*radius**2
    for name,expected in (('radiusMeters',radius),('thicknessMeters',thickness),('areaSquareMeters',area),('stiffnessPascalPerCubicMeter',stiffness)):
        assert abs(field[name]/expected-1)<=spec['geometryArithmeticRelative'],name
    assert binding['mechanicalParametersFingerprint']==parameters['fingerprint'] and binding['fieldFingerprint']==field['fingerprint']
    assert binding['mechanicalStiffnessPascalPerCubicMeter']==stiffness and binding['fieldStiffnessPascalPerCubicMeter']==field['stiffnessPascalPerCubicMeter']
    assert binding['relativeDifference']==abs(field['stiffnessPascalPerCubicMeter']/stiffness-1)<=spec['geometryArithmeticRelative']
    x=placed['rootXOverRadius'];root=-math.sqrt(1-x*x);tip=root+placed['bundleLengthMeters']/radius
    shape=2*(1-x*x-tip*tip);assert shape>0
    gain=placed['excitatoryPolarity']*shape/area*1e9
    candidate=spec['candidateBounds'];reports=[];common=[]
    common_grid=np.array(spec['queryReferenceTicks']['1'],dtype=np.int64)
    common_ticks=np.concatenate([i*fine+common_grid for i in range(199)])
    common_ref=references[np.searchsorted(expected_ref_ticks,common_ticks)]
    for refinement,run in zip((1,2,4),result['runs'],strict=True):
        folder=package/'study'/f'r{refinement}';old=baseline/'study'/f'r{refinement}'
        initial=read(folder/'initial.json');final=read(folder/'final.json');original_initial=read(old/'initial.json');original_final=read(old/'final.json')
        assert final==run and final['completed'] and final['error'] is None and final['refinement']==refinement
        assert final['state']['failure'] is None and final['state']['pendingTrace'] is None
        assert final['state']['mechanics']==original_final['state'],'Final mechanical state/accounting changed.'
        assert initial['initial']['mechanics']==original_initial['initial'] and initial['clock']==clock
        assert initial['schedule']==original_initial['schedule']
        n=353*refinement;interval_ticks=65536*refinement;factor=4//refinement
        qgrid=np.array(spec['queryReferenceTicks'][str(refinement)],dtype=np.int64);qcount=len(qgrid)
        assert final['steps']==199*n and final['denseSamples']==199*qcount and final['receipts']==201 and final['intervals']==199
        # Compare the complete saved 59-column stream, including every stage and energy ledger.
        assert expanded_hash(folder/'trajectory.csv.gz')==expanded_hash(old/'trajectory.csv.gz'),'Accepted mechanics or stage/energy accounting changed.'
        for j in range(201):
            endpoint=read(folder/f'endpoint-{j:03}.json')
            assert endpoint==read(old/f'endpoint-{j:03}.json'),'Original endpoint/receipt/acknowledgement changed.'
            if j<2:continue
            trace=read(folder/f'trace-{j:03}.json')
            assert trace['fingerprint'] and trace['mechanicalIntervalFingerprint']==endpoint['completedInterval']['fingerprint']
            assert trace['firstTick']==(j-1)*interval_ticks and trace['lastTick']==j*interval_ticks
            assert trace['ticksPerPhysicalInterval']==interval_ticks and trace['knots']==n+1 and trace['queryCount']==qcount
            for key,step in (('initial',j-1),('final',j)):
                sample=trace[key];old_endpoint=read(old/f'endpoint-{step:03}.json')['mechanics']
                assert sample['clockTick']==step*interval_ticks and sample['mechanics']==old_endpoint['point']
                assert sample['physicalTimeMs']==old_endpoint['physicalTimeMs'] and sample['neuralTimeMs']==old_endpoint['neuralTimeMs']
                bundle=sample['bundle'];assert bundle['hairCellId']==attachment['hairCellId'] and bundle['geometryFingerprint']==field['fingerprint'] and bundle['attachmentFingerprint']==attachment['fingerprint']
        rows=table(folder/'bundle.csv.gz',['index','physicalMs','neuralMs','intervalStep']+[f'state{i}' for i in range(12)]+
            ['solidVolume','solidFlow','meanDisplacement','shape','rootShear','bundleNm','bundleVelocityNmPerSecond'])
        assert rows.shape==(199*qcount,23)
        indices=np.concatenate([j*interval_ticks+qgrid//factor for j in range(1,200)])
        assert np.array_equal(rows[:,0],indices) and np.array_equal(rows[:,3],np.repeat(np.arange(2,201),qcount))
        elapsed=indices*(clock['physicsStepMs']/interval_ticks)
        assert np.array_equal(rows[:,1],clock['physicalEpochMs']+elapsed) and np.array_equal(rows[:,2],clock['neuralEpochMs']+elapsed)
        native_ticks=np.concatenate([j*interval_ticks+np.array(ticks(refinement)) for j in range(1,200)])
        native_lookup=np.searchsorted(indices,native_ticks);assert np.array_equal(indices[native_lookup],native_ticks)
        with gzip.open(old/'trajectory.csv.gz','rt',encoding='utf-8') as stream:
            stream.readline();native=np.loadtxt(stream,delimiter=',',ndmin=2)
        assert np.array_equal(rows[native_lookup,4:16],native[:,4:16]),'Interpolation altered accepted endpoint states.'
        target_ticks=(indices-interval_ticks)*factor;ref_indices=np.searchsorted(expected_ref_ticks,target_ticks)
        assert np.array_equal(expected_ref_ticks[ref_indices],target_ticks)
        target=references[ref_indices];states=rows[:,4:16];error=ratio(states,target,candidate)
        common_lookup=np.searchsorted(target_ticks,common_ticks);assert np.array_equal(target_ticks[common_lookup],common_ticks)
        common_error=ratio(states[common_lookup],common_ref,candidate);common.append(common_error['maximum'])
        # Separate analytic geometry arithmetic from inherited trajectory error.
        q,v=states[:,3],states[:,9]
        expected=np.column_stack((q,v,q/area,np.full(len(q),shape),-4*q*root/(area*radius),gain*q,gain*v))
        scale=spec['denseProjectionArithmeticAbsolute']+spec['denseProjectionArithmeticRelative']*abs(expected)
        arithmetic=float(np.max(abs(rows[:,16:23]-expected)/scale))
        displacement=float(np.max(abs(rows[:,21]-gain*target[:,3])/(abs(gain)*candidate['volumeAbsolute']+candidate['relative']*np.maximum(abs(rows[:,21]),abs(gain*target[:,3])))))
        velocity=float(np.max(abs(rows[:,22]-gain*target[:,9])/(abs(gain)*candidate['flowAbsolute']+candidate['relative']*np.maximum(abs(rows[:,22]),abs(gain*target[:,9])))))
        reports.append(dict(refinement=refinement,acceptedSteps=199*n,querySamples=len(rows),denseError=error,commonQueryError=common_error,
            bundleDisplacementMaximumFraction=displacement,bundleVelocityMaximumFraction=velocity,arithmeticMaximumFraction=arithmetic,
            arithmeticPassed=arithmetic<=1,allAcceptedStatesExactlyPreserved=True,originalClockAndReceiptsPreserved=True,
            maximumAbsBundleNanometers=float(np.max(abs(rows[:,21])))))
    errors=[r['denseError']['maximum'] for r in reports]
    refined=all(b<=spec['refinementMaximumRemainingFraction']*a+spec['refinementNormalizedFloor'] for a,b in zip(common,common[1:]))
    finest=reports[-1]
    passed=refined and errors[-1]<=1 and max(finest['bundleDisplacementMaximumFraction'],finest['bundleVelocityMaximumFraction'])<=1 and all(r['arithmeticPassed'] for r in reports)
    return dict(completed=True,numericalCriteriaPassed=bool(passed),sampledDenseRefinementPassed=bool(refined),runs=reports,
        originalMechanicalTrajectoriesExactlyPreserved=True,savedReferenceSamples=459293,newReferenceSolvesExecuted=False,
        acceptedMechanicalSteps=sum(r['acceptedSteps'] for r in reports),denseSamples=sum(r['querySamples'] for r in reports),
        continuousMotionAccuracyQualified=False,anatomicalCalibrationQualified=False,connectedSensoryDynamicsExecuted=False,
        afferentFiringQualified=False,avatarBalanceAssessed=False)

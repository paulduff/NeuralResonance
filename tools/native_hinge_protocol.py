"""Prospective comparison of reviewed native trajectories, without replaying physics."""
from copy import deepcopy
import hashlib
import json
import math
import struct

VERSION='native-hinge-captured-comparison-v1'
CASES=('quiet','left','right','balanced')
REFERENCE_BOUNDS=dict(jointAngleRadians=1e-9,jointSpeedRadiansPerSecond=1e-8,
    fiberLengthMeters=1e-9,fiberVelocityMetersPerSecond=1e-7,yieldFraction=1e-8,
    calcium=1e-8,activation=1e-8,fluxIntegral=1e-7,forceNewtons=1e-5,
    accelerationMetersPerSecondSquared=1e-4,impulseNewtonSeconds=1e-8,workJoules=1e-8)
CANDIDATE_BOUNDS=dict(jointAngleRadians=1e-5,jointSpeedRadiansPerSecond=1e-5,
    fiberLengthMeters=5e-7,fiberVelocityMetersPerSecond=5e-5,yieldFraction=1e-5,
    calcium=1e-5,activation=1e-5,fluxIntegral=1e-4,forceNewtons=.01,
    accelerationMetersPerSecondSquared=.1,impulseNewtonSeconds=1e-5,workJoules=1e-5)


def decode(value):
    if isinstance(value,dict):
        if set(value)=={'bits'}:
            bits=value['bits'];assert isinstance(bits,str) and len(bits)==16 and all(c in '0123456789ABCDEF' for c in bits)
            result=struct.unpack('>d',bytes.fromhex(bits))[0];assert math.isfinite(result);return result
        return {key:decode(child) for key,child in value.items()}
    if isinstance(value,list):return [decode(child) for child in value]
    return value


def read(path):return decode(json.loads(path.read_text(encoding='utf-8-sig')))


def make_protocol(native, muscle_input):
    original=read(muscle_input);spec={k:deepcopy(original[k]) for k in ('candidateVersion','muscleVersion','geometry','parameterRows',
        'unitRows','initialFiberLengthMeters','referenceSettings','maximumReferenceStepSeconds')}
    assert spec['unitRows']==[0,0]
    cases=[];geometry_json=None
    for name in CASES:
        folder=native/name;initial=read(folder/'initial.json');final=read(folder/'final.json')
        body=initial['native'];state=json.loads(body['stateJson']);clock=initial['clock']
        assert initial['scenario']==name and initial['muscleSubsteps']==2 and not initial['neuronalModelExecuted']
        assert initial['actualSteps']==1024 and final['plant']['completedPhysicsStep']==1024
        assert clock['initialPhysicsStep']==0 and clock['physicalEpochMs']==clock['neuralEpochMs']==0
        assert body['physicsStepMs']==clock['physicsStepMs'] and body['physicalTimeMs']==0
        assert hashlib.sha256(body['geometryJson'].encode()).hexdigest().upper()==initial['bodyFingerprint']
        if geometry_json is None:geometry_json=body['geometryJson']
        assert body['geometryJson']==geometry_json
        assert len(initial['definitions'])==2
        events=[]
        for i,definition in enumerate(initial['definitions']):
            assert definition['muscleId']=='m'+str(i) and definition['geometry']==spec['geometry']
            assert definition['initialFiberLengthMeters']==spec['initialFiberLengthMeters'] and definition['initialFiberVelocityMetersPerSecond']==0
            unit=definition['units'][0];parameter=spec['parameterRows'][0]
            assert len(definition['units'])==1 and all(unit[k]==parameter[k] for k in ('calcium','lengthGainAboveOptimal','lengthGainBelowOptimal'))
            batch=read(folder/f'source-{i}.json');contact=unit['contact']
            assert batch['sessionId']==clock['sessionId'] and batch['sourceInstanceKey']==contact['sourceInstanceKey']
            assert batch['completeThroughNeuralMs']==1024*clock['physicsStepMs']
            for spike in batch['spikes']:
                assert spike['sourceNeuronId']==contact['sourceNeuronId'] and spike['projectionId']==contact['projectionId']
                events.append(dict(muscleIndex=i,arrivalAtMs=spike['firedAtNeuralMs']+contact['conductionDelayMs'],
                    originalMessageId=spike['messageId'],originalSourceNeuronId=spike['sourceNeuronId'],originalContactId=contact['contactId']))
        assert len(events)==dict(quiet=0,left=2,right=2,balanced=4)[name]
        assert sorted({e['arrivalAtMs'] for e in events})==([] if name=='quiet' else [10.03125,60.03125])
        cases.append(dict(key=name,durationMs=1024*clock['physicsStepMs'],physicalStepMs=clock['physicsStepMs'],physicalSteps=1024,
            initialAngleRadians=state['angle'],initialSpeedRadiansPerSecond=state['speed'],
            events=sorted(events,key=lambda e:(e['arrivalAtMs'],e['muscleIndex']))))
    return dict(version=VERSION,**spec,cases=cases,nativeGeometry=json.loads(geometry_json),nativeGeometryJson=geometry_json,
        referenceBounds=REFERENCE_BOUNDS,candidateBounds=CANDIDATE_BOUNDS,
        calciumLedgerAbsolute=1e-9,fiberMomentumLedgerNewtonSeconds=1e-10,fiberKineticLedgerJoules=1e-7,
        referenceJointMomentumLedgerNewtonMeterSeconds=1e-10,referenceJointKineticLedgerJoules=1e-10,
        releaseWaveformAbsolute=1e-9,pathGeometryMeters=2e-6,tendonQuadratureAgreementJoules=1e-12,
        referenceTendonEnergyLedgerJoules=1e-8,
        nativePhysicsReexecuted=False,physicalStepRefinementAssessed=False,muscleStepRefinementAssessed=False,
        physiologyQualified=False,avatarRecoveryAssessed=False)

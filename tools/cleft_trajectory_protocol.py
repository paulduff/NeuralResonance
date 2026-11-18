"""Prospective complete trajectories and acceptance criteria; constructs fixtures only."""
from copy import deepcopy
from uuid import UUID,uuid5
from cleft_membrane_protocol import protocol as membrane_protocol
from cleft_dynamics_gate import COUNTS

VERSION='joint-cleft-trajectory-comparison-v1'
SESSION='32122cc1-eeb0-4f74-882c-41dc71d2ef4c'
STATE_FIELDS=('cleftMv','calyxMv','potassiumMm','sodiumMm','hairPotassiumLow','hairCalcium','hairHcn1',
              'calyxInnerKv74','calyxInnerHcn2','calyxOuterKv74','calyxOuterHcn2')
NODE_LEDGERS=('hairToCleftChargeFc','calyxToCleftChargeFc','calyxToPerilymphChargeFc','calyxToAxonChargeFc',
              'hairPotassiumAttomoles','hairSodiumAttomoles','calyxPotassiumAttomoles','calyxSodiumAttomoles')
EDGE_LEDGERS=('potassiumLeftToRightAttomoles','sodiumLeftToRightAttomoles','cleftLeftToRightChargeFc','calyxLeftToRightChargeFc')
FACE_LEDGERS=('potassiumToBathAttomoles','sodiumToBathAttomoles','chargeToBathFc')


def labels(fixture):
    t=fixture['topology'];nodes=sorted(t['compartments'],key=lambda x:x['id'])
    states=['hairMv']+[n['id']+'/'+name for n in nodes for name in STATE_FIELDS]
    ledgers=['metOutwardChargeFc','endolymphHoldingChargeFc','perilymphHoldingChargeFc']
    ledgers+=['membrane/'+n['id']+'/'+name for n in nodes for name in NODE_LEDGERS]
    ledgers+=['edge/'+e['id']+'/'+name for e in sorted(t['connections'],key=lambda x:x['id']) for name in EDGE_LEDGERS]
    ledgers+=['apex/'+f['id']+'/'+name for f in sorted(t['faces'],key=lambda x:x['id']) for name in FACE_LEDGERS]
    return states,ledgers


def group(label):
    if 'Attomoles' in label:return 'amount'
    if label.endswith('Fc'):return 'charge'
    if label.endswith('Mv'):return 'voltage'
    if label.endswith('Mm'):return 'ion'
    return 'gate'


def protocol():
    seeds={c['key']:c for c in membrane_protocol()['cases']};cases=[]
    definitions=[('passive-gates','1-uniform-mixed-passive',4000,'quiet'),
        ('quiet-active','1-uniform-mixed-baseline',16000,'quiet'),
        ('bundle-one','1-uniform-mixed-baseline',16000,'bundle'),
        ('axon-one','1-uniform-mixed-baseline',16000,'axon'),
        ('graded-two','2-graded-mixed-baseline',16000,'quiet'),
        ('bundle-two','2-uniform-mixed-baseline',16000,'bundle'),
        ('bundle-eight','8-graded-mixed-baseline',4000,'bundle')]
    for key,seed,total,drive in definitions:
        fixture=deepcopy(seeds[seed])
        if key=='passive-gates':
            fixture['hairMv']=0.
            for s in fixture['states']:s['cleftMv']=s['calyxMv']=0.
        nodes=sorted(fixture['topology']['compartments'],key=lambda n:n['id']);segments=[]
        for part in range(4):
            bundle=(0.,200.,-200.,0.)[part] if drive=='bundle' else 0.
            current=(0.,10.,-10.,0.)[part] if drive=='axon' else 0.
            segments.append(dict(endTick=total*(part+1)//4,bundleDisplacementNanometers=bundle,
                axonCurrents=[dict(id=n['id'],outgoingPa=current/len(nodes)) for n in nodes]))
        cases.append(dict(key=key,cellId=str(uuid5(UUID(SESSION),key)),fixture=fixture,segments=segments,totalTicks=total))
    return dict(version=VERSION,kernelVersion='joint-hair-cleft-calyx-sdirk2-compensated-v1',sessionId=SESSION,
        ticksPerMs=2000,sampleTicks=20,stepTicks=[4,2,1],expectedCases=7,expectedNativeRuns=21,expectedReferences=21,cases=cases,
        nonlinearSettings=dict(voltageAbsoluteToleranceMv=1e-9,ionAbsoluteToleranceMm=1e-11,gateAbsoluteTolerance=1e-12,
            relativeTolerance=1e-11,maximumNewtonIterations=12,maximumLineSearchHalvings=16),
        referenceDecimalPrecision=40,referenceCheckPrecisions=[60,90],
        referenceSettings=[dict(key='radau-standard',method='Radau',rtol=2e-9,atolScale=1.),
            dict(key='radau-tight',method='Radau',rtol=2e-11,atolScale=.01),dict(key='bdf-tight',method='BDF',rtol=2e-11,atolScale=.01)],
        referenceAtol=dict(voltage=2e-10,ion=2e-12,gate=2e-13,charge=2e-10,amount=2e-12),
        referenceComparison=dict(absolute=dict(voltage=5e-7,ion=5e-9,gate=5e-10,charge=5e-7,amount=5e-9),relative=1e-8),
        candidateComparison=dict(absolute=dict(voltage=.002,ion=2e-5,gate=2e-6,charge=.0002,amount=2e-6),relative=2e-6),
        refinement=dict(maximumRemainingFraction=.4,normalizedErrorFloor=.01),
        ledgerResidualBounds=dict(chargeFc=2e-8,ionAttomoles=2e-9),
        stationaryInitializationQualified=False,spatialGeometryCalibrated=False,afferentFiringQualified=False,
        physiologyQualified=False,avatarRecoveryAssessed=False)


def validate(value):
    assert value==protocol(),'Prospective trajectory fixtures or bounds changed.'
    return value

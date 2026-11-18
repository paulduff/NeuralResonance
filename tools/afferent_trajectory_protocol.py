"""Prospective fast joint electrical trajectories; fixture construction only."""
from copy import deepcopy
from uuid import UUID,uuid5
from afferent_numerical_protocol import protocol as derivatives,GATES
from cleft_trajectory_protocol import labels as sensory_labels,group
from afferent_dynamics_gate import COUNTS

VERSION='joint-sensory-nerve-fast-trajectory-v1'
SESSION='016401f0-73b2-49c7-917a-caf3909b3397'


def labels(fixture):
    state,ledger=sensory_labels(fixture['sensory']);t=fixture['cableTopology']
    for n in sorted(t['nodes'],key=lambda n:n['id']):state+=['axon/'+n['id']+'/'+k for k in ('potentialMv',)+GATES]
    ledger+=['axon-membrane/'+n['id']+'/outwardChargeFc' for n in sorted(t['nodes'],key=lambda n:n['id'])]
    ledger+=['axon-edge/'+e['id']+'/leftToRightChargeFc' for e in sorted(t['edges'],key=lambda e:e['id'])]
    ledger+=['axon-terminal/'+t['id']+'/nodeToCalyxChargeFc' for t in sorted(t['terminals'],key=lambda t:t['id'])]
    return state,ledger


def protocol():
    seeds={r['key']:r for r in derivatives()['joints']};cases=[]
    definitions=[('quiet-one','joint-1-uniform-1-behind',16384,'quiet',True),
        ('bundle-one','joint-1-uniform-1-behind',16384,'bundle',True),
        ('loaded-one','joint-1-uniform-1-behind',4096,'quiet',False),
        ('reverse-one','joint-1-uniform-1-ahead',4096,'quiet',False),
        ('cut-one','joint-1-uniform-1-cut',4096,'quiet',False),
        ('graded-two','joint-2-graded-1-behind',16384,'bundle',True),
        ('bundle-eight','joint-8-graded-1-behind',4096,'bundle',True)]
    for key,seed,total,drive,matched in definitions:
        f=deepcopy(seeds[seed])
        if matched:
            potential=next(s['calyxMv'] for s in f['sensory']['states'] if s['id']=='p00')
            for s in f['cableStates']:s['potentialMv']=potential
        segments=[dict(endTick=total*(i+1)//4,bundleDisplacementNanometers=(0.,200.,-200.,0.)[i] if drive=='bundle' else 0.) for i in range(4)]
        cases.append(dict(key=key,cellId=str(uuid5(UUID(SESSION),key)),fixture=f,segments=segments,totalTicks=total))
    return dict(version=VERSION,kernelVersion='joint-hair-cleft-calyx-axon-sdirk2-compensated-v1',sessionId=SESSION,
        ticksPerMs=262144,sampleTicks=16,stepTicks=[4,2,1],expectedCases=7,expectedNativeRuns=21,expectedReferences=21,cases=cases,
        nonlinearSettings=dict(voltageAbsoluteToleranceMv=1e-9,ionAbsoluteToleranceMm=1e-11,gateAbsoluteTolerance=1e-12,
            relativeTolerance=1e-11,maximumNewtonIterations=12,maximumLineSearchHalvings=16),
        referenceDecimalPrecision=40,referenceCheckPrecisions=[60,90],referenceMaximumStepMs=.001,
        referenceSettings=[dict(key='radau-standard',method='Radau',rtol=2e-9,atolScale=1.),
            dict(key='radau-tight',method='Radau',rtol=2e-11,atolScale=.01),dict(key='bdf-tight',method='BDF',rtol=2e-11,atolScale=.01)],
        referenceAtol=dict(voltage=2e-10,ion=2e-12,gate=2e-13,charge=2e-10,amount=2e-12),
        referenceComparison=dict(absolute=dict(voltage=5e-7,ion=5e-9,gate=5e-10,charge=5e-7,amount=5e-9),relative=1e-8),
        candidateComparison=dict(absolute=dict(voltage=.002,ion=2e-5,gate=2e-6,charge=.0002,amount=2e-6),relative=2e-6),
        refinement=dict(maximumRemainingFraction=.4,normalizedErrorFloor=.01),
        ledgerResidualBounds=dict(chargeFc=2e-8,ionAttomoles=2e-9,cableChargeFc=2e-8,interfaceChargeFc=2e-8),
        fastTransientOnly=True,stationaryInitializationQualified=False,spatialGeometryCalibrated=False,
        afferentFiringQualified=False,physiologyQualified=False,avatarRecoveryAssessed=False)


def validate(value):
    assert value==protocol(),'Prospective sensory nerve trajectories or accuracy bounds changed.'
    return value

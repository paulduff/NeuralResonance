"""Prospective coupled derivative fixtures; no membrane equations are evaluated."""
from copy import deepcopy
from itertools import product
from vestibular_hair_cell_protocol import PARAMETERS as HAIR
from calyx_membrane_protocol import PARAMETERS as CALYX
from cleft_open_protocol import COUNTS as OPEN_COUNTS

COUNTS = dict(OPEN_COUNTS, VestibularCleftMembraneTests=12, VestibularHairCellTests=23, CalyxMembraneTests=22)
VERSION = 'coupled-cleft-membrane-derivative-comparison-v1'
KERNEL = 'local-hair-calyx-open-cleft-membranes-v1'
# key, hair/calyx/cleft voltage relative to perilymph, K, Na, bundle nm,
# gate preset, total prescribed axon terminal current. These are fixtures.
SCENARIOS = [
    ('mixed',-70.,-60.,0.,5.,140.,0.,'mixed',0.),
    ('gates-zero',-70.,-60.,0.,5.,140.,0.,'zero',0.),
    ('gates-one',-70.,-60.,0.,5.,140.,0.,'one',0.),
    ('hyperpolarized',-110.,-100.,0.,.5,180.,-1000.,'mixed',0.),
    ('depolarized',20.,10.,0.,20.,100.,1000.,'mixed',0.),
    ('cleft-positive',-70.,-60.,9.,5.,140.,0.,'mixed',0.),
    ('cleft-negative',-70.,-60.,-9.,5.,140.,0.,'mixed',0.),
    ('depleted',-70.,-60.,0.,.5,60.,0.,'mixed',0.),
    ('accumulated',-70.,-60.,0.,30.,200.,0.,'mixed',0.),
    ('met-reversal',5.,-60.,0.,5.,140.,0.,'mixed',0.),
    ('axon-out',-70.,-60.,0.,5.,140.,300.,'mixed',25.),
    ('axon-in',-70.,-60.,0.,5.,140.,-300.,'mixed',-25.),
]


def protocol():
    cases=[]
    for count, shape, scenario, variant in product((1,2,8),('uniform','graded'),SCENARIOS,('baseline','no-met','passive')):
        key,hv,cv,sv,k,na,bundle,preset,axon=scenario
        hp=deepcopy(HAIR);cp=deepcopy(CALYX)
        if variant=='no-met': hp['metMaximumNs']=0.
        if variant=='passive':
            for field in ('metMaximumNs','potassiumLowMaximumNs','calciumMaximumNs','hcn1MaximumNs','pumpDensityPerSquareMicrometer'): hp[field]=0.
            for field in ('kv74MaximumNs','hcn2MaximumNs','innerPumpDensityPerSquareMicrometer','outerPumpDensityPerSquareMicrometer','innerKccMaximumPaPerSquareMicrometer','outerLeakNs'): cp[field]=0.
        weights=[1/count if shape=='uniform' else (i+1)/(count*(count+1)/2) for i in range(count)]
        nodes=[dict(id=f'p{i:02}',volumeCubicMicrometers=8*w,
            hairCleftPf=hp['specificCapacitancePfPerSquareMicrometer']*hp['cleftFacingAreaSquareMicrometers']*w,
            calyxInnerPf=cp['specificCapacitancePfPerSquareMicrometer']*cp['innerAreaSquareMicrometers']*w,
            calyxOuterPf=cp['specificCapacitancePfPerSquareMicrometer']*cp['outerAreaSquareMicrometers']*w) for i,w in enumerate(weights)]
        links=[dict(id=f'e{i:02}',leftId=nodes[i]['id'],rightId=nodes[i+1]['id'],
            cleftFaceAreaSquareMicrometers=.02,centerDistanceMicrometers=.5,calyxAxialConductanceNs=3.) for i in range(count-1)]
        perilymph=0. if shape=='uniform' else 32.
        states=[];inputs=[]
        for i,(node,w) in enumerate(zip(nodes,weights,strict=True)):
            grade=i if shape=='graded' else 0
            states.append(dict(id=node['id'],cleftMv=perilymph+sv+2*grade,calyxMv=perilymph+cv+grade,
                               potassiumMm=k+.25*grade,sodiumMm=na+grade))
            gates=[0.]*7 if preset=='zero' else [1.]*7 if preset=='one' else [.2+.1*grade/count,.3,.4,.2,.3+.1*grade/count,.4,.5]
            inputs.append(dict(id=node['id'],hairGates=dict(zip(('potassiumLow','calcium','hcn1'),gates[:3],strict=True)),
                calyxGates=dict(zip(('innerKv74','innerHcn2','outerKv74','outerHcn2'),gates[3:],strict=True)),axonOutgoingPa=axon*w))
        if len(cases)%2: states.reverse();inputs.reverse()
        topology=dict(hairCellId='coupled-fixture-hair',hairApicalPf=hp['apicalCapacitancePf'],
            hairPerilymphPf=(hp['basolateralAreaSquareMicrometers']-hp['cleftFacingAreaSquareMicrometers'])*hp['specificCapacitancePfPerSquareMicrometer'],
            material=dict(potassiumDiffusionSquareMicrometersPerMs=.81,sodiumDiffusionSquareMicrometersPerMs=.56,thermalVoltageMv=26.,otherIonConductivityNsPerMicrometer=600.),
            compartments=nodes,connections=links,reservoir=dict(id='perilymph',potassiumMm=5.,sodiumMm=140.),
            faces=[dict(id='apex',compartmentId=nodes[-1]['id'],areaSquareMicrometers=.02,centerToBathDistanceMicrometers=.25)])
        cases.append(dict(index=len(cases),key=f'{count}-{shape}-{key}-{variant}',hairParameters=hp,calyxParameters=cp,
            topology=topology,hairMv=perilymph+hv,perilymphMv=perilymph,bundleDisplacementNanometers=bundle,states=states,inputs=inputs))
    return dict(version=VERSION,kernelVersion=KERNEL,cases=cases,expectedCases=216,expectedClosedRows=624,expectedOpenRows=192,
        decimalPrecisions=[60,90],referenceAbsoluteTolerance='1e-40',referenceRelativeTolerance='1e-40',
        candidateAbsoluteTolerance='2e-10',candidateRelativeTolerance='2e-12',
        timeIntegrated=False,spatialGeometryCalibrated=False,afferentFiringQualified=False,physiologyQualified=False,avatarRecoveryAssessed=False)


def validate(value):
    assert value==protocol(),'Coupled membrane fixtures or original bounds changed.'
    return value

"""Prospective sensory nerve derivative fixtures; no equations are evaluated."""
from copy import deepcopy
from itertools import product
import math
from cleft_membrane_protocol import protocol as sensory_protocol
from afferent_component_gate import COUNTS

VERSION='sensory-afferent-derivative-comparison-v1'
GATES=('sodiumActivation','sodiumInactivation','kv1Activation','kv7Activation','kv34Activation','kv34Inactivation')
MIXED=dict(zip(GATES,(.2,.3,.4,.5,.6,.7),strict=True))
NODE=dict(specificCapacitancePfPerSquareMicrometer=.01,sodiumMaximumNsPerSquareMicrometer=120.,
    kv1MaximumNsPerSquareMicrometer=.13,kv7MaximumNsPerSquareMicrometer=.1,kv34MaximumNsPerSquareMicrometer=.2,
    insidePotassiumMm=150.,outsidePotassiumMm=5.,insideSodiumMm=12.,outsideSodiumMm=140.,thermalVoltageMv=26.)
UNMYEL=dict(NODE,sodiumMaximumNsPerSquareMicrometer=30.,kv1MaximumNsPerSquareMicrometer=0.,kv7MaximumNsPerSquareMicrometer=0.,kv34MaximumNsPerSquareMicrometer=0.)
MYEL=dict(UNMYEL,sodiumMaximumNsPerSquareMicrometer=0.,specificCapacitancePfPerSquareMicrometer=.00002)


def chain(divisions):
    boundaries=(0.,8.,9.,80.,82.,382.,384.,425.)
    regions=('unmyelinated','heminode','internode1','node1','internode2','node2','distal')
    parameters=(UNMYEL,NODE,MYEL,NODE,MYEL,NODE,MYEL);nodes=[];edges=[]
    for i,name in enumerate(regions):
        for j in range(divisions):
            node=dict(id=f'{name}-{j:03}',lengthMicrometers=(boundaries[i+1]-boundaries[i])/divisions,radiusMicrometers=1.5,membrane=deepcopy(parameters[i]))
            if nodes:edges.append(dict(id=f'edge-{len(nodes):04}',leftId=nodes[-1]['id'],rightId=node['id'],crossSectionSquareMicrometers=math.pi*1.5*1.5,
                centerDistanceMicrometers=(nodes[-1]['lengthMicrometers']+node['lengthMicrometers'])/2,conductivityNsPerMicrometer=1000.))
            nodes.append(node)
    return dict(nodes=nodes,edges=edges,terminals=[dict(id='calyx',nodeId=nodes[0]['id'],conductanceNs=1000*math.pi*1.5*1.5/(nodes[0]['lengthMicrometers']/2)),
        dict(id='distal-sealed',nodeId=nodes[-1]['id'],conductanceNs=0.)])


def protocol():
    presets=dict(zero=dict.fromkeys(GATES,0.),one=dict.fromkeys(GATES,1.),mixed=MIXED)
    volts=set(float(v) for v in range(-55,61,5));volts.update(v+2.5 for v in range(-55,60,5))
    volts.update((-140.,-100.,-80.,-78.8,-65.84,-60.,-41.,-40.1,-31.3,61.,80.))
    for endpoint in (-55.,-45.,60.):volts.update((endpoint-1e-6,endpoint+1e-6))
    profiles=dict(node=NODE,unmyelinated=UNMYEL,myelinated=MYEL,potassiumRaised=dict(NODE,outsidePotassiumMm=10.))
    for name in ('sodium','kv1','kv7','kv34'):profiles[name+'Removed']=dict(NODE,**{name+'MaximumNsPerSquareMicrometer':0.})
    membranes=[dict(key=f'{name}-{v:.17g}-{preset}',parameters=deepcopy(p),voltageMv=v,gates=deepcopy(g))
        for (name,p),v,(preset,g) in product(profiles.items(),sorted(volts),presets.items())]
    clamps=[dict(key=f'clamp-{v:g}-{dt:g}-{preset}',voltageMv=v,elapsedMs=dt,gates=deepcopy(g))
        for v,dt,(preset,g) in product((-100.,-55.,-45.,-40.1,0.,60.,80.),(0.,.01,1.,50.),presets.items())]
    cables=[]
    for divisions,bath,style in product((1,2,4),(0.,32.),('uniform','graded','forward','reverse')):
        topology=chain(divisions);states=[]
        for i,n in enumerate(topology['nodes']):
            vm=-80+2*i if style=='graded' else -70.
            gates=dict(MIXED,sodiumActivation=.1+.2*i/(len(topology['nodes'])-1))
            states.append(dict(id=n['id'],potentialMv=bath+vm,gates=gates))
        terminal=-60. if style=='forward' else -80. if style=='reverse' else -70.
        cables.append(dict(key=f'chain-{divisions}-bath{bath:g}-{style}',topology=topology,perilymphMv=bath,states=states[::-1],
            terminalStates=[dict(id='distal-sealed',potentialMv=bath),dict(id='calyx',potentialMv=bath+terminal)]))
    # Analytic passive graph and a nonzero driven boundary independently check
    # incidence, current direction and power, without physiological assumptions.
    pair=dict(nodes=[dict(id=n,lengthMicrometers=1.,radiusMicrometers=1.,membrane=deepcopy(MYEL)) for n in ('a','b')],
        edges=[dict(id='ab',leftId='a',rightId='b',crossSectionSquareMicrometers=2.,centerDistanceMicrometers=4.,conductivityNsPerMicrometer=6.)],
        terminals=[dict(id='boundary',nodeId='a',conductanceNs=0.)])
    for drive in (False,True):
        topology=deepcopy(pair);topology['terminals'][0]['conductanceNs']=2. if drive else 0.
        cables.append(dict(key='analytic-driven' if drive else 'analytic-passive',topology=topology,perilymphMv=0.,
            states=[dict(id='a',potentialMv=0.,gates=deepcopy(MIXED)),dict(id='b',potentialMv=10.,gates=deepcopy(MIXED))],
            terminalStates=[dict(id='boundary',potentialMv=20.)]))
    seeds={c['key']:c for c in sensory_protocol()['cases']};joints=[]
    for count,shape,divisions,style in product((1,2,8),('uniform','graded'),(1,2),('behind','ahead','cut')):
        fixture=deepcopy(seeds[f'{count}-{shape}-mixed-baseline']);topology=chain(divisions)
        if style=='cut':topology['terminals'][0]['conductanceNs']=0.
        vm=-50. if style=='ahead' else -70.;bath=fixture['perilymphMv']
        states=[dict(id=n['id'],potentialMv=bath+vm+i*.125,gates=deepcopy(MIXED)) for i,n in enumerate(topology['nodes'])]
        joints.append(dict(key=f'joint-{count}-{shape}-{divisions}-{style}',sensory=fixture,cableTopology=topology,
            attachments=[dict(terminalId='calyx',cleftCompartmentId='p00')],cableStates=states[::-1]))
    for count,divisions in product((2,8),(1,2)):
        row=deepcopy(next(c for c in joints if c['key']==f'joint-{count}-graded-{divisions}-behind'));row['key']=f'multiple-{count}-{divisions}'
        original=row['cableTopology']['terminals'][0];g=original['conductanceNs'];original['conductanceNs']=.75*g
        row['cableTopology']['terminals'].append(dict(id='calyx-other',nodeId=original['nodeId'],conductanceNs=.25*g))
        row['attachments'].append(dict(terminalId='calyx-other',cleftCompartmentId=f'p{count-1:02}'));joints.append(row)
    groups=dict(membranes=membranes,clamps=clamps,cables=cables,joints=joints)
    for rows in groups.values():assert len({r['key'] for r in rows})==len(rows)
    return dict(version=VERSION,membraneVersion='vestibular-afferent-deposited-may14-linear-v1',
        cableVersion='vestibular-identified-finite-volume-axon-v1',jointVersion='joint-cleft-afferent-current-owner-v1',
        expectedCounts={k:len(v) for k,v in groups.items()},**groups,decimalPrecisions=[60,90],
        referenceComparison=dict(absolute='1e-40',relative='1e-40'),
        membraneComparison=dict(absolute='2e-10',relative='2e-12'),clampComparison=dict(absolute='2e-13',relative='2e-12'),
        cableComparison=dict(absolute='2e-8',relative='2e-12'),sensoryComparison=dict(absolute='2e-10',relative='2e-12'),
        conservation=dict(absoluteCurrentPa=1e-10,absolutePowerFw=1e-8,roundoffMultiplier=256.,binary64Epsilon=2.220446049250313e-16),
        residualFieldsCheckedByConservation=True,timeIntegrated=False,stationaryInitializationQualified=False,
        spatialGeometryCalibrated=False,afferentFiringQualified=False,physiologyQualified=False,avatarRecoveryAssessed=False)


def validate(value):
    assert value==protocol(),'Prospective sensory nerve fixtures or bounds changed.'
    return value

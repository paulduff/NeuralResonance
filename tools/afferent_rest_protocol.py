"""Prospective connected sensory/nerve stationary study; constructs inputs only."""
from copy import deepcopy
from afferent_numerical_protocol import protocol as derivatives
from afferent_stationary_gate import COUNTS
from afferent_trajectory_protocol import labels,group
from cleft_rest_protocol import protocol as sealed

VERSION='joint-sensory-nerve-stationary-comparison-v1'


def protocol():
    derivative=derivatives();seeds={c['key']:c for c in derivative['joints']};old=sealed();cases=[]
    for count in (1,2,8):
        for shape in ('uniform','graded'):
            for name,voltage,k_scale,na_scale in (('original',0.,1.,1.),('hyperpolarized',-20.,.8,.9),('depolarized',20.,1.2,1.1)):
                f=deepcopy(seeds[f'joint-{count}-{shape}-1-behind']);s=f['sensory'];s['hairMv']+=voltage
                for state in s['states']:
                    state['calyxMv']+=voltage;state['potassiumMm']*=k_scale;state['sodiumMm']*=na_scale
                for state in f['cableStates']:state['potentialMv']+=voltage
                assert s['bundleDisplacementNanometers']==0 and all(i['axonOutgoingPa']==0 for i in s['inputs'])
                cases.append(dict(key=f'{count}-{shape}-{name}',geometryKey=f'{count}-{shape}',seed=name,fixture=f))
    return dict(version=VERSION,kernelVersion='joint-cleft-afferent-algebraic-stationary-search-v1',expectedCases=18,cases=cases,
        nativeSettings=deepcopy(old['nativeSettings']),rootCriteria=deepcopy(old['rootCriteria']),reference=deepcopy(old['reference']),
        sameRootDistance=deepcopy(old['sameRootDistance']),precisionComparison=deepcopy(old['precisionComparison']),
        derivativeCriteria={k:deepcopy(derivative[k]) for k in ('sensoryComparison','cableComparison','conservation')},
        stability=dict(deepcopy(old['stability']),eigenvectorsRetained=True),
        stabilityControls=[dict(name=name,matrix=matrix,expected=expected) for name,matrix,expected in (
            ('stable',[[-2.,3.],[0.,-1.]],'locally-decaying estimate'),
            ('unstable',[[-2.,0.],[0.,1.]],'locally-growing estimate'),
            ('neutral',[[-2.,0.],[0.,0.]],'inconclusive'),
            ('oscillatory',[[-1.,-3.],[3.,-1.]],'locally-decaying estimate'),
            ('defective',[[-1.,1.],[0.,-1.]],'inconclusive'))],
        terminalCondition='same-state reciprocal calyx/axon attachment; explicit sealed distal boundary',
        cableGeometry='one finite-volume cell per declared region; not a spatial convergence result',
        originalBiologyChanged=False,spatialGeometryCalibrated=False,physiologicalRestQualified=False,
        afferentFiringQualified=False,avatarRecoveryAssessed=False)


def validate(value):
    assert value==protocol(),'Connected stationary inputs, method or bounds changed.'
    return value

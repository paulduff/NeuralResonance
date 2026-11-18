"""Active stationary roots and full-state local stability; constructs inputs only."""
from copy import deepcopy
from cleft_membrane_protocol import protocol as membrane_protocol
from cleft_stationary_gate import COUNTS

VERSION='active-cleft-stationary-comparison-v1'


def protocol():
    seeds={c['key']:c for c in membrane_protocol()['cases']};cases=[]
    for count in (1,2,8):
        for shape in ('uniform','graded'):
            for name,voltage,k_scale,na_scale in (('original',0.,1.,1.),('hyperpolarized',-20.,.8,.9),('depolarized',20.,1.2,1.1)):
                fixture=deepcopy(seeds[f'{count}-{shape}-mixed-baseline'])
                fixture['hairMv']+=voltage
                for s in fixture['states']:
                    s['calyxMv']+=voltage;s['potassiumMm']*=k_scale;s['sodiumMm']*=na_scale
                assert fixture['bundleDisplacementNanometers']==0 and all(x['axonOutgoingPa']==0 for x in fixture['inputs'])
                cases.append(dict(key=f'{count}-{shape}-{name}',geometryKey=f'{count}-{shape}',seed=name,fixture=fixture))
    return dict(version=VERSION,kernelVersion='joint-cleft-algebraic-stationary-search-v1',expectedCases=18,cases=cases,
        nativeSettings=dict(voltageRateToleranceMvPerMs=1e-10,ionRateToleranceMmPerMs=1e-12,gateRateTolerancePerMs=1e-13,
            maximumNewtonIterations=80,maximumLineSearchHalvings=28),
        rootCriteria=dict(voltageRateMvPerMs=1e-9,ionRateMmPerMs=1e-11,gateRatePerMs=1e-12),
        reference=dict(decimalPrecision=60,witnessPrecision=90,method='trf',jacobian='3-point',maximumEvaluations=2000,
            ftol=1e-15,xtol=1e-15,gtol=1e-15,voltageUnknownScaleMv=50.,logIonUnknownScale=1.,
            voltageResidualScale=1.,ionResidualScale=.01),
        sameRootDistance=dict(voltageMv=1e-4,ionMm=1e-6,gate=1e-7),
        derivativeComparison=dict(absolute='2e-10',relative='2e-12'),
        precisionComparison=dict(absolute='1e-40',relative='1e-40'),
        stability=dict(centralDifferencePowers=[12,14,16],voltageScaleMv=100.,ionScaleMm=100.,gateScale=1.,
            classificationMarginPerMs=1e-10,eigenResidualLimit=1e-10,roundoffMultiplier=64.,
            ionScaleCappedAtRootConcentration=True,freeGateBlockAbsoluteTolerance=1e-12,freeGateBlockRelativeTolerance=1e-12,
            jacobianRefinementMaximumRemainingFraction=.4,jacobianRefinementAbsoluteFloorPerMs=1e-10,
            uncertaintyIsEmpirical=True,gateEliminationDuringJacobian=False,nonlinearPerturbationsIntegrated=False),
        terminalCondition='explicit zero current: isolated sealed terminal, no afferent cable',
        spatialGeometryCalibrated=False,physiologicalRestQualified=False,afferentFiringQualified=False,avatarRecoveryAssessed=False)


def validate(value):
    assert value==protocol(),'Stationary fixtures, method or criteria changed.'
    return value

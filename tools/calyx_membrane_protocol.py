"""Declared isolated calyx arithmetic/clamp inputs, not model execution."""
from copy import deepcopy

COUNTS=dict(CalyxMembraneTests=22,SolutionManifestTests=1)
PARAMETERS=dict(innerAreaSquareMicrometers=294.63,outerAreaSquareMicrometers=288.33,
    specificCapacitancePfPerSquareMicrometer=.01,kv74MaximumNs=24.,kv74InnerFraction=.8,
    hcn2MaximumNs=3.7,hcn2InnerFraction=.5,innerPumpDensityPerSquareMicrometer=1000.,
    outerPumpDensityPerSquareMicrometer=2000.,innerKccMaximumPaPerSquareMicrometer=2.,outerLeakNs=2.)
VARIANTS=dict(baseline={},no_kv74=dict(kv74MaximumNs=0.),no_hcn2=dict(hcn2MaximumNs=0.),
    no_pumps=dict(innerPumpDensityPerSquareMicrometer=0.,outerPumpDensityPerSquareMicrometer=0.),
    no_kcc=dict(innerKccMaximumPaPerSquareMicrometer=0.),no_leak=dict(outerLeakNs=0.))


def protocol():
    return dict(version='calyx-membrane-comparison-v1',kernelVersion='isolated-calyx-membranes-deposited-20221205-v1',
        parameters=deepcopy(PARAMETERS),variants=[dict(key=k,parameters=dict(PARAMETERS,**v)) for k,v in VARIANTS.items()],
        voltagesMv=[-120+5*i for i in range(37)],cleftPotentialsMv=[-9.,0.,9.],
        cleftPotassiumMm=[3.,5.,20.],cleftSodiumMm=[100.,140.],gatePresets=['zero','one','mixed','conditioned'],
        clampVoltagesMv=[[-90.,0.],[-60.,0.],[-40.,0.],[-90.,9.],[-60.,9.],[-40.,9.],[-60.,-9.],[0.,0.]],
        clampDurationsMs=[0.,.125,.5,1.,10.,100.,1000.],clampSubdivisionMs=.125,
        arithmeticAbsoluteTolerance=2e-10,arithmeticRelativeTolerance=2e-12,clampAbsoluteTolerance=1e-12,
        expectedArithmeticRows=15984,expectedClampRows=224,expectedVariants=6,
        freeMembraneIntegrated=False,cleftDynamicsIntegrated=False,afferentFiringQualified=False,
        liveAvatarSelected=False,physiologyQualified=False,avatarRecoveryAssessed=False)


def validate(spec):
    assert spec==protocol(), 'Calyx inputs/criteria changed.'
    return spec

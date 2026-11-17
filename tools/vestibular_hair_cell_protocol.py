"""Prospective physical inputs and numerical criteria; no equations are evaluated."""
from copy import deepcopy


PARAMETERS = dict(basolateralAreaSquareMicrometers=340.21, cleftFacingAreaSquareMicrometers=294.63,
    apicalCapacitancePf=3., specificCapacitancePfPerSquareMicrometer=.01,
    metMaximumNs=5., potassiumLowMaximumNs=80., calciumMaximumNs=.5, hcn1MaximumNs=4.2,
    pumpDensityPerSquareMicrometer=1000., endolymphPotentialMv=5., metReversalMv=0.)
BATH = dict(potassiumOutsideMm=5., sodiumOutsideMm=140., calciumOutsideMm=1.3,
    potassiumInsideMm=150., sodiumInsideMm=12., calciumInsideMm=.001, basolateralOutsidePotentialMv=0.)
VERSION = 'isolated-type-I-hair-cell-fixed-bath-v1'
CELL_VERSION = VERSION + '+local-clock-rk4-compensated-v1'
COUNTS = dict(VestibularHairCellTests=23, ClockedVestibularMechanicsTests=16,
              VestibularLabyrinthTransducerTests=7, SolutionManifestTests=1)
HISTORIES = {
    'quiet': [(0, 0)],
    'positive': [(0, 0), (250, 300), (750, 0)],
    'negative': [(0, 0), (250, -300), (750, 0)],
    'strong': [(0, 0), (250, 1000), (750, 0)],
    'reversal': [(0, 0), (250, 300), (500, -300), (750, 0)],
    'paired': [(0, 0), (250, 1000), (300, 0), (500, 1000), (550, 0)],
}
REFERENCE_SETTINGS = [
    dict(key='dop853', method='DOP853', relativeTolerance=1e-10, maxStepMs=.25),
    dict(key='dop853-tight', method='DOP853', relativeTolerance=1e-12, maxStepMs=.125),
    dict(key='radau', method='Radau', relativeTolerance=1e-11, maxStepMs=.25),
]


def protocol(session_id):
    return dict(version='vestibular-hair-cell-comparison-v1', candidateVersion=CELL_VERSION,
        sessionId=str(session_id), parameters=deepcopy(PARAMETERS), bath=deepcopy(BATH),
        initialVoltageMv=-75., durationMs=1000., samplePeriodMs=.125,
        stepsMs=[.03125, .015625, .0078125],
        cases=[dict(key=key, changes=[dict(timeMs=t, displacementNanometers=x) for t,x in changes])
               for key,changes in HISTORIES.items()],
        referenceSettings=deepcopy(REFERENCE_SETTINGS), referenceAbsoluteTolerance=[1e-10]+[1e-12]*3+[1e-8]*7,
        arithmeticAbsoluteTolerance=1e-11, arithmeticRelativeTolerance=2e-12,
        referenceBounds=dict(voltageMv=1e-6, gates=2e-8, chargeFc=1e-4),
        candidateBounds=dict(voltageMv=.005, gates=2e-5, chargeFc=.05),
        finestBounds=dict(voltageMv=.00005, gates=2e-7, chargeFc=.0005),
        refinementMaximumRatio=.5, refinementRoundoffFloor=dict(voltageMv=1e-9, gates=1e-11, chargeFc=1e-7),
        chargeLedgerAbsoluteFc=1e-7, referenceGateDomainSlack=1e-10,
        expectedArithmeticRows=7260, expectedNativeCaptures=18, expectedReferences=18,
        expectedSamplesPerCapture=8001, expectedReferenceControls=12,
        controlBounds=dict(passiveVoltageMv=1e-8, passiveChargeFc=1e-7,
            zeroDerivative=1e-12, jacobian=2e-7, instantaneousChargePa=1e-10),
        fixedBathReduction=True, metAdaptationIncluded=False,
        cleftDynamicsIncluded=False, afferentFiringIncluded=False, liveAvatarSelected=False,
        physiologyQualified=False, avatarRecoveryAssessed=False)


def validate(spec):
    assert spec == protocol(spec['sessionId']), 'Prospective hair-cell protocol/criteria changed.'
    return spec

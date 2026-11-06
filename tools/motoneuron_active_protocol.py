"""Fixed diagnostic inputs and acceptance bounds; no simulation on import."""


def protocol():
    def segment(until, current=0, excitation=0, inhibition=0):
        return dict(untilMs=until,somaCurrentPa=current,dendriteExcitatoryNs=excitation,dendriteInhibitoryNs=inhibition)
    def case(key,segments,block='None'):
        return dict(key=key,block=block,segments=segments)
    def pulse(current):return [segment(1000),segment(3000,current=current),segment(6000)]
    def dendritic():return [segment(1000),segment(3000,excitation=25),segment(6000)]
    return dict(version='motoneuron-active-comparison-v1',durationMs=6000,sampleMs=.1,initialVoltageMv=-65,
        stepsMs=[.0125,.00625,.003125],thresholdMv=-20,
        sourceRevision='0d11354490fad5fcd940d016a8d69b000693fd81',cell='FivecomptcMG_0',
        radauJacobian='motoneuron-bounded-central-physical-v1',
        cases=[case('quiet',[segment(6000)]),
            case('soma-1000',pulse(1000)),case('soma-3000',pulse(3000)),case('soma-6000',pulse(6000)),
            case('brief-soma',[segment(1000),segment(1002,current=20000),segment(6000)]),
            case('dendritic',dendritic()),
            case('dendritic-inhibited',[segment(1000),segment(1800,excitation=25),
                segment(2200,excitation=25,inhibition=100),segment(3000,excitation=25),segment(6000)]),
            case('without-fast-sodium',pulse(6000),'WithoutTransientSodium'),
            case('without-calcium-l',dendritic(),'WithoutCalciumL'),
            case('without-kca',pulse(3000),'WithoutPotassiumCalcium')],
        criteria=dict(referenceVoltageMv=.005,referenceGate=5e-5,referenceCalciumMm=5e-8,referenceCrossingMs=.001,
            candidateVoltageMv=.05,candidateGate=5e-4,candidateCalciumMm=5e-7,candidateCrossingMs=.01,
            calciumLedgerMm=1e-10,chargeLedgerFc=1e-5,refinementRatio=1.1),
        referenceMethods=[dict(key='dop853',method='DOP853',rtol=1e-8,atolScale=1.,maximumStepMs=.25),
            dict(key='dop853-tight',method='DOP853',rtol=1e-10,atolScale=.01,maximumStepMs=.125),
            dict(key='radau',method='Radau',rtol=1e-10,atolScale=.01,maximumStepMs=.125)],
        physiologicalCalibrationComplete=False,nativeNeuronExecuted=False,liveBrainAccessed=False,
        activeMotorSourceQualified=False,avatarRecoveryAssessed=False)

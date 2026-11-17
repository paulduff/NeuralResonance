"""Prospective receptor/motor numerical protocol; no model execution on import."""


def protocol(crossing='Linear'):
    assert crossing in ('Linear', 'CubicHermite')
    excitation = [1000.125 + 10 * i for i in range(20)]
    inhibition = [1050.375 + 20 * i for i in range(8)]

    def contact(site, compartment, kind, conductance, events):
        return dict(site=site, compartment=compartment, kind=kind,
            maximumConductanceNs=conductance, axonalDelayMs=.125, sourceTimesMs=list(events))

    def distributed():
        return [contact('exc-' + str(i), i, 'AMPA', 50, excitation) for i in range(1, 5)]

    spec = dict(version='receptor-membrane-comparison-v1', durationMs=1500, sampleMs=.1,
        initialVoltageMv=-65, stepsMs=[.0125, .00625, .003125], stateAccumulation='Compensated',
        motorSourceRevision='0d11354490fad5fcd940d016a8d69b000693fd81',
        receptorSourceRevision='937ed5d0d04519d4cd7af474a0f6c53a6e437e06',
        cell='FivecomptcMG_0', pulseConcentrationMm=1, pulseDurationMs=1, thresholdMv=-20,
        cases=[dict(key='quiet', contacts=[contact('quiet', 1, 'AMPA', 50, [])]),
            dict(key='soma-excitation', contacts=[contact('exc-soma', 0, 'AMPA', 400, excitation)]),
            dict(key='local-excitation', contacts=[contact('exc-local', 1, 'AMPA', 200, excitation)]),
            dict(key='distributed-excitation', contacts=distributed()),
            dict(key='distributed-inhibited', contacts=distributed() + [contact('inh-soma', 0, 'GABAA', 200, inhibition)]),
            dict(key='inhibition-only', contacts=[contact('inh-soma', 0, 'GABAA', 200, inhibition)])],
        criteria=dict(referenceVoltageMv=.005, referenceGate=5e-5, referenceCalciumMm=5e-8, referenceCrossingMs=.001,
            candidateVoltageMv=.05, candidateGate=5e-4, candidateCalciumMm=5e-7, candidateCrossingMs=.01,
            referenceReceptor=1e-8, candidateReceptor=1e-6, calciumLedgerMm=1e-10, chargeLedgerFc=1e-5,
            referenceDomainSlack=1e-10, refinementRatio=1.1, refinementFloor=1e-12,
            analyticControlReceptor=1e-8, jacobianRelativeError=1e-5,
            requireExcitedCrossing=True, requireQuietNoCrossings=True),
        referenceMethods=[dict(key='dop853-tight', method='DOP853', rtol=1e-10, atolScale=.01, maximumStepMs=.125),
            dict(key='dop853-tighter', method='DOP853', rtol=1e-11, atolScale=.001, maximumStepMs=.0625),
            dict(key='radau', method='Radau', rtol=1e-10, atolScale=.01, maximumStepMs=.125)],
        candidateConditions=18, referenceConditions=18, expectedAnalyticControls=6,
        presynapticEventsAreFixtures=True, presynapticNeuronsExecuted=False,
        allCandidateAcceptedEndpointsCompared=True, nativePhysicsExecuted=False,
        liveBrainAccessed=False, physiologicalCalibrationComplete=False, avatarRecoveryAssessed=False)
    # Preserve every legacy input field and all numerical acceptance criteria.
    if crossing == 'CubicHermite': spec['crossingInterpolation'] = crossing
    return spec


def crossing_mode(spec):
    mode = spec.get('crossingInterpolation', 'Linear')
    assert mode in ('Linear', 'CubicHermite') and spec == protocol(mode)
    return mode


def cell_version(spec):
    suffix = '+cubic-hermite-crossings-v1' if crossing_mode(spec) == 'CubicHermite' else ''
    return 'isolated-motoneuron-five-compartment-rk4-compensated-v2' + suffix + '+point-receptors-stage-evaluated-v1'

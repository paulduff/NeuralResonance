"""Prospective coupled-cell protocol; retain the version and bounds in each capture."""

def protocol():
    conditions = []
    active = [('quiet', 'None', 0), ('depolarize', 'None', 50), ('hyperpolarize', 'None', -50),
              ('no-nap', 'WithoutPersistentSodium', 0), ('no-hcn', 'WithoutHcn', 0), ('no-sk', 'WithoutSk', 0)]
    for name, block, current in active + [('passive-soma', 'Passive', 25), ('passive-electrode', 'Passive', 25)]:
        passive = block == 'Passive'
        settings = [(h, 1) for h in (.015625, .0078125, .00390625)]
        if not passive:
            settings.append((.00390625, .125))
        for h, scale in settings:
            conditions.append(dict(key=name + '-h' + str(h) + '-tol' + str(scale), protocol=name, block=block,
                currentPa=current, stepMs=h, nonlinearToleranceScale=scale, durationMs=100 if passive else 6000,
                injectionStartMs=20 if passive else 2000, injectionEndMs=60 if passive else 3000,
                electrode=name == 'passive-electrode'))
    return dict(version='golgi-sdirk-reference-comparison-v1', candidateVersion='isolated-golgi-coupled-sdirk2-v1',
        temperatureCelsius=23, initialVoltageMv=-60, sampleMs=.25, thresholdMv=-20, cases=conditions,
        criteria=dict(voltageMv=.05, gate=.0005, sk=.0005, calciumMm=5e-7, crossingMs=.01,
            referenceFraction=.1, nonlinearFraction=.01, refinementRatio=1.1, refinementFloorFraction=.01,
            chargeLedgerFc=.02, calciumLedgerMm=1e-9, kirchhoffPa=1e-5, gateStagePerMs=1e-7,
            calciumStageMmPerMs=1e-10, skMassError=1e-8, nonlinearResidual=1,
            passiveVoltageMv=.0002),
        primaryReference='bdf-tighter', crossMethodReference='radau-strict',
        originalReferenceCriteria=dict(somaMv=.05, crossingMs=.01, matchingCounts=True),
        nativeNeuronExecuted=False, liveBrainAccessed=False, physiologyQualified=False, avatarRecoveryAssessed=False)

"""Declared source-specimen comparison; construction performs no model solve."""
VERSION = 'published-coupled-canal-trajectory-comparison-v1'
TICKS_PER_SECOND = 2**26


def protocol():
    cases = []
    for regime in ('fast', 'middle', 'slow'):
        for condition in ('quiet', 'hc', 'ac', 'pc', 'free-decay'):
            phases = []; end = 0
            for amplitude in (1, 0, -1, 0):
                drive = [0, 0, 0]
                if condition in ('hc', 'ac', 'pc'):
                    drive[('hc', 'ac', 'pc').index(condition)] = amplitude
                if regime == 'fast':
                    schedule = [(2**9, 4)]
                elif regime == 'middle':
                    schedule = [(2**11, 4), (2**21 - 2**11, 2**11)]
                else:
                    schedule = [(2**11, 4), (2**23 - 2**11, 2**11), (32*TICKS_PER_SECOND - 2**23, 2**21)]
                for duration, step in schedule:
                    assert duration % step == 0 and step % 4 == 0
                    end += duration
                    phases.append(dict(endTick=end, baseStepTicks=step, input=drive.copy()))
            initial = [0.] * 12
            if condition == 'free-decay':
                initial = [1e-12, -2e-12, 3e-12, 2e-12, -1e-12, .5e-12,
                           0, 0, 0, 1e-9, -.5e-9, .25e-9]
            absolute = dict(fast=[1e-25, 1e-21], middle=[1e-18, 1e-17], slow=[1e-16, 1e-17])[regime]
            cases.append(dict(key=regime+'-'+condition, regime=regime, condition=condition,
                initial=initial, phases=phases, durationTicks=end,
                candidateBounds=dict(volumeAbsolute=absolute[0], flowAbsolute=absolute[1], relative=1e-5),
                referenceBounds=dict(volumeAbsolute=absolute[0]/100, flowAbsolute=absolute[1]/100, relative=1e-7)))
    return dict(version=VERSION, candidateVersion='coupled-canal-radau-iia2-v1', ticksPerSecond=TICKS_PER_SECOND,
        refinements=[1, 2, 4], expectedCases=15, expectedNativeRuns=45, referencePrecisions=[60, 90],
        matrixPrecisionAbsolute='1e-40', matrixPrecisionRelative='1e-40',
        refinementMaximumRemainingFraction=.3, refinementNormalizedFloor=1e-5,
        energyAbsoluteJoules=1e-32, energyRelative=5e-10,
        cases=cases, inputConvention='source-prime acceleration held from the start of each declared interval',
        allNativeEndpointsCompared=True, prescribedDiagnosticInputs=True, sourceToSkullFrameQualified=False,
        localBundleMapQualified=False, physiologyQualified=False, avatarRecoveryAssessed=False)

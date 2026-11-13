"""Predeclared matched host timing; no probe executes on import."""
COUNTS = [192, 3072, 12288]
STEPS = [.025, .00625]
MODES = ['reference', 'candidate']
VERSIONS = dict(reference='isolated-olive-cuda-double-v1', candidate='isolated-olive-cuda-host-v1')


def protocol():
    return dict(version='olive-cuda-host-timing-v1', counts=COUNTS, steps=STEPS, modes=MODES,
        implementationVersions=VERSIONS, rounds=6, startFrame=256, frames=128,
        trialsPerProcess=['warmup', 'measured'], expectedProcesses=72, expectedTrials=144,
        modeOrder='alternate by (round + case index) modulo two; three first positions per mode per case',
        alternateRequestOrder=True, timing='128 complete synchronous Advance calls; prebuilt requests; disk writes excluded',
        constructorTimingSeparate=True, phaseProfiling=False, tieredCompilation=False,
        exactRecordedBytesRequired=True, minimumFreeHeadroomBytes=1073741824,
        practicalTimeSavingFraction=.05, minimumFasterPairs=5,
        benefitRule='candidate median at least 5 percent lower and faster in at least five of six matched pairs; descriptive, not a statistical confidence claim',
        oldDeviceIdentityRequired=False, currentDeviceAndRuntimeConstantRequired=True,
        wholeBrainThroughputMeasured=False, physiologyQualified=False, avatarRecoveryAssessed=False)


def schedule():
    rows = []
    for case, (count, step) in enumerate((c, s) for c in COUNTS for s in STEPS):
        for round_id in range(6):
            for position in range(2):
                mode = MODES[(position+round_id+case) % 2]
                rows.append(dict(count=count, step=step, round=round_id, mode=mode,
                                 path=f'cells-{count}-h-{step}/round-{round_id}/{mode}'))
    return rows

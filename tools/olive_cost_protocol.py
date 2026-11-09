"""Prospective cost attribution; no model or benchmark executes on import."""
COUNTS=[192,3072,12288]
STEPS=[.025,.00625]
MODES=['control','host','device']
PHASES=['inputValidationAndPacking','uploadCall','launchAndSynchronization','downloadCall','reportValidationSortingAndCommit']
BOUNDS=[1e-6]*3+[1e-9]*8+[1e-6]+[1e-9]*2+[0,1e-6,1e-6]

def protocol():
    return dict(version='olive-cuda-cost-v1',candidateVersion='isolated-olive-cuda-double-v1',
        counts=COUNTS,steps=STEPS,modes=MODES,rounds=5,startFrame=256,frames=128,
        trialsPerProcess=['warmup','measured'],expectedProcesses=90,expectedTrials=180,
        modeOrder='rotate by (round + case index) modulo three',phaseNames=PHASES,
        absoluteBounds=BOUNDS,instrumentationMaterialRatio=[.9,1.1],
        eventTiming='default stream start/end around kernel submission, queried after timed trajectory; includes scheduling and may include idle time',
        hostTiming='five contiguous phases inside Advance; launch/sync are nested subsets, device events are not additive host phases',
        transferTiming='complete upload/download calls, including ownership checks, pinning and download allocation; not isolated bus time',
        biologicalWork='same saved CPU frames 256..383; repeated 192-cell prototypes with unique identities',
        exactAcrossModes=True,minimumFreeHeadroomBytes=1073741824,
        wholeBrainThroughputMeasured=False,physiologyQualified=False,avatarRecoveryAssessed=False)

def schedule():
    result=[]
    for case_index,(count,step) in enumerate((c,s) for c in COUNTS for s in STEPS):
        for round_id in range(5):
            for position in range(3):
                mode=MODES[(position+round_id+case_index)%3]
                result.append(dict(count=count,step=step,round=round_id,mode=mode,
                    path=f'cells-{count}-h-{step}/round-{round_id}/{mode}'))
    return result

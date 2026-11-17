"""Frozen synthetic open-cleft input data; no model equations are evaluated."""
from copy import deepcopy
from cleft_coupling_protocol import protocol as closed_protocol, COUNTS as CLOSED_COUNTS

COUNTS=dict(CLOSED_COUNTS,CleftOpenBoundaryTests=11)


def protocol():
    closed=closed_protocol();cases=[]
    for spatial in closed['spatialCases']:
        for split in (False,True):
            owner=spatial['compartments'][-1]['id']
            faces=[dict(id='apex'+str(i),compartmentId=owner,areaSquareMicrometers=.01 if split else .02,
                        centerToBathDistanceMicrometers=.25) for i in range(2 if split else 1)]
            cases.append(dict(index=len(cases),spatial=deepcopy(spatial),
                              reservoir=dict(id='perilymph',potassiumMm=5.,sodiumMm=140.),faces=faces))
    result=dict(version='cleft-open-comparison-v1',openVersion='shared-hair-open-cleft-perilymph-v1',
        spatialVersion=closed['spatialVersion'],cases=cases,expectedCases=192,expectedClosedCases=624,
        timeIntegrated=False,spatialGeometryCalibrated=False,physiologyQualified=False,avatarRecoveryAssessed=False)
    for key in ('decimalPrecisions','referenceAbsoluteTolerance','referenceRelativeTolerance',
                'candidateAbsoluteTolerance','candidateRelativeTolerance'): result[key]=closed[key]
    return result


def validate(value):
    assert value==protocol(),'Open-cleft inputs, inventory or original numerical bounds changed.'
    return value

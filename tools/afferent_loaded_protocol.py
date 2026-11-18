"""Declared four-ms experiment from a retained, independently checked root.

Construction uses saved state only; no equilibrium gate reset or model solve.
"""
from copy import deepcopy
from uuid import UUID,uuid5
from afferent_precision_gate import protocol as previous
from afferent_rest_analysis import deepcopy_fixture

SESSION='3486c536-3c70-4d30-96d0-dfe458cbe5da'
ROOT_KEY='2-uniform-original'


def protocol(rest_input,native):
    assert native['stationaryCriteriaPassed'] and native['outcome']=='StationaryResidualCriteriaMet'
    point=native['acceptedIterations'][-1]['point']
    assert point['scaledResidual']<=1
    case=next(c for c in rest_input['cases'] if c['key']==ROOT_KEY)
    fixture=deepcopy_fixture(case['fixture'],point)
    p=previous();cases=[];total=1048576
    for key,perturbation,drive in [('quiet',0.,False),('hair-plus',1.,False),('hair-minus',-1.,False),('bundle-return',0.,True)]:
        f=deepcopy(fixture);f['sensory']['hairMv']+=perturbation
        # Perturb voltage alone. Preserve the actual saved gates and ions.
        segments=[dict(endTick=total*(i+1)//4,bundleDisplacementNanometers=20. if drive and i==1 else 0.) for i in range(4)]
        cases.append(dict(key=key,cellId=str(uuid5(UUID(SESSION),key)),fixture=f,segments=segments,totalTicks=total))
    p.update(version='joint-sensory-nerve-loaded-rest-v1',sessionId=SESSION,sampleTicks=256,stepTicks=[256,128,64],
        cases=cases,expectedCases=4,expectedNativeRuns=12,expectedReferences=12,
        referenceMaximumStepMs=1/256,restInitialized=True)
    p.pop('fastTransientOnly')
    # All error, conservation, refinement and nonlinear tolerances are inherited
    # unchanged. Duration and tested steps are new prospective conditions.
    p['restObservation']=dict(durationMs=4.,seedKey=ROOT_KEY,activationPolicy='EndpointHold',
        equilibriumGatesResetDuringIntegration=False,quietDriftBounds=dict(voltage=1e-6,ion=1e-8,gate=1e-8),
        biologicalStabilityQualified=False,returnToRestRequiredForNumericalPass=False)
    return p

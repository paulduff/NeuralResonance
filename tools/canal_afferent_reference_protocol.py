"""Prospective full-prefix reference settings; construction executes no equations."""
from copy import deepcopy
from canal_afferent_protocol import protocol as connected_protocol


def protocol(bundle, loaded):
    connected = connected_protocol(bundle, loaded)
    assert bundle['frame']['clock']['initialPhysicsStep'] == 0
    assert connected['sourceStepsPerInterval'] == [16384, 32768, 65536]
    keys = ('referenceDecimalPrecision', 'referenceCheckPrecisions', 'referenceMaximumStepMs',
            'referenceSettings', 'referenceAtol', 'referenceComparison', 'candidateComparison',
            'refinement', 'ledgerResidualBounds')
    result = {key: deepcopy(loaded[key]) for key in keys}
    result.update(version='continuous-canal-joint-afferent-reference-v1', connected=connected,
        conditions=deepcopy(connected['conditions']), physicalIntervals=connected['physicalIntervals'],
        sourceStepsPerInterval=max(connected['sourceStepsPerInterval']), expectedReferences=6,
        expectedUniqueSamplesPerReference=655361, expectedStoredSamplesPerReference=655370,
        initialPolicy=connected['initialStatePolicy'], activationPolicy=connected['activationPolicy'],
        forcingPolicy='independent continuous published linear mechanics and declared spatial projection; no native stages or held values',
        precisionWitnessPolicy='each physical interval: start, first accepted node, midpoint, largest absolute observed-node voltage among accepted nodes, end; coincident times retained once',
        interpolationPolicy='retain all accepted reference nodes; independently evaluate dense output at every finest source-clock tick',
        failurePolicy='retain accepted nodes and populated dense samples of the failed interval; never restart, zero-fill or repair the prefix',
        matchedHeldInputAccuracyQualified=False, connectedSensoryAccuracyQualified=False,
        physiologicalCalibrationQualified=False, avatarBalanceAssessed=False)
    assert len(result['referenceSettings']) * len(result['conditions']) == result['expectedReferences']
    assert result['sourceStepsPerInterval'] * result['physicalIntervals'] + 1 == result['expectedUniqueSamplesPerReference']
    assert (result['sourceStepsPerInterval'] + 1) * result['physicalIntervals'] == result['expectedStoredSamplesPerReference']
    return result

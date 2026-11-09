"""One predeclared refinement beyond the verified, still-failed third-order run."""
from copy import deepcopy
from golgi_sdirk_protocol import protocol as original_protocol

STEP_MS = .0009765625


def protocol():
    original = original_protocol(3)
    result = deepcopy(original)
    result['version'] = 'golgi-sdirk3-finer-comparison-v1'
    result['cases'] = []
    for c in original['cases']:
        if c['stepMs'] != .001953125:
            continue
        row = dict(c, stepMs=STEP_MS)
        row['key'] = c['protocol']+'-h'+str(STEP_MS)+'-tol'+str(c['nonlinearToleranceScale'])
        result['cases'].append(row)
    assert len(result['cases']) == 14 and len({c['key'] for c in result['cases']}) == 14
    assert result['criteria'] == original['criteria']
    return result

"""Explicit forcing-only protocol change; no neuronal or reference equation evaluation."""
from copy import deepcopy
import numpy as np

ORIGINAL_VERSION = 'recorded-canal-connected-afferent-v1'
MIDPOINT_VERSION = 'recorded-canal-midpoint-afferent-v2'


def protocol(reference):
    if reference['version'] != 'continuous-canal-joint-afferent-reference-v1':
        raise ValueError('The preserved continuous reference protocol is required.')
    original = reference['connected']
    if original['version'] != ORIGINAL_VERSION or 'forcingPolicy' in original:
        raise ValueError('Original start-held capture protocol required.')
    result = deepcopy(original)
    result.update(version=MIDPOINT_VERSION, forcingPolicy='MidpointHeld')
    return result


def policy(spec):
    if spec['version'] == ORIGINAL_VERSION and 'forcingPolicy' not in spec:
        return 'StartHeld'
    if spec['version'] == MIDPOINT_VERSION and spec.get('forcingPolicy') == 'MidpointHeld':
        return 'MidpointHeld'
    raise ValueError('Unknown or inconsistent canal forcing protocol.')


def forcing_ticks(start_ticks, stride, forcing):
    ticks = np.asarray(start_ticks)
    if (type(stride) is not int or stride < 1 or forcing not in ('StartHeld', 'MidpointHeld')
            or ticks.ndim != 1 or ticks.dtype != np.dtype('int64') or not len(ticks)
            or (ticks < 0).any() or (ticks[1:] <= ticks[:-1]).any()):
        raise ValueError('Ordered exact integer source starts and a declared policy are required.')
    if forcing == 'MidpointHeld' and (stride < 2 or stride % 2):
        raise ValueError('Midpoint must be exactly addressable; no rounding.')
    if stride > np.iinfo(np.int64).max or (ticks > np.iinfo(np.int64).max - stride).any():
        raise ValueError('Source end tick is unrepresentable.')
    return ticks.copy() if forcing == 'StartHeld' else ticks + stride // 2

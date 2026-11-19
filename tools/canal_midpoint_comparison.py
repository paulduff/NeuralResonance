"""New native midpoint capture against preserved independent continuous solutions."""
from pathlib import Path
from afferent_precision_gate import read, save
from canal_midpoint_protocol import protocol
from canal_afferent_analysis import analyse as account_native
from canal_afferent_reference_analysis import compare_trajectories, require


def inputs(package):
    p = read(package / 'continuous-reference-input.json')
    candidate = protocol(p)
    require(read(package / 'connected-input.json') == candidate, 'Only the declared forcing policy may change.')
    require(read(package / 'study/input.json') == candidate, 'Captured midpoint protocol differs.')
    return p


def accounting(package):
    p = inputs(package)
    result = account_native(package)
    save(package / 'accounting.json', result)
    require(result['completed'] and result['connectedAccountingPassed']
        and result['sourceSteps'] == p['connected']['expectedSourceSteps'] == 2293760,
        'Midpoint input, original states or observed packet accounting failed.')
    return result


def compare(package):
    package = Path(package)
    p = inputs(package)
    accounted = read(package / 'accounting.json')
    expected = [(c, n) for c in p['conditions'] for n in p['connected']['sourceStepsPerInterval']]
    require(accounted['completed'] and accounted['connectedAccountingPassed']
        and accounted['sourceSteps'] == 2293760, 'Candidate accounting must pass before comparison.')
    require([(r['condition'], r['sourceStepsPerInterval']) for r in accounted['runs']] == expected
        and all(r['accountingPassed'] for r in accounted['runs']), 'Candidate accounting omitted a run.')
    captured = read(package / 'study/results.json')
    require(captured['completed'] and len(captured['runs']) == len(expected), 'Native capture incomplete.')
    for (condition, count), report in zip(expected, captured['runs'], strict=True):
        require(read(package / 'study' / f'{condition}-n{count}' / 'final.json') == report,
            'Final native state differs from capture summary.')
    require(read(package / 'continuous-reference/results.json') == dict(completed=True,
        attempted=p['expectedReferences'], failedRuns=[], comparisonsExecuted=False,
        coupledAccuracyQualified=False, physiologicalCalibrationQualified=False, avatarBalanceAssessed=False),
        'Preserved independent reference executions are incomplete.')
    source = read(package / 'source-decisions.json')
    result = compare_trajectories(package, p, source, native_directory=package / 'study')
    save(package / 'analysis.json', dict(result, candidateForcingPolicy='MidpointHeld',
        originalFailurePreserved=True, independentReferencesReused=6, referenceOdesRerun=False,
        connectedAccountingPassed=True))
    return result

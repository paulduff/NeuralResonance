"""Validate completed vestibular transfer captures and summarize observed delivery."""
import argparse
from collections import defaultdict
from hashlib import sha256
import json
import math
from pathlib import Path


def require(condition, message):
    if not condition:
        raise ValueError(message)


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('study', type=Path)
    args = parser.parse_args()
    study = args.study.resolve()
    protocol, report = read(study / 'protocol.json'), read(study / 'results.json')
    require(protocol['study'] == 'vestibular-transfer' and report['completed'], 'A completed transfer study is required')
    expected = {f'{weights}-all-{plan}' for weights in protocol['weightVariants'] for plan in protocol['cases']}
    require(len(report['results']) == report['caseCount'] == len(expected) and
            {case['case'] for case in report['results']} == expected, 'Incomplete case matrix')
    for filename, digest in protocol['protectedInputs'].items():
        require(sha256(Path(filename).read_bytes()).hexdigest().upper() == digest.upper(), 'Protected input changed: ' + filename)
    cases = []
    duration = protocol['caseMilliseconds']
    for case in report['results']:
        folder = study / case['case']
        transfer = read(folder / 'transfer-summary.json')
        initial = read(folder / 'initial-state.json')
        require(len(initial) == 14 and all(item['expectedInbound'] == item['loadedInbound'] and
                item['expectedOutbound'] == item['loadedOutbound'] for item in initial), 'Saved-state preflight failed')
        require(case['sourceBudgetDrops'] == case['queueRejected'] == 0, 'Transfer was not uncapped and fully admitted')
        rows = [json.loads(line) for line in (folder / 'neural-trace.jsonl').read_text().splitlines()]
        require([row['tick'] for row in rows] == list(range(10, duration + 1, 10)), 'Incomplete neural trace')
        for item in transfer['accounting']:
            require(item['integrated'] + item['pending'] == item['admitted'], 'Delivery accounting does not close')
            observed = sum(value['count'] for key, value in transfer['paths'].items() if key.split('|')[1] == item['instance'])
            require(observed == item['integrated'], 'Path counts differ from observed integration')
            samples = [value for key, value in transfer['electrical'].items() if key.split('|')[1] == item['instance']]
            require(sum(value['millisecondSamples'] for value in samples) == duration, 'Incomplete electrical sampling')
            region = next(region for region in rows[-1]['regions'] if region['instance'] == item['instance'])
            require(sum(value['spikes'] for value in samples) == region['spikeOutCount'], 'Electrical and emitted-spike counts differ')
            # Step acknowledgments precede that tick's route enqueue. Those last
            # admissions remain pending at the end of the case.
            require(item['integrated'] <= region['spikeInCount'] <= item['admitted'], 'Neural snapshot ingress falls outside integration/admission bounds')
        require(transfer['completeIntegrationCounts'] == sum(item['integrated'] for item in transfer['accounting']), 'Integration total differs')
        for stats in list(transfer['paths'].values()) + list(transfer['electrical'].values()):
            require(all(math.isfinite(value) for value in stats.values()), 'Non-finite model diagnostics')
        if case['plan'] == 'muted-tilt':
            require(case['primaryQueued'] == 0, 'Muted control received primary input')
        if case['plan'] == 'left-only-tilt':
            require(case['rightPrimaryQueued'] == 0, 'Left-only control received right primary input')
        if case['plan'] in ['quiet-hold', 'head-motion-return']:
            require(case['primaryFallSensitiveMessages'] == 0, 'Negative fixture contains primary fall-sensitive events')
        allowed = set()
        if case['weights'] in ['saved-afferent-baseline', 'saved-both-baseline']:
            allowed |= {'L_VestibularAfferents', 'R_VestibularAfferents'}
        if case['weights'] in ['saved-nucleus-baseline', 'saved-both-baseline']:
            allowed |= {'L_VestibularNuclei', 'R_VestibularNuclei'}
        actual = {item['instanceKey'] for item in initial if item['intervention']['selectedEntries'] > 0}
        require(actual == allowed, 'The baseline intervention affected the wrong nuclei')
        stages = defaultdict(list)
        for row in rows:
            require(all(math.isfinite(row[key]) for key in ['populationError', 'afferent', 'descending', 'bilateralDrive']), 'Invalid neural decode')
            stages[row['phase']].append(row)
        cases.append({
            'case': case['case'], 'weights': case['weights'], 'plan': case['plan'],
            'primaryQueued': case['primaryQueued'], 'primaryFallSensitiveMessages': case['primaryFallSensitiveMessages'],
            'accounting': transfer['accounting'], 'transmission': transfer['paths'], 'electrical': transfer['electrical'],
            'interventions': {item['instanceKey']: item['intervention'] for item in initial if item['instanceKey'] in allowed},
            'phases': {phase: {'samples': len(values), 'peakPopulationError': max(row['populationError'] for row in values),
                              'errorAtLeastEntryThresholdSamples': sum(row['populationError'] >= .4 for row in values),
                              'latchSamples': sum(row['latch'] for row in values)} for phase, values in stages.items()},
        })
    result = {
        'completed': True, 'caseCount': len(cases), 'smoke': protocol['smoke'],
        'validation': {'protectedInputsUnchanged': True, 'initialCountsVerified': True, 'integrationAccountingCloses': True,
                       'oneMillisecondElectricalSamplingComplete': True, 'electricalAndEmittedSpikesAgree': True,
                       'controlsHeld': True, 'interventionsLimitedToDeclaredCopies': True},
        'scope': 'Observed model currents, somatic voltages and signal transfer in an isolated circuit; no physical recovery assessment.',
        'portableBitwiseReproducibility': False,
        'timingCaution': 'Message IDs align equivalent events within the process; production HashCode salts differ across processes. Cross-host quality comparisons require replication.',
        'analysisSourceSha256': sha256(Path(__file__).read_bytes()).hexdigest(),
        'liveBrainAccessed': False, 'physicalRecoveryAssessed': False, 'results': cases,
    }
    (study / 'transfer-analysis.json').write_text(json.dumps(result, indent=2, allow_nan=False) + '\n', encoding='utf-8')
    print(json.dumps({key: value for key, value in result.items() if key != 'results'}, indent=2))


if __name__ == '__main__':
    main()

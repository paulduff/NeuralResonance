"""Check and summarize a completed isolated postural replay; no live brain access."""
import argparse
from collections import defaultdict
from hashlib import sha256
import json
import math
from pathlib import Path
from statistics import fmean


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def require(condition, message):
    if not condition:
        raise ValueError(message)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('run', type=Path)
    args = parser.parse_args()
    run = args.run.resolve()
    study = run / 'study'
    protocol, results = read(study / 'protocol.json'), read(study / 'results.json')
    require(results['completed'] and not protocol['smoke'], 'A completed full study is required')
    require(results['protectedInputsUnchanged'] and not results['liveBrainAccessed'], 'Study isolation failed')
    for filename, expected in protocol['protectedInputs'].items():
        require(sha256(Path(filename).read_bytes()).hexdigest().upper() == expected.upper(),
                'Protected input changed: ' + filename)

    source = Path(protocol['inputs']['sourceRun']) / 'standing-trace.jsonl'
    bodies = {json.loads(line)['body']['sequence']: json.loads(line)['body']
              for line in source.read_text(encoding='utf-8-sig').splitlines()}
    inputs = {}
    for phase in ['stable', 'fallen']:
        body = bodies[protocol['inputs'][phase + 'Sequence']]
        muscle = body['articulation']['musculoskeletal']
        inputs[phase] = {
            'sequence': body['sequence'], 'physicalBalanceError': muscle['balanceError'],
            'linearVelocityMetersPerSecond': [body['linearVelocity' + axis + 'MetersPerSecond'] for axis in 'XYZ'],
            'angularVelocityRadiansPerSecond': [body['angularVelocity' + axis + 'RadiansPerSecond'] for axis in 'XYZ'],
            'neckPitchRadians': body['articulation']['neckPitchRadians'], 'balance': muscle['balance'],
        }

    expected_cases = {f'{weights}-{limit}-{plan}'
                      for weights in ['saved', 'fresh'] for limit in ['24', 'all'] for plan in protocol['cases']}
    summaries = {item['case']: item for item in results['results']}
    require(set(summaries) == expected_cases and results['caseCount'] == len(expected_cases) == 20,
            'Full 20-case matrix is incomplete')
    instances = {side + '_' + region['id'] for region in protocol['regions'] for side in ['L', 'R']}
    duration = protocol['caseMilliseconds']
    all_cases, vermis_traces = [], []
    maximum_recomposition_error = 0.0
    for name in sorted(summaries):
        summary = summaries[name]
        rows = [json.loads(line) for line in (study / name / 'neural-trace.jsonl').read_text().splitlines()]
        require([row['tick'] for row in rows] == list(range(10, duration + 1, 10)), 'Incomplete trace: ' + name)
        initial = read(study / name / 'initial-state.json')
        require({item['instanceKey'] for item in initial} == instances, 'Incomplete saved-state preflight')
        require(all(item['expectedInbound'] == item['loadedInbound'] and
                    item['expectedOutbound'] == item['loadedOutbound'] for item in initial), 'Saved-state load mismatch')
        if summary['plan'] == 'fallen-silent':
            require(summary['primaryQueued'] == 0, 'Muted control received primary input')
        if summary['plan'] == 'fallen-left-only':
            require(summary['rightPrimaryQueued'] == 0, 'Left-only control received right primary input')

        stages = defaultdict(list)
        previous_counts = {}
        for row in rows:
            require(len(row['regions']) == len(instances) and
                    {region['instance'] for region in row['regions']} == instances, 'Missing neural population')
            for region in row['regions']:
                require(all(math.isfinite(region[key]) and region[key] >= 0 for key in
                            ['meanFiringRateHz', 'spikeInCount', 'spikeOutCount', 'feedbackQueueDepth']), 'Invalid neural trace')
                current = region['spikeInCount'], region['spikeOutCount']
                before = previous_counts.get(region['instance'], (0, 0))
                require(all(a >= b for a, b in zip(current, before)), 'Cumulative spike count went backwards')
                previous_counts[region['instance']] = current
            means = {region: fmean(item['meanFiringRateHz'] for item in row['regions']
                                   if item['instance'][2:] == region)
                     for region in ['VestibularNuclei', 'CerebellarVermis', 'SpinalCordMotor']}
            residual = means['VestibularNuclei'] - .55 * means['CerebellarVermis'] - .25 * means['SpinalCordMotor']
            difference = abs(row['populationError'] - max(0, residual))
            maximum_recomposition_error = max(maximum_recomposition_error, difference)
            require(difference < .0001, 'Recorded error differs from the production rate formula')
            require(all(math.isfinite(row[key]) for key in
                        ['populationError', 'afferent', 'descending', 'bilateralDrive']), 'Invalid decoder trace')
            transition = summary['plan'] == 'stable-fallen-stable'
            stage = ('initial-stable' if row['tick'] <= duration / 3 else
                     'fallen' if row['tick'] <= duration * 2 / 3 else 'returned-stable') if transition else row['phase']
            expected_phase = 'stable' if 'stable' in stage else 'fallen'
            require(row['phase'] == 'captured-' + expected_phase and
                    row['physicalBalanceError'] == inputs[expected_phase]['physicalBalanceError'], 'Input phase mismatch')
            stages[stage].append(row)

        def statistics(samples):
            return {
                'samples': len(samples), 'firstTick': samples[0]['tick'], 'lastTick': samples[-1]['tick'],
                'meanPopulationError': fmean(row['populationError'] for row in samples),
                'peakPopulationError': max(row['populationError'] for row in samples),
                'errorAtLeastEntryThresholdSamples': sum(row['populationError'] >= .4 for row in samples),
                'latchSamples': sum(row['latch'] for row in samples),
                'drivePresentButErrorGateClosedSamples': sum(row['bilateralDrive'] >= .04 and row['populationError'] < .4 for row in samples),
                'meanRegionRatesHz': {region: fmean(item['meanFiringRateHz'] for row in samples
                                                  for item in row['regions'] if item['instance'][2:] == region)
                                      for region in ['VestibularNuclei', 'CerebellarVermis', 'SpinalCordMotor', 'ReticularFormation']},
            }

        vermis_traces.append([[item['meanFiringRateHz'], item['spikeOutCount']]
                              for row in rows for item in row['regions'] if item['instance'][2:] == 'CerebellarVermis'])
        all_cases.append({
            'case': name, 'weights': summary['weights'], 'sourceLimit': summary['sourceLimit'],
            'sourceBudgetDrops': summary['sourceBudgetDrops'], 'queueRejected': summary['queueRejected'],
            'primaryQueued': summary['primaryQueued'], 'primaryFallSensitiveMessages': summary['primaryFallSensitiveMessages'],
            'taggedRelays': summary['taggedRelays'], 'allSamples': statistics(rows),
            'afterFirstSecond': statistics([row for row in rows if row['tick'] > 1000]),
            'stages': {stage: statistics(samples) for stage, samples in stages.items()},
            'finalRegionCounters': rows[-1]['regions'],
        })

    saved = [case for case in all_cases if case['weights'] == 'saved']
    result = {
        'run': run.name, 'cases': len(all_cases), 'samples': sum(case['allSamples']['samples'] for case in all_cases),
        'analysisSourceSha256': sha256(Path(__file__).read_bytes()).hexdigest(),
        'validation': {'completeMatrixAndTraces': True, 'protectedInputsUnchanged': True,
                       'exactInitialSynapseCounts': True, 'controlsHeld': True,
                       'maximumRateFormulaRecompositionDifference': maximum_recomposition_error},
        'savedCasesZeroPopulationErrorEverySample': all(case['allSamples']['peakPopulationError'] == 0 for case in saved),
        'savedVestibularNucleiTotalOutputSpikes': sum(item['spikeOutCount'] for case in saved
                                                   for item in case['finalRegionCounters'] if item['instance'][2:] == 'VestibularNuclei'),
        'identicalVermisRateAndOutputCountTracesAcrossAllCases': all(trace == vermis_traces[0] for trace in vermis_traces),
        'totalQueueRejections': sum(case['queueRejected'] for case in all_cases),
        'heldInputs': inputs, 'results': all_cases,
        'scope': 'Isolated circuit with reinitialized membranes and regularized held inputs; not whole-brain failure localization or physical recovery qualification.',
        'limitations': [
            'Captured stable input includes motion and is not a stationary negative control.',
            'Phase changes hold velocities and positions; no full physical trajectory is replayed.',
            'Intrinsic vermis output alone does not establish sensory compensation or numerical instability.',
            'Fresh unrestricted population-error excursions also occur without primary afferents; muted and one-sided cases have no latch.',
        ],
        'liveBrainAccessed': False, 'physicalRecoveryAssessed': False,
    }
    (run / 'postural-analysis.json').write_text(json.dumps(result, indent=2, allow_nan=False) + '\n', encoding='utf-8')
    print(json.dumps({key: value for key, value in result.items() if key not in ['results', 'heldInputs', 'limitations']}, indent=2))


if __name__ == '__main__':
    main()

"""Analyse completed Unity recovery captures without changing measurements."""
import argparse
from collections import Counter
import json
from pathlib import Path
import re


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('run', type=Path)
    args = parser.parse_args()
    run = args.run.resolve()
    read = lambda name: json.loads((run / name).read_text(encoding='utf-8-sig'))
    report = read('standing-report.json')
    traces = [json.loads(line) for line in (run / 'standing-trace.jsonl').read_text(encoding='utf-8-sig').splitlines()]
    measured = lambda trace: trace['body']['articulation']['musculoskeletal']
    fallen = [t for t in traces if measured(t)['balance']['phase'] == 'fallen']
    before_fall = [t for t in traces if 0 <= t['simulationSeconds'] < report['metrics']['firstFallSeconds']]
    journal_path = run / 'motor-telemetry.jsonl'
    journal = [json.loads(line) for line in journal_path.read_text(encoding='utf-8-sig').splitlines()] if journal_path.exists() else []
    active = [json.loads(row['responseJson'])['runtime'] for row in journal if row['running']]
    value = lambda item, key: float(re.search(key + r'=([0-9.]+)', item['evidence']).group(1))
    error = lambda item: float(re.search(r'balance-error=population:([0-9.]+)', item['evidence']).group(1))
    export = read('coordinator-after.json')
    dispatch = export['dispatchSpikeTrace']
    first_tick = min((r['tick'] for r in active), default=-1)
    last_tick = max((r['tick'] for r in active), default=-1)
    overlap = [row for row in dispatch if first_tick <= row['tick'] <= last_tick]
    states = export['latestSnapshot']['structureStates']
    result = {
        'run': run.name, 'completed': report['completed'], 'finite': report['finite'],
        'integrationChecksPassed': report['integrationChecksPassed'],
        'recoveryObserved': report['recoveryObserved'], 'metrics': report['metrics'],
        'capturedPhysicalSamples': len(traces),
        'phaseCounts': dict(Counter(measured(t)['balance']['phase'] for t in traces)),
        'fallenSamples': len(fallen),
        'fallenBalanceErrorCounts': dict(Counter(measured(t)['balanceError'] for t in fallen)),
        'maximumCapturedMuscleFatigueBeforeFirstFall': max((m['fatigueFraction'] for t in before_fall for m in measured(t)['muscles']), default=0),
        'motorJournal': {
            'responses': len(journal), 'responsesWhileRunning': len(active),
            'tickRangeWhileRunning': [first_tick, last_tick],
            'sampledRightingLatchActive': sum(r['rightingLatchActive'] for r in active),
            'loggedRoundedBalanceErrorCounts': dict(Counter(error(r) for r in active)),
            'loggedRoundedMaximumAfferentDrive': max((value(r, 'afferent') for r in active), default=0),
            'loggedRoundedMaximumDescendingDrive': max((value(r, 'descending') for r in active), default=0),
            'loggedDescendingDriveAtLeast004Samples': sum(value(r, 'descending') >= .04 for r in active),
            'maximumStandDrive': max((r['standDrive'] for r in active), default=0)
        },
        'postTrialTransportSnapshot': export['transportStats'],
        'postTrialRoutingTrace': {
            'samples': len(dispatch),
            'tickRange': [min((d['tick'] for d in dispatch), default=-1), max((d['tick'] for d in dispatch), default=-1)],
            'samplesOverlappingActiveMotorTickRange': len(overlap),
            'primaryAfferentSourceNeuronCounts': {str(structure): dict(Counter(d['sourceNeuronId'] for d in dispatch if d['sourceStructure'] == structure)) for structure in [9, 10]},
            'scope': 'Bounded post-trial routing window; do not attribute these routes to the active trial unless tick ranges overlap.'
        },
        'postTrialPosturalPopulationRates': [{k: s[k] for k in ['structureId', 'meanFiringRateHz', 'vestibuloReticularDiagnostics']} for s in states if s['structureId'] in [12, 45, 49, 52]],
        'interpretation': [
            'Physical balance error is an input/learning measurement. Motor balance-error telemetry is a separate estimate from mean vestibular, vermis and spinal activity; these are not the same quantity.',
            'Afferent righting activity is observed, but spinal descending activity and the emergency latch must be assessed separately.',
            'Budget-drop and transport counters are sampled single-tick statistics, not whole-trial totals.',
            'The FIFO prefix transport policy can systematically favor low-index neurons under saturation. This is a code finding, not proof that every missing relay was discarded.',
            'All original physical and motor captures are unchanged. API acceptance does not prove complete neuronal delivery.',
            'Missing Unity axial muscles and posture transitions still limit physical recovery qualification.'
        ],
        'sensoryDeliveryVerified': False, 'learnedRecoveryAssessed': False,
        'originalCapturesModified': False
    }
    (run / 'trace-analysis.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
    print(json.dumps({k: result[k] for k in ['run', 'fallenSamples', 'fallenBalanceErrorCounts', 'motorJournal']}, indent=2))


if __name__ == '__main__':
    main()

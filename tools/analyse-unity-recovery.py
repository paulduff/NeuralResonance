"""Analyse completed Unity recovery captures without changing measurements."""
import argparse
from collections import Counter
import json
from pathlib import Path
import re
from statistics import median


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
    active_rows = [row for row in journal if row['running']]
    active = [json.loads(row['responseJson'])['runtime'] for row in active_rows]
    value = lambda item, key: float(re.search(key + r'=([0-9.]+)', item['evidence']).group(1))
    error = lambda item: float(re.search(r'balance-error=population:([0-9.]+)', item['evidence']).group(1))
    export = read('coordinator-after.json')
    dispatch = export['dispatchSpikeTrace']
    first_tick = min((r['tick'] for r in active), default=-1)
    last_tick = max((r['tick'] for r in active), default=-1)
    overlap = [row for row in dispatch if first_tick <= row['tick'] <= last_tick]
    states = export['latestSnapshot']['structureStates']
    gate_samples = []
    for row, runtime in zip(active_rows, active):
        bilateral = re.search(r'righting=bilateral:([0-9.]+)', runtime['evidence'])
        if bilateral:
            gate_samples.append({
                'physicsSeconds': row['physicsStep'] * .01,
                'drive': float(bilateral.group(1)), 'error': error(runtime)
            })
    transport_path = run / 'transport-telemetry.jsonl'
    transport_rows = [json.loads(line) for line in transport_path.read_text(encoding='utf-8-sig').splitlines() if line.strip()] if transport_path.exists() else []
    active_transport = [row for row in transport_rows if row['Phase'] == 'trial']
    transport_fields = [
        'effectiveMaxSpikeDispatchPerServicePerTick', 'effectiveMaxSpikeDispatchTotalPerTick',
        'activeServices', 'tickWallMs', 'ackLatencyEwmaMs', 'deliveredSpikes',
        'droppedByBudget', 'dispatchQueueDispatchErrors', 'dispatchQueueDroppedSpikes'
    ]
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
        'rightingEntryGate': {
            'sampledBilateralResponses': len(gate_samples),
            'requiredBalanceError': .4, 'requiredDrive': .04,
            'conservativelyAboveDriveThreshold': sum(r['drive'] >= .041 for r in gate_samples),
            'aboveBalanceErrorThreshold': sum(r['error'] >= .4 for r in gate_samples),
            'postFallDrivePresentButErrorGateClosed': sum(
                r['physicsSeconds'] >= report['metrics']['firstFallSeconds'] >= 0 and
                r['drive'] >= .041 and r['error'] < .001 for r in gate_samples),
            'scope': 'Rounded saved evidence for the existing conjunctive entry gate; not a full decoder or physical counterfactual replay.',
            'conservativeDriveCutoff': .041,
            'caution': 'Removing the error gate is not a qualified remedy: stable tonic postural activity must still release or avoid the latch.'
        },
        'activeTransportSamples': {
            'samples': len(active_transport),
            'profiles': sorted({row['PerformanceProfileName'] for row in active_transport}),
            'scope': 'Sparse single-tick samples, not whole-trial totals.',
            'ranges': {field: {
                'min': min(row['TransportStats'][field] for row in active_transport),
                'median': median(row['TransportStats'][field] for row in active_transport),
                'max': max(row['TransportStats'][field] for row in active_transport)
            } for field in transport_fields} if active_transport else {}
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
    print(json.dumps({k: result[k] for k in ['run', 'fallenSamples', 'fallenBalanceErrorCounts', 'motorJournal', 'rightingEntryGate']}, indent=2))


if __name__ == '__main__':
    main()

"""Review the operator-reported rubral capture; replay no neuron or test."""
import argparse
from collections import Counter
import csv
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec); spec.loader.exec_module(result); return result


def main(declared=False, convergent=False):
    declared = declared or convergent
    capture = ROOT / ('artifacts/rubral-ten-20261117-0419e8' if convergent else 'artifacts/rubral-declared-20261117-3e3db0' if declared else 'artifacts/rubral-motor-20261117-844975')
    output = ROOT / ('artifacts/rubral-ten-review-20261117-v2' if convergent else 'artifacts/rubral-declared-review-20261117' if declared else 'artifacts/rubral-motor-review-20261117')
    assert not output.exists()
    revision = '9621023d3cd2569fe5cf355b5091f4bfbb5baa30' if convergent else '149e5ea778878dded50bb8d867292c9f5a367a6d' if declared else '621109a7604c95bd4278a5a9c2efe779a41be2fb'
    expected_tests = 155 if convergent else 150 if declared else 136
    common = module('rubral_review_integrity', ROOT / 'tools/review-olive-and-motor-20261109.py')
    print('Verifying reported rubral C:/D: files and source...', flush=True)
    review = common.verify(capture, ['test-binaries.json', 'probe-binaries.json'], revision)
    archive = module('rubral_review_archive', ROOT / 'tools/review-spindle-root-and-causal-motor-20261113.py')
    review['verifiedGitFiles'] = archive.source_archive(common, capture, review['sourceRevision'])
    audit = module('rubral_completed_audit', capture / 'source/tools/rubral_motor_gate.py')
    original_records = audit.records; summaries = {}
    def observed_records(path):
        if path.name != 'source-frames.jsonl.gz' or path.parent.parent != capture / 'study':
            yield from original_records(path); return
        counts = Counter(); targets = Counter(); cells = {}; first_event = {}; last_event = {}
        for frame in original_records(path):
            payload = json.loads(frame['CanonicalJson'])
            for event in payload['OriginalEvents']:
                name = event['SourceNeuronId']; counts[name] += 1; targets[event['TargetNeuronId']] += 1
                first_event.setdefault(name, event['TimestampMs']); last_event[name] = event['TimestampMs']
            for cell in payload['Observation']['Cells']:
                name = cell['Neuron']; voltage = cell['VoltageMv']; current = cell['NetCurrent']
                row = cells.setdefault(name, dict(samples=0, activeSamples=0, minimumVoltageMv=voltage,
                    maximumVoltageMv=voltage, minimumNetCurrent=current, maximumNetCurrent=current))
                row['samples'] += 1; row['activeSamples'] += int(cell['ActiveAtObservation'])
                row['minimumVoltageMv'] = min(row['minimumVoltageMv'], voltage)
                row['maximumVoltageMv'] = max(row['maximumVoltageMv'], voltage)
                row['minimumNetCurrent'] = min(row['minimumNetCurrent'], current)
                row['maximumNetCurrent'] = max(row['maximumNetCurrent'], current)
            yield frame
        topology = common.read(path.parent / 'topology.json')
        selected = [r['SourceNeuron'] for r in topology['selected']]
        assert len(selected) == (10 if convergent else 1)
        for name, row in cells.items():
            row.update(emittedEvents=counts[name], firstEventMs=first_event.get(name), lastEventMs=last_event.get(name))
        summaries[path.parent.name] = dict(selectedTarget=topology['identity']['Neuron'], selectedSources=selected,
            totalOriginalEvents=sum(counts.values()), emittingSourceCells=len(counts), reachedTargets=len(targets),
            eventsByTarget=dict(sorted(targets.items())), cells=dict(sorted(cells.items())),
            selectedTelemetry={name: cells[name] for name in selected})
    audit.records = observed_records
    analysis = audit.audit(capture, declared=True, convergent=True) if convergent else audit.audit(capture, declared=True) if declared else audit.audit(capture)
    assert analysis == common.read(capture / 'analysis.json') and analysis['tests']['passedCases'] == expected_tests
    assert all(s['totalOriginalEvents'] == row['sourceEvents'] for row in analysis['cases'] for key, s in summaries.items() if key == row['key'])
    assert len(summaries) == 4 and all(len(s['cells']) == 256 for s in summaries.values())
    for summary in summaries.values(): assert all(c['samples'] == 12000 for c in summary['cells'].values())
    assert common.digest(capture / 'managed-runtime-before.json') == common.digest(capture / 'managed-runtime-after.json') == \
        common.digest(capture / 'reference/qualified-managed-runtime.json')
    config = common.read(capture / 'configuration.json'); status = common.read(capture / 'window-status.json')
    assert config['NoLiveMonitoring'] and config['IsolatedRubralMotor'] and status['ManagedRuntimePreserved']
    assert status['ComponentCausalityChecksPassed'] and status['SuccessfulConditions'] == 4
    for key in ('LiveBrainAccessed', 'OldSavesLoaded', 'UpstreamNumericalAccuracyQualified', 'LearnedContactEfficacyTransferred',
                'NativePhysicsExecuted', 'MuscleDynamicsExecuted', 'PhysiologyQualified', 'AvatarRecoveryAssessed', 'FullUnitSuitePassed'):
        assert status[key] is False
    motor_summaries = {}
    if declared:
        assert status['ActualFixtureDeliveryVerified'] and analysis['deliveredFixtureEvents'] == 15360
        for key in summaries:
            folder = capture / 'study' / key
            with common.io(folder / 'packets1/trajectory.csv').open(newline='', encoding='utf-8-sig') as active, \
                 common.io(folder / 'contacts-disabled/trajectory.csv').open(newline='', encoding='utf-8-sig') as disabled:
                on = csv.DictReader(active); off = csv.DictReader(disabled)
                assert on.fieldnames == off.fieldnames
                receptors = [name for name in on.fieldnames if name.startswith('receptor_')]
                assert len(receptors) == (10 if convergent else 1)
                maxima = [-float('inf')] * 5; delta = [0.] * 5
                peak = 0.; first = None; samples = 0
                for a, b in zip(on, off, strict=True):
                    assert a['timeMs'] == b['timeMs']; t = float(a['timeMs']); samples += 1
                    differences = [float(a[f'state{i}']) - float(b[f'state{i}']) for i in range(5)]
                    if any(x != 0 for x in differences) and first is None: first = t
                    for i in range(5):
                        delta[i] = max(delta[i], abs(differences[i]))
                        if t >= 1000: maxima[i] = max(maxima[i], float(a[f'state{i}']))
                    peak = max(peak, max(float(a[name]) for name in receptors))
                assert samples == 15001
                motor_summaries[key] = dict(samples=samples, firstVoltageDifferenceFromDisabledMs=first,
                    maximumAbsoluteVoltageDifferenceFromDisabledMv=delta,
                    maximumVoltageAfter1000Ms=maxima, maximumSavedReceptorState=peak,
                    voltageIndices='soma followed by four dendritic compartments; verify against frozen cell source')
                final = common.read(folder / 'packets1/final.json')
                motor_summaries[key]['motorSpikeTimesMs'] = [e['FiredAtNeuralMs'] for e in final['source']['Emissions']]
    review.update(reviewedUtc=datetime.now(timezone.utc).isoformat(), operatorReportedCompletion=True,
        savedAnalysisExactlyReproduced=True, analysis=analysis, populationObservationSummary=summaries,
        passedTests=expected_tests, originalFrames=48000, observedCellsPerFrame=256, roundedTelemetryIsCompleteState=False,
        declaredInputContacts=declared, tenConvergentInputs=convergent, motorTrajectorySummary=motor_summaries,
        numericalModelExecutedDuringReview=False, testsExecutedDuringReview=False,
        neuronalMotorResponseObserved=analysis['observedNeuronalMotorResponse'], physiologyQualified=False, avatarRecoveryAssessed=False)
    output.mkdir(); (output / 'review.json').write_text(json.dumps(review, indent=2)+'\n', encoding='utf-8')
    print(json.dumps(dict(review=review['verifiedFilesPerCopy'], cases={key: {k:v for k,v in summary.items()
        if k not in ('cells', 'eventsByTarget')} for key, summary in summaries.items()}, motor=motor_summaries), indent=2), flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(); parser.add_argument('--operator-reported-complete', action='store_true')
    parser.add_argument('--declared', action='store_true'); parser.add_argument('--ten', action='store_true'); args = parser.parse_args()
    if not args.operator_reported_complete: raise RuntimeError('Wait for the operator report before reading the capture.')
    if not __debug__: raise RuntimeError('Assertions must remain enabled.')
    main(args.declared, args.ten)

"""Review the operator-reported rubral capture; replay no neuron or test."""
import argparse
from collections import Counter
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec); spec.loader.exec_module(result); return result


def main():
    capture = ROOT / 'artifacts/rubral-motor-20261117-844975'
    output = ROOT / 'artifacts/rubral-motor-review-20261117'; assert not output.exists()
    common = module('rubral_review_integrity', ROOT / 'tools/review-olive-and-motor-20261109.py')
    print('Verifying reported rubral C:/D: files and source...', flush=True)
    review = common.verify(capture, ['test-binaries.json', 'probe-binaries.json'], '621109a7604c95bd4278a5a9c2efe779a41be2fb')
    archive = module('rubral_review_archive', ROOT / 'tools/review-spindle-root-and-causal-motor-20261113.py')
    review['verifiedGitFiles'] = archive.source_archive(common, capture, review['sourceRevision'])
    audit = module('rubral_completed_audit', capture / 'source/tools/rubral_motor_gate.py')
    original_records = audit.records; summaries = {}
    def observed_records(path):
        if path.name != 'source-frames.jsonl.gz':
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
        for name, row in cells.items():
            row.update(emittedEvents=counts[name], firstEventMs=first_event.get(name), lastEventMs=last_event.get(name))
        summaries[path.parent.name] = dict(selectedTarget=topology['identity']['Neuron'], selectedSources=selected,
            totalOriginalEvents=sum(counts.values()), emittingSourceCells=len(counts), reachedTargets=len(targets),
            eventsByTarget=dict(sorted(targets.items())), cells=dict(sorted(cells.items())),
            selectedTelemetry={name: cells[name] for name in selected})
    audit.records = observed_records
    analysis = audit.audit(capture)
    assert analysis == common.read(capture / 'analysis.json') and analysis['tests']['passedCases'] == 136
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
    review.update(reviewedUtc=datetime.now(timezone.utc).isoformat(), operatorReportedCompletion=True,
        savedAnalysisExactlyReproduced=True, analysis=analysis, populationObservationSummary=summaries,
        passedTests=136, originalFrames=48000, observedCellsPerFrame=256, roundedTelemetryIsCompleteState=False,
        numericalModelExecutedDuringReview=False, testsExecutedDuringReview=False,
        neuronalMotorResponseObserved=analysis['observedNeuronalMotorResponse'], physiologyQualified=False, avatarRecoveryAssessed=False)
    output.mkdir(); (output / 'review.json').write_text(json.dumps(review, indent=2)+'\n', encoding='utf-8')
    print(json.dumps(dict(review=review['verifiedFilesPerCopy'], cases={key: {k:v for k,v in summary.items()
        if k not in ('cells', 'eventsByTarget')} for key, summary in summaries.items()}), indent=2), flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(); parser.add_argument('--operator-reported-complete', action='store_true'); args = parser.parse_args()
    if not args.operator_reported_complete: raise RuntimeError('Wait for the operator report before reading the capture.')
    if not __debug__: raise RuntimeError('Assertions must remain enabled.')
    main()

"""Inspect saved, already completed contact recipients; execute no model or test."""
import argparse
from collections import Counter
import importlib.util
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def main():
    spec = importlib.util.spec_from_file_location('routing_review_helpers', ROOT / 'tools/prepare-causal-motor.py')
    h = importlib.util.module_from_spec(spec); spec.loader.exec_module(h)
    reviewed = ROOT / 'artifacts/rubral-motor-review-20261117'; h.verify(reviewed, ['review.json'])
    review = h.read(reviewed / 'review.json'); assert review['operatorReportedCompletion'] and review['savedAnalysisExactlyReproduced']
    capture = Path(review['capture']); rows = []; common_mapping = None
    for key in ('drive-quarter', 'drive-1', 'drive-4'):
        name = 'study/'+key+'/source-state/isolated-rubral-study.synapses.json'
        h.verify(capture, [name, 'study/'+key+'/input.json'])
        state = h.read(capture / name); inputs = h.read(capture / 'study' / key / 'input.json')['originalEvents']
        contacts = {c['SourceNeuronId']: c for c in state['Inbound']}
        assert len(contacts) == len(state['Inbound']) == 256 and len(inputs) == 5120
        mapping = {}
        for event in inputs:
            contact = contacts[event['SourceNeuronId']]
            assert contact['SourceInstanceKey'] == event['SourceInstanceKey']
            assert contact['SourceStructure'] == event['SourceStructure'] and contact['ProjectionId'] == event['ProjectionId']
            assert contact['Neurotransmitter'] == event['Neurotransmitter'] and not contact['IsFeedback'] and not contact['IsSyntheticBackground']
            value = dict(namedTarget=event['TargetNeuronId'], actualTargetIndex=contact['LastTargetNeuronIndex'],
                receivingContact=contact['SynapseId'], projection=contact['ProjectionId'])
            if event['SourceNeuronId'] in mapping: assert mapping[event['SourceNeuronId']] == value
            mapping[event['SourceNeuronId']] = value
        if common_mapping is None: common_mapping = mapping
        else: assert mapping == common_mapping
        count = Counter(v['actualTargetIndex'] for v in mapping.values())
        stats = review['populationObservationSummary'][key]['cells']
        assert len(count) == 172 and count[81] == 0 and mapping['fixture-081']['actualTargetIndex'] == 34
        assert all(stats[f'n-{i:03d}']['minimumNetCurrent'] == stats[f'n-{i:03d}']['maximumNetCurrent'] == 0
                   for i in range(256) if i not in count)
        rows.append(dict(case=key, contacts=256, fixtureEvents=5120, actualRecipients=len(count),
            unaddressedCells=sorted(set(range(256))-set(count)),
            namedTargetMatches=sum(v['namedTarget'] == f"n-{v['actualTargetIndex']:03d}" for v in mapping.values()),
            recipientMultiplicity=dict(sorted(Counter(count.values()).items())), selectedSourceInputs=count[81]))
    result = dict(operatorReportedCompletion=True, capture=str(capture), captureManifestSha256=h.digest(capture / 'backup-manifest.json'),
        parentReviewSha256=h.digest(reviewed / 'review.json'), conditions=rows, savedInputContactMapping=common_mapping,
        recipientMappingIdenticalAcrossDriveStrengths=True, selectedSourceCell='n-081',
        selectedSourceReceivedNoInput=True, fixture081ActualRecipient='n-034',
        allIntendedRecipientsWereActuallyStimulated=False, plasticStructuralRewiringDemonstrated=False,
        modelsExecuted=False, testsExecuted=False)
    output = ROOT / 'artifacts/rubral-input-routing-review-20261117'; output.mkdir()
    (output / 'review.json').write_text(json.dumps(result, indent=2)+'\n', encoding='utf-8')
    print(json.dumps({k:v for k,v in result.items() if k != 'savedInputContactMapping'}, indent=2))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(); parser.add_argument('--operator-reported-complete', action='store_true'); args = parser.parse_args()
    if not args.operator_reported_complete: raise RuntimeError('Wait for the operator completion report.')
    if not __debug__: raise RuntimeError('Assertions must remain enabled.')
    main()

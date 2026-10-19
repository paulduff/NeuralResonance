"""Check two isolated repair captures; does not qualify physiology or behaviour."""
import hashlib
import json
from pathlib import Path
import sys


def verify(repository, first, second):
    def read(folder, name):
        return json.loads((folder / (name + '.json')).read_text(encoding='utf-8-sig'))

    identities = [read(folder, 'connection-identity') for folder in (first, second)]
    assert all(item['separateConnectionLearningState'] for item in identities)
    mappings = [item['projectionFingerprint'] for item in identities]
    assert len(mappings[0]) == len(mappings[1]) == 128
    assert mappings[0] == mappings[1], 'Target mapping changed across processes'
    checks = {}
    for folder in (first, second):
        completion = read(folder, 'completion')
        assert completion['completed'] and not completion['liveBrainAccessed']
        for file, digest in completion['sourceHashes'].items():
            assert hashlib.sha256((repository / file).read_bytes()).hexdigest().upper() == digest, file
        post = read(folder, 'postsynaptic-event')
        assert post['inboundEvents'] == 1 and post['forcedDiagnosticPostsynapticSpikes'] > 0
        assert post['after']['updateCount'] - post['before']['updateCount'] == post['forcedDiagnosticPostsynapticSpikes']
        assert post['after']['weight'] > post['before']['weight']
        delayed = read(folder, 'delayed-modulation')
        assert delayed['additionalGlutamatergicPresynapticEvents'] == 0
        assert delayed['after']['weight'] > delayed['before']['weight']
        outbound = read(folder, 'outbound-learning')
        for case in outbound['cases']:
            assert case['initialWeight'] == case['finalWeight'] == case['minimumRelease'] == case['maximumRelease']
            assert case['totalAbsolutePlasticityChange'] == 0
        cadence = read(folder, 'cadence')
        for key in ('rateEightMillisecondsAfterSpike', 'activityEightMillisecondsAfterSpike'):
            values = [case[key] for case in cadence['cases']]
            assert max(values) - min(values) < .0001
        clock = read(folder, 'clock-budget')
        assert clock['warmStartInitialChange'] == 0 and clock['warmStartChangeAfterOneSecond'] > 0
        checks[folder.name] = dict(postEvent=post, delayedModulation=delayed,
                                  outbound=outbound, cadence=cadence, warmStart=clock)
    return dict(passed=True, targetMappingsCompared=128, targetMappingsDifferent=0,
                captures=checks, liveBrainChanged=False, biologicalEquivalenceQualified=False,
                physicalRecoveryAssessed=False)


if __name__ == '__main__':
    if len(sys.argv) != 5:
        raise SystemExit('Usage: verify_repair.py repository capture-a capture-b new-output.json')
    result = verify(*(Path(arg).resolve() for arg in sys.argv[1:4]))
    with Path(sys.argv[4]).open('x', encoding='utf-8') as output:
        json.dump(result, output, indent=2)
    print('REPAIR MECHANISM CHECKS PASSED: 0/128 differing target mappings. Physiological equivalence and recovery remain unqualified.')

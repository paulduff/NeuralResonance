"""Compare completed fresh reduced/expanded circuits; do not qualify physical recovery."""
import argparse
from collections import defaultdict
import json
import math
from pathlib import Path


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def require(condition, message):
    if not condition:
        raise ValueError(message)


def summarize(folder, protocol, case):
    instances = {side + '_' + r['id'] for r in protocol['regions'] for side in ['L', 'R']}
    energy = defaultdict(list)
    clocks = defaultdict(list)
    with (folder / case['case'] / 'electrical-trace.jsonl').open(encoding='utf-8-sig') as stream:
        for line in stream:
            row = json.loads(line)
            fields = ['meanMetabolicReserve', 'minimumMetabolicReserve', 'meanMetabolicFatigue', 'cellsAtReserveFloor']
            require(all(math.isfinite(row[k]) for k in fields), 'Non-finite energy observations')
            clocks[row['instance']].append(row['tick'])
            energy[row['phase'] + '|' + row['instance']].append(row)
    require(set(clocks) == instances, 'Missing instance energy observations')
    expected = list(range(10, protocol['caseMilliseconds'] + 1, 10))
    require(all(clock == expected for clock in clocks.values()), 'Incomplete/duplicate energy timeline')
    learning = read(folder / case['case'] / 'learning-summary.json')
    require({r['instance'] for r in learning['instances']} == instances, 'Incomplete learning summary')
    return {
        'plan': case['plan'], 'phases': case['phases'], 'electrical': case['electrical'],
        'transmission': case['transmission'], 'learning': learning['instances'],
        'energyByPhaseAndInstance': {key: {
            'samples': len(rows), 'firstMeanReserve': rows[0]['meanMetabolicReserve'],
            'lastMeanReserve': rows[-1]['meanMetabolicReserve'],
            'minimumCellReserve': min(r['minimumMetabolicReserve'] for r in rows),
            'maximumMeanFatigue': max(r['meanMetabolicFatigue'] for r in rows),
            'peakCellsAtReserveFloor': max(r['cellsAtReserveFloor'] for r in rows)
        } for key, rows in energy.items()}
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('package', type=Path)
    args = parser.parse_args()
    package = args.package.resolve()
    stages = [package / name for name in ['reduced-study', 'expanded-study']]
    protocols = [read(stage / 'protocol.json') for stage in stages]
    analyses = [read(stage / 'transfer-analysis.json') for stage in stages]
    for p, a, count, expanded in zip(protocols, analyses, [14, 20], [False, True]):
        require(p['freshOnly'] and not p['smoke'] and p['observeAllInstances'], 'Expected full fresh observed circuits')
        require(a['completed'] and all(a['validation'].values()), 'Stage validation failed')
        require(a['instanceCount'] == count and p['expandedCerebellarLoop'] == expanded, 'Unexpected circuit')
    for field in ['cases', 'caseMilliseconds', 'neuralTickMilliseconds', 'sensoryIntervalMilliseconds',
                  'modelDynamicsVersion', 'pacemakerClockVersion', 'microtubules', 'inputs']:
        require(protocols[0][field] == protocols[1][field], 'Mismatched paired protocol: ' + field)
    paired = []
    by_plan = [{r['plan']: r for r in a['results']} for a in analyses]
    for plan in protocols[0]['cases']:
        first, second = (rows[plan] for rows in by_plan)
        for field in ['primaryQueued', 'primaryFallSensitiveMessages']:
            require(first[field] == second[field], 'Different primary receptor input: ' + plan)
        paired.append({'plan': plan, 'reduced': summarize(stages[0], protocols[0], first),
                       'expanded': summarize(stages[1], protocols[1], second)})
    result = {'completed': True, 'matchedInputProtocols': True, 'casePairs': len(paired),
              'neuralTickMilliseconds': protocols[0]['neuralTickMilliseconds'], 'results': paired,
              'biologicalEquivalenceQualified': False, 'physicalRecoveryAssessed': False,
              'caution': 'Selected circuit comparison, not the whole brain. Energy quantities are model state, not measured metabolism. Contact change does not establish useful learning. Decoder/latch summaries sample every 10 ms.'}
    (package / 'circuit-comparison.json').write_text(json.dumps(result, indent=2, allow_nan=False) + '\n', encoding='utf-8')
    print(f"Saved {len(paired)} circuit pairs with complete energy observations. Neural quality awaits review.")


if __name__ == '__main__':
    main()

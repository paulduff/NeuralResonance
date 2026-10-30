"""Validate isolated support interventions and summarize results without assigning biological validity."""
import argparse
import itertools
import json
import math
from pathlib import Path


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def require(condition, message):
    if not condition:
        raise ValueError(message)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('study', type=Path)
    study = parser.parse_args().study.resolve()
    p, r = read(study/'protocol.json'), read(study/'results.json')
    require(p['study'] == 'cell-support-controls' and r['completed'], 'Completed support study required')
    expected = set(itertools.product(p['profiles'], p['controls'], p['integrationStepsMilliseconds'], p['inputs']))
    key = lambda c: (c['structure'], c['control'], c['stepMilliseconds'], c['input'])
    require(len(r['cases']) == len(expected) == p['caseCount'] and {key(c) for c in r['cases']} == expected, 'Incomplete matrix')
    data = {key(c): read(study/(c['case']+'.json')) for c in r['cases']}
    count, duration = p['sampledCellsPerCondition'], p['durationMilliseconds']
    ends = p['phaseEndsMilliseconds']
    lengths = dict(zip(p['phases'], [b-a for a,b in zip([0]+ends[:-1],ends)]))
    for k, c in data.items():
        dt = c['stepMilliseconds']
        applications = int(duration/dt)*count
        require(c['reserveApplications'] == (applications if c['control'] in ['hold-reserve-fatigue','hold-both'] else 0), 'Reserve intervention count differs')
        require(c['potassiumApplications'] == (applications if c['control'] in ['hold-potassium','hold-both'] else 0), 'Potassium intervention count differs')
        challenge = c['input'] == 'challenge'
        require(c['glutamateEvents'] == (math.ceil(lengths['excitation-window']/50)*count if challenge else 0), 'Excitation count differs')
        require(c['gabaEvents'] == (math.ceil(lengths['inhibition-window']/10)*count if challenge else 0), 'Inhibition count differs')
        indices = [x['index'] for x in c['phases']['initial-quiet']]
        require(len(set(indices)) == len(indices) == count, 'Incorrect cell sample')
        for phase, rows in c['phases'].items():
            require([x['index'] for x in rows] == indices, 'Cell identity changed')
            start = ends[p['phases'].index(phase)] - lengths[phase]
            for x in rows:
                require(x['samples']*dt == x['elapsedMilliseconds'] == lengths[phase], 'Incomplete sampling')
                require(all(math.isfinite(v) for v in [x['rateHz'], x['minimumReserve'], x['lastReserve'], x['maximumFatigue'], x['maximumPotassium']]), 'Non-finite observation')
                require(.2 <= x['minimumReserve'] <= x['lastReserve'] <= 1, 'Invalid reserve')
                times = x['spikeTimesMilliseconds']
                require(times == sorted(set(times)) and all(start < t <= start+lengths[phase] for t in times), 'Invalid spike timeline')
        times = []
        with (study/(c['case']+'.jsonl')).open(encoding='utf-8-sig') as stream:
            for line in stream:
                row = json.loads(line); times.append(row['timeMilliseconds'])
                require([x['index'] for x in row['cells']] == indices, 'Trace cell identity changed')
                require(all(math.isfinite(v) for x in row['cells'] for v in x.values()), 'Non-finite trace')
        require(times == list(range(p['traceIntervalMilliseconds'], duration+1,p['traceIntervalMilliseconds'])), 'Incomplete trace')
        quiet = data[(k[0],k[1],k[2],'quiet')]
        require(c['phases']['initial-quiet'] == quiet['phases']['initial-quiet'], 'Input arms start differently')
    references = []
    if not p['smoke'] or (study.parent/'reference').exists():
        for dt in p['integrationStepsMilliseconds']:
            token = str(dt).replace('.','')
            reference = read(study.parent/'reference'/f'CerebellarVermis-pacemaker-{token}ms-none.json')
            native = data[('CerebellarVermis','native',dt,'quiet')]
            for i, cell in enumerate(native['phases']['initial-quiet']):
                old_times = [t for phase in ['initial','stimulus','return'] for t in reference['phases'][phase][i]['spikeTimesMilliseconds'] if t <= ends[0]]
                require(cell['index'] == reference['phases']['initial'][i]['index'] and cell['spikeTimesMilliseconds'] == old_times, 'Native cells do not reproduce the previous eight-second capture')
            references.append({'stepMilliseconds':dt,'cells':count,'spikeTimesExactlyMatch':True})
    rows = []
    for k, c in data.items():
        quiet = data[(k[0],k[1],k[2],'quiet')]
        stats = {}
        for phase, cells in c['phases'].items():
            rates = [x['rateHz'] for x in cells]
            stats[phase] = {'meanRateHz':sum(rates)/count,
                'rateDifferenceFromMatchedQuietHz':sum(x['rateHz']-q['rateHz'] for x,q in zip(cells,quiet['phases'][phase]))/count,
                'minimumReserve':min(x['minimumReserve'] for x in cells),
                'meanLastReserve':sum(x['lastReserve'] for x in cells)/count,
                'meanMaximumFatigue':sum(x['maximumFatigue'] for x in cells)/count,
                'meanMaximumPotassium':sum(x['maximumPotassium'] for x in cells)/count,
                'cellsReachingReserveFloor':sum(x['firstReserveFloorTimeMilliseconds'] is not None for x in cells)}
        rows.append({'case':c['case'],'structure':k[0],'control':k[1],'stepMilliseconds':k[2],'input':k[3],'phases':stats})
    result = {'completed':True,'caseCount':len(rows),'smoke':p['smoke'],'accountingValidated':True,
              'freshBaselinesMatched':True,'nativeReferenceMatches':references,'results':rows,
              'interventionsAreArtificial':True,'biologicalEquivalenceQualified':False,'physicalRecoveryAssessed':False,
              'caution':'Held model state tests causal contribution. It is not a biological repair or a validated replacement energy supply. In-step potassium accumulation remains active.'}
    (study/'cell-analysis.json').write_text(json.dumps(result,indent=2,allow_nan=False)+'\n',encoding='utf-8')
    print(f"Validated {len(rows)} conditions, input/intervention counts and traces. Native reference matches: {len(references)}. Biological quality awaits review.")


if __name__ == '__main__':
    main()

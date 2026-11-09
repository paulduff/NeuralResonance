"""Complete one missing solve from a sealed Golgi capture; never modify that capture.

Each stage runs in the independent PowerShell window. Preflight and audit only
read saved arrays; only --solve may call the frozen reference integrator.
"""
import argparse
from datetime import datetime, timezone
from hashlib import sha256
import importlib.metadata
import importlib.util
import json
import os
from pathlib import Path
import sys

for name in ('OPENBLAS_NUM_THREADS', 'OMP_NUM_THREADS', 'MKL_NUM_THREADS'):
    os.environ[name] = '1'
import numpy as np
import scipy

PROTOCOLS = ['quiet', 'depolarize', 'hyperpolarize', 'no-nap', 'no-hcn', 'no-sk']
LEVELS = [dict(label='bdf-tighter', method='BDF', rtol=1e-12, voltage_atol=1e-13,
               gate_atol=1e-15, calcium_atol=1e-14, max_step=.125),
          dict(label='radau-strict', method='Radau', rtol=1e-10, voltage_atol=1e-11,
               gate_atol=1e-13, calcium_atol=1e-12, max_step=.125)]
MISSING = ('no-sk', 'radau-strict')
CRITERIA = dict(maximumSomaDifferenceMv=.05, maximumOrdinalDifferenceMs=.01, matchingCounts=True)
PAIRS = ['old-strict-vs-bdf-tighter', 'bdf-tighter-vs-radau-strict']
read = lambda p: json.loads(p.read_text(encoding='utf-8-sig'))


def io_path(path):
    text = str(path.absolute())
    return Path('\\\\?\\' + text) if os.name == 'nt' and not text.startswith('\\\\?\\') else path


def digest(path):
    return sha256(io_path(path).read_bytes()).hexdigest().upper()


def write_new(path, value):
    with path.open('x', encoding='utf-8') as stream:
        stream.write(json.dumps(value, indent=2, allow_nan=False) + '\n')


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result)
    return result


def environment():
    paths = {Path(sys.executable).resolve(), Path(sys._base_executable).resolve()}
    paths.update(Path(sys.base_prefix).glob('python*.dll'))
    for name in ('numpy', 'scipy'):
        distribution = importlib.metadata.distribution(name)
        for entry in distribution.files:
            path = Path(distribution.locate_file(entry)).resolve()
            if path.suffix != '.pyc':
                assert io_path(path).is_file(), 'Recorded dependency is missing: ' + str(path)
                paths.add(path)
    return dict(python=sys.version, numpy=np.__version__, scipy=scipy.__version__,
                files=[dict(path=str(p), sha256=digest(p)) for p in sorted(paths)])


def validate_package(package):
    config = read(package / 'configuration.json')
    assert config['ExpectedReusedSolves'] == 11 and config['ExpectedNewSolves'] == 1
    assert config['ExpectedReferenceComparisons'] == 12 and config['ExpectedCandidateComparisons'] == 36
    assert config['NoLiveMonitoring'] and config['OriginalRevision'] == '73db8166d81e0f88c46e6368aee196bf5b647070'
    for entry in config['PackageFiles']:
        assert digest(package / entry['Path']) == entry['Sha256'], entry['Path']
    spec = read(package / 'retained-reference/protocol.json')
    assert spec['protocols'] == PROTOCOLS and spec['levels'] == LEVELS
    assert spec['inheritedCriteria'] == CRITERIA and spec['requiredPairs'] == PAIRS
    assert spec['candidateUnchanged'] and not spec['biologicalParametersChanged']
    assert np.__version__ == spec['numpyVersion'] and scipy.__version__ == spec['scipyVersion']
    runs = read(package / 'retained-reference/completed-solves.json')
    expected = {(p, level['label']) for p in PROTOCOLS for level in LEVELS} - {MISSING}
    assert len(runs) == 11 and {(r['protocol'], r['label']) for r in runs} == expected
    for row in runs:
        level = next(x for x in LEVELS if x['label'] == row['label'])
        assert all(row[k] == v for k, v in level.items())
        assert row['nfev'] > 0 and row['njev'] >= 0 and row['nlu'] >= 0 and row['skMassError'] < 1e-8
    assert {p.name for p in (package / 'retained-reference').glob('*.npz')} == {p + '-' + l + '.npz' for p, l in expected}
    cases = read(package / 'golgi-input/study/results.json')
    assert cases['completed'] and len(cases['cases']) == 24
    for name in PROTOCOLS:
        selected = [c for c in cases['cases'] if c['protocol'] == name]
        assert len(selected) == 3 and {c['stepMs'] for c in selected} == {.015625, .0078125, .00390625}
        assert all(c['durationMs'] == 6000 and c['injectionStartMs'] == 2000 and c['injectionEndMs'] == 3000 for c in selected)
        assert len({(c['block'], c['injectionPa']) for c in selected}) == 1
    return cases['cases']


def array(path):
    with np.load(path, allow_pickle=False) as data:
        assert set(data.files) == {'times', 'states', 'spikes'}
        result = {k: data[k].copy() for k in data.files}
    t, y, s = result['times'], result['states'], result['spikes']
    assert np.array_equal(t, np.arange(24001) / 4) and y.shape == (24001, 159)
    assert y.dtype == np.float64 and np.isfinite(y).all()
    assert s.ndim == 1 and np.isfinite(s).all() and (np.diff(s) > 0).all()
    assert (s >= 0).all() and (s <= 6000).all()
    assert y[:, 133:157].min() > -1e-8 and y[:, 133:157].max() < 1 + 1e-8
    assert y[:, -2:].min() > 0 and np.max(np.abs(y[:, 151:157].sum(axis=1) - 1)) < 1e-8
    return result


def candidate_comparison(case, trace, ref, label):
    assert np.array_equal(trace['timeMs'], ref['times'])
    assert all(np.isfinite(trace[n]).all() for n in trace.dtype.names)
    s = np.array(case['somaticCrossingsMs'])
    v = np.abs(trace['somaMv'] - (ref['states'][:, 0] - 55))
    return dict(protocol=case['protocol'], stepMs=case['stepMs'], reference=label,
                counts=[len(s), len(ref['spikes'])],
                maximumOrdinalDifferenceMs=float(np.abs(s - ref['spikes']).max(initial=0)) if len(s) == len(ref['spikes']) else None,
                maximumSomaDifferenceMv=float(v.max()),
                maximumFirst20MsDifferenceMv=float(v[ref['times'] <= 20].max()),
                maximumHighCalciumDifferenceMm=float(np.abs(trace['highCaMm'] - ref['states'][:, -2] / 1000).max()))


def audit(package, complete):
    cases = validate_package(package)
    original = module('frozen_comparison', package / 'reference-source/golgi-reference-refinement.py')
    saved = read(package / 'retained-reference/partial-comparisons.json')
    comparisons, candidates = [], []
    protocols = PROTOCOLS if complete else PROTOCOLS[:-1]
    for name in protocols:
        old = array(package / f'golgi-input/study/active-reference/{name}-strict.npz')
        refs = {}
        for level in LEVELS:
            folder = 'study' if (name, level['label']) == MISSING else 'retained-reference'
            refs[level['label']] = array(package / folder / (name + '-' + level['label'] + '.npz'))
        for pair, a, b in ((PAIRS[0], old, refs['bdf-tighter']), (PAIRS[1], refs['bdf-tighter'], refs['radau-strict'])):
            comparisons.append(dict(protocol=name, pair=pair, **original.compare(a, b)))
        for case in (c for c in cases if c['protocol'] == name):
            trace = np.genfromtxt(package / 'golgi-input/study' / (case['key'] + '.csv'), delimiter=',', names=True)
            for level in LEVELS:
                candidates.append(candidate_comparison(case, trace, refs[level['label']], level['label']))
        print('Recomputed saved-array comparisons: ' + name, flush=True)
    # The first five protocols must still reproduce every recorded metric exactly.
    assert comparisons[:10] == saved['comparisons'] and candidates[:30] == saved['candidate']
    # Preflight also validates the eleventh cached solve, even though its pair is missing.
    if not complete:
        array(package / 'retained-reference/no-sk-bdf-tighter.npz')
    assert len(comparisons) == (12 if complete else 10) and len(candidates) == (36 if complete else 30)
    if complete:
        new = read(package / 'study/new-solve.json')
        assert new['protocol'] == MISSING[0] and new['label'] == MISSING[1]
        assert all(new[k] == v for k, v in LEVELS[1].items())
        assert new['sha256'] == digest(package / 'study/no-sk-radau-strict.npz')
        assert new['nfev'] > 0 and new['skMassError'] < 1e-8
        assert {p.name for p in (package / 'study').glob('*.npz')} == {'no-sk-radau-strict.npz'}
    return dict(completed=complete, reusedSolves=11, newSolves=1 if complete else 0,
                reproducedRecordedReferenceComparisons=10, reproducedRecordedCandidateComparisons=30,
                allOriginalReferenceCriteriaPassed=complete and all(c['passedOriginalCriteria'] for c in comparisons),
                comparisons=comparisons, candidateComparisons=candidates,
                activeCandidateQualified=False, physiologyQualified=False, avatarRecoveryAssessed=False)


def main(package, stage):
    if stage == 'preflight':
        assert not (package / 'study').exists(), 'Use a fresh package.'
        report = audit(package, False)
        write_new(package / 'preflight.json', report)
        write_new(package / 'environment.json', environment())
        print('Eleven cached solves validated; ten reference and thirty candidate comparisons reproduced.', flush=True)
    elif stage == 'solve':
        cases = validate_package(package)
        preflight = read(package / 'preflight.json')
        assert preflight['reproducedRecordedCandidateComparisons'] == 30 and preflight['reusedSolves'] == 11
        assert environment() == read(package / 'environment.json'), 'Numerical environment changed.'
        output = package / 'study'
        output.mkdir()  # No overwrite or implicit restart after interruption.
        write_new(output / 'continuation.json', dict(startedUtc=datetime.now(timezone.utc).isoformat(),
                  protocol=MISSING[0], level=LEVELS[1], computedSolves=1, reusedSolves=11,
                  originalProtocolSha256=digest(package / 'retained-reference/protocol.json')))
        reference = module('frozen_golgi_reference', package / 'reference-source/golgi-active-reference.py')
        cable = module('frozen_golgi_cable', package / 'reference-source/analyse-golgi-cell.py')
        inputs = package / 'golgi-input'
        nodes = cable.morphology(inputs / 'reference')
        cap, stiffness = cable.matrix(nodes)
        channels = reference.ReferenceChannels(inputs / 'reference')
        clamp_error = reference.verify_clamps(channels, read(inputs / 'clamps/channels.json'))
        solver = reference.CoupledReference(nodes, cap, stiffness, channels)
        case = next(c for c in cases if c['protocol'] == MISSING[0])
        print('Computing the single missing solve: no-sk / radau-strict. This may take hours.', flush=True)
        result, stats = solver.run(case, **{k: v for k, v in LEVELS[1].items() if k != 'label'})
        path = output / 'no-sk-radau-strict.npz'
        with path.open('xb') as stream:
            np.savez_compressed(stream, **result)
        array(path)
        write_new(output / 'new-solve.json', dict(protocol=MISSING[0], **LEVELS[1], **stats,
                  sha256=digest(path), gateError=clamp_error, completedUtc=datetime.now(timezone.utc).isoformat()))
        assert environment() == read(package / 'environment.json'), 'Numerical environment changed.'
        print('Missing solve saved. Full reference and candidate comparisons are next.', flush=True)
    else:
        assert environment() == read(package / 'environment.json'), 'Numerical environment changed.'
        report = audit(package, True)
        write_new(package / 'study/analysis.json', report)
        if not report['allOriginalReferenceCriteriaPassed']:
            raise RuntimeError('Original reference criteria not all met; complete evidence retained without changing bounds.')
        print('All twelve reference contrasts pass their original bounds; 36 candidate comparisons retained.', flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('package', type=Path)
    parser.add_argument('--stage', choices=['preflight', 'solve', 'audit'], required=True)
    args = parser.parse_args()
    main(args.package.resolve(), args.stage)

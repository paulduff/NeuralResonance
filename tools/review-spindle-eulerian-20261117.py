"""Reconcile operator-reported saved continuous-reference evidence; no integration."""
import argparse
from datetime import datetime, timezone
import importlib.util
import json
import os
from pathlib import Path
import sys

for key in ('OMP_NUM_THREADS', 'OPENBLAS_NUM_THREADS', 'MKL_NUM_THREADS', 'NUMEXPR_NUM_THREADS'): os.environ[key] = '1'
import numpy as np
ROOT = Path(__file__).resolve().parents[1]


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec); spec.loader.exec_module(result); return result


def main():
    capture = ROOT / 'artifacts/spindle-eulerian-20261117-f0020c'
    output = ROOT / 'artifacts/spindle-eulerian-review-20261117'; assert not output.exists()
    common = module('eulerian_integrity', ROOT / 'tools/review-olive-and-motor-20261109.py')
    print('Verifying reported continuous spindle C:/D: files and source...', flush=True)
    review = common.verify(capture, [], '857518d9048c0c50a81d43540e7f95cac461bb8a')
    archive = module('eulerian_archive', ROOT / 'tools/review-spindle-root-and-causal-motor-20261113.py')
    review['verifiedGitFiles'] = archive.source_archive(common, capture, review['sourceRevision'])
    sys.path.insert(0, str(capture / 'source/tools'))
    audit = module('eulerian_completed_analysis', capture / 'source/tools/analyse-spindle-eulerian.py')
    def forbidden(*args, **kwargs): raise RuntimeError('A completed-state review cannot execute a model or control.')
    audit.EulerianPopulation.__init__ = forbidden; audit.integrate = forbidden; audit.solve_case = forbidden
    # This function performs only contrasts on saved arrays. Verify its output
    # in memory without replacing any original capture file.
    comparisons = {}
    def check_saved(path, value):
        assert value == common.read(path), path.name
        comparisons[path.name] = value
    audit.save = check_saved
    current = audit.environment(capture)
    assert current == common.read(capture / 'preparation-environment.json') == common.read(capture / 'reference-preflight.json')['environment']
    post = common.read(capture / 'reference-postflight.json'); assert post['unchanged'] and post['environment'] == current
    config = common.read(capture / 'configuration.json'); status = common.read(capture / 'window-status.json')
    assert config['NoLiveMonitoring'] and config['IsolatedEulerianSpindle']
    assert status['ComponentChecksPassed'] and status['NumericalEnvironmentUnchanged'] and status['FrozenPackageUnchanged']
    spec = common.read(capture / 'eulerian-input.json'); assert spec == audit.protocol()
    provenance = common.read(capture / 'provenance.json')
    assert not provenance['testsExecutedDuringPreparation'] and not provenance['modelsExecutedDuringPreparation']
    for item in provenance['copied']:
        assert common.digest(capture / item['path']) == item['sha256'] == common.digest(Path(item['capture']) / item['original'])
    controls = common.read(capture / 'controls-analysis.json'); assert controls['completed'] and controls['passed'] and len(controls['cases']) == 12
    for index, row in enumerate(controls['cases']):
        assert row == common.read(capture / 'controls' / (row['label']+'.json'))
        assert row['passed'] and row['maximumError'] <= row['bound'] == spec['criteria']['analyticStateAbsolute']
        if index < 8:
            folder = capture / 'controls' / row['label']
            with np.load(folder / 'trace.npz', allow_pickle=False) as actual, np.load(folder / 'analytic.npz', allow_pickle=False) as expected:
                error = float(np.max(abs(actual['states'][-1]-expected['finalState']))) if row['label'] == 'outflow-return' else \
                    float(np.max(abs(actual['states']-expected['states'])))
                assert error == row['maximumError']
    fixed = common.read(capture / 'fixed-analysis.json'); moving = common.read(capture / 'moving-results.json')
    assert fixed['completed'] and fixed['passed'] and len(fixed['cases']) == 6 and len(fixed['comparisons']) == 18
    assert moving['completed'] and len(moving['cases']) == 42
    totals = dict(sampledEndpoints=0, acceptedNodes=0, maximumLedgerResidual=0., maximumDomainViolation=0.)
    print('Reconciling sampled distributions, accepted nodes and original contrasts...', flush=True)
    def accounting(states, lengths):
        bound = states[:, 1:-4].sum(axis=1); fraction = (1120.-(lengths-815.))/735.
        mass = float(np.max(abs(states[:, 0]+bound-1)))
        actin = float(np.max(abs(states[:, -4]+states[:, -3]-1)))
        domain = float(max(0., -states.min(), np.max(bound-states[:, -3]), np.max(states[:, -3]-fraction)))
        totals['maximumLedgerResidual'] = max(totals['maximumLedgerResidual'], mass, actin)
        totals['maximumDomainViolation'] = max(totals['maximumDomainViolation'], domain)
        return dict(myosinResidual=mass, actinResidual=actin, domainViolation=domain,
            passed=bool(max(mass, actin) <= spec['criteria']['conservationAbsolute'] and domain <= spec['criteria']['domainAbsolute']))
    for kind, rows in (('fixed', fixed['cases']), ('moving', moving['cases'])):
        for row in rows:
            folder = capture / kind / row['label']; result = common.read(folder / 'result.json')
            assert all(row[k] == v for k, v in result.items()) and result['completed']
            with np.load(folder / 'trace.npz', allow_pickle=False) as raw:
                times, states, lengths, edges = (raw[k] for k in ('times', 'states', 'lengths', 'edges'))
                n = result['finiteVolumeCells']; subdivision = result['subdivision']
                assert states.shape == (result['samples'], n+5) and len(edges) == n+1 and np.all(np.diff(edges) > 0)
                assert np.isfinite(states).all() and np.isfinite(lengths).all() and np.all(np.diff(times) > 0)
                assert edges[0] == -result['halfRangeNm'] and edges[-1] == result['halfRangeNm']
                initial = np.zeros(n+5); initial[0] = initial[-4] = 1
                assert np.array_equal(states[0], initial)
                assert accounting(states, lengths) == result['sampledAccounting'] and result['sampledAccounting']['passed']
                observed = np.concatenate((states[:, :1], states[:, 1:-4].reshape(len(times), -1, subdivision).sum(axis=-1), states[:, -4:-2]), axis=1)
                assert np.array_equal(observed, raw['originalBins'])
                centres = (edges[:-1]+edges[1:])/2
                cross = 69000.*(states[:, 1:-4] @ (centres+2.5))
                assert np.array_equal(cross, raw['crossBridgeStressPa'])
                passive, slack = (90., 1050.) if result['fiber'] == 'bag' else (250., 1200.)
                assert np.array_equal(cross+passive*(lengths-slack), raw['stressPa'])
                previous = initial; end_time = times[0]
                for i, segment in enumerate(result['segments']):
                    assert segment == common.read(folder / f'segment-{i}.json') and segment['success']
                    with np.load(folder / f'segment-{i}.npz', allow_pickle=False) as saved:
                        t, y, length = (saved[k] for k in ('times', 'states', 'lengths'))
                        assert len(t) == segment['acceptedNodes'] and y.shape == (len(t), n+5)
                        assert t[0] == end_time == segment['startSeconds'] and t[-1] == segment['endSeconds'] and np.all(np.diff(t) > 0)
                        assert np.isfinite(y).all() and np.array_equal(y[0], previous) and np.array_equal(saved['initial'], previous)
                        assert np.array_equal(length, segment['initialLengthNm']+segment['lengthSpeedNmPerSecond']*(t-t[0]))
                        assert accounting(y, length) == result['acceptedNodeAccounting'][i] and result['acceptedNodeAccounting'][i]['passed']
                        assert np.all(np.diff(y[:, -2:], axis=0) >= -1e-14)
                        at_end = np.flatnonzero(times == t[-1]); assert len(at_end) == 1 and np.array_equal(states[at_end[0]], y[-1])
                        previous = y[-1].copy(); end_time = t[-1]; totals['acceptedNodes'] += len(t)
                assert end_time == times[-1]
                totals['sampledEndpoints'] += len(times)
    for pair in fixed['comparisons']:
        folder = capture / 'fixed' / (pair['fiber']+'-'+pair['protocol'])
        with np.load(folder / 'trace.npz', allow_pickle=False) as actual, np.load(capture / 'reference/fixed' /
                (pair['fiber']+'-'+pair['protocol']+'-'+pair['reference']+'.npz'), allow_pickle=False) as expected:
            assert np.array_equal(actual['times'], expected['times'])
            error = float(np.max(abs(actual['originalBins']-expected['states'])))
            passive, slack = (90., 1050.) if pair['fiber'] == 'bag' else (250., 1200.)
            stress = 69000.*(expected['states'][:, 1:-2] @ (np.arange(-20., 20.5, .5)+2.5))+passive*(1250-slack)
            stress_error = float(np.max(abs(actual['stressPa']-stress)))
            assert error == pair['maximumStateDifference'] and stress_error == pair['maximumStressDifferencePa'] and pair['passed']
    audit.compare(capture)
    analysis = comparisons['eulerian-analysis.json']
    assert not analysis['sampledReferenceDiscretizationCriteriaPassed'] and not status['SampledReferenceDiscretizationCriteriaPassed']
    summary = {}
    for kind in ('temporal', 'spatial', 'range', 'candidate'):
        rows = analysis[kind+'Contrasts']
        if kind == 'spatial': rows = [r for r in rows if r['fineSubdivision'] == 64]
        summary[kind] = dict(contrasts=len(rows), maximumPopulationL1=max(r['maximumPopulationL1'] for r in rows),
            maximumScalarDifference=max(r['maximumScalarDifference'] for r in rows),
            maximumStressDifferencePa=max(r['maximumStressDifferencePa'] for r in rows),
            passed=all(r.get('passed', False) for r in rows))
    review.update(reviewedUtc=datetime.now(timezone.utc).isoformat(), operatorReportedCompletion=True,
        savedRecordsReconciled=True, savedComparisonsExactlyReproduced=True, analyticControls=12, fixedCases=6,
        movingSolves=42, originalBoundsChanged=False, summary=summary, accounting=totals,
        referenceDiscretizationQualified=False, candidateMovingAccuracyQualified=False,
        modelsExecutedDuringReview=False, testsExecutedDuringReview=False, physiologyQualified=False, avatarRecoveryAssessed=False)
    output.mkdir(); (output / 'review.json').write_text(json.dumps(review, indent=2)+'\n', encoding='utf-8')
    print(json.dumps(dict(summary=summary, accounting=totals, verifiedFilesPerCopy=review['verifiedFilesPerCopy']), indent=2), flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(); parser.add_argument('--operator-reported-complete', action='store_true'); args = parser.parse_args()
    if not args.operator_reported_complete: raise RuntimeError('Wait for the operator report before reading the capture.')
    if not __debug__: raise RuntimeError('Assertions must remain enabled.')
    main()

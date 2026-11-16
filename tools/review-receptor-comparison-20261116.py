"""Review reported receptor capture and decompose saved crossing-time errors.

No integration, derivative evaluation, tests, new parameters or changed bounds.
The original FAILED capture is read-only. Secants use saved reference samples.
"""
import argparse
from datetime import datetime, timezone
import importlib.util
import json
import os
from pathlib import Path
import sys

for name in ('OMP_NUM_THREADS', 'OPENBLAS_NUM_THREADS', 'MKL_NUM_THREADS', 'NUMEXPR_NUM_THREADS'):
    os.environ[name] = '1'
import numpy as np

ROOT = Path(__file__).resolve().parents[1]
CAPTURE = ROOT / 'artifacts/receptor-membrane-comparison-20261116-f7ece6'
REVISION = 'ade8c0252749a204dee4715cb88ab850d1492fb5'


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    value = importlib.util.module_from_spec(spec); spec.loader.exec_module(value)
    return value


def main():
    output = ROOT / 'artifacts/receptor-comparison-review-20261116'
    assert not output.exists(), 'Preserve previous reviews.'
    common = module('receptor_complete_integrity', ROOT / 'tools/review-olive-and-motor-20261109.py')
    print('Verifying completed C:/D: bytes and frozen source...', flush=True)
    review = common.verify(CAPTURE, ['probe-binaries.json'], REVISION, 'FAILED', 1)
    archive = module('receptor_complete_archive', ROOT / 'tools/review-spindle-root-and-causal-motor-20261113.py')
    review['verifiedGitFiles'] = archive.source_archive(common, CAPTURE, REVISION)
    sys.path.insert(0, str(CAPTURE / 'source/tools'))
    dependencies = module('receptor_complete_environment', CAPTURE / 'source/tools/golgi-reference-resume.py')
    assert dependencies.environment() == common.read(CAPTURE / 'reference-preflight.json')['environment']
    runtimes = [common.read(CAPTURE / name) for name in ('managed-runtime-before.json',
        'managed-runtime-after-candidate.json', 'managed-runtime-after-output.json', 'managed-runtime-after.json')]
    assert all(r == runtimes[0] for r in runtimes)
    status = common.read(CAPTURE / 'window-status.json')
    assert status['CandidateMatrixComplete'] and status['SourceOutputCaptureComplete']
    assert status['ManagedRuntimePreserved'] and status['NumericalEnvironmentPreserved'] and status['FrozenPackageUnchanged']
    report = common.read(CAPTURE / 'membrane-reference/comparison.json')
    assert report['completed'] and not report['numericalCriteriaPassed'] and report['frozenInputsAndEnvironmentPreserved']
    spec = common.read(CAPTURE / 'receptor-membrane-input.json')
    criteria = spec['criteria']; assert report['criteria'] == criteria
    candidate_audit = module('receptor_completed_candidate', CAPTURE / 'source/tools/audit-receptor-membrane.py')
    print('Auditing 18 saved membrane traces and 36 recorded source/receiver cases...', flush=True)
    candidate = candidate_audit.audit(CAPTURE)
    assert candidate['summary'] == report['candidateAudit']
    output_audit = module('receptor_completed_output', CAPTURE / 'source/tools/analyse-receptor-motor-output.py')
    source_output = output_audit.audit(CAPTURE, candidate)
    assert source_output == report['sourceOutputAudit'] == common.read(CAPTURE / 'motor-output-analysis.json')
    reference = module('receptor_completed_metrics', CAPTURE / 'source/tools/receptor_membrane_reference.py')
    # Parse pinned geometry only; do not construct/evaluate the neuronal RHS.
    parser = module('receptor_completed_geometry', CAPTURE / 'source/tools/motoneuron-reference-check.py')
    _, soma, dendrite = parser.model(CAPTURE / 'reference/motor-source')
    sections = [soma] + [dendrite] * 4
    area = np.asarray([np.pi * x['diam'] * x['L'] for x in sections])
    cap = .01 * area * np.asarray([x['cm'] for x in sections])
    assert len(report['analyticControls']) == 6 and all(x['passed'] for x in report['analyticControls'])
    assert sum(len(c['jacobianControls']) for c in report['cases']) == 36
    assert [c['key'] for c in report['cases']] == [c['key'] for c in spec['cases']]
    decomposition = []; failures = []; counts = dict(referenceContrasts=0, candidateContrasts=0, refinementContrasts=0)
    for case in report['cases']:
        print('Rechecking saved numerical comparisons: ' + case['key'], flush=True)
        native = candidate['cases'][case['key']]['conditions']; saved = []
        for method, record in zip(spec['referenceMethods'], case['referenceMethods']):
            assert method['key'] == record['key']
            with np.load(CAPTURE / 'membrane-reference' / (case['key'] + '-' + method['key'] + '.npz')) as data:
                time = data['timeMs']; state = data['state']; crossings = data['crossingMs']
                assert reference.physical(data['acceptedState'], cap, criteria) == record['acceptedNodes']
                assert reference.physical(state, cap, criteria) == record['comparisonPoints']
                assert record['acceptedNodes']['passed'] and record['comparisonPoints']['passed']
                assert crossings.tolist() == record['crossingsMs']
                saved.append((time, state, crossings))
        assert len(saved) == len(case['referenceMethods']) == 3
        assert all(np.array_equal(x[0], saved[0][0]) for x in saved)
        for pair, stored in zip(((0, 1), (0, 2), (1, 2)), case['referenceContrasts']):
            left, right = pair
            error = reference.errors(saved[left][1], saved[right][1], saved[left][2], saved[right][2])
            assert error == stored['error'] and stored['passed'] == reference.meets(error, criteria, 'reference')
            assert stored['passed']; counts['referenceContrasts'] += 1
        grid, primary, events = saved[1]
        for condition, stored in zip(native, case['candidateContrasts']):
            t = condition['time']; y = condition['state']; indices = np.searchsorted(grid, t)
            assert np.array_equal(grid[indices], t)
            error = reference.errors(y, primary[indices], condition['crossings'], events)
            physical = reference.physical(y, cap, criteria)
            assert error == stored['error'] and physical == stored['physical']
            assert stored['passed'] == (physical['passed'] and reference.meets(error, criteria, 'candidate'))
            assert stored['passed']; counts['candidateContrasts'] += 1
            brackets = np.flatnonzero((y[:-1, 0] < -20) & (y[1:, 0] >= -20))
            assert len(brackets) == len(events)
            for ordinal, i in enumerate(brackets):
                t0, t1 = float(t[i]), float(t[i+1]); actual = float(condition['crossings'][ordinal])
                v0, v1 = map(float, primary[indices[i:i+2], 0])
                bracketed = v0 < -20 <= v1
                assert bracketed, 'Reference root outside the candidate bracket; retain for a different decomposition.'
                reference_secant = t0 + (t1-t0) * (-20-v0) / (v1-v0)
                event = float(events[ordinal])
                trajectory = actual-reference_secant; interpolation = reference_secant-event
                total = actual-event
                assert abs(total-trajectory-interpolation) <= 1e-12
                decomposition.append(dict(scenario=case['key'], maximumStepMs=condition['maximumStepMs'],
                    ordinal=ordinal, nativeCrossingMs=actual, referenceCrossingMs=event,
                    bracketStartMs=t0, bracketEndMs=t1, referenceBracketVoltageMv=[v0, v1],
                    referenceFraction=(event-t0)/(t1-t0), referenceSecantMs=reference_secant,
                    trajectoryContributionMs=trajectory, secantContributionMs=interpolation,
                    totalErrorMs=total, referenceBracketed=bracketed))
        for (coarse, fine), stored in zip(zip(case['candidateContrasts'], case['candidateContrasts'][1:]), case['refinement']):
            checks = {k: coarse['error'][k] is not None and fine['error'][k] is not None and
                      fine['error'][k] <= max(criteria['refinementFloor'], criteria['refinementRatio'] * coarse['error'][k])
                      for k in ('voltageMv', 'gate', 'calciumMm', 'receptor', 'crossingMs')}
            assert checks == stored['checks'] and stored['passed'] == all(checks.values())
            counts['refinementContrasts'] += 1
            if not stored['passed']: failures.append(dict(scenario=case['key'], **stored))
        assert case['passed'] == all(r['passed'] for category in ('referenceContrasts', 'candidateContrasts', 'refinement') for r in case[category])
    assert counts == dict(referenceContrasts=18, candidateContrasts=18, refinementContrasts=12)
    assert len(failures) == 1 and failures[0]['scenario'] == 'distributed-excitation'
    assert [k for k,v in failures[0]['checks'].items() if not v] == ['crossingMs']
    assert report['quietHasNoCrossings'] and report['nontrivialExcitedCrossings']
    review.update(reviewedUtc=datetime.now(timezone.utc).isoformat(), operatorReportedCompletion=True,
        originalStatus='FAILED', originalBoundsChanged=False, savedCandidateAndOutputAuditsReproduced=True,
        savedNumericalContrastsReproduced=True, recordedAnalyticControls=6, recordedJacobianControls=36,
        contrastCounts=counts, failures=failures, candidateAudit=candidate['summary'], sourceOutputAudit=source_output,
        crossingDecomposition=decomposition, allAbsoluteAccuracyChecksPassed=True,
        originalRefinementGatePassed=False, coupledNumericalCriteriaPassed=False,
        rootSearchRerun=False, modelsExecutedDuringReview=False, testsExecutedDuringReview=False,
        presynapticNeuronalAuthorityQualified=False, physiologyQualified=False, avatarRecoveryAssessed=False)
    output.mkdir()
    (output / 'review.json').write_text(json.dumps(review, indent=2, allow_nan=False) + '\n', encoding='utf-8')
    print(json.dumps({k:v for k,v in review.items() if not isinstance(v, (dict, list))}, indent=2), flush=True)
    print(json.dumps([r for r in decomposition if r['scenario']=='distributed-excitation'], indent=2), flush=True)


if __name__ == '__main__':
    if not __debug__: raise RuntimeError('Assertions must remain enabled.')
    parser = argparse.ArgumentParser()
    parser.add_argument('--operator-reported-complete', required=True, action='store_true')
    parser.parse_args(); main()

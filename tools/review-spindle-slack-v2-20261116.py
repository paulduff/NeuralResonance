"""Audit reported v2 force records and backup bytes; no root searches or kinetics."""
import argparse
from collections import Counter
from datetime import datetime, timezone
import json
from pathlib import Path

from importlib.util import module_from_spec, spec_from_file_location

ROOT = Path(__file__).resolve().parents[1]
REVISION = 'e09d37db87b91a0e8df43f6db4fd66124be9228c'
BOUNDS = dict(forceAbsolutePa=1e-7, rootAbsoluteNm=1e-9,
              conservationAbsolute=1e-10, domainAbsolute=1e-8)


def module(name, path):
    spec = spec_from_file_location(name, path)
    value = module_from_spec(spec)
    spec.loader.exec_module(value)
    return value


def main():
    capture = ROOT / 'artifacts/spindle-slack-roots-20261116-ba3c0c'
    output = ROOT / 'artifacts/spindle-slack-v2-review-20261116'
    assert not output.exists(), 'Preserve existing reviews.'
    common = module('completed_capture_integrity', ROOT / 'tools/review-olive-and-motor-20261109.py')
    result = common.verify(capture, [], REVISION)
    archive = module('completed_capture_archive', ROOT / 'tools/review-spindle-root-and-causal-motor-20261113.py')
    result['verifiedGitFiles'] = archive.source_archive(common, capture, REVISION)
    environment = module('completed_spindle_environment', capture / 'source/tools/golgi-reference-resume.py').environment()
    assert environment == common.read(capture / 'preparation-environment.json')
    assert environment == common.read(capture / 'reference-preflight.json')['environment']
    assert common.read(capture / 'reference-postflight.json') == dict(environment=environment, unchanged=True, modelExecuted=False)
    protocol = common.read(capture / 'spindle-slack-input.json')
    assert protocol['version'] == 'spindle-active-force-landscape-v2'
    assert protocol['criteria'] == BOUNDS
    assert protocol['analyticControls'] == 8 and protocol['savedStates'] == 2812
    assert protocol['targetStressPa'] == 0.
    controls = common.read(capture / 'controls/controls.json')
    expected_controls = {'bag-passive', 'chain-passive', 'bag-passive-load', 'bag-passive-knot',
        'bag-central-bin-three-roots', 'bag-edge-bin-discontinuous-roots',
        'two-distinct-near-knot-roots', 'saved-step48-near-knot-regression'}
    assert controls['completed'] and controls['passed']
    assert len(controls['cases']) == 8 and {r['label'] for r in controls['cases']} == expected_controls
    assert not controls['physiologicalControls'] and not controls['kineticsExecuted']
    for control in controls['cases']:
        row = common.read(capture / 'controls' / (control['label'] + '.json'))
        assert control['passed'] and row['passed'] and row['result']['passed']
        roots = row['result']['roots']
        assert len(row['expectedRootsNm']) == len(roots)
        assert all(abs(a - b['lengthNm']) <= BOUNDS['rootAbsoluteNm']
                   for a, b in zip(row['expectedRootsNm'], roots))
        if control['label'] == 'two-distinct-near-knot-roots':
            close = [r for r in roots if abs(r['lengthNm'] - 1210.) < 1e-9]
            assert len(close) == 2 and close[0]['lengthNm'] < 1210. < close[1]['lengthNm']
            assert all(r['kind'] == 'open-interval' and r['nearKnotCertified'] for r in close)
        if control['label'] == 'saved-step48-near-knot-regression':
            assert len(roots) == 3 and all(r['kind'] == 'open-interval' for r in roots)
            assert any(r['lengthNm'] == 1200. for r in row['result']['nonzeroNearKnots'])
    analysis = common.read(capture / 'spindle-slack-analysis.json')
    assert analysis['completed'] and analysis['forceLandscapeNumericsPassed']
    assert analysis['statesChecked'] == 2812 and analysis['criteria'] == BOUNDS
    for key in ('originalBoundsChanged', 'nativeRerun', 'kineticsExecuted', 'slackTrajectoryAssessed',
                'movingTrajectoryAccuracyAssessed', 'rootSelected', 'afferentFiringAssessed',
                'physiologyQualified', 'avatarRecoveryAssessed'):
        assert analysis[key] is False
    expected_labels = {f'{fiber}-{name}-{step}us': step for fiber in ('bag', 'chain')
                       for name in ('stretch-shorten', 'imposed-below-slack') for step in (1000, 500, 250)}
    assert len(analysis['trajectories']) == 12
    assert {s['label'] for s in analysis['trajectories']} == set(expected_labels)
    assert {p.stem for p in (capture / 'landscapes').glob('*.jsonl')} == set(expected_labels)
    assert not list((capture / 'landscapes').glob('*failure*'))
    totals = Counter(); admissible_totals = Counter(); combinations = Counter()
    zero_states = []; multiple_examples = []; counts = Counter()
    for summary in analysis['trajectories']:
        label = summary['label']; step_us = expected_labels[label]
        rows = [json.loads(line) for line in (capture / 'landscapes' / (label + '.jsonl')).read_text().splitlines()]
        assert len(rows) == summary['capturedStates'] == summary['checkedStates'] == 1 + 100000 // step_us
        root_counts = Counter(len(r['roots']) for r in rows)
        admissible = Counter(sum(int(x['admissibleState']) for x in r['roots']) for r in rows)
        assert {str(k): v for k, v in root_counts.items()} == summary['rootCountHistogram']
        assert {str(k): v for k, v in admissible.items()} == summary['admissibleRootCountHistogram']
        assert sum(int(r['originalStressPa'] < 0) for r in rows) == summary['negativeOriginalStressStates']
        jumps = sum(int(any(b['signChangeAcrossJump'] for b in r['discontinuities'])) for r in rows)
        assert jumps == summary['statesWithDiscontinuousSignChange']
        counts['statesWithDiscontinuousSignChange'] += jumps
        assert summary['passed'] and all(r['passed'] and r['statePreserved'] for r in rows)
        for key, bound in (('maximumRootResidualPa', BOUNDS['forceAbsolutePa']),
                           ('maximumForceDifferencePa', BOUNDS['forceAbsolutePa']),
                           ('maximumRootDifferenceNm', BOUNDS['rootAbsoluteNm'])):
            assert max(r[key] for r in rows) == summary[key] <= bound
        for index, row in enumerate(rows):
            assert row['step'] == index and row['timeSeconds'] == index * (step_us / 1e6)
            assert not row['flatIntervals']
            roots = row['roots']; independent = row['independentRootsNm']
            assert len(roots) == len(independent)
            assert all(abs(r['lengthNm'] - v) <= BOUNDS['rootAbsoluteNm'] for r, v in zip(roots, independent))
            assert all(305. < r['lengthNm'] < 1935. and r['admissibleState']
                       and abs(r['residualPa']) <= BOUNDS['forceAbsolutePa']
                       and abs(r['independentResidualPa']) <= BOUNDS['forceAbsolutePa']
                       and abs(r['massResidual']) <= BOUNDS['conservationAbsolute']
                       and r['stateDomainViolation'] <= BOUNDS['domainAbsolute'] for r in roots)
            category = 'none' if not roots else 'unique' if len(roots) == 1 else 'multiple'
            combinations[('negative' if row['originalStressPa'] < 0 else 'nonnegative', category)] += 1
            counts['negativeOriginalStressStates'] += int(row['originalStressPa'] < 0)
            counts['nonzeroNearKnotProbes'] += len(row['nonzeroNearKnots'])
            counts['nearKnotCertifiedRoots'] += sum(int(r.get('nearKnotCertified', False)) for r in roots)
            evidence = dict(trajectory=label, step=index, timeSeconds=row['timeSeconds'],
                            originalLengthNm=row['originalLengthNm'], originalStressPa=row['originalStressPa'],
                            roots=roots, discontinuities=row['discontinuities'])
            if not roots:
                zero_states.append(evidence)
            elif len(roots) > 1 and (not multiple_examples or multiple_examples[-1]['trajectory'] != label):
                multiple_examples.append(evidence)
        totals.update(root_counts); admissible_totals.update(admissible)
    assert sum(totals.values()) == 2812
    result.update(reviewedUtc=datetime.now(timezone.utc).isoformat(), operatorReportedCompletion=True,
        analyticControlsPassed=8, savedStateQueries=2812, completedTrajectories=12,
        savedRecordsReconciled=True, forceLandscapeNumericsPassed=True,
        rootCountHistogram=dict(sorted(totals.items())), admissibleRootCountHistogram=dict(sorted(admissible_totals.items())),
        uniqueRootStates=totals[1], multipleRootStates=sum(v for k, v in totals.items() if k > 1),
        noRootStates=totals[0], stressAndRootCounts=[dict(stress=k[0], roots=k[1], states=v)
                                                   for k, v in sorted(combinations.items())],
        recordCounts=dict(counts), noRootEvidence=zero_states, multipleRootExamples=multiple_examples,
        maximumRootResidualPa=max(s['maximumRootResidualPa'] for s in analysis['trajectories']),
        maximumForceDifferencePa=max(s['maximumForceDifferencePa'] for s in analysis['trajectories']),
        maximumRootDifferenceNm=max(s['maximumRootDifferenceNm'] for s in analysis['trajectories']),
        originalBoundsChanged=False, rootSearchRerun=False, kineticsExecuted=False,
        rootSelected=False, slackTrajectoryAssessed=False, movingTrajectoryAccuracyAssessed=False,
        afferentFiringAssessed=False, physiologyQualified=False, avatarRecoveryAssessed=False,
        analysis=analysis)
    output.mkdir()
    (output / 'review.json').write_text(json.dumps(result, indent=2, allow_nan=False) + '\n', encoding='utf-8')
    print(json.dumps({k: v for k, v in result.items() if not isinstance(v, (dict, list))}, indent=2))
    print(json.dumps(result['stressAndRootCounts']))


if __name__ == '__main__':
    if not __debug__:
        raise RuntimeError('Assertions must remain enabled.')
    parser = argparse.ArgumentParser()
    parser.add_argument('--operator-reported-complete', required=True, action='store_true')
    parser.parse_args()
    main()

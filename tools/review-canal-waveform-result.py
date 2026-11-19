"""Review an operator-reported waveform capture using saved arithmetic only."""
import argparse
import ast
from datetime import datetime, timezone
from decimal import Decimal as D, localcontext
import importlib.util
import json
import math
from pathlib import Path
import re
import shutil
import sys

ROOT = Path(__file__).resolve().parents[1]
REVISION = 'deb4b145bab025864bc49f827e81084c5a0307a7'


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result)
    return result


def lines(path):
    with path.open(encoding='utf-8') as stream:
        return [json.loads(line) for line in stream]


def saved_comparison(package, frozen):
    spec = frozen.read(package / 'waveform-input.json')
    bundle = frozen.read(package / 'baseline/bundle-input.json')
    catalog = frozen.read(package / 'source/docs/reference-data/vestibular-ifediba-2007.json')
    analysis = frozen.read(package / 'analysis.json')
    dt = bundle['frame']['clock']['physicsStepMs']
    times = [dt * q['numerator'] / q['denominator'] for q in spec['queries']]
    operators = {p: lines(package / f'waveform/operators-{p}.jsonl') for p in (60, 90)}
    samples = {p: lines(package / f'waveform/samples-{p}.jsonl') for p in (60, 90, 'scipy')}
    angular = {p: [r for r in frozen.read(package / f'baseline/angular-{p}.json')['rows'] if r['sensorId'] == 'centre'] for p in (60, 90)}
    assert len(times) == 53 and all(len(v) == 53 for v in operators.values())
    assert all(len(v) == 1060 for v in samples.values())
    # Reconstruct only the declared scalar geometry and comparison denominators;
    # no matrix exponential, derivative, ODE solver or native model is called.
    gains = {p: frozen.decimal_gain(bundle, catalog, p) for p in (60, 90)}
    projection = frozen.BundleProjection.from_spec(bundle, catalog)
    maxima = dict(state=0., bundle=0., decimalState=0., decimalBundle=0., matrix=0., geometry=0.)
    worst = {key: None for key in maxima}

    def retain(key, value, location):
        assert math.isfinite(value)
        if value > maxima[key]:
            maxima[key], worst[key] = value, location

    def ratio(a, b, absolute, relative):
        return abs(a - b) / (absolute + relative * max(abs(a), abs(b)))

    with localcontext() as context:
        context.prec = 90
        for query, (a, b) in enumerate(zip(operators[60], operators[90], strict=True)):
            for p, row in ((60, a), (90, b)):
                assert row['query'] == query and row['localMs'] == times[query]
                with localcontext() as child:
                    child.prec = p
                    assert D(row['seconds']) == D.from_float(times[query]) / 1000
                assert row['metadata']['precision'] == p and len(row['matrix']) == 15
                assert all(len(r) == 15 and all(D(v).is_finite() for v in r) for r in row['matrix'])
            for i, (left, right) in enumerate(zip(a['matrix'], b['matrix'], strict=True)):
                for j, (x, y) in enumerate(zip(left, right, strict=True)):
                    retain('matrix', float(ratio(D(x), D(y), D(spec['matrixPrecisionAbsolute']),
                        D(spec['matrixPrecisionRelative']))), dict(query=query, row=i, column=j))
        retain('geometry', float(abs(D.from_float(projection.gain_nm_per_m3) / gains[90] - 1)
            / D.from_float(spec['geometryArithmeticRelative'])), dict(projection='bundleGain'))
    bounds = spec['referenceBounds']
    count = 0
    intervals = []
    for condition in spec['conditions']:
        previous = None
        for step in range(1, 11):
            initial = {p: samples[p][count]['state'] for p in samples}
            if previous is None:
                assert all(not any(map(D, row)) for row in initial.values())
            else:
                for p in samples:
                    assert list(map(D, initial[p])) == list(map(D, previous[p]))
            for query, ms in enumerate(times):
                location = dict(condition=condition, heldPhysicalStep=step, query=query, localMs=ms)
                rows = {p: samples[p][count] for p in samples}
                for row in rows.values():
                    assert {k: row[k] for k in location} == location and len(row['state']) == 12
                    assert all(D(v).is_finite() for v in row['state']) and D(row['bundleNm']).is_finite()
                for p in (60, 90):
                    with localcontext() as context:
                        context.prec = p
                        point = list(map(D, initial[p])) + ([D(v) for v in angular[p][step]['projection']['prime']]
                            if condition == 'recorded-head' else [D(0)] * 3)
                        # Check each recorded point from its recorded interval
                        # start and saved operator, not a newly integrated state.
                        for coefficients, value in zip(operators[p][query]['matrix'][:12], rows[p]['state'], strict=True):
                            expected = sum((D(a) * b for a, b in zip(coefficients, point, strict=True) if D(a) and b), D(0))
                            assert expected == D(value), (p, location)
                        assert gains[p] * D(rows[p]['state'][3 + projection.canal]) == D(rows[p]['bundleNm'])
                for column, (a, b, c) in enumerate(zip(rows['scipy']['state'], rows[60]['state'], rows[90]['state'], strict=True)):
                    absolute = bounds['volumeAbsolute'] if column < 6 else bounds['flowAbsolute']
                    retain('state', ratio(a, float(c), absolute, bounds['relative']), dict(**location, column=column))
                    with localcontext() as context:
                        context.prec = 90
                        retain('decimalState', float(ratio(D(b), D(c), D(spec['matrixPrecisionAbsolute']),
                            D(spec['matrixPrecisionRelative']))), dict(**location, column=column))
                a, b, c = (rows[p]['bundleNm'] for p in ('scipy', 60, 90))
                allowance = abs(float(gains[90])) * bounds['volumeAbsolute'] + (
                    bounds['relative'] + spec['geometryArithmeticRelative']) * max(abs(a), abs(float(c)))
                retain('bundle', abs(a - float(c)) / allowance, location)
                with localcontext() as context:
                    context.prec = 90
                    retain('decimalBundle', float(ratio(D(b), D(c), D(spec['matrixPrecisionAbsolute']), D(spec['matrixPrecisionRelative']))), location)
                if condition == 'stationary-head':
                    assert all(not any(map(D, row['state'])) and D(row['bundleNm']) == 0 for row in rows.values())
                previous = {p: row['state'] for p, row in rows.items()}
                count += 1
            intervals.append(dict(condition=condition, heldPhysicalStep=step, queries=53))
    assert count == analysis['queries'] == 1060
    assert maxima == analysis['maximumErrorFractions'] and worst == analysis['worstLocations'] and intervals == analysis['intervals']
    assert analysis['completed'] and analysis['waveformCriteriaPassed'] and all(v <= 1 for v in maxima.values())
    return analysis


def review(package, reported):
    assert reported, 'Await the operator final footer.'
    assert package == Path('D:/DNNE-build-work/canal-waveform-20261119-2dd76a')
    output = ROOT / 'artifacts' / (package.name + '-review')
    assert not output.exists()
    common = module('waveform_files', ROOT / 'tools/review-olive-and-motor-20261109.py')
    archive = module('waveform_git', ROOT / 'tools/review-spindle-root-and-causal-motor-20261113.py')
    result = common.verify(package, [], REVISION, progress_every=1000)
    result['verifiedGitFiles'] = archive.source_archive(common, package, REVISION)
    status = common.read(package / 'window-status.json')
    for flag in ('AnalyticControlsPassed', 'IndependentWaveformCriteriaPassed', 'SourceAndDependenciesPreserved'):
        assert status[flag] is True
    for flag in ('CoupledSensoryAccuracyQualified', 'NeuronalModelsExecuted', 'LiveBrainAccessed', 'OldSavesLoaded',
                 'NativeBodyExecuted', 'PendingCaptureAccessed', 'PhysiologyQualified', 'AvatarRecoveryAssessed', 'FullUnitSuitePassed'):
        assert status[flag] is False
    checks = common.read(package / 'analytic-controls/input-controls.json')
    assert checks == dict(expected=15, executed=15, successful=True, failures=[], errors=[], skipped=[])
    code = ast.parse((package / 'source/tools/check_canal_afferent_reference_input.py').read_text(encoding='utf-8'))
    expected = sorted(n.name for n in ast.walk(code) if isinstance(n, ast.FunctionDef) and n.name.startswith('test_'))
    log = (package / 'controls.log').read_text(encoding='utf-8-sig')
    actual = re.findall(r'^(test_\w+) \(check_canal_afferent_reference_input\.ReferenceInputControls\.\1\) \.\.\. ok$', log, re.M)
    assert len(expected) == 15 and sorted(actual) == expected
    controls = common.read(package / 'analytic-controls/exponential/controls.json')
    assert controls['completed'] and controls['expected'] == controls['passed'] == len(controls['cases']) == 6
    assert [c['key'] for c in controls['cases']] == ['zero', 'diagonal', 'nilpotent', 'damped-jordan', 'stiff-decay', 'constant-forcing']
    assert all(c['passed'] and all(D(v) <= D('1e-45') for v in c['absoluteErrors']) for c in controls['cases'])
    sys.path.insert(0, str(package / 'source/tools'))
    try:
        gate = module('waveform_frozen_gate', package / 'source/tools/canal_waveform_gate.py')
        gate.preflight(package)
        frozen = module('waveform_frozen_arithmetic', package / 'source/tools/canal_waveform_study.py')
        analysis = saved_comparison(package, frozen)
    finally:
        sys.path.pop(0)
    result.update(reviewedUtc=datetime.now(timezone.utc).isoformat(), operatorReportedCompletion=True,
        passedAnalyticControls=21, exactInputControlInventory=True, verifiedQueries=1060, numericalCriteriaPassed=True,
        savedComparisonExactlyReproduced=True, savedReferenceMatrixProductsVerified=True,
        analysisSha256=common.digest(package / 'analysis.json'), modelsExecutedDuringReview=False,
        testsExecutedDuringReview=False, referenceSolvesExecutedDuringReview=False, pendingCaptureAccessed=False,
        connectedSensoryAccuracyQualified=False, physiologicalCalibrationQualified=False, avatarBalanceAssessed=False,
        archiveCopiesShareDrive=True)
    output.mkdir()
    for name, value in (('review.json', result), ('analysis.json', analysis)):
        (output / name).write_text(json.dumps(value, indent=2, allow_nan=False) + '\n', encoding='utf-8')
    shutil.copyfile(__file__, output / Path(__file__).name)
    print(json.dumps(dict(output=str(output), verifiedFiles=result['verifiedFilesPerCopy'], passedControls=21, verifiedQueries=1060), indent=2), flush=True)


if __name__ == '__main__':
    if not __debug__:
        raise RuntimeError('Assertions must remain enabled.')
    parser = argparse.ArgumentParser()
    parser.add_argument('package', type=Path)
    parser.add_argument('--operator-reported-complete', action='store_true')
    args = parser.parse_args()
    review(args.package.resolve(), args.operator_reported_complete)

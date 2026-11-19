"""Independent timed-input qualification; execute only in its visible window."""
from contextlib import ExitStack
from decimal import Decimal as D, localcontext
import json
import math
import traceback
import unittest

import numpy as np

from canal_afferent_reference_input import BundleProjection, CanalInterval
from check_canal_afferent_reference_input import ReferenceInputControls
from coupled_canal_reference import read, write, generator_decimal, exponential, apply, controls, source_matrices


def analytic_controls(package):
    output = package / 'analytic-controls'
    output.mkdir()
    suite = unittest.defaultTestLoader.loadTestsFromTestCase(ReferenceInputControls)
    assert suite.countTestCases() == 15
    result = unittest.TextTestRunner(verbosity=2).run(suite)
    write(output / 'input-controls.json', dict(expected=15, executed=result.testsRun,
        successful=result.wasSuccessful(), failures=[dict(test=str(t), trace=s) for t, s in result.failures],
        errors=[dict(test=str(t), trace=s) for t, s in result.errors], skipped=result.skipped))
    # These separately derived Decimal controls also have closed-form answers.
    controls(output / 'exponential')
    if not result.wasSuccessful() or result.testsRun != 15 or result.skipped:
        raise RuntimeError('Reference-input analytic controls failed; preserve the evidence.')


def decimal_gain(bundle, catalog, precision):
    with localcontext() as context:
        context.prec = precision
        g, a = bundle['geometry'], bundle['attachment']
        canal = g['canal']
        stiffness = source_matrices(catalog)[2][3 + canal][3 + canal]
        # Match the declared binary64 material/placement constants and pi; the
        # independent source matrices continue to use their printed decimals.
        shear = D.from_float(g['shearModulusPascal'])
        aspect = D.from_float(g['thicknessOverRadius'])
        pi = D.from_float(math.pi)
        radius = (8 * shear * aspect / (pi * stiffness)) ** (D(1) / 3)
        x = D.from_float(a['rootXOverRadius'])
        root = -(1 - x * x).sqrt()
        tip = root + D.from_float(a['bundleLengthMeters']) / radius
        return D(a['excitatoryPolarity']) * 2 * (1 - x * x - tip * tip) / (pi * radius * radius) * D(10)**9


def fraction(a, b, absolute, relative):
    return abs(a - b) / (absolute + relative * max(abs(a), abs(b)))


def run(package):
    spec = read(package / 'waveform-input.json')
    bundle = read(package / 'baseline/bundle-input.json')
    catalog = read(package / 'source/docs/reference-data/vestibular-ifediba-2007.json')
    output = package / 'waveform'
    output.mkdir()
    dt = bundle['frame']['clock']['physicsStepMs']
    times = [dt * r['numerator'] / r['denominator'] for r in spec['queries']]
    assert len(times) == 53 and times[0] == 0 and times[-1] == dt
    assert all(a < b for a, b in zip(times, times[1:]))
    projection = BundleProjection.from_spec(bundle, catalog)
    angular = {p: [r for r in read(package / f'baseline/angular-{p}.json')['rows']
                    if r['sensorId'] == 'centre'] for p in (60, 90)}
    for rows in angular.values():
        assert [r['step'] for r in rows] == list(range(201))
        assert all(rows[i]['fromStep'] == i - 1 for i in range(1, 11))
    operators, gains = {}, {}
    for precision in (60, 90):
        operators[precision] = []
        with localcontext() as context:
            context.prec = precision
            generator = generator_decimal(catalog)
            gains[precision] = decimal_gain(bundle, catalog, precision)
            with (output / f'operators-{precision}.jsonl').open('x', encoding='utf-8', buffering=1) as stream:
                for index, ms in enumerate(times):
                    seconds = D.from_float(ms) / 1000
                    matrix, metadata = exponential([[v * seconds for v in row] for row in generator], precision)
                    operators[precision].append(matrix)
                    stream.write(json.dumps(dict(query=index, localMs=ms, seconds=str(seconds),
                        matrix=[[str(v) for v in row] for row in matrix], metadata=metadata), allow_nan=False) + '\n')
                    if index % 8 == 0:
                        print(f'INDEPENDENT INPUT OPERATOR precision={precision} query={index + 1}/53', flush=True)
    bounds = spec['referenceBounds']
    maxima = dict(state=0., bundle=0., decimalState=0., decimalBundle=0., matrix=0., geometry=0.)
    worst = {key: None for key in maxima}
    count = 0

    def record_error(key, value, location):
        if not math.isfinite(value):
            raise ArithmeticError('Nonfinite comparison: ' + key)
        if value > maxima[key]:
            maxima[key] = value
            worst[key] = location

    with localcontext() as context:
        context.prec = 90
        absolute = D(spec['matrixPrecisionAbsolute'])
        relative = D(spec['matrixPrecisionRelative'])
        for index, (a, b) in enumerate(zip(operators[60], operators[90], strict=True)):
            for i, (row, other) in enumerate(zip(a, b, strict=True)):
                for j, (x, y) in enumerate(zip(row, other, strict=True)):
                    record_error('matrix', float(fraction(x, y, absolute, relative)), dict(query=index, row=i, column=j))
        record_error('geometry', float(abs(D.from_float(projection.gain_nm_per_m3) / gains[90] - 1)
                                      / D.from_float(spec['geometryArithmeticRelative'])), dict(projection='bundleGain'))
    records = []
    try:
        with ExitStack() as stack:
            streams = {p: stack.enter_context((output / f'samples-{p}.jsonl').open('x', encoding='utf-8', buffering=1))
                       for p in ('scipy', 60, 90)}
            for condition in spec['conditions']:
                points = {p: [D(0)] * 15 for p in (60, 90)}
                interval = None
                for held_step in range(1, 11):
                    for p in (60, 90):
                        points[p][12:] = [D(v) for v in angular[p][held_step]['projection']['prime']] if condition == 'recorded-head' else [D(0)] * 3
                    acceleration = [float(v) for v in points[90][12:]]
                    interval = (CanalInterval.from_source(catalog, projection, np.zeros(12), acceleration, dt)
                                if interval is None else interval.continue_with(acceleration))
                    for query, ms in enumerate(times):
                        location = dict(condition=condition, heldPhysicalStep=held_step, query=query, localMs=ms)
                        reference = {p: apply(operators[p][query], points[p], p) for p in (60, 90)}
                        native = interval.point_at_ms(ms)
                        bundle_nm = interval.at_ms(ms)
                        reference_bundle = {}
                        for p in (60, 90):
                            with localcontext() as context:
                                context.prec = p
                                reference_bundle[p] = gains[p] * reference[p][3 + projection.canal]
                            streams[p].write(json.dumps(dict(**location, state=[str(v) for v in reference[p][:12]],
                                bundleNm=str(reference_bundle[p])), allow_nan=False) + '\n')
                        streams['scipy'].write(json.dumps(dict(**location, state=native, bundleNm=bundle_nm), allow_nan=False) + '\n')
                        for column, (a, b, c) in enumerate(zip(native, reference[60][:12], reference[90][:12], strict=True)):
                            absolute = bounds['volumeAbsolute'] if column < 6 else bounds['flowAbsolute']
                            record_error('state', fraction(a, float(c), absolute, bounds['relative']), dict(**location, column=column))
                            with localcontext() as context:
                                context.prec = 90
                                record_error('decimalState', float(fraction(b, c, D(spec['matrixPrecisionAbsolute']),
                                    D(spec['matrixPrecisionRelative']))), dict(**location, column=column))
                        # Propagate unchanged volume bounds through the declared
                        # linear spatial map; separately allow only its existing
                        # 1e-12 relative arithmetic allowance, not a fitted gain.
                        allowance = abs(float(gains[90])) * bounds['volumeAbsolute'] + (
                            bounds['relative'] + spec['geometryArithmeticRelative']) * max(abs(bundle_nm), abs(float(reference_bundle[90])))
                        record_error('bundle', abs(bundle_nm - float(reference_bundle[90])) / allowance, location)
                        with localcontext() as context:
                            context.prec = 90
                            record_error('decimalBundle', float(fraction(reference_bundle[60], reference_bundle[90],
                                D(spec['matrixPrecisionAbsolute']), D(spec['matrixPrecisionRelative']))), location)
                        if condition == 'stationary-head':
                            assert not any(native) and bundle_nm == 0 and not any(reference[90][:12])
                        count += 1
                    # Retain the complete endpoint and carry it into the next
                    # interval; no reinitialisation at the next head sample.
                    for p in (60, 90):
                        points[p] = reference[p]
                    records.append(dict(condition=condition, heldPhysicalStep=held_step, queries=53))
                    print(f'INDEPENDENT INPUT {condition} physical interval {held_step}/10', flush=True)
        passed = count == spec['expectedQueries'] and all(v <= 1 for v in maxima.values())
        write(package / 'analysis.json', dict(completed=True, waveformCriteriaPassed=passed, queries=count,
            maximumErrorFractions=maxima, worstLocations=worst, intervals=records,
            originalMechanicalAndReferenceBoundsPreserved=True, pendingNativeCaptureAccessed=False,
            neuronalModelsExecuted=False, coupledSensoryAccuracyQualified=False,
            anatomicalCalibrationQualified=False, afferentFiringQualified=False, avatarBalanceAssessed=False))
        if not passed:
            raise RuntimeError('Independent waveform criteria failed; retain all original bounds and samples.')
    except Exception:
        write(output / 'failure.json', dict(error=traceback.format_exc(), recordedQueries=count, completedIntervals=records))
        raise

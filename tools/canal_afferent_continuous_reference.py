"""Full continuous sensory references, for a later frozen visible study only.

This module neither reads nor monitors a pending native capture. The frozen
reference gate must verify operator-reported completion and the preserved
prerequisite studies before invoking this worker in its visible window.
"""
from decimal import Decimal as D, localcontext
import json
import math
from pathlib import Path
import traceback

import numpy as np
from scipy.integrate import Radau, BDF

from afferent_trajectory_reference import Equation, precision_witness, save
from afferent_trajectory_protocol import group
from canal_afferent_reference_input import BundleProjection, CanalInterval, DrivenSensoryEquation
from canal_waveform_study import decimal_gain
from coupled_canal_reference import read, generator_decimal, exponential, apply


def archive(path, **arrays):
    with path.open('xb') as stream:
        np.savez_compressed(stream, **arrays)


def integrate_interval(drive, initial, duration_ms, grid_count, setting, atol, maximum_step_ms,
                       validate, output):
    """Step the independent solver explicitly so an exception retains its prefix.

    Local time starts at zero, but *all* caller-supplied state/flux variables are
    carried unchanged. Every requested grid point is populated only after its
    enclosing reference step is accepted. No sample beyond that prefix is saved.
    """
    if type(grid_count) is not int or grid_count < 1 or grid_count > 2**24 or grid_count & (grid_count - 1):
        raise ValueError('A bounded dyadic source grid is required.')
    if not math.isfinite(duration_ms) or duration_ms <= 0:
        raise ValueError('Finite positive interval duration required.')
    if setting['method'] not in ('Radau', 'BDF'):
        raise ValueError('Only the declared independent methods are supported.')
    y = np.array(initial, dtype=np.float64, copy=True)
    if y.ndim != 1 or not len(y) or not np.isfinite(y).all():
        raise ValueError('A finite, complete initial state is required.')
    grid = np.arange(grid_count + 1, dtype=np.int64)
    times = grid * (duration_ms / grid_count)
    if times[-1] != duration_ms or not (np.diff(times) > 0).all():
        raise ValueError('Source grid lost time resolution.')
    output = Path(output)
    output.mkdir()
    values = np.empty((len(y), len(times)), dtype=np.float64)
    values[:, 0] = y
    populated = 1
    node_times, nodes = [0.], [y.copy()]
    solver = None
    completed = False
    error = None
    try:
        validate(y[:, None])
        constructor = Radau if setting['method'] == 'Radau' else BDF
        solver = constructor(drive.rates, 0., y, duration_ms, rtol=setting['rtol'], atol=atol,
                             jac=drive.jacobian, max_step=maximum_step_ms)
        while solver.status == 'running':
            previous = float(solver.t)
            message = solver.step()
            if solver.status == 'failed':
                raise ArithmeticError('Independent solver failed: ' + str(message))
            if not previous < solver.t <= duration_ms:
                raise ArithmeticError('Independent solver did not advance within the physical interval.')
            node_times.append(float(solver.t))
            nodes.append(solver.y.copy())
            validate(solver.y[:, None])
            right = int(np.searchsorted(times, solver.t, side='right'))
            if right > populated:
                dense = solver.dense_output()(times[populated:right])
                # Preserve returned values even if a subsequent physical-domain
                # check fails. The failure status prevents qualification.
                values[:, populated:right] = dense
                populated = right
                validate(dense)
        if solver.status != 'finished' or solver.t != duration_ms or populated != len(times):
            raise ArithmeticError('Independent trajectory or its complete source grid is missing.')
        completed = True
        return solver.y.copy(), np.array(node_times), np.column_stack(nodes), times, values
    except Exception:
        error = traceback.format_exc()
        raise
    finally:
        archive(output / 'accepted-nodes.npz', localMs=np.array(node_times), values=np.column_stack(nodes))
        archive(output / 'samples.npz', localTicks=grid[:populated], localMs=times[:populated], values=values[:, :populated])
        save(output / 'integration.json', dict(completed=completed, error=error, method=setting['method'],
            durationMs=duration_ms, requestedGridCount=grid_count, populatedSamples=populated,
            acceptedNodes=len(nodes), completedThroughLocalMs=node_times[-1],
            nfev=None if solver is None else solver.nfev, njev=None if solver is None else solver.njev,
            nlu=None if solver is None else solver.nlu, stateAndLedgersCarriedContinuously=True,
            unusedSampleTailSaved=False, referenceTrajectoryCriteriaPassed=False))


class RecordedInput:
    """Retain every distinct physical query made by the independent solver."""

    def __init__(self, interval, stream):
        self.interval, self.stream = interval, stream
        self.seen = set()

    def at_ms(self, local_ms):
        value = self.interval.at_ms(local_ms)
        if local_ms not in self.seen:
            self.stream.write(json.dumps(dict(localMs=float(local_ms), bundleNm=value,
                mechanicalState=self.interval.point_at_ms(local_ms)), allow_nan=False) + '\n')
            self.seen.add(local_ms)
        return value


def mechanical_witness(catalog, bundle, prefixes, angular, interval, local_ms):
    references = {}
    metadata = {}
    for precision in (60, 90):
        with localcontext() as context:
            context.prec = precision
            generator = generator_decimal(catalog)
            seconds = D.from_float(local_ms) / 1000
            matrix, meta = exponential([[v * seconds for v in row] for row in generator], precision)
            point = apply(matrix, prefixes[precision] + angular[precision], precision)
            displacement = decimal_gain(bundle, catalog, precision) * point[3 + bundle['geometry']['canal']]
            references[precision] = dict(state=[str(v) for v in point[:12]], bundleNm=str(displacement))
            metadata[precision] = meta
    values = interval.point_at_ms(local_ms)
    nm = interval.at_ms(local_ms)
    limits = bundle['referenceBounds']
    errors = [abs(v - float(r)) / ((limits['volumeAbsolute'] if i < 6 else limits['flowAbsolute'])
        + limits['relative'] * max(abs(v), abs(float(r)))) for i, (v, r) in enumerate(zip(values, references[90]['state'], strict=True))]
    with localcontext() as context:
        context.prec = 90
        precision_error = max(abs(D(a) - D(b)) / (D(bundle['matrixPrecisionAbsolute']) + D(bundle['matrixPrecisionRelative']) * max(abs(D(a)), abs(D(b))))
            for a, b in zip(references[60]['state'] + [references[60]['bundleNm']], references[90]['state'] + [references[90]['bundleNm']], strict=True))
    gain = abs(float(decimal_gain(bundle, catalog, 90)))
    projected_bound = gain * limits['volumeAbsolute'] + (limits['relative'] + bundle['geometryArithmeticRelative']) * max(abs(nm), abs(float(references[90]['bundleNm'])))
    bundle_error = abs(nm - float(references[90]['bundleNm'])) / projected_bound
    return dict(localMs=local_ms, scipyState=list(values), scipyBundleNm=nm, references=references, metadata=metadata,
        maximumStateFraction=max(errors), bundleFraction=bundle_error, decimalPrecisionFraction=str(precision_error),
        # A failed NumPy comparison can short-circuit ``and`` to np.bool_.
        # Convert explicitly so the rejected witness is saved before the
        # caller raises the numerical failure, instead of a JSON type error.
        passed=bool(max(errors) <= 1 and bundle_error <= 1 and precision_error <= 1))


def run_case(package, protocol, condition, setting, source, output):
    output.mkdir()
    connected = protocol['connected']
    assert condition in ('recorded-head', 'stationary-head')
    bundle = connected['bundle']
    catalog = read(package / 'source/docs/reference-data/vestibular-ifediba-2007.json')
    projection = BundleProjection.from_spec(bundle, catalog)
    equation = Equation(connected['fixture'], source, protocol['referenceDecimalPrecision'])
    assert source['activationOutsidePolicy'] == 'endpoint-hold'
    initial = equation.initial.copy()
    y = initial.copy()
    dt = bundle['frame']['clock']['physicsStepMs']
    grid = protocol['sourceStepsPerInterval']
    mechanical_ticks = 65536 * connected['mechanicalRefinement']
    assert grid == 65536 and mechanical_ticks % grid == 0
    assert bundle['frame']['clock']['initialPhysicsStep'] == 0
    source_epoch = bundle['frame']['clock']['neuralEpochMs'] + dt
    angular_rows = {p: [r for r in read(package / f'baseline/angular-{p}.json')['rows'] if r['sensorId'] == 'centre'] for p in (60, 90)}
    assert all([r['step'] for r in rows] == list(range(201)) for rows in angular_rows.values())
    prefixes = {p: [D(0)] * 12 for p in (60, 90)}
    interval = None
    completed = []
    observed_column = equation.state_labels.index('axon/' + connected['observationNode'] + '/potentialMv')
    atol = np.array([protocol['referenceAtol'][group(n)] * setting['atolScale'] for n in equation.labels])
    save(output / 'input.json', dict(condition=condition, setting=setting, protocol=protocol, source=source,
        labels=equation.labels, initial=initial.tolist(), sourceEpochMs=source_epoch,
        referenceNeuralStepMs=dt / grid, projectedCellId=projection.hair_cell_id,
        derivative='preserved independent Decimal joint derivative', explicitJacobian=True,
        ledgerJacobianColumnsExactlyZero=True, nativeStagesOrTrajectoryInputsUsed=False))
    try:
        for index in range(protocol['physicalIntervals']):
            physical_step = index + 1
            angular = {p: [D(v) for v in angular_rows[p][physical_step]['projection']['prime']]
                       if condition == 'recorded-head' else [D(0)] * 3 for p in (60, 90)}
            acceleration = [float(v) for v in angular[90]]
            interval = (CanalInterval.from_source(catalog, projection, np.zeros(12), acceleration, dt)
                        if interval is None else interval.continue_with(acceleration))
            folder = output / f'interval-{physical_step:02}'
            with (output / f'input-queries-{physical_step:02}.jsonl').open('x', encoding='utf-8', buffering=1) as stream:
                drive = DrivenSensoryEquation(equation, RecordedInput(interval, stream))
                y, node_times, nodes, sample_times, samples = integrate_interval(drive, y, dt, grid, setting, atol,
                    protocol['referenceMaximumStepMs'], equation.validate_samples, folder)
            ticks = index * grid + np.arange(grid + 1, dtype=np.int64)
            archive(folder / 'clock.npz', sourceTicks=ticks, localMs=sample_times,
                sourceNeuralMs=source_epoch + ticks * (dt / grid),
                physicalNeuralMs=bundle['frame']['clock']['neuralEpochMs'] +
                    (mechanical_ticks + ticks * (mechanical_ticks // grid)) * (dt / mechanical_ticks))
            # Store only declared witnesses; these supplement, not replace, the
            # full-grid Radau/BDF and conservation comparison still required.
            max_index = int(np.argmax(abs(nodes[observed_column])))
            candidates = [(0., nodes[:, 0]), (float(node_times[1]), nodes[:, 1]),
                          (dt / 2, samples[:, grid // 2]), (float(node_times[max_index]), nodes[:, max_index]),
                          (dt, nodes[:, -1])]
            witnesses = []
            for local_ms, state in sorted({t: state for t, state in candidates}.items()):
                mechanical = mechanical_witness(catalog, bundle, prefixes, angular, interval, local_ms)
                forcing = {'bundleDisplacementNanometers': interval.at_ms(local_ms)}
                neuronal = precision_witness(equation, state, forcing, 0, protocol)
                neuronal.pop('tick')  # Arbitrary solver nodes are not grid ticks.
                neuronal.update(localMs=local_ms, heldPhysicalStep=physical_step, forcing=forcing)
                witnesses.append(dict(mechanical=mechanical, neuronal=neuronal))
            save(folder / 'precision.json', witnesses)
            assert all(w['mechanical']['passed'] and w['neuronal']['precision']['passed'] and w['neuronal']['workingPrecision']['passed'] for w in witnesses)
            last = next(w for w in witnesses if w['mechanical']['localMs'] == dt)
            for p in (60, 90):
                prefixes[p] = [D(v) for v in last['mechanical']['references'][p]['state']]
            completed.append(dict(heldPhysicalStep=physical_step, storedSamples=grid + 1, acceptedNodes=len(node_times),
                inputQueries=len(drive.bundle_input.seen), precisionWitnesses=len(witnesses)))
            print(f'CONTINUOUS SENSORY REFERENCE {condition} {setting["key"]} interval {physical_step}/10', flush=True)
        save(output / 'results.json', dict(completed=True, intervals=completed,
            uniqueSamples=grid * len(completed) + 1, storedSamples=(grid + 1) * len(completed),
            allAcceptedNodesAndGridSamplesPhysicallyAdmissible=True, candidateTrajectoryUsedForIntegration=False,
            coupledAccuracyQualified=False, physiologicalCalibrationQualified=False, avatarBalanceAssessed=False))
    except Exception:
        save(output / 'failure.json', dict(error=traceback.format_exc(), completedIntervals=completed,
            lastIndependentInput=equation.last_input, originalInitialState=initial.tolist()))
        raise


def run(package):
    package = Path(package)
    reviewed = read(package / 'reviewed-connected.json')
    assert reviewed['operatorReportedCompletion'] and reviewed['connectedAccountingPassed'], \
        'Native capture must be reported and its accounting reviewed before execution.'
    protocol = read(package / 'continuous-reference-input.json')
    source = read(package / 'source-decisions.json')
    assert protocol['version'] == 'continuous-canal-joint-afferent-reference-v1'
    root = package / 'continuous-reference'
    root.mkdir()
    failures = []
    for condition in protocol['conditions']:
        for setting in protocol['referenceSettings']:
            name = condition + '-' + setting['key']
            try:
                run_case(package, protocol, condition, setting, source, root / name)
            except Exception:
                failures.append(name)
                print('REFERENCE FAILED ' + name + '; accepted prefix preserved.', flush=True)
    save(root / 'results.json', dict(completed=not failures, attempted=protocol['expectedReferences'], failedRuns=failures,
        comparisonsExecuted=False, coupledAccuracyQualified=False, physiologicalCalibrationQualified=False, avatarBalanceAssessed=False))
    if failures:
        raise RuntimeError('Continuous sensory references incomplete; retain prefixes and original criteria.')

"""Prospective saved-data comparison; no derivative, ODE or live-run access.

There is deliberately no CLI. A future frozen gate must first verify the
operator-reported native capture, its complete accounting review, source/runtime
identities and prerequisite copies. Only that gate may call ``analyse``.

The native start-held input is compared with independently continuous mechanics.
Thus errors include mechanical interpolation and forcing discretization. They
are not an identical-input, neuronal-solver-only measurement.
"""
from contextlib import ExitStack
from decimal import Decimal as D, localcontext
import gzip
import hashlib
import json
from pathlib import Path
import traceback

import numpy as np

from afferent_trajectory_reference import Equation, save
from afferent_trajectory_protocol import group
from afferent_trajectory_analysis import balance, RESIDUALS
from canal_afferent_reference_input import BundleProjection
from canal_waveform_study import decimal_gain
from cleft_rest_analysis import comparison as record_comparison
from coupled_canal_reference import read


def require(condition, message):
    if not condition:
        raise ValueError(message)


def same(actual, expected, message):
    require(np.array_equal(actual, expected), message)


def arrays(path, keys):
    with np.load(path, allow_pickle=False) as saved:
        require(set(saved.files) == set(keys), 'Unexpected arrays: ' + str(path))
        values = {key: saved[key].copy() for key in keys}
    for key, value in values.items():
        require(value.dtype == (np.dtype('int64') if key.endswith('Ticks') else np.dtype('float64')),
                'Unexpected array type: ' + str(path) + '/' + key)
        require(np.isfinite(value).all(), 'Nonfinite array: ' + str(path) + '/' + key)
    return values


class ErrorEnvelope:
    """Accumulate original bounds, including the earliest and every later sample."""

    def __init__(self, names, bounds):
        self.names = list(names)
        self.groups = {key: [i for i, name in enumerate(names) if group(name) == key]
                       for key in bounds['absolute']}
        require(bool(names) and all(self.groups.values()), 'Missing state/flux group.')
        self.absolute = np.array([bounds['absolute'][group(name)] for name in names])
        self.relative = bounds['relative']
        require(np.isfinite(self.absolute).all() and (self.absolute > 0).all()
                and np.isfinite(self.relative) and self.relative >= 0, 'Invalid original bounds.')
        self.samples = 0
        self.maximum = 0.
        self.worst = None
        self.absolute_maxima = {key: 0. for key in self.groups}
        self.last_tick = None
        self.grid_hash = hashlib.sha256()

    def add(self, actual, expected, ticks):
        actual, expected, ticks = np.asarray(actual), np.asarray(expected), np.asarray(ticks)
        require(actual.shape == expected.shape == (len(ticks), len(self.names)) and len(ticks) > 0,
                'Incomplete or mismatched comparison grid.')
        require(ticks.dtype.kind in 'iu' and ticks[0] >= 0 and (ticks <= 2**53 - 1).all()
                and (ticks[1:] > ticks[:-1]).all(),
                'Comparison needs ordered unique integer ticks.')
        require(self.last_tick is None or ticks[0] > self.last_tick, 'Duplicate or reversed comparison boundary.')
        require(np.isfinite(actual).all() and np.isfinite(expected).all(), 'Nonfinite compared state.')
        delta = abs(actual - expected)
        scaled = delta / (self.absolute + self.relative * abs(expected))
        require(np.isfinite(scaled).all(), 'Unrepresentable comparison error.')
        row, column = np.unravel_index(np.argmax(scaled), scaled.shape)
        maximum = float(scaled[row, column])
        if self.worst is None or maximum > self.maximum:
            self.maximum = maximum
            self.worst = dict(sourceTick=int(ticks[row]), component=self.names[column],
                              actual=float(actual[row, column]), expected=float(expected[row, column]))
        for key, columns in self.groups.items():
            self.absolute_maxima[key] = max(self.absolute_maxima[key], float(delta[:, columns].max()))
        self.samples += len(ticks)
        self.last_tick = int(ticks[-1])
        self.grid_hash.update(ticks.astype('<i8').tobytes())

    def report(self):
        require(self.samples > 0, 'Empty comparison cannot pass.')
        return dict(samples=self.samples, numericValues=self.samples * len(self.names),
                    gridSha256=self.grid_hash.hexdigest(),
                    maximumAllowedFraction=self.maximum, worst=self.worst,
                    maximumAbsoluteByGroup=self.absolute_maxima.copy(), passed=self.maximum <= 1)


def refinement(reports, limits):
    """Only reports evaluated at exactly the same physical/source ticks belong here."""
    require(len(reports) >= 2 and len({(r['samples'], r['gridSha256']) for r in reports}) == 1,
            'Refinement grids differ.')
    ratios = [r['maximumAllowedFraction'] for r in reports]
    require(all(np.isfinite(r) and r >= 0 for r in ratios), 'Invalid refinement errors.')
    checks = [dict(coarser=a, finer=b,
                   allowed=limits['maximumRemainingFraction'] * a + limits['normalizedErrorFloor'],
                   passed=b <= limits['maximumRemainingFraction'] * a + limits['normalizedErrorFloor'])
              for a, b in zip(ratios, ratios[1:])]
    return dict(checks=checks, passed=all(c['passed'] for c in checks))


def clocks(protocol, index, count):
    connected = protocol['connected']
    grid = protocol['sourceStepsPerInterval']
    require(type(count) is int and count > 0 and grid % count == 0, 'Unmatched source resolution.')
    require(type(index) is int and 0 <= index < protocol['physicalIntervals'], 'Invalid physical interval.')
    clock = connected['bundle']['frame']['clock']
    dt = clock['physicsStepMs']
    mechanical_ticks = 65536 * connected['mechanicalRefinement']
    require(clock['initialPhysicsStep'] == 0 and mechanical_ticks % grid == 0, 'Unsupported clock origin/grid.')
    local_ticks = np.arange(count + 1, dtype=np.int64)
    ticks = index * grid + local_ticks * (grid // count)
    mechanical = mechanical_ticks + ticks * (mechanical_ticks // grid)
    return dict(sourceTicks=ticks, localMs=local_ticks * (dt / count),
                sourceNeuralMs=clock['neuralEpochMs'] + dt + ticks * (dt / grid),
                physicalNeuralMs=clock['neuralEpochMs'] + mechanical * (dt / mechanical_ticks))


def residuals(equation, values, maximum):
    # Flux ledgers are cumulative from the original initial state, including
    # when a later physical interval is inspected in isolation.
    measured = balance(equation.fixture, np.vstack((equation.initial, values)))
    require(set(measured) == set(maximum), 'Conservation fields changed.')
    for key in maximum:
        require(np.isfinite(measured[key]), 'Nonfinite conserved quantity.')
        maximum[key] = max(maximum[key], measured[key])


def mechanical_precision(witness, bundle, gains):
    """Reproduce saved precision arithmetic; never evaluate an exponential."""
    limits = bundle['referenceBounds']
    values = witness['scipyState']
    a, b = witness['references']['60'], witness['references']['90']
    require(len(values) == len(a['state']) == len(b['state']) == 12, 'Incomplete mechanical witness.')
    require(np.isfinite(values).all() and np.isfinite(witness['scipyBundleNm']), 'Nonfinite mechanical witness.')
    errors = [abs(v - float(r)) / ((limits['volumeAbsolute'] if i < 6 else limits['flowAbsolute'])
              + limits['relative'] * max(abs(v), abs(float(r))))
              for i, (v, r) in enumerate(zip(values, b['state'], strict=True))]
    with localcontext() as context:
        context.prec = 90
        numbers = [D(v) for entry in (a, b) for v in entry['state'] + [entry['bundleNm']]]
        require(all(v.is_finite() for v in numbers), 'Nonfinite Decimal witness.')
        precision = max(abs(D(x) - D(y)) / (D(bundle['matrixPrecisionAbsolute'])
            + D(bundle['matrixPrecisionRelative']) * max(abs(D(x)), abs(D(y))))
            for x, y in zip(a['state'] + [a['bundleNm']], b['state'] + [b['bundleNm']], strict=True))
    for key in ('60', '90'):
        with localcontext() as context:
            context.prec = int(key)
            expected = gains[key] * D(witness['references'][key]['state'][3 + bundle['geometry']['canal']])
            require(D(witness['references'][key]['bundleNm']) == expected, 'Decimal projection changed.')
    nm = witness['scipyBundleNm']
    bound = abs(float(gains['90'])) * limits['volumeAbsolute'] + (limits['relative']
        + bundle['geometryArithmeticRelative']) * max(abs(nm), abs(float(b['bundleNm'])))
    error = abs(nm - float(b['bundleNm'])) / bound
    expected = dict(maximumStateFraction=max(errors), bundleFraction=error,
                    decimalPrecisionFraction=str(precision), passed=max(errors) <= 1 and error <= 1 and precision <= 1)
    require(all(witness[k] == v for k, v in expected.items()), 'Mechanical precision report does not reproduce.')
    return expected['passed']


class ReferenceReader:
    def __init__(self, package, protocol, source, condition, setting, equation, projection, gains):
        self.protocol, self.equation = protocol, equation
        self.condition, self.setting = condition, setting
        self.projection, self.gains = projection, gains
        self.bundle = protocol['connected']['bundle']
        self.folder = package / 'continuous-reference' / (condition + '-' + setting['key'])
        self.grid = protocol['sourceStepsPerInterval']
        self.dt = self.bundle['frame']['clock']['physicsStepMs']
        expected = dict(condition=condition, setting=setting, protocol=protocol, source=source,
            labels=equation.labels, initial=equation.initial.tolist(),
            sourceEpochMs=self.bundle['frame']['clock']['neuralEpochMs'] + self.dt,
            referenceNeuralStepMs=self.dt / self.grid, projectedCellId=projection.hair_cell_id,
            derivative='preserved independent Decimal joint derivative', explicitJacobian=True,
            ledgerJacobianColumnsExactlyZero=True, nativeStagesOrTrajectoryInputsUsed=False)
        require(read(self.folder / 'input.json') == expected, 'Independent reference inputs changed.')
        self.result = read(self.folder / 'results.json')
        require(self.result['completed'] and len(self.result['intervals']) == protocol['physicalIntervals']
            and self.result['uniqueSamples'] == protocol['expectedUniqueSamplesPerReference']
            and self.result['storedSamples'] == protocol['expectedStoredSamplesPerReference']
            and self.result['allAcceptedNodesAndGridSamplesPhysicallyAdmissible']
            and not self.result['candidateTrajectoryUsedForIntegration'], 'Independent reference incomplete.')
        self.previous = equation.initial.copy()
        self.mechanical_previous = {'scipy': [0.] * 12, '60': ['0'] * 12, '90': ['0'] * 12}
        self.ledger = {key: 0. for key in protocol['ledgerResidualBounds']}
        self.endpoint_errors = ErrorEnvelope(equation.labels, protocol['referenceComparison'])
        self.precision_passed = True
        self.nodes = self.queries = self.witness_count = self.next_interval = 0

    def interval(self, index):
        require(index == self.next_interval, 'Reference physical interval skipped or repeated.')
        folder = self.folder / f'interval-{index + 1:02}'
        nodes = arrays(folder / 'accepted-nodes.npz', ('localMs', 'values'))
        samples = arrays(folder / 'samples.npz', ('localTicks', 'localMs', 'values'))
        clock = arrays(folder / 'clock.npz', ('sourceTicks', 'localMs', 'sourceNeuralMs', 'physicalNeuralMs'))
        expected_clock = clocks(self.protocol, index, self.grid)
        for key in expected_clock:
            same(clock[key], expected_clock[key], 'Reference clock mismatch: ' + key)
        same(samples['localTicks'], np.arange(self.grid + 1, dtype=np.int64), 'Incomplete reference sample grid.')
        same(samples['localMs'], clock['localMs'], 'Dense-output clock mismatch.')
        times, accepted, values = nodes['localMs'], nodes['values'], samples['values']
        require(times.ndim == 1 and len(times) >= 2 and times[0] == 0 and times[-1] == self.dt
                and (np.diff(times) > 0).all(), 'Missing or unordered accepted reference nodes.')
        require(accepted.shape == (self.equation.size, len(times))
                and values.shape == (self.equation.size, self.grid + 1), 'Incomplete reference states/fluxes.')
        same(accepted[:, 0], self.previous, 'Reference state or flux reset at physical boundary.')
        same(values[:, 0], self.previous, 'Reference dense initial state changed.')
        self.equation.validate_samples(accepted)
        self.equation.validate_samples(values)
        for data in (accepted, values):
            residuals(self.equation, data.T, self.ledger)
        # Dense output may differ from the accepted endpoint by roundoff. Keep
        # that error under the original reference bounds, not a new epsilon.
        self.endpoint_errors.add(values[:, -1:].T, accepted[:, -1:].T,
                                 np.array([(index + 1) * self.grid], dtype=np.int64))
        meta = read(folder / 'integration.json')
        expected_meta = dict(completed=True, error=None, method=self.setting['method'], durationMs=self.dt,
            requestedGridCount=self.grid, populatedSamples=self.grid + 1, acceptedNodes=len(times),
            completedThroughLocalMs=self.dt, stateAndLedgersCarriedContinuously=True,
            unusedSampleTailSaved=False, referenceTrajectoryCriteriaPassed=False)
        require(all(meta[k] == v for k, v in expected_meta.items()), 'Independent integration metadata mismatch.')
        require(all(type(meta[k]) is int and meta[k] >= 0 for k in ('nfev', 'njev', 'nlu')), 'Invalid solver counters.')
        query_count, witness_count = self.witnesses(index, folder, times, accepted, values)
        expected_interval = dict(heldPhysicalStep=index + 1, storedSamples=self.grid + 1,
            acceptedNodes=len(times), inputQueries=query_count, precisionWitnesses=witness_count)
        require(self.result['intervals'][index] == expected_interval, 'Reference interval accounting changed.')
        self.previous = accepted[:, -1].copy()
        self.nodes += len(times)
        self.queries += query_count
        self.witness_count += witness_count
        self.next_interval += 1
        return values.T, clock

    def witnesses(self, index, folder, times, accepted, values):
        observed = self.equation.state_labels.index('axon/' + self.protocol['connected']['observationNode'] + '/potentialMv')
        maximum = int(np.argmax(abs(accepted[observed])))
        candidates = [(0., accepted[:, 0]), (float(times[1]), accepted[:, 1]),
                      (self.dt / 2, values[:, self.grid // 2]), (float(times[maximum]), accepted[:, maximum]),
                      (self.dt, accepted[:, -1])]
        expected = dict(candidates)
        witnesses = read(folder / 'precision.json')
        require([w['mechanical']['localMs'] for w in witnesses] == sorted(expected), 'Missing precision witness.')
        by_time = {}
        for witness in witnesses:
            mechanical, neuronal = witness['mechanical'], witness['neuronal']
            time = mechanical['localMs']
            same(neuronal['state'], expected[time], 'Neuronal witness state differs from retained trajectory.')
            require(neuronal['localMs'] == time and neuronal['heldPhysicalStep'] == index + 1
                    and neuronal['forcing'] == dict(bundleDisplacementNanometers=mechanical['scipyBundleNm']),
                    'Neuronal witness forcing or clock changed.')
            references = neuronal['references']
            require(len(references) == 2, 'Missing independent precision evaluation.')
            precision = record_comparison(references[0], references[1], dict(absolute='1e-40', relative='1e-40'))
            working = record_comparison(neuronal['reference40'], references[1], dict(absolute='1e-25', relative='1e-25'))
            require(neuronal['precision'] == precision and neuronal['workingPrecision'] == working,
                    'Neuronal precision arithmetic changed.')
            mechanical_passed = mechanical_precision(mechanical, self.bundle, self.gains)
            self.precision_passed &= precision['passed'] and working['passed'] and mechanical_passed
            require(mechanical['scipyBundleNm'] == self.projection.gain_nm_per_m3
                    * mechanical['scipyState'][3 + self.projection.canal], 'Saved mechanical projection changed.')
            if time == 0:
                same(mechanical['scipyState'], self.mechanical_previous['scipy'], 'Mechanical state reset.')
                for key in ('60', '90'):
                    require([D(v) for v in mechanical['references'][key]['state']]
                            == [D(v) for v in self.mechanical_previous[key]], 'Decimal mechanical state reset.')
            if self.condition == 'stationary-head':
                require(not any(mechanical['scipyState']) and mechanical['scipyBundleNm'] == 0
                    and all(D(v) == 0 for key in ('60', '90') for v in mechanical['references'][key]['state']),
                    'Stationary reference has mechanical drive.')
            by_time[time] = mechanical
        end = by_time[self.dt]
        self.mechanical_previous = dict(scipy=end['scipyState'],
            **{key: end['references'][key]['state'] for key in ('60', '90')})
        seen = set()
        with (self.folder / f'input-queries-{index + 1:02}.jsonl').open(encoding='utf-8') as stream:
            for line in stream:
                query = json.loads(line)
                time, state = query['localMs'], query['mechanicalState']
                require(np.isfinite(time) and 0 <= time <= self.dt and time not in seen,
                        'Invalid or duplicate input query.')
                require(len(state) == 12 and np.isfinite(state).all() and np.isfinite(query['bundleNm']),
                        'Incomplete input query.')
                require(query['bundleNm'] == self.projection.gain_nm_per_m3 * state[3 + self.projection.canal],
                        'Input query projection changed.')
                if self.condition == 'stationary-head':
                    require(not any(state) and query['bundleNm'] == 0, 'Stationary input query is driven.')
                if time in by_time:
                    require(state == by_time[time]['scipyState'] and query['bundleNm'] == by_time[time]['scipyBundleNm'],
                            'Recorded RHS input differs from its precision witness.')
                seen.add(time)
        require(0. in seen and self.dt in seen, 'Reference endpoint forcing queries missing.')
        return len(seen), len(witnesses)

    def report(self):
        require(self.next_interval == self.protocol['physicalIntervals'], 'Missing reference intervals.')
        ledger_pass = all(self.ledger[key] <= bound for key, bound in self.protocol['ledgerResidualBounds'].items())
        endpoint = self.endpoint_errors.report()
        return dict(setting=self.setting['key'], acceptedNodes=self.nodes, uniqueSamples=self.result['uniqueSamples'],
            storedSamples=self.result['storedSamples'], inputQueries=self.queries, precisionWitnesses=self.witness_count,
            precisionPassed=bool(self.precision_passed), independentLedgerResiduals=self.ledger,
            conservationPassed=ledger_pass, denseEndpointErrors=endpoint,
            passed=bool(ledger_pass and self.precision_passed and endpoint['passed']))


class NativeReader:
    def __init__(self, stack, package, protocol, condition, count, equation, native_directory=None):
        self.protocol, self.equation, self.condition, self.count = protocol, equation, condition, count
        connected = protocol['connected']
        self.folder = (package / 'native/study' if native_directory is None else Path(native_directory)) / f'{condition}-n{count}'
        initial = read(self.folder / 'initial.json')
        self.final = read(self.folder / 'final.json')
        state, source, clock = initial['neuronal'], initial['source'], initial['binding']['physicalClock']
        expected_clock = connected['bundle']['frame']['clock']
        require(clock == (expected_clock if condition == 'recorded-head' else dict(expected_clock,
            sessionId=connected['quietSessionId'], fingerprint=clock['fingerprint'])), 'Native body clock changed.')
        require(initial['condition'] == condition and initial['count'] == count, 'Native run identity changed.')
        require(state['stateLabels'] == equation.state_labels and state['ledgerLabels'] == equation.ledger_labels,
                'Native state/flux labels changed.')
        same(state['values'] + state['ledgers'], equation.initial, 'Native starting state/flux differs from reference.')
        same(state['initialValues'], state['values'], 'Native original initial state changed.')
        require(not any(state['additionResiduals']) and not any(state['ledgerAdditionResiduals']), 'Nonzero initial compensation.')
        require(state['completedSteps'] == 0 and state['nonlinearEvaluations'] == 0 and state['failure'] is None,
                'Native state already advanced or failed.')
        require(state['sessionId'] == source['sessionId'] == clock['sessionId']
            and state['cellId'] == source['cellId'] == initial['attachment']['hairCellId']
            == connected['bundle']['attachment']['hairCellId'], 'Native source identity changed.')
        require(source['cableNodeId'] == connected['observationNode'], 'Observed cable node changed.')
        dt = clock['physicsStepMs']
        require(state['epochMs'] == state['timeMs'] == source['epochMs'] == clock['neuralEpochMs'] + dt
            and source['stepMs'] == dt / count, 'Native source clock changed.')
        require(state['settings'] == dict(stepMs=dt / count, **connected['nonlinearSettings']), 'Native solver settings changed.')
        require(self.final['completed'] and self.final['failure'] is None and self.final['condition'] == condition
                and self.final['count'] == count and self.final['steps'] == protocol['physicalIntervals'] * count
                and self.final['intervals'] == protocol['physicalIntervals'], 'Incomplete native capture.')
        ns, nl = len(equation.state_labels), len(equation.ledger_labels)
        diagnostics = list(RESIDUALS.values()) + ['nonlinearEvaluations', 'maximumNewtonIterations', 'maximumAcceptedScaledResidual']
        self.header = ['step', 'timeMs', 'heldBundleNm', 'mechanicalStartTick', 'mechanicalEndTick',
            'mechanicalEndNeuralMs', 'representationDifferenceMs', 'startMv', 'endMv', 'startSlopeMvPerMs', 'endSlopeMvPerMs']
        self.header += equation.state_labels + ['stateError/' + n for n in equation.state_labels]
        self.header += equation.ledger_labels + ['ledgerError/' + n for n in equation.ledger_labels] + diagnostics
        self.slices = [slice(11, 11 + ns), slice(11 + ns, 11 + 2 * ns),
                       slice(11 + 2 * ns, 11 + 2 * ns + nl), slice(11 + 2 * ns + nl, 11 + 2 * ns + 2 * nl)]
        self.stream = stack.enter_context(gzip.open(self.folder / 'trajectory.csv.gz', 'rt', encoding='utf-8'))
        require(self.stream.readline().rstrip('\r\n').split(',') == self.header, 'Native trajectory header changed.')
        self.ledger = {key: 0. for key in protocol['ledgerResidualBounds']}
        self.own = ErrorEnvelope(equation.labels, protocol['candidateComparison'])
        self.common = ErrorEnvelope(equation.labels, protocol['candidateComparison'])
        self.next_interval = 0
        self.previous_voltage = state['values'][equation.state_labels.index('axon/' + connected['observationNode'] + '/potentialMv')]
        self.bath = state['perilymphMv']
        require(self.bath == equation.fixture['sensory']['perilymphMv'], 'Reference bath changed.')

    def interval(self, index, reference, reference_clock):
        require(index == self.next_interval, 'Native physical interval skipped or repeated.')
        rows = np.loadtxt(self.stream, delimiter=',', max_rows=self.count, ndmin=2)
        require(rows.shape == (self.count, len(self.header)) and np.isfinite(rows).all(), 'Missing/nonfinite native steps.')
        grid = self.protocol['sourceStepsPerInterval']
        connected = self.protocol['connected']
        stride = grid // self.count
        own_clock = clocks(self.protocol, index, self.count)
        for key in own_clock:
            same(own_clock[key], reference_clock[key][::stride], 'Native/reference grid differs: ' + key)
        steps = np.arange(index * self.count + 1, (index + 1) * self.count + 1, dtype=np.int64)
        mechanical_ticks = 65536 * connected['mechanicalRefinement']
        end_ticks = mechanical_ticks + own_clock['sourceTicks'][1:] * (mechanical_ticks // grid)
        same(rows[:, 0], steps, 'Missing or reordered native step.')
        same(rows[:, 1], own_clock['sourceNeuralMs'][1:], 'Native source timestamp differs.')
        same(rows[:, 3], end_ticks - mechanical_ticks // self.count, 'Native mechanical start tick differs.')
        same(rows[:, 4], end_ticks, 'Native mechanical end tick differs.')
        same(rows[:, 5], own_clock['physicalNeuralMs'][1:], 'Native mechanical timestamp differs.')
        same(rows[:, 6], own_clock['sourceNeuralMs'][1:] - own_clock['physicalNeuralMs'][1:], 'Clock representation difference changed.')
        values = np.column_stack((rows[:, self.slices[0]], rows[:, self.slices[2]]))
        self.equation.validate_samples(values.T)
        residuals(self.equation, values, self.ledger)
        for column, key in enumerate(RESIDUALS):
            require((rows[:, -7 + column] >= 0).all()
                    and rows[:, -7 + column].max() <= connected['ledgerResidualBounds'][key], 'Native residual diagnostic exceeds bound.')
        require(rows[:, -2].max() <= connected['nonlinearSettings']['maximumNewtonIterations']
                and rows[:, -1].max() <= 1, 'Native nonlinear solve criteria failed.')
        observed = self.equation.state_labels.index('axon/' + connected['observationNode'] + '/potentialMv')
        same(rows[:, 8], values[:, observed] - self.bath, 'Native observed voltage differs from source state.')
        same(rows[:, 7], np.concatenate(([self.previous_voltage - self.bath], values[:-1, observed] - self.bath)),
             'Native observation reset at physical interval.')
        self.previous_voltage = values[-1, observed]
        self.own.add(values, reference[stride::stride], own_clock['sourceTicks'][1:])
        coarsest = min(connected['sourceStepsPerInterval'])
        factor = self.count // coarsest
        self.common.add(values[factor - 1::factor], reference[grid // coarsest::grid // coarsest],
                        own_clock['sourceTicks'][factor::factor])
        self.last_row = rows[-1].copy()
        self.next_interval += 1

    def report(self):
        require(self.next_interval == self.protocol['physicalIntervals'], 'Missing native physical interval.')
        require(self.stream.read() == '', 'Additional native trajectory rows.')
        final = self.final['state']
        require(final['failure'] is None and final['pendingPacket'] is None and final['mechanical']['pendingTrace'] is None,
                'Native pending delivery or failure.')
        require(final['completedSteps'] == final['acknowledgedSteps'] == final['observer']['observedThroughStep']
                == self.next_interval * self.count, 'Incomplete native acknowledgement.')
        for subset, name in zip(self.slices, ('values', 'additionResiduals', 'ledgers', 'ledgerAdditionResiduals'), strict=True):
            same(self.last_row[subset], final['neuronal'][name], 'Native final state/flux/compensation differs: ' + name)
        require(self.last_row[1] == final['neuronal']['timeMs'], 'Native final time differs.')
        ledger_pass = all(self.ledger[key] <= bound for key, bound in self.protocol['ledgerResidualBounds'].items())
        own, common = self.own.report(), self.common.report()
        require(own['samples'] == self.next_interval * self.count
                and common['samples'] == self.next_interval * min(self.protocol['connected']['sourceStepsPerInterval']),
                'Incomplete comparison coverage.')
        return dict(sourceStepsPerInterval=self.count, allEndpoints=own, commonCoarseGrid=common,
                    independentLedgerResiduals=self.ledger, conservationPassed=ledger_pass)


def analyse(package):
    """Use only later frozen/copied completed evidence, never the running package."""
    package = Path(package)
    reviewed = read(package / 'reviewed-connected.json')
    require(reviewed['operatorReportedCompletion'] is True and reviewed['connectedAccountingPassed'] is True,
            'Reported and reviewed native accounting is required.')
    p, source = read(package / 'continuous-reference-input.json'), read(package / 'source-decisions.json')
    require(p['version'] == 'continuous-canal-joint-afferent-reference-v1'
            and source['activationOutsidePolicy'] == 'endpoint-hold', 'Unexpected reference version/policy.')
    require(read(package / 'native/connected-input.json') == p['connected'], 'Native/reference protocols differ.')
    require(read(package / 'native/study/input.json') == p['connected'], 'Captured native protocol differs.')
    accounting = read(package / 'native/analysis.json')
    require(accounting['completed'] and accounting['connectedAccountingPassed']
            and accounting['sourceSteps'] == p['connected']['expectedSourceSteps'], 'Native causal review incomplete.')
    expected_runs = [(c, n) for c in p['conditions'] for n in p['connected']['sourceStepsPerInterval']]
    require([(r['condition'], r['sourceStepsPerInterval']) for r in accounting['runs']] == expected_runs
            and all(r['accountingPassed'] for r in accounting['runs']), 'Native causal cases differ.')
    native_result = read(package / 'native/study/results.json')
    require(native_result['completed'] and len(native_result['runs']) == len(expected_runs), 'Native summary incomplete.')
    for (condition, count), result in zip(expected_runs, native_result['runs'], strict=True):
        require(read(package / 'native/study' / f'{condition}-n{count}' / 'final.json') == result,
                'Native final state differs from completed summary.')
    require(read(package / 'continuous-reference/results.json') == dict(completed=True, attempted=p['expectedReferences'],
        failedRuns=[], comparisonsExecuted=False, coupledAccuracyQualified=False,
        physiologicalCalibrationQualified=False, avatarBalanceAssessed=False), 'Reference execution incomplete.')
    return compare_trajectories(package, p, source)


def compare_trajectories(package, p, source, *, native_directory=None, reference_package=None):
    """Shared original comparison after caller verifies capture/input/reference provenance.

    Separate native and reference roots allow new captures to use the preserved
    independent solutions without copying/reintegrating them. All comparison
    equations, endpoints, conservation limits and refinement tests are unchanged.
    """
    package = Path(package)
    reference_package = package if reference_package is None else Path(reference_package)
    output = package / 'continuous-comparison'
    output.mkdir()
    reports = []
    try:
        equation = Equation(p['connected']['fixture'], source, p['referenceDecimalPrecision'])
        require(p['sourceStepsPerInterval'] == 65536 and p['physicalIntervals'] == 10
            and p['connected']['sourceStepsPerInterval'] == [16384, 32768, 65536]
            and p['conditions'] == ['recorded-head', 'stationary-head'], 'Prospective comparison coverage changed.')
        catalog = read(package / 'source/docs/reference-data/vestibular-ifediba-2007.json')
        bundle = p['connected']['bundle']
        projection = BundleProjection.from_spec(bundle, catalog)
        gains = {str(n): decimal_gain(bundle, catalog, n) for n in (60, 90)}
        require([s['key'] for s in p['referenceSettings']] == ['radau-standard', 'radau-tight', 'bdf-tight'],
                'Required independent methods changed.')
        for condition in p['conditions']:
            readers = {s['key']: ReferenceReader(reference_package, p, source, condition, s, equation, projection, gains)
                       for s in p['referenceSettings']}
            errors = {key: ErrorEnvelope(equation.labels, p['referenceComparison'])
                      for key in readers if key != 'radau-tight'}
            with ExitStack() as stack:
                natives = [NativeReader(stack, package, p, condition, count, equation, native_directory)
                           for count in p['connected']['sourceStepsPerInterval']]
                for index in range(p['physicalIntervals']):
                    expected, clock = readers['radau-tight'].interval(index)
                    selected = slice(None) if index == 0 else slice(1, None)
                    for key, envelope in errors.items():
                        actual, other_clock = readers[key].interval(index)
                        for name in clock:
                            same(clock[name], other_clock[name], 'Independent method clocks differ.')
                        envelope.add(actual[selected], expected[selected], clock['sourceTicks'][selected])
                    for native in natives:
                        native.interval(index, expected, clock)
                    save(output / f'{condition}-interval-{index + 1:02}.json', dict(completed=True,
                        heldPhysicalStep=index + 1, referenceErrors={key: value.report() for key, value in errors.items()},
                        nativeErrors=[dict(sourceStepsPerInterval=n.count, allEndpoints=n.own.report(),
                                           commonCoarseGrid=n.common.report()) for n in natives]))
                native_reports = [n.report() for n in natives]
            reference_reports = [r.report() for r in readers.values()]
            error_reports = {key: envelope.report() for key, envelope in errors.items()}
            require(all(r['samples'] == p['expectedUniqueSamplesPerReference'] for r in error_reports.values()),
                    'Independent comparison lost fine-grid samples.')
            reference_passed = all(r['passed'] for r in reference_reports) and all(r['passed'] for r in error_reports.values())
            convergence = refinement([r['commonCoarseGrid'] for r in native_reports], p['refinement'])
            finest = native_reports[-1]['allEndpoints']['passed']
            conservation = all(r['conservationPassed'] for r in native_reports)
            reports.append(dict(condition=condition, references=reference_reports, referenceErrors=error_reports,
                referenceCriteriaPassed=reference_passed, candidates=native_reports, commonTimeRefinement=convergence,
                finestAllEndpointCriteriaPassed=finest, conservationPassed=conservation,
                passed=bool(reference_passed and convergence['passed'] and finest and conservation)))
        passed = all(r['passed'] for r in reports)
        result = dict(completed=True, sampledContinuousInputAccuracyPassed=passed, cases=reports,
            comparedNativeSteps=sum(r['allEndpoints']['samples'] for c in reports for r in c['candidates']),
            originalBoundsChanged=False, identicalHeldInputAccuracyQualified=False, eventTimeAccuracyQualified=False,
            physiologicalCalibrationQualified=False, liveBrainAuthorityQualified=False, avatarBalanceAssessed=False)
        require(result['comparedNativeSteps'] == p['connected']['expectedSourceSteps'], 'Incomplete native comparison.')
        save(output / 'analysis.json', result)
        if not passed:
            raise RuntimeError('Continuous-input trajectory criteria failed; retain every sample and original bound.')
        return result
    except Exception:
        save(output / 'failure.json', dict(error=traceback.format_exc(), completedCases=reports,
            sampledContinuousInputAccuracyPassed=False, originalBoundsChanged=False))
        raise

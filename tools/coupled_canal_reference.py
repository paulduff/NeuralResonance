"""Independent linear matrix-exponential reference; execute only in visible studies.

Uses source CGS decimal strings, exact SI conversion and Decimal Taylor scaling/
squaring. It shares neither Radau stages nor the native linear solver. A second
binary64 reference uses SciPy's matrix exponential and independently solved M.
"""
from contextlib import ExitStack
from decimal import Decimal, localcontext, ROUND_HALF_EVEN
import gzip
import json
from pathlib import Path
import numpy as np
from scipy.linalg import expm

D = Decimal


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def write(path, value):
    with path.open('x', encoding='utf-8') as stream:
        json.dump(value, stream, indent=2, allow_nan=False)


def identity(n):
    return [[D(int(i == j)) for j in range(n)] for i in range(n)]


def multiply(a, b):
    n = len(b[0]); result = [[D(0)] * n for _ in a]
    for i, row in enumerate(a):
        for k, value in enumerate(row):
            if value:
                for j, other in enumerate(b[k]):
                    if other: result[i][j] += value * other
    return result


def norm(a):
    return max(sum(map(abs, row), D(0)) for row in a)


def inverse(a):
    n = len(a); augmented = [row.copy() + identity(n)[i] for i, row in enumerate(a)]
    for column in range(n):
        pivot = max(range(column, n), key=lambda i: abs(augmented[i][column]))
        if not augmented[pivot][column]: raise ArithmeticError('Singular source mass matrix.')
        augmented[column], augmented[pivot] = augmented[pivot], augmented[column]
        diagonal = augmented[column][column]
        augmented[column] = [v / diagonal for v in augmented[column]]
        for i in range(n):
            if i != column:
                factor = augmented[i][column]
                augmented[i] = [x - factor * y for x, y in zip(augmented[i], augmented[column])]
    return [row[n:] for row in augmented]


def source_matrices(catalog):
    def converted(row):
        factor = D(row['exactUnitFactor'])
        return [[D(value) * factor for value in values] for values in row['sourceDecimalStrings']]
    m, c, k = [converted(catalog['matrices'][name]) for name in ('mass', 'damping', 'stiffness')]
    f = converted(catalog['forcing'])
    assert all(len(a) == 6 and all(len(row) == 6 for row in a) for a in (m, c, k))
    assert len(f) == 6 and all(len(row) == 3 for row in f)
    return m, c, k, f


def generator_decimal(catalog):
    m, c, k, f = source_matrices(catalog); mi = inverse(m)
    mk, mc, mf = multiply(mi, k), multiply(mi, c), multiply(mi, f)
    a = [[D(0)] * 15 for _ in range(15)]
    for i in range(6):
        a[i][i + 6] = D(1)
        for j in range(6):
            a[i + 6][j] = -mk[i][j]; a[i + 6][j + 6] = -mc[i][j]
        a[i + 6][12:] = mf[i]
    return a


def generator_binary64(catalog):
    # Separate binary64 solve and construction, with source decimal conversion.
    m, c, k, f = [np.array(a, dtype=float) for a in source_matrices(catalog)]
    a = np.zeros((15, 15)); a[:6, 6:12] = np.eye(6)
    a[6:12, :6] = -np.linalg.solve(m, k)
    a[6:12, 6:12] = -np.linalg.solve(m, c)
    a[6:12, 12:] = np.linalg.solve(m, f)
    return a


def exponential(a, precision):
    # ||scaled A||_infinity <= 1/8. Once k terms are retained, the remaining
    # Taylor series norm is <= ||term_k||*r/(1-r), r=||scaled A||/(k+1).
    with localcontext() as ctx:
        ctx.prec = precision; ctx.rounding = ROUND_HALF_EVEN
        reduced = [row.copy() for row in a]; original_norm = norm(reduced); squarings = 0
        while norm(reduced) > D('.125'):
            reduced = [[x / 2 for x in row] for row in reduced]; squarings += 1
            if squarings > 128: raise ArithmeticError('Exponential scaling exceeds declared range.')
        scaled_norm = norm(reduced); term = identity(len(a)); value = identity(len(a))
        goal = D(10) ** (-precision + 5); tail = D(0)
        for order in range(1, 1000):
            term = [[x / order for x in row] for row in multiply(term, reduced)]
            value = [[x + y for x, y in zip(row, addition)] for row, addition in zip(value, term)]
            ratio = scaled_norm / (order + 1)
            tail = norm(term) * ratio / (1 - ratio)
            if tail <= goal: break
        else: raise ArithmeticError('Exponential Taylor bound did not converge.')
        for _ in range(squarings): value = multiply(value, value)
        if not all(x.is_finite() for row in value for x in row): raise ArithmeticError('Nonfinite exponential.')
        return value, dict(precision=precision, originalNorm=str(original_norm), scaledNorm=str(scaled_norm),
            squarings=squarings, taylorOrder=order, localSeriesTailBound=str(tail), localSeriesGoal=str(goal),
            tailIsNotAGlobalRoundoffBound=True)


def apply(matrix, point, precision):
    with localcontext() as ctx:
        ctx.prec = precision; ctx.rounding = ROUND_HALF_EVEN
        return [sum((a * b for a, b in zip(row, point) if a and b), D(0)) for row in matrix]


def controls(output):
    assert not output.exists(); output.mkdir()
    results = []
    with localcontext() as ctx:
        ctx.prec = 90; ctx.rounding = ROUND_HALF_EVEN
        fixtures = [
            ('zero', [[D(0)] * 2 for _ in range(2)], identity(2)),
            ('diagonal', [[D(-2), D(0)], [D(0), D('.25')]], [[D(-2).exp(), D(0)], [D(0), D('.25').exp()]]),
            ('nilpotent', [[D(0), D(3), D(0)], [D(0), D(0), D(4)], [D(0), D(0), D(0)]],
             [[D(1), D(3), D(6)], [D(0), D(1), D(4)], [D(0), D(0), D(1)]]),
            ('damped-jordan', [[D(-1), D(2)], [D(0), D(-1)]], [[D(-1).exp(), 2 * D(-1).exp()], [D(0), D(-1).exp()]]),
            ('stiff-decay', [[D(-1000), D(0)], [D(0), D(-1)]], [[D(-1000).exp(), D(0)], [D(0), D(-1).exp()]]),
            ('constant-forcing', [[D(-2), D(3)], [D(0), D(0)]], [[D(-2).exp(), D('1.5') * (1 - D(-2).exp())], [D(0), D(1)]])]
        for key, a, expected in fixtures:
            values = []; metadata = []
            for precision in (60, 90):
                value, record = exponential(a, precision); values.append(value); metadata.append(record)
            errors = [max(abs(x - y) for row, other in zip(value, expected) for x, y in zip(row, other)) for value in values]
            results.append(dict(key=key, passed=all(error <= D('1e-45') for error in errors),
                absoluteErrors=[str(error) for error in errors], metadata=metadata,
                expected=[[str(x) for x in row] for row in expected], values=[[[str(x) for x in row] for row in value] for value in values]))
    write(output / 'controls.json', dict(completed=True, expected=6, passed=sum(row['passed'] for row in results), cases=results, physicalModelExecuted=False))
    if not all(row['passed'] for row in results): raise RuntimeError('Independent exponential controls failed.')


def run(package):
    spec = read(package / 'trajectory-input.json'); catalog = read(package / 'source/docs/reference-data/vestibular-ifediba-2007.json')
    output = package / 'reference-trajectories'; assert not output.exists(); output.mkdir()
    matrices = {}; matrix_records = []
    steps = sorted({phase['baseStepTicks'] // 4 for case in spec['cases'] for phase in case['phases']})
    a64 = generator_binary64(catalog)
    for step in steps:
        operators = {}
        for precision in spec['referencePrecisions']:
            with localcontext() as ctx:
                ctx.prec = precision; ctx.rounding = ROUND_HALF_EVEN
                a = generator_decimal(catalog); seconds = D(step) / spec['ticksPerSecond']
                operator, record = exponential([[v * seconds for v in row] for row in a], precision)
                operators[precision] = operator
                matrix_records.append(dict(stepTicks=step, precision=precision, matrix=[[str(v) for v in row] for row in operator], metadata=record))
        operators['scipy'] = expm(a64 * (step / spec['ticksPerSecond']))
        matrices[step] = operators
    write(output / 'operators.json', matrix_records)
    np.savez_compressed(output / 'scipy-operators.npz', **{str(step): operators['scipy'] for step, operators in matrices.items()})
    reports = []
    for case in spec['cases']:
        print('INDEPENDENT PUBLISHED CANAL ' + case['key'], flush=True)
        folder = output / case['key']; folder.mkdir()
        count = 1; previous = 0
        for phase in case['phases']:
            count += (phase['endTick'] - previous) // (phase['baseStepTicks'] // 4); previous = phase['endTick']
        ticks = np.empty(count, dtype=np.int64)
        values = {precision: np.empty((count, 12)) for precision in [*spec['referencePrecisions'], 'scipy']}
        points = {precision: [D.from_float(float(v)) for v in case['initial']] + [D(0)] * 3 for precision in spec['referencePrecisions']}
        point64 = np.array(case['initial'] + [0.] * 3); index = 0; tick = 0
        resources = ExitStack(); completed = False; recorded = 0
        try:
            streams = {precision: resources.enter_context(gzip.open(folder / ('decimal' + str(precision) + '.csv.gz'), 'xt', encoding='utf-8', newline='')) for precision in spec['referencePrecisions']}
            def record():
                nonlocal recorded
                ticks[index] = tick
                for precision, point in points.items():
                    values[precision][index] = [float(x) for x in point[:12]]
                    streams[precision].write(str(tick) + ',' + ','.join(str(x) for x in point[:12]) + '\n')
                values['scipy'][index] = point64[:12]
                recorded = index + 1
            record()
            for phase in case['phases']:
                dt = phase['baseStepTicks'] // 4
                for precision in points: points[precision][12:] = [D(v) for v in phase['input']]
                point64[12:] = phase['input']
                while tick < phase['endTick']:
                    for precision in points: points[precision] = apply(matrices[dt][precision], points[precision], precision)
                    point64 = matrices[dt]['scipy'] @ point64
                    tick += dt; index += 1; record()
            assert tick == case['durationTicks'] and index + 1 == count
            completed = True
        finally:
            resources.close()
            np.savez_compressed(folder / ('samples.npz' if completed else 'partial-samples.npz'),
                ticks=ticks[:recorded], decimal60=values[60][:recorded], decimal90=values[90][:recorded], scipy=values['scipy'][:recorded])
            write(folder / 'results.json', dict(completed=completed, samples=recorded, endTick=int(ticks[recorded - 1]) if recorded else None,
                independentSourceDecimals=True, nativeValuesUsedForIntegration=False, sourceToSkullFrameQualified=False, physiologyQualified=False))
        reports.append(dict(key=case['key'], completed=True, samples=count, endTick=tick))
    write(output / 'results.json', dict(completed=True, cases=reports, nativeValuesUsedForIntegration=False,
        localBundleMapQualified=False, physiologyQualified=False, avatarRecoveryAssessed=False))

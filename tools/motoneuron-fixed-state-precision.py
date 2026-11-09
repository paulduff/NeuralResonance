"""Fixed-state arithmetic diagnosis only; no ODE solve or changed acceptance gate."""
import argparse
from decimal import Decimal, localcontext
from hashlib import sha256
import importlib.util
import json
import os
from pathlib import Path
import sys
for name in ('OPENBLAS_NUM_THREADS', 'OMP_NUM_THREADS', 'MKL_NUM_THREADS'):
    os.environ[name] = '1'
import numpy as np
import scipy

SCENARIOS = ['quiet', 'soma-1000', 'without-fast-sodium', 'soma-6000']
METHODS = ['dop853-tight', 'dop853-tighter', 'radau']
GATES = np.r_[np.arange(5, 14), np.arange(15, 27)]
read = lambda p: json.loads(p.read_text(encoding='utf-8-sig'))
digest = lambda p: sha256(p.read_bytes()).hexdigest().upper()
D = lambda x: Decimal.from_float(float(x))


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result)
    return result


def write_new(path, value):
    with path.open('x', encoding='utf-8') as stream:
        stream.write(json.dumps(value, indent=2, allow_nan=False) + '\n')


def load(package):
    config = read(package / 'configuration.json')
    assert np.__version__ == '2.2.6' and scipy.__version__ == '1.16.3'
    assert config['Scenarios'] == SCENARIOS and config['OriginalRevision'] == '22d295951135b2b04992884ddfd309607cc40f6c'
    for entry in config['PackageFiles']:
        assert digest(package / entry['Path']) == entry['Sha256'], entry['Path']
    sys.path.insert(0, str(package / 'reference-source'))
    reference = module('frozen_motor_reference', package / 'reference-source/motoneuron-active-reference.py')
    spec = read(package / 'retained/motoneuron-active-input.json')
    recorded = read(package / 'retained/motoneuron-active-reference/comparison.json')
    assert recorded['completed'] and not recorded['numericalCriteriaPassed'] and recorded['criteria'] == spec['criteria']
    return reference, spec, recorded


def select(package):
    reference, spec, recorded = load(package)
    results = read(package / 'retained/motoneuron-active-study/results.json')
    grid = np.arange(60001) * .1
    points = []
    for name in SCENARIOS:
        scenario = next(c for c in spec['cases'] if c['key'] == name)
        saved = next(c for c in recorded['cases'] if c['scenario'] == name)
        arrays, crossings = {}, {}
        for method in METHODS:
            label = name + '-' + method
            with np.load(package / 'retained/motoneuron-active-reference' / (label + '.npz'), allow_pickle=False) as data:
                assert np.array_equal(data['timeMs'], grid)
                arrays[label] = data['state'].copy()
                crossings[label] = data['crossingsMs'].copy()
        target = arrays[name + '-dop853-tighter']
        events = crossings[name + '-dop853-tighter']
        indices = {0, 9990, 9999, 10000, 10001, 12000, 29999, 30000, 30001, 59999, 60000}
        for contrast in saved['referenceContrasts']:
            a, b = (name + '-' + contrast[k] for k in ('left', 'right'))
            assert reference.compare(arrays[a], arrays[b], crossings[a], crossings[b]) == contrast['maximumAbsolute']
            indices.add(int(np.argmax(np.abs(arrays[a][:, 14] - arrays[b][:, 14]))))
        for candidate in saved['candidates']:
            label = candidate['key']
            original = next(c for c in results['cases'] if c['key'] == label)
            trace = np.loadtxt(package / 'retained/motoneuron-active-study' / (label + '.csv'), delimiter=',', skiprows=1)
            assert original['completed'] and trace.shape == (60001, 31) and np.array_equal(trace[:, 0], grid)
            arrays[label] = trace[:, 1:]
            assert reference.compare(arrays[label], target, np.asarray(original['crossingsMs']), events) == candidate['maximumAbsolute']
            indices.add(int(np.argmax(np.abs(arrays[label][:, 14] - target[:, 14]))))
        indices.add(int(np.argmax(target[:, 14])))
        for label, states in arrays.items():
            assert states.shape == (60001, 30) and np.isfinite(states).all()
            for index in sorted(indices):
                t = float(grid[index])
                command = next((s for s in scenario['segments'] if t < s['untilMs']), scenario['segments'][-1])
                points.append(dict(key=label + ':' + str(index), scenario=name, trace=label, sampleIndex=index, timeMs=t,
                    block=scenario['block'], currentPa=command['somaCurrentPa'], excitationNs=command['dendriteExcitatoryNs'],
                    inhibitionNs=command['dendriteInhibitoryNs'], state=states[index].tolist()))
        print('Reproduced recorded errors and selected fixed states: ' + name, flush=True)
    return dict(version='motoneuron-fixed-state-precision-v1', points=points,
                pythonVersion=sys.version, numpyVersion=np.__version__, scipyVersion=scipy.__version__)


def decimal_geometry(parameters, pi, precision):
    # Exact algebra on the declared binary64 inputs, not a higher-precision
    # anatomical model. The unit-conversion factor 100000 is an exact integer.
    with localcontext() as context:
        context.prec = precision
        sections = [parameters['soma']] + [parameters['dendrite']] * 4
        areas = [D(pi) * D(s['diameterUm']) * D(s['lengthUm']) for s in sections]
        cap = [D(.01) * a * D(s['capacitanceUfPerCm2']) for a, s in zip(areas, sections)]
        half = [Decimal(100000) * D(pi) * D(s['diameterUm']) ** 2 /
                (2 * D(s['axialResistivityOhmCm']) * D(s['lengthUm'])) for s in sections]
        coupling = [[Decimal(0) for _ in range(5)] for _ in range(5)]
        for start in (1, 3):
            total = half[0] + half[start] + half[start + 1]
            for a, b in ((0, start), (0, start + 1), (start, start + 1)):
                coupling[a][b] = coupling[b][a] = half[a] * half[b] / total
        return areas, cap, coupling


def calcium(point, parameters, constants, precision):
    with localcontext() as context:
        context.prec = precision
        y = point['state']
        density = D(parameters['calciumHigh']) * D(y[11]) * (D(y[0]) - D(constants['reversalMv']))
        entry = max(Decimal(0), -10000 * density / (2 * D(constants['faraday']) * D(constants['depthUm'])))
        removal = (D(y[14]) - D(constants['restingCalciumMm'])) / D(parameters['calciumRemovalMs'])
        return [entry - removal, entry, removal]


def audit(package):
    reference, spec, recorded = load(package)
    request = read(package / 'fixed-states.json')
    assert request == select(package), 'Fixed-state selection changed.'
    observed = read(package / 'candidate/derivatives.json')
    assert observed['completed'] and observed['inputSha256'] == digest(package / 'fixed-states.json')
    assert not observed['stateAdvanced'] and not observed['exactCheckpointReplay']
    original = read(package / 'retained/motoneuron-active-study/protocol.json')
    for key in ('parameters', 'areasUm2', 'capacitancesPf', 'couplingNs'):
        assert observed[key] == original[key], 'Candidate definitions changed: ' + key
    rhs, _, area, cap, axial = reference.source_model(package / 'motoneuron-input')
    coupling = -axial.copy()
    np.fill_diagonal(coupling, 0)
    p, constants = observed['parameters'], observed['constants']
    low = decimal_geometry(p, constants['pi'], 80)
    exact = decimal_geometry(p, constants['pi'], 120)
    geometry = []
    with localcontext() as context:
        context.prec = 120
        for label, candidate, ref, hi, lo in (
            ('areaUm2', observed['areasUm2'], area, exact[0], low[0]),
            ('capacitancePf', observed['capacitancesPf'], cap, exact[1], low[1]),
            ('couplingNs', np.asarray(observed['couplingNs']).ravel(), coupling.ravel(),
             [v for row in exact[2] for v in row], [v for row in low[2] for v in row])):
            assert max(abs(a-b) for a, b in zip(hi, lo)) < Decimal('1e-65')
            geometry.append(dict(quantity=label, candidateMaximumAbsoluteError=str(max(abs(D(a)-b) for a,b in zip(candidate,hi))),
                referenceMaximumAbsoluteError=str(max(abs(D(a)-b) for a,b in zip(ref,hi))),
                decimal80vs120MaximumDifference=str(max(abs(a-b) for a,b in zip(hi,lo)))))
        rows, rejected = [], []
        assert len(observed['points']) == len(request['points'])
        for point, result in zip(request['points'], observed['points']):
            assert result['key'] == point['key']
            y = np.asarray(point['state'])
            command = dict(somaCurrentPa=point['currentPa'], dendriteExcitatoryNs=point['excitationNs'], dendriteInhibitoryNs=point['inhibitionNs'])
            ref = rhs(point['block'], command)(point['timeMs'], y)
            if result['derivative'] is None:
                # Reference tolerance permits tiny gate excursions that the
                # candidate rejects. Retain the rejection; never clip the state.
                assert result['rejection'] and '-h' not in point['trace'], 'An archived candidate state was rejected.'
                rejected.append(dict(key=point['key'], error=result['rejection'], state=point['state'], referenceRhs=ref.tolist()))
                continue
            assert result['rejection'] is None
            candidate = np.asarray(result['derivative'])
            assert ref.shape == candidate.shape == (30,) and np.isfinite(ref).all() and np.isfinite(candidate).all()
            ca80 = calcium(point, p, constants, 80)
            ca120 = calcium(point, p, constants, 120)
            assert max(abs(a-b) for a,b in zip(ca80,ca120)) < Decimal('1e-65')
            candidate_error = [D(candidate[i])-v for i,v in zip((14,27,28), ca120)]
            reference_error = [D(ref[i])-v for i,v in zip((14,27,28), ca120)]
            precise_axial = [sum(exact[2][i][j] * (D(y[i])-D(y[j])) for j in range(5)) for i in range(5)]
            reference_axial = axial @ y[:5]
            delta = np.abs(candidate-ref)
            rows.append(dict(key=point['key'], timeMs=point['timeMs'], scenario=point['scenario'],
                maximumVoltageRhsDifferenceMvPerMs=float(delta[:5].max()), maximumGateRhsDifferencePerMs=float(delta[GATES].max()),
                calciumRhsDifferenceMmPerMs=float(delta[14]), candidateRhs=candidate.tolist(), referenceRhs=ref.tolist(),
                calciumDecimal120=[str(v) for v in ca120], candidateCalciumArithmeticErrors=[str(v) for v in candidate_error],
                referenceCalciumArithmeticErrors=[str(v) for v in reference_error],
                referenceAxialArithmeticErrorPa=[str(D(a)-b) for a,b in zip(reference_axial,precise_axial)],
                candidateCalciumLocalBiasTimesRemovalMs=str(candidate_error[0]*D(p['calciumRemovalMs'])),
                referenceCalciumLocalBiasTimesRemovalMs=str(reference_error[0]*D(p['calciumRemovalMs']))))
        eca_exact = Decimal('12.5') * Decimal(40000).ln()
        assert rows
        report = dict(completed=True, requestedPoints=len(request['points']), evaluatedPoints=len(rows),
            rejectedReferencePoints=rejected, scenarios=SCENARIOS, geometry=geometry, rows=rows,
            decimalPrecisions=[80,120], constantsUseExactBinary64Inputs=True,
            calciumReversal=dict(candidate=str(D(constants['reversalMv'])), reference=str(D(12.5*np.log(40000))),
                symbolicDecimal120=str(eca_exact)),
            maximumVoltageRhsDifferenceMvPerMs=max(r['maximumVoltageRhsDifferenceMvPerMs'] for r in rows),
            maximumGateRhsDifferencePerMs=max(r['maximumGateRhsDifferencePerMs'] for r in rows),
            maximumCalciumRhsDifferenceMmPerMs=max(r['calciumRhsDifferenceMmPerMs'] for r in rows),
            originalNumericalStatus='FAILED', originalBoundsChanged=False, odeSolved=False, causalAttributionEstablished=False,
            exactCheckpointReplay=False, activeMotorSourceQualified=False, physiologyQualified=False, avatarRecoveryAssessed=False,
            interpretation='Fixed-state differences and local bias times removal constant are diagnostics, not a trajectory error bound or proof of the failure cause.')
    return report


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('package', type=Path)
    parser.add_argument('--stage', choices=['select', 'analyse'], required=True)
    args = parser.parse_args()
    package = args.package.resolve()
    if args.stage == 'select':
        write_new(package / 'fixed-states.json', select(package))
    else:
        write_new(package / 'precision-analysis.json', audit(package))
        print('Fixed-state arithmetic diagnosis complete. Original numerical failure and biology remain unqualified.', flush=True)

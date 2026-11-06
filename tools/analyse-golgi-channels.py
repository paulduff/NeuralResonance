"""Independent SK2 propagation using SciPy expm and the pinned NMODL graph.

Does not import/execute deposited source code, access services or read brain saves.
Voltage-clamped gates are not evidence for free-cell firing or a working circuit.
"""
import argparse
import hashlib
import itertools
import json
from pathlib import Path
import re

import numpy as np
from scipy.linalg import expm


def analyse(output: Path, reference: Path):
    manifest = json.loads((reference / 'manifest.json').read_text(encoding='utf-8-sig'))
    assert manifest['revision'] == '2ffaeca558235d61222240fc74247cab1cd02fe7'
    for name, digest in manifest['sha256'].items():
        assert hashlib.sha256((reference / name).read_bytes()).hexdigest() == digest, name
    text = (reference / 'Golgi_SK2.mod').read_text(encoding='latin-1')
    block = text.split('PARAMETER {', 1)[1].split('}', 1)[0]
    params = {key: float(value) for key, value in re.findall(
        r'(?m)^\s*(\w+)\s*=\s*([-+]?\d+(?:\.\d*)?(?:[eE][-+]?\d+)?)', block)}
    states = text.split('STATE {', 1)[1].split('}', 1)[0].split()
    assert states == ['c1', 'c2', 'c3', 'c4', 'o1', 'o2']
    edges = re.findall(r'~\s*(\w+)\s*<->\s*(\w+)\s*\(\s*(\w+)\s*,\s*(\w+)\s*\)', text)
    assert len(edges) == 5
    assert 'rates(cai/diff)' in text and 'celsius -23(degC)' in text

    def generator(ca, temperature):
        matrix = np.zeros((6, 6))
        for source, target, forward_name, reverse_name in edges:
            for a, b, name in [(source, target, forward_name), (target, source, reverse_name)]:
                # Deposited _t rates apply Q10; _t_ca additionally applies local calcium/diff.
                coefficient = params[name.removesuffix('_ca').removesuffix('_t')]
                rate = coefficient * params['Q10'] ** ((temperature - 23) / 10)
                if name.endswith('_ca'):
                    rate *= ca / params['diff']
                i, j = states.index(a), states.index(b)
                matrix[j, i] += rate
                matrix[i, i] -= rate
        return matrix

    data = json.loads((output / 'channels.json').read_text(encoding='utf-8'))
    assert data['completed'] and data['caseCount'] == 36 and len(data['cases']) == 36
    assert data['sourceRevision'] == manifest['revision']
    assert data['candidateVersion'] == 'golgi-solinas-channels-v1'
    for key in ['liveBrainAccessed', 'oldSavesLoaded', 'freeMembraneAssessed', 'synapticCircuitAssessed', 'physicalRecoveryAssessed']:
        assert data[key] is False, key
    expected = set(itertools.product([23, 37], [-100, -80, -60, -40, 0, 30], [.00005, .001, .01]))
    seen = set()
    times = [0, .125, 1, 5, 25, 100, 250]
    assert data['timesMs'] == times and data['initialCalciumMm'] == .00005 and data['initialVoltageMv'] == -60
    assert data['gateOrder'] == ['NaM', 'NaH', 'NaP', 'NaRS', 'NaRF', 'Kv', 'Km', 'KaA', 'KaB',
        'HvaS', 'HvaU', 'LvaM', 'LvaH', 'Hcn1Fast', 'Hcn1Slow', 'Hcn2Fast', 'Hcn2Slow', 'Bk']
    maximum_error = maximum_mass_error = 0.0
    comparisons = []
    for case in data['cases']:
        key = (case['temperatureCelsius'], case['voltageMv'], case['calciumMm'])
        assert key in expected and key not in seen, key
        seen.add(key)
        matrix = generator(case['calciumMm'], case['temperatureCelsius'])
        initial_matrix = generator(data['initialCalciumMm'], case['temperatureCelsius'])
        initial_matrix[-1] = 1
        rhs = np.zeros(6); rhs[-1] = 1
        initial = np.linalg.solve(initial_matrix, rhs)
        assert [s['timeMs'] for s in case['samples']] == times
        for sample in case['samples']:
            sk = np.asarray(sample['sk']); gates = np.asarray(sample['gates'])
            assert sk.shape == (6,) and gates.shape == (18,)
            assert np.isfinite(sk).all() and np.isfinite(gates).all()
            assert (sk >= 0).all() and (sk <= 1).all() and (gates >= 0).all() and (gates <= 1).all()
            reference_state = expm(matrix * sample['timeMs']) @ initial
            error = float(np.max(np.abs(reference_state - sk)))
            mass_error = abs(float(sk.sum()) - 1)
            assert error < 2e-11 and mass_error < 2e-11, (key, sample['timeMs'], error, mass_error)
            currents = sample['currents']
            required = {'sodiumTransient', 'sodiumPersistent', 'sodiumResurgent', 'potassiumDelayed', 'potassiumSlow',
                'potassiumA', 'calciumHigh', 'calciumLow', 'hcn1', 'hcn2', 'bk', 'sk', 'leak', 'total'}
            assert set(currents) == required and all(np.isfinite(x) for x in currents.values())
            assert abs(sum(v for k, v in currents.items() if k != 'total') - currents['total']) < 1e-12
            maximum_error = max(maximum_error, error); maximum_mass_error = max(maximum_mass_error, mass_error)
            comparisons.append(dict(condition=key, timeMs=sample['timeMs'], skMaximumAbsoluteError=error, skMassError=mass_error))
    assert seen == expected
    report = dict(completed=True, channelAccountingValidated=True, skIndependentReferencePassed=True,
        caseCount=len(seen), comparisonCount=len(comparisons), maximumSkAbsoluteError=maximum_error,
        maximumSkMassError=maximum_mass_error, comparisons=comparisons,
        freeMembraneAssessed=False, wholeCellPhysiologyQualified=False, circuitQualified=False,
        referenceMethod='SciPy matrix exponential; NMODL graph/rates, linear-system initial equilibrium',
        limitations='Independent numerical comparison covers SK2 only. Other channels have mechanism tests, not complete NEURON trace equivalence.')
    (output / 'channel-analysis.json').write_text(json.dumps(report, indent=2, allow_nan=False) + '\n', encoding='utf-8')
    return {k: v for k, v in report.items() if k != 'comparisons'}


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('output', type=Path); parser.add_argument('reference', type=Path)
    args = parser.parse_args()
    print(json.dumps(analyse(args.output, args.reference), indent=2, allow_nan=False))

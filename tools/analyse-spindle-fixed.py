"""Fixed-length spindle comparison; new numerical work belongs to PowerShell."""
import argparse
import importlib.util
import json
import os
from pathlib import Path
import traceback

for name in ('OMP_NUM_THREADS', 'OPENBLAS_NUM_THREADS', 'MKL_NUM_THREADS', 'NUMEXPR_NUM_THREADS'):
    os.environ[name] = '1'
import numpy as np
from scipy.io import loadmat
from spindle_fixed_reference import (FIBERS, PROTOCOLS, STEPS_US, SETTINGS, CRITERIA,
    Kinetics, protocol, intervals, accounting, integrate, analytic_controls)


def read(path): return json.loads(path.read_text(encoding='utf-8-sig'))


def save(path, value):
    with path.open('x', encoding='utf-8') as stream:
        json.dump(value, stream, indent=2, allow_nan=False); stream.write('\n')


def environment(package):
    spec = importlib.util.spec_from_file_location('dependencies', package / 'source/tools/golgi-reference-resume.py')
    value = importlib.util.module_from_spec(spec); spec.loader.exec_module(value)
    return value.environment()


def native(package, fiber, name, step):
    return load_native(package / 'native' / f'{fiber}-{name}-{step}us.mat', fiber, name, step)


def load_native(path, fiber, name, step):
    """Read a completed capture, without evaluating a derivative or integrating."""
    label = f'{fiber}-{name}-{step}us'
    result = loadmat(path, simplify_cells=True)['result']
    assert result['fiber'] == fiber and result['protocol'] == name
    assert result['completed'] and result['finite'] and result['completedSteps'] == 100000 // step
    assert result['dtSeconds'] == step / 1e6 and result['durationSeconds'] == .1
    assert result['driver'] == 'isolated-imposed-length-forwardStep'
    assert result['timeConvention'] == 'initial plus completed endpoints'
    assert not result['slackControllerUsed'] and not result['upstreamODEOptionsChanged']
    model = Kinetics(result['parameters'], fiber)
    snapshots = result['snapshots']; assert len(snapshots) == 1 + 100000 // step
    times = np.asarray([s['timeSeconds'] for s in snapshots], dtype=np.float64)
    states = np.asarray([s['y'] for s in snapshots], dtype=np.float64)
    assert states.shape == (len(times), 84) and np.array_equal(states[0], model.initial())
    assert np.array_equal(times, np.arange(len(times)) * (step / 1e6))
    stress = np.asarray([s['stressPa'] for s in snapshots]); measured_cross = np.asarray([s['crossBridgeStressPa'] for s in snapshots])
    force, cross = model.stress(states)
    force_error = float(max(np.max(np.abs(force - stress)), np.max(np.abs(cross - measured_cross))))
    assert force_error <= 1e-7, 'Native force reconstruction disagrees with the preserved state.'
    for k, s in enumerate(snapshots):
        assert s['step'] == k and s['finite']
        assert s['hsLengthNm'] == s['commandLengthNm'] == 1250. and s['overlap'] == model.overlap
        # Match the original driver's multiplication, including binary64 edges.
        input_time = max(k - 1, 0) * (step / 1e6)
        expected_pca = next(pca for start, end, pca in intervals(name) if start <= input_time < end)
        assert s['pCa'] == expected_pca
        assert np.array_equal(s['reportedBinPopulations'], states[k, 1:-2])
        assert s['reportedDetached'] == states[k, 0]
        assert abs(s['reportedBound'] - states[k, 1:-2].sum()) <= 1e-12
    return dict(label=label, times=times, states=states, stress=stress, model=model,
        reconstructionErrorPa=force_error, accounting=accounting(states, model.overlap))


def validate(package):
    assert read(package / 'spindle-fixed-input.json') == protocol()
    review = read(package / 'reference/state-review.json')
    assert review['operatorReportedCompletion'] and review['canonicalStateChecksPassed']
    assert review['currentAnalysisExactlyMatchesRepairedFrozenAnalysis']
    assert review['completedTrajectories'] == 30 and review['completedBoundaryRecords'] == 2
    assert review['maximumStateDifference'] == review['maximumPostInitialStressDifferencePa'] == 0
    return [native(package, fiber, name, step) for fiber in FIBERS for name in PROTOCOLS for step in STEPS_US]


def preflight(package):
    captures = validate(package); current = environment(package)
    assert current == read(package / 'preparation-environment.json')
    assert current == read(package / 'reference/qualified-numerical-preflight.json')['environment']
    save(package / 'reference-preflight.json', dict(completed=True, environment=current, protocol=protocol(),
        captures=[dict(label=c['label'], samples=len(c['times']), forceReconstructionErrorPa=c['reconstructionErrorPa'],
            accounting=c['accounting']) for c in captures], modelExecuted=False))


def postflight(package):
    current = environment(package)
    unchanged = current == read(package / 'reference-preflight.json')['environment'] == read(package / 'preparation-environment.json')
    save(package / 'reference-postflight.json', dict(environment=current, unchanged=unchanged, modelExecuted=False))
    if not unchanged: raise RuntimeError('Numerical dependencies changed during the study.')


def controls(package):
    validate(package)
    out = package / 'reference-controls'; out.mkdir()
    model = native(package, 'bag', 'quiet-hold', 1000)['model']
    result = analytic_controls(model.p, out)
    save(out / 'controls.json', result)
    if not result['passed']: raise RuntimeError('Independent analytic controls failed; preserve evidence.')


def solve(package):
    assert read(package / 'reference-controls/controls.json')['passed']
    captures = validate(package); root = package / 'references'; root.mkdir()
    outcomes = []
    for fiber in FIBERS:
        for name in PROTOCOLS:
            selected = [c for c in captures if c['label'].startswith(f'{fiber}-{name}-')]
            assert len(selected) == 3
            times = np.unique(np.concatenate([c['times'] for c in selected]))
            for setting in SETTINGS:
                label = f'{fiber}-{name}-{setting["label"]}'; out = root / label; out.mkdir()
                print('Reference ' + label, flush=True)
                try:
                    result = integrate(selected[0]['model'], name, times, setting, out)
                    save(out / 'result.json', result)
                    outcomes.append(dict(label=label, completed=True))
                except Exception:
                    save(out / 'failure.json', dict(label=label, error=traceback.format_exc(), completed=False))
                    outcomes.append(dict(label=label, completed=False))
    save(root / 'execution.json', dict(outcomes=outcomes, completed=all(r['completed'] for r in outcomes)))
    if not all(r['completed'] for r in outcomes): raise RuntimeError('Reference solve failed; retained all available prefixes.')


def difference(a, b, model, state_bound, stress_bound):
    state_error = float(np.max(np.abs(a - b)))
    stress_error = float(np.max(np.abs(model.stress(a)[0] - model.stress(b)[0])))
    return dict(maximumStateDifference=state_error, maximumStressDifferencePa=stress_error,
        stateBound=state_bound, stressBoundPa=stress_bound,
        passed=bool(state_error <= state_bound and stress_error <= stress_bound))


def audit(package):
    captures = validate(package)
    execution = read(package / 'references/execution.json')
    assert execution['completed'] and len(execution['outcomes']) == 18
    expected_labels = {f'{f}-{p}-{s["label"]}' for f in FIBERS for p in PROTOCOLS for s in SETTINGS}
    assert {r['label'] for r in execution['outcomes']} == expected_labels
    reference_checks, candidate_checks, references = [], [], []
    controls_result = read(package / 'reference-controls/controls.json')
    assert controls_result['passed'] and len(controls_result['analytic']) == 6
    for fiber in FIBERS:
        for name in PROTOCOLS:
            selected = [c for c in captures if c['label'].startswith(f'{fiber}-{name}-')]
            times = np.unique(np.concatenate([c['times'] for c in selected])); model = selected[0]['model']
            traces = []
            for setting in SETTINGS:
                label = f'{fiber}-{name}-{setting["label"]}'; folder = package / 'references' / label
                result = read(folder / 'result.json'); assert result['completed'] and result['settings'] == setting
                with np.load(folder / 'trace.npz', allow_pickle=False) as raw:
                    assert set(raw.files) == {'times', 'states', 'internal_times', 'internal_states'}
                    assert np.array_equal(raw['times'], times)
                    states = raw['states'].copy(); internal = raw['internal_states'].copy()
                    internal_times = raw['internal_times'].copy()
                assert states.shape == (len(times), 84) and np.array_equal(states[0], model.initial())
                assert len(result['segments']) == len(intervals(name))
                offset = 0
                for index, (start, end, pca) in enumerate(intervals(name)):
                    segment = result['segments'][index]
                    assert segment['success'] and segment['startSeconds'] == start and segment['endSeconds'] == end and segment['pCa'] == pca
                    with np.load(folder / f'segment-{index}.npz', allow_pickle=False) as raw:
                        t, y = raw['times'], raw['states']
                        assert t[0] == start and t[-1] == end and (np.diff(t) > 0).all()
                        assert len(t) == segment['acceptedNodes'] and y.shape == (len(t), 84)
                        assert np.array_equal(internal_times[offset:offset + len(t)], t)
                        assert np.array_equal(internal[offset:offset + len(t)], y)
                        if offset: assert np.array_equal(internal[offset - 1], y[0])
                        else: assert np.array_equal(y[0], model.initial())
                        offset += len(t)
                assert offset == len(internal_times)
                assert np.array_equal(states[-1], internal[-1])
                sampled_check = accounting(states, model.overlap); internal_check = accounting(internal, model.overlap)
                assert sampled_check == result['sampled'] and internal_check == result['internal']
                references.append(dict(label=label, sampled=sampled_check, internal=internal_check,
                    passed=sampled_check['passed'] and internal_check['passed']))
                traces.append(states)
            for a, b in ((0, 1), (0, 2), (1, 2)):
                reference_checks.append(dict(fiber=fiber, protocol=name, first=SETTINGS[a]['label'], second=SETTINGS[b]['label'],
                    **difference(traces[a], traces[b], model, CRITERIA['referenceStateAbsolute'], CRITERIA['referenceStressAbsolutePa'])))
            for capture in selected:
                indices = np.searchsorted(times, capture['times'])
                assert np.array_equal(times[indices], capture['times'])
                for setting, trace in zip(SETTINGS, traces):
                    candidate_checks.append(dict(label=capture['label'], reference=setting['label'],
                        accounting=capture['accounting'], **difference(capture['states'], trace[indices], model,
                            CRITERIA['candidateStateAbsolute'], CRITERIA['candidateStressAbsolutePa'])))
    assert len(references) == len(reference_checks) == 18 and len(candidate_checks) == 54
    reference_passed = all(r['passed'] for r in references + reference_checks)
    candidate_passed = reference_passed and all(r['passed'] and r['accounting']['passed'] for r in candidate_checks)
    analysis = dict(completed=True, referenceAccuracyPassed=reference_passed,
        capturedFixedLengthAccuracyPassed=candidate_passed, criteria=CRITERIA,
        references=references, referenceComparisons=reference_checks, candidateComparisons=candidate_checks,
        nativeTrajectories=18, nativeRerun=False, movingLengthAssessed=False,
        slackAssessed=False, afferentFiringAssessed=False, physiologicalCalibrationAssessed=False,
        liveBodyIntegrated=False, avatarRecoveryAssessed=False)
    save(package / 'spindle-fixed-analysis.json', analysis)
    if not candidate_passed: raise RuntimeError('Fixed-length numerical criteria not all met; preserve evidence and original bounds.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(); parser.add_argument('package', type=Path)
    parser.add_argument('--stage', required=True, choices=('preflight', 'postflight', 'controls', 'solve', 'audit'))
    args = parser.parse_args(); globals()[args.stage](args.package.resolve())

"""Saved-state active slack diagnostic; execute controls/searches in PowerShell."""
import argparse
import importlib.util
import json
import os
from pathlib import Path
from collections import Counter

for name in ('OMP_NUM_THREADS', 'OPENBLAS_NUM_THREADS', 'MKL_NUM_THREADS', 'NUMEXPR_NUM_THREADS'):
    os.environ[name] = '1'
import numpy as np
from scipy.io import loadmat
from spindle_fixed_reference import Kinetics, accounting
from spindle_slack_roots import (FIBERS, PROTOCOLS, STEPS_US, CRITERIA, protocol,
    overlap, reference_force, check_landscape)


def read(path): return json.loads(path.read_text(encoding='utf-8-sig'))


def save(path, data):
    with path.open('x', encoding='utf-8') as stream:
        json.dump(data, stream, indent=2, allow_nan=False); stream.write('\n')


def environment(package):
    spec = importlib.util.spec_from_file_location('dependencies', package / 'source/tools/golgi-reference-resume.py')
    module = importlib.util.module_from_spec(spec); spec.loader.exec_module(module)
    return module.environment()


def imposed(name, t):
    excursion = 20. if name == 'stretch-shorten' else -250.
    if .02 < t <= .04: return 1250. + excursion * (t - .02) / .02
    if .04 < t <= .06: return 1250. + excursion
    if .06 < t <= .08: return 1250. + excursion - excursion * (t - .06) / .02
    return 1250.


def load_native(path, fiber, name, step):
    """Read completed observations; no kinetics, root solve or new trajectory."""
    result = loadmat(path, simplify_cells=True)['result']
    assert result['fiber'] == fiber and result['protocol'] == name
    assert result['completed'] and result['finite'] and result['completedSteps'] == 100000 // step
    assert result['dtSeconds'] == step / 1e6 and result['durationSeconds'] == .1
    assert result['driver'] == 'isolated-imposed-length-forwardStep'
    assert result['timeConvention'] == 'initial plus completed endpoints'
    assert not result['slackControllerUsed'] and not result['upstreamODEOptionsChanged']
    p = result['parameters']; model = Kinetics(p, fiber)
    snapshots = result['snapshots']; count = 1 + 100000 // step
    assert len(snapshots) == count
    states = np.asarray([s['y'] for s in snapshots]); assert states.shape == (count, 84)
    assert np.array_equal(states[0], model.initial()) and np.isfinite(states).all()
    max_force = 0.; max_geometry = 0.; checks = []
    for k, s in enumerate(snapshots):
        assert s['step'] == k and s['timeSeconds'] == k * (step / 1e6) and s['finite']
        length = s['hsLengthNm']; geometry = overlap(p, length)
        assert s['commandLengthNm'] == length and abs(length-imposed(name, s['timeSeconds'])) <= 1e-12
        max_geometry = max(max_geometry, abs(geometry-s['overlap']))
        assert s['pCa'] == 6.5
        assert np.array_equal(s['reportedBinPopulations'], states[k, 1:-2])
        assert s['reportedDetached'] == states[k, 0]
        assert abs(s['reportedBound'] - states[k, 1:-2].sum()) <= 1e-12
        force = reference_force(p, states[k], length, length)
        max_force = max(max_force, abs(force-s['stressPa']))
        checks.append(accounting(states[k:k+1], geometry))
    assert max_force <= CRITERIA['forceAbsolutePa'] and max_geometry <= 1e-12
    assert all(c['passed'] for c in checks)
    return dict(label=path.stem, parameters=p, snapshots=snapshots, states=states,
        forceReconstructionErrorPa=max_force, geometryReconstructionError=max_geometry)


def validate(package):
    assert read(package / 'spindle-slack-input.json') == protocol()
    state = read(package / 'reference/state-review.json')
    assert state['operatorReportedCompletion'] and state['canonicalStateChecksPassed']
    assert state['currentAnalysisExactlyMatchesRepairedFrozenAnalysis'] and state['completedTrajectories'] == 30
    fixed = read(package / 'reference/fixed-review.json')
    assert fixed['operatorReportedCompletion'] and fixed['savedAnalysisExactlyReproduced']
    assert fixed['capturedFixedLengthAccuracyPassed'] and fixed['candidateComparisons'] == 54
    failure=read(package/'reference/slack-failure-review.json')
    assert failure['operatorReportedCompletion'] and failure['duplicateKnotRootDiagnosed']
    assert failure['originalStatus']=='FAILED' and failure['savedStateQueries']==1556
    rows = [load_native(package / 'native' / f'{f}-{p}-{s}us.mat', f, p, s)
            for f in FIBERS for p in PROTOCOLS for s in STEPS_US]
    assert sum(len(r['snapshots']) for r in rows) == 2812
    return rows


def preflight(package):
    captures = validate(package); current = environment(package)
    assert current == read(package / 'preparation-environment.json')
    assert current == read(package / 'reference/qualified-numerical-preflight.json')['environment']
    save(package / 'reference-preflight.json', dict(completed=True, environment=current, protocol=protocol(),
        captures=[dict(label=c['label'], states=len(c['states']), forceReconstructionErrorPa=c['forceReconstructionErrorPa'],
                       geometryReconstructionError=c['geometryReconstructionError']) for c in captures],
        modelExecuted=False, rootSearchExecuted=False))


def postflight(package):
    current = environment(package)
    unchanged = current == read(package / 'reference-preflight.json')['environment'] == read(package / 'preparation-environment.json')
    save(package / 'reference-postflight.json', dict(environment=current, unchanged=unchanged, modelExecuted=False))
    if not unchanged: raise RuntimeError('Numerical dependencies changed during the study.')


def controls(package):
    rows = validate(package); out = package / 'controls'; out.mkdir()
    parameters = {fiber: next(r['parameters'] for r in rows if r['label'].startswith(fiber+'-')) for fiber in FIBERS}
    cases = []
    for fiber in FIBERS:
        p = parameters[fiber]; y = np.zeros(84); y[0] = y[-2] = 1.
        cases.append((fiber+'-passive', p, y, 1250., 0., [p['hsl_slack']], False))
    p = parameters['bag']; y = np.zeros(84); y[0] = y[-2] = 1.
    cases.append(('bag-passive-load', p, y.copy(), 1250., 1000., [p['hsl_slack'] + 1000./p['k_passive']], False))
    cases.append(('bag-passive-knot', p, y.copy(), 1090., 0., [p['hsl_slack']], False))
    p = parameters['bag']; y = np.zeros(84); y[0]=.8; y[41]=.2; y[-2]=.6; y[-1]=.4
    # A central occupied bin gives three closed-form roots: passive tail,
    # boundary taper during loss from the finite output grid, and translated head.
    value = 69000.*.2*2.5 + p['k_passive']*(1250.-p['hsl_slack'])
    slope = 69000.*.2*.5 + p['k_passive']
    taper_start = 1250.-41.
    taper_root = taper_start - p['k_passive']*(taper_start-p['hsl_slack'])/(-69000.*.2*17.5+p['k_passive'])
    expected = sorted([p['hsl_slack'], taper_root, 1250.-value/slope])
    cases.append(('bag-central-bin-three-roots', p, y.copy(), 1250., 0., expected, False))
    # A bin on the source support boundary has jumps. On displacement (j,j+1),
    # its only retained weight is .2*(dx-j) at output x=-20+.5*(j+1).
    # Enumerate the resulting scalar affine roots analytically, not by sampling
    # or treating a sign change across a jump as a zero.
    y[41]=0.; y[1]=.2; expected=[p['hsl_slack']]
    # In (-1,0) nm displacement, the left output endpoint retains .2*(1+dx).
    edge_start=1250.-1.
    expected.append(edge_start-p['k_passive']*(edge_start-p['hsl_slack'])/(-69000.*.2*17.5+p['k_passive']))
    for j in range(80):
        weight=-17.5+.5*(j+1)
        gradient=69000.*.2*weight+p['k_passive']
        if gradient:
            delta=j-p['k_passive']*(1250.+j-p['hsl_slack'])/gradient
            if j < delta < j+1: expected.append(1250.+delta)
    cases.append(('bag-edge-bin-discontinuous-roots', p, y.copy(), 1250., 0., sorted(expected), True))
    # Two genuine roots straddling one knot, both within the old 1e-9 nm bound.
    # A proximity-based deduplicator would incorrectly turn these into one.
    y=np.zeros(84); mass=14400./1207500.+1e-14
    y[0]=1.-mass; y[41]=mass; y[-2]=.6; y[-1]=.4
    valley=1210.; value=-69000.*mass*17.5+p['k_passive']*(valley-p['hsl_slack'])
    left=-69000.*mass*17.5+p['k_passive']; right=69000.*mass*.5+p['k_passive']
    expected=sorted([p['hsl_slack'],valley-value/left,valley-value/right])
    cases.append(('two-distinct-near-knot-roots',p,y.copy(),1250.,0.,expected,False))
    saved=next(r for r in rows if r['label']=='chain-stretch-shorten-500us')
    original=read(package/'reference/slack-first-failure.json'); step=original['snapshotStep']; assert step==48
    expected=[r['lengthNm'] for r in original['result']['roots'] if r['kind']=='open-interval']
    cases.append(('saved-step48-near-knot-regression',saved['parameters'],saved['states'][step].copy(),
                  saved['snapshots'][step]['hsLengthNm'],0.,expected,False))
    outcomes = []
    for label, p, y, length, target, expected, require_jump in cases:
        result = check_landscape(p, y, length, target)
        actual = [r['lengthNm'] for r in result['roots']]
        exact = len(expected) == len(actual) and all(abs(a-b) <= CRITERIA['rootAbsoluteNm'] for a,b in zip(actual, expected))
        jump = any(b['signChangeAcrossJump'] for b in result['boundaries'])
        if label=='two-distinct-near-knot-roots':
            close=[r for r in result['roots'] if abs(r['lengthNm']-1210.)<1e-9]
            exact &= len(close)==2 and close[0]['lengthNm']<1210.<close[1]['lengthNm']
            exact &= all(r['kind']=='open-interval' and r.get('nearKnotCertified') for r in close)
        if label=='saved-step48-near-knot-regression':
            exact &= any(r['lengthNm']==1200. for r in result['nonzeroNearKnots'])
            exact &= not any(r['kind']=='knot' and r['lengthNm']==1200. for r in result['roots'])
        record = dict(label=label, expectedRootsNm=expected, result=result,
                      passed=bool(result['passed'] and exact and (jump or not require_jump)))
        save(out / (label+'.json'), record); outcomes.append(dict(label=label, passed=record['passed']))
    report = dict(completed=True, passed=all(x['passed'] for x in outcomes), cases=outcomes,
                  physiologicalControls=False, kineticsExecuted=False)
    save(out / 'controls.json', report)
    if not report['passed']: raise RuntimeError('Analytic force/root controls failed; preserve evidence and original bounds.')


def analyse(package):
    assert read(package / 'controls/controls.json')['passed']
    rows = validate(package); out = package / 'landscapes'; out.mkdir()
    summaries = []; total = 0; passed = True
    for row in rows:
        p = row['parameters']; counts = Counter(); admissible_counts = Counter()
        negative = 0; jumps = 0; max_residual = 0.; max_force_error = 0.; max_root_error = 0.
        all_passed = True; filename = out / (row['label']+'.jsonl')
        with filename.open('x', encoding='utf-8') as stream:
            for s, y in zip(row['snapshots'], row['states']):
                result = check_landscape(p, y, s['hsLengthNm'])
                roots = result['roots']; counts[len(roots)] += 1
                admissible_counts[sum(int(r['admissibleState']) for r in roots)] += 1
                negative += int(s['stressPa'] < 0)
                discontinuities = [b for b in result['boundaries'] if b['jumpPa'] > CRITERIA['forceAbsolutePa']]
                jumps += int(any(b['signChangeAcrossJump'] for b in discontinuities))
                max_residual = max(max_residual, result['maximumRootResidualPa'])
                max_force_error = max(max_force_error, result['maximumForceDifferencePa'])
                if result['maximumRootDifferenceNm'] is not None: max_root_error = max(max_root_error, result['maximumRootDifferenceNm'])
                all_passed &= result['passed']; total += 1
                saved = {k:v for k,v in result.items() if k not in ('segments','boundaries')}
                saved.update(step=int(s['step']), timeSeconds=float(s['timeSeconds']),
                    originalLengthNm=float(s['hsLengthNm']), originalStressPa=float(s['stressPa']),
                    discontinuities=discontinuities)
                stream.write(json.dumps(saved, allow_nan=False)+'\n'); stream.flush()
                if not result['passed']:
                    save(out / (row['label']+'-first-failure.json'), dict(snapshotStep=int(s['step']), result=result))
                    break  # Preserve the first numerical discrepancy without pretending full coverage.
        summary = dict(label=row['label'], capturedStates=len(row['states']), checkedStates=sum(counts.values()),
            negativeOriginalStressStates=negative, rootCountHistogram=dict(counts),
            admissibleRootCountHistogram=dict(admissible_counts), statesWithDiscontinuousSignChange=jumps,
            maximumRootResidualPa=max_residual, maximumForceDifferencePa=max_force_error,
            maximumRootDifferenceNm=max_root_error, passed=bool(all_passed))
        summaries.append(summary); passed &= all_passed
        print(json.dumps(summary), flush=True)
        if not all_passed: break
    report = dict(completed=len(summaries)==12 and total==2812, statesChecked=total,
        forceLandscapeNumericsPassed=bool(passed and total==2812), trajectories=summaries,
        criteria=CRITERIA, originalBoundsChanged=False, nativeRerun=False, kineticsExecuted=False,
        slackTrajectoryAssessed=False, movingTrajectoryAccuracyAssessed=False, rootSelected=False,
        afferentFiringAssessed=False, physiologyQualified=False, avatarRecoveryAssessed=False)
    save(package / 'spindle-slack-analysis.json', report)
    if not report['forceLandscapeNumericsPassed']: raise RuntimeError('Force landscape checks failed or incomplete; preserve original bounds.')


if __name__ == '__main__':
    if not __debug__: raise RuntimeError('Assertions must remain enabled.')
    parser=argparse.ArgumentParser(); parser.add_argument('package', type=Path)
    parser.add_argument('--stage', required=True, choices=('preflight','postflight','controls','analyse'))
    args=parser.parse_args(); globals()[args.stage](args.package.resolve())

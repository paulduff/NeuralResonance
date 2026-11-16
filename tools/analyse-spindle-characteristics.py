# SPDX-License-Identifier: AGPL-3.0-only
"""Execute retained-interval comparisons only in the separate visible window."""
import argparse
import importlib.util
import json
import os
from pathlib import Path

for key in ('OMP_NUM_THREADS', 'OPENBLAS_NUM_THREADS', 'MKL_NUM_THREADS', 'NUMEXPR_NUM_THREADS'):
    os.environ[key] = '1'
import numpy as np
from spindle_characteristic_transport import CRITERIA, RetainedPopulation, protocol
from spindle_population_transport import SHIFTS_NM, overlap_reference


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    value = importlib.util.module_from_spec(spec); spec.loader.exec_module(value); return value


old = module('characteristic_saved_inputs', Path(__file__).with_name('analyse-spindle-population-transport.py'))
read, save, encode = old.read, old.save, old.encode


def preflight(package):
    assert read(package / 'characteristic-input.json') == protocol()
    old.preflight(package)


def compare(source, shift):
    before = np.asarray(source).copy(); candidate = RetainedPopulation(source)
    c = candidate.move_to(shift); masses = candidate.observe()
    reference = overlap_reference(source, shift)
    errors = dict(populationL1=float(np.sum(abs(masses - reference['masses']))),
        fluxError=max(abs(c[k] - reference[k]) for k in ('leftOutflow', 'rightOutflow')),
        forceErrorPa=abs(c['forcePa'] - reference['continuumForcePa']),
        conservationResidual=abs(c['conservationResidual']),
        domainViolation=max(0., -float(np.min(masses)), -c['leftOutflow'], -c['rightOutflow']))
    passed = (errors['populationL1'] <= CRITERIA['populationAbsolute'] and
        errors['fluxError'] <= CRITERIA['populationAbsolute'] and errors['forceErrorPa'] <= CRITERIA['forceAbsolutePa'] and
        errors['conservationResidual'] <= CRITERIA['conservationAbsolute'] and errors['domainViolation'] <= CRITERIA['domainAbsolute'])
    assert np.array_equal(source, before)
    return dict(candidate=c, candidateMasses=masses, reference=reference, errors=errors, passed=bool(passed))


def controls(package):
    out = package / 'controls'; out.mkdir(); records = []
    cases = [('stationary', 40, .2, 0., {40:.2}, 0., 0.),
        ('half-cell', 40, .2, .25, {40:.1,41:.1}, 0., 0.),
        ('right-partial', 80, .2, .25, {80:.1}, 0., .1),
        ('left-partial', 0, .2, -.25, {0:.1}, .1, 0.),
        ('right-full', 80, .2, .5, {}, 0., .2),
        ('left-full', 0, .2, -.5, {}, .2, 0.),
        ('multicell', 40, .2, 1.125, {42:.15,43:.05}, 0., 0.),
        ('whole-domain', 40, .2, 41., {}, 0., .2),
        ('empty', 40, 0., .125, {}, 0., 0.)]
    for label, index, amount, shift, wanted, left, right in cases:
        source = np.zeros(81); source[index] = amount
        result = compare(source, shift); desired = np.zeros(81)
        for i, value in wanted.items(): desired[i] = value
        assert result['passed'] and np.sum(abs(result['candidateMasses'] - desired)) <= CRITERIA['populationAbsolute']
        assert abs(result['candidate']['leftOutflow'] - left) <= CRITERIA['populationAbsolute']
        assert abs(result['candidate']['rightOutflow'] - right) <= CRITERIA['populationAbsolute']
        save(out / (label + '.json'), result); records.append(dict(label=label, passed=True))
    source = np.zeros(81); source[40] = .2; c = RetainedPopulation(source)
    source[:] = 0; c.move_to(.125)
    before = json.dumps(c.snapshot(), default=encode, sort_keys=True)
    _ = c.observe(); _ = c.observe(np.linspace(-20.25, 20.25, 1001)); detached = c.snapshot()
    detached['leftOriginNm'][:] = 999
    assert json.dumps(c.snapshot(), default=encode, sort_keys=True) == before
    c.move_to(0)
    expected = np.zeros(81); expected[40] = .2
    assert np.sum(abs(c.observe() - expected)) <= CRITERIA['populationAbsolute']
    assert abs(c.summary()['secondMomentNm2'] - .2 * .5**2 / 12) <= CRITERIA['secondMomentAbsoluteNm2']
    records.append(dict(label='readout-and-return-preserve-shape', passed=True, state=c.snapshot()))
    source[-1] = .2; c = RetainedPopulation(source); c.move_to(.25); c.move_to(0)
    s = c.summary()
    assert abs(s['retained'] - .1) <= CRITERIA['populationAbsolute'] and abs(s['rightOutflow'] - .1) <= CRITERIA['populationAbsolute']
    assert abs(s['forcePa'] - 69000 * .1 * (19.875 + 2.5)) <= CRITERIA['forceAbsolutePa']
    c.move_to(41); c.move_to(-41); c.move_to(0)
    assert abs(c.summary()['retained']) <= CRITERIA['populationAbsolute']
    assert abs(c.summary()['rightOutflow'] - .2) <= CRITERIA['populationAbsolute']
    assert abs(c.summary()['leftOutflow']) <= CRITERIA['populationAbsolute']
    records.append(dict(label='escaped-cohorts-never-reappear', passed=True, state=c.snapshot()))
    before = json.dumps(c.snapshot(), default=encode, sort_keys=True)
    rejected = 0
    for action in (lambda: c.move_to(float('nan')), lambda: c.observe([0., 0.]), lambda: RetainedPopulation([float('inf')])):
        try: action()
        except ValueError: rejected += 1
    assert rejected == 3 and json.dumps(c.snapshot(), default=encode, sort_keys=True) == before
    records.append(dict(label='invalid-input-preserves-state', passed=True))
    assert len(records) == 12
    save(out / 'controls.json', dict(completed=True, passed=True, cases=records))


def matrix(package):
    rows, _ = old.inputs(package); out = package / 'matrix'; out.mkdir(); summaries = []; total = 0
    assert read(package / 'controls/controls.json')['passed']
    for row in rows:
        records = []; candidates = []; references = []
        with (out / (row['label'] + '.jsonl')).open('x', encoding='utf-8') as stream:
            for state in row['states']:
                for shift in SHIFTS_NM:
                    result = compare(state[1:-2], shift)
                    candidates.append(result.pop('candidateMasses')); references.append(result['reference'].pop('masses'))
                    result['shiftNm'] = shift; result['stateIndex'] = len(records) // len(SHIFTS_NM)
                    stream.write(json.dumps(result, default=encode, allow_nan=False) + '\n'); stream.flush()
                    records.append(result)
                    if not result['passed']: break
                if not records[-1]['passed']: break
        np.savez_compressed(out / (row['label'] + '.npz'), candidateMasses=candidates, referenceMasses=references)
        total += len(records)
        summary = dict(label=row['label'], queries=len(records), passed=all(r['passed'] for r in records),
            maximumPopulationL1=max(r['errors']['populationL1'] for r in records),
            maximumForceErrorPa=max(r['errors']['forceErrorPa'] for r in records))
        summaries.append(summary); print(json.dumps(summary), flush=True)
        if not summary['passed']: break
    report = dict(completed=total == 25308, queries=total, cases=summaries,
        passed=total == 25308 and all(r['passed'] for r in summaries))
    save(package / 'matrix-analysis.json', report)
    if not report['passed']: raise RuntimeError('Retained-interval matrix failed; preserve bounds.')


def continuity(package):
    rows, review = old.inputs(package); lookup = {r['label']:r for r in rows}; cases = []
    eps = 1e-8
    for e in review['noRootEvidence']:
        source = lookup[e['trajectory']]['states'][e['step']][1:-2]
        for jump in e['discontinuities']:
            if not jump['signChangeAcrossJump']: continue
            q = (jump['lengthNm'] - e['originalLengthNm']) * .5
            points = [compare(source, q + delta) for delta in (-eps, 0., eps)]
            span = abs(points[-1]['candidate']['forcePa'] - points[0]['candidate']['forcePa'])
            bound = 69000 * np.sum(abs(source)) * (1 + 22.75 / .5) * 2 * eps + 2 * CRITERIA['forceAbsolutePa']
            cases.append(dict(trajectory=e['trajectory'], step=e['step'], shiftNm=q, forceSpanPa=span,
                boundPa=float(bound), points=points, passed=all(p['passed'] for p in points) and span <= bound))
    expected = sum(int(j['signChangeAcrossJump']) for e in review['noRootEvidence'] for j in e['discontinuities'])
    assert len(cases) == expected and len(review['noRootEvidence']) == 31
    save(package / 'continuity-analysis.json', dict(completed=True, cases=cases, probes=len(cases),
        passed=all(c['passed'] for c in cases), rootsRecomputed=False))
    if not all(c['passed'] for c in cases): raise RuntimeError('Retained-force continuity failed.')


def histories(package):
    out = package / 'histories'; out.mkdir(); spec = protocol(); summaries = []; groups = {}
    profiles = [('interior', -4.25, 3.75, 1.25), ('right-edge', 19.25, 20.25, .375), ('left-edge', -20.25, -19.25, -.375)]
    for half in spec['halfRangesNm']:
        for width in spec['widthsNm']:
            edges = -half + width * np.arange(round(2 * half / width) + 1)
            for name, a, b, excursion in profiles:
                density = .2 / (b - a); initial = old.rectangle(edges, a, b, density)
                for path in ('one-way', 'return'):
                    for steps in spec['steps']:
                        positions = list(np.linspace(0, excursion, steps + 1))
                        if path == 'return': positions += list(np.linspace(excursion, 0, steps + 1)[1:])
                        c = RetainedPopulation(initial, -half, width); low = high = 0.; records = []; arrays = []; exacts = []
                        lefts = []; rights = []
                        for index, position in enumerate(positions):
                            state = c.move_to(float(position)); masses = c.observe()
                            low = min(low, position); high = max(high, position)
                            # Independent characteristic solution of one continuous rectangle.
                            start = max(a, -half - low); end = min(b, half - high)
                            exact = old.rectangle(edges, start + position, end + position, density)
                            retained = max(0., end - start) * density
                            first = retained * (start + end + 2 * position) * .5
                            second = retained * ((start + position)**2 + (start + position)*(end + position) + (end + position)**2) / 3
                            errors = dict(populationL1=float(np.sum(abs(masses - exact))),
                                forceErrorPa=abs(state['forcePa'] - 69000 * (first + 2.5 * retained)),
                                secondMomentErrorNm2=abs(state['secondMomentNm2'] - second),
                                fluxError=max(abs(state['leftOutflow'] - max(0., start-a)*density),
                                              abs(state['rightOutflow'] - max(0., b-end)*density)),
                                domainViolation=max(0., -float(np.min(masses)), -state['leftOutflow'], -state['rightOutflow']))
                            passed = (errors['populationL1'] <= CRITERIA['populationAbsolute'] and
                                errors['forceErrorPa'] <= CRITERIA['forceAbsolutePa'] and errors['secondMomentErrorNm2'] <= CRITERIA['secondMomentAbsoluteNm2'] and
                                errors['fluxError'] <= CRITERIA['populationAbsolute'] and errors['domainViolation'] <= CRITERIA['domainAbsolute'] and
                                abs(state['conservationResidual']) <= CRITERIA['conservationAbsolute'])
                            records.append(dict(index=index, **state, exactRetained=retained, **errors, passed=bool(passed)))
                            arrays.append(masses); exacts.append(exact); snapshot=c.snapshot()
                            lefts.append(snapshot['leftOriginNm']); rights.append(snapshot['rightOriginNm'])
                        label = f'{name}-{path}-range{half}-width{width}-steps{steps}'
                        arrays = np.asarray(arrays)
                        np.savez_compressed(out / (label + '.npz'), edgesNm=edges, initialMasses=initial,
                            candidateHistoryMasses=arrays, exactHistoryMasses=exacts, leftOriginHistoryNm=lefts, rightOriginHistoryNm=rights)
                        save(out / (label + '.json'), records)
                        summary = dict(label=label, profile=name, path=path, halfRangeNm=half, widthNm=width, steps=steps,
                            endpoints=len(records), maximumPopulationL1=max(r['populationL1'] for r in records),
                            maximumForceErrorPa=max(r['forceErrorPa'] for r in records),
                            maximumSecondMomentErrorNm2=max(r['secondMomentErrorNm2'] for r in records),
                            passed=all(r['passed'] for r in records))
                        summaries.append(summary); groups.setdefault((half,width,name,path), []).append((steps, arrays, records))
    cadence = []
    for key, group in groups.items():
        coarse_steps, coarse, original = group[0]
        for steps, fine, records in group[1:]:
            stride = steps // coarse_steps; shared = fine[::stride]; paired = records[::stride]
            assert len(shared) == len(coarse) and len(paired) == len(original)
            population = float(np.max(np.sum(abs(shared - coarse), axis=1)))
            force = max(abs(a['forcePa'] - b['forcePa']) for a,b in zip(original,paired))
            moment = max(abs(a['secondMomentNm2'] - b['secondMomentNm2']) for a,b in zip(original,paired))
            flux = max(abs(a[k]-b[k]) for a,b in zip(original,paired) for k in ('leftOutflow','rightOutflow'))
            cadence.append(dict(case=list(key),coarseSteps=coarse_steps,fineSteps=steps,populationL1=population,
                forceDifferencePa=force,secondMomentDifferenceNm2=moment,outflowDifference=flux,
                passed=population<=CRITERIA['populationAbsolute'] and force<=CRITERIA['forceAbsolutePa'] and
                    moment<=CRITERIA['secondMomentAbsoluteNm2'] and flux<=CRITERIA['populationAbsolute']))
    report = dict(completed=len(summaries)==144 and len(cadence)==96, cases=summaries, cadence=cadence,
        passed=len(summaries)==144 and len(cadence)==96 and all(r['passed'] for r in summaries+cadence))
    save(package / 'history-analysis.json', report)
    if not report['passed']: raise RuntimeError('Exact shape/moment/history criteria failed; preserve bounds.')


def summarise(package):
    reports = {key:read(package / (key+'-analysis.json')) for key in ('matrix','continuity','history')}
    control = read(package / 'controls/controls.json')
    assert control['completed'] and control['passed'] and len(control['cases'])==12
    assert all(r['completed'] and r['passed'] for r in reports.values())
    save(package / 'characteristic-analysis.json', dict(completed=True, transportNumericsPassed=True,
        analyticControls=12, savedStateQueries=reports['matrix']['queries'], noRootStatesProbed=31,
        continuityProbes=reports['continuity']['probes'], historyCases=len(reports['history']['cases']),
        cadenceComparisons=len(reports['history']['cadence']), criteria=CRITERIA,
        originalBoundsChanged=False, kineticsExecuted=False, rootSelected=False, slackTrajectoryAssessed=False,
        sensoryFiringAssessed=False, physiologyQualified=False, avatarRecoveryAssessed=False))


if __name__=='__main__':
    if not __debug__: raise RuntimeError('Assertions must remain enabled.')
    parser=argparse.ArgumentParser(); parser.add_argument('package',type=Path)
    parser.add_argument('--stage',required=True,choices=['preflight','controls','matrix','continuity','histories','summarise','postflight'])
    args=parser.parse_args()
    (old.postflight if args.stage=='postflight' else globals()[args.stage])(args.package.resolve())

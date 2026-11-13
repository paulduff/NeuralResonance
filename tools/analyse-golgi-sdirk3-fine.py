"""Audit 14 finer traces against unchanged references and 38 preserved coarse cases."""
from hashlib import sha256
import importlib.util
import json
import os
from pathlib import Path
for name in ('OPENBLAS_NUM_THREADS', 'OMP_NUM_THREADS', 'MKL_NUM_THREADS'):
    os.environ[name] = '1'
import numpy as np
import scipy.integrate
from golgi_sdirk3_fine_protocol import protocol
from golgi_sdirk_protocol import protocol as original_protocol
from golgi_environment_transition import controls, validate_analyzer_change, validate_transition

read = lambda p: json.loads(p.read_text(encoding='utf-8-sig'))


def digest(path):
    value = sha256()
    path = path.absolute()
    if os.name == 'nt' and not str(path).startswith('\\\\?\\'):
        path = Path('\\\\?\\'+str(path))
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(4*1024*1024), b''): value.update(block)
    return value.hexdigest().upper()


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    value = importlib.util.module_from_spec(spec); spec.loader.exec_module(value)
    return value


def validate(package):
    config = read(package/'configuration.json')
    assert config['SolverOrder'] == 3 and config['ExpectedConditions'] == 14 and config['ExpectedBaselineConditions'] == 38
    assert config['NoLiveMonitoring'] and config['BaselineReviewed'] and config['ReusePassiveReferences']
    assert config['EnvironmentTransitionReviewed']
    for entry in config['PackageFiles']:
        assert digest(package/entry['Path']) == entry['Sha256'], entry['Path']
    spec = read(package/'sdirk-input.json'); assert spec == protocol()
    baseline = package/'baseline'
    assert read(baseline/'sdirk-input.json') == original_protocol(3)
    assert read(baseline/'configuration.json')['Revision'] == 'faac10d04c87d48155e33946edf6a6cdb1ded763'
    provenance = read(package/'provenance.json')
    assert provenance['review']['originalAnalysisReproduced'] and provenance['review']['completedConditions'] == 38
    assert digest(baseline/'backup-manifest.json') == provenance['review']['manifestSha256']
    for path in provenance['unchangedSources']:
        assert (package/'source'/path).read_text(encoding='utf-8-sig') == (baseline/'source'/path).read_text(encoding='utf-8-sig'), path
    validate_analyzer_change((baseline/'source/tools/analyse-golgi-sdirk.py').read_text(encoding='utf-8-sig'),
                             (package/'source/tools/analyse-golgi-sdirk.py').read_text(encoding='utf-8-sig'))
    assert provenance['retainedAnalyzerChange'] == 'explicit-replay-environment-only'
    validate_transition(read(baseline/'reference-preflight.json')['environment'],
                        read(package/'declared-environment.json'), read(package/'environment-review.json'))
    marker = '        Directory.CreateDirectory(output);'
    original = (baseline/'source/tools/GolgiCellProbe/SdirkStudy.cs').read_text(encoding='utf-8-sig')
    candidate = (package/'source/tools/GolgiCellProbe/SdirkStudy.cs').read_text(encoding='utf-8-sig')
    assert original.count(marker) == candidate.count(marker) == 1
    assert original.split(marker)[1] == candidate.split(marker)[1], 'Cell execution/sampling loop changed.'
    # Only retained arrays may supply references. Missing evidence cannot trigger
    # an unannounced replacement solve in either preflight or saved-array review.
    def prohibited_solve(*args, **kwargs):
        raise RuntimeError('This refinement must reuse preserved references; no ODE reference solve permitted.')
    scipy.integrate.solve_ivp = prohibited_solve
    base = module('retained_sdirk_analysis', package/'source/tools/analyse-golgi-sdirk.py')
    return spec, baseline, base


def preflight(package):
    spec, baseline, base = validate(package)
    environment_controls = controls()
    declared = read(package/'declared-environment.json')
    environment = base.module('fine_environment', package/'source/tools/golgi-reference-resume.py').environment()
    with (package/'observed-preflight-environment.json').open('x', encoding='utf-8') as stream:
        stream.write(json.dumps(environment, indent=2)+'\n')
    with (package/'environment-controls.json').open('x', encoding='utf-8') as stream:
        stream.write(json.dumps(dict(passed=True, controls=environment_controls), indent=2)+'\n')
    assert environment == declared, 'Current dependencies differ from the explicitly reviewed transition.'
    transition = validate_transition(read(baseline/'reference-preflight.json')['environment'], declared,
                                     read(package/'environment-review.json'))
    analysis = base.audit(baseline, replay_environment=declared)
    assert analysis == read(baseline/'sdirk-analysis.json')
    assert analysis['completedCases'] == 38 and not analysis['numericalCriteriaPassed']
    assert analysis['criteria'] == spec['criteria'] and all(r['physicalPassed'] for r in analysis['cases'])
    assert all(all(r['passed'] for r in a['refinement']) for a in analysis['assessment'])
    assert all(a.get('nonlinearSensitivityPassed', True) for a in analysis['assessment'])
    report = dict(completed=True, originalAnalysisReproduced=True, originalBoundsChanged=False,
                  baselineConditions=38, newConditions=14, originalNumericalStatus='FAILED',
                  baselineAnalysisSha256=digest(baseline/'sdirk-analysis.json'), environment=environment,
                  environmentTransition=transition, environmentControls=environment_controls,
                  savedAnalysisCompatibleWithDeclaredRuntime=True, originalRuntimeReproduced=False,
                  referenceSolved=False, physiologyQualified=False, avatarRecoveryAssessed=False)
    with (package/'fine-preflight.json').open('x', encoding='utf-8') as stream:
        stream.write(json.dumps(report, indent=2)+'\n')
    print('Preserved 38-condition failed result reproduced exactly; unchanged references and bounds retained.', flush=True)


def audit(package):
    spec, baseline, base = validate(package)
    pre = read(package/'fine-preflight.json')
    assert pre['completed'] and pre['originalAnalysisReproduced'] and pre['baselineConditions'] == 38
    assert pre['environment'] == read(package/'declared-environment.json')
    assert pre['savedAnalysisCompatibleWithDeclaredRuntime'] and not pre['originalRuntimeReproduced']
    assert len(pre['environmentControls']) == 9
    assert digest(baseline/'sdirk-analysis.json') == pre['baselineAnalysisSha256']
    original = read(baseline/'sdirk-analysis.json'); criteria = spec['criteria']
    assert original['criteria'] == criteria
    refs, uncertainty = base.load_references(baseline, spec)
    assert uncertainty == original['referenceUncertainty']
    passive, cap, passive_checks = base.load_passive(baseline)
    metadata, outcomes = (read(package/'study'/name) for name in ('protocol.json', 'results.json'))
    assert metadata['inputSha256'] == digest(package/'sdirk-input.json')
    assert metadata['specification'] == {key: spec[key] for key in metadata['specification']}
    assert metadata['rowDoubles'] == 162 and not metadata['exactCheckpoint'] and not metadata['liveBrainAccessed']
    assert outcomes['completed'] and len(outcomes['cases']) == 14
    assert outcomes['successfulCases'] == sum(r['completed'] for r in outcomes['cases'])
    assert outcomes['allConditionsCompleted'] == (outcomes['successfulCases'] == 14)
    assert [r['key'] for r in outcomes['cases']] == [r['key'] for r in spec['cases']]
    rows, states = [], {}
    for row, condition in zip(outcomes['cases'], spec['cases']):
        item = dict(key=condition['key'], protocol=condition['protocol'], stepMs=condition['stepMs'],
                    nonlinearToleranceScale=condition['nonlinearToleranceScale'], completed=False, physicalPassed=False)
        try:
            state, voltage, physical = base.candidate(package, row, condition, cap, criteria)
            item.update(completed=True, physicalPassed=physical['passed'], physical=physical)
            if condition['block'] == 'Passive':
                item.update(passiveVoltageErrorMv=float(np.abs(voltage-passive[condition['protocol']]).max()),
                            noSpikes=len(state['spikes']) == 0)
            else:
                item['comparisons'] = {name: base.compare(state, target) for name,target in refs[condition['protocol']].items()}
                states[condition['key']] = state
        except Exception as error:
            item.update(error=repr(error), recordedError=row.get('error'), samplesSaved=row.get('samplesSaved'), timeMs=row.get('timeMs'))
        rows.append(item)
        print('Audited '+condition['key']+': '+str(item['completed']), flush=True)
    assessments = []
    for name in dict.fromkeys(c['protocol'] for c in spec['cases']):
        prior = next(a for a in original['assessment'] if a['protocol'] == name)
        retained = [r for r in original['cases'] if r['protocol'] == name]
        coarse = next(r for r in retained if r['stepMs'] == .001953125 and r['nonlinearToleranceScale'] == 1)
        selected = [r for r in rows if r['protocol'] == name]
        result = dict(protocol=name, passed=False, retainedRefinement=prior['refinement'],
                      precedingAbsolutePassed=prior.get('finestAbsolutePassed', prior['passed']))
        try:
            assert all(r['completed'] and r['physicalPassed'] for r in retained+selected)
            assert all(r['passed'] for r in prior['refinement'])
            fine = next(r for r in selected if r['nonlinearToleranceScale'] == 1)
            if name.startswith('passive-'):
                assert len(selected) == 1 and all(r['noSpikes'] for r in retained+selected)
                difference = next(r['toleranceDifferenceMv'] for r in passive_checks if r['protocol'] == name)
                limit = max(criteria['refinementRatio']*coarse['passiveVoltageErrorMv'],
                            2*difference, criteria['refinementFloorFraction']*criteria['passiveVoltageMv'])
                result.update(finestAbsolutePassed=fine['passiveVoltageErrorMv'] <= criteria['passiveVoltageMv'],
                              refinement=dict(coarse=coarse['key'], fine=fine['key'], limitMv=limit,
                                              passed=fine['passiveVoltageErrorMv'] <= limit))
                result['passed'] = result['finestAbsolutePassed'] and result['refinement']['passed']
            else:
                assert len(selected) == 2 and prior['nonlinearSensitivityPassed']
                tight = next(r for r in selected if r['nonlinearToleranceScale'] == .125)
                primary = spec['primaryReference']
                result['finestAbsolutePassed'] = all(base.within(r['comparisons'][method], criteria)
                    for r in selected for method in (primary, spec['crossMethodReference']))
                result['allRetainedAndNewCountsMatch'] = all(r['comparisons'][primary]['equalCrossingCounts'] for r in retained+selected)
                limits = {f: max(criteria['refinementRatio']*coarse['comparisons'][primary][f],
                                 2*uncertainty[name][f], criteria['refinementFloorFraction']*criteria[f]) for f in base.FIELDS}
                result['refinement'] = dict(coarse=coarse['key'], fine=fine['key'], limits=limits,
                                            passed=base.within(fine['comparisons'][primary], limits))
                sensitivity = base.compare(states[fine['key']], states[tight['key']])
                result['nonlinearSensitivity'] = sensitivity
                result['nonlinearSensitivityPassed'] = base.within(sensitivity, criteria, criteria['nonlinearFraction'])
                result['passed'] = (result['finestAbsolutePassed'] and result['allRetainedAndNewCountsMatch']
                                    and result['refinement']['passed'] and result['nonlinearSensitivityPassed'])
        except Exception as error:
            result.update(passed=False, error=repr(error))
        assessments.append(result)
    environment = base.module('fine_environment_check', package/'source/tools/golgi-reference-resume.py').environment()
    assert environment == pre['environment'], 'Numerical dependencies changed.'
    return dict(completed=True, caseCount=14, completedCases=sum(r['completed'] for r in rows),
                retainedBaselineCases=38, totalConditions=52, cases=rows, criteria=criteria,
                referenceUncertainty=uncertainty, assessment=assessments, originalBoundsChanged=False,
                originalBaselineStatus='FAILED', numericalCriteriaPassed=all(a['passed'] for a in assessments),
                referenceSolved=False, physiologyQualified=False, activeAxonQualified=False, avatarRecoveryAssessed=False)


def main(package):
    report = audit(package)
    with (package/'fine-analysis.json').open('x', encoding='utf-8') as stream:
        stream.write(json.dumps(report, indent=2, allow_nan=False)+'\n')
    if not report['numericalCriteriaPassed']:
        raise RuntimeError('Finer-step criteria not all met; original bounds and all evidence retained.')
    print('Finer numerical comparison passed. Physiology, connected function and Avatar recovery remain separate.', flush=True)


if __name__ == '__main__':
    import argparse
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('package', type=Path); parser.add_argument('--preflight', action='store_true')
    args = parser.parse_args()
    (preflight if args.preflight else main)(args.package.resolve())

"""Review two operator-reported completed captures; no models, tests or ODE solves."""
import argparse
from datetime import datetime, timezone
import importlib.util
import json
import os
from pathlib import Path
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result)
    return result


def tests(capture, expected):
    ns = {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
    cases = ET.parse(capture/'test-results/focused.trx').findall('.//t:UnitTestResult', ns)
    assert len(cases) == sum(expected.values()) and all(c.attrib['outcome'] == 'Passed' for c in cases)
    assert len({c.attrib['testId'] for c in cases}) == len(cases)
    counts = {name: sum(c.attrib['testName'].startswith('NeuralResonanceEngine.DNNE.Tests.'+name+'.')
                       for c in cases) for name in expected}
    assert counts == expected
    return counts


def main(only):
    common = module('completed_review_io', ROOT/'tools/review-olive-and-motor-20261109.py')
    if only != 'golgi':
        review_physical(common)
    if only == 'physical':
        return
    review_golgi(common)


def review_physical(common):
    physical = ROOT/'artifacts/physical-sample-20261109-44584f'
    report = common.verify(physical, ['test-binaries.json'], '0cff80e63bf0fea24626c1fc1f3a84e46a7188d4')
    config, status = (common.read(physical/name) for name in ('configuration.json', 'window-status.json'))
    counts = tests(physical, {'PhysicalSampleTransportTests': 22, 'SolutionManifestTests': 1})
    assert config['NoLiveMonitoring'] and config['ExpectedTestCases'] == status['PassedFocusedTests'] == 23
    assert status['SampleTransportChecksPassed'] and not status['ModelExecuted'] and not status['LiveBrainAccessed']
    assert config['TestClasses'] == list(counts)
    report.update(reviewedUtc=datetime.now(timezone.utc).isoformat(), operatorReportedCompletion=True,
                  passedFocusedTests=23, testCounts=counts, sampleTransportChecksPassed=True,
                  modelExecuted=False, physiologyQualified=False, avatarRecoveryAssessed=False,
                  fullUnitSuitePassed=False)
    output = ROOT/'artifacts/physical-sample-review-20261109'; output.mkdir()
    (output/'review.json').write_text(json.dumps(report, indent=2)+'\n', encoding='utf-8')
    print(json.dumps(report, indent=2), flush=True)


def review_golgi(common):
    capture = ROOT/'artifacts/golgi-sdirk3-20261109-210c3d'
    report = common.verify(capture, ['test-binaries.json', 'probe-binaries.json'],
                           'faac10d04c87d48155e33946edf6a6cdb1ded763', expected_status='FAILED', expected_exit=1)
    config, status = (common.read(capture/name) for name in ('configuration.json', 'window-status.json'))
    counts = tests(capture, {'GolgiSdirk3Tests': 11, 'GolgiSdirkTests': 7, 'GolgiReferenceCellTests': 8,
                            'GolgiChannelKineticsTests': 16, 'SolutionManifestTests': 1})
    assert config['SolverOrder'] == status['SolverOrder'] == 3
    assert config['ExpectedTestCases'] == status['PassedFocusedTests'] == 43 and config['NoLiveMonitoring']
    assert config['TestClasses'] == list(counts) and status['ReferencePreflightPassed']
    assert not status['LiveBrainAccessed'] and not status['NumericalCriteriaPassed']

    # The frozen analyzer only reads retained reference arrays in audit mode.
    # Make accidental solve_ivp use fail rather than launching a model inline.
    for name in ('OPENBLAS_NUM_THREADS', 'OMP_NUM_THREADS', 'MKL_NUM_THREADS'):
        os.environ[name] = '1'  # Match the frozen analyzer before importing NumPy/SciPy.
    import scipy.integrate
    def prohibited_solve(*args, **kwargs):
        raise RuntimeError('No ODE integration is permitted during completed-capture review.')
    scipy.integrate.solve_ivp = prohibited_solve
    sys.path.insert(0, str(capture/'source/tools'))
    audit = module('completed_golgi3_analysis', capture/'source/tools/analyse-golgi-sdirk.py')
    analysis = audit.audit(capture)
    saved = common.read(capture/'sdirk-analysis.json')
    if analysis != saved:
        differences = []
        def compare_tree(a, b, path=''):
            if isinstance(a, dict) and isinstance(b, dict) and a.keys() == b.keys():
                for key in a: compare_tree(a[key], b[key], path+'/'+key)
            elif isinstance(a, list) and isinstance(b, list) and len(a) == len(b):
                for i, (x,y) in enumerate(zip(a,b)): compare_tree(x, y, path+'/'+str(i))
            elif a != b: differences.append(dict(path=path, recomputed=a, saved=b))
        compare_tree(analysis, saved)
        print(json.dumps(differences[:20], indent=2), flush=True)
        raise AssertionError('Saved analysis differs at '+str(len(differences))+' fields; no acceptance override.')
    assert analysis['completedCases'] == analysis['caseCount'] == 38
    assert not analysis['numericalCriteriaPassed'] and all(r['physicalPassed'] for r in analysis['cases'])
    old = common.read(ROOT/'artifacts/golgi-sdirk-20261109-440bb8/sdirk-analysis.json')
    assert old['criteria'] == analysis['criteria'], 'Original bounds changed.'
    rows = []
    for assessment in analysis['assessment']:
        selected = sorted((r for r in analysis['cases'] if r['protocol'] == assessment['protocol']
                           and r['nonlinearToleranceScale'] == 1), key=lambda r: r['stepMs'], reverse=True)
        item = dict(protocol=assessment['protocol'], passed=assessment['passed'],
                    refinementPassed=all(r['passed'] for r in assessment['refinement']))
        if assessment['protocol'].startswith('passive-'):
            item['finestVoltageErrorMv'] = selected[-1]['passiveVoltageErrorMv']
        else:
            comparisons = [r['comparisons']['bdf-tighter'] for r in selected]
            final = comparisons[-1]
            item.update(ordinaryStepsMs=[r['stepMs'] for r in selected], ordinaryComparisons=comparisons,
                        originalResolutionAbsolutePassed=assessment['originalResolutionAbsolutePassed'],
                        finestAbsolutePassed=assessment['finestAbsolutePassed'],
                        finestFailedFields=[f for f in audit.FIELDS if final[f] is None or final[f] > analysis['criteria'][f]],
                        nonlinearSensitivityPassed=assessment['nonlinearSensitivityPassed'],
                        nonlinearSensitivity=assessment['nonlinearSensitivity'],
                        voltageErrorRatios=[a['voltageMv']/b['voltageMv'] if b['voltageMv'] else None
                                            for a,b in zip(comparisons, comparisons[1:])],
                        crossingErrorRatios=[a['crossingMs']/b['crossingMs'] if b['crossingMs'] else None
                                             for a,b in zip(comparisons, comparisons[1:])])
        rows.append(item)
    report.update(reviewedUtc=datetime.now(timezone.utc).isoformat(), operatorReportedCompletion=True,
                  originalStatus='FAILED', passedFocusedTests=43, testCounts=counts,
                  completedConditions=38, allPhysicalAccountingPassed=True, originalAnalysisReproduced=True,
                  originalBoundsChanged=False, criteria=analysis['criteria'], protocolSummaries=rows,
                  numericalCriteriaPassed=False, odeSolved=False, physiologyQualified=False, avatarRecoveryAssessed=False)
    output = ROOT/'artifacts/golgi-sdirk3-review-20261109'; output.mkdir()
    (output/'review.json').write_text(json.dumps(report, indent=2, allow_nan=False)+'\n', encoding='utf-8')
    print(json.dumps({k:v for k,v in report.items() if k != 'protocolSummaries'}, indent=2), flush=True)
    for row in rows:
        print(json.dumps(dict(protocol=row['protocol'], passed=row['passed'],
              finest=row.get('ordinaryComparisons', [{}])[-1], failedFields=row.get('finestFailedFields', []),
              voltageRatios=row.get('voltageErrorRatios', []), crossingRatios=row.get('crossingErrorRatios', []))), flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--operator-reported', action='store_true', required=True)
    parser.add_argument('--only', choices=('both', 'physical', 'golgi'), default='both')
    args = parser.parse_args()
    main(args.only)

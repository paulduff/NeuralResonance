"""Review the operator-reported completed vestibular capture; integrate nothing."""
import argparse
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec); spec.loader.exec_module(result); return result


def forbidden(*args, **kwargs):
    raise RuntimeError('Saved-data review must not integrate a model or rerun numerical controls.')


def main():
    capture = ROOT / 'artifacts/vestibular-numerics-20261117-f21f4b'
    output = ROOT / 'artifacts/vestibular-numerics-review-20261117'
    assert not output.exists()
    common = module('vestibular_review_integrity', ROOT / 'tools/review-olive-and-motor-20261109.py')
    print('Verifying completed vestibular C:/D: files, source and binaries...', flush=True)
    review = common.verify(capture, ['test-binaries.json', 'probe-binaries.json'], '36df4f0b5425319d086a1de9a1becfafd8304563')
    archive = module('vestibular_review_archive', ROOT / 'tools/review-spindle-root-and-causal-motor-20261113.py')
    review['verifiedGitFiles'] = archive.source_archive(common, capture, review['sourceRevision'])
    audit = module('vestibular_saved_comparison', capture / 'source/tools/analyse-vestibular-numerics.py')
    audit.expm = audit.solve_ivp = audit.controls = audit.reference = forbidden
    captured = {}
    def intercept(path, value):
        assert path == capture / 'analysis.json' and not captured
        captured.update(value)
    audit.save = intercept
    audit.compare(capture)
    assert captured == common.read(capture / 'analysis.json')
    status = common.read(capture / 'window-status.json')
    for flag in ('FocusedTestsPassed', 'NumericalCriteriaPassed', 'NumericalEnvironmentUnchanged',
                 'ManagedRuntimePreserved', 'FrozenPackageUnchanged'):
        assert status[flag] is True
    for flag in ('NeuronalModelExecuted', 'NativeBodyExecuted', 'LiveBrainAccessed', 'OldSavesLoaded',
                 'SkullTransformationQualified', 'ContinuousHeadMotionAccuracyQualified', 'PhysiologyQualified',
                 'AvatarRecoveryAssessed', 'FullUnitSuitePassed'):
        assert status[flag] is False
    assert common.digest(capture / 'managed-runtime-before.json') == common.digest(capture / 'managed-runtime-after.json')
    pre = common.read(capture / 'reference-preflight.json')['environment']
    post = common.read(capture / 'reference-postflight.json')
    assert post['unchanged'] and pre == post['environment'] == audit.environment()
    controls = common.read(capture / 'reference-controls.json')
    assert len(controls['controls']) == 6 and all(c['passed'] and c['error'] <= c['bound'] for c in controls['controls'])
    maxima = {name: max(row['maximumAbsolute'] for row in captured[name])
              for name in ('referenceContrasts', 'candidateContrasts', 'subdivisionContrasts')}
    assert captured['testInventory']['passedCases'] == 62 and captured['numericalCriteriaPassed']
    review.update(reviewedUtc=datetime.now(timezone.utc).isoformat(), operatorReportedCompletion=True,
        savedAnalysisExactlyReproduced=True, passedTests=62, analysis=captured, maximumErrors=maxima,
        numericalEnvironmentUnchanged=True, numericalModelExecutedDuringReview=False,
        controlsExecutedDuringReview=False, testsExecutedDuringReview=False,
        physiologicalCalibrationQualified=False, avatarRecoveryAssessed=False)
    output.mkdir(); (output / 'review.json').write_text(json.dumps(review, indent=2)+'\n', encoding='utf-8')
    print(json.dumps(dict(verifiedFilesPerCopy=review['verifiedFilesPerCopy'], passedTests=62,
        records=captured['originalRecords'], acceptedNodes=captured['odeAcceptedNodes'], maximumErrors=maxima), indent=2), flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(); parser.add_argument('--operator-reported-complete', action='store_true'); args = parser.parse_args()
    if not args.operator_reported_complete: raise RuntimeError('Wait for the operator report before inspecting the capture.')
    if not __debug__: raise RuntimeError('Assertions must remain enabled.')
    main()

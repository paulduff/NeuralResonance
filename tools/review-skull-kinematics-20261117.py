"""Review the operator-reported skull component capture; execute no model/test."""
import argparse
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec); spec.loader.exec_module(result); return result


def main():
    capture = ROOT / 'artifacts/skull-kinematics-20261117-f740bd'
    output = ROOT / 'artifacts/skull-kinematics-review-20261117'
    assert not output.exists()
    common = module('skull_review_integrity', ROOT / 'tools/review-olive-and-motor-20261109.py')
    print('Verifying completed skull C:/D: files, frozen source and binaries...', flush=True)
    review = common.verify(capture, ['test-binaries.json', 'probe-binaries.json'], '5aa5f28c2fc0a2252c64a02defa098902a141e26')
    archive = module('skull_review_archive', ROOT / 'tools/review-spindle-root-and-causal-motor-20261113.py')
    review['verifiedGitFiles'] = archive.source_archive(common, capture, review['sourceRevision'])
    audit = module('skull_saved_comparison', capture / 'source/tools/skull_kinematics_gate.py')
    captured = {}
    def intercept(path, value):
        assert path == capture / 'analysis.json' and not captured
        captured.update(value)
    audit.save = intercept
    audit.compare(capture)  # Only parses saved TRX/JSON and hashes existing files.
    assert captured == common.read(capture / 'analysis.json')
    status = common.read(capture / 'window-status.json')
    assert common.read(capture / 'configuration.json')['NoLiveMonitoring']
    for flag in ('FocusedTestsPassed', 'ComponentChecksPassed', 'ManagedRuntimePreserved', 'FrozenPackageUnchanged'):
        assert status[flag] is True
    for flag in ('NativeHeadCaptureExecuted', 'IndependentHeadTrajectoryQualified', 'NeuronalModelExecuted',
                 'LiveBrainAccessed', 'OldSavesLoaded', 'PhysiologyQualified', 'AvatarRecoveryAssessed', 'FullUnitSuitePassed'):
        assert status[flag] is False
    assert common.digest(capture / 'managed-runtime-before.json') == common.digest(capture / 'managed-runtime-after.json') == \
        common.digest(capture / 'reference/qualified-managed-runtime.json')
    assert captured['tests']['passedCases'] == 74 and captured['defaultNativeFilesPreserved'] == 61
    review.update(reviewedUtc=datetime.now(timezone.utc).isoformat(), operatorReportedCompletion=True,
        savedAnalysisExactlyReproduced=True, passedTests=74, analysis=captured,
        numericalModelExecutedDuringReview=False, testsExecutedDuringReview=False,
        nativeHeadSamplingQualified=False, physiologyQualified=False, avatarRecoveryAssessed=False)
    output.mkdir(); (output / 'review.json').write_text(json.dumps(review, indent=2)+'\n', encoding='utf-8')
    print(json.dumps(dict(verifiedFilesPerCopy=review['verifiedFilesPerCopy'], passedTests=74,
        defaultFilesExactlyPreserved=61), indent=2), flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(); parser.add_argument('--operator-reported-complete', action='store_true'); args = parser.parse_args()
    if not args.operator_reported_complete: raise RuntimeError('Wait for the operator report before inspecting the capture.')
    if not __debug__: raise RuntimeError('Assertions must remain enabled.')
    main()

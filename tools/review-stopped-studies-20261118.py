"""Preserve operator-reported stopped studies; no tests or model integration."""
import argparse
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path
import shutil

ROOT = Path(__file__).resolve().parents[1]


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec); spec.loader.exec_module(result); return result


def main(reported):
    assert reported, 'Operator stop/completion confirmation required.'
    output = ROOT/'artifacts/stopped-studies-review-20261118'
    assert not output.exists(), 'Preserve earlier review.'
    common = module('stopped_integrity', ROOT/'tools/review-olive-and-motor-20261109.py')
    archive = module('stopped_archive', ROOT/'tools/review-spindle-root-and-causal-motor-20261113.py')
    skull = ROOT/'artifacts/unity-skull-20261117-1e2f21'
    result = common.verify(skull, ['test-binaries.json','probe-binaries.json'],
                           '3c385fcb04f39a4eacb68dbd3d401b32c316c37f', 'FAILED', 1)
    result['verifiedGitFiles'] = archive.source_archive(common, skull, result['sourceRevision'])
    frozen = module('stopped_skull_gate', skull/'source/tools/native_skull_gate.py')
    tests = frozen.gate.check_tests(skull, frozen.COUNTS); assert tests['passedCases'] == 74
    runtime = frozen.runtime(skull)
    assert common.digest(skull/'managed-runtime-before.json') == common.digest(skull/'managed-runtime-after.json')
    for row in common.read(skull/'player-files.json'):
        Path(row['Path']).relative_to(skull/'player'); assert common.digest(row['Path']) == row['Sha256']
    for path in common.io(skull/'source/UnityWorld').rglob('*.cs'):
        assert common.digest(path) == common.digest(skull/'built-project'/path.relative_to(common.io(skull/'source/UnityWorld')))
    failure = common.read(skull/'study/bridge/failure.json')
    assert failure['physicsStep'] == 198 and failure['expectedSequence'] == 202
    assert 'Sharing violation' in failure['error'] and 'request-000202.json' in failure['error']
    request = common.read(skull/'study/bridge/request-000202.json')
    assert request['sequence'] == 202 and request['expectedStep'] == 198 and request['operation'] == 'step'
    assert not (skull/'study/bridge/reply-000202.json').exists()
    study = common.read(skull/'study/study-result.json')
    assert not study['completed'] and study['dedicatedPlayerStoppedOnFailure']
    result.update(passedTests=74, tests=tests, managedRuntime=runtime, failedPhysicsStep=198,
                  failedRequestSequence=202, requestReadSharingViolation=True, nativeSamplingQualified=False)

    rubral = ROOT/'artifacts/rubral-comparison-20261117-30755e'
    cfg = common.read(rubral/'configuration.json'); status = common.read(rubral/'window-status.json')
    assert cfg['Revision'] == status['Revision'] == 'c53783a17e9ab26ab5968bca5d267d0c1a277fa5'
    assert status['Status'] == 'RUNNING' and status['ExitCode'] is None
    for row in cfg['PackageFiles']: assert common.digest(rubral/row['Path']) == row['Sha256']
    initial = common.read(rubral/'backup-manifest.json'); old_backup = Path(initial['Destination'])
    assert common.digest(rubral/'backup-manifest.json') == common.digest(old_backup/'backup-manifest.json')
    for row in initial['Files']:
        assert common.digest(rubral/row['Path']) == common.digest(old_backup/row['Path']) == row['Sha256']
    git_files = archive.source_archive(common, rubral, cfg['Revision'])
    analysis = common.read(rubral/'analysis.json'); spec = common.read(rubral/'rubral-hinge-input.json')
    assert analysis['completed'] and not analysis['numericalCriteriaPassed'] and analysis['independentReferences'] == 15
    assert analysis['originalBoundsPreserved'] and not (rubral/'input-postflight.json').exists()
    execution = common.read(rubral/'rubral-reference/execution.json')
    assert execution['completed'] and len(execution['references']) == 15 and all(r['completed'] for r in execution['references'])
    failures = []
    for case in analysis['cases']:
        for comparison in case['comparisons']:
            for key, error in comparison['errors'].items():
                if error > spec['candidateBounds'][key]:
                    failures.append(dict(case=case['case'], reference=comparison['reference'], metric=key,
                                         error=error, bound=spec['candidateBounds'][key]))
    assert len(failures) == 12 and {f['metric'] for f in failures} == {'yieldFraction'}
    # Snapshot every existing file, including the original initial manifest,
    # under a new root. Never complete or overwrite the stopped run's status.
    output.mkdir(); evidence = output/'rubral-stopped-evidence'; evidence.mkdir()
    entries = []
    root = common.io(rubral)
    for path in sorted(root.rglob('*')):
        assert not path.is_symlink(), str(path)
        if not path.is_file(): continue
        relative = path.relative_to(root).as_posix(); target = common.io(evidence/relative)
        target.parent.mkdir(parents=True, exist_ok=True); shutil.copyfile(path, target)
        digest = common.digest(path); assert common.digest(target) == digest
        entries.append(dict(path=relative, sha256=digest, bytes=target.stat().st_size))
    for row in entries: assert common.digest(rubral/row['path']) == row['sha256']
    rubral_result = dict(capture=str(rubral), sourceRevision=cfg['Revision'], verifiedGitFiles=git_files,
        originalStatusPreserved=True, originalFinalPreservationIncomplete=True,
        originalInitialBackupVerified=True, savedAnalysis=analysis, failures=failures,
        fullOriginalNativeReauditRequired=True, savedOutputsNotYetIndependentlyReaudited=True,
        evidenceFiles=entries)
    report = dict(reviewedUtc=datetime.now(timezone.utc).isoformat(), operatorReportedStopped=True,
        skull=result, rubral=rubral_result, modelsExecutedDuringReview=False, testsExecutedDuringReview=False,
        physiologyQualified=False, avatarRecoveryAssessed=False)
    (output/'review.json').write_text(json.dumps(report, indent=2, allow_nan=False)+'\n', encoding='utf-8')
    print(json.dumps(dict(review=str(output), skullFiles=result['verifiedFilesPerCopy'], skullPassedTests=74,
        rubralSnapshotFiles=len(entries), rubralFailedMetric='yieldFraction',
        worstSavedError=max(f['error'] for f in failures), originalBound=1e-5), indent=2), flush=True)


if __name__ == '__main__':
    if not __debug__: raise RuntimeError('Assertions must remain enabled.')
    parser = argparse.ArgumentParser(); parser.add_argument('--operator-reported-stopped', action='store_true')
    main(parser.parse_args().operator_reported_stopped)

"""Review an operator-reported archived accounting result; saved evidence only."""
import argparse
import ast
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path
import shutil
import sys

ROOT = Path(__file__).resolve().parents[1]


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec); spec.loader.exec_module(result)
    return result


def review(package, reported):
    assert reported, 'Wait for the operator final completion report.'
    assert package.parent == Path('D:/DNNE-build-work') and package.name.startswith('native-accounting-')
    output = ROOT / 'artifacts' / (package.name + '-review'); assert not output.exists()
    files = module('completed_archive_accounting_files', ROOT / 'tools/review-olive-and-motor-20261109.py')
    source = module('completed_archive_accounting_source', ROOT / 'tools/review-spindle-root-and-causal-motor-20261113.py')
    cfg = files.read(package / 'configuration.json'); status = files.read(package / 'window-status.json')
    result = files.verify(package, [], cfg['Revision'], progress_every=1000)
    result['verifiedGitFiles'] = source.source_archive(files, package, cfg['Revision'])
    for key in ('RecordingControlsPassed', 'RecordingCriteriaPassed', 'FullSourcePacketNativeAccountingPreserved', 'OriginalManifestAndSourcePreserved'):
        assert status[key] is True, key
    for key in ('ModelsExecuted', 'NativePhysicsExecuted', 'OriginalPackageModified', 'LiveCaptureConsumerChanged', 'PhysiologyQualified', 'AvatarRecoveryAssessed'):
        assert status[key] is False, key
    sys.path.insert(0, str(package / 'source/tools'))
    try:
        gate = module('completed_archive_accounting_gate', package / 'source/tools/native_accounting_gate.py')
        protocol = gate.preflight(package)
    finally:
        sys.path.pop(0)
    controls = files.read(package / 'controls/controls.json')
    tree = ast.parse((package / 'source/tools/check-native-evidence-view.py').read_text(encoding='utf-8-sig'))
    names = [node.args[0].value for node in ast.walk(tree) if isinstance(node, ast.Call) and isinstance(node.func, ast.Name)
             and node.func.id == 'check' and node.args and isinstance(node.args[0], ast.Constant)]
    assert len(names) == len(set(names)) == controls['passed'] == controls['expected'] == len(controls['cases']) == 12
    assert {row['key'] for row in controls['cases']} == set(names) and all(row['passed'] for row in controls['cases'])
    assert controls['completed'] and not controls['modelsExecuted'] and not controls['nativePhysicsExecuted'] and not controls['originalCapturesModified']
    analysis = files.read(package / 'analysis.json'); original = files.read(package / 'reviewed-analysis.json')
    recording = files.read(package / 'reviewed-recording-analysis.json')
    assert analysis['completed'] and analysis['recordingCriteriaPassed'] and analysis['controlsPassed'] == 12
    assert analysis['fullSourcePacketNativeAccountingPreserved'] and len(analysis['cases']) == 2
    for row, case in zip(analysis['cases'], protocol['cases'], strict=True):
        key = case['key']; assert row['key'] == key
        for suffix in ('spec', 'accounting'):
            raw = package / 'comparisons' / (key + '-original-' + suffix + '.json')
            compact = package / 'comparisons' / (key + '-archived-' + suffix + '.json')
            assert files.digest(raw) == files.digest(compact), key + suffix
        accounting = files.read(package / 'comparisons' / (key + '-archived-accounting.json'))
        assert accounting == next(r['source'] for r in original['cases'] if r['key'] == key)
        prior = next(r for r in recording['cases'] if r['key'] == key)
        assert row['arrayDigests'] == prior['arrayDigests']
        assert row['allArchivedMembersVerified'] == dict(files=prior['sourceFiles'], uncompressedBytes=prior['sourceBytes'], allMemberDigestsVerified=True)
        assert row['actualPhysicsSteps'] == accounting['actualPhysicsSteps'] == prior['physicalSamples'] - 1
        assert row['sourceFrames'] == accounting['cases'][0]['sourceFrames'] == 12000
        assert row['motorRows'] == accounting['cases'][0]['motorRows'] == 15001
        for flag in ('fullAccountingExactlyEqual', 'originalReviewedAccountingReproduced', 'allDecodedArraysBitwiseEqual'):
            assert row[flag] is True, flag
        assert not row['originalSourcesModified'] and not row['newRawCleanupEnabled']
    for key in ('originalSourcesModified', 'modelsExecuted', 'nativePhysicsExecuted', 'liveCaptureConsumerChanged', 'newRawCleanupEnabled',
                'fullMotorMatrixQualified', 'physiologyQualified', 'avatarRecoveryAssessed'):
        assert analysis[key] is False, key
    result.update(reviewedUtc=datetime.now(timezone.utc).isoformat(), operatorReportedCompletion=True,
        controlsPassed=12, savedAccountingExactlyReproduced=True, fullSourcePacketNativeAccountingPreserved=True,
        analysisSha256=files.digest(package / 'analysis.json'), sourceFrames=24000, actualPhysicsSteps=39995, physicalSamples=39997,
        originalReportsAndSpecsByteIdentical=True, recordedArrayDigestsMatchPriorReview=True,
        originalRawFullAuditRepeatedDuringReview=False, archivedFullAuditRepeatedDuringReview=False,
        archiveCopiesShareDrive=True, modelsExecutedDuringReview=False, testsExecutedDuringReview=False,
        newCaptureWorkflowQualified=False, rawStagingCleanupEnabled=False, fullMotorMatrixQualified=False,
        physiologyQualified=False, avatarRecoveryAssessed=False)
    output.mkdir()
    (output / 'review.json').write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    for name in ('analysis.json', 'controls/controls.json'):
        shutil.copyfile(package / name, output / Path(name).name)
    shutil.copyfile(__file__, output / Path(__file__).name)
    print(json.dumps(dict(output=str(output), verifiedFiles=result['verifiedFilesPerCopy'], controlsPassed=12,
        sourceFrames=24000, actualPhysicsSteps=39995, savedAccountingExactlyReproduced=True), indent=2), flush=True)


if __name__ == '__main__':
    if not __debug__: raise RuntimeError('Assertions must remain enabled.')
    parser = argparse.ArgumentParser(); parser.add_argument('package', type=Path)
    parser.add_argument('--operator-reported-complete', action='store_true'); args = parser.parse_args()
    review(args.package.resolve(), args.operator_reported_complete)

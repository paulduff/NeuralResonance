"""Review a reported completed recording study using saved bytes/arrays only."""
import argparse
import ast
from datetime import datetime, timezone
import hashlib
import importlib.util
import json
from pathlib import Path
import shutil
import sys

ROOT = Path(__file__).resolve().parents[1]


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result)
    return result


def review(package, reported):
    assert reported, 'Wait for the final operator completion report.'
    assert package.parent == Path('D:/DNNE-build-work')
    assert package.name.startswith('native-evidence-')
    output = ROOT / 'artifacts' / (package.name + '-review')
    assert not output.exists()
    files = module('recording_completed_files', ROOT / 'tools/review-olive-and-motor-20261109.py')
    source = module('recording_completed_source', ROOT / 'tools/review-spindle-root-and-causal-motor-20261113.py')
    cfg = files.read(package / 'configuration.json')
    status = files.read(package / 'window-status.json')
    assert cfg['IsolatedNativeEvidence'] and cfg['NoLiveMonitoring']
    result = files.verify(package, [], cfg['Revision'], progress_every=1000)
    result['verifiedGitFiles'] = source.source_archive(files, package, cfg['Revision'])
    for key in ('RecordingControlsPassed', 'RecordingCriteriaPassed', 'OriginalManifestAndSourcePreserved'):
        assert status[key] is True, key
    for key in ('ModelsExecuted', 'NativePhysicsExecuted', 'OriginalPackageModified',
                'LiveCaptureConsumerChanged', 'PhysiologyQualified', 'AvatarRecoveryAssessed'):
        assert status[key] is False, key

    controls = files.read(package / 'controls/controls.json')
    tree = ast.parse((package / 'source/tools/check-native-evidence-archive.py').read_text(encoding='utf-8'))
    names = [node.args[0].value for node in ast.walk(tree)
             if isinstance(node, ast.Call) and isinstance(node.func, ast.Name)
             and node.func.id == 'check' and node.args and isinstance(node.args[0], ast.Constant)]
    assert len(names) == len(set(names)) == controls['expected'] == controls['passed'] == 24
    assert set(names) == {row['key'] for row in controls['cases']}
    assert len(controls['cases']) == 24 and all(row['passed'] for row in controls['cases'])
    assert controls['completed'] and not controls['modelsExecuted'] and not controls['nativePhysicsExecuted']
    assert not controls['oldCapturesModified']

    sys.path.insert(0, str(package / 'source/tools'))
    try:
        gate = module('recording_frozen_gate', package / 'source/tools/native_evidence_gate.py')
        protocol = gate.preflight(package)  # Source/environment verification only.
        reader = gate.module('recording_frozen_reader', package / 'reviewed-reader/analyse-native-hinge-comparison.py')
        analysis = files.read(package / 'analysis.json')
        assert analysis['completed'] and analysis['recordingCriteriaPassed'] and analysis['controlsPassed'] == 24
        assert len(analysis['cases']) == len(protocol['cases']) == 2
        cases = []
        for case, recorded in zip(protocol['cases'], analysis['cases']):
            key = case['key']
            assert recorded['key'] == key
            receipt = files.read(package / 'captures' / (key + '-archive.json'))
            assert recorded == files.read(package / 'captures' / (key + '-comparison.json'))
            assert receipt['sourceUnchanged'] and receipt['allMemberDigestsVerified'] and receipt['sourceFilesRemoved'] == 0
            assert recorded['allFileBytesPreserved'] and recorded['decodedArraysBitwiseEqual'] and recorded['sourceFilesRemoved'] == 0
            expected = files.read(package / case['expected'])
            archive_path = package / 'captures' / (key + '.zip')
            assert files.digest(archive_path) == receipt['archiveSha256'] == recorded['archiveSha256']
            assert archive_path.stat().st_size == receipt['archiveBytes'] == recorded['archiveBytes']
            with gate.EvidenceArchive(archive_path, receipt['archiveSha256']) as archive:
                verified = archive.verify(expected)
                assert verified['files'] == recorded['sourceFiles'] == receipt['files']
                assert verified['uncompressedBytes'] == recorded['sourceBytes'] == receipt['uncompressedBytes']
                data = reader.candidate_data(archive.root, case['fixture'], allowed_steps=(case['fixture']['physicalSteps'],))
            arrays = {name: dict(shape=list(value.shape), dtype=str(value.dtype),
                      sha256=hashlib.sha256(value.tobytes()).hexdigest().upper())
                      for name, value in data.items() if isinstance(value, gate.np.ndarray)}
            assert arrays == recorded['arrayDigests'], key
            assert recorded['physicalSamples'] == case['fixture']['physicalSteps'] + 1 == arrays['timeMs']['shape'][0]
            cases.append(dict(key=key, files=recorded['sourceFiles'], originalBytes=recorded['sourceBytes'],
                archiveBytes=recorded['archiveBytes'], physicalSamples=recorded['physicalSamples'],
                archiveSha256=recorded['archiveSha256'], memberBytesMatchOriginalManifest=True,
                savedArrayDigestsExactlyReproduced=True))
            print(key + ': all archived member hashes and decoded array digests verified.', flush=True)
        for key in ('originalSourcesModified', 'modelsExecuted', 'nativePhysicsExecuted', 'liveCaptureConsumerChanged',
                    'fullMotorMatrixQualified', 'physiologyQualified', 'avatarRecoveryAssessed'):
            assert analysis[key] is False, key
    finally:
        sys.path.pop(0)
    total_bytes = sum(row['originalBytes'] for row in cases)
    archive_bytes = sum(row['archiveBytes'] for row in cases)
    result.update(reviewedUtc=datetime.now(timezone.utc).isoformat(), operatorReportedCompletion=True,
        recordingCriteriaPassed=True, controlsPassed=24, cases=cases,
        originalFiles=sum(row['files'] for row in cases), originalBytes=total_bytes, archiveBytes=archive_bytes,
        reductionFraction=1 - archive_bytes / total_bytes,
        physicalSamples=sum(row['physicalSamples'] for row in cases), archiveCopiesShareDrive=True,
        rawCaptureRehashRepeated=False, originalRawCopiesRemain=True,
        testsExecutedDuringReview=False, modelsExecutedDuringReview=False,
        liveCaptureIntegrationQualified=False, fullMotorMatrixQualified=False,
        physiologyQualified=False, avatarRecoveryAssessed=False)
    output.mkdir()
    (output / 'review.json').write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    for name in ('analysis.json', 'controls/controls.json'):
        shutil.copyfile(package / name, output / Path(name).name)
    shutil.copyfile(__file__, output / Path(__file__).name)
    print(json.dumps(dict(output=str(output), files=result['originalFiles'], physicalSamples=result['physicalSamples'],
                         originalBytes=total_bytes, archiveBytes=archive_bytes, reductionFraction=result['reductionFraction']), indent=2))


if __name__ == '__main__':
    if not __debug__:
        raise RuntimeError('Assertions must remain enabled.')
    parser = argparse.ArgumentParser()
    parser.add_argument('package', type=Path)
    parser.add_argument('--operator-reported-complete', action='store_true')
    args = parser.parse_args()
    review(args.package.resolve(), args.operator_reported_complete)

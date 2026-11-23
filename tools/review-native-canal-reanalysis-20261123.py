"""Read-only review of completed saved sensory accounting; no model/analysis rerun."""
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path
import shutil
import sys

ROOT = Path(__file__).resolve().parents[1]
PACKAGE = Path('D:/DNNE-build-work/native-canal-reanalysis-20261123-8dd5a0')
REVISION = 'c22d3d6b7bf0928b73e54f094769889b956e71af'


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec); spec.loader.exec_module(result); return result


def review():
    output = ROOT/'artifacts'/(PACKAGE.name+'-review'); assert not output.exists()
    common = module('native_canal_saved_reanalysis_files', ROOT/'tools/review-olive-and-motor-20261109.py')
    archive = module('native_canal_saved_reanalysis_archive', ROOT/'tools/review-spindle-root-and-causal-motor-20261113.py')
    result = common.verify(PACKAGE, [], REVISION, progress_every=1000)
    result['verifiedGitFiles'] = archive.source_archive(common, PACKAGE, REVISION)
    status = common.read(PACKAGE/'window-status.json'); cfg = common.read(PACKAGE/'configuration.json')
    for key in ('NoLiveMonitoring', 'SavedCaptureReanalysis', 'IdentityControlsPassed', 'ConnectedAccountingPassed', 'OriginalCapturesAndDependenciesPreserved'):
        assert status[key] is True
    for key in ('NativeCaptureRerun', 'NativeBodyExecuted', 'SensoryModelExecuted', 'LiveBrainAccessed', 'OldSavesLoaded',
                'OriginalNumericalLimitsChanged', 'PhysiologicalFiringQualified', 'NeuronalMotorAuthority',
                'AvatarStandingQualified', 'IndependentConnectedAccuracyQualified'):
        assert status[key] is False
    controls = common.read(PACKAGE/'identity-controls.json'); analysis = common.read(PACKAGE/'analysis.json')
    assert controls['passed'] and controls['cases'] == len(set(controls['methods'])) == 25
    assert controls['methods'] == cfg['ExpectedControlMethods'] and not controls['failures'] and not controls['errors'] and not controls['skipped']
    assert analysis['completed'] and analysis['connectedAccountingPassed'] and analysis['originalNativeTrajectoriesPreserved'] and analysis['originalHeadOwnershipPreserved']
    assert [analysis[k] for k in ('nativeCases', 'physicsSteps', 'physicalEndpoints', 'headReceipts', 'sourceSteps', 'packets')] == [6, 48, 54, 162, 516096, 1008]
    assert len(analysis['runs']) == len(analysis['native']) == len(analysis['nucleus']) == 6
    for native, nucleus, run in zip(analysis['native'], analysis['nucleus'], analysis['runs'], strict=True):
        assert native['case'] == nucleus['case'] == run['condition']+'-n'+str(run['sourceStepsPerInterval'])
        assert native['originalPhysicalStatesExact'] and native['nativeHeadPayloadsExact']
        g = native['crossSessionGeometry']
        assert g['mechanicalGeometryExactApartFromInstanceIds'] and g['independentlyVerifiedPathsPerSession'] == 38
        assert not g['liveIdentityRulesChanged'] and not g['numericalToleranceIntroduced'] and not g['originalRawGeometryIdentical']
        assert len(g['nativeInstanceMapping']) == 34 and sum(r['current'] != r['original'] for r in g['nativeInstanceMapping']) == 30
        assert run['accountingPassed'] and run['conservationPassed'] and run['heldInputArithmeticMaximumFraction'] <= 1
        assert run['events'] == nucleus['originalEventsAdmitted'] == nucleus['actualContactDeliveries'] == nucleus['futureDeliveries'] == 0
        assert nucleus['fixedReceiverGridPreserved'] and nucleus['receiverTicks'] == 281 and nucleus['frames'] == run['packets']
    original = Path(cfg['OriginalCapture']); original_backup = Path(common.read(original/'backup-manifest.json')['Destination'])
    assert common.digest(original/'backup-manifest.json') == common.digest(original_backup/'backup-manifest.json') == cfg['OriginalManifestSha256']
    assert common.read(original/'window-status.json')['Status'] == 'FAILED'
    result.update(reviewedUtc=datetime.now(timezone.utc).isoformat(), operatorReportedCompletion=True,
        passedIdentityControls=25, sourceSteps=516096, physicalEndpoints=54, headReceipts=162, packets=1008,
        savedAccountingPassed=True, originalFailurePreserved=True, recordedEvents=0, actualNucleusContactDeliveries=0,
        maxHeldInputArithmeticAllowanceFraction=max(r['heldInputArithmeticMaximumFraction'] for r in analysis['runs']),
        originalFullCopiesVerifiedByCompletedStudy=True, modelsExecutedDuringReview=False, testsExecutedDuringReview=False,
        fullAnalysisRecomputedDuringReview=False, independentConnectedAccuracyQualified=False,
        physiologicalFiringQualified=False, neuronalMotorAuthority=False, avatarStandingQualified=False)
    output.mkdir(); (output/'review.json').write_text(json.dumps(result, indent=2)+'\n', encoding='utf-8')
    for name in ('analysis.json', 'identity-controls.json', 'window-status.json'): shutil.copyfile(PACKAGE/name, output/name)
    shutil.copyfile(__file__, output/Path(__file__).name)
    print(json.dumps(dict(output=str(output), verifiedFiles=result['verifiedFilesPerCopy'], passedIdentityControls=25,
                         sourceSteps=516096, recordedEvents=0)), flush=True)


if __name__ == '__main__':
    assert '--operator-reported-complete' in sys.argv
    if not __debug__: raise RuntimeError('Assertions must remain enabled.')
    review()

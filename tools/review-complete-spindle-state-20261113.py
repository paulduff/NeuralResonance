"""Reanalyse the reported completed native capture; do not rerun its models."""
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    value = importlib.util.module_from_spec(spec); spec.loader.exec_module(value)
    return value


def main():
    common = module('capture_audit', ROOT / 'tools/review-olive-and-motor-20261109.py')
    source = module('source_audit', ROOT / 'tools/review-spindle-root-and-causal-motor-20261113.py')
    package = ROOT / 'artifacts/spindle-state-20261113-a43f1a'
    revision = 'b9937e9163233b89322510b7742ee6d292361836'
    review = common.verify(package, [], revision, 'FAILED', 1)
    review['verifiedGitFiles'] = source.source_archive(common, package, revision)
    assert common.read(package / 'runtime-before.json') == common.read(package / 'runtime-after.json') == common.read(package / 'preparation-verification.json')
    frozen = module('frozen_completed_spindle', package / 'source/tools/spindle-reference.py')
    candidate, api = frozen.state_candidate()
    assert not hasattr(api, 'vector') and not hasattr(api, 'one')
    assert "has no attribute 'vector'" in (package / 'analysis.log').read_text()
    # Reproduce the actual adapter failure using saved arrays only, then supply
    # exactly the two parser functions already defined in the frozen module.
    try:
        candidate.analyse(package, api)
    except AttributeError as error:
        assert "has no attribute 'vector'" in str(error)
    else:
        raise AssertionError('Frozen adapter failure did not reproduce.')
    api.vector, api.one = frozen.vector, frozen.one
    analysis = candidate.analyse(package, api)
    assert analysis['complete'] and analysis['canonicalStateChecksPassed']
    assert len(analysis['trajectories']) == 30 and all(r['passed'] for r in analysis['trajectories'])
    assert all(r['maximumStateDifference'] == r['maximumPostInitialStressDifferencePa'] == 0 for r in analysis['trajectories'])
    assert len(analysis['sourceComparison']['boundaries']) == 2 and len(analysis['boundaryControls']['fibers']) == 2
    current = (ROOT / 'tools/spindle-reference.py').read_text()
    old = (package / 'source/tools/spindle-reference.py').read_text()
    assert current.count('vector=vector, one=one, ') == 1 and current.replace('vector=vector, one=one, ', '') == old
    assert (ROOT / 'tools/spindle-state-candidate.py').read_text() == (package / 'source/tools/spindle-state-candidate.py').read_text()
    repaired = module('repaired_completed_spindle', ROOT / 'tools/spindle-reference.py')
    repaired_candidate, repaired_api = repaired.state_candidate()
    assert repaired_candidate.analyse(package, repaired_api) == analysis
    review.update(reviewedUtc=datetime.now(timezone.utc).isoformat(), operatorReportedCompletion=True,
        originalFailedCapturePreserved=True, originalAdapterFailureReproduced=True,
        soleAdapterRepair='Expose the existing frozen vector and one parser functions.',
        originalAnalysisAndBoundsUnchanged=True, currentAnalysisExactlyMatchesRepairedFrozenAnalysis=True,
        runtimePreparationBeforeAfterMatch=True, completedTrajectories=30, completedBoundaryRecords=2,
        canonicalStateChecksPassed=True, maximumStateDifference=0., maximumPostInitialStressDifferencePa=0.,
        fullSlackDriverExecuted=False, zeroOverlapKineticsImplemented=False, independentNumericalAccuracyQualified=False,
        physiologicalCalibrationQualified=False, liveBodyIntegrated=False, testsExecutedDuringReview=False,
        modelsSolvedDuringReview=False)
    output = ROOT / 'artifacts/spindle-state-review-20261113'; output.mkdir()
    (output / 'review.json').write_text(json.dumps(review, indent=2, allow_nan=False) + '\n')
    (output / 'analysis.json').write_text(json.dumps(analysis, indent=2, allow_nan=False) + '\n')
    print(json.dumps(review, indent=2), flush=True)


if __name__ == '__main__':
    main()

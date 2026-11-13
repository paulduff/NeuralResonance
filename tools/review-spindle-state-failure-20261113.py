"""Review the reported canonical-spindle capture without executing a model."""
from datetime import datetime, timezone
from io import BytesIO
import importlib.util
import json
from pathlib import Path
import subprocess
from zipfile import ZipFile

ROOT = Path(__file__).resolve().parents[1]


def main():
    spec = importlib.util.spec_from_file_location('capture_audit', ROOT / 'tools/review-olive-and-motor-20261109.py')
    common = importlib.util.module_from_spec(spec); spec.loader.exec_module(common)
    package = ROOT / 'artifacts/spindle-state-20261113-3468db'
    revision = 'cd0c193d44426704dce0a03b3d27aa0864088d95'
    review = common.verify(package, [], revision, 'FAILED', 1)
    data = subprocess.check_output(['git', 'archive', '--format=zip', revision], cwd=ROOT)
    with ZipFile(BytesIO(data)) as git, ZipFile(package / 'source.zip') as frozen:
        assert frozen.comment.decode().strip() == revision and git.namelist() == frozen.namelist()
        files = 0
        for entry in frozen.infolist():
            assert git.read(entry.filename) == frozen.read(entry)
            if not entry.is_dir():
                assert common.io(package / 'source' / entry.filename).read_bytes() == frozen.read(entry)
                files += 1
    review['verifiedGitFiles'] = files
    assert common.read(package / 'runtime-before.json') == common.read(package / 'runtime-after.json') == common.read(package / 'preparation-verification.json')
    base = common.module('frozen_spindle_audit', package / 'source/tools/spindle-reference.py')
    candidate, api = base.state_candidate()
    assert candidate.verify(package, api) == 18
    analysis = base.analyse(package)
    assert analysis['complete'] and len(analysis['trajectories']) == 30 and len(analysis['boundaries']) == 2
    records = []
    for result in analysis['trajectories']:
        label = result['label']
        old = common.read(package / 'state-baseline/mechanics' / (label + '.json'))
        new = common.read(package / 'mechanics' / (label + '.json'))
        assert old['parameters'] == new['parameters'] and len(old['snapshots']) == len(new['snapshots'])
        state_error = stress_error = 0.
        for before, after in zip(old['snapshots'], new['snapshots']):
            y = base.vector(after['y'])
            assert before['step'] == after['step'] and before['timeSeconds'] == after['timeSeconds']
            state_error = max(state_error, max(abs(a-b) for a,b in zip(y, base.vector(before['y']))))
            assert base.vector(after['reportedBinPopulations']) == y[1:-2]
            assert base.one(after['reportedDetached']) == y[0]
            assert abs(base.one(after['reportedBound']) - sum(y[1:-2])) <= 1e-12
            assert abs(y[0] + sum(y[1:-2]) - 1) <= 1e-12 and abs(y[-2] + y[-1] - 1) <= 1e-12
            assert min(y) >= -1e-12 and sum(y[1:-2]) <= y[-1] + 1e-12 and y[-1] <= after['overlap'] + 1e-12
            stress = base.one(after['stressPa'])
            if after['step']:
                stress_error = max(stress_error, abs(stress - base.one(before['stressPa'])))
            else:
                assert stress == new['parameters']['k_passive'] * (after['hsLengthNm'] - new['parameters']['hsl_slack'])
        assert state_error <= 1e-12 and stress_error <= 1e-7
        records.append(dict(label=label, maximumStateDifference=state_error, maximumPostInitialStressDifferencePa=stress_error))
    for boundary in analysis['boundaries']:
        assert boundary['exactBoundaryFromPositive'] == boundary['exactBoundaryFromZero'] == 0
        assert all(s['cacheDifference'] == 0 and s['forceElements'] == 1 for s in boundary['states'].values())
    log = common.io(package / 'native-mechanics.log').read_text()
    assert 'assert (abs (root - hs.hsl_slack) <= 1e-9) failed' in log
    assert not (package / 'state-controls.json').exists() and not (package / 'mechanics-analysis.json').exists()
    review.update(reviewedUtc=datetime.now(timezone.utc).isoformat(), operatorReportedCompletion=True,
        originalFailurePreserved=True, runtimeBeforeAfterAndPreparationMatch=True, verifiedRuntimeFiles=59532,
        completedTrajectories=30, completedBoundaryRecords=2, trajectoryComparison=records, savedTrajectoryAnalysis=analysis,
        failure='First unloaded-root assertion failed; no root value or solver exit information was saved.',
        rootValueKnown=False, completeStateControlsPassed=False, independentNumericalAccuracyQualified=False,
        testsExecutedDuringReview=False, modelsSolvedDuringReview=False)
    output = ROOT / 'artifacts/spindle-state-failure-review-20261113'; output.mkdir()
    (output / 'review.json').write_text(json.dumps(review, indent=2, allow_nan=False) + '\n')
    print(json.dumps({k:v for k,v in review.items() if k not in ('trajectoryComparison','savedTrajectoryAnalysis')}, indent=2), flush=True)
    print('Maximum state difference:', max(r['maximumStateDifference'] for r in records))
    print('Maximum post-initial stress difference:', max(r['maximumPostInitialStressDifferencePa'] for r in records))


if __name__ == '__main__':
    main()

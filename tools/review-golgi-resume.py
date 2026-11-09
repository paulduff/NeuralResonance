"""Offline review of the operator-reported Golgi continuation. No solver calls."""
from datetime import datetime, timezone
from hashlib import sha256
import importlib.util
import json
from pathlib import Path

root = Path(__file__).resolve().parents[1]
capture = root / 'artifacts/golgi-resume-20261109-3b4dff'
output = root / 'artifacts/golgi-resume-review-20261109'
read = lambda p: json.loads(p.read_text(encoding='utf-8-sig'))
digest = lambda p: sha256(p.read_bytes()).hexdigest().upper()
manifest = read(capture / 'backup-manifest.json')
backup = Path(manifest['Destination'])
assert digest(capture / 'backup-manifest.json') == digest(backup / 'backup-manifest.json')
expected = {e['Path'].replace('\\', '/') for e in manifest['Files']}
for folder in (capture, backup):
    actual = {p.relative_to(folder).as_posix() for p in folder.rglob('*') if p.is_file()} - {'backup-manifest.json'}
    assert expected == actual
    for entry in manifest['Files']:
        assert digest(folder / entry['Path']) == entry['Sha256'], entry['Path']
status = read(capture / 'window-status.json')
assert status['Status'] == 'DIAGNOSTICS_COMPLETE' and status['ExitCode'] == 0
assert status['Revision'] == '2d86a516046d016c5f8b69eaf37220bb43c0c166'
spec = importlib.util.spec_from_file_location('reported_golgi_resume', capture / 'source/tools/golgi-reference-resume.py')
audit = importlib.util.module_from_spec(spec)
spec.loader.exec_module(audit)
recomputed = audit.audit(capture, True)  # Only reads arrays; never calls main/solve.
assert recomputed == read(capture / 'study/analysis.json')
assert recomputed['allOriginalReferenceCriteriaPassed'] and len(recomputed['comparisons']) == 12
rows = []
for name in audit.PROTOCOLS:
    comparisons = [c for c in recomputed['comparisons'] if c['protocol'] == name]
    candidate = [c for c in recomputed['candidateComparisons'] if c['protocol'] == name and c['reference'] == 'radau-strict']
    ordered = sorted(candidate, key=lambda c: c['stepMs'], reverse=True)
    assert len(ordered) == 3
    rows.append(dict(protocol=name, counts=ordered[-1]['counts'],
        referenceMaximumTimingDifferenceMs=max(c['maximumOrdinalDifferenceMs'] for c in comparisons),
        referenceMaximumSomaDifferenceMv=max(c['maximumSomaDifferenceMv'] for c in comparisons),
        candidateTimingDifferenceMs=[c['maximumOrdinalDifferenceMs'] for c in ordered],
        finestCandidateSomaDifferenceMv=ordered[-1]['maximumSomaDifferenceMv'],
        finestCandidateFirst20MsDifferenceMv=ordered[-1]['maximumFirst20MsDifferenceMv']))
report = dict(reviewedUtc=datetime.now(timezone.utc).isoformat(), capture=str(capture), backup=str(backup),
    sourceRevision=status['Revision'], completedUtc=status['CompletedUtc'],
    verifiedFilesPerCopy=len(manifest['Files']), verifiedBytesPerCopy=sum(e['Bytes'] for e in manifest['Files']),
    verifiedPackageFiles=len(read(capture / 'configuration.json')['PackageFiles']),
    recomputedReferenceComparisons=12, recomputedCandidateComparisons=36, reusedSolves=11, newSolves=1,
    allOriginalReferenceCriteriaPassed=True, rows=rows, originalCapturesUnchanged=True,
    odeSolved=False, activeCandidateQualified=False, physiologyQualified=False, avatarRecoveryAssessed=False)
output.mkdir()
(output / 'review.json').write_text(json.dumps(report, indent=2, allow_nan=False) + '\n', encoding='utf-8')
print(json.dumps(report, indent=2))

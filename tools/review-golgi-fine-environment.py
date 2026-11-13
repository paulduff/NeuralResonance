"""Review an operator-reported preflight failure; inspect hashes only, no model run."""
import argparse
from datetime import datetime, timezone
import importlib.util
import json
import os
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result)
    return result


def main(output):
    if output.exists():
        raise FileExistsError('Preserve existing review: '+str(output))
    for name in ('OPENBLAS_NUM_THREADS', 'OMP_NUM_THREADS', 'MKL_NUM_THREADS'):
        os.environ[name] = '1'
    capture = ROOT/'artifacts/golgi-sdirk3-fine-20261109-4b5c0f'
    common = module('failed_capture_hashes', ROOT/'tools/review-olive-and-motor-20261109.py')
    report = common.verify(capture, ['test-binaries.json'], '69bc40f1593ec5cab71883be705cdbe2a8cfbec4', 'FAILED', 1)
    checks = module('failed_focused_tests', ROOT/'tools/review-golgi3-and-physical-20261109.py')
    counts = checks.tests(capture, dict(GolgiSdirk3Tests=11, GolgiSdirkTests=7, GolgiReferenceCellTests=8,
                                       GolgiChannelKineticsTests=16, SolutionManifestTests=1))
    assert not any((capture/name).exists() for name in ('study', 'fine-preflight.json', 'probe-binaries.json'))
    previous = common.read(capture/'baseline/reference-preflight.json')['environment']
    current = module('recorded_environment', capture/'source/tools/golgi-reference-resume.py').environment()
    before = {r['path']:r['sha256'] for r in previous['files']}
    after = {r['path']:r['sha256'] for r in current['files']}
    changed = [dict(path=p, previous=before.get(p), current=after.get(p))
               for p in sorted(before.keys()|after.keys()) if before.get(p) != after.get(p)]
    metadata = {k:dict(previous=previous.get(k), current=current.get(k))
                for k in previous.keys()|current.keys() if k != 'files' and previous.get(k) != current.get(k)}
    report.update(reviewedUtc=datetime.now(timezone.utc).isoformat(), operatorReportedCompletion=True,
        passedFocusedTests=43, testCounts=counts, newConditionsExecuted=0, referenceSolved=False,
        modelExecutedDuringReview=False, environmentFilesBefore=len(before), environmentFilesNow=len(after),
        changedFiles=changed, metadataChanges=metadata)
    output.mkdir()
    for name, data in [('current-environment.json', current), ('review.json', report)]:
        (output/name).write_text(json.dumps(data, indent=2, allow_nan=False)+'\n', encoding='utf-8')
    print(json.dumps({k:v for k,v in report.items() if k != 'changedFiles'}, indent=2))
    print('Changed dependency files:', len(changed))
    print(json.dumps(changed[:20], indent=2))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('output', type=Path)
    parser.add_argument('--operator-reported', action='store_true', required=True)
    args = parser.parse_args()
    main(args.output.resolve())

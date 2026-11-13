"""Review the operator-reported matched host timing capture, without execution."""
import argparse
from datetime import datetime, timezone
import importlib.util
from io import BytesIO
import json
from pathlib import Path
import subprocess
import sys
import xml.etree.ElementTree as ET
from zipfile import ZipFile

ROOT = Path(__file__).resolve().parents[1]


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    value = importlib.util.module_from_spec(spec); spec.loader.exec_module(value)
    return value


def main(output):
    assert not output.exists(), 'Preserve previous review'
    capture = ROOT / 'artifacts/olive-host-timing-20261113-ab15ac'
    revision = '8a3c63c1c8cc1f4c4a5337fbb5b56001c1f87417'
    common = module('timing_capture_io', ROOT / 'tools/review-olive-and-motor-20261109.py')
    names = module('recorded_test_identities', ROOT / 'tools/review-golgi-and-contacts-20261113.py')
    report = common.verify(capture, ['test-binaries.json', 'reference-binaries.json', 'candidate-binaries.json'], revision)
    assert Path(report['backup']) == Path('D:/DNNE-desktop-runs/UnityWorld') / capture.name
    regenerated = subprocess.check_output(['git', 'archive', '--format=zip', revision], cwd=ROOT)
    source_count = 0
    with ZipFile(BytesIO(regenerated)) as git_archive, ZipFile(capture / 'source.zip') as saved:
        assert saved.comment.decode('ascii').strip() == revision
        assert set(saved.namelist()) == set(git_archive.namelist())
        for entry in saved.infolist():
            data = saved.read(entry)
            assert data == git_archive.read(entry.filename)
            if not entry.is_dir():
                assert common.io(capture / 'source' / entry.filename).read_bytes() == data
                source_count += 1
    expected = set()
    for cls, count in [('OlivaryBatchTests', 10), ('SolutionManifestTests', 1)]:
        source = common.io(capture / 'source/tests/NeuralResonanceEngine.DNNE.Tests' / (cls + '.cs')).read_text(encoding='utf-8-sig')
        cases = names.declared_cases(source, 'NeuralResonanceEngine.DNNE.Tests.' + cls + '.')
        assert len(cases) == count
        expected.update(cases)
    cases = ET.parse(capture / 'test-results/focused.trx').findall('.//t:UnitTestResult',
        {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'})
    assert len(cases) == 11 and {names.result_identity(c.attrib['testName']) for c in cases} == expected
    assert all(c.attrib['outcome'] == 'Passed' for c in cases)
    status = common.read(capture / 'window-status.json')
    assert status['AccuracyPassed'] and status['TimingMeasured'] and status['ProcessScheduleChecked']
    assert status['PassedFocusedTests'] == 11 and status['CompletedTrials'] == 144
    assert not any(status[k] for k in ('LiveBrainAccessed', 'OldSavesLoaded', 'PhysiologyQualified', 'AvatarRecoveryAssessed', 'FullUnitSuitePassed'))
    print('Full C:/D: capture, frozen Git files, binaries and eleven test identities verified.', flush=True)
    sys.path.insert(0, str(capture / 'source/tools'))
    analyzer = module('completed_host_timing', capture / 'source/tools/analyse-olive-host-timing.py')
    analysis = analyzer.audit(capture)
    assert analysis == common.read(capture / 'host-timing-analysis.json')
    assert analysis['accuracyPassed'] and analysis['exactAcrossVariantsRoundsAndWarmups']
    assert analysis['processes'] == 72 and analysis['trials'] == 144 and analysis['controlGroups'] == 24
    assert analysis['benefitIsDescriptive'] and not analysis['wholeBrainThroughputMeasured']
    report.update(operatorReportedCompletion=True, reviewedUtc=datetime.now(timezone.utc).isoformat(),
        verifiedGitFiles=source_count, passedTests=11, savedAnalysisExactlyReproduced=True,
        analysisSha256=common.digest(capture / 'host-timing-analysis.json'),
        modelsExecutedDuringReview=False, testsExecutedDuringReview=False,
        analysis={k: v for k, v in analysis.items() if k != 'cases'},
        cases=[{k: v for k, v in c.items() if k != 'rows'} for c in analysis['cases']])
    output.mkdir()
    (output / 'review.json').write_text(json.dumps(report, indent=2, allow_nan=False) + '\n', encoding='utf-8')
    print(json.dumps({k: report[k] for k in ('verifiedFilesPerCopy', 'verifiedBytesPerCopy', 'manifestSha256', 'savedAnalysisExactlyReproduced')}, indent=2))
    for case in report['cases']:
        print(case['key'], 'saving', case['medianTimeSavingFraction'], 'faster pairs', case['fasterPairs'],
              'material benefit', case['materialConsistentBenefit'], flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('output', type=Path)
    parser.add_argument('--operator-reported-complete', action='store_true', required=True)
    args = parser.parse_args(); main(args.output.resolve())

"""Review operator-reported finer Golgi and repaired contact captures, without solves."""
import argparse
from collections import Counter
from datetime import datetime, timezone
import importlib.util
from io import BytesIO
import json
from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET
from zipfile import ZipFile
import subprocess

ROOT = Path(__file__).resolve().parents[1]


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    value = importlib.util.module_from_spec(spec); spec.loader.exec_module(value)
    return value


def declared_cases(source, prefix):
    cases = set()
    pattern = r'((?:\s*\[[^\]]+\](?:[ \t]*//[^\n]*)?)+)\s+public\s+(?:async\s+)?(?:void|Task)\s+(\w+)\s*\(([^)]*)\)'
    for attributes, name, parameters in re.findall(pattern, source):
        if '[Fact]' in attributes:
            cases.add((prefix + name, ()))
        elif '[Theory]' in attributes:
            assert '[MemberData' not in attributes and '[ClassData' not in attributes
            names = [p.strip().split()[-1] for p in parameters.split(',')]
            rows = re.findall(r'\[InlineData\((.*?)\)\]', attributes)
            assert rows
            for row in rows:
                row = re.sub(r'(?<![\w.])(-?)\.(\d+)', r'\g<1>0.\2', row)
                values = json.loads('[' + row + ']')
                assert len(values) == len(names)
                cases.add((prefix + name, tuple(zip(names, values))))
    return cases


def result_identity(name):
    if '(' not in name:
        return name, ()
    method, arguments = name[:-1].split('(', 1)
    values = []
    for item in arguments.split(', '):
        key, value = item.split(': ', 1)
        if value in ('True', 'False'):
            value = value.lower()
        values.append((key, json.loads(value)))
    return method, tuple(values)


def main(output):
    assert not output.exists(), 'Preserve previous review'
    common = module('capture_verification', ROOT / 'tools/review-olive-and-motor-20261109.py')
    golgi = ROOT / 'artifacts/golgi-sdirk3-fine-20261113-ca813f'
    contacts = ROOT / 'artifacts/physical-observation-20261113-014b85'
    reports = {}
    for name, capture, revision, binaries, counts in (
        ('golgi', golgi, 'ad68cc7a3c5e1f854d1c0bfe489602a9caad37f4', ['test-binaries.json', 'probe-binaries.json'],
            dict(GolgiSdirk3Tests=11, GolgiSdirkTests=7, GolgiReferenceCellTests=8, GolgiChannelKineticsTests=16, SolutionManifestTests=1)),
        ('contacts', contacts, '8a273337542e83433ee92be53a6fb07fee00d233', ['test-binaries.json'],
            dict(PhysicalObservationTests=28, PhysicalSampleProducerTests=16, PhysicalSampleTransportTests=22, SolutionManifestTests=1))):
        report = common.verify(capture, binaries, revision)
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
        for cls, count in counts.items():
            source = common.io(capture / 'source/tests/NeuralResonanceEngine.DNNE.Tests' / (cls + '.cs')).read_text(encoding='utf-8-sig')
            names = declared_cases(source, 'NeuralResonanceEngine.DNNE.Tests.' + cls + '.')
            assert len(names) == count, (cls, len(names), count)
            expected.update(names)
        ns = {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
        cases = ET.parse(capture / 'test-results/focused.trx').findall('.//t:UnitTestResult', ns)
        assert len(cases) == len(expected) and {result_identity(c.attrib['testName']) for c in cases} == expected
        assert all(c.attrib['outcome'] == 'Passed' for c in cases)
        assert dict(Counter(result_identity(c.attrib['testName'])[0].rsplit('.', 1)[0].rsplit('.', 1)[-1] for c in cases)) == counts
        config, status = (common.read(capture / n) for n in ('configuration.json', 'window-status.json'))
        assert config['ExpectedTestCases'] == status['PassedFocusedTests'] == sum(counts.values())
        assert config['NoLiveMonitoring'] and not status['LiveBrainAccessed'] and not status['OldSavesLoaded']
        assert not status['PhysiologyQualified'] and not status['AvatarRecoveryAssessed'] and not status['FullUnitSuitePassed']
        report.update(operatorReportedCompletion=True, verifiedGitFiles=source_count, passedTests=len(cases), testClassCounts=counts,
                      testsExecutedDuringReview=False, modelExecutedDuringReview=False)
        reports[name] = report
        print(name + ': capture, Git archive, binaries and exact test names verified', flush=True)
    status = common.read(contacts / 'window-status.json')
    assert status['CombinedObservationAndRegressionChecksPassed'] and not status['ModelExecuted']
    assert not status['LiveUnityIntegrated'] and not status['TimingMeasured']
    reports['contacts'].update(combinedFixtureChecksPassed=True, liveUnityIntegrated=False, physiologyQualified=False)
    # This frozen audit explicitly prohibits solve_ivp before loading references.
    # It compares complete retained arrays, with the original thresholds intact.
    sys.path.insert(0, str(golgi / 'source/tools'))
    analyzer = module('completed_fine_golgi', golgi / 'source/tools/analyse-golgi-sdirk3-fine.py')
    analysis = analyzer.audit(golgi)
    assert analysis == common.read(golgi / 'fine-analysis.json')
    assert analysis['completedCases'] == analysis['caseCount'] == 14 and analysis['retainedBaselineCases'] == 38
    assert analysis['totalConditions'] == 52 and analysis['numericalCriteriaPassed']
    assert not any(analysis[k] for k in ('originalBoundsChanged', 'referenceSolved', 'physiologyQualified', 'activeAxonQualified', 'avatarRecoveryAssessed'))
    assert len(analysis['assessment']) == 8 and all(a['passed'] for a in analysis['assessment'])
    pre = common.read(golgi / 'fine-preflight.json')
    assert pre['originalAnalysisReproduced'] and len(pre['environmentControls']) == 9
    assert pre['savedAnalysisCompatibleWithDeclaredRuntime'] and not pre['originalRuntimeReproduced']
    comparisons = [c for row in analysis['cases'] for c in row.get('comparisons', {}).values()]
    maximum = {f: max(c[f] for c in comparisons) for f in ('voltageMv', 'gate', 'sk', 'calciumMm', 'crossingMs')}
    assert all(c['equalCrossingCounts'] for c in comparisons)
    reports['golgi'].update(savedAnalysisExactlyReproduced=True, passedEnvironmentControls=9,
        newConditions=14, retainedConditions=38, passedProtocolAssessments=8, maximumActiveErrors=maximum,
        criteria=analysis['criteria'], numericalCriteriaPassed=True, originalBoundsChanged=False,
        referenceSolved=False, physiologyQualified=False, activeAxonQualified=False, avatarRecoveryAssessed=False)
    output.mkdir()
    for name, report in reports.items():
        (output / (name + '-review.json')).write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    (output / 'review.json').write_text(json.dumps(dict(operatorReportedBoth=True,
        reviewedUtc=datetime.now(timezone.utc).isoformat(), modelsExecuted=False, testsExecuted=False), indent=2) + '\n', encoding='utf-8')
    print(json.dumps(reports, indent=2), flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('output', type=Path)
    parser.add_argument('--operator-reported-both', action='store_true', required=True)
    args = parser.parse_args(); main(args.output.resolve())

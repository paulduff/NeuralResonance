"""Audit two operator-reported failures. No tests, ODEs or physics execute."""
from collections import Counter
from datetime import datetime, timezone
from io import BytesIO
import importlib.util
import json
from pathlib import Path
import re
import subprocess
import xml.etree.ElementTree as ET
from zipfile import ZipFile

ROOT = Path(__file__).resolve().parents[1]


def main():
    spec = importlib.util.spec_from_file_location('capture_audit', ROOT / 'tools/review-olive-and-motor-20261109.py')
    common = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(common)

    def git_source(package, revision):
        generated = subprocess.check_output(['git', 'archive', '--format=zip', revision], cwd=ROOT)
        with ZipFile(BytesIO(generated)) as git, ZipFile(package / 'source.zip') as frozen:
            assert frozen.comment.decode().strip() == revision
            assert git.namelist() == frozen.namelist()
            count = 0
            for entry in frozen.infolist():
                assert git.read(entry.filename) == frozen.read(entry)
                if not entry.is_dir():
                    assert common.io(package / 'source' / entry.filename).read_bytes() == frozen.read(entry)
                    count += 1
        return count

    unity = ROOT / 'artifacts/unity-body-contact-20261113-162523'
    revision = 'c5438665baeb75eeecd1382785ed96be39b77f5a'
    body_report = common.verify(unity, ['test-binaries.json'], revision, 'FAILED', 1)
    body_report['verifiedGitFiles'] = git_source(unity, revision)
    counts = dict(PhysicalObservationTests=31, UnityBodyEncodingTests=4, PhysicalSampleProducerTests=16,
                  PhysicalSampleTransportTests=22, SolutionManifestTests=1)
    config = common.read(unity / 'configuration.json')
    assert config['ExpectedClassCounts'] == counts and config['ExpectedTestCases'] == 74
    expected = set()
    for name, count in counts.items():
        source = common.io(unity / 'source/tests/NeuralResonanceEngine.DNNE.Tests' / (name + '.cs')).read_text()
        methods = re.findall(r'\[Fact\]\s*public\s+(?:async\s+)?(?:void|Task)\s+(\w+)\(', source)
        assert len(methods) == len(set(methods)) == count
        expected.update('NeuralResonanceEngine.DNNE.Tests.' + name + '.' + m for m in methods)
    ns = {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
    cases = ET.parse(unity / 'test-results/focused.trx').findall('.//t:UnitTestResult', ns)
    assert len(cases) == 74 and {c.attrib['testName'] for c in cases} == expected
    assert Counter(c.attrib['outcome'] for c in cases) == {'Passed': 73, 'Failed': 1}
    failed = next(c for c in cases if c.attrib['outcome'] == 'Failed')
    assert failed.attrib['testName'].endswith('PhysicalObservationTests.WrongStreamClockSampleIdentityAndPayloadAreRejected')
    message = failed.find('.//t:Message', ns).text
    assert 'No exception was thrown' in message
    test_source = common.io(unity / 'source/tests/NeuralResonanceEngine.DNNE.Tests/PhysicalObservationTests.cs').read_text()
    protocol = common.io(unity / 'source/Shared.Contracts/PhysicalObservationProtocol.cs').read_text()
    assert 'p with { Version = 2 }' in test_source and 'public const int Version = 2;' in protocol
    status = common.read(unity / 'window-status.json')
    assert not status['PhysicsStudyAttempted'] and not status['UnityBuildSucceeded']
    assert not (unity / 'study').exists() and not (unity / 'player').exists()
    body_report.update(passedTests=73, failedTests=1, failedTest=failed.attrib['testName'], message=message,
        nativeSchemaTestsPassed=4, physicalModelExecuted=False,
        diagnosis='The rejection fixture supplies the current valid protocol version 2; it is not an invalid packet.')

    spindle = ROOT / 'artifacts/spindle-reference-20261113-9a5090'
    revision = 'd3f148e5e281f3354b4c391f0f2b160768807abb'
    spindle_report = common.verify(spindle, [], revision, 'FAILED', 1)
    spindle_report['verifiedGitFiles'] = git_source(spindle, revision)
    before = common.read(spindle / 'runtime-before.json')
    assert before == common.read(spindle / 'runtime-after.json') == common.read(spindle / 'preparation-verification.json')
    assert before['verifiedRuntimeFiles'] == 59532 and not before['modelExecutedDuringVerification']
    config = common.read(spindle / 'configuration.json')
    archive = Path(config['ExternalFiles'][0]['Path'])
    assert common.digest(archive) == '050F71D846360410E4E917B74FB7A2F3E497C16E2107F0F53552C069D8869444'
    with ZipFile(archive) as upstream:
        root = 'spindleModelTutorial-' + config['UpstreamRevision'] + '/'
        for entry in upstream.infolist():
            assert entry.filename.startswith(root)
            if not entry.is_dir():
                assert common.io(spindle / 'upstream' / entry.filename[len(root):]).read_bytes() == upstream.read(entry)
    execution = common.read(spindle / 'mechanics/execution.json')
    protocols = ('quiet-hold', 'active-hold', 'drive-on-off', 'stretch-shorten', 'imposed-below-slack')
    labels = {f'{fiber}-{p}-{dt}us' for fiber in ('bag', 'chain') for p in protocols for dt in (1000, 500, 250)}
    boundary = {fiber + '-state-boundaries' for fiber in ('bag', 'chain')}
    outcomes = execution['outcomes']
    assert len(outcomes) == 32 and {o['label'] for o in outcomes} == labels | boundary
    for outcome in outcomes:
        assert not outcome['completed'] and outcome['error'] == 'derivs: function called with too many inputs'
        data = common.read(spindle / 'mechanics' / (outcome['label'] + '.json'))
        assert data['error'] == outcome['error']
        assert (spindle / 'mechanics' / (outcome['label'] + '.mat')).stat().st_size > 0
        if outcome['label'] in labels:
            assert data['completedSteps'] == 0 and len(data['snapshots']) == 1
            assert data['failedAttempt']['step'] == 1
            assert data['failedAttempt']['y'] == [1] + [0] * 81 + [1, 0]
    resolution = common.read(spindle / 'mechanics/resolution.json')
    runtime_manifest = common.read(spindle / 'runtime-files.json')
    runtime = Path(config['RuntimeDirectory'])
    entry = next(e for e in runtime_manifest['files'] if e['path'] == Path(resolution['ode23']).relative_to(runtime).as_posix())
    assert common.digest(resolution['ode23']) == entry['sha256'].upper()
    octave_source = common.io(resolution['ode23']).read_text()
    assert 'if (! isstruct (varargin{1}))' in octave_source and 'funarguments = varargin;' in octave_source
    for fiber in ('Bag', 'Chain'):
        path = spindle / 'upstream/vendor/matlabMuscleSpindleModellingTools/spindle_model' / ('@halfSarcWithCoop' + fiber) / ('halfSarcWithCoop' + fiber + '.m')
        source = common.io(path).read_text()
        assert source.count('ode23(@derivs,time_step*[0 1],y,[])') == 1
        assert 'function dy = derivs(time_step,y)' in source
    spindle_report.update(verifiedRuntimeFilesBeforeAndAfter=59532, failedTrajectories=30, failedBoundaryRecords=2,
        completedIntegrationSteps=0, error='derivs: function called with too many inputs',
        diagnosis='Octave treats the non-struct fourth argument [] as an extra derivative argument; the upstream callback takes two inputs.',
        numericalAccuracyAssessed=False, physiologyQualified=False)
    output = ROOT / 'artifacts/spindle-unity-failure-review-20261113'
    output.mkdir()
    (output / 'review.json').write_text(json.dumps(dict(reviewedUtc=datetime.now(timezone.utc).isoformat(),
        operatorReportedBothComplete=True, unity=body_report, spindle=spindle_report,
        testsExecutedDuringReview=False, modelsExecutedDuringReview=False), indent=2) + '\n')
    print(json.dumps(dict(output=str(output), unity=body_report, spindle=spindle_report), indent=2))


if __name__ == '__main__':
    main()

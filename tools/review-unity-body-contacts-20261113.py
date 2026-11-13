"""Read the reported native combined capture; never step Unity or a model."""
from collections import Counter
from datetime import datetime, timezone
from io import BytesIO
import importlib.util
import json
import math
from pathlib import Path
import re
import struct
import subprocess
import xml.etree.ElementTree as ET
from zipfile import ZipFile

ROOT = Path(__file__).resolve().parents[1]


def main():
    spec = importlib.util.spec_from_file_location('capture_hashes', ROOT / 'tools/review-olive-and-motor-20261109.py')
    common = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(common)
    package = ROOT / 'artifacts/unity-body-contact-20261113-7800c5'
    revision = 'c0ce3e9e7a42a25d19821819b0a47e7933cc2e1d'
    report = common.verify(package, ['test-binaries.json', 'probe-binaries.json'], revision)
    regenerated = subprocess.check_output(['git', 'archive', '--format=zip', revision], cwd=ROOT)
    with ZipFile(BytesIO(regenerated)) as git, ZipFile(package / 'source.zip') as frozen:
        assert frozen.comment.decode().strip() == revision and git.namelist() == frozen.namelist()
        files = 0
        for entry in frozen.infolist():
            assert frozen.read(entry) == git.read(entry.filename)
            if not entry.is_dir():
                assert common.io(package / 'source' / entry.filename).read_bytes() == frozen.read(entry)
                files += 1
    counts = dict(PhysicalObservationTests=31, UnityBodyEncodingTests=4, PhysicalSampleProducerTests=16,
                  PhysicalSampleTransportTests=22, SolutionManifestTests=1)
    config = common.read(package / 'configuration.json')
    status = common.read(package / 'window-status.json')
    assert config['ExpectedClassCounts'] == counts and config['ExpectedTestCases'] == 74
    assert config['CombinedBodyContacts'] and config['ObservationProtocolVersion'] == 2
    expected = set()
    for name, count in counts.items():
        source = common.io(package / 'source/tests/NeuralResonanceEngine.DNNE.Tests' / (name + '.cs')).read_text()
        methods = re.findall(r'\[Fact\]\s*public\s+(?:async\s+)?(?:void|Task)\s+(\w+)\(', source)
        assert len(methods) == len(set(methods)) == count
        expected.update('NeuralResonanceEngine.DNNE.Tests.' + name + '.' + m for m in methods)
    cases = ET.parse(package / 'test-results/focused.trx').findall('.//t:UnitTestResult',
        {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'})
    assert len(cases) == 74 and {c.attrib['testName'] for c in cases} == expected
    assert Counter(c.attrib['outcome'] for c in cases) == {'Passed': 74}
    assert all(status[k] for k in ('ProducerAndTransportChecksPassed', 'UnityBuildSucceeded', 'PhysicsStudyAttempted',
                                 'UnityPhysicsChecksPassed', 'CompleteBodyContactChecksPassed'))
    assert not any(status[k] for k in ('LiveBrainAccessed', 'OldSavesLoaded', 'NeuronalModelExecuted',
        'LiveUnityIntegrated', 'TimingMeasured', 'PhysiologyQualified', 'AvatarRecoveryAssessed', 'FullUnitSuitePassed'))
    player_files = common.read(package / 'player-files.json')
    for entry in player_files:
        path = Path(entry['Path']); path.relative_to(package / 'player')
        assert common.digest(path) == entry['Sha256']
    compiled = 0
    built = common.io(package / 'built-project/Assets')
    for path in built.rglob('*.cs'):
        assert path.read_bytes() == common.io(package / 'source/UnityWorld/Assets' / path.relative_to(built)).read_bytes()
        compiled += 1
    assert compiled > 0
    probe = package / 'source/artifacts/unity-physical-probe/bin/UnityPhysicalSampleProbe/release/UnityPhysicalSampleProbe.dll'
    audit = json.loads(subprocess.check_output([config['DotnetExecutable'], str(probe), '--audit-combined', str(package / 'study')],
                                             cwd=ROOT, encoding='utf-8'))
    assert audit == common.read(package / 'study/saved-analysis.json')
    result = common.read(package / 'study/study-result.json')
    assert result['completed'] and result['exitCode'] == 0 and result['completeBodyAndContactDeliveryChecksPassed']
    assert result['actualPhysicsSteps'] == audit['actualPhysicsSteps'] == 201
    assert result['capturedAndConsumedSamples'] == audit['deliveredBodySamples'] == 201
    assert result['deliveredContactObservations'] == audit['deliveredContactObservations'] == 3015
    assert result['completeThroughStep'] == audit['completeThroughStep'] == 200
    assert audit['originalGridSamples'] == 202 and audit['heldBodyAndContactInspections'] == 4
    assert audit['missingContactInjectionRejected'] and not audit['nativeSourceActuallyLostContact']

    # Independent field-by-field audit of every delivered native measurement.
    # Shared float fields are compared as binary32, substrate fields as binary64;
    # this accounts for equivalent JSON decimal renderings, not tolerance fitting.
    def typed(value, path=()):
        if isinstance(value, dict):
            return {k: typed(v, path + (k,)) for k, v in value.items()}
        if isinstance(value, list):
            return [typed(v, path) for v in value]
        if isinstance(value, (int, float)) and not isinstance(value, bool):
            assert math.isfinite(value)
            if path[-1] not in ('sequence', 'timestampMs') and 'neuralSubstrates' not in path:
                return struct.pack('<f', value).hex()
        return value
    study = package / 'study'
    rows = common.read(study / 'source-grid.json')
    originals = {r['step']: common.read(study / r['replyFile']) for r in rows}
    consumed = common.read(study / 'consumed.json')
    layout = common.read(study / 'layout.json')
    omitted = ('leftHandApertureFraction', 'rightHandApertureFraction', 'leftGripForceNewtons', 'rightGripForceNewtons',
               'leftHandFatigue', 'rightHandFatigue', 'leftHandSlip', 'rightHandSlip')
    for name in omitted:
        assert '/articulation/' + name in layout['bodyProfile']['unavailableMembers']
    contacts = 0
    for step, sample in enumerate(consumed):
        assert sample['physicsStep'] == step
        frame = json.loads(sample['observationJson'])
        original = originals[step]
        body = json.loads(original['bodyJson'])
        for name in omitted:
            assert name not in body['articulation']
            body['articulation'][name] = 1 if name.endswith('ApertureFraction') else 0
        assert typed(body) == typed(frame['body']), step
        native = {c['inputSource']: c for c in map(json.loads, original['contactsJson'])}
        assert len(native) == len(frame['contacts']) == len(layout['contactSites']) == 15
        for site, received in zip(layout['contactSites'], frame['contacts']):
            assert site['siteId'] == received['siteId']
            assert typed(native[site['sourceId']]) == typed(received['observation']), (step, site)
            contacts += 1
    assert len(consumed) == 201 and contacts == 3015
    report.update(verifiedGitFiles=files, verifiedBuiltCSharpFiles=compiled, verifiedPlayerFiles=len(player_files),
        passedTests=74, exactSavedAuditReproduction=True, independentlyComparedBodies=201,
        independentlyComparedContacts=3015, result=result, savedAudit=audit,
        testsExecutedDuringReview=False, modelsExecutedDuringReview=False)
    output = ROOT / 'artifacts/unity-body-contact-review-20261113'
    output.mkdir()
    (output / 'review.json').write_text(json.dumps(dict(reviewedUtc=datetime.now(timezone.utc).isoformat(),
        operatorReportedCompletion=True, **report), indent=2) + '\n')
    print(json.dumps(report, indent=2))


if __name__ == '__main__':
    main()

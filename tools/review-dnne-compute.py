"""Review an operator-reported completed CPU study without changing its capture."""
from datetime import datetime, timezone
from hashlib import sha256
import importlib.util
import json
from pathlib import Path
import sys
import xml.etree.ElementTree as ET

capture=Path(sys.argv[1]).resolve();output=Path(sys.argv[2]).resolve()
assert not output.exists(),'Use a new review directory; preserve the completed capture.'
read=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
def digest(path):
    h=sha256()
    with path.open('rb') as stream:
        for block in iter(lambda:stream.read(4*1024*1024),b''):h.update(block)
    return h.hexdigest().upper()

manifest=read(capture/'backup-manifest.json');backup=Path(manifest['Destination'])
assert (capture/'backup-manifest.json').read_bytes()==(backup/'backup-manifest.json').read_bytes()
expected=set();total=0
for entry in manifest['Files']:
    relative=Path(entry['Path'])
    assert not relative.is_absolute() and '..' not in relative.parts
    assert relative.as_posix() not in expected;expected.add(relative.as_posix())
    for folder in (capture,backup):
        path=folder/relative
        assert path.stat().st_size==entry['Bytes'] and digest(path)==entry['Sha256'],str(path)
    total+=entry['Bytes']
for folder in (capture,backup):
    actual={p.relative_to(folder).as_posix() for p in folder.rglob('*') if p.is_file()}
    assert actual==expected|{'backup-manifest.json'},str(folder)
configuration=read(capture/'configuration.json')
for entry in configuration['PackageFiles']:assert digest(capture/entry['Path'])==entry['Sha256']
for name in ('test-binaries.json','probe-binaries.json'):
    for entry in read(capture/name):
        path=Path(entry['Path']);assert path.is_relative_to(capture) and digest(path)==entry['Sha256']
status=read(capture/'window-status.json')
assert status['Revision']==configuration['Revision']
assert status['Status']=='DIAGNOSTICS_COMPLETE' and status['ExitCode']==0
assert status['ObservationChecksPassed'] and status['PerformanceCaptureComplete']
assert not any(status[k] for k in ('LiveBrainAccessed','OldSavesLoaded','GpuMeasured','NetworkTransportMeasured',
    'UnityPhysicsMeasured','PhysiologyQualified','AvatarRecoveryAssessed','FullUnitSuitePassed'))
ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
tests=ET.parse(capture/'test-results/focused.trx').findall('.//t:UnitTestResult',ns)
assert len(tests)==status['PassedFocusedTests']==configuration['ExpectedTestCases']==56
assert all(t.attrib['outcome']=='Passed' for t in tests)
for name in configuration['TestClasses']:
    assert any(t.attrib['testName'].startswith('NeuralResonanceEngine.DNNE.Tests.'+name+'.') for t in tests),name
spec=importlib.util.spec_from_file_location('compute_audit',Path(__file__).with_name('analyse-dnne-compute.py'))
audit=importlib.util.module_from_spec(spec);spec.loader.exec_module(audit)
data=read(capture/'study/results.json');protocol=read(capture/'study/protocol.json');saved=read(capture/'study/analysis.json')
assert data['completed'] and protocol['version']=='isolated-cpu-costs-v2'
assert protocol['durationMs']==1000 and protocol['pairs']==3
assert [c['Key'] for c in protocol['cases']]==audit.expected
assert len(data['neuralRuns'])==42 and len(data['body']['runs'])==21
neural=audit.audit_neural(capture/'study',data,protocol);body=audit.audit_body(capture/'study',data)
assert neural==saved['neural'] and body==saved['motor']
prior=read(capture/'prior-compute-review.json')
baseline={(r['key'],r['run']):(r['outputsSha256'],r['stateSha256']) for r in prior['neuralEvidence']}
current={(r['Key'],r['Run']):(r['OutputsSha256'],r['StateSha256']) for r in data['neuralRuns']}
assert len(baseline)==42 and baseline==current and saved['previousNeuralEvidenceReproduced']
assert data['exactNeuralObservationPairs']==saved['neuralObservationPairs']==18
assert data['body']['exactSerializationPairs']==saved['motorSerializationPairs']==9
report=dict(reviewedUtc=datetime.now(timezone.utc).isoformat(),capture=str(capture),backup=str(backup),
    sourceRevision=configuration['Revision'],completedUtc=status['CompletedUtc'],originalStatus=status['Status'],
    verifiedCopies=True,verifiedFilesPerCopy=len(manifest['Files']),verifiedBytesPerCopy=total,
    packagedFiles=len(configuration['PackageFiles']),passedFocusedTests=len(tests),completedNeuralRuns=42,
    completedMotorRuns=21,exactNeuralObservationPairs=18,exactMotorSerializationPairs=9,
    previousNeuralEvidenceReproduced=True,neural=neural,motor=body,
    gpuMeasured=False,networkTransportMeasured=False,unityPhysicsMeasured=False,
    physiologyQualified=False,avatarRecoveryAssessed=False)
output.mkdir(parents=True)
(output/'review.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
print(json.dumps({k:v for k,v in report.items() if k not in ('neural','motor')},indent=2))

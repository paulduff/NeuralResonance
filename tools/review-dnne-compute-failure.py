"""Verify an operator-reported failed capture and its completed neural evidence."""
from datetime import datetime, timezone
from hashlib import sha256
import importlib.util
import json
from pathlib import Path
import sys
import xml.etree.ElementTree as ET

capture=Path(sys.argv[1]).resolve();output=Path(sys.argv[2]).resolve()
assert not output.exists(),'Use a new review directory; never alter the failed capture.'
read=lambda path:json.loads(path.read_text(encoding='utf-8-sig'))
manifest=read(capture/'backup-manifest.json');backup=Path(manifest['Destination'])
assert (capture/'backup-manifest.json').read_bytes()==(backup/'backup-manifest.json').read_bytes()
def digest(path):
    h=sha256()
    with path.open('rb') as stream:
        for block in iter(lambda:stream.read(4*1024*1024),b''):h.update(block)
    return h.hexdigest().upper()
total=0
for entry in manifest['Files']:
    relative=Path(entry['Path'])
    assert not relative.is_absolute() and '..' not in relative.parts
    for folder in (capture,backup):
        path=folder/relative
        assert path.stat().st_size==entry['Bytes'] and digest(path)==entry['Sha256'],str(path)
    total+=entry['Bytes']
configuration=read(capture/'configuration.json')
for entry in configuration['PackageFiles']:assert digest(capture/entry['Path'])==entry['Sha256']
for name in ('test-binaries.json','probe-binaries.json'):
    for entry in read(capture/name):
        path=Path(entry['Path']);assert path.is_relative_to(capture) and digest(path)==entry['Sha256']
status=read(capture/'window-status.json');failed=read(capture/'study/FAILED.json')
assert configuration['Revision']==status['Revision']=='67e7ea55c997276614f88243fe895f14afc48c0e'
assert status['Status']=='FAILED' and status['PassedFocusedTests']==53
assert 'System.IntPtr' in failed['error'] and 'MotorPlantStudy.Run' in failed['error']
ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
tests=ET.parse(capture/'test-results/focused.trx').findall('.//t:UnitTestResult',ns)
assert len(tests)==53 and all(t.attrib['outcome']=='Passed' for t in tests)
spec=importlib.util.spec_from_file_location('compute_audit',Path(__file__).with_name('analyse-dnne-compute.py'))
audit=importlib.util.module_from_spec(spec);spec.loader.exec_module(audit)
rows=failed['completedNeuralRuns'];assert len(rows)==42
neural=audit.audit_neural(capture/'study',{'neuralRuns':rows},read(capture/'study/protocol.json'))
output.mkdir(parents=True)
report=dict(reviewedUtc=datetime.now(timezone.utc).isoformat(),capture=str(capture),backup=str(backup),
    sourceRevision=configuration['Revision'],originalStatus='FAILED',originalStatusPreserved=True,
    verifiedCopies=True,verifiedFilesPerCopy=len(manifest['Files']),verifiedBytesPerCopy=total,
    packagedFiles=len(configuration['PackageFiles']),passedFocusedTests=53,completedNeuralRuns=42,
    exactNeuralObservationPairs=18,neural=neural,
    neuralEvidence=[dict(key=r['Key'],run=r['Run'],outputsSha256=r['OutputsSha256'],stateSha256=r['StateSha256']) for r in rows],
    failure='Diagnostic reflection attempted JSON serialization of System.IntPtr in motor plant state evidence.',
    motorStudyComplete=False,performanceStudyComplete=False,gpuMeasured=False,physiologyQualified=False,avatarRecoveryAssessed=False)
(output/'review.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
print(json.dumps({k:v for k,v in report.items() if k not in ('neural','neuralEvidence')},indent=2))
for row in neural: print(row['key'],'plain tick median ms',round(row['medianPlainTickMs'],3),'timing overhead ratio',round(row['medianTimingOverheadRatio'],3))

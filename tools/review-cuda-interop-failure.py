"""Preserve and verify the reported NVRTC helper-load failure, without native calls."""
from datetime import datetime, timezone
from hashlib import sha256
import json
from pathlib import Path
import sys

capture=Path(sys.argv[1]).resolve();output=Path(sys.argv[2]).resolve()
assert not output.exists(),'Use a new review directory.'
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
    relative=Path(entry['Path']);assert not relative.is_absolute() and '..' not in relative.parts
    assert relative.as_posix() not in expected;expected.add(relative.as_posix())
    for folder in (capture,backup):
        path=folder/relative;assert path.stat().st_size==entry['Bytes'] and digest(path)==entry['Sha256']
    total+=entry['Bytes']
for folder in (capture,backup):
    assert {p.relative_to(folder).as_posix() for p in folder.rglob('*') if p.is_file()}==expected|{'backup-manifest.json'}
configuration=read(capture/'configuration.json')
for entry in configuration['PackageFiles']:assert digest(capture/entry['Path'])==entry['Sha256']
for entry in read(capture/'probe-binaries.json'):
    path=Path(entry['Path']);assert path.is_relative_to(capture) and digest(path)==entry['Sha256']
status=read(capture/'window-status.json');failure=read(capture/'study/FAILED.json');device=read(capture/'study/device.json')
assert status['Status']=='FAILED' and status['ExitCode']==1 and status['Revision']==configuration['Revision']=='5cd3a850814ae373bfdc35f92891df0ce53ec70d'
assert 'failed to open nvrtc-builtins64_130.dll' in failure['error'] and not failure['completedCases']
assert not (capture/'study/interop.ptx').exists() and not (capture/'study/results.json').exists()
helper=capture/'compiler/unpacked/cuda_nvrtc-windows-x86_64-13.0.88-archive/bin/x64/nvrtc-builtins64_130.dll'
assert digest(helper)=='CD4CE3E0351B0D10F43B07C667782028420E03858999FAB65207C40A28546D19'
report=dict(reviewedUtc=datetime.now(timezone.utc).isoformat(),capture=str(capture),backup=str(backup),
    sourceRevision=configuration['Revision'],completedUtc=status['CompletedUtc'],originalStatus='FAILED',
    verifiedCopies=True,verifiedFilesPerCopy=len(manifest['Files']),verifiedBytesPerCopy=total,packagedFiles=len(configuration['PackageFiles']),
    failure=failure['error'],verifiedHelperPresent=True,device=device,completedArithmeticCases=0,
    interopQualified=False,speedupMeasured=False,physiologyQualified=False,avatarRecoveryAssessed=False)
output.mkdir(parents=True);(output/'review.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
print(json.dumps({k:v for k,v in report.items() if k!='failure'},indent=2))

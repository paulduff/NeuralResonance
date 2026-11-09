"""Fetch a pinned official compiler into artifacts; never install drivers or execute it."""
from datetime import datetime, timezone
from hashlib import sha256
import json
from pathlib import Path, PurePosixPath
import shutil
from urllib.request import urlopen
from zipfile import ZipFile

root = Path(__file__).resolve().parents[1]
metadata_url = 'https://developer.download.nvidia.com/compute/cuda/redist/redistrib_13.0.2.json'
relative = 'cuda_nvrtc/windows-x86_64/cuda_nvrtc-windows-x86_64-13.0.88-archive.zip'
expected = '8c50a52467826167e0dbe99936140c52d62272bfc5849fe2d6587d050c8c5d29'
size = 318076161
directory = root/'artifacts/cuda-nvrtc-13.0.88'
manifest_path = directory/'dependency.json'
def digest(path):
    h=sha256()
    with path.open('rb') as stream:
        for block in iter(lambda:stream.read(4*1024*1024),b''): h.update(block)
    return h.hexdigest()
if directory.exists():
    if not manifest_path.is_file(): raise RuntimeError('Partial compiler package exists; inspect it rather than overwriting.')
    manifest=json.loads(manifest_path.read_text(encoding='utf-8'))
    assert manifest['archiveSha256']==expected and manifest['archiveBytes']==size
    for entry in manifest['files']:
        path=directory/entry['path']; assert path.resolve().is_relative_to(directory.resolve())
        assert digest(path)==entry['sha256'] and path.stat().st_size==entry['bytes']
    print(json.dumps(dict(directory=str(directory),verified=True,compilerExecuted=False),indent=2))
else:
    directory.mkdir(parents=True)
    with urlopen(metadata_url,timeout=60) as response: raw=response.read()
    metadata=json.loads(raw); component=metadata['cuda_nvrtc']; platform=component['windows-x86_64']
    assert metadata['release_label']=='13.0.2' and component['version']=='13.0.88'
    assert platform['relative_path']==relative and platform['sha256']==expected and int(platform['size'])==size
    (directory/'redistrib_13.0.2.json').write_bytes(raw)
    archive_path=directory/'nvrtc-13.0.88.zip'
    with urlopen('https://developer.download.nvidia.com/compute/cuda/redist/'+relative,timeout=60) as source, archive_path.open('xb') as target:
        shutil.copyfileobj(source,target,4*1024*1024)
    assert archive_path.stat().st_size==size and digest(archive_path)==expected,'Compiler archive hash mismatch; do not load it.'
    with ZipFile(archive_path) as archive:
        for entry in archive.infolist():
            path=PurePosixPath(entry.filename)
            assert not path.is_absolute() and '..' not in path.parts and '\\' not in entry.filename and ':' not in entry.filename
            assert ((entry.external_attr>>16)&0o170000)!=0o120000,'No archive symlinks.'
        archive.extractall(directory/'unpacked')
    dlls=list((directory/'unpacked').rglob('nvrtc64_130_0.dll'))
    builtins=list((directory/'unpacked').rglob('nvrtc-builtins64_130.dll'))
    assert len(dlls)==len(builtins)==1 and dlls[0].parent==builtins[0].parent
    assert list((directory/'unpacked').rglob('LICENSE*')),'Preserve the supplied license.'
    files=[dict(path=p.relative_to(directory).as_posix(),sha256=digest(p),bytes=p.stat().st_size)
           for p in sorted(directory.rglob('*')) if p.is_file()]
    manifest=dict(version='pinned-nvrtc-dependency-v1',retrievedUtc=datetime.now(timezone.utc).isoformat(),
        metadataUrl=metadata_url,archiveUrl='https://developer.download.nvidia.com/compute/cuda/redist/'+relative,
        archiveSha256=expected,archiveBytes=size,compilerVersion='13.0.88',
        compilerDll=dlls[0].relative_to(directory).as_posix(),builtinsDll=builtins[0].relative_to(directory).as_posix(),files=files,
        compilerExecuted=False,driverChanged=False,systemPathChanged=False)
    manifest_path.write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(dict(directory=str(directory),verified=True,compilerExecuted=False,files=len(files)),indent=2))

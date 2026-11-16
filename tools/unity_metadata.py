"""Validate frozen Unity asset identities before starting an editor/build."""
from hashlib import sha256
from pathlib import Path
import re


def validate(project):
    assets=Path(project)/'Assets'
    if not assets.is_dir():raise ValueError('Unity Assets directory is missing: '+str(assets))
    entries=[]; owners={}
    for path in sorted(assets.rglob('*.meta')):
        text=path.read_text(encoding='utf-8-sig')
        matches=re.findall(r'^guid:[ \t]*([^\r\n]*)',text,re.M)
        if len(matches)!=1 or not re.fullmatch(r'[0-9a-fA-F]{32}',matches[0].strip()) or matches[0].strip()=='0'*32:
            raise ValueError('Invalid Unity asset GUID (require exactly 32 hexadecimal characters): '+str(path))
        guid=matches[0].strip().lower()
        if guid in owners:raise ValueError('Duplicate Unity asset GUID: '+str(owners[guid])+' and '+str(path))
        owners[guid]=path
        entries.append(dict(path=path.relative_to(project).as_posix(),guid=guid,sha256=sha256(path.read_bytes()).hexdigest().upper()))
    scripts=sorted(assets.rglob('*.cs'))
    for path in scripts:
        if not path.with_suffix('.cs.meta').is_file():raise ValueError('Missing stable Unity script metadata: '+str(path))
    if not entries or not scripts:raise ValueError('Expected Unity asset metadata and C# sources.')
    return dict(metadataFiles=len(entries),scriptFiles=len(scripts),uniqueNonzeroGuids=True,allScriptSidecarsPresent=True,files=entries)

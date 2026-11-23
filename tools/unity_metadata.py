"""Validate frozen Unity asset identities before starting an editor/build."""
from hashlib import sha256
import json
from pathlib import Path
import re
import shutil


def avatar_assets(project):
    """Check the attributed, non-Git build inputs without importing Unity assets."""
    project = Path(project)
    folder = project/'Assets/Avatar'
    attribution = json.loads((folder/'provenance.json').read_text(encoding='utf-8-sig'))
    entries = attribution['files']
    required = {'MikeAlger.fbx', 'MikeAlger_Texture.jpeg'}
    if len(entries) != 2 or {e['file'] for e in entries} != required:
        raise ValueError('Require exactly the two attributed Avatar assets, with no substituted paths.')
    result = []
    for entry in sorted(entries, key=lambda e: e['file']):
        path = folder/entry['file']; sidecar = folder/(entry['file']+'.meta')
        if not path.is_file() or not sidecar.is_file():
            raise ValueError('Missing attributed Avatar asset or importer metadata: '+str(path))
        data = path.read_bytes(); digest = sha256(data).hexdigest().upper()
        if len(data) != entry['bytes'] or digest != entry['sha256'].upper():
            raise ValueError('Attributed Avatar asset differs: '+str(path))
        matches = re.findall(r'^guid:[ \t]*([0-9a-fA-F]{32})[ \t]*$', sidecar.read_text(encoding='utf-8-sig'), re.M)
        if len(matches) != 1 or matches[0] == '0'*32:
            raise ValueError('Invalid attributed Avatar importer identity: '+str(sidecar))
        result.append(dict(path=path.relative_to(project).as_posix(), bytes=len(data), sha256=digest,
                           metadataSha256=sha256(sidecar.read_bytes()).hexdigest().upper(), guid=matches[0].lower()))
    return dict(attributedAssets=result, modelsImported=False, testsExecuted=False)


def freeze_avatar_assets(source_project, frozen_project):
    """Supply ignored binary assets to a Git snapshot, retaining its importer files."""
    source_project = Path(source_project); frozen_project = Path(frozen_project)
    source = source_project/'Assets/Avatar'; target = frozen_project/'Assets/Avatar'
    assets = avatar_assets(source_project)
    read = lambda p: json.loads(p.read_text(encoding='utf-8-sig'))
    if read(source/'provenance.json') != read(target/'provenance.json'):
        raise ValueError('Working and frozen Avatar attribution differ.')
    paths = [(source_project/e['path'], frozen_project/e['path']) for e in assets['attributedAssets']]
    if any(dst.exists() for _, dst in paths):
        raise ValueError('Refuse to replace existing frozen Avatar assets.')
    for src, dst in paths:
        shutil.copyfile(src, dst)
    return avatar_assets(frozen_project)


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

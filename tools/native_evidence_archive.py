"""Lossless immutable evidence container; never edits or removes source records.

This is a recording format, not a neural/native protocol or a model. Publication
requires a complete byte/hash comparison. Readers expose original bytes only.
"""
from dataclasses import dataclass,field
import fnmatch
import hashlib
import io
import json
import os
from pathlib import Path,PurePosixPath
import stat
from zipfile import ZipFile,ZipInfo,ZIP_DEFLATED,ZIP_STORED

VERSION='dnne-native-evidence-zip-v1'
INDEX='__dnne_evidence_manifest__.json'
CHUNK=1024*1024


def physical(path):
    value=str(Path(path).absolute())
    if os.name=='nt' and not value.startswith('\\\\?\\'):
        value='\\\\?\\UNC\\'+value[2:] if value.startswith('\\\\') else '\\\\?\\'+value
    return Path(value)


def name(value):
    if not isinstance(value,str) or not value or '\\' in value or ':' in value or any(ord(c)<32 for c in value):
        raise ValueError('Unsafe evidence name.')
    parts=value.split('/')
    reserved={'CON','PRN','AUX','NUL'}|{f'{p}{i}' for p in ('COM','LPT') for i in range(1,10)}
    if any(p in ('','.','..') or p[-1] in (' ','.') or p.split('.')[0].upper() in reserved for p in parts):
        raise ValueError('Noncanonical evidence name.')
    if PurePosixPath(value).is_absolute():raise ValueError('Absolute evidence name.')
    return value


def digest_stream(stream):
    algorithm=hashlib.sha256();size=0
    while data:=stream.read(CHUNK):algorithm.update(data);size+=len(data)
    return size,algorithm.hexdigest().upper()


def digest_file(path):
    with physical(path).open('rb') as stream:return digest_stream(stream)[1]


def inventory(source):
    root=physical(source);result={};directories=[root]
    while directories:
        directory=directories.pop();info=directory.lstat()
        if stat.S_ISLNK(info.st_mode) or getattr(info,'st_file_attributes',0)&0x400:
            raise ValueError('Reparse points are not evidence directories.')
        for item in sorted(directory.iterdir()):
            info=item.lstat()
            if stat.S_ISLNK(info.st_mode) or getattr(info,'st_file_attributes',0)&0x400:
                raise ValueError('Reparse points are not evidence files.')
            if stat.S_ISDIR(info.st_mode):directories.append(item);continue
            if not stat.S_ISREG(info.st_mode):raise ValueError('Only regular evidence files are supported.')
            key=name(item.relative_to(root).as_posix())
            if key==INDEX or key.endswith('.tmp'):raise ValueError('Reserved or unpublished evidence file.')
            result[key]=(info.st_size,info.st_mtime_ns)
    if not result or len({p.casefold() for p in result})!=len(result):raise ValueError('Empty or case-ambiguous evidence set.')
    return dict(sorted(result.items()))


class EvidenceArchive:
    def __init__(self,path,expected_sha256=None):
        self.path=Path(path);self.closed=True
        if expected_sha256 is not None and digest_file(path)!=expected_sha256.upper():raise ValueError('Archive digest changed.')
        self.zip=ZipFile(physical(path),'r')
        try:
            infos=self.zip.infolist();names=[i.filename for i in infos]
            if len(names)!=len(set(names)) or len({n.casefold() for n in names})!=len(names):raise ValueError('Duplicate archive entry.')
            for entry in infos:
                name(entry.filename)
                mode=(entry.external_attr>>16)&0o170000
                if entry.is_dir() or mode not in (0,stat.S_IFREG) or entry.flag_bits&1 or entry.compress_type not in (ZIP_STORED,ZIP_DEFLATED):
                    raise ValueError('Unsupported evidence entry type.')
            if INDEX not in names or self.zip.getinfo(INDEX).file_size>512*1024**2:raise ValueError('Missing or oversized evidence index.')
            self.manifest=json.loads(self.zip.read(INDEX))
            if set(self.manifest)!={'version','source','files','totalBytes'} or self.manifest['version']!=VERSION:raise ValueError('Unknown evidence format.')
            rows=self.manifest['files']
            if not isinstance(rows,list) or not rows:raise ValueError('Empty evidence index.')
            for row in rows:
                if set(row)!={'path','bytes','sha256'} or name(row['path'])==INDEX:raise ValueError('Invalid evidence index row.')
                if type(row['bytes']) is not int or row['bytes']<0:raise ValueError('Invalid evidence byte count.')
                if not isinstance(row['sha256'],str) or len(row['sha256'])!=64 or any(c not in '0123456789ABCDEF' for c in row['sha256']):raise ValueError('Invalid evidence digest.')
            self.entries={r['path']:r for r in rows}
            if list(self.entries)!=sorted(self.entries) or len(self.entries)!=len(rows):raise ValueError('Unordered or repeated evidence index.')
            if set(names)!={INDEX}|set(self.entries):raise ValueError('Archive/index file set differs.')
            if any(self.zip.getinfo(r['path']).file_size!=r['bytes'] for r in rows):raise ValueError('Archive/index byte counts differ.')
            if type(self.manifest['totalBytes']) is not int or sum(r['bytes'] for r in rows)!=self.manifest['totalBytes']:raise ValueError('Incorrect total evidence bytes.')
            self.closed=False
        except BaseException:self.zip.close();raise

    def __enter__(self):return self
    def __exit__(self,*args):self.close()
    def close(self):self.zip.close();self.closed=True
    @property
    def root(self):return EvidencePath(self,'')
    def _require_open(self):
        if self.closed:raise ValueError('Evidence archive is closed.')
    def read_bytes(self,key):
        self._require_open();key=name(key);row=self.entries[key];data=self.zip.read(key)
        if len(data)!=row['bytes'] or hashlib.sha256(data).hexdigest().upper()!=row['sha256']:raise ValueError('Evidence digest mismatch: '+key)
        return data
    def verify(self,expected=None,progress=None):
        self._require_open()
        if expected is not None and self.manifest['files']!=expected:raise ValueError('Evidence differs from the declared original manifest.')
        for index,(key,row) in enumerate(self.entries.items(),1):
            with self.zip.open(key) as stream:size,digest=digest_stream(stream)
            if size!=row['bytes'] or digest!=row['sha256']:raise ValueError('Evidence digest mismatch: '+key)
            if progress and (index%10000==0 or index==len(self.entries)):progress('archive-verify',index,len(self.entries))
        return dict(files=len(self.entries),uncompressedBytes=self.manifest['totalBytes'],allMemberDigestsVerified=True)


@dataclass(frozen=True)
class EvidencePath:
    owner: EvidenceArchive=field(repr=False)
    key: str
    def __truediv__(self,part):
        part=name(str(part));return EvidencePath(self.owner,name(self.key+'/'+part if self.key else part))
    @property
    def name(self):return PurePosixPath(self.key).name
    def __lt__(self,other):
        if not isinstance(other,EvidencePath) or self.owner is not other.owner:return NotImplemented
        return self.key<other.key
    def __str__(self):return str(self.owner.path)+'!/'+self.key
    def read_bytes(self):return self.owner.read_bytes(self.key)
    def read_text(self,encoding='utf-8',errors='strict'):return self.read_bytes().decode(encoding,errors)
    def open(self,mode='r',encoding=None):
        if mode not in ('r','rt','rb'):raise ValueError('Evidence paths are read-only.')
        stream=io.BytesIO(self.read_bytes())
        return stream if mode=='rb' else io.TextIOWrapper(stream,encoding=encoding or 'utf-8')
    def exists(self):return self.is_file() or self.is_dir()
    def is_file(self):self.owner._require_open();return self.key in self.owner.entries
    def is_dir(self):
        self.owner._require_open();prefix=self.key+'/' if self.key else ''
        return any(k.startswith(prefix) for k in self.owner.entries) and not self.is_file()
    def glob(self,pattern):
        self.owner._require_open()
        if '/' in pattern or '\\' in pattern or pattern in ('','.','..'):raise ValueError('Only a single directory glob is supported.')
        prefix=self.key+'/' if self.key else ''
        return (EvidencePath(self.owner,k) for k in sorted(self.owner.entries)
            if k.startswith(prefix) and '/' not in k[len(prefix):] and fnmatch.fnmatchcase(k[len(prefix):],pattern))


def pack(source,destination,expected=None,progress=None):
    source=Path(source);destination=Path(destination)
    if not source.is_absolute() or not destination.is_absolute():raise ValueError('Absolute evidence paths required.')
    destination=destination.parent.resolve(strict=True)/destination.name;root=physical(source)
    # An archive must never become part of its own source inventory.
    canonical_source=source.resolve(strict=True)
    if destination==canonical_source or canonical_source in destination.parents:raise ValueError('Archive must be outside the source tree.')
    pending=destination.with_name(destination.name+'.pending')
    if physical(destination).exists() or physical(pending).exists():raise FileExistsError('Never overwrite earlier evidence.')
    before=inventory(source);rows=[]
    # Any unsuccessful attempt leaves its unpublished prefix for diagnosis.
    with physical(pending).open('xb') as output:
        with ZipFile(output,'w',compression=ZIP_DEFLATED,compresslevel=6,allowZip64=True) as archive:
            for index,(key,(size,_)) in enumerate(before.items(),1):
                sha=hashlib.sha256();written=0
                info=ZipInfo(key);info.compress_type=ZIP_DEFLATED;info.external_attr=(stat.S_IFREG|0o444)<<16
                with (root/key).open('rb') as original,archive.open(info,'w',force_zip64=True) as member:
                    while data:=original.read(CHUNK):member.write(data);sha.update(data);written+=len(data)
                if written!=size:raise ValueError('Source size changed while archiving: '+key)
                rows.append(dict(path=key,bytes=written,sha256=sha.hexdigest().upper()))
                if progress and (index%10000==0 or index==len(before)):progress('pack',index,len(before))
            manifest=dict(version=VERSION,source=str(source),files=rows,totalBytes=sum(r['bytes'] for r in rows))
            archive.writestr(INDEX,json.dumps(manifest,separators=(',',':'),ensure_ascii=False).encode('utf-8'))
        output.flush();os.fsync(output.fileno())
    if inventory(source)!=before:raise ValueError('Source inventory changed while archiving.')
    for index,row in enumerate(rows,1):
        if digest_file(root/row['path'])!=row['sha256']:raise ValueError('Source bytes changed while archiving: '+row['path'])
        if progress and (index%10000==0 or index==len(rows)):progress('source-verify',index,len(rows))
    if inventory(source)!=before:raise ValueError('Source inventory changed during verification.')
    with EvidenceArchive(pending) as archive:verified=archive.verify(expected,progress)
    # Atomic create-without-replacement. Both names are in the same directory;
    # only this attempt's temporary hard link is removed after publication.
    os.link(physical(pending),physical(destination));physical(pending).unlink()
    return dict(version=VERSION,source=str(source),archive=str(destination),archiveSha256=digest_file(destination),
        archiveBytes=physical(destination).stat().st_size,**verified,sourceFilesRemoved=0,sourceUnchanged=True)

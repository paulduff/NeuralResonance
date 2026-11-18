"""Read-only motor recording tree with explicit archive mounts and header hashes.

There is no filesystem fallback beneath an archive mount, and no write/delete
or extraction API. A caller keeps its externally owned archives open while the
view is in use. Every access uses the original member or declared header hash.
"""
from dataclasses import dataclass, field
import fnmatch
import hashlib
import io
from pathlib import Path, PurePosixPath
from types import MappingProxyType
from native_evidence_archive import EvidenceArchive, inventory, name, physical

VERSION = 'explicit-native-evidence-view-v1'


class EvidenceView:
    def __init__(self, headers, expected_headers, mounts):
        self.headers = Path(headers)
        self.closed = True
        if not self.headers.is_absolute():
            raise ValueError('Absolute header directory required.')
        actual = inventory(headers)
        rows = [dict(row) for row in expected_headers]
        declared = {}
        for row in rows:
            if set(row) != {'path', 'bytes', 'sha256'} or name(row['path']) in declared:
                raise ValueError('Invalid or duplicated header index.')
            if type(row['bytes']) is not int or row['bytes'] < 0 or not isinstance(row['sha256'], str) or len(row['sha256']) != 64 or any(c not in '0123456789ABCDEF' for c in row['sha256']):
                raise ValueError('Invalid header size or digest.')
            declared[row['path']] = MappingProxyType(row)
        if set(actual) != set(declared) or any(actual[k][0] != row['bytes'] for k, row in declared.items()):
            raise ValueError('Header inventory differs from its declaration.')
        mounted = {}
        for prefix, archive in mounts.items():
            name(prefix)
            if not isinstance(archive, EvidenceArchive):
                raise TypeError('Only verified evidence archive readers can be mounted.')
            archive._require_open()
            folded = prefix.casefold()
            if any(folded == p.casefold() or folded.startswith(p.casefold() + '/') or p.casefold().startswith(folded + '/') for p in mounted):
                raise ValueError('Overlapping or ambiguous archive mounts.')
            if any(k.casefold() == folded or k.casefold().startswith(folded + '/') or folded.startswith(k.casefold() + '/') for k in declared):
                raise ValueError('Header and archive namespaces overlap.')
            mounted[prefix] = archive
        if not mounted:
            raise ValueError('An evidence view requires explicit archive mounts.')
        names = sorted([*declared, *(prefix + '/' + member for prefix, archive in mounted.items() for member in archive.entries)])
        if len(names) != len(set(names)) or len(names) != len({k.casefold() for k in names}):
            raise ValueError('Ambiguous logical file names.')
        # File/directory names must also be unambiguous on Windows.
        kinds = {}
        spelling = {}
        for key in names:
            parts = key.split('/')
            for i in range(1, len(parts) + 1):
                part = '/'.join(parts[:i]); folded = part.casefold(); kind = 'file' if i == len(parts) else 'directory'
                if folded in kinds and (kinds[folded] != kind or spelling[folded] != part):
                    raise ValueError('Ambiguous logical directory or file.')
                kinds[folded] = kind; spelling[folded] = part
        self.expected = MappingProxyType(declared)
        self.mounts = MappingProxyType(mounted)
        self.names = tuple(names)
        self.name_set = frozenset(names)
        self.directories = frozenset(spelling[k] for k, kind in kinds.items() if kind == 'directory') | {''}
        self.closed = False

    def __enter__(self):
        self._require_open()
        return self

    def __exit__(self, *args):
        self.close()

    def close(self):
        self.closed = True

    def _require_open(self):
        if self.closed:
            raise ValueError('Evidence view is closed.')
        for archive in self.mounts.values():
            archive._require_open()

    @property
    def root(self):
        self._require_open()
        return EvidenceViewPath(self, '')

    def read_bytes(self, key):
        self._require_open(); name(key)
        for prefix, archive in self.mounts.items():
            if key.startswith(prefix + '/'):
                # Missing members fail here; never fall back to raw records.
                return archive.read_bytes(key[len(prefix) + 1:])
        if key not in self.expected:
            raise FileNotFoundError('Undeclared evidence path: ' + key)
        row = self.expected[key]
        target = physical(self.headers / key)
        if target.resolve(strict=True) != physical(self.headers).resolve(strict=True).joinpath(*key.split('/')):
            raise ValueError('Header path no longer has its declared location.')
        data = target.read_bytes()
        if len(data) != row['bytes'] or hashlib.sha256(data).hexdigest().upper() != row['sha256']:
            raise ValueError('Header bytes changed: ' + key)
        return data


@dataclass(frozen=True)
class EvidenceViewPath:
    owner: EvidenceView = field(repr=False)
    key: str

    def __truediv__(self, part):
        part = name(str(part))
        return EvidenceViewPath(self.owner, name(self.key + '/' + part if self.key else part))

    @property
    def name(self):
        return PurePosixPath(self.key).name

    def __lt__(self, other):
        if not isinstance(other, EvidenceViewPath) or self.owner is not other.owner:
            return NotImplemented
        return self.key < other.key

    def __str__(self):
        return str(self.owner.headers) + '!view/' + self.key

    def read_bytes(self):
        return self.owner.read_bytes(self.key)

    def read_text(self, encoding='utf-8', errors='strict'):
        return self.read_bytes().decode(encoding, errors)

    def open(self, mode='r', encoding=None):
        if mode not in ('r', 'rt', 'rb'):
            raise ValueError('Evidence views are read-only.')
        stream = io.BytesIO(self.read_bytes())
        return stream if mode == 'rb' else io.TextIOWrapper(stream, encoding=encoding or 'utf-8')

    def is_file(self):
        self.owner._require_open()
        return self.key in self.owner.name_set

    def is_dir(self):
        self.owner._require_open()
        return self.key in self.owner.directories

    def exists(self):
        return self.is_file() or self.is_dir()

    def glob(self, pattern):
        self.owner._require_open()
        if '/' in pattern or '\\' in pattern or pattern in ('', '.', '..'):
            raise ValueError('Only a single directory glob is supported.')
        prefix = self.key + '/' if self.key else ''
        return (EvidenceViewPath(self.owner, k) for k in self.owner.names
                if k.startswith(prefix) and '/' not in k[len(prefix):] and fnmatch.fnmatchcase(k[len(prefix):], pattern))

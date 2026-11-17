"""Declared interpreter transition; numerical compatibility is tested separately."""
from copy import deepcopy
from pathlib import PureWindowsPath
import re


CONTROL_NAMES = [
    'declared-three-binary-transition', 'changed-numerical-library',
    'removed-dependency', 'added-dependency', 'changed-version',
    'undeclared-runtime-hash', 'duplicate-file', 'relocated-dependency',
    'missing-declaration', 'partial-runtime-transition',
]


def describe(previous, current):
    assert previous.keys() == current.keys() == {'python', 'numpy', 'scipy', 'files'}
    assert all(previous[k] == current[k] for k in ('python', 'numpy', 'scipy')), 'Dependency versions changed.'

    def files(environment):
        rows = environment['files']
        assert rows and all(set(r) == {'path', 'sha256'} for r in rows)
        assert all(re.fullmatch('[0-9A-F]{64}', r['sha256']) for r in rows)
        result = {r['path']: r['sha256'] for r in rows}
        assert len(result) == len(rows), 'Duplicate dependency identity.'
        return result

    old, new = files(previous), files(current)
    assert old.keys() == new.keys(), 'Dependency paths changed.'
    changes = [dict(path=p, previous=old[p], current=new[p]) for p in sorted(old) if old[p] != new[p]]
    paths = [PureWindowsPath(r['path']) for r in changes]
    assert len(paths) == 3 and {p.name for p in paths} == {'python.exe', 'python3.dll', 'python312.dll'}, 'Unexpected dependency change.'
    assert len({p.parent for p in paths}) == 1, 'Runtime binaries are not colocated.'
    return dict(kind='rubral-explicit-python-transition-v1', changedFiles=changes,
                unchangedDependencyFiles=len(old)-3, versionsUnchanged=True,
                numericalCompatibilityEstablished=False)


def validate(previous, current, declaration):
    assert declaration == describe(previous, current), 'Undeclared interpreter change.'


def controls():
    """Execute only in the independent PowerShell pipeline."""
    previous = dict(python='fixture', numpy='fixture', scipy='fixture', files=[
        dict(path='C:/fixture/runtime/'+name, sha256='A'*64)
        for name in ('python.exe', 'python3.dll', 'python312.dll', 'numpy.pyd')])
    current = deepcopy(previous)
    for row in current['files'][:3]:
        row['sha256'] = 'B'*64
    declaration = describe(previous, current)
    validate(previous, current, declaration)
    passed = [CONTROL_NAMES[0]]
    mutations = [
        lambda c,d:c['files'][-1].update(sha256='B'*64),
        lambda c,d:c['files'].pop(),
        lambda c,d:c['files'].append(dict(path='C:/fixture/new.dll', sha256='A'*64)),
        lambda c,d:c.update(python='different'),
        lambda c,d:c['files'][0].update(sha256='C'*64),
        lambda c,d:c['files'].append(deepcopy(c['files'][0])),
        lambda c,d:c['files'][-1].update(path='D:/other/numpy.pyd'),
        lambda c,d:d.clear(),
        lambda c,d:c['files'][0].update(sha256='A'*64),
    ]
    for name, mutation in zip(CONTROL_NAMES[1:], mutations, strict=True):
        candidate, declared = deepcopy(current), deepcopy(declaration)
        mutation(candidate, declared)
        try:
            validate(previous, candidate, declared)
        except AssertionError:
            passed.append(name)
        else:
            raise AssertionError('Invalid transition accepted: '+name)
    assert passed == CONTROL_NAMES
    return passed

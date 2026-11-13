"""Explicit interpreter-only transition for replaying the preserved Golgi analysis.

This validates recorded hashes, not numerical compatibility. Exact reproduction
of the old saved analysis remains a separate mandatory preflight gate.
"""
from copy import deepcopy
from pathlib import PureWindowsPath
import re

OLD_SIGNATURE = 'def audit(package):'
NEW_SIGNATURE = 'def audit(package, *, replay_environment=None):'
OLD_CHECK = "    assert environment==read(package/'reference-preflight.json')['environment'],'Numerical dependencies changed during the study.'"
NEW_CHECK = "    expected_environment = read(package/'reference-preflight.json')['environment'] if replay_environment is None else replay_environment\n    assert environment==expected_environment,'Numerical dependencies changed during the study.'"


def validate_analyzer_change(original, candidate):
    assert original.count(OLD_SIGNATURE) == original.count(OLD_CHECK) == 1
    expected = original.replace(OLD_SIGNATURE, NEW_SIGNATURE).replace(OLD_CHECK, NEW_CHECK)
    assert candidate == expected, 'Retained analyzer changed beyond its explicit replay-environment argument.'


def validate_transition(previous, declared, review):
    assert previous.keys() == declared.keys() == {'python', 'numpy', 'scipy', 'files'}
    assert all(previous[k] == declared[k] for k in ('python', 'numpy', 'scipy')), 'Dependency versions changed.'
    def files(environment):
        rows = environment['files']
        assert rows and all(set(r) == {'path', 'sha256'} for r in rows)
        assert all(re.fullmatch('[0-9A-F]{64}', r['sha256']) for r in rows)
        result = {r['path']: r['sha256'] for r in rows}
        assert len(result) == len(rows), 'Duplicate dependency identity.'
        return result
    old, new = files(previous), files(declared)
    assert old.keys() == new.keys(), 'Dependency file set or paths changed.'
    changed = [dict(path=p, previous=old[p], current=new[p]) for p in sorted(old) if old[p] != new[p]]
    paths = [PureWindowsPath(r['path']) for r in changed]
    assert len(paths) == 3 and {p.name for p in paths} == {'python.exe', 'python3.dll', 'python312.dll'}, 'Unexpected dependency change.'
    assert len({p.parent for p in paths}) == 1, 'Runtime binaries are not colocated.'
    assert review['operatorReportedCompletion'] and review['passedFocusedTests'] == 43
    assert review['newConditionsExecuted'] == 0 and not review['referenceSolved'] and not review['modelExecutedDuringReview']
    assert review['environmentFilesBefore'] == review['environmentFilesNow'] == len(old)
    assert review['metadataChanges'] == {} and review['changedFiles'] == changed
    return dict(kind='explicit-python-binary-transition-v1', changedFiles=changed,
                unchangedDependencyFiles=len(old)-3, versionsUnchanged=True,
                numericalCompatibilityEstablished=False)


def controls():
    """Called only by the independent visible preflight, never during preparation."""
    names = ['python.exe', 'python3.dll', 'python312.dll', 'numpy.pyd']
    previous = dict(python='fixture', numpy='fixture', scipy='fixture',
                    files=[dict(path='C:/fixture/runtime/'+n, sha256='A'*64) for n in names])
    declared = deepcopy(previous)
    for row in declared['files'][:3]: row['sha256'] = 'B'*64
    old = {r['path']: r['sha256'] for r in previous['files']}
    changes = [dict(path=r['path'], previous=old[r['path']], current=r['sha256'])
               for r in sorted(declared['files'], key=lambda r:r['path']) if old[r['path']] != r['sha256']]
    review = dict(operatorReportedCompletion=True, passedFocusedTests=43, newConditionsExecuted=0,
                  referenceSolved=False, modelExecutedDuringReview=False, environmentFilesBefore=4,
                  environmentFilesNow=4, metadataChanges={}, changedFiles=changes)
    validate_transition(previous, declared, review)
    passed = ['declared-three-binary-transition']
    mutations = [
        ('changed-numerical-library', lambda d,r:d['files'][-1].update(sha256='B'*64)),
        ('removed-dependency', lambda d,r:d['files'].pop()),
        ('added-dependency', lambda d,r:d['files'].append(dict(path='C:/fixture/new.dll',sha256='A'*64))),
        ('changed-version', lambda d,r:d.update(python='different')),
        ('undeclared-runtime-hash', lambda d,r:d['files'][0].update(sha256='C'*64)),
        ('duplicate-file', lambda d,r:d['files'].append(deepcopy(d['files'][0]))),
        ('unreported-failure', lambda d,r:r.update(operatorReportedCompletion=False)),
        ('relocated-dependency', lambda d,r:d['files'][-1].update(path='D:/other/numpy.pyd')),
    ]
    for name, mutate in mutations:
        candidate, evidence = deepcopy(declared), deepcopy(review)
        mutate(candidate, evidence)
        try:
            validate_transition(previous, candidate, evidence)
        except AssertionError:
            passed.append(name)
        else:
            raise AssertionError('Invalid transition accepted: '+name)
    assert len(passed) == 9
    return passed

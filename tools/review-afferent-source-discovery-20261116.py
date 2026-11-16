"""Review saved public source metadata/text only; never execute downloaded code."""
from datetime import datetime, timezone
from hashlib import sha1, sha256
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CAPTURE = ROOT / 'artifacts/afferent-source-discovery-20261116-29b5c8'


def read(name): return json.loads((CAPTURE / name).read_text(encoding='utf-8-sig'))


def anchor(relative, fragment):
    path = CAPTURE / relative
    lines = path.read_text(encoding='utf-8-sig').splitlines()
    found = [index + 1 for index, line in enumerate(lines) if fragment in line]
    assert len(found) == 1, (relative, fragment, found)
    return dict(path=relative, line=found[0], fragment=fragment, sha256=sha256(path.read_bytes()).hexdigest())


def main():
    requests = read('requests.json')['requests']
    for record in requests + read('followup.json'):
        if 'sha256' not in record: continue
        raw = (CAPTURE / (record['name'] + '.response')).read_bytes()
        assert len(raw) == record['bytes'] and sha256(raw).hexdigest() == record['sha256']
    names, ids = read('modeldb-names.response'), read('modeldb-ids.response')
    assert len(names) == len(ids) == 1931 and len(set(ids)) == len(ids)
    matches = [dict(id=ids[i], name=name) for i, name in enumerate(names) if 'spindle' in name.lower()]
    assert len(matches) == 4
    # Positional lookup locates candidates only; fetched model objects confirm
    # identities, rather than relying solely on simultaneous catalogue ordering.
    for name in ('modeldb-moraud', 'modeldb-kim', 'modeldb-ltmr'):
        model = read(name + '.response'); assert names[ids.index(model['id'])] == model['name']
    trees = read('author-tree-review.json'); assert len(trees) == 4
    author_records = []
    for index, record in enumerate(trees):
        commit_path = CAPTURE / f'author-{index}-commit.response'
        tree_path = CAPTURE / f'author-{index}-tree.response'
        assert sha256(commit_path.read_bytes()).hexdigest() == record['commitResponseSha256']
        assert sha256(tree_path.read_bytes()).hexdigest() == record['treeResponseSha256']
        commit = read(commit_path.name); tree = read(tree_path.name)
        assert commit['sha'] == record['commit'] and tree['sha'] == commit['commit']['tree']['sha']
        assert not tree['truncated'] and len(tree['tree']) == record['files']
        source_files = [e['path'] for e in tree['tree'] if e['type'] == 'blob']
        native = [p for p in source_files if p.lower().endswith(('.hoc', '.mod', '.slx', '.mdl'))]
        author_records.append(dict(repository=record['repository'], commit=record['commit'],
            completeTreeEntries=len(tree['tree']), nativeNeuronOrSimulinkFiles=native))
    assert all(not r['nativeNeuronOrSimulinkFiles'] for r in author_records)
    kim = read('kim-source-manifest.json'); commit = read('kim-commit.response'); tree = read('kim-tree.response')
    assert kim['commit'] == commit['sha'] == '9cf940b9ce2885cd5b2207a72c1252483f31cf19'
    assert tree['sha'] == commit['commit']['tree']['sha'] and not tree['truncated']
    entries = {e['path']: e for e in tree['tree'] if e['type'] == 'blob'}
    assert len(kim['files']) == 6
    for entry in kim['files']:
        data = (CAPTURE / 'kim-source' / entry['path']).read_bytes()
        assert len(data) == entry['bytes'] == entries[entry['path']]['size']
        assert sha256(data).hexdigest() == entry['sha256']
        assert sha1(b'blob ' + str(len(data)).encode() + b'\0' + data).hexdigest() == entry['gitBlobSha1'] == entries[entry['path']]['sha']
    findings = [
        anchor('kim-source/fig2_4_6/motor_unit.hoc', 'xopen("group_Ia.hoc")'),
        anchor('kim-source/fig2_4_6/syn_Ia.mod', 'i = gmax*(v - e)'),
        anchor('kim-source/fig9/syn_Ia_sinewave.mod', 'g = gmax * m(t)'),
        anchor('kim-source/fig9/syn_Ia_sinewave.mod', 'm = (sin(2*pie/tp*(t-tp/4-del)) + 1)/2'),
    ]
    report = dict(reviewedUtc=datetime.now(timezone.utc).isoformat(), capture=str(CAPTURE),
        publicCatalogueModels=len(names), catalogueSpindleMatches=matches, authorRepositories=author_records,
        kimRevision=kim['commit'], verifiedKimTextFiles=len(kim['files']), sourceFindings=findings,
        dryadPublicDownload=next(r for r in requests if r['name'] == 'dryad-public-tail'),
        housleyIaMembraneLocated=False, absenceProven=False,
        kimIaInputIsPrescribedConductance=True, kimIaSourceNotAdopted=True,
        downloadedCodeExecuted=False, testsExecuted=False, liveBodyChanged=False,
        pendingStudiesInspected=False,
        nextSourceRequirement='Pinned Ia morphology, mechanisms, temperature/initialization and exact transduction input with units; no generic membrane substitution.')
    with (CAPTURE / 'source-review.json').open('x', encoding='utf-8') as stream:
        json.dump(report, stream, indent=2, allow_nan=False); stream.write('\n')
    print(json.dumps(dict(catalogueModels=len(names), authorTrees=len(trees), verifiedKimFiles=6,
        IaMembraneLocated=False, newModelExecution=False)))


if __name__ == '__main__': main()

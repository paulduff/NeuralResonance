"""Reject corrupt causal controls, rather than merely rerunning their implementation."""
import contextlib
from hashlib import sha256
import importlib.util
import io
import json
from pathlib import Path
import shutil
import sys
import tempfile

sys.dont_write_bytecode=True
spec=importlib.util.spec_from_file_location('analysis',Path(__file__).resolve().parents[1]/'analyse-boundary-timing.py')
a=importlib.util.module_from_spec(spec);spec.loader.exec_module(a)
source=Path(sys.argv[1]);read=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
assert read(source/'protocol.json')['smoke']
hashes=lambda:{str(p):sha256(p.read_bytes()).hexdigest() for p in source.rglob('*') if p.is_file()}
before=hashes()
with contextlib.redirect_stdout(io.StringIO()):a.analyse(source,write_report=False)
assert hashes()==before
print('Valid capture accepted; original evidence unchanged.',flush=True)
for change in ('marker-time','missing-marker','fake-release','missing-state-row','wrong-owner','baseline-hash','energy','missing-case','marker-schedule'):
    with tempfile.TemporaryDirectory(prefix='nre-boundary-validator-') as tmp:
        root=Path(tmp)/'capture';shutil.copytree(source,root);shutil.copy2(source.parent/'baseline.json',root.parent/'baseline.json')
        cases=read(root/'results.json')['cases'];marker=next(x for x in cases if x['boundaryCondition']=='markers');connected=next(x for x in cases if x['boundaryCondition']=='connected')
        directory=root/marker['key']
        if change in ('marker-time','missing-marker','fake-release','wrong-owner'):
            path=directory/'events.jsonl';events=[json.loads(line) for line in path.read_text().splitlines()]
            if change in ('marker-time','missing-marker'):
                target=next(x for x in events if x['kind']=='boundary')
                if change=='marker-time':target['value']['processedMs']+=.001
                else:events.remove(target)
            elif change=='fake-release':
                events.append(next(json.loads(line) for line in (root/connected['key']/'events.jsonl').read_text().splitlines() if json.loads(line)['kind']=='learning' and json.loads(line)['value']['source']=='InferiorOlive'))
            else:next(x for x in events if x['kind']=='learning')['value']['target']=9
            path.write_text('\n'.join(json.dumps(x) for x in events)+'\n')
        elif change in ('missing-state-row','energy'):
            path=directory/'cell-states.csv';lines=path.read_text().splitlines()
            if change=='missing-state-row':lines.pop(2)
            else:
                columns=lines[0].split(',');row=lines[1].split(',');row[columns.index('reserve')]='2';lines[1]=','.join(row)
            path.write_text('\n'.join(lines)+'\n')
        else:
            path=root.parent/'baseline.json' if change=='baseline-hash' else root/'results.json' if change=='missing-case' else directory/'design.json'
            data=read(path)
            if change=='baseline-hash':data['cases'][0]['files']['trace.csv']['8000']['sha256']='invalid'
            elif change=='missing-case':data['cases'].pop()
            else:data['noOpBoundaries'][0]+=.125
            path.write_text(json.dumps(data))
        rejected=False
        try:
            with contextlib.redirect_stdout(io.StringIO()):a.analyse(root,write_report=False)
        except AssertionError:rejected=True
        assert rejected,change
        print(change+': correctly rejected',flush=True)

"""Freeze archived replay baselines; never modify the completed experiment."""
from hashlib import sha256
import json
from pathlib import Path
import shutil
import sys

read=lambda p:json.loads(p.read_text(encoding="utf-8-sig"))
def digest(p):
    h=sha256()
    with p.open('rb') as f:
        for b in iter(lambda:f.read(8*1024*1024),b''):h.update(b)
    return h.hexdigest().upper()

def prefix_hash(path,duration):
    h=sha256();count=0
    with path.open('rb') as f:
        h.update(next(f))
        for line in f:
            if float(line.split(b',',1)[0])>duration:break
            h.update(line);count+=1
    return dict(sha256=h.hexdigest().upper(),rows=count)

def main(run,output):
    run=run.resolve();output=output.resolve()
    if output.exists():raise ValueError('Use a fresh baseline file')
    status=read(run/'window-status.json');assert status['Status']=='FINISHED' and status['OldBackupUnchanged']
    manifest=read(run/'backup-manifest.json');expected={x['Path'].replace('\\','/'):x['Sha256'] for x in manifest['Files']}
    assert digest(run/'backup-manifest.json')==digest(Path(manifest['Destination'])/'backup-manifest.json')
    checked={}
    def verify(path):
        relative=path.relative_to(run).as_posix();value=digest(path);assert value==expected[relative].upper()
        checked[relative]=value
    verify(run/'inputs.json');verify(run/'study/results.json')
    assert read(run/'study/cell-analysis.json')['accountingValidated']
    cases=[]
    for c in read(run/'study/results.json')['cases']:
        if c['kind']!='replay' or c['variant'] not in ('calcium','disconnected'):continue
        directory=run/'study'/c['stage']/c['key'];files={}
        for name in ('trace.csv','contacts.csv'):
            path=directory/name;verify(path)
            files[name]={str(duration):prefix_hash(path,duration) for duration in (8000,60000)}
        cases.append(dict(sourceId=c['sourceId'],step=c['step'],condition='connected' if c['variant']=='calcium' else 'disconnected',spikes=c['spikes'],files=files))
    assert len(cases)==16
    output.parent.mkdir(parents=True,exist_ok=True)
    output.write_text(json.dumps(dict(source=str(run),revision=status['Revision'],checkedFiles=checked,cases=cases),indent=2)+'\n',encoding='utf-8')
    bundle=output.with_name('inputs.json')
    if bundle.exists():raise ValueError('Use a fresh bundle location')
    shutil.copy2(run/'inputs.json',bundle)
    print(f'Prepared 16 archived baselines; verified {len(checked)} source files and matching manifest copies.')

if __name__=='__main__':main(Path(sys.argv[1]),Path(sys.argv[2]))

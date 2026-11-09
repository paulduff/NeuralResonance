"""Freeze actual-cell cortical precision checks with verified legacy evidence."""
from datetime import datetime, timezone
from hashlib import sha256
import json
from pathlib import Path
import shutil
import subprocess
import sys
import uuid
from zipfile import ZipFile

ROOT=Path(__file__).resolve().parents[1]
read=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
git=lambda *a:subprocess.check_output(['git',*a],cwd=ROOT,encoding='utf-8').strip()

def digest(path):
    h=sha256()
    with path.open('rb') as stream:
        for block in iter(lambda:stream.read(4*1024*1024),b''):h.update(block)
    return h.hexdigest().upper()

def verified(capture,names):
    manifest=read(capture/'backup-manifest.json');backup=Path(manifest['Destination'])
    assert Path(manifest['Source']).resolve()==capture.resolve()
    assert digest(capture/'backup-manifest.json')==digest(backup/'backup-manifest.json')
    entries={e['Path'].replace('\\','/'):e for e in manifest['Files']}
    for name in names:
        for folder in (capture,backup):assert digest(folder/name)==entries[name]['Sha256'],name

def main():
    assert not git('status','--porcelain'),'Commit before freezing.'
    revision=git('rev-parse','HEAD');assert revision==git('rev-parse','origin/main'),'Push before freezing.'
    previous=ROOT/'artifacts/cortical-memory-20261106-2be2a4'
    numerical=ROOT/'artifacts/cortical-contact-numerics-20261106-f9f915'
    for capture in (previous,numerical):
        verified(capture,['window-status.json'])
        status=read(capture/'window-status.json')
        assert status['Status']=='DIAGNOSTICS_COMPLETE' and status['ExitCode']==0
    verified(numerical,['numerical-study/analysis.json','source/Structures/_SharedRuntime/CorticalContactNumerics.cs'])
    assert read(numerical/'numerical-study/analysis.json')['isolatedNumericalCriteriaPassed']
    # Only the documentation header may differ from the completed numerical candidate.
    core=lambda p:p.read_text(encoding='utf-8-sig').split('internal sealed record CorticalContactState',1)[1]
    assert core(ROOT/'Structures/_SharedRuntime/CorticalContactNumerics.cs')==core(numerical/'source/Structures/_SharedRuntime/CorticalContactNumerics.cs')
    verified(previous,['study/protocol.json','study/results.json'])
    protocol=read(previous/'study/protocol.json');assert len(protocol['scenarios'])==7 and read(previous/'study/results.json')['completed']
    names=['study/protocol.json','study/results.json']
    names += ['study/'+s['key']+'/'+n for s in protocol['scenarios'] for n in ('ticks.jsonl','inputs.jsonl','learning.jsonl','inbound.jsonl','declared-targets.json')]
    verified(previous,names)
    stamp=datetime.now(timezone.utc).strftime('%Y%m%d')
    package=ROOT/'artifacts'/('cortical-precision-'+stamp+'-'+uuid.uuid4().hex[:6])
    backup=Path('D:/DNNE-desktop-runs/UnityWorld')/package.name
    assert not package.exists() and not backup.exists();package.mkdir()
    subprocess.run(['git','archive','--format=zip','--output='+str(package/'source.zip'),revision],cwd=ROOT,check=True)
    with ZipFile(package/'source.zip') as archive:
        assert all(not Path(n).is_absolute() and '..' not in Path(n).parts for n in archive.namelist())
        archive.extractall(package/'source')
    copied=[]
    for name in names:
        target=package/'legacy-baseline'/Path(name).relative_to('study')
        target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(previous/name,target)
        assert digest(target)==digest(previous/name)
        copied.append(dict(sourcePath=name,targetPath=target.relative_to(package).as_posix(),sha256=digest(target)))
    (package/'provenance.json').write_text(json.dumps(dict(legacyCapture=str(previous),
        legacyManifestSha256=digest(previous/'backup-manifest.json'),numericalCapture=str(numerical),
        numericalManifestSha256=digest(numerical/'backup-manifest.json'),copiedFiles=copied),indent=2)+'\n',encoding='utf-8')
    shutil.copy2(package/'source/tools/backup-unity-run.ps1',package/'backup-run.ps1')
    classes=dict(CorticalPrecisionIntegrationTests=13,DeclaredSynapticTargetTests=8,BiologicalContactLearningTests=27,
        InboundPlasticityApplicationTests=9,StructureEngineDeterminismTests=10,SolutionManifestTests=1)
    files=[dict(Path=p.relative_to(package).as_posix(),Sha256=digest(p)) for p in sorted(package.rglob('*')) if p.is_file()]
    dotnet=shutil.which('dotnet');assert dotnet
    config=dict(Revision=revision,PreparedUtc=datetime.now(timezone.utc).isoformat(),PackageFiles=files,
        DotnetExecutable=dotnet,PythonExecutable=sys.executable,BackupDestination=str(backup),NoLiveMonitoring=True,
        ExpectedTestCases=sum(classes.values()),ExpectedCases=7,TestClassCounts=classes,
        TestFilter='|'.join('FullyQualifiedName~'+name for name in classes),
        LiveBrainAccessed=False,OldSavesLoaded=False,LegacyControls=7)
    (package/'configuration.json').write_text(json.dumps(config,indent=2)+'\n',encoding='utf-8')
    pointer=dict(Package=str(package),Backup=str(backup),Revision=revision,PreparedOnly=True,NoLiveMonitoring=True,
        Launcher=str(package/'source/tools/start-cortical-precision-window.ps1'))
    (ROOT/'artifacts'/('cortical-precision-prepared-'+stamp+'.json')).write_text(json.dumps(pointer,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(pointer,indent=2))

if __name__=='__main__':main()

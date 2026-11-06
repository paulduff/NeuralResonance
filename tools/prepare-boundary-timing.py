"""Freeze the boundary-only study; full old-file checks run in its PowerShell window."""
from datetime import datetime,timezone
from hashlib import sha256
import json
from pathlib import Path
import shutil
import subprocess
import sys
import uuid
import xml.etree.ElementTree as ET

repo=Path(__file__).resolve().parents[1]
read=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
def digest(p):
    h=sha256()
    with p.open('rb') as f:
        for b in iter(lambda:f.read(8*1024*1024),b''):h.update(b)
    return h.hexdigest().upper()
def write(p,x):p.write_text(json.dumps(x,indent=2,allow_nan=False)+'\n',encoding='utf-8')
def copy(a,b):b.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(a,b)
def git(*args):return subprocess.check_output(['git',*args],cwd=repo,text=True,encoding='utf-8').strip()

def main():
    assert not git('status','--porcelain')
    revision=git('rev-parse','HEAD');assert revision==git('rev-parse','origin/main')
    prep=repo/'artifacts/boundary-timing-preparation-20261106';build=repo/'artifacts/boundary-timing-build-20261106/bin/PosturalCellProbe/release'
    prior=repo/'artifacts/split-numerics-20261102-1e49f9'
    report=read(prep/'smoke/cell-analysis.json');assert report['accountingValidated'] and report['caseCount']==24 and report['baselineMatches']==16 and report['boundaryHypothesisSupported']
    assert (prep/'analysis-tests.log').read_text(encoding='utf-8-sig').count('correctly rejected')==9
    provenance=read(prep/'build-provenance.json');assert provenance['revision']==revision
    for x in provenance['binaries']:assert digest(build/x['path'])==x['sha256']
    for path,value in read(prep/'baseline.json')['checkedFiles'].items():assert digest(prior/path)==value
    assert digest(prior/'inputs.json')==digest(prep/'inputs.json')
    ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
    trx=ET.parse(prep/'dnne-tests.trx');results=trx.findall('.//t:UnitTestResult',ns)
    failures=[x.attrib['testName'] for x in results if x.attrib['outcome']!='Passed']
    expected='NeuralResonanceEngine.DNNE.Tests.NeuronalMemoryTests.RepeatedCorticalExperienceGraduallyRaisesConsolidation'
    assert len(results)==1051 and failures==[expected]
    original=ET.parse(prep/'baseline-memory-check.trx').findall('.//t:UnitTestResult',ns)
    assert len(original)==1 and original[0].attrib['testName']==expected and original[0].attrib['outcome']=='Failed'
    protected={x['Path'].lower():x for x in read(prior/'configuration.json')['ProtectedBackupFiles']}
    for capture in (prior,repo/'artifacts/split-numerics-review-20261106'):
        m=read(capture/'backup-manifest.json');destination=Path(m['Destination'])
        assert digest(capture/'backup-manifest.json')==digest(destination/'backup-manifest.json')
        for base in (capture,destination):
            for row in m['Files']:
                path=base/row['Path'];protected[str(path).lower()]=dict(Path=str(path),Sha256=row['Sha256'])
            path=base/'backup-manifest.json';protected[str(path).lower()]=dict(Path=str(path),Sha256=digest(path))
    # Manifest expectations are carried forward, not relabelled as a new full
    # audit. The independent launcher verifies every old file before any case.
    print(f'Carried forward {len(protected)} protected paths; full verification runs in PowerShell.',flush=True)
    unchanged=[];changed=[]
    for path in git('ls-files','Structures/_SharedRuntime','Protocol','Shared.Contracts').splitlines():
        a,b=digest(repo/path),digest(prior/'source'/path)
        (unchanged if a==b else changed).append(dict(path=path,sha256=a,priorSha256=b))
    assert [x['path'] for x in changed]==['Structures/_SharedRuntime/StructureEngine.cs']
    run=repo/'artifacts'/('boundary-timing-20261106-'+uuid.uuid4().hex[:6]);backup=Path('D:/DNNE-desktop-runs/UnityWorld')/run.name
    assert not run.exists() and not backup.exists();run.mkdir()
    paths=git('ls-files','Structures/_SharedRuntime','Protocol','Shared.Contracts','Structures/NeuralResonanceEngine.Structures.Runtime.csproj',
        'tools/PosturalCellProbe','Directory.Build.props','Directory.Build.targets','Directory.Packages.props','global.json','NuGet.Config','AGENTS.md').splitlines()
    paths+=['tools/start-postural-cell-window.ps1','tools/_start-dnne-project.ps1','tools/backup-unity-run.ps1',
        'tools/prepare-boundary-timing-inputs.py','tools/prepare-boundary-timing.py','tools/analyse-boundary-timing.py','tools/tests/boundary-timing-analysis.Tests.py',
        'tests/NeuralResonanceEngine.DNNE.Tests/ConnectedTimingTests.cs','docs/DNNE_Boundary_Timing_Study_2026-11-06.md','docs/DNNE_Split_Numerical_Results_2026-11-06.md']
    for path in sorted(set(paths)):copy(repo/path,run/'source'/path)
    for source,target in ((prep,run/'preparation'),(build,run/'bin')):
        for p in source.rglob('*'):
            if p.is_file():copy(p,target/p.relative_to(source))
    for source,target in ((repo/'tools/analyse-boundary-timing.py','analysis.py'),(repo/'tools/backup-unity-run.ps1','backup-run.ps1'),(prep/'inputs.json','inputs.json'),(prep/'baseline.json','baseline.json')):copy(source,run/target)
    write(run/'runtime-scope.json',dict(comparedTo=str(prior),unchanged=unchanged,changed=changed,scope='Isolated-only no-op timing hook; default mathematics and production fingerprints unchanged.'))
    write(run/'validation.json',dict(preflight=report,unitTests=dict(total=1051,passed=1050,failed=failures,prechangeBinaryAlsoFails=True),corruptionRejections=9))
    (run/'source-revision.txt').write_text(revision+'\n',encoding='utf-8')
    (run/'runtime-diff.patch').write_text(git('diff',read(prior/'configuration.json')['Revision'],revision,'--','Structures/_SharedRuntime'),encoding='utf-8')
    python=Path(sys.executable).resolve();dotnet=Path(shutil.which('dotnet'))
    (run/'python-runtime.txt').write_text(subprocess.check_output([str(python),'--version'],text=True),encoding='utf-8')
    (run/'dotnet-runtime.txt').write_text(subprocess.check_output([str(dotnet),'--info'],text=True,encoding='utf-8'),encoding='utf-8')
    files=[dict(Path=str(p),Sha256=digest(p)) for p in sorted(run.rglob('*')) if p.is_file()]
    config=dict(Revision=revision,PreparedUtc=datetime.now(timezone.utc).isoformat(),Repository=str(repo),ProbeAssembly=str(run/'bin/NeuralResonanceEngine.DNNE.Tests.dll'),
        ProbeArguments=['--boundary-timing',str(run/'inputs.json')],DotnetExecutable=str(dotnet),PythonExecutable=str(python),CaseCount=24,ExecutionHours=24,
        BackupDestination=str(backup),PackageFiles=files,ProtectedBackupFiles=list(protected.values()),NoLiveMonitoring=True,FreshCellsOnly=True,
        PreservationVerifiedDuringPreparation=False,KnownBaselineTestFailure=expected,WindowTitle='DNNE isolated timing-boundary test',
        StudyDescription='24 conditions: connected, disconnected, and disconnected with timing markers that release no transmitter. Fixed archived inputs, fresh cells, exact baseline checks. One cortical-consolidation unit test also fails in the pre-change binary; it remains separately unresolved. FINISHED means capture/accounting completed, not physiological qualification.')
    write(run/'configuration.json',config)
    pointer=dict(Package=str(run),Backup=str(backup),Revision=revision,PackageFiles=len(files),ProtectedFiles=len(protected),NoLiveMonitoring=True)
    write(repo/'artifacts/boundary-timing-launch-20261106.json',pointer);print(json.dumps(pointer,indent=2))

if __name__=='__main__':main()

"""Freeze the independent reference/replay rig and its isolated Python environment."""
from datetime import datetime,timezone
from hashlib import sha256
import json
from pathlib import Path
import shutil
import subprocess
import sys
import uuid

repo=Path(__file__).resolve().parents[1]
read=lambda p:json.loads(p.read_text(encoding="utf-8-sig"))
def write(p,v):p.write_text(json.dumps(v,indent=2,allow_nan=False)+"\n",encoding="utf-8")
def digest(p):
    h=sha256()
    with p.open("rb") as f:
        for chunk in iter(lambda:f.read(8*1024*1024),b""):h.update(chunk)
    return h.hexdigest().upper()
def copy(a,b):b.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(a,b)
def git(*args):return subprocess.check_output(["git",*args],cwd=repo,text=True,encoding="utf-8").strip()

def main():
    assert not git("status","--porcelain")
    revision=git("rev-parse","HEAD");assert revision==git("rev-parse","origin/main")
    prep=repo/"artifacts/split-numerics-preparation-20261102";build=repo/"artifacts/split-numerics-build-20261102/bin/PosturalCellProbe/release"
    python_env=repo/"artifacts/numerical-reference-env-20261102"
    report=read(prep/"smoke-final/cell-analysis.json");assert report["accountingValidated"] and report["caseCount"]==80 and report["baselineMatches"]==4
    assert read(prep/"self-test/self-tests.json")["defaultStepperMatchesProduction"]
    ref=read(prep/"reference-tests.json");assert ref["completed"] and ref["derivativeVectors"]==4 and ref["analyticReceptorChecks"] and ref["compiledMatchesPython"]
    tests=(prep/"analysis-tests.log").read_text(encoding="utf-8-sig");assert tests.count("correctly rejected")==15 and "read-only audit" in tests
    provenance=read(prep/"build-provenance.json");assert provenance["revision"]==revision
    for x in provenance["binaries"]:assert digest(build/x["path"])==x["sha256"]
    for x in read(prep/"inputs-v2.json")["provenance"]:assert digest(Path(x["path"]))==x["sha256"].upper()
    prior=repo/"artifacts/connected-timing-20261102-26272f"
    protected={x["Path"].lower():x for x in read(prior/"configuration.json")["ProtectedBackupFiles"]}
    for capture in (prior,repo/"artifacts/connected-timing-review-20261102"):
        m=read(capture/"backup-manifest.json")
        for base in (capture,Path(m["Destination"])):
            for row in m["Files"]:
                p=base/row["Path"];protected[str(p).lower()]=dict(Path=str(p),Sha256=row["Sha256"])
            p=base/"backup-manifest.json";protected[str(p).lower()]=dict(Path=str(p),Sha256=digest(p))
    for x in protected.values():assert digest(Path(x["Path"]))==x["Sha256"].upper(),x["Path"]
    print(f"Preservation verified: {len(protected)} older files.",flush=True)
    unchanged=[]
    for path in git("ls-files","Structures/_SharedRuntime","Protocol","Shared.Contracts").splitlines():
        assert digest(repo/path)==digest(prior/"source"/path),path
        unchanged.append(dict(path=path,sha256=digest(repo/path)))
    run=repo/"artifacts"/("split-numerics-20261102-"+uuid.uuid4().hex[:6]);backup=Path("D:/DNNE-desktop-runs/UnityWorld")/run.name
    assert not run.exists() and not backup.exists();run.mkdir()
    paths=git("ls-files","Protocol","Shared.Contracts","Structures/_SharedRuntime","Structures/NeuralResonanceEngine.Structures.Runtime.csproj",
        "tools/PosturalCellProbe","Directory.Build.props","Directory.Build.targets","Directory.Packages.props","global.json","NuGet.Config","AGENTS.md").splitlines()
    paths += ["tools/start-postural-cell-window.ps1","tools/_start-dnne-project.ps1","tools/backup-unity-run.ps1","tools/prepare-split-numerics.py",
        "tools/prepare-split-numerics-inputs.py","tools/run-split-numerics.py","tools/olive_numerical_reference.py","tools/analyse-split-numerics.py",
        "tools/tests/split-numerics-analysis.Tests.py","docs/DNNE_Split_Numerical_Qualification_2026-11-02.md","docs/DNNE_Connected_Timing_Results_2026-11-02.md"]
    for path in sorted(set(paths)):copy(repo/path,run/"source"/path)
    for source,target in ((build,run/"bin"),(prep,run/"preparation"),(python_env,run/"python")):
        for p in source.rglob("*"):
            if p.is_file() and "__pycache__" not in p.parts and p.suffix not in (".pyc",".nbi",".nbc"):copy(p,target/p.relative_to(source))
    for source,target in (("tools/run-split-numerics.py","driver.py"),("tools/olive_numerical_reference.py","olive_numerical_reference.py"),
        ("tools/analyse-split-numerics.py","analysis.py"),("tools/backup-unity-run.ps1","backup-run.ps1")):
        copy(repo/source,run/target)
    copy(prep/"inputs-v2.json",run/"inputs.json")
    write(run/"unchanged-runtime.json",dict(comparedTo=str(prior),files=unchanged))
    (run/"source-revision.txt").write_text(revision+"\n",encoding="utf-8")
    python=run/"python/Scripts/python.exe";dotnet=Path(shutil.which("dotnet"))
    # Imports from the copied environment, not the preparatory environment.
    check="import sys,json,numpy,scipy,numba;print(json.dumps(dict(version=sys.version,prefix=sys.prefix,numpy=numpy.__version__,scipy=scipy.__version__,numba=numba.__version__)))"
    environment=json.loads(subprocess.check_output([str(python),"-B","-c",check],text=True))
    assert Path(environment["prefix"]).resolve()==(run/"python").resolve()
    assert (environment["numpy"],environment["scipy"],environment["numba"])==("2.2.6","1.16.3","0.61.2")
    write(run/"python-runtime.json",environment)
    (run/"dotnet-runtime.txt").write_text(subprocess.check_output([str(dotnet),"--info"],text=True,encoding="utf-8"),encoding="utf-8")
    files=[dict(Path=str(p),Sha256=digest(p)) for p in sorted(run.rglob("*")) if p.is_file()]
    config=dict(Revision=revision,PreparedUtc=datetime.now(timezone.utc).isoformat(),Repository=str(repo),
        ProbeAssembly=str(run/"bin/NeuralResonanceEngine.DNNE.Tests.dll"),DriverScript=str(run/"driver.py"),
        DriverArguments=["--assembly",str(run/"bin/NeuralResonanceEngine.DNNE.Tests.dll"),"--bundle",str(run/"inputs.json"),"--dotnet",str(dotnet)],
        DotnetExecutable=str(dotnet),PythonExecutable=str(python),CaseCount=80,ExecutionHours=24,
        BackupDestination=str(backup),PackageFiles=files,ProtectedBackupFiles=list(protected.values()),NoLiveMonitoring=True,FreshCellsOnly=True,
        WindowTitle="DNNE source reference and fixed-spike replay",
        StudyDescription="80 isolated conditions: independent olive membrane/receptor reference without metabolic feedback, matched production controls with/without energy, and identical recorded olive inputs replayed to Purkinje. Same biological parameters; fresh diagnostic states only. FINISHED validates capture accounting, not physiological or solver qualification.")
    write(run/"configuration.json",config)
    pointer=dict(Package=str(run),Backup=str(backup),Revision=revision,PackageFiles=len(files),ProtectedFiles=len(protected),UnchangedRuntimeFiles=len(unchanged),NoLiveMonitoring=True)
    write(repo/"artifacts/split-numerics-launch-20261102.json",pointer);print(json.dumps(pointer,indent=2))

if __name__=="__main__":main()

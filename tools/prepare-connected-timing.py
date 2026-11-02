"""Freeze committed timing code and evidence; preserve every earlier protected file."""
from datetime import datetime, timezone
from hashlib import sha256
import json
from pathlib import Path
import shutil
import subprocess
import sys
import uuid

repo=Path(__file__).resolve().parents[1]
read=lambda p:json.loads(p.read_text(encoding="utf-8-sig"))
write=lambda p,v:p.write_text(json.dumps(v,indent=2,allow_nan=False)+"\n",encoding="utf-8")
def digest(p):
    h=sha256()
    with p.open("rb") as f:
        for block in iter(lambda:f.read(8*1024*1024),b""):h.update(block)
    return h.hexdigest().upper()
git=lambda *a:subprocess.check_output(["git",*a],cwd=repo,text=True,encoding="utf-8").strip()
def copy(a,b):b.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(a,b)

def main():
    assert not git("status","--porcelain"),"Freeze committed source only"
    revision=git("rev-parse","HEAD");assert revision==git("rev-parse","origin/main")
    preparation=repo/"artifacts/connected-timing-preparation-20261102"
    build=repo/"artifacts/connected-timing-build-20261102/bin/PosturalCellProbe/release"
    checked=read(preparation/"quick-release/cell-analysis.json")
    assert checked["accountingValidated"] and checked["hostPacketInvarianceValidated"] and checked["caseCount"]==60
    tests=(preparation/"analysis-tests.log").read_text(encoding="utf-8-sig")
    assert tests.count("correctly rejected")==12 and "read-only audit" in tests
    unit=(preparation/"full-tests.log").read_text(encoding="utf-8-sig")
    assert "Passed!" in unit and "1049" in unit
    provenance=read(preparation/"build-provenance.json");assert provenance["revision"]==revision
    for entry in provenance["binaries"]:assert digest(build/entry["path"])==entry["sha256"]
    for entry in provenance["testedSource"]:assert digest(repo/entry["path"])==entry["sha256"]
    prior=repo/"artifacts/olive-purkinje-20261102-923ebc"
    protected={r["Path"].lower():r for r in read(prior/"configuration.json")["ProtectedBackupFiles"]}
    for capture in [prior,repo/"artifacts/olive-purkinje-review-20261102"]:
        manifest=read(capture/"backup-manifest.json")
        for base in [capture,Path(manifest["Destination"])]:
            for row in manifest["Files"]:
                path=base/row["Path"];protected[str(path).lower()]=dict(Path=str(path),Sha256=row["Sha256"])
            path=base/"backup-manifest.json";protected[str(path).lower()]=dict(Path=str(path),Sha256=digest(path))
    for row in protected.values():assert digest(Path(row["Path"]))==row["Sha256"].upper(),row["Path"]
    # Numerical coupling changes; calibrated olive physiology and all learning
    # parameters remain the same. Chemistry source differs only in voltage input.
    unchanged=[]
    for name in ["OlivaryMembrane.cs","OlivarySynapses.cs","OlivaryIntegration.cs","NeuralEnergyBudget.cs","IntrinsicCircuitDriveTopology.cs","PurkinjeClimbingCompartment.cs"]:
        relative="Structures/_SharedRuntime/"+name
        assert digest(repo/relative)==digest(prior/"source"/relative),relative
        unchanged.append(dict(path=relative,sha256=digest(repo/relative)))
    run=repo/"artifacts"/("connected-timing-20261102-"+uuid.uuid4().hex[:6]);backup=Path("D:/DNNE-desktop-runs/UnityWorld")/run.name
    assert not run.exists() and not backup.exists();run.mkdir()
    paths=git("ls-files","Protocol","Shared.Contracts","Structures/_SharedRuntime","Structures/Cerebellum.PurkinjeCellLayer",
        "Structures/NeuralResonanceEngine.Structures.Runtime.csproj","tools/PosturalCellProbe","Directory.Build.props",
        "Directory.Build.targets","Directory.Packages.props","global.json","NuGet.Config","AGENTS.md").splitlines()
    paths += ["tools/start-postural-cell-window.ps1","tools/_start-dnne-project.ps1","tools/backup-unity-run.ps1",
        "tools/prepare-connected-timing.py","tools/analyse-olive-purkinje.py","tools/tests/olive-purkinje-analysis.Tests.py",
        "tests/NeuralResonanceEngine.DNNE.Tests/ConnectedTimingTests.cs","tests/NeuralResonanceEngine.DNNE.Tests/PurkinjeCalciumTests.cs",
        "docs/DNNE_Connected_Timing_Repair_2026-11-02.md","docs/DNNE_Purkinje_Calcium_Candidate_2026-11-02.md",
        "docs/DNNE_Purkinje_Calcium_Results_2026-11-02.md"]
    for name in sorted(set(paths)):copy(repo/name,run/"source"/name)
    for source,target in [(build,run/"bin"),(preparation,run/"preparation")]:
        for path in source.rglob("*"):
            if path.is_file():copy(path,target/path.relative_to(source))
    copy(repo/"tools/analyse-olive-purkinje.py",run/"analysis.py")
    copy(repo/"tools/backup-unity-run.ps1",run/"backup-run.ps1")
    write(run/"unchanged-physiology.json",dict(comparedTo=str(prior),files=unchanged))
    dotnet=Path(shutil.which("dotnet"));(run/"source-revision.txt").write_text(revision+"\n",encoding="utf-8")
    (run/"runtime-info.txt").write_text(subprocess.check_output([str(dotnet),"--info"],text=True,encoding="utf-8"),encoding="utf-8")
    files=[dict(Path=str(p),Sha256=digest(p)) for p in sorted(run.rglob("*")) if p.is_file()]
    config=dict(Revision=revision,PreparedUtc=datetime.now(timezone.utc).isoformat(),Repository=str(repo),
        ProbeAssembly=str(run/"bin/NeuralResonanceEngine.DNNE.Tests.dll"),ProbeArguments=["--connected-timing"],CaseCount=120,
        DotnetExecutable=str(dotnet),PythonExecutable=sys.executable,BackupDestination=str(backup),PackageFiles=files,
        ProtectedBackupFiles=list(protected.values()),NoLiveMonitoring=True,FreshCellsOnly=True,PhysicalRecoveryAssessed=False,
        WindowTitle="DNNE connected timing - arrival and solver refinement",
        StudyDescription="One olive and two Purkinje cells. 120 fresh conditions compare packet cadence independently of solver resolution. Fractional arrival boundaries, all real axonal events and unchanged learning constants. 60 biological seconds per condition; no live brain or world. FINISHED means validated accounting and packet invariance; solver convergence and physiology need separate review.")
    write(run/"configuration.json",config)
    pointer=dict(Package=str(run),Backup=str(backup),Revision=revision,PackageFiles=len(files),ProtectedFiles=len(protected),NoLiveMonitoring=True)
    write(repo/"artifacts/connected-timing-launch-20261102.json",pointer);print(json.dumps(pointer,indent=2))

if __name__=="__main__":main()

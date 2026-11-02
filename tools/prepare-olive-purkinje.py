"""Freeze a committed circuit/chemistry comparison without touching older saves."""
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
write=lambda p,v:p.write_text(json.dumps(v,indent=2,allow_nan=False)+"\n",encoding="utf-8")
digest=lambda p:sha256(p.read_bytes()).hexdigest().upper()
git=lambda *a:subprocess.check_output(["git",*a],cwd=repo,text=True,encoding="utf-8").strip()
def copy(a,b):b.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(a,b)

def main():
    assert not git("status","--porcelain"),"Freeze committed source only"
    revision=git("rev-parse","HEAD");assert revision==git("rev-parse","origin/main")
    preparation=repo/"artifacts/olive-purkinje-preparation-20261102"
    build=repo/"artifacts/olive-purkinje-build-20261102/bin/PosturalCellProbe/release"
    checked=read(preparation/"quick-release/cell-analysis.json")
    assert checked["accountingValidated"] and checked["caseCount"]==64
    tests=(preparation/"analysis-tests.log").read_text(encoding="utf-8-sig")
    assert tests.count("correctly rejected")==9 and "read-only audit" in tests
    unit=(preparation/"full-tests.log").read_text(encoding="utf-8-sig")
    assert "Passed!" in unit and "1044" in unit
    provenance=read(preparation/"build-provenance.json");assert provenance["revision"]==revision
    for entry in provenance["binaries"]:assert digest(build/entry["path"])==entry["sha256"]
    prior=repo/"artifacts/olive-response-20261102-f961e3"
    protected={r["Path"].lower():r for r in read(prior/"configuration.json")["ProtectedBackupFiles"]}
    for capture in [prior,repo/"artifacts/olive-response-review-20261102"]:
        manifest=read(capture/"backup-manifest.json")
        for base in [capture,Path(manifest["Destination"])]:
            for row in manifest["Files"]:
                path=base/row["Path"];protected[str(path).lower()]=dict(Path=str(path),Sha256=row["Sha256"])
            path=base/"backup-manifest.json";protected[str(path).lower()]=dict(Path=str(path),Sha256=digest(path))
    for row in protected.values():assert digest(Path(row["Path"]))==row["Sha256"].upper(),row["Path"]
    # Preserve calibrated olive equations, receptor kinetics, integration and supply.
    unchanged=[]
    for name in ["OlivaryMembrane.cs","OlivarySynapses.cs","OlivaryIntegration.cs","NeuralEnergyBudget.cs","IntrinsicCircuitDriveTopology.cs"]:
        relative="Structures/_SharedRuntime/"+name
        assert digest(repo/relative)==digest(prior/"source"/relative),relative
        unchanged.append(dict(path=relative,sha256=digest(repo/relative)))
    run=repo/"artifacts"/("olive-purkinje-20261102-"+uuid.uuid4().hex[:6]);backup=Path("D:/DNNE-desktop-runs/UnityWorld")/run.name
    assert not run.exists() and not backup.exists();run.mkdir()
    paths=git("ls-files","Protocol","Shared.Contracts","Structures/_SharedRuntime","Structures/Cerebellum.PurkinjeCellLayer",
        "Structures/NeuralResonanceEngine.Structures.Runtime.csproj","tools/PosturalCellProbe","Directory.Build.props",
        "Directory.Build.targets","Directory.Packages.props","global.json","NuGet.Config","AGENTS.md").splitlines()
    paths += ["tools/start-postural-cell-window.ps1","tools/_start-dnne-project.ps1","tools/backup-unity-run.ps1",
        "tools/prepare-olive-purkinje.py","tools/analyse-olive-purkinje.py","tools/tests/olive-purkinje-analysis.Tests.py",
        "tests/NeuralResonanceEngine.DNNE.Tests/PurkinjeCalciumTests.cs","tests/NeuralResonanceEngine.DNNE.Tests/BiologicalContactLearningTests.cs",
        "tests/NeuralResonanceEngine.DNNE.Tests/OlivaryCalibrationTests.cs","docs/DNNE_Purkinje_Calcium_Candidate_2026-11-02.md",
        "docs/DNNE_Olivary_Response_Results_2026-11-02.md","src/NRE.BlazorEditor/wwwroot/data/brain-atlas.json"]
    for name in sorted(set(paths)):copy(repo/name,run/"source"/name)
    for source,target in [(build,run/"bin"),(preparation,run/"preparation")]:
        for path in source.rglob("*"):
            if path.is_file():copy(path,target/path.relative_to(source))
    copy(repo/"tools/analyse-olive-purkinje.py",run/"analysis.py")
    copy(repo/"tools/backup-unity-run.ps1",run/"backup-run.ps1")
    write(run/"unchanged-olive.json",dict(comparedTo=str(prior),files=unchanged))
    dotnet=Path(shutil.which("dotnet"));(run/"source-revision.txt").write_text(revision+"\n",encoding="utf-8")
    (run/"runtime-info.txt").write_text(subprocess.check_output([str(dotnet),"--info"],text=True,encoding="utf-8"),encoding="utf-8")
    files=[dict(Path=str(p),Sha256=digest(p)) for p in sorted(run.rglob("*")) if p.is_file()]
    config=dict(Revision=revision,PreparedUtc=datetime.now(timezone.utc).isoformat(),Repository=str(repo),
        ProbeAssembly=str(run/"bin/NeuralResonanceEngine.DNNE.Tests.dll"),ProbeArguments=["--olive-purkinje"],CaseCount=128,
        DotnetExecutable=str(dotnet),PythonExecutable=sys.executable,BackupDestination=str(backup),PackageFiles=files,
        ProtectedBackupFiles=list(protected.values()),NoLiveMonitoring=True,FreshCellsOnly=True,PhysicalRecoveryAssessed=False,
        WindowTitle="DNNE olive to Purkinje - calcium learning comparison",
        StudyDescription="One olive, two Purkinje cells. 128 fresh conditions compare old learning, calcium learning and calcium entry/store-release blocks. Actual axonal bursts, contact ownership, timing, chemical and energy ledgers. Four biological minutes per condition; no live brain or world. FINISHED means validated accounting, not physiological qualification.")
    write(run/"configuration.json",config)
    pointer=dict(Package=str(run),Backup=str(backup),Revision=revision,PackageFiles=len(files),ProtectedFiles=len(protected),NoLiveMonitoring=True)
    write(repo/"artifacts/olive-purkinje-launch-20261102.json",pointer);print(json.dumps(pointer,indent=2))

if __name__=="__main__":main()

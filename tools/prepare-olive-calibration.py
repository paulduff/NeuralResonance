"""Freeze fresh olivary calibration; does not launch or monitor an experiment."""
from datetime import datetime, timezone
from hashlib import sha256
import json
from pathlib import Path
import shutil
import subprocess
import sys
import uuid
import xml.etree.ElementTree as ET

repo=Path(__file__).resolve().parents[1]
read=lambda p:json.loads(p.read_text(encoding="utf-8-sig"))
write=lambda p,v:p.write_text(json.dumps(v,indent=2,allow_nan=False)+"\n",encoding="utf-8")
digest=lambda p:sha256(p.read_bytes()).hexdigest().upper()
git=lambda *args:subprocess.check_output(["git",*args],cwd=repo,text=True,encoding="utf-8").strip()


def copy(source,target):
    target.parent.mkdir(parents=True,exist_ok=True)
    shutil.copy2(source,target)


def main():
    assert not git("status","--porcelain"),"Freeze committed source only"
    revision=git("rev-parse","HEAD")
    assert revision==git("rev-parse","origin/main"),"Push stable source before freezing"
    preparation=repo/"artifacts/olive-calibration-preparation-20261102"
    build=repo/"artifacts/olive-calibration-build-20261102/bin/PosturalCellProbe/release"
    assert read(preparation/"smoke-release/cell-analysis.json")["accountingValidated"]
    assert read(preparation/"smoke-release/protocol.json")["caseCount"]==180
    counters=ET.parse(preparation/"tests/release-regression.trx").find(".//{*}Counters").attrib
    assert counters["failed"]=="0" and counters["passed"]==counters["total"]=="1034"
    # The final build records which committed revision produced this package.
    provenance=read(preparation/"build-provenance.json")
    assert provenance["revision"]==revision
    for entry in provenance["binaries"]:assert digest(build/entry["path"])==entry["sha256"]
    prior=repo/"artifacts/energy-recovery-20261030-661900"
    protected={r["Path"].lower():r for r in read(prior/"configuration.json")["ProtectedBackupFiles"]}
    for capture in [prior,repo/"artifacts/energy-recovery-review-20261102"]:
        manifest=read(capture/"backup-manifest.json")
        for root in [capture,Path(manifest["Destination"])]:
            for row in manifest["Files"]:
                path=root/row["Path"];protected[str(path).lower()]=dict(Path=str(path),Sha256=row["Sha256"])
            path=root/"backup-manifest.json";protected[str(path).lower()]=dict(Path=str(path),Sha256=digest(path))
    for row in protected.values():assert digest(Path(row["Path"]))==row["Sha256"].upper(),row["Path"]
    run=repo/"artifacts"/("olive-calibration-20261102-"+uuid.uuid4().hex[:6])
    backup=Path("D:/DNNE-desktop-runs/UnityWorld")/run.name
    assert not run.exists() and not backup.exists()
    run.mkdir()
    paths=git("ls-files","Protocol","Shared.Contracts","Structures/_SharedRuntime","ControlProgram",
        "Structures/NeuralResonanceEngine.Structures.Runtime.csproj","tools/PosturalCellProbe",
        "tests/NeuralResonanceEngine.DNNE.Tests","Directory.Build.props","Directory.Build.targets",
        "Directory.Packages.props","global.json","NuGet.Config","AGENTS.md").splitlines()
    paths += ["tools/start-postural-cell-window.ps1","tools/_start-dnne-project.ps1","tools/backup-unity-run.ps1",
        "tools/prepare-olive-calibration.py","tools/analyse-olive-calibration.py",
        "tools/tests/olive-analysis.Tests.py",
        "docs/DNNE_Olivary_Calibration_2026-11-02.md","docs/DNNE_Energy_Recovery_Results_2026-11-02.md"]
    for path in sorted(set(paths)):copy(repo/path,run/"source"/path)
    for source,target in [(build,run/"bin"),(preparation,run/"preparation")]:
        for path in source.rglob("*"):
            if path.is_file():copy(path,target/path.relative_to(source))
    copy(repo/"tools/analyse-olive-calibration.py",run/"analysis.py")
    copy(repo/"tools/backup-unity-run.ps1",run/"backup-run.ps1")
    for name in ["protocol.json","results.json","cell-analysis.json"]:copy(prior/"study"/name,run/"previous-model"/name)
    dotnet=Path(shutil.which("dotnet"))
    (run/"runtime-info.txt").write_text(subprocess.check_output([str(dotnet),"--info"],text=True,encoding="utf-8"),encoding="utf-8")
    (run/"source-revision.txt").write_text(revision+"\n",encoding="utf-8")
    files=[dict(Path=str(p),Sha256=digest(p)) for p in sorted(run.rglob("*")) if p.is_file()]
    config=dict(Revision=revision,PreparedUtc=datetime.now(timezone.utc).isoformat(),Repository=str(repo),
        ProbeAssembly=str(run/"bin/NeuralResonanceEngine.DNNE.Tests.dll"),ProbeArguments=["--olive-calibration"],CaseCount=180,
        DotnetExecutable=str(dotnet),PythonExecutable=sys.executable,BackupDestination=str(backup),
        PackageFiles=files,ProtectedBackupFiles=list(protected.values()),NoLiveMonitoring=True,
        WindowTitle="DNNE inferior olive - 180 biological calibration conditions",
        StudyDescription="Three-compartment olive: 60 simulated seconds per fresh cell; receptor responses, inhibition, paired timing, fuel restore and channel controls. Three time steps. Execution is not physiological qualification.",
        FreshCellsOnly=True,PhysicalRecoveryAssessed=False)
    write(run/"configuration.json",config)
    pointer=dict(Package=str(run),Backup=str(backup),Revision=revision,PackageFiles=len(files),ProtectedFiles=len(protected),NoLiveMonitoring=True)
    write(repo/"artifacts/olive-calibration-launch-20261102.json",pointer)
    print(json.dumps(pointer,indent=2))


if __name__=="__main__":main()

"""Freeze the 2026-10-30 fresh-cell experiment; does not launch or monitor it."""
from datetime import datetime, timezone
from hashlib import sha256
import json
from pathlib import Path
import shutil
import subprocess
import sys
import uuid

repo = Path(__file__).resolve().parents[1]
read = lambda p: json.loads(p.read_text(encoding="utf-8-sig"))
write = lambda p,v: p.write_text(json.dumps(v,indent=2,allow_nan=False)+"\n",encoding="utf-8")
digest = lambda p: sha256(p.read_bytes()).hexdigest().upper()
git = lambda *args: subprocess.check_output(["git",*args],cwd=repo,text=True,encoding="utf-8").strip()


def copy(source, target):
    target.parent.mkdir(parents=True,exist_ok=True)
    shutil.copy2(source,target)


def main():
    assert not git("status","--porcelain"), "Freeze committed source only"
    revision = git("rev-parse","HEAD")
    assert revision == git("rev-parse","origin/main"), "Push stable source before freezing"
    build = repo / "artifacts/energy-recovery-build-20261030/bin/PosturalCellProbe/release"
    preparation = repo / "artifacts/energy-recovery-preparation-20261030"
    assert read(preparation / "smoke-packaged/cell-analysis.json")["accountingValidated"]
    assert read(preparation / "smoke-packaged/protocol.json")["caseCount"] == 120
    run = repo / "artifacts" / ("energy-recovery-20261030-"+uuid.uuid4().hex[:6])
    backup = Path("D:/DNNE-desktop-runs/UnityWorld") / run.name
    assert not run.exists() and not backup.exists()
    run.mkdir()
    paths = git("ls-files","Protocol","Shared.Contracts","Structures/_SharedRuntime","ControlProgram",
        "src/NRE.SimAvatar","src/NRE.BlazorEditor/wwwroot/js","UnityWorld/Assets/NRE/Runtime",
        "Structures/NeuralResonanceEngine.Structures.Runtime.csproj","tools/PosturalCellProbe",
        "Directory.Build.props","Directory.Build.targets","Directory.Packages.props","global.json","NuGet.Config","AGENTS.md").splitlines()
    paths += ["tools/start-postural-cell-window.ps1","tools/_start-dnne-project.ps1","tools/backup-unity-run.ps1",
        "tools/prepare-energy-recovery.py","tools/analyse-energy-recovery.py","tools/prepare-unity-physiology.py",
        "docs/DNNE_Energy_And_Recovery_Repair_2026-10-30.md","docs/DNNE_Support_State_Diagnosis_2026-10-30.md",
        "docs/Unity_Avatar_Migration_Audit_2026-10-12.md","tests/NeuralResonanceEngine.DNNE.Tests/NeuralEnergyRecoveryTests.cs",
        "tests/NeuralResonanceEngine.DNNE.Tests/BiologicalContactLearningTests.cs"]
    for path in sorted(set(paths)): copy(repo/path,run/"source"/path)
    for path in build.rglob("*"):
        if path.is_file(): copy(path,run/"bin"/path.relative_to(build))
    for path in preparation.rglob("*"):
        if path.is_file(): copy(path,run/"preparation"/path.relative_to(preparation))
    copy(repo/"tools/analyse-energy-recovery.py",run/"analysis.py")
    copy(repo/"tools/backup-unity-run.ps1",run/"backup-run.ps1")
    dotnet = Path(shutil.which("dotnet"))
    (run/"runtime-info.txt").write_text(subprocess.check_output([str(dotnet),"--info"],text=True,encoding="utf-8"),encoding="utf-8")
    (run/"source-revision.txt").write_text(revision+"\n",encoding="utf-8")
    prior = repo / "artifacts/cell-support-20261030-29b24e"
    protected = {r["Path"].lower():r for r in read(prior/"configuration.json")["ProtectedBackupFiles"]}
    for capture in [prior,repo/"artifacts/cell-support-review-20261030"]:
        manifest = read(capture/"backup-manifest.json")
        for root in [capture,Path(manifest["Destination"])]:
            for row in manifest["Files"]:
                path = root / row["Path"]
                protected[str(path).lower()] = dict(Path=str(path),Sha256=row["Sha256"])
            path = root/"backup-manifest.json"
            protected[str(path).lower()] = dict(Path=str(path),Sha256=digest(path))
    for row in protected.values(): assert digest(Path(row["Path"])) == row["Sha256"].upper(),row["Path"]
    for name in ["protocol.json","results.json","study/cell-analysis.json"]:
        path=prior/name
        if path.is_file():copy(path,run/"previous-model"/name)
    # The full old results live in study/; preserve an accessible comparison copy.
    for name in ["protocol.json","results.json"]:copy(prior/"study"/name,run/"previous-model"/name)
    files = [dict(Path=str(p),Sha256=digest(p)) for p in sorted(run.rglob("*")) if p.is_file()]
    config = dict(Revision=revision,PreparedUtc=datetime.now(timezone.utc).isoformat(),Repository=str(repo),
        ProbeAssembly=str(run/"bin/NeuralResonanceEngine.DNNE.Tests.dll"),ProbeArguments=["--energy-recovery"],CaseCount=120,
        DotnetExecutable=str(dotnet),PythonExecutable=sys.executable,BackupDestination=str(backup),
        PackageFiles=files,ProtectedBackupFiles=list(protected.values()),NoLiveMonitoring=True,
        WindowTitle="DNNE energy and recovery - 120 isolated conditions",
        StudyDescription="60 simulated seconds per condition, 32 fresh cells, two time steps. Fuel withdrawal/restore, inhibition/recovery, channel ablations and old-drive control. Completion is not physiological qualification.",
        FreshCellsOnly=True,PhysicalRecoveryAssessed=False)
    write(run/"configuration.json",config)
    pointer = dict(Package=str(run),Backup=str(backup),Revision=revision,PackageFiles=len(files),ProtectedFiles=len(protected),NoLiveMonitoring=True)
    write(repo/"artifacts/energy-recovery-launch-20261030.json",pointer)
    print(json.dumps(pointer,indent=2))


if __name__ == "__main__":main()

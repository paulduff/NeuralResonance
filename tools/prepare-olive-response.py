"""Freeze the matched evoked-response test, preserving all completed evidence."""
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
digest=lambda p:sha256(p.read_bytes()).hexdigest().upper()
git=lambda *args:subprocess.check_output(["git",*args],cwd=repo,text=True,encoding="utf-8").strip()


def copy(source,target):
    target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(source,target)


def main():
    assert not git("status","--porcelain"),"Freeze committed source only"
    revision=git("rev-parse","HEAD")
    assert revision==git("rev-parse","origin/main"),"Push the stable source first"
    preparation=repo/"artifacts/olive-response-preparation-20261102"
    build=repo/"artifacts/olive-response-build-20261102/bin/PosturalCellProbe/release"
    checked=read(preparation/"quick-release/cell-analysis.json")
    assert checked["accountingValidated"] and checked["matchedBaselineVerified"]
    assert checked["identicalStepTraceComparisons"]==checked["stepTraceComparisons"]==10
    assert read(preparation/"quick-release/protocol.json")["caseCount"]==20
    tests=(preparation/"analysis-tests.log").read_text(encoding="utf-8-sig")
    assert tests.count("correctly rejected")==7 and "Valid short capture and all full schedules: accepted" in tests
    provenance=read(preparation/"build-provenance.json")
    assert provenance["revision"]==revision
    for entry in provenance["binaries"]:assert digest(build/entry["path"])==entry["sha256"]
    prior=repo/"artifacts/olive-calibration-20261102-8edb50"
    # The experiment changes the stimulus protocol, not the calibrated biology.
    runtime=[]
    for name in git("ls-files","Structures/_SharedRuntime","Protocol","Shared.Contracts").splitlines():
        assert digest(repo/name)==digest(prior/"source"/name),"Runtime changed since calibration: "+name
        runtime.append(dict(path=name,sha256=digest(repo/name)))
    protected={r["Path"].lower():r for r in read(prior/"configuration.json")["ProtectedBackupFiles"]}
    for capture in [prior,repo/"artifacts/olive-calibration-review-20261102"]:
        manifest=read(capture/"backup-manifest.json")
        for base in [capture,Path(manifest["Destination"])]:
            for row in manifest["Files"]:
                path=base/row["Path"];protected[str(path).lower()]=dict(Path=str(path),Sha256=row["Sha256"])
            path=base/"backup-manifest.json";protected[str(path).lower()]=dict(Path=str(path),Sha256=digest(path))
    for row in protected.values():assert digest(Path(row["Path"]))==row["Sha256"].upper(),row["Path"]
    run=repo/"artifacts"/("olive-response-20261102-"+uuid.uuid4().hex[:6])
    backup=Path("D:/DNNE-desktop-runs/UnityWorld")/run.name
    assert not run.exists() and not backup.exists();run.mkdir()
    paths=git("ls-files","Protocol","Shared.Contracts","Structures/_SharedRuntime","ControlProgram",
        "Structures/NeuralResonanceEngine.Structures.Runtime.csproj","tools/PosturalCellProbe",
        "Directory.Build.props","Directory.Build.targets","Directory.Packages.props","global.json","NuGet.Config","AGENTS.md").splitlines()
    paths += ["tools/start-postural-cell-window.ps1","tools/_start-dnne-project.ps1","tools/backup-unity-run.ps1",
        "tools/prepare-olive-response.py","tools/analyse-olive-response.py","tools/tests/olive-response-analysis.Tests.py",
        "docs/DNNE_Olivary_Response_Recovery_2026-11-02.md","docs/DNNE_Olivary_Results_2026-11-02.md",
        "docs/DNNE_Olivary_Calibration_2026-11-02.md","tests/NeuralResonanceEngine.DNNE.Tests/OlivaryCalibrationTests.cs"]
    for name in sorted(set(paths)):copy(repo/name,run/"source"/name)
    for source,target in [(build,run/"bin"),(preparation,run/"preparation")]:
        for path in source.rglob("*"):
            if path.is_file():copy(path,target/path.relative_to(source))
    copy(repo/"tools/analyse-olive-response.py",run/"analysis.py")
    copy(repo/"tools/backup-unity-run.ps1",run/"backup-run.ps1")
    for name in ["protocol.json","results.json","cell-analysis.json"]:copy(prior/"study"/name,run/"previous-calibration"/name)
    copy(repo/"artifacts/olive-calibration-review-20261102/review.json",run/"previous-calibration/review.json")
    write(run/"unchanged-runtime.json",dict(comparedTo=str(prior),files=runtime,allIdentical=True))
    dotnet=Path(shutil.which("dotnet"))
    (run/"runtime-info.txt").write_text(subprocess.check_output([str(dotnet),"--info"],text=True,encoding="utf-8"),encoding="utf-8")
    (run/"source-revision.txt").write_text(revision+"\n",encoding="utf-8")
    files=[dict(Path=str(p),Sha256=digest(p)) for p in sorted(run.rglob("*")) if p.is_file()]
    config=dict(Revision=revision,PreparedUtc=datetime.now(timezone.utc).isoformat(),Repository=str(repo),
        ProbeAssembly=str(run/"bin/NeuralResonanceEngine.DNNE.Tests.dll"),ProbeArguments=["--olive-response"],CaseCount=160,
        DotnetExecutable=str(dotnet),PythonExecutable=sys.executable,BackupDestination=str(backup),PackageFiles=files,
        ProtectedBackupFiles=list(protected.values()),NoLiveMonitoring=True,FreshCellsOnly=True,PhysicalRecoveryAssessed=False,
        WindowTitle="DNNE olive response recovery - 160 matched conditions",
        StudyDescription="Fixed biological olive. Identical sparse four-contact inputs before/during/after inhibition or fuel withdrawal, with matched sham and rest-during-challenge controls. Eight onset offsets, two time steps. Saved response windows and all background spikes; completion is not physiological qualification.")
    write(run/"configuration.json",config)
    pointer=dict(Package=str(run),Backup=str(backup),Revision=revision,PackageFiles=len(files),ProtectedFiles=len(protected),UnchangedRuntimeFiles=len(runtime),NoLiveMonitoring=True)
    write(repo/"artifacts/olive-response-launch-20261102.json",pointer);print(json.dumps(pointer,indent=2))


if __name__=="__main__":main()

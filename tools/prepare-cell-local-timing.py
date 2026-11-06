"""Freeze source and prior evidence; execute no tests or brain simulation here."""
from datetime import datetime, timezone
from hashlib import sha256
import json
from pathlib import Path
import shutil
import subprocess
import sys
import uuid
from zipfile import ZipFile

REPO = Path(__file__).resolve().parents[1]
read = lambda p: json.loads(p.read_text(encoding="utf-8-sig"))


def digest(path):
    value = sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(8*1024*1024), b""):
            value.update(block)
    return value.hexdigest().upper()


def git(*args):
    return subprocess.check_output(["git", *args], cwd=REPO, encoding="utf-8").strip()


def write(path, value):
    path.write_text(json.dumps(value, indent=2, allow_nan=False)+"\n", encoding="utf-8")


def main():
    assert not git("status", "--porcelain"), "Commit source before freezing."
    revision = git("rev-parse", "HEAD")
    assert revision == git("rev-parse", "origin/main"), "Push source before freezing."
    prior = REPO/"artifacts/boundary-timing-20261106-2c82a3"
    status = read(prior/"window-status.json")
    assert status["Status"] == "FINISHED" and status["ExitCode"] == 0 and status["OldBackupUnchanged"]
    review = read(REPO/"artifacts/boundary-timing-review-20261106/review.json")
    assert review["savedAnalysisReproducedExactly"] and review["analysisSummary"]["boundaryHypothesisSupported"]
    protected = {x["Path"].lower(): x for x in read(prior/"configuration.json")["ProtectedBackupFiles"]}
    captures = [prior, REPO/"artifacts/boundary-timing-review-20261106",
        REPO/"artifacts/golgi-channels-20261106-943d65", REPO/"artifacts/golgi-channel-review-20261106"]
    for capture in captures:
        manifest = read(capture/"backup-manifest.json")
        backup = Path(manifest["Destination"])
        assert digest(capture/"backup-manifest.json") == digest(backup/"backup-manifest.json")
        for base in (capture, backup):
            for row in manifest["Files"]:
                path = base/row["Path"]
                protected[str(path).lower()] = dict(Path=str(path), Sha256=row["Sha256"])
            path = base/"backup-manifest.json"
            protected[str(path).lower()] = dict(Path=str(path), Sha256=digest(path))
    # Carry forward expectations, without claiming a fresh audit of every old
    # file. The independent PowerShell window hashes them before and after work.
    baseline = {}
    for stage in ("preflight", "study"):
        baseline[stage] = {}
        for case in read(prior/stage/"results.json")["cases"]:
            files = {}
            for name in ("trace.csv", "contacts.csv", "cell-states.csv"):
                path = prior/stage/case["key"]/name
                value = digest(path)
                assert value == protected[str(path).lower()]["Sha256"]
                files[name] = value
            baseline[stage][case["key"]] = dict(spikes=case["spikes"], files=files)
        assert len(baseline[stage]) == 24
    dotnet = shutil.which("dotnet"); assert dotnet
    stamp = datetime.now(timezone.utc).strftime("%Y%m%d")
    package = REPO/"artifacts"/("cell-local-timing-"+stamp+"-"+uuid.uuid4().hex[:6])
    backup = Path("D:/DNNE-desktop-runs/UnityWorld")/package.name
    assert not package.exists() and not backup.exists()
    package.mkdir()
    archive = package/"source.zip"
    subprocess.run(["git", "archive", "--format=zip", "--output="+str(archive), revision], cwd=REPO, check=True)
    with ZipFile(archive) as zipped:
        for name in zipped.namelist():
            assert not Path(name).is_absolute() and ".." not in Path(name).parts
        zipped.extractall(package/"source")
    for name in ("inputs.json", "baseline.json"):
        assert digest(prior/name) == protected[str(prior/name).lower()]["Sha256"]
        shutil.copy2(prior/name, package/name)
    write(package/"legacy-captures.json", baseline)
    shutil.copy2(package/"source/tools/backup-unity-run.ps1", package/"backup-run.ps1")
    (package/"source-revision.txt").write_text(revision+"\n", encoding="utf-8")
    package_files = [dict(Path=p.relative_to(package).as_posix(), Sha256=digest(p))
        for p in sorted(package.rglob("*")) if p.is_file()]
    write(package/"configuration.json", dict(Revision=revision, PreparedUtc=datetime.now(timezone.utc).isoformat(),
        DotnetExecutable=dotnet, PythonExecutable=str(Path(sys.executable).resolve()), PackageFiles=package_files,
        ProtectedBackupFiles=list(protected.values()), BackupDestination=str(backup), NoLiveMonitoring=True,
        MinimumFullSuiteTests=1076, ConnectedTimingTests=16,
        KnownUnresolvedFullSuiteFailure="NeuralResonanceEngine.DNNE.Tests.NeuronalMemoryTests.RepeatedCorticalExperienceGraduallyRaisesConsolidation",
        FreshCellsOnly=True, ProductionCellLocalModeEnabled=False, PreservationVerifiedDuringPreparation=False))
    pointer = dict(Package=str(package), Backup=str(backup), Revision=revision,
        Launcher=str(package/"source/tools/start-cell-local-timing-window.ps1"),
        PackageFiles=len(package_files), ProtectedFiles=len(protected), NoLiveMonitoring=True)
    write(REPO/("artifacts/cell-local-timing-launch-"+stamp+".json"), pointer)
    print(json.dumps(pointer, indent=2))


if __name__ == "__main__":
    main()

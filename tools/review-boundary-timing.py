"""Read-only review of a completed boundary-timing capture and its D: copy."""
from concurrent.futures import ThreadPoolExecutor
import contextlib
from datetime import datetime, timezone
from hashlib import sha256
import importlib.util
import io
import json
from pathlib import Path
import sys

sys.dont_write_bytecode = True


def read(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def digest(path):
    with path.open("rb") as stream:
        value = sha256()
        for block in iter(lambda: stream.read(8 * 1024 * 1024), b""):
            value.update(block)
        return value.hexdigest().upper()


def audit(base, manifest):
    hashes = {}
    for row in manifest["Files"]:
        path = (base / row["Path"]).resolve()
        assert base.resolve() in path.parents, str(path)
        assert path.stat().st_size == row["Bytes"], str(path)
        value = digest(path)
        assert value == row["Sha256"].upper(), str(path)
        assert str(path).lower() not in hashes
        hashes[str(path).lower()] = value
    return hashes


def main(run, output):
    run, output = run.resolve(), output.resolve()
    if output.exists() or output == run or run in output.parents:
        raise ValueError("Use a new review directory outside the completed capture.")
    status = read(run / "window-status.json")
    config = read(run / "configuration.json")
    manifest = read(run / "backup-manifest.json")
    assert status["Status"] == "FINISHED" and status["ExitCode"] == 0
    assert status["FreshCellsOnly"] and status["OldBackupUnchanged"]
    assert not status["LiveBrainAccessed"] and not status["PhysicalRecoveryAssessed"]
    assert status["Revision"] == config["Revision"]
    backup = Path(manifest["Destination"])
    assert backup.resolve() == Path(config["BackupDestination"]).resolve()
    assert digest(run / "backup-manifest.json") == digest(backup / "backup-manifest.json")
    protocol = read(run / "study/protocol.json")
    assert not protocol["smoke"] and protocol["durationMs"] == 60000
    analyser = run / "analysis.py"
    # Re-run the manifest-verified frozen validator, even when current source has
    # gained a later candidate protocol. Do not rewrite the completed capture.
    assert digest(analyser) == digest(run / "source/tools/analyse-boundary-timing.py")
    saved_hash = digest(run / "study/cell-analysis.json")
    with ThreadPoolExecutor(max_workers=2) as pool:
        local = pool.submit(audit, run, manifest)
        remote = pool.submit(audit, backup, manifest)
        spec = importlib.util.spec_from_file_location("frozen_analysis", analyser)
        module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
        with contextlib.redirect_stdout(io.StringIO()):
            analysis = module.analyse(run / "study", write_report=False)
        assert analysis == read(run / "study/cell-analysis.json")
        assert saved_hash == digest(run / "study/cell-analysis.json")
        hashes = local.result()
        assert len(hashes) == len(remote.result())
    for row in config["PackageFiles"]:
        assert hashes[str(Path(row["Path"]).resolve()).lower()] == row["Sha256"].upper()
    comparisons = []
    for item in analysis["comparisons"]:
        contrast = item["connectedVersusDisconnected"]
        marker = item["connectedVersusMarkers"]
        comparisons.append(dict(
            source=item["sourceId"], stepMs=item["step"],
            markersRestoreUntaughtStatesAndSpikes=marker["sampledStatesIdentical"] and marker["spikesIdentical"],
            disconnectedIdentical=contrast["sampledStatesIdentical"],
            firstDifferentSampleMs=(contrast["firstDifferentSample"] or {}).get("timeMs"),
            maximumOrdinalDifferenceMs=contrast["maximumOrdinalDifferenceMs"],
            maximumSampledVoltageDifferenceMv=contrast["maximumSampledVoltageDifferenceMv"],
            disconnectedVersusMarkersCounts=[(x["firstCount"], x["secondCount"]) for x in item["disconnectedVersusMarkers"]]))
    report = dict(reviewedUtc=datetime.now(timezone.utc).isoformat(), capture=str(run), backup=str(backup),
        revision=status["Revision"], completedUtc=status["CompletedUtc"],
        verifiedFilesPerCopy=len(hashes), verifiedBytesPerCopy=sum(x["Bytes"] for x in manifest["Files"]),
        packagedFilesVerified=len(config["PackageFiles"]), savedAnalysisReproducedExactly=True,
        olderFilePreservation="Run recorded all 40,076 protected paths unchanged; this review rehashes the completed capture and backup only.",
        analysisSummary={k: v for k, v in analysis.items() if k != "comparisons"}, comparisons=comparisons)
    output.mkdir(parents=True)
    (output / "review.json").write_text(json.dumps(report, indent=2, allow_nan=False) + "\n", encoding="utf-8")
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main(Path(sys.argv[1]), Path(sys.argv[2]))

"""Read a completed fresh package; verify preservation and reconstruct its decoder."""
import argparse
from hashlib import file_digest
import json
from pathlib import Path
from statistics import mean


def read(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def digest(path):
    with path.open("rb") as stream:
        return file_digest(stream, "sha256").hexdigest().upper()


def require(value, message):
    if not value:
        raise ValueError(message)


def rate(row, region):
    cells = [r for r in row["regions"] if r["instance"].endswith("_" + region)]
    require(len(cells) == 2, "Missing bilateral region: " + region)
    return mean(r["meanFiringRateHz"] for r in cells)


def spikes(row, region):
    return sum(r["spikeOutCount"] for r in row["regions"] if r["instance"].endswith("_" + region))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("package", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    package, output = args.package.resolve(), args.output.resolve()
    manifest = read(package / "backup-manifest.json")
    backup = Path(manifest["Destination"]).resolve()
    require(not output.exists() and not output.is_relative_to(package) and not output.is_relative_to(backup),
            "Use a new analysis directory outside both preserved copies")
    for item in manifest["Files"]:
        for base in (package, backup):
            require(digest(base / item["Path"]) == item["Sha256"], "Hash mismatch: " + str(base / item["Path"]))
    require(digest(package / "backup-manifest.json") == digest(backup / "backup-manifest.json"), "Manifest mismatch")
    config = read(package / "configuration.json")
    for item in config["ProtectedBackupFiles"] + config["PackageFiles"]:
        require(digest(Path(item["Path"])) == item["Sha256"], "Protected/package file changed: " + item["Path"])
    status = read(package / "window-status.json")
    require(status["Status"] == "FINISHED" and status["ExitCode"] == 0 and status["OldBackupUnchanged"], "Run did not finish cleanly")
    # Reconstruct this captured implementation, not a proposed replacement.
    decoder = package / "source/ControlProgram/NeuronalMotorControl.cs"
    source = decoder.read_text(encoding="utf-8-sig")
    require("Math.Max(0f, vestibular - ((vermis * 0.55f) + (spinalTone * 0.25f)))" in source
            and "balancePredictionError >= 0.40f" in source and "rightingReflex.Drive >= 0.04" in source,
            "Decoder changed; review this reconstruction before applying it")
    for stage, count in (("preflight", 4), ("study", 8)):
        analysis = read(package / stage / "transfer-analysis.json")
        require(analysis["completed"] and analysis["freshOnly"] and analysis["caseCount"] == count
                and all(analysis["validation"].values()), "Recorded validation failed: " + stage)
        require(analysis["analysisSourceSha256"].upper() == digest(package / "analysis.py"), "Unpinned stage analyser")
    study = package / "study"
    cases = read(study / "results.json")["results"]
    traces = {c["plan"]: [json.loads(line) for line in (study / c["case"] / "neural-trace.jsonl")
                         .read_text(encoding="utf-8-sig").splitlines()] for c in cases}
    quiet = traces["quiet-hold"]
    reports = []
    for case in cases:
        rows = traces[case["plan"]]
        require([row["tick"] for row in rows] == list(range(10, 8001, 10)), "Incomplete trace")
        folder = study / case["case"]
        learning = read(folder / "learning-summary.json")
        transfer = read(folder / "transfer-summary.json")
        initial = read(folder / "initial-state.json")
        require(len(initial) == 14 and all(r["loadedInbound"] == r["loadedOutbound"] == 0 and
                not r["copiedSavedState"] for r in initial), "Inherited state")
        raw = [rate(r, "VestibularNuclei") - .55 * rate(r, "CerebellarVermis") - .25 * rate(r, "SpinalCordMotor") for r in rows]
        residual = max(abs(max(0, value) - r["populationError"]) for value, r in zip(raw, rows))
        require(residual < 1e-4, "Decoder reconstruction differs from captured output")
        ownership = []
        for item in learning["instances"]:
            state = read(folder / "isolated-synapses" / (item["instance"] + ".synapses.json"))
            require(state["SchemaVersion"] == 4 and state["InstanceKey"] == item["instance"], "Wrong checkpoint owner")
            contacts = state["Inbound"]
            require(len(contacts) == item["contacts"], "Learning/persistence count mismatch")
            require(sum(c["TotalAbsolutePlasticityChange"] > 0 for c in contacts) == item["changedContacts"], "Change count mismatch")
            ownership.append({"instance": item["instance"], "contacts": len(contacts),
                              "targetsReceivingContacts": len({c["LastTargetNeuronIndex"] for c in contacts}),
                              "sourceInstances": sorted({c["SourceInstanceKey"] for c in contacts})})
        windows = []
        for start, end in ((0, 200), (200, 600), (600, 800)):
            values = rows[start:end]
            count = spikes(rows[end-1], "VestibularNuclei") - (spikes(rows[start-1], "VestibularNuclei") if start else 0)
            base = spikes(quiet[end-1], "VestibularNuclei") - (spikes(quiet[start-1], "VestibularNuclei") if start else 0)
            windows.append({"startMilliseconds": start*10, "endMilliseconds": end*10,
                "vestibularSpikes": count, "matchedQuietSpikes": base, "spikeDifference": count-base,
                "meanVestibularRateDifferenceHz": mean(rate(r, "VestibularNuclei")-rate(q, "VestibularNuclei") for r, q in zip(values, quiet[start:end])),
                "meanBilateralDrive": mean(r["bilateralDrive"] for r in values),
                "driveThresholdSamples": sum(r["bilateralDrive"] >= .04 for r in values),
                "errorThresholdSamples": sum(r["populationError"] >= .4 for r in values),
                "latchSamples": sum(r["latch"] for r in values)})
        paths = transfer["paths"]
        integrated = sum(v["count"] for v in paths.values())
        fastigial = sum(v["count"] for k, v in paths.items() if "|FastigialNucleus|" in k)
        regions = ["VestibularNuclei", "CerebellarVermis", "FastigialNucleus", "SpinalCordMotor"]
        reports.append({"case": case["case"], "plan": case["plan"], "windows": windows,
            "vestibularSpikes": spikes(rows[-1], "VestibularNuclei"),
            "meanRateHz": {name: mean(rate(r, name) for r in rows) for name in regions},
            "unclampedErrorRange": [min(raw), max(raw)], "maximumDecoderResidual": residual,
            "peakPopulationError": max(r["populationError"] for r in rows),
            "peakBilateralDrive": max(r["bilateralDrive"] for r in rows),
            "fastigialFractionOfIntegratedVestibularInput": fastigial/integrated,
            "vermisSampledRateAndCumulativeSpikesMatchQuiet": all(rate(r,"CerebellarVermis") == rate(q,"CerebellarVermis") and
                spikes(r,"CerebellarVermis") == spikes(q,"CerebellarVermis") for r,q in zip(rows,quiet)),
            "initialRegionTracesMatchQuiet": all(r["regions"] == q["regions"] for r,q in zip(rows[:200],quiet[:200])),
            "contactCount": sum(r["contacts"] for r in learning["instances"]),
            "contactsWithRecordedPlasticity": sum(r["changedContacts"] for r in learning["instances"]),
            "presynapticContactEvents": sum(r["presynapticEvents"] for r in learning["telemetry"]),
            "postsynapticContactEvents": sum(r["postsynapticEvents"] for r in learning["telemetry"]),
            "ownership": ownership})
    output.mkdir(parents=True)
    report = {"sourcePackage": str(package), "revision": config["Revision"],
        "verification": {"sourceAndBackupFiles": len(manifest["Files"]), "protectedOldFiles": len(config["ProtectedBackupFiles"]),
                         "packagedFiles": len(config["PackageFiles"]), "backupManifestSha256": digest(package/"backup-manifest.json")},
        "decoderSourceSha256": digest(decoder), "analyserSha256": digest(Path(__file__)),
        "scope": "Offline reconstruction of saved 10 ms samples; spike totals are cumulative. No causal ablation or physical recovery test.",
        "biologicalEquivalenceQualified": False, "physicalRecoveryAssessed": False, "cases": reports}
    (output / "fresh-review.json").write_text(json.dumps(report, indent=2, allow_nan=False)+"\n", encoding="utf-8")
    print(json.dumps({"output": str(output), "verification": report["verification"], "cases": len(reports)}))


if __name__ == "__main__":
    main()

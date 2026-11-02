"""Read-only audit of a completed energy/recovery package; write a separate review."""
from datetime import datetime, timezone
from hashlib import sha256
import json
import math
from pathlib import Path
import statistics
import struct
import sys

read = lambda p: json.loads(p.read_text(encoding="utf-8-sig"))
digest = lambda p: sha256(p.read_bytes()).hexdigest().upper()


def main(run, output):
    if output.exists():
        raise FileExistsError("Use a new review directory; completed packages are never edited.")
    status, config, manifest = (read(run / p) for p in ("window-status.json", "configuration.json", "backup-manifest.json"))
    assert status["Status"] == "FINISHED" and status["ExitCode"] == 0
    assert status["Revision"] == config["Revision"]
    backup = Path(manifest["Destination"])
    for base in (run, backup):
        for entry in manifest["Files"]:
            assert digest(base / entry["Path"]) == entry["Sha256"].upper(), str(base / entry["Path"])
    assert digest(run / "backup-manifest.json") == digest(backup / "backup-manifest.json")
    for entry in config["PackageFiles"] + config["ProtectedBackupFiles"]:
        assert digest(Path(entry["Path"])) == entry["Sha256"].upper(), entry["Path"]

    study = run / "study"
    protocol, result, analysis = (read(study / p) for p in ("protocol.json", "results.json", "cell-analysis.json"))
    assert not protocol["smoke"] and not protocol["quick"]
    assert protocol["durationMilliseconds"] == 60000 and protocol["sampledCellsPerCondition"] == 32
    assert result["completed"] and analysis["accountingValidated"] and len(result["cases"]) == 120
    phase_ms = protocol["phaseMilliseconds"]
    summaries, keys = [], set()
    max_error, trace_rows, sampled_states = 0, 0, 0
    for case in result["cases"]:
        key = (case["profile"], case["control"], case["dtMs"], case["scenario"])
        assert key not in keys
        keys.add(key)
        assert len(case["phases"]) == 6
        indices = [c["cellIndex"] for c in case["phases"][0]]
        assert len(indices) == len(set(indices)) == 32
        for phase in case["phases"]:
            assert [c["cellIndex"] for c in phase] == indices
            for c in phase:
                assert c["finite"] and c["steps"] == phase_ms / case["dtMs"]
                assert 0 <= c["minimumReserve"] <= c["endReserve"] <= 1
                max_error = max(max_error, c["maximumBalanceError"])
        rates = [statistics.mean(c["spikes"] for c in phase) * 1000 / phase_ms for phase in case["phases"]]
        saved = next(c for c in analysis["conditions"] if (c["profile"], c["control"], c["dtMs"], c["scenario"]) == key)
        assert rates == saved["phaseRatesHz"]
        challenge = case["scenario"] == "receptor-challenge"
        assert case["excitatoryEvents"] == (32 * phase_ms / 50 if challenge else 0)
        assert case["inhibitoryEvents"] == (32 * phase_ms / 10 if challenge else 0)
        count = 0
        with (study / (case["name"] + ".jsonl")).open(encoding="utf-8-sig") as handle:
            for line in handle:
                trace = json.loads(line)
                count += 1
                assert trace["timestampMs"] == count * 100
                phase = math.floor((trace["timestampMs"] - case["dtMs"]) / phase_ms)
                assert trace["phase"] == phase
                assert [c["index"] for c in trace["cells"]] == indices
                withdrawing = phase in (1, 2)
                assert trace["supply"]["glucoseAvailability"] == (0 if withdrawing and case["scenario"] == "glucose-withdrawal" else 1)
                assert trace["supply"]["oxygenAvailability"] == (0 if withdrawing and case["scenario"] == "oxygen-withdrawal" else 1)
                for i,c in enumerate(trace["cells"]):
                    assert all(math.isfinite(v) for v in c.values())
                    for gate in ("hcnGate", "inwardActivation", "inwardAvailability"):
                        assert -1e-9 <= c[gate] <= 1+1e-9
                    assert abs(c["reserve"] - (1+c["produced"]-c["consumed"]-c["overflow"])) < 1e-8
                    assert abs(c["requested"]-c["consumed"]-c["unmet"]) < 1e-8
                    state = case["phases"][phase][i]
                    # Trace voltage is serialized as float32; extrema promote
                    # that same value to double. Restore its exact float32 value
                    # before comparison rather than treating JSON formatting as drift.
                    voltage = struct.unpack("f",struct.pack("f",c["voltage"]))[0]
                    assert state["minimumVoltage"] <= voltage <= state["maximumVoltage"]
                    if trace["timestampMs"] % phase_ms == 0:
                        assert c["reserve"] == state["endReserve"]
        assert count == 600
        assert all(a["reserve"] == b["reserve"] for a,b in zip(trace["cells"],case["energy"]))
        trace_rows += count
        sampled_states += count * len(indices)
        returned_phase = 4 if challenge else 3
        returned_ms = returned_phase * phase_ms
        latencies = []
        if case["scenario"] != "quiet":
            for index in indices:
                times = [c["firstSpikeMs"] for phase in case["phases"][returned_phase:]
                         for c in phase if c["cellIndex"] == index and c["firstSpikeMs"] is not None]
                latencies.append(min(times) - returned_ms if times else None)
        observed = [x for x in latencies if x is not None]
        summaries.append(dict(profile=key[0],control=key[1],dtMs=key[2],scenario=key[3],phaseRatesHz=rates,
            activeCellsInFinalPhase=sum(c["spikes"]>0 for c in case["phases"][5]),
            firstReturnSpikeMs=dict(minimum=min(observed),median=statistics.median(observed),maximum=max(observed)) if observed else None,
            cellsWithNoReturnSpike=sum(x is None for x in latencies),
            minimumReserve=min(c["minimumReserve"] for phase in case["phases"] for c in phase),
            finalMeanReserve=statistics.mean(c["reserve"] for c in case["energy"]),
            unmetDemand=sum(c["unmet"] for c in case["energy"])))
    assert max_error < 1e-8
    expected = {(p,c,dt,s) for p in protocol["profiles"] for c in protocol["controls"]
                for dt in protocol["integrationStepsMilliseconds"] for s in protocol["scenarios"]}
    assert keys == expected
    lookup = {(c["profile"],c["control"],c["dtMs"],c["scenario"]):c for c in summaries}
    max_rate_gap = max(abs(a-b) for p,c,dt,s in keys if dt==.25
                       for a,b in zip(lookup[p,c,.25,s]["phaseRatesHz"],lookup[p,c,.125,s]["phaseRatesHz"]))
    candidates = [c for c in summaries if c["control"] == "candidate"]
    report = dict(reviewedUtc=datetime.now(timezone.utc).isoformat(),run=str(run),revision=config["Revision"],
        completed=True,captureFilesVerifiedAtBothLocations=len(manifest["Files"]),
        packageFilesVerified=len(config["PackageFiles"]),protectedFilesVerified=len(config["ProtectedBackupFiles"]),
        validatedConditions=len(summaries),traceRowsVerified=trace_rows,cellTraceStatesVerified=sampled_states,
        maximumEnergyBalanceError=max_error,maximumPhaseRateGapHz=max_rate_gap,
        allCandidateCellsActiveInFinalPhase=all(c["activeCellsInFinalPhase"]==32 for c in candidates),
        candidateUnmetDemand=sum(c["unmetDemand"] for c in candidates),
        physiologicalCalibrationPassed=False,physicalRecoveryAssessed=False,
        mechanismConclusion="At the new current conversion, recovery survives removal of either or both added inward channel mechanisms. Prior conversion loses Purkinje recovery despite both channels. Channel necessity is not established; operating regime matters.",
        latencyCaution="First spike after oxygen restoration is not necessarily a restart: some cells still fire sparsely during oxygen withdrawal. Glucose withdrawal and the inhibitory phase are separately reported.",
        conditions=summaries)
    output.mkdir(parents=True)
    (output / "review.json").write_text(json.dumps(report,indent=2,allow_nan=False)+"\n",encoding="utf-8")
    print(json.dumps({k:v for k,v in report.items() if k!="conditions"},indent=2))


if __name__ == "__main__":
    main(Path(sys.argv[1]).resolve(),Path(sys.argv[2]).resolve())

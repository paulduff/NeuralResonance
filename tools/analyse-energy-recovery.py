"""Validate saved energy/recovery accounting; report response without assuming success."""
import json
import math
from pathlib import Path
import sys


def analyse(root):
    read = lambda p: json.loads(p.read_text(encoding="utf-8-sig"))
    protocol, result = read(root / "protocol.json"), read(root / "results.json")
    count, phase_ms = protocol["sampledCellsPerCondition"], protocol["phaseMilliseconds"]
    assert result["completed"] and len(result["cases"]) == protocol["caseCount"] == 120
    keys, summaries = set(), []
    for case in result["cases"]:
        key = (case["profile"], case["control"], case["dtMs"], case["scenario"])
        assert key not in keys
        keys.add(key)
        assert len(case["phases"]) == 6
        for phase in case["phases"]:
            assert len(phase) == count
            assert len({c["cellIndex"] for c in phase}) == count
            for cell in phase:
                assert cell["finite"] and cell["steps"] == phase_ms / case["dtMs"]
                assert 0 <= cell["minimumReserve"] <= cell["endReserve"] <= 1
                assert cell["maximumBalanceError"] < 1e-8
        challenge = case["scenario"] == "receptor-challenge"
        assert case["excitatoryEvents"] == (count * phase_ms / 50 if challenge else 0)
        assert case["inhibitoryEvents"] == (count * phase_ms / 10 if challenge else 0)
        for energy in case["energy"]:
            assert all(math.isfinite(v) for v in energy.values())
            assert abs(energy["balanceError"]) < 1e-8
            assert abs(energy["requested"] - energy["consumed"] - energy["unmet"]) < 1e-8
        traces = 0
        with (root / (case["name"] + ".jsonl")).open(encoding="utf-8-sig") as handle:
            for line in handle:
                trace = json.loads(line)
                traces += 1
                assert trace["timestampMs"] == traces * 100
                assert len(trace["cells"]) == count
                assert all(abs(c["balanceError"]) < 1e-8 for c in trace["cells"])
                for c in trace["cells"]:
                    assert all(-1e-9 <= c[g] <= 1+1e-9 for g in ("hcnGate", "inwardActivation", "inwardAvailability"))
                withdrawal = trace["phase"] in (1, 2)
                assert trace["supply"]["glucoseAvailability"] == (0 if withdrawal and case["scenario"] == "glucose-withdrawal" else 1)
                assert trace["supply"]["oxygenAvailability"] == (0 if withdrawal and case["scenario"] == "oxygen-withdrawal" else 1)
        assert traces == protocol["durationMilliseconds"] / 100
        rates = [sum(c["spikes"] for c in p) / count * 1000 / phase_ms for p in case["phases"]]
        summaries.append(dict(profile=case["profile"], control=case["control"], dtMs=case["dtMs"], scenario=case["scenario"],
            phaseRatesHz=rates, phaseMinimumReserve=[min(c["minimumReserve"] for c in p) for p in case["phases"]],
            finalActiveCellFraction=sum(c["spikes"]>0 for c in case["phases"][5])/count,
            firstSpikeMillisecondsByPhase=[[c["firstSpikeMs"] for c in phase] for phase in case["phases"]],
            finalMeanReserve=sum(c["reserve"] for c in case["energy"]) / count,
            finalUnmetDemand=sum(c["unmet"] for c in case["energy"])))
    lookup = {(s["profile"], s["control"], s["dtMs"], s["scenario"]): s for s in summaries}
    response = []
    for p in protocol["profiles"]:
        for h in protocol["controls"]:
            for dt in (.25, .125):
                quiet, challenge = lookup[p,h,dt,"quiet"], lookup[p,h,dt,"receptor-challenge"]
                response.append(dict(profile=p, control=h, dtMs=dt,
                    quietSustained=quiet["phaseRatesHz"][5]>0,
                    inhibitorySuppressionHz=quiet["phaseRatesHz"][3]-challenge["phaseRatesHz"][3],
                    recoveredRateFraction=challenge["phaseRatesHz"][5]/quiet["phaseRatesHz"][5] if quiet["phaseRatesHz"][5]>0 else None))
    convergence = []
    for p,h,dt,scenario in sorted(keys):
        if dt != .25: continue
        coarse, fine = lookup[p,h,.25,scenario],lookup[p,h,.125,scenario]
        convergence.append(dict(profile=p,control=h,scenario=scenario,
            maximumPhaseRateGapHz=max(abs(a-b) for a,b in zip(coarse["phaseRatesHz"],fine["phaseRatesHz"])),
            finalReserveGap=abs(coarse["finalMeanReserve"]-fine["finalMeanReserve"])))
    report = dict(completed=True,accountingValidated=True,conditions=summaries,response=response,convergence=convergence,
        biologicalEquivalenceQualified=False,physicalRecoveryAssessed=False,
        interpretation="Execution/accounting success is separate from cell response, channel attribution, and physiological calibration. No restart pulses or reserve clamps in the candidate.")
    (root / "cell-analysis.json").write_text(json.dumps(report,indent=2,allow_nan=False)+"\n",encoding="utf-8")
    print(f"Validated {len(summaries)} conditions; inspect saved response and convergence before accepting the mechanism.")


if __name__ == "__main__":
    analyse(Path(sys.argv[1]).resolve())

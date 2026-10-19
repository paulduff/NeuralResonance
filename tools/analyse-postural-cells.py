"""Validate cell-probe event/clock accounting and report paired responses, without tuning."""
import argparse
import json
import math
from pathlib import Path
from statistics import mean


def read(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def require(condition, message):
    if not condition:
        raise ValueError(message)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", type=Path)
    args = parser.parse_args()
    root = args.directory
    protocol, results = read(root/"protocol.json"), read(root/"results.json")
    require(protocol["study"] == "postural-cell-response" and results["completed"], "Incomplete cell probe")
    cases = [read(root/(item["case"]+".json")) for item in results["cases"]]
    keys = lambda c: (c["structure"], c["pacemaker"], c["stepMilliseconds"], c["input"])
    indexed = {keys(c): c for c in cases}
    expected = {(p["id"], pacemaker, dt, stimulus["name"]) for p in protocol["profiles"]
                for pacemaker in (True,False) for dt in protocol["integrationStepsMilliseconds"] for stimulus in protocol["inputs"]}
    require(len(cases) == len(indexed) == protocol["caseCount"] == 72 and set(indexed) == expected, "Case matrix mismatch")
    inputs = {r["name"]: r for r in protocol["inputs"]}
    output = []
    for case in cases:
        duration = protocol["durationMilliseconds"]
        count = protocol["sampledCellsPerCondition"]
        dt = case["stepMilliseconds"]
        stimulus = inputs[case["input"]]
        for transmitter in ("glutamate", "gaba"):
            period = stimulus[transmitter+"PeriodMs"]
            expected_events = math.ceil((duration/2)/period)*count if period else 0
            require(case[transmitter+"Events"] == expected_events, "Receptor event accounting: "+case["case"])
        require(set(case["phases"]) == {"initial","stimulus","return"}, "Missing phases")
        baseline = indexed[(case["structure"], case["pacemaker"], dt, "none")]
        require(case["phases"]["initial"] == baseline["phases"]["initial"], "Unmatched fresh baseline: "+case["case"])
        phase_rates = {}
        for phase, cells in case["phases"].items():
            elapsed = duration/(2 if phase == "stimulus" else 4)
            begin = {"initial":0, "stimulus":duration/4, "return":duration*3/4}[phase]
            require(len(cells) == len({c["index"] for c in cells}) == count, "Wrong cell sample")
            for cell in cells:
                require(cell["samples"]*dt == cell["elapsedMilliseconds"] == elapsed, "Clock accounting mismatch")
                times = cell["spikeTimesMilliseconds"]
                require(cell["spikes"] == len(times) and all(begin < t <= begin+elapsed for t in times)
                        and all(a < b for a,b in zip(times,times[1:])), "Spike timing/count mismatch")
                require(all(math.isfinite(v) for v in cell.values() if isinstance(v,(float,int))), "Nonfinite observation")
            phase_rates[phase] = mean(c["rateHz"] for c in cells)
        stimulus_cells = case["phases"]["stimulus"]
        baseline_cells = baseline["phases"]["stimulus"]
        output.append({"case":case["case"], "structure":case["structure"], "pacemaker":case["pacemaker"],
            "stepMilliseconds":dt, "input":case["input"], "meanRateHz":phase_rates,
            "stimulusRateDifferenceFromMatchedNoInputHz":phase_rates["stimulus"]-mean(c["rateHz"] for c in baseline_cells),
            "cellsWithDifferentStimulusSpikeTimes":sum(a["spikeTimesMilliseconds"] != b["spikeTimesMilliseconds"] for a,b in zip(stimulus_cells,baseline_cells)),
            "meanStimulusNetCurrent":mean(c["meanNetSynapticCurrent"] for c in stimulus_cells),
            "aisBoundaryFraction":sum(c["aisBoundaryMilliseconds"] for cells in case["phases"].values() for c in cells)/(duration*count)})
    by_key = {(c["structure"],c["pacemaker"],c["stepMilliseconds"],c["input"]):c for c in output}
    for case in output:
        other = by_key[(case["structure"],case["pacemaker"],.25,case["input"])]
        case["stimulusRateDifferenceFromQuarterMillisecondHz"] = case["meanRateHz"]["stimulus"]-other["meanRateHz"]["stimulus"]
    report = {"completed": True, "caseCount":len(cases), "smoke":protocol["smoke"],
        "accountingValidated":True, "freshBaselinesMatched":True, "results":output,
        "interpretation":"Paired model response and numerical sensitivity, not physiological validation. Intrinsic cell tonic drive remains on in the no-pacemaker control. No learned contacts or network behaviour are assessed.",
        "biologicalEquivalenceQualified":False,"physicalRecoveryAssessed":False}
    (root/"cell-analysis.json").write_text(json.dumps(report,indent=2,allow_nan=False)+"\n",encoding="utf-8")
    print(json.dumps({k:v for k,v in report.items() if k != "results"},indent=2))


if __name__ == "__main__":
    main()

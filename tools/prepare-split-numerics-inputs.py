"""Copy exact archived stimuli and two complete olive trains into a new input bundle."""
from hashlib import sha256
import csv
import json
from pathlib import Path
import sys

def main(capture,output):
    if output.exists():raise ValueError("Use a new bundle path")
    read=lambda p:json.loads(p.read_text(encoding="utf-8-sig"))
    status=read(capture/"window-status.json");assert status["Status"]=="FINISHED"
    study=capture/"study";analysis=read(study/"cell-analysis.json")
    assert analysis["accountingValidated"] and analysis["hostPacketInvarianceValidated"]
    sources=[];replays=[];provenance=[]
    for plan in ("paired100","inhibition"):
        for offset in (0,60):
            key=f"calcium-{plan}-{offset}-0.125-h0.03125";directory=study/key
            design=read(directory/"design.json")
            for name in ("design.json","events.jsonl","summary.json"):
                p=directory/name;provenance.append(dict(path=str(p.resolve()),sha256=sha256(p.read_bytes()).hexdigest()))
            inputs=[dict(arrivalMs=x["message"]["timestampMs"]+x["delayMs"],message=x["message"],role=x["role"],delayMs=x["delayMs"]) for x in design["inputs"] if x["message"]["targetStructure"]=="InferiorOlive"]
            baseline_key=f"calcium-{plan}-{offset}-0.125-h0.125"
            baseline=next(c for c in analysis["cases"] if c["key"]==baseline_key)
            baseline_path=study/baseline_key/"cells.csv"
            provenance.append(dict(path=str(baseline_path.resolve()),sha256=sha256(baseline_path.read_bytes()).hexdigest()))
            with baseline_path.open(newline="") as f:
                samples=[{k:float(v) for k,v in row.items() if k in ("timeMs","oliveSoma","oliveDendrite","oliveAxon","oReserve","oProduced","oRequested","oConsumed","oUnmet","oOverflow")} for row in csv.DictReader(f)]
            sources.append(dict(id=f"{plan}-{offset}",plan=plan,offset=offset,inputs=inputs,baseline=dict(spikeTimes=baseline["sourceTimes"],samples=samples)))
            if plan!="paired100":continue
            inputs=[dict(arrivalMs=x["message"]["timestampMs"]+x["delayMs"],message=x["message"],role=x["role"],delayMs=x["delayMs"]) for x in design["inputs"] if x["message"]["targetStructure"]=="PurkinjeCellLayer"]
            with (directory/"events.jsonl").open() as f:
                for line in f:
                    e=json.loads(line)
                    if e["kind"]=="climbing":
                        x=e["value"];assert x["connected"]
                        inputs.append(dict(arrivalMs=x["message"]["timestampMs"]+x["delayMs"],message=x["message"],role="climbing",delayMs=x["delayMs"]))
            inputs.sort(key=lambda x:(x["arrivalMs"],x["role"]))
            assert sum(x["role"]=="climbing" for x in inputs)==read(directory/"summary.json")["oliveAxon"]
            replays.append(dict(id=f"paired100-{offset}",offset=offset,inputs=inputs,contacts=design["contacts"],taughtCell=design["taughtCell"]))
    data=dict(sourceCapture=str(capture.resolve()),sourceRevision=status["Revision"],durationMs=60000,
        interpretation="Recorded finest-step source is an immutable diagnostic stimulus, not a certified physiological train.",
        sourceCases=sources,replayCases=replays,provenance=provenance)
    output.parent.mkdir(parents=True,exist_ok=True);output.write_text(json.dumps(data,indent=2)+"\n",encoding="utf-8")
    print(f"Frozen {len(sources)} source schedules and {len(replays)} complete replay trains; originals unchanged.")

if __name__=="__main__":main(Path(sys.argv[1]),Path(sys.argv[2]))

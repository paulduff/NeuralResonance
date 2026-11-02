"""Read-only completed-run audit and descriptive calcium-learning comparison."""
import bisect
import contextlib
import csv
from datetime import datetime, timezone
from hashlib import sha256
import importlib.util
import io
import json
from pathlib import Path
import statistics
import sys

sys.dont_write_bytecode=True
spec=importlib.util.spec_from_file_location("analysis",Path(__file__).with_name("analyse-olive-purkinje.py"))
analysis=importlib.util.module_from_spec(spec);spec.loader.exec_module(analysis)
read=lambda p:json.loads(p.read_text(encoding="utf-8-sig"))
def digest(p):
    h=sha256()
    with p.open("rb") as f:
        for block in iter(lambda:f.read(8*1024*1024),b""):h.update(block)
    return h.hexdigest().upper()
def save(p,value):p.write_text(json.dumps(value,indent=2,allow_nan=False)+"\n",encoding="utf-8")

def main(run,output):
    run=run.resolve();output=output.resolve()
    if output.exists() or output==run or run in output.parents:raise ValueError("Use a fresh review directory outside the capture.")
    config,status,manifest=(read(run/p) for p in ("configuration.json","window-status.json","backup-manifest.json"))
    assert status["Status"]=="FINISHED" and status["ExitCode"]==0 and status["OldBackupUnchanged"]
    assert status["Revision"]==config["Revision"]
    for base in (run,Path(manifest["Destination"])):
        for entry in manifest["Files"]:assert digest(base/entry["Path"])==entry["Sha256"].upper(),str(base/entry["Path"])
    assert digest(run/"backup-manifest.json")==digest(Path(manifest["Destination"])/"backup-manifest.json")
    for entry in config["PackageFiles"]+config["ProtectedBackupFiles"]:
        assert digest(Path(entry["Path"]))==entry["Sha256"].upper(),entry["Path"]
    unchanged=read(run/"unchanged-olive.json")
    for entry in unchanged["files"]:
        assert digest(run/"source"/entry["path"])==digest(Path(unchanged["comparedTo"])/"source"/entry["path"])==entry["sha256"]
    assert digest(run/"analysis.py")==digest(Path(__file__).with_name("analyse-olive-purkinje.py"))
    study=run/"study";protocol=read(study/"protocol.json");results=read(study/"results.json")
    assert not protocol["smoke"] and not protocol["quick"] and protocol["durationMs"]==240000
    original=digest(study/"cell-analysis.json")
    with contextlib.redirect_stdout(io.StringIO()):validated=analysis.analyse(study,write_report=False)
    assert validated==read(study/"cell-analysis.json") and original==digest(study/"cell-analysis.json")
    print("Capture, package, older saves and complete study accounting independently verified.",flush=True)
    lookup={c["key"]:c for c in validated["cases"]}
    fine=[c for c in results["cases"] if c["dt"]==.125]
    details=[]
    for case in fine:
        directory=study/case["key"];design=read(directory/"design.json")
        ids={x["contactId"]:x["role"] for x in design["contacts"]}
        # Stored trace frames are 20 ms apart. These are sampled attainment times,
        # not invented sub-frame molecular or peak-calcium timestamps.
        trajectories={role:dict(firstMaximumInductionMs=None,maximumInduction=-1,weightAt10000=None,
            weightAt60000=None,weightAt240000=None,inductionAt10000=None,inductionAt60000=None,
            peakSampledCalcium=0,peakSampledIntegratedCalcium=0) for role in analysis.ROLES}
        with (directory/"contacts.csv").open(newline="") as f:
            for raw in csv.DictReader(f):
                role=raw.pop("role");row={k:float(v) for k,v in raw.items()};t=row["timeMs"];data=trajectories[role]
                if row["inducedDepression"]>data["maximumInduction"]:
                    data["maximumInduction"]=row["inducedDepression"];data["firstMaximumInductionMs"]=t
                data["peakSampledCalcium"]=max(data["peakSampledCalcium"],row["calcium"])
                data["peakSampledIntegratedCalcium"]=max(data["peakSampledIntegratedCalcium"],row["integratedCalcium"])
                if t in (10000,60000,240000):data["weightAt"+str(int(t))]=row["weight"]
                if t in (10000,60000):data["inductionAt"+str(int(t))]=row["inducedDepression"]
        parallel_times={r:[] for r in analysis.ROLES};climbing=[];pc=[[],[]];event_changes=[]
        for line in (directory/"events.jsonl").open(encoding="utf-8"):
            event=json.loads(line);kind=event["kind"];x=event["value"]
            if kind=="purkinje-spike":pc[int(x["sourceNeuronId"][2:])].append(x["timestampMs"])
            if kind!="learning":continue
            if x["contactId"] in ids:parallel_times[ids[x["contactId"]]].append(x["processedMs"])
            if x["source"]!="InferiorOlive":continue
            climbing.append(x["processedMs"])
            before={c["contactId"]:c for c in x["before"]}
            for after in x["after"]:
                delta=after["weight"]-before[after["contactId"]]["weight"]
                if delta!=0:event_changes.append(dict(timeMs=x["processedMs"],role=ids[after["contactId"]],delta=delta))
        arrival_lags={}
        for role,times in parallel_times.items():
            lags=[]
            for t in climbing:
                i=bisect.bisect_right(times,t)-1
                if i>=0:lags.append(t-times[i])
            arrival_lags[role]=dict(count=len(lags),within50ms=sum(0<=x<=50 for x in lags),
                within200ms=sum(0<=x<=200 for x in lags),medianMs=statistics.median(lags) if lags else None)
        changes=[]
        for row in event_changes:
            times=parallel_times[row["role"]];i=bisect.bisect_right(times,row["timeMs"])-1
            changes.append(dict(**row,msSinceLastParallel= row["timeMs"]-times[i] if i>=0 else None))
        durations=[(0,60000),(60000,240000),(230000,240000)]
        rates={f"{a}-{b}":[sum(a<t<=b for t in train)*1000/(b-a) for train in pc] for a,b in durations}
        details.append(dict(key=case["key"],variant=case["variant"],plan=case["plan"],offset=case["offset"],
            trajectories=trajectories,arrivalLags=arrival_lags,legacyContactChanges=changes,purkinjeRatesHz=rates,
            maximumClimbingArrivalMs=max(climbing,default=None)))
    grouped=[]
    for variant in analysis.VARIANTS:
        for plan in analysis.PLANS:
            cs=[c for c in fine if c["variant"]==variant and c["plan"]==plan];assert len(cs)==2
            contacts={}
            for role in analysis.ROLES:
                states=[next(x for x in c["contacts"] if x["role"]==role) for c in cs]
                losses=[100*(1-x["state"]["weight"]) for x in states]
                induction=[lookup[c["key"]]["contacts"][role]["inducedDepression"] for c in cs]
                contacts[role]=dict(meanLossPercent=statistics.mean(losses),lossRangePercent=[min(losses),max(losses)],
                    meanPeakCalciumUm=statistics.mean(x["peakCalcium"] for x in states),
                    meanAsymptoticLossPercent=100*statistics.mean(induction))
            grouped.append(dict(variant=variant,plan=plan,oliveAxonalCounts=[c["oliveAxon"] for c in cs],
                deliveredCounts=[c["delivered"] for c in cs],purkinjeSpikeCounts=[c["pcSpikes"] for c in cs],contacts=contacts))
    step_summary={}
    for v in analysis.VARIANTS:
        rows=[x for x in validated["stepComparisons"] if x["variant"]==v]
        step_summary[v]={k:max(abs(x[k]) for x in rows) for k in ("oliveCountDifference","purkinjeCountDifference","maximumWeightDifference","maximumInductionDifference")}
    report=dict(reviewedUtc=datetime.now(timezone.utc).isoformat(),run=str(run),revision=config["Revision"],completedUtc=status["CompletedUtc"],
        audit=dict(captureFilesPerCopy=len(manifest["Files"]),packageFiles=len(config["PackageFiles"]),protectedFiles=len(config["ProtectedBackupFiles"]),
            unchangedOliveFiles=len(unchanged["files"]),caseCount=validated["caseCount"],cellRows=validated["cellRows"],
            contactRows=validated["contactRows"],eventRows=validated["eventRows"],originalEvidenceUnchanged=True,
            maximumEnergyBalanceError=max(c["maxEnergyError"] for c in results["cases"]),
            maximumCalciumMassError=max(x["massError"] for c in results["cases"] for x in c["contacts"])),
        physiologicalCalibrationComplete=False,readyForFastigialQualification=False,physicalRecoveryAssessed=False,
        interpretation="Descriptive deterministic comparison, two onset offsets at the finer step only. No statistical biological replication. Calcium dependence is demonstrable but useful teaching, contact selectivity and physiological dose calibration remain unqualified.",
        groups=grouped,stepSensitivity=step_summary,details=details)
    output.mkdir(parents=True);save(output/"review.json",report)
    with (output/"contact-comparison.tsv").open("w",newline="",encoding="utf-8") as f:
        writer=csv.writer(f,delimiter='\t');writer.writerow(["variant","plan","role","meanLossPercent","minimumLossPercent","maximumLossPercent","meanPeakCalciumUm","meanAsymptoticLossPercent"])
        for group in grouped:
            for role,c in group["contacts"].items():writer.writerow([group["variant"],group["plan"],role,c["meanLossPercent"],*c["lossRangePercent"],c["meanPeakCalciumUm"],c["meanAsymptoticLossPercent"]])
    print(json.dumps(dict(audit=report["audit"],stepSensitivity=step_summary,output=str(output)),indent=2))

if __name__=="__main__":main(Path(sys.argv[1]),Path(sys.argv[2]))

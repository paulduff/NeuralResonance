"""Read-only review of the frozen olive-reference / fixed-Purkinje-input study."""
from concurrent.futures import ThreadPoolExecutor
import contextlib
import csv
from datetime import datetime, timezone
from hashlib import sha256
import importlib.util
import io
import json
from pathlib import Path
import sys

sys.dont_write_bytecode=True
read=lambda p:json.loads(p.read_text(encoding="utf-8-sig"))
def digest(p):
    h=sha256()
    with p.open("rb") as f:
        for b in iter(lambda:f.read(8*1024*1024),b""):h.update(b)
    return h.hexdigest().upper()

def save(p,x):p.write_text(json.dumps(x,indent=2,allow_nan=False)+"\n",encoding="utf-8")

def audit_copy(base,manifest):
    hashes={}
    for row in manifest["Files"]:
        p=base/row["Path"];value=digest(p)
        assert value==row["Sha256"].upper() and p.stat().st_size==row["Bytes"],str(p)
        hashes[str(p.resolve()).lower()]=value
    print(f"Verified {len(hashes)} captured files: {base}",flush=True)
    return hashes

def csv_rows(path):
    with path.open(newline="") as f:yield from csv.DictReader(f)

def main(run,output):
    run=run.resolve();output=output.resolve()
    if output.exists() or output==run or run in output.parents:raise ValueError("Use a new review directory outside the completed capture.")
    status=read(run/"window-status.json");config=read(run/"configuration.json");manifest=read(run/"backup-manifest.json")
    assert status["Status"]=="FINISHED" and status["ExitCode"]==0 and status["OldBackupUnchanged"] and not status["LiveBrainAccessed"]
    assert status["Revision"]==config["Revision"]
    backup=Path(manifest["Destination"])
    assert backup.resolve()==Path(config["BackupDestination"]).resolve()
    assert digest(run/"backup-manifest.json")==digest(backup/"backup-manifest.json")
    analyser=run/"analysis.py"
    assert digest(analyser)==digest(Path(__file__).with_name("analyse-split-numerics.py"))
    study=run/"study";protocol=read(study/"protocol.json")
    assert not protocol["smoke"] and protocol["durationMs"]==60000 and protocol["caseCount"]==80
    analysis_hash=digest(study/"cell-analysis.json")
    # Independent copies can be verified concurrently. No experiment is running.
    with ThreadPoolExecutor(max_workers=2) as pool:
        local=pool.submit(audit_copy,run,manifest);remote=pool.submit(audit_copy,backup,manifest)
        spec=importlib.util.spec_from_file_location("frozen_analysis",analyser)
        module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
        with contextlib.redirect_stdout(io.StringIO()):validated=module.analyse(study,write_report=False)
        assert validated==read(study/"cell-analysis.json") and analysis_hash==digest(study/"cell-analysis.json")
        print("All 80 conditions independently revalidated; saved analysis reproduced unchanged.",flush=True)
        local_hashes=local.result();remote.result()
    for row in config["PackageFiles"]:
        assert local_hashes[str(Path(row["Path"]).resolve()).lower()]==row["Sha256"].upper(),row["Path"]
    unchanged=read(run/"unchanged-runtime.json")
    for row in unchanged["files"]:
        assert local_hashes[str((run/"source"/row["path"]).resolve()).lower()]==row["sha256"].upper()
        assert digest(Path(unchanged["comparedTo"])/"source"/row["path"])==row["sha256"].upper()
    # Historical protection checks are explicitly reported as the run's recorded
    # completion check; this review does not claim a new hash of every older save.
    cases=read(study/"results.json")["cases"]
    lookup={c["key"]:c for c in cases};bundle=read(study/"inputs.json")
    def directory(c):return study/c["stage"]/c["key"]
    source_traces={c["key"]:[tuple(float(r[k]) for k in ("timeMs","vs","vd","va")) for r in csv_rows(directory(c)/"trace.csv")] for c in cases if c["kind"]!="replay"}
    source=[]
    for c in validated["sourceComparisons"]:
        a,b=source_traces[c["first"]],source_traces[c["second"]]
        assert len(a)==len(b) and all(x[0]==y[0] for x,y in zip(a,b))
        errors=[max(abs(x[i]-y[i]) for i in (1,2,3)) for x,y in zip(a,b)]
        windows=c["responseWindows"]
        differing=[dict(startMs=windows["edges"][i],endMs=windows["edges"][i+1],firstCount=x,secondCount=y) for i,(x,y) in enumerate(zip(windows["firstCounts"],windows["secondCounts"])) if x!=y]
        source.append(dict(c,prefixMaximumSampledVoltageDifferenceMv={str(end):max(errors[:end//2]) for end in (1000,4000,10000,20000,60000)},differentWindows=differing))
    del source_traces
    # Calcium trajectories, not just their retained peak or rounded final weight.
    trajectories={};final_cases=[]
    for c in cases:
        if c["kind"]!="replay" or c["variant"]!="calcium":continue
        roles={};timeline={}
        for raw in csv_rows(directory(c)/"contacts.csv"):
            role=raw.pop("role");r={k:float(v) for k,v in raw.items()}
            timeline.setdefault(role,[]).append(r)
            if role not in roles or r["inducedDepression"]>roles[role]["maximumInduction"]:
                roles[role]=dict(maximumInduction=r["inducedDepression"],firstSampledMaximumMs=r["timeMs"])
        trajectories[c["key"]]=dict(timeline=timeline,roles=roles)
        if c["step"]==.015625:
            final_cases.append(dict(key=c["key"],sourceId=c["sourceId"],spikeCounts=[len(t) for t in c["spikes"]],
                contacts={role:dict(finalWeight=rows[-1]["weight"],lossPercent=100*(1-rows[-1]["weight"]),
                    induction=roles[role]["maximumInduction"],firstSampledMaximumMs=roles[role]["firstSampledMaximumMs"],
                    peakCalciumUm=next(x["peakCa"] for x in c["contacts"] if x["contactId"]==next(s["contactId"] for s in next(q for q in bundle["replayCases"] if q["id"]==c["sourceId"])["contacts"] if s["role"]==role))) for role,rows in timeline.items()}))
    calcium_comparisons=[]
    for c in validated["replayComparisons"]:
        if c["variant"]!="calcium":continue
        a=next(x for x in cases if x["kind"]=="replay" and x["sourceId"]==c["sourceId"] and x["variant"]=="calcium" and x["step"]==c["coarseStep"])
        b=next(x for x in cases if x["kind"]=="replay" and x["sourceId"]==c["sourceId"] and x["variant"]=="calcium" and x["step"]==c["fineStep"])
        differences={}
        for role,aa in trajectories[a["key"]]["timeline"].items():
            bb=trajectories[b["key"]]["timeline"][role]
            assert len(aa)==len(bb) and all(x["timeMs"]==y["timeMs"] for x,y in zip(aa,bb))
            values={field:max(abs(x[field]-y[field]) for x,y in zip(aa,bb)) for field in ("weight","calcium","integratedCalcium","inducedDepression")}
            fine_induction=trajectories[b["key"]]["roles"][role]["maximumInduction"]
            coarse_induction=trajectories[a["key"]]["roles"][role]["maximumInduction"]
            values["finalInductionRelativeDifferencePercent"]=100*abs(fine_induction-coarse_induction)/fine_induction if fine_induction else 0
            differences[role]=values
        calcium_comparisons.append(dict(sourceId=c["sourceId"],coarseStep=c["coarseStep"],fineStep=c["fineStep"],contacts=differences))
    # Entry blockade preserves the event grid. Disconnecting climbing input
    # removes some global arrival boundaries even for the untaught neuron, so
    # report that comparison rather than assuming exact independence.
    untaught=[]
    for source_case in bundle["replayCases"]:
        cell=1-source_case["taughtCell"]
        for step in (.125,.0625,.03125,.015625):
            group=[c for c in cases if c["kind"]=="replay" and c["sourceId"]==source_case["id"] and c["step"]==step]
            assert len(group)==3
            trains={c["variant"]:c["spikes"][cell] for c in group}
            assert trains["calcium"]==trains["no-entry"]
            a,b=trains["calcium"],trains["disconnected"]
            untaught.append(dict(sourceId=source_case["id"],step=step,cell=cell,count=len(a),entryBlockIdentical=True,
                disconnectedIdentical=a==b,disconnectedCount=len(b),
                disconnectedMaximumOrdinalDifferenceMs=max((abs(x-y) for x,y in zip(a,b)),default=0) if len(a)==len(b) else None))
    audit=dict(captureFilesPerCopy=len(manifest["Files"]),captureBytesPerCopy=sum(x["Bytes"] for x in manifest["Files"]),
        packagedFiles=len(config["PackageFiles"]),unchangedRuntimeFiles=len(unchanged["files"]),originalEvidenceUnchanged=True,
        olderProtectedFilesAtRunCompletion=len(config["ProtectedBackupFiles"]),olderFilesUnchangedAtRunCompletion=status["OldBackupUnchanged"],
        olderProtectedFilesRehashedByThisReview=False,caseCount=validated["caseCount"],baselineMatches=validated["baselineMatches"],
        traceRows=validated["traceRows"],contactRows=validated["contactRows"],eventRows=validated["eventRows"])
    report=dict(reviewedUtc=datetime.now(timezone.utc).isoformat(),run=str(run),revision=status["Revision"],completedUtc=status["CompletedUtc"],audit=audit,
        solverConvergenceQualified=False,physiologicalCalibrationComplete=False,physicalRecoveryAssessed=False,
        interpretation="Fixed-input calcium metrics improve under refinement, while spike timing remains unresolved. The olive has appreciable production coupling error and initial-state sensitivity; the reference itself loses paired long-trajectory agreement. No chaos or physiological qualification is inferred.",
        sourceComparisons=source,replayComparisons=validated["replayComparisons"],calciumTrajectoryComparisons=calcium_comparisons,
        finestCalciumCases=final_cases,sampledInductionMaxima={k:v["roles"] for k,v in trajectories.items()},untaughtCellChecks=untaught)
    output.mkdir(parents=True);save(output/"review.json",report)
    with (output/"source-comparisons.tsv").open("w",newline="",encoding="utf-8") as f:
        w=csv.writer(f,delimiter='\t');w.writerow(["comparison","schedule","first","second","spikes1","spikes2","changedWindows","firstOver1mVms","max4sMv","max10sMv","max60sMv"])
        for c in source:w.writerow([c["comparison"],c["sourceId"],c["first"],c["second"],c["spikes"]["firstCount"],c["spikes"]["secondCount"],c["responseWindows"]["changedCount"],c["firstSampledDifferenceOverOneMvMs"],*[c["prefixMaximumSampledVoltageDifferenceMv"][str(t)] for t in (4000,10000,60000)]])
    print(json.dumps(dict(audit=audit,output=str(output)),indent=2))

if __name__=="__main__":main(Path(sys.argv[1]),Path(sys.argv[2]))

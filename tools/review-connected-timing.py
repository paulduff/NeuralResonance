"""Read-only audit and descriptive refinement review of a completed timing capture."""
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
    assert status["Status"]=="FINISHED" and status["ExitCode"]==0 and status["OldBackupUnchanged"] and not status["LiveBrainAccessed"]
    assert status["Revision"]==config["Revision"]
    for base in (run,Path(manifest["Destination"])):
        for entry in manifest["Files"]:assert digest(base/entry["Path"])==entry["Sha256"].upper(),str(base/entry["Path"])
    assert digest(run/"backup-manifest.json")==digest(Path(manifest["Destination"])/"backup-manifest.json")
    print("Completed C:/D: captures match their manifests.",flush=True)
    for entry in config["PackageFiles"]+config["ProtectedBackupFiles"]:
        assert digest(Path(entry["Path"]))==entry["Sha256"].upper(),entry["Path"]
    unchanged=read(run/"unchanged-physiology.json")
    for entry in unchanged["files"]:
        assert digest(run/"source"/entry["path"])==digest(Path(unchanged["comparedTo"])/"source"/entry["path"])==entry["sha256"]
    print("Frozen package, older saves and unchanged physiology verified.",flush=True)
    analyser=Path(__file__).with_name("analyse-olive-purkinje.py")
    assert digest(run/"analysis.py")==digest(analyser)
    spec=importlib.util.spec_from_file_location("analysis",analyser)
    analysis=importlib.util.module_from_spec(spec);spec.loader.exec_module(analysis)
    study=run/"study";protocol=read(study/"protocol.json");results=read(study/"results.json")
    assert not protocol["smoke"] and not protocol["quick"] and protocol["durationMs"]==60000
    original=digest(study/"cell-analysis.json")
    with contextlib.redirect_stdout(io.StringIO()):validated=analysis.analyse(study,write_report=False)
    assert validated==read(study/"cell-analysis.json") and original==digest(study/"cell-analysis.json")
    print("Full schedules, events, contact/energy accounting and 60 packet comparisons reproduced unchanged.",flush=True)
    cases=validated["cases"];lookup={(c["variant"],c["plan"],c["offset"],c["dt"],c["resolution"]):c for c in cases}
    # Mechanistic ablation must not change the unidirectional olive source.
    for c in cases:
        other=lookup[("calcium",c["plan"],c["offset"],c["dt"],c["resolution"])]
        assert c["sourceTimes"]==other["sourceTimes"]
        if c["plan"] in ("paired20","disconnected"):
            assert c["sourceTimes"]==lookup[(c["variant"],"paired100",c["offset"],c["dt"],c["resolution"])]["sourceTimes"]
    selected=[c for c in cases if c["variant"]=="calcium" and c["dt"]==.125]
    trajectories={};windows={};cells={}
    for c in selected:
        directory=study/c["key"];design=read(directory/"design.json")
        arrivals=sorted({x["message"]["timestampMs"]+x["delayMs"] for x in design["inputs"] if x["role"]=="olive-excitation"})
        times=c["sourceTimes"];edges=[0,*arrivals,60000]
        windows[c["key"]]=dict(edges=edges,counts=[bisect.bisect_left(times,b)-bisect.bisect_left(times,a) for a,b in zip(edges,edges[1:])])
        roles={r:dict(maximumInduction=-1,firstSampledMaximumMs=None) for r in analysis.ROLES}
        with (directory/"contacts.csv").open(newline="") as f:
            for row in csv.DictReader(f):
                role=roles[row["role"]];value=float(row["inducedDepression"])
                if value>role["maximumInduction"]:role.update(maximumInduction=value,firstSampledMaximumMs=float(row["timeMs"]))
        trajectories[c["key"]]=roles
        with (directory/"cells.csv").open(newline="") as f:
            cells[c["key"]]=[{k:float(v) for k,v in r.items()} for r in csv.DictReader(f)]
    comparisons=[]
    for c in selected:
        if c["resolution"]==.03125:continue
        fine=lookup[("calcium",c["plan"],c["offset"],.125,c["resolution"]/2)]
        a,b=windows[c["key"]],windows[fine["key"]];assert a["edges"]==b["edges"]
        differences=[dict(startMs=a["edges"][i],endMs=a["edges"][i+1],coarseCount=x,fineCount=y) for i,(x,y) in enumerate(zip(a["counts"],b["counts"])) if x!=y]
        rows=list(zip(cells[c["key"]],cells[fine["key"]]))
        assert all(x["timeMs"]==y["timeMs"] for x,y in rows)
        first_arrival=a["edges"][1]
        def source_error(x,y):return max(abs(x[k]-y[k]) for k in ("oliveSoma","oliveDendrite","oliveAxon"))
        comparisons.append(dict(plan=c["plan"],offset=c["offset"],coarseStep=c["resolution"],fineStep=fine["resolution"],
            coarseCount=c["oliveAxon"],fineCount=fine["oliveAxon"],changedResponseWindows=len(differences),differentWindows=differences,
            quietMaximumSampledVoltageDifferenceMv=max(source_error(x,y) for x,y in rows if x["timeMs"]<first_arrival),
            firstSampledVoltageDifferenceOverPointOneMs=next((x["timeMs"] for x,y in rows if source_error(x,y)>.1),None),
            firstSampledVoltageDifferenceOverOneMs=next((x["timeMs"] for x,y in rows if source_error(x,y)>1),None)))
    groups=[]
    for variant in protocol["variants"]:
        for plan in protocol["plans"]:
            cs=[c for c in cases if c["variant"]==variant and c["plan"]==plan and c["dt"]==.125 and c["resolution"]==.03125]
            assert len(cs)==2
            groups.append(dict(variant=variant,plan=plan,oliveCounts=[c["oliveAxon"] for c in cs],purkinjeCounts=[c["purkinjeSpikes"] for c in cs],
                contacts={r:dict(meanLossPercent=statistics.mean(100*(1-c["contacts"][r]["weight"]) for c in cs),
                    meanInductionPercent=statistics.mean(100*c["contacts"][r]["inducedDepression"] for c in cs),
                    meanPeakCalciumUm=statistics.mean(c["peakCalcium"][r] for c in cs)) for r in analysis.ROLES}))
    report=dict(reviewedUtc=datetime.now(timezone.utc).isoformat(),run=str(run),revision=config["Revision"],completedUtc=status["CompletedUtc"],
        audit=dict(captureFilesPerCopy=len(manifest["Files"]),packageFiles=len(config["PackageFiles"]),protectedFiles=len(config["ProtectedBackupFiles"]),
            unchangedPhysiologyFiles=len(unchanged["files"]),caseCount=validated["caseCount"],cellRows=validated["cellRows"],contactRows=validated["contactRows"],eventRows=validated["eventRows"],
            exactPacketComparisons=len(validated["stepComparisons"]),originalEvidenceUnchanged=True,
            maximumEnergyBalanceError=max(c["maxEnergyError"] for c in results["cases"]),
            maximumCalciumMassError=max(x["massError"] for c in results["cases"] for x in c["contacts"])),
        packetInvarianceValidated=True,solverConvergenceQualified=False,physiologicalCalibrationComplete=False,physicalRecoveryAssessed=False,
        interpretation="Packet scheduling is reproducible for the tested cadences. Successive solver refinements materially alter source burst counts and do not consistently diminish differences. Similar small active-contact induction is not a substitute for converged source activity. No biological population replication.",
        finestGroups=groups,sourceComparisons=comparisons,resolutionComparisons=validated["resolutionComparisons"],sampledInductionTrajectories=trajectories)
    output.mkdir(parents=True);save(output/"review.json",report)
    with (output/"source-refinement.tsv").open("w",newline="",encoding="utf-8") as f:
        writer=csv.writer(f,delimiter='\t');keys=["plan","offset","coarseStep","fineStep","coarseCount","fineCount","changedResponseWindows","quietMaximumSampledVoltageDifferenceMv","firstSampledVoltageDifferenceOverPointOneMs","firstSampledVoltageDifferenceOverOneMs"]
        writer.writerow(keys)
        for row in comparisons:writer.writerow([row[k] for k in keys])
    print(json.dumps(dict(audit=report["audit"],output=str(output)),indent=2))

if __name__=="__main__":main(Path(sys.argv[1]),Path(sys.argv[2]))

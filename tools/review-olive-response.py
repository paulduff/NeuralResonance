"""Read-only validation and matched response review of a completed package."""
import contextlib
from datetime import datetime, timezone
from hashlib import sha256
import importlib.util
import io
import json
from pathlib import Path
import statistics
import sys

sys.dont_write_bytecode=True
spec=importlib.util.spec_from_file_location("response_analysis",Path(__file__).with_name("analyse-olive-response.py"))
analysis=importlib.util.module_from_spec(spec);spec.loader.exec_module(analysis)
read=lambda p:json.loads(p.read_text(encoding="utf-8-sig"))
digest=lambda p:sha256(p.read_bytes()).hexdigest().upper()


def main(run,output):
    if output.exists() or run==output or run in output.parents:
        raise ValueError("Use a new review directory outside the completed capture.")
    status,config,manifest=(read(run/p) for p in ("window-status.json","configuration.json","backup-manifest.json"))
    assert status["Status"]=="FINISHED" and status["ExitCode"]==0 and status["OldBackupUnchanged"]
    assert status["Revision"]==config["Revision"]
    for base in (run,Path(manifest["Destination"])):
        for entry in manifest["Files"]:assert digest(base/entry["Path"])==entry["Sha256"].upper(),str(base/entry["Path"])
    assert digest(run/"backup-manifest.json")==digest(Path(manifest["Destination"])/"backup-manifest.json")
    for entry in config["PackageFiles"]+config["ProtectedBackupFiles"]:
        assert digest(Path(entry["Path"]))==entry["Sha256"].upper(),entry["Path"]
    runtime=read(run/"unchanged-runtime.json")
    assert runtime["allIdentical"]
    for entry in runtime["files"]:
        assert digest(run/"source"/entry["path"])==digest(Path(runtime["comparedTo"])/"source"/entry["path"])==entry["sha256"]
    study=run/"study";protocol,result=(read(study/p) for p in ("protocol.json","results.json"))
    assert not protocol["smoke"] and not protocol["quick"] and protocol["durationMs"]==60000
    assert len(result["cases"])==config["CaseCount"]==160
    original_report_hash=digest(study/"cell-analysis.json")
    with contextlib.redirect_stdout(io.StringIO()):validated=analysis.analyse(study,write_report=False)
    assert validated==read(study/"cell-analysis.json") and digest(study/"cell-analysis.json")==original_report_hash
    assert validated["identicalStepTraceComparisons"]==validated["stepTraceComparisons"]==80
    cases=result["cases"];lookup={(c["challenge"],c["schedule"],c["offsetMs"],c["dt"]):c for c in cases}
    maximum_latency_difference=0
    for identity,c in lookup.items():
        if c["dt"]!=1:continue
        fine=lookup[identity[:3]+(.125,)]
        for name in ("somaticSpikes","axonalSpikes","outsideSoma","outsideAxon"):
            assert c[name]==fine[name]
        for coarse_response,fine_response in zip(c["responses"],fine["responses"]):
            for name in ("soma","axon","precedingSoma","precedingAxon"):
                assert coarse_response[name]==fine_response[name]
            for name in ("firstSomaLatencyMs","firstAxonLatencyMs"):
                a,b=coarse_response[name],fine_response[name]
                assert (a is None)==(b is None)
                if a is not None:
                    assert 0<=a-b<=.875
                    maximum_latency_difference=max(maximum_latency_difference,a-b)
    # Use only the finer step in biological summaries; the other step is a
    # numerical repeat, not another biological observation.
    fine_cases=[c for c in cases if c["dt"]==.125]
    groups=[];off_window=[];wider={}
    for c in fine_cases:
        events=read(study/(c["key"]+"-events.json"))
        for event in events:
            if any(p["timeMs"]<event["timeMs"]<=p["timeMs"]+250 for p in c["pulses"]):continue
            earlier=[p for p in c["pulses"] if p["timeMs"]<event["timeMs"]]
            preceding=max(earlier,key=lambda p:p["timeMs"]) if earlier else None
            off_window.append(dict(challenge=c["challenge"],schedule=c["schedule"],offsetMs=c["offsetMs"],**event,
                msSincePreviousProbe=event["timeMs"]-preceding["timeMs"] if preceding else None,
                epoch="before" if event["timeMs"]<=10000 else "during" if event["timeMs"]<=30000 else "after-early" if event["timeMs"]<=40000 else "after-late"))
        expanded=[]
        for p in c["pulses"]:
            assert p["timeMs"]+500<=60000
            ev=[e for e in events if p["timeMs"]<e["timeMs"]<=p["timeMs"]+500]
            expanded.append(dict(phase=p["phase"],soma=sum(e["soma"] for e in ev),axon=sum(e["axon"] for e in ev),
                firstAxonLatencyMs=next((e["timeMs"]-p["timeMs"] for e in ev if e["axon"]>0),None),
                precedingSoma=0,precedingAxon=0))
        assert all(a["timeMs"]+500<b["timeMs"] for a,b in zip(c["pulses"],c["pulses"][1:]))
        wider[c["key"]]=expanded
    for schedule in protocol["schedules"]:
        for challenge in protocol["challenges"]:
            selected=[c for c in fine_cases if c["challenge"]==challenge and c["schedule"]==schedule]
            assert len(selected)==8
            sham=[c for c in fine_cases if c["challenge"]=="sham" and c["schedule"]==schedule]
            phases={};sensitivity={};differences={}
            for phase in analysis.PHASES:
                records=[p for c in selected for p in c["responses"] if p["phase"]==phase]
                phases[phase]=analysis.stats(records)
                control=analysis.stats([p for c in sham for p in c["responses"] if p["phase"]==phase])
                differences[phase]={name:phases[phase][name]-control[name] if phases[phase][name] is not None else None
                    for name in ("somaticHitFraction","axonalHitFraction","meanSomaticSpikes","meanAxonalSpikes","medianAxonalLatencyMs")}
                # Post-hoc window sensitivity, not a replacement for the primary
                # 250 ms definition. Preceding-spike fields are not used here.
                expanded=analysis.stats([p for c in selected for p in wider[c["key"]] if p["phase"]==phase])
                sensitivity[phase]={name:expanded[name] for name in ("probes","somaticHits","axonalHits","meanSomaticSpikes","meanAxonalSpikes")}
            local_off=[e for e in off_window if e["challenge"]==challenge and e["schedule"]==schedule]
            groups.append(dict(challenge=challenge,schedule=schedule,offsets=8,phases=phases,matchedShamDifference=differences,
                postHoc500msWindow=sensitivity,offWindowSoma=sum(e["soma"] for e in local_off),offWindowAxon=sum(e["axon"] for e in local_off),
                latestOffWindowMs=max((e["timeMs"] for e in local_off),default=None),minimumReserve=min(c["minimumReserve"] for c in selected),
                finalReserveRange=[min(c["finalReserve"] for c in selected),max(c["finalReserve"] for c in selected)],unmet=sum(c["unmet"] for c in selected)))
    # A summary table includes denominators; no confidence intervals from these
    # deterministic offsets and no automatic connected-system qualification.
    lines=["challenge\tschedule\tphase\tprobes\tsomatic_hits\taxonal_hits\tmean_somatic_spikes\tmean_axonal_spikes\tmedian_axonal_latency_ms"]
    for group in groups:
        for phase,p in group["phases"].items():
            lines.append('\t'.join(str(v) for v in [group["challenge"],group["schedule"],phase,p["probes"],p["somaticHits"],p["axonalHits"],p["meanSomaticSpikes"],p["meanAxonalSpikes"],p["medianAxonalLatencyMs"]]))
    report=dict(reviewedUtc=datetime.now(timezone.utc).isoformat(),run=str(run),revision=config["Revision"],completedUtc=status["CompletedUtc"],
        completed=True,captureFilesVerifiedAtBothLocations=len(manifest["Files"]),packageFilesVerified=len(config["PackageFiles"]),
        protectedFilesVerified=len(config["ProtectedBackupFiles"]),unchangedRuntimeFilesVerified=len(runtime["files"]),
        conditionsVerified=160,traceRowsVerified=validated["traceRowsVerified"],identicalStepTraceComparisons=80,
        matchingResponseCountsAcrossSteps=True,maximumLatencyQuantizationDifferenceMs=maximum_latency_difference,
        maximumReportedEnergyBalanceError=max(c["maximumError"] for c in cases),
        primaryResponseWindowMs=250,postHocSensitivityWindowMs=500,summaryStepMs=.125,
        totalOffWindowSomaticSpikes=sum(e["soma"] for e in off_window),totalOffWindowAxonalSpikes=sum(e["axon"] for e in off_window),
        latestOffWindowSpikeMs=max((e["timeMs"] for e in off_window),default=None),
        lateResponseAxonalFractionRange=[min(g["phases"]["after-late"]["axonalHitFraction"] for g in groups),max(g["phases"]["after-late"]["axonalHitFraction"] for g in groups)],
        physiologicalCalibrationComplete=False,connectedTeachingAssessed=False,physicalRecoveryAssessed=False,
        interpretation="Late responses return close to matched sham in these isolated conditions. Inhibition reduces burst output without a monotonic reduction in hit probability. Brief delayed/window-splitting spikes remain visible; test their actual teaching effects in an isolated circuit. Offsets are deterministic and not independent biological subjects.",
        groups=groups,offWindowEvents=off_window)
    output.mkdir(parents=True)
    (output/"review.json").write_text(json.dumps(report,indent=2,allow_nan=False)+"\n",encoding="utf-8")
    (output/"response-summary.tsv").write_text('\n'.join(lines)+'\n',encoding="utf-8")
    print(json.dumps({k:v for k,v in report.items() if k not in ("groups","offWindowEvents")},indent=2))


if __name__=="__main__":main(Path(sys.argv[1]).resolve(),Path(sys.argv[2]).resolve())

"""Audit completed olivary evidence without changing the captured experiment."""
import csv
from datetime import datetime, timezone
from hashlib import sha256
import json
import math
from pathlib import Path
import statistics
import sys

read=lambda p:json.loads(p.read_text(encoding="utf-8-sig"))
digest=lambda p:sha256(p.read_bytes()).hexdigest().upper()


def oscillation_cycles(rows, events):
    peaks=[i for i in range(1,len(rows)-1) if rows[i-1][1]<rows[i][1]>=rows[i+1][1]]
    cycles=[]
    for a,b in zip(peaks,peaks[1:]):
        start,end=rows[a][0],rows[b][0]
        cycles.append(dict(startMs=start,endMs=end,periodMs=end-start,
            amplitudeMv=rows[a][1]-min(r[1] for r in rows[a:b+1]),
            hasSpike=any(start<t<=end for t in events)))
    return cycles


def main(run,output):
    if output.exists():raise FileExistsError("Use a new review directory; completed evidence is immutable.")
    status,config,manifest=(read(run/p) for p in ("window-status.json","configuration.json","backup-manifest.json"))
    assert status["Status"]=="FINISHED" and status["ExitCode"]==0 and status["OldBackupUnchanged"]
    assert status["Revision"]==config["Revision"]
    backup=Path(manifest["Destination"])
    for base in (run,backup):
        for entry in manifest["Files"]:assert digest(base/entry["Path"])==entry["Sha256"].upper(),str(base/entry["Path"])
    assert digest(run/"backup-manifest.json")==digest(backup/"backup-manifest.json")
    for entry in config["PackageFiles"]+config["ProtectedBackupFiles"]:
        assert digest(Path(entry["Path"]))==entry["Sha256"].upper(),entry["Path"]
    study=run/"study"
    protocol,result,analysis=(read(study/p) for p in ("protocol.json","results.json","cell-analysis.json"))
    assert result["completed"] and analysis["accountingValidated"]
    assert not protocol["smoke"] and not protocol["pilot"] and protocol["durationMilliseconds"]==60000
    assert len(result["cases"])==protocol["caseCount"]==config["CaseCount"]==180
    expected={(v["name"],p,dt) for v in protocol["variants"] for p in protocol["protocols"] for dt in protocol["stepsMilliseconds"]}
    assert expected=={(c["variant"],c["protocol"],c["dt"]) for c in result["cases"]}
    summaries=[];row_count=0;maximum_error=0;trace_signatures={}
    for case in result["cases"]:
        key=case["key"]
        assert read(study/(key+"-summary.json"))==case
        events=read(study/(key+"-events.json"))
        assert sum(e["soma"] for e in events)==case["somaticSpikes"]
        assert sum(e["axon"] for e in events)==case["axonalSpikes"]
        assert all(a["timeMs"]<b["timeMs"] for a,b in zip(events,events[1:]))
        assert all(0<e["timeMs"]<=60000 and e["soma"] in (0,1) and e["axon"] in (0,1) and e["soma"]+e["axon"]>0 for e in events)
        for i,segment in enumerate(case["segments"]):
            assert segment["endMs"]==(i+1)*10000
            for field in ("soma","axon"):
                assert segment[field]==sum(e[field] for e in events if i*10000<e["timeMs"]<=(i+1)*10000)
        assert len(case["segments"])==6
        # Independently count the scheduled inputs, including the cut-off pair.
        name=case["protocol"];exc=inh=0
        if name=="excitation":exc=200
        if name.startswith("volley-"):exc=200*int(name[7:])
        if name=="inhibition":inh=1000
        if name.startswith("pair-"):
            delay=int(name[5:]);inh=len(range(10000,40000,997));exc=len(range(10000+delay,40000,997))
        assert (exc,inh)==(case["excEvents"],case["inhEvents"])
        rows=[];previous=None
        trace_path=study/(key+".csv")
        trace_signatures[(case["variant"],name,case["dt"])]=digest(trace_path)
        with trace_path.open(encoding="utf-8-sig",newline="") as handle:
            reader=csv.reader(handle);header=next(reader)
            assert header==["timeMs","somaMv","dendriteMv","axonMv","calciumUm","hcn","catActivation","catAvailability","kca","reserve","produced","requested","consumed","unmet","overflow"]
            for i,raw in enumerate(reader,1):
                r=list(map(float,raw));assert len(r)==15 and all(math.isfinite(v) for v in r)
                assert r[0]==i*2 and all(0<=r[j]<=1 for j in range(5,10)) and r[4]>=0
                error=abs(r[9]-(1+r[10]-r[12]-r[14]));maximum_error=max(maximum_error,error)
                assert error<1e-8 and abs(r[11]-r[12]-r[13])<1e-8
                assert all(r[j]>=0 and (previous is None or r[j]>=previous[j]) for j in range(10,15))
                phase=case["segments"][min(5,int((r[0]-case["dt"])/10000))]
                assert phase["minimumSoma"]-1e-9<=r[1]<=phase["maximumSoma"]+1e-9
                assert phase["minReserve"]-1e-9<=r[9]<=1
                rows.append(r);previous=r
        assert len(rows)==30000 and rows[-1][1]==case["finalVoltage"] and rows[-1][9]==case["finalReserve"]
        assert abs(rows[-1][13]-case["unmet"])<1e-12
        row_count+=len(rows)
        spike_times=[e["timeMs"] for e in events if e["soma"] or e["axon"]]
        cycles=oscillation_cycles(rows,spike_times)
        phases=[]
        for i,segment in enumerate(case["segments"]):
            phase_rows=rows[i*5000:(i+1)*5000]
            phase_cycles=[c for c in cycles if i*10000<c["startMs"] and c["endMs"]<=(i+1)*10000 and not c["hasSpike"] and c["amplitudeMv"]>.1]
            periods=[c["periodMs"] for c in phase_cycles]
            phases.append(dict(endMs=(i+1)*10000,somaticHz=segment["soma"]/10,axonalHz=segment["axon"]/10,
                meanSomaMv=statistics.fmean(r[1] for r in phase_rows),sampledSomaMin=min(r[1] for r in phase_rows),sampledSomaMax=max(r[1] for r in phase_rows),
                nonspikingPeakRateHz=1000/statistics.median(periods) if len(periods)>=3 else None,
                medianNonspikingCycleAmplitudeMv=statistics.median(c["amplitudeMv"] for c in phase_cycles) if phase_cycles else None,
                minimumReserve=segment["minReserve"],endReserve=phase_rows[-1][9]))
        # Quantify restored oscillations; this does NOT establish restored evoked
        # spikes, since no further strong input was sent after these challenges.
        release=20000 if name in ("excitation","volley-4","volley-16") else 30000 if name in ("inhibition","glucose","oxygen") else 40000 if name.startswith("pair-") else None
        baseline=[c for c in cycles if 2000<c["startMs"] and c["endMs"]<=10000 and not c["hasSpike"] and c["amplitudeMv"]>.1]
        recovery=None
        if release is not None and baseline:
            amplitude=statistics.median(c["amplitudeMv"] for c in baseline);period=statistics.median(c["periodMs"] for c in baseline)
            after=[c for c in cycles if c["startMs"]>=release]
            for i in range(len(after)-4):
                group=after[i:i+5]
                if all(not c["hasSpike"] and abs(c["amplitudeMv"]/amplitude-1)<=.1 and abs(c["periodMs"]/period-1)<=.05 for c in group):
                    recovery=group[-1]["endMs"]-release;break
        summaries.append(dict(key=key,variant=case["variant"],protocol=name,dt=case["dt"],energyEnabled=case["energyEnabled"],
            totalSomaticSpikes=case["somaticSpikes"],totalAxonalSpikes=case["axonalSpikes"],excitatoryContactEvents=exc,inhibitoryContactEvents=inh,
            phases=phases,minimumReserve=min(s["minReserve"] for s in case["segments"]),finalReserve=case["finalReserve"],unmet=case["unmet"],
            fiveStableCyclesAfterReleaseMs=recovery))
    lookup={(c["variant"],c["protocol"],c["dt"]):c for c in summaries}
    maximum_spike_gap=max(abs(lookup[v,p,dt][field]-lookup[v,p,.125][field]) for v,p,dt in expected for field in ("totalSomaticSpikes","totalAxonalSpikes"))
    equal_traces=sum(trace_signatures[v,p,dt]==trace_signatures[v,p,.125] for v,p,dt in expected if dt!=.125)
    report=dict(reviewedUtc=datetime.now(timezone.utc).isoformat(),run=str(run),revision=config["Revision"],completedUtc=status["CompletedUtc"],
        completed=True,captureFilesVerifiedAtBothLocations=len(manifest["Files"]),packageFilesVerified=len(config["PackageFiles"]),
        protectedFilesVerified=len(config["ProtectedBackupFiles"]),conditionsVerified=180,traceRowsVerified=row_count,
        maximumTraceEnergyBalanceError=maximum_error,maximumReportedEnergyBalanceError=max(c["maxBalance"] for c in result["cases"]),
        maximumTotalSpikeCountDifference=maximum_spike_gap,identicalTraceComparisons=equal_traces,totalTraceComparisons=120,
        physiologyFullyQualified=False,physicalRecoveryAssessed=False,
        peakRateCaution="Peak rates during stimulation include evoked voltage deflections; only unstimulated, nonspiking windows support interpretation as intrinsic STO frequency.",
        recoveryDefinition="First completion of five consecutive nonspiking cycles after input release/supply restoration with amplitude within 10% and period within 5% of the 2-10s baseline. This is oscillation recovery, not an evoked-response recovery test.",
        pairedInputCaution="The 40s excitation cut-off leaves 31 inhibitory releases in all pair cases, but 31 excitatory releases at 50ms and 30 at 100/150ms. A matched pair comparison must correct this endpoint difference.",
        conditions=summaries)
    output.mkdir(parents=True)
    (output/"review.json").write_text(json.dumps(report,indent=2,allow_nan=False)+"\n",encoding="utf-8")
    print(json.dumps({k:v for k,v in report.items() if k!="conditions"},indent=2))


if __name__=="__main__":main(Path(sys.argv[1]).resolve(),Path(sys.argv[2]).resolve())

"""Validate every saved trace/event and report physiology separately from execution."""
import csv
import json
import math
from pathlib import Path
import statistics
import sys


def analyse(root):
    read=lambda p: json.loads(p.read_text(encoding="utf-8-sig"))
    protocol,result=read(root/"protocol.json"),read(root/"results.json")
    assert result["completed"] and len(result["cases"])==protocol["caseCount"]
    duration=protocol["durationMilliseconds"]
    expected={(v["name"],p,dt) for v in protocol["variants"] for p in protocol["protocols"] for dt in protocol["stepsMilliseconds"]}
    observed={(c["variant"],c["protocol"],c["dt"]) for c in result["cases"]}
    assert expected==observed and len(expected)==len(result["cases"])
    reports=[]
    for case in result["cases"]:
        key=case["key"]
        assert read(root/(key+"-summary.json"))==case
        events=read(root/(key+"-events.json"))
        assert sum(e["soma"] for e in events)==sum(s["soma"] for s in case["segments"])==case["somaticSpikes"]
        assert sum(e["axon"] for e in events)==sum(s["axon"] for s in case["segments"])==case["axonalSpikes"]
        previous_time=0
        for event in events:
            assert previous_time<event["timeMs"]<=duration
            assert event["soma"] in (0,1) and event["axon"] in (0,1) and event["soma"]+event["axon"]>0
            assert abs(event["timeMs"]/case["dt"]-round(event["timeMs"]/case["dt"]))<1e-6
            previous_time=event["timeMs"]
        traces=[];previous=None;ledger_max=0
        with (root/(key+".csv")).open(encoding="utf-8-sig",newline="") as handle:
            for i,raw in enumerate(csv.DictReader(handle),1):
                row={k:float(v) for k,v in raw.items()}
                assert all(math.isfinite(v) for v in row.values()),key
                assert row["timeMs"]==i*protocol["tracePeriodMilliseconds"]
                assert all(0<=row[g]<=1 for g in ("hcn","catActivation","catAvailability","kca","reserve"))
                assert row["calciumUm"]>=0
                # Broad numerical range is not a physiological pass criterion.
                assert all(-150<row[v]<150 for v in ("somaMv","dendriteMv","axonMv"))
                balance=abs(row["reserve"]-(1+row["produced"]-row["consumed"]-row["overflow"]))
                assert balance<1e-8 and abs(row["requested"]-row["consumed"]-row["unmet"])<1e-8,key
                ledger_max=max(ledger_max,balance)
                for name in ("produced","requested","consumed","unmet","overflow"):
                    assert row[name]>=0 and (previous is None or row[name]>=previous[name]),key
                traces.append(row);previous=row
        assert len(traces)==duration/protocol["tracePeriodMilliseconds"]
        assert abs(traces[-1]["somaMv"]-case["finalVoltage"])<1e-10
        assert abs(traces[-1]["reserve"]-case["finalReserve"])<1e-10
        assert ledger_max<=case["maxBalance"]+1e-10 and case["maxBalance"]<1e-8
        # Independently count scheduled incoming contact events.
        start,stop=(1000,2000) if protocol["pilot"] else (10000,20000)
        name=case["protocol"];exc=inh=0
        for t in range(duration):
            if (name=="excitation" or name.startswith("volley-")) and start<=t<stop and (t-start)%50==0:
                exc+=int(name[7:]) if name.startswith("volley-") else 1
            offset=0 if protocol["pilot"] else 10000
            if name=="inhibition" and start+offset<=t<stop+offset and (t-start)%10==0:inh+=1
            if name.startswith("pair-"):
                delay=int(name[5:])
                if 10000<=t<40000 and (t-10000)%997==0:inh+=1
                if 10000+delay<=t<40000 and (t-10000-delay)%997==0:exc+=1
        assert (exc,inh)==(case["excEvents"],case["inhEvents"]),key
        # Peaks are an exploratory STO estimate, only for a non-spiking late
        # quiet trace. Never confuse these slow voltage waves with AP counts.
        late=[r for r in traces if r["timeMs"]>=max(1000,duration-10000)]
        peaks=[late[i]["timeMs"] for i in range(1,len(late)-1)
            if late[i-1]["somaMv"]<late[i]["somaMv"]>=late[i+1]["somaMv"]]
        periods=[b-a for a,b in zip(peaks,peaks[1:])]
        late_events=[e for e in events if e["timeMs"]>max(1000,duration-10000)]
        sto=1000/statistics.median(periods) if name=="quiet" and not late_events and len(periods)>=3 else None
        reports.append(dict(key=key,variant=case["variant"],protocol=name,dt=case["dt"],somaticSpikes=case["somaticSpikes"],
            axonalSpikes=case["axonalSpikes"],quietStoHz=sto,lateSomaRange=[min((r["somaMv"] for r in late),default=None),max((r["somaMv"] for r in late),default=None)],
            finalReserve=case["finalReserve"],unmet=case["unmet"],maximumLedgerError=case["maxBalance"]))
    by={(r["variant"],r["protocol"],r["dt"]):r for r in reports}
    convergence=[]
    for variant,name,dt in sorted(expected):
        if dt==.125:continue
        a,b=by[(variant,name,dt)],by[(variant,name,.125)]
        convergence.append(dict(variant=variant,protocol=name,coarseStep=dt,
            somaticSpikeDifference=a["somaticSpikes"]-b["somaticSpikes"],axonalSpikeDifference=a["axonalSpikes"]-b["axonalSpikes"],
            reserveDifference=a["finalReserve"]-b["finalReserve"]))
    checks={}
    if not protocol["smoke"]:
        for dt in protocol["stepsMilliseconds"]:
            quiet=by[("production","quiet",dt)]
            checks[str(dt)]=dict(sparseQuietSoma=quiet["somaticSpikes"]/(duration/1000)<=2,
                quietStoInBroadReferenceBand=quiet["quietStoHz"] is not None and 1<=quiet["quietStoHz"]<=12,
                evokedSomaticAndAxonalResponse=by[("production","volley-4",dt)]["somaticSpikes"]>0 and by[("production","volley-4",dt)]["axonalSpikes"]>0)
    report=dict(completed=True,accountingValidated=True,physiologicallyQualified=False,
        reason="Execution checks are separate from biological fit. Review inhibition/recovery, phase dependence, step sensitivity and reference comparisons before connected use; gap junctions and in-vivo firing are untested.",
        exploratoryChecks=checks,cases=reports,convergence=convergence)
    (root/"cell-analysis.json").write_text(json.dumps(report,indent=2,allow_nan=False)+"\n",encoding="utf-8")
    print(json.dumps(dict(completed=True,accountingValidated=True,cases=len(reports),exploratoryChecks=checks,physiologicallyQualified=False),indent=2))
    return report


if __name__=="__main__":analyse(Path(sys.argv[1]))

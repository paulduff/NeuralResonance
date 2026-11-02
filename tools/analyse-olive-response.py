"""Validate stimulus matching and response attribution; physiological review is separate."""
import csv
from hashlib import sha256
import json
import math
from pathlib import Path
import statistics
import sys

PHASES=("before","during","after-early","after-middle","after-late")
read=lambda p:json.loads(p.read_text(encoding="utf-8-sig"))


def calendar(smoke,offset,schedule):
    if smoke:
        times=[(1000+offset,"before")]+([(2000+offset,"during")] if schedule=="throughout" else [])+[(3000+offset,"after-early")]
    else:
        times=[(2000+offset+i*997,"before") for i in range(8)]
        if schedule=="throughout":times += [(10000+offset+i*997,"during") for i in range(20)]
        times += [(30000+offset+i*997,PHASES[2+i//10]) for i in range(30)]
    return [dict(index=i,timeMs=t,phase=phase) for i,(t,phase) in enumerate(times)]


def stats(responses):
    n=len(responses)
    return dict(probes=n,somaticHits=sum(r["soma"]>0 for r in responses),axonalHits=sum(r["axon"]>0 for r in responses),
        somaticHitFraction=sum(r["soma"]>0 for r in responses)/n if n else None,
        axonalHitFraction=sum(r["axon"]>0 for r in responses)/n if n else None,
        meanSomaticSpikes=sum(r["soma"] for r in responses)/n if n else None,
        meanAxonalSpikes=sum(r["axon"] for r in responses)/n if n else None,
        meanAxonalSpikesWhenResponsive=statistics.fmean(r["axon"] for r in responses if r["axon"]>0) if any(r["axon"]>0 for r in responses) else None,
        medianAxonalLatencyMs=statistics.median(r["firstAxonLatencyMs"] for r in responses if r["firstAxonLatencyMs"] is not None) if any(r["firstAxonLatencyMs"] is not None for r in responses) else None,
        precedingSomaticSpikes=sum(r["precedingSoma"] for r in responses),precedingAxonalSpikes=sum(r["precedingAxon"] for r in responses))


def analyse(root):
    protocol,result=read(root/"protocol.json"),read(root/"results.json")
    duration,start,end=(protocol[k] for k in ("durationMs","challengeStartMs","challengeEndMs"))
    smoke=protocol["smoke"]
    assert protocol["challenges"]==["sham","inhibition-1","inhibition-4","glucose","oxygen"]
    assert protocol["schedules"]==["throughout","before-after"] and protocol["stepsMs"]==[1,.125]
    assert protocol["offsetsMs"]==([0] if protocol["quick"] else list(range(0,106,15)))
    assert not protocol["quick"] or smoke
    plan=read(root/"schedule-check.json")
    assert len(plan)==16 and {(r["offsetMs"],r["schedule"]) for r in plan}=={(o,s) for o in range(0,106,15) for s in protocol["schedules"]}
    for row in plan:
        assert row["pulses"]==calendar(False,row["offsetMs"],row["schedule"])
        assert row["smokePulses"]==calendar(True,row["offsetMs"],row["schedule"])
        assert len(row["pulses"])==(58 if row["schedule"]=="throughout" else 38)
        assert all(a["timeMs"]+250<b["timeMs"] for a,b in zip(row["pulses"],row["pulses"][1:]))
        assert row["pulses"][-1]["timeMs"]+250<=60000
    assert (duration,start,end)==((4000,2000,3000) if smoke else (60000,10000,30000))
    assert protocol["responseWindowMs"]==250 and protocol["excitatoryContacts"]==4 and protocol["quanta"]==1
    assert protocol["inhibitionPeriodMs"]==10 and protocol["tracePeriodMs"]==2
    assert protocol["dynamics"]=="substrate-energy-olivary-v4" and protocol["membrane"]=="olivary-three-compartment-v1"
    assert protocol["parameters"]==dict(lowCalcium=1.1,highCalcium=4.5,hcn=.12,calciumPotassium=35,axonalPotassium=240)
    expected={(c,s,o,dt) for c in protocol["challenges"] for s in protocol["schedules"] for o in protocol["offsetsMs"] for dt in protocol["stepsMs"]}
    assert result["completed"] and len(result["cases"])==protocol["caseCount"]==len(expected)
    observed=set();summaries=[];prefixes={};trace_hashes={};row_count=0
    for case in result["cases"]:
        key=case["key"];identity=(case["challenge"],case["schedule"],case["offsetMs"],case["dt"])
        assert identity not in observed;observed.add(identity)
        assert read(root/(key+"-summary.json"))==case
        pulses=calendar(smoke,case["offsetMs"],case["schedule"])
        assert pulses==case["pulses"] and len(pulses)==len(case["responses"])
        assert all(p["timeMs"]+250<=duration for p in pulses)
        assert all(a["timeMs"]+250<b["timeMs"] for a,b in zip(pulses,pulses[1:]))
        for p in pulses:
            if p["phase"]=="before":assert p["timeMs"]+250<=start
            elif p["phase"]=="during":assert start<=p["timeMs"] and p["timeMs"]+250<=end
            else:assert p["timeMs"]>=end
        inputs=[dict(timeMs=p["timeMs"],transmitter="glutamate",contacts=4) for p in pulses]
        if case["challenge"].startswith("inhibition-"):
            inputs += [dict(timeMs=t,transmitter="gaba",contacts=int(case["challenge"][11:])) for t in range(start,end,10)]
        inputs.sort(key=lambda r:(r["timeMs"],r["transmitter"]=="gaba"))
        assert read(root/(key+"-inputs.json"))==inputs,key
        assert case["excEvents"]==len(pulses)*4
        assert case["inhEvents"]==sum(r["contacts"] for r in inputs if r["transmitter"]=="gaba")
        events=read(root/(key+"-events.json"))
        assert all(0<e["timeMs"]<=duration and e["soma"] in (0,1) and e["axon"] in (0,1) and e["soma"]+e["axon"]>0 for e in events)
        assert all(a["timeMs"]<b["timeMs"] for a,b in zip(events,events[1:]))
        assert all(abs(e["timeMs"]/case["dt"]-round(e["timeMs"]/case["dt"]))<1e-8 for e in events)
        assert sum(e["soma"] for e in events)==case["somaticSpikes"] and sum(e["axon"] for e in events)==case["axonalSpikes"]
        for p,r in zip(pulses,case["responses"]):
            assert all(p[k]==r[k] for k in ("index","timeMs","phase")) and r["endMs"]==p["timeMs"]+250
            window=[e for e in events if p["timeMs"]<e["timeMs"]<=r["endMs"]]
            preceding=[e for e in events if p["timeMs"]-250<e["timeMs"]<=p["timeMs"]]
            for field,title in (("soma","Soma"),("axon","Axon")):
                assert r[field]==sum(e[field] for e in window)
                assert r["preceding"+title]==sum(e[field] for e in preceding)
                latency=next((e["timeMs"]-p["timeMs"] for e in window if e[field]>0),None)
                assert r["first"+title+"LatencyMs"]==latency
            assert all(math.isfinite(v) for v in r["pre"].values()) and 0<=r["pre"]["reserve"]<=1 and r["pre"]["calciumUm"]>=0
        outside=[e for e in events if not any(p["timeMs"]<e["timeMs"]<=p["timeMs"]+250 for p in pulses)]
        assert case["outsideSoma"]==sum(e["soma"] for e in outside) and case["outsideAxon"]==sum(e["axon"] for e in outside)
        previous=None;count=0;prefix=sha256();full=sha256();before_by_time={r["timeMs"]:r["pre"] for r in case["responses"]}
        with (root/(key+".csv")).open(encoding="utf-8-sig",newline="") as handle:
            header=handle.readline();full.update(header.encode());prefix.update(header.encode())
            assert header.strip().split(',')==["timeMs","somaMv","dendriteMv","axonMv","calciumUm","hcn","catActivation","catAvailability","kca","reserve","produced","requested","consumed","unmet","overflow","glucose","oxygen","perfusion"]
            for line in handle:
                count+=1;r=list(map(float,next(csv.reader([line]))))
                assert len(r)==18 and all(math.isfinite(v) for v in r) and r[0]==count*2
                assert all(0<=r[j]<=1 for j in range(5,10)) and r[4]>=0 and all(-150<r[j]<150 for j in range(1,4))
                assert abs(r[9]-(1+r[10]-r[12]-r[14]))<1e-8 and abs(r[11]-r[12]-r[13])<1e-8
                assert all(r[j]>=0 and (previous is None or r[j]>=previous[j]) for j in range(10,15))
                challenged=start<=r[0]-case["dt"]<end
                assert r[15:]==[0.0 if case["challenge"]=="glucose" and challenged else 1.0,0.0 if case["challenge"]=="oxygen" and challenged else 1.0,1.0]
                if r[0] in before_by_time:
                    pre=before_by_time[r[0]]
                    assert [pre[k] for k in ("somaMv","dendriteMv","axonMv","calciumUm","reserve")]==[r[1],r[2],r[3],r[4],r[9]]
                assert case["minimumReserve"]-1e-9<=r[9]
                full.update(line.encode())
                if r[0]<=start:prefix.update(line.encode())
                previous=r
        assert count==duration/2 and previous[1]==case["finalSomaMv"] and previous[9]==case["finalReserve"] and previous[13]==case["unmet"]
        assert 0<=case["maximumError"]<1e-8
        row_count+=count
        # Baseline is identical across challenges, schedules and host steps at
        # each onset offset. This is a matched intervention, not a new cell fit.
        if case["offsetMs"] in prefixes:assert prefixes[case["offsetMs"]]==prefix.hexdigest(),key
        else:prefixes[case["offsetMs"]]=prefix.hexdigest()
        trace_hashes[identity]=full.hexdigest()
        phases={phase:stats([r for r in case["responses"] if r["phase"]==phase]) for phase in PHASES}
        summaries.append(dict(key=key,challenge=case["challenge"],schedule=case["schedule"],offsetMs=case["offsetMs"],dt=case["dt"],phases=phases,
            outsideSoma=case["outsideSoma"],outsideAxon=case["outsideAxon"],minimumReserve=case["minimumReserve"],finalReserve=case["finalReserve"],unmet=case["unmet"]))
    assert observed==expected
    lookup={(c["challenge"],c["schedule"],c["offsetMs"],c["dt"]):c for c in summaries}
    matches=[]
    for identity,c in lookup.items():
        challenge,schedule,offset,dt=identity
        sham=lookup[("sham",schedule,offset,dt)]
        assert c["phases"]["before"]==sham["phases"]["before"]
        matches.append(dict(challenge=challenge,schedule=schedule,offsetMs=offset,dt=dt,phaseDifferences={phase:{
            "somaticHits":c["phases"][phase]["somaticHits"]-sham["phases"][phase]["somaticHits"],
            "axonalHits":c["phases"][phase]["axonalHits"]-sham["phases"][phase]["axonalHits"],
            "meanAxonalSpikes":c["phases"][phase]["meanAxonalSpikes"]-sham["phases"][phase]["meanAxonalSpikes"] if c["phases"][phase]["probes"] else None} for phase in PHASES}))
    equal=sum(trace_hashes[c,s,o,1]==trace_hashes[c,s,o,.125] for c,s,o,dt in expected if dt==1)
    report=dict(completed=True,accountingValidated=True,physiologicallyQualified=False,traceRowsVerified=row_count,
        matchedBaselineVerified=True,identicalStepTraceComparisons=equal,stepTraceComparisons=len(expected)//2,
        interpretation="Response windows, matched sham and off-window events require review. These deterministic offsets are not independent biological samples. No automatic physiological or connected-brain qualification.",cases=summaries,matchedSham=matches)
    (root/"cell-analysis.json").write_text(json.dumps(report,indent=2,allow_nan=False)+"\n",encoding="utf-8")
    print(json.dumps({k:v for k,v in report.items() if k not in ("cases","matchedSham")},indent=2));return report


if __name__=="__main__":analyse(Path(sys.argv[1]))

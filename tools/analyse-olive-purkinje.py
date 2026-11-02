"""Audit complete circuit events and chemical/energy ledgers; do not certify biology."""
import csv
import json
import math
from pathlib import Path
import sys
from hashlib import sha256

read=lambda p:json.loads(p.read_text(encoding="utf-8-sig"))
VARIANTS=["legacy","calcium","no-entry","no-release"]
PLANS=["paired100","paired20","reversed100","early500","disconnected","quiet-olive","inhibition","glucose"]
ROLES=["active","late","dormant","other-cell","nonparallel"]

def close(a,b,tol=1e-7):
    assert math.isfinite(a) and math.isfinite(b) and abs(a-b)<=tol,(a,b)

def analyse(root,write_report=True):
    root=Path(root);p=read(root/"protocol.json");results=read(root/"results.json")
    timing=p["study"]=="connected-timing"
    variants=["calcium","no-entry"] if timing else VARIANTS
    plans=["paired100","paired20","disconnected","inhibition","glucose"] if timing else PLANS
    resolutions=[.125,.0625,.03125] if timing else [.125]
    assert results["completed"] and not results["physiologicallyQualified"]
    assert p["variants"]==variants and p["plans"]==plans and p["steps"]==[1,.125]
    if timing:assert p["resolutions"]==resolutions and p["integrationVersion"]=="event-aligned-calcium-v1"
    assert p["offsets"]==([0] if p["quick"] else [0,60])
    duration=4000 if p["smoke"] else 60000 if timing else 240000
    assert p["durationMs"]==duration and p["samplePeriodMs"]==20
    expected={(v,plan,o,dt,h) for v in variants for plan in plans for o in p["offsets"] for dt in p["steps"] for h in resolutions}
    assert len(results["cases"])==len(expected)==p["caseCount"]
    summaries=[];source_trains={};sample_rows=0;contact_rows=0;event_rows=0
    for case in results["cases"]:
        v,plan,o,dt=(case[k] for k in ("variant","plan","offset","dt"))
        h=case["resolution"] if timing else .125
        assert (v,plan,o,dt,h) in expected;expected.remove((v,plan,o,dt,h))
        directory=root/case["key"];design=read(directory/"design.json")
        assert read(directory/"summary.json")==case
        for k in ("key","variant","plan","offset","dt","duration"):assert design[k]==case[k]
        if timing:assert design["resolution"]==h and design["fixtureTimeShiftMs"]==.03125
        times=[1000+o,2000+o,3000+o] if p["smoke"] else [2000+o+i*997 for i in range(58)]
        assert design["probeTimes"]==times
        shift={"paired20":-20,"reversed100":100,"early500":-500}.get(plan,-100)
        assert design["pfShift"]==shift
        start,end=(2000,3000) if p["smoke"] else (10000,30000)
        assert (design["challengeStart"],design["challengeEnd"])==(start,end)
        schedule={r:[100]+([] if r=="dormant" else [t+(300 if r=="late" else shift)+b*10 for t in times for b in range(5)]) for r in ROLES}
        schedule["olive-excitation"]=[] if plan=="quiet-olive" else [t for t in times for _ in range(4)]
        schedule["olive-inhibition"]=[t for t in range(start,end,10) for _ in range(4)] if plan=="inhibition" else []
        if timing:schedule={r:[t+.03125 for t in ts] for r,ts in schedule.items()}
        for role,ts in schedule.items():assert sorted(x["message"]["timestampMs"] for x in design["inputs"] if x["role"]==role)==sorted(ts)
        assert len(design["inputs"])==sum(map(len,schedule.values()))==case["inputs"]
        declared={x["message"]["messageId"]:x for x in design["inputs"]};assert len(declared)==case["inputs"]
        roles={x["contactId"]:x["role"] for x in design["contacts"]};assert set(roles.values())==set(ROLES)
        taught=design["taughtCell"]
        for x in design["contacts"]:assert x["target"]==(1-taught if x["role"]=="other-cell" else taught)
        inputs={};climbs={};received={};olive_received={};crossings=[];pc_spikes=0;all_spike_ids=set();created={};pc_times=[[],[]]
        for line in (directory/"events.jsonl").open(encoding="utf-8"):
            event_rows+=1;event=json.loads(line);kind=event["kind"];x=event["value"]
            if kind=="input":
                m=x["message"];mid=m["messageId"];assert mid in declared and mid not in inputs
                assert m==declared[mid]["message"] and x["role"]==declared[mid]["role"];inputs[mid]=m
            elif kind=="climbing":
                m=x["message"];mid=m["messageId"];assert mid not in all_spike_ids;all_spike_ids.add(mid)
                assert m["sourceStructure"]=="InferiorOlive" and m["targetStructure"]=="PurkinjeCellLayer" and m["sourceInstanceKey"]=="isolated-olive" and m["sourceNeuronId"]=="n-000"
                assert m["neurotransmitter"]=="GLUTAMATE" and m["vesicleQuanta"]==1 and not m["isSyntheticBackground"]
                assert x["connected"]==(plan!="disconnected");climbs[mid]=x
            elif kind=="olive-crossing":
                assert x["soma"] in (0,1) and x["axon"] in (0,1) and x["soma"]+x["axon"]>0
                assert 0<x["timeMs"]<=duration and x["timeMs"]%dt==0;crossings.append(x)
            elif kind=="purkinje-spike":
                assert x["messageId"] not in all_spike_ids;all_spike_ids.add(x["messageId"])
                assert x["sourceStructure"]=="PurkinjeCellLayer" and x["neurotransmitter"]=="GABA"
                assert x["sourceNeuronId"] in ("n-000","n-001");pc_spikes+=1
                assert 0<x["timestampMs"]<=duration
                pc_times[int(x["sourceNeuronId"][2:])].append(x["timestampMs"])
            elif kind=="olive-delivery":
                mid=x["messageId"];assert mid in inputs and mid not in olive_received
                assert inputs[mid]["targetStructure"]=="InferiorOlive";olive_received[mid]=x
                close(x["scheduledMs"],inputs[mid]["timestampMs"]+declared[mid]["delayMs"])
                assert 0<=x["processedMs"]-x["scheduledMs"]<dt+1e-9 and x["target"]==0
                if timing:close(x["processedMs"],x["scheduledMs"],1e-10)
            elif kind=="learning":
                mid=x["messageId"];assert mid not in received;received[mid]=x
                if x["source"]=="InferiorOlive":
                    assert mid in climbs and climbs[mid]["connected"] and x["target"]==taught
                    emitted=climbs[mid]["message"]["timestampMs"];delay=climbs[mid]["delayMs"]
                else:
                    assert mid in inputs and inputs[mid]["targetStructure"]=="PurkinjeCellLayer"
                    emitted=inputs[mid]["timestampMs"];delay=declared[mid]["delayMs"]
                close(x["scheduledMs"],emitted+delay);assert 0<=x["processedMs"]-x["scheduledMs"]<dt+1e-9
                if timing:close(x["processedMs"],x["scheduledMs"],1e-10)
                before={c["contactId"]:c for c in x["before"]};after={c["contactId"]:c for c in x["after"]};assert before.keys()==after.keys()
                assert x["contactId"] in before
                for cid,b in before.items():
                    a=after[cid];created.setdefault(cid,x["processedMs"])
                    for k in ("contactId","sourceInstance","source","sourceNeuron","target","synthetic"):assert a[k]==b[k]
                    change=a["weight"]-b["weight"]
                    if v!="legacy":assert change==0,"Candidate changed weight directly on arrival"
                    elif change!=0:
                        assert x["source"]=="InferiorOlive" and b["source"]=="CerebellarGranule" and b["target"]==x["target"] and not b["synthetic"] and change<0
                        eligibility=b["preTrace"]*math.exp(-(x["processedMs"]-b["timestampMs"])/20)
                        assert eligibility>=.1-1e-7 and -change<=.04+1e-7
                    if b["target"]!=x["target"]:assert a==b,"Cross-cell learning mutation"
            else:raise AssertionError(kind)
        assert len(inputs)==len(declared)
        assert len(olive_received)==sum(m["targetStructure"]=="InferiorOlive" for m in inputs.values())
        assert len(climbs)==case["outgoing"]==case["oliveAxon"]==sum(x["axon"] for x in crossings)
        assert sum(x["soma"] for x in crossings)==case["oliveSoma"] and pc_spikes==case["pcSpikes"]
        delivered=sum(x["source"]=="InferiorOlive" for x in received.values());assert delivered==case["delivered"]==(0 if plan=="disconnected" else len(climbs))
        assert len(received)==sum(m["targetStructure"]=="PurkinjeCellLayer" for m in inputs.values())+delivered
        source_times=sorted(x["message"]["timestampMs"] for x in climbs.values())
        assert [math.ceil(t/dt)*dt if timing else t for t in source_times]==[x["timeMs"] for x in crossings if x["axon"]]
        if plan=="quiet-olive":assert not climbs
        source_key=(plan,o,dt,h)
        if source_key in source_trains:assert source_trains[source_key]==crossings,"Learning variant changed the unidirectional source"
        else:source_trains[source_key]=crossings
        last=0;maximum_energy=0
        with (directory/"cells.csv").open(newline="") as f:
            for raw in csv.DictReader(f):
                row={k:float(v) for k,v in raw.items()};assert all(math.isfinite(v) for v in row.values())
                t=row["timeMs"];assert t==last+20;last=t;sample_rows+=1
                assert row["oliveGlucose"]==(0 if plan=="glucose" and start<t<=end else 1)
                for prefix in ("o","p0","p1"):
                    error=abs(1+row[prefix+"Produced"]-row[prefix+"Consumed"]-row[prefix+"Overflow"]-row[prefix+"Reserve"])
                    maximum_energy=max(maximum_energy,error);assert error<1e-7
                    assert 0<=row[prefix+"Reserve"]<=1 and row[prefix+"Unmet"]==0
                    close(row[prefix+"Requested"],row[prefix+"Consumed"]+row[prefix+"Unmet"],1e-7)
        assert last==duration and case["maxEnergyError"]<1e-7
        previous={};last_rows={}
        with (directory/"contacts.csv").open(newline="") as f:
            for raw in csv.DictReader(f):
                role=raw.pop("role");assert role in ROLES;row={k:float(v) for k,v in raw.items()};contact_rows+=1
                assert all(math.isfinite(v) for v in row.values());t=row["timeMs"];assert t%20==0 and 0<t<=duration
                if role in previous:assert t==previous[role]+20 and row["weight"]<=last_rows[role]["weight"]+1e-7
                else:
                    cid=next(cid for cid,r in roles.items() if r==role);assert t==math.ceil(created[cid]/20)*20
                previous[role]=t;last_rows[role]=row
                if v!="legacy" and role!="nonparallel":
                    assert .628-1e-7<=row["weight"]<=1 and .05-1e-10<=row["calcium"] and 0<=row["store"]<=5+1e-10
                    close(row["calcium"]+row["store"],5.05+row["entry"]-row["extruded"],1e-7)
                    assert row["massError"]<1e-7 and 0<=row["inducedDepression"]<=.372 and row["integratedCalcium"]>=0
                    if v=="no-entry":assert row["entry"]==0
                    if v=="no-release":assert row["store"]==5
                else:assert all(row[k]==0 for k in ("ip3","calcium","store","integratedCalcium","inducedDepression","entry","extruded","massError"))
        assert previous==dict.fromkeys(ROLES,duration)
        for c in case["contacts"]:
            role=c["role"]
            if role!="climbing":close(last_rows[role]["weight"],c["state"]["weight"])
            if role in ("climbing","nonparallel"):assert c["state"]["weight"]==1
            assert c["massError"]<1e-7
        summary=dict(key=case["key"],variant=v,plan=plan,offset=o,dt=dt,oliveAxon=len(climbs),delivered=delivered,purkinjeSpikes=pc_spikes,
            contacts={r:dict(weight=x["weight"],inducedDepression=x["inducedDepression"]) for r,x in last_rows.items()})
        if timing:summary.update(resolution=h,sourceTimes=source_times,purkinjeTimes=pc_times,
            traceHashes={name:sha256((directory/name).read_bytes()).hexdigest() for name in ("cells.csv","contacts.csv")},
            peakCalcium={c["role"]:c["peakCalcium"] for c in case["contacts"]})
        summaries.append(summary)
    assert not expected
    lookup={(c["variant"],c["plan"],c["offset"],c["dt"],c.get("resolution",.125)):c for c in summaries}
    step_comparisons=[]
    for c in summaries:
        if c["dt"]!=1:continue
        fine=lookup[(c["variant"],c["plan"],c["offset"],.125,c.get("resolution",.125))]
        if timing:
            assert c["sourceTimes"]==fine["sourceTimes"],"Host packet period changed olive axonal times"
            assert c["purkinjeTimes"]==fine["purkinjeTimes"],"Host packet period changed Purkinje spike times"
            assert c["traceHashes"]==fine["traceHashes"],"Host packet period changed electrical/chemical traces"
        step_comparisons.append(dict(variant=c["variant"],plan=c["plan"],offset=c["offset"],
            oliveCountDifference=c["oliveAxon"]-fine["oliveAxon"],purkinjeCountDifference=c["purkinjeSpikes"]-fine["purkinjeSpikes"],
            maximumWeightDifference=max(abs(c["contacts"][r]["weight"]-fine["contacts"][r]["weight"]) for r in ROLES),
            maximumInductionDifference=max(abs(c["contacts"][r]["inducedDepression"]-fine["contacts"][r]["inducedDepression"]) for r in ROLES)))
    report=dict(completed=True,accountingValidated=True,caseCount=len(summaries),cellRows=sample_rows,contactRows=contact_rows,eventRows=event_rows,
        physiologicalCalibrationComplete=False,physicalRecoveryAssessed=False,calciumMechanismQualified=False,
        interpretation="Accounting and routing validation only. Compare calcium/weight specificity, blocked mechanisms, timing and step sensitivity before promoting physiological claims.",cases=summaries,stepComparisons=step_comparisons)
    if timing:
        comparisons=[]
        for c in summaries:
            if c["dt"]!=.125 or c["resolution"]==.03125:continue
            fine=lookup[(c["variant"],c["plan"],c["offset"],.125,c["resolution"]/2)]
            trains=[]
            for a,b in zip([c["sourceTimes"],*c["purkinjeTimes"]],[fine["sourceTimes"],*fine["purkinjeTimes"]]):
                trains.append(dict(coarseCount=len(a),fineCount=len(b),countDifference=len(a)-len(b),
                    maximumOrdinalTimeDifferenceMs=max((abs(x-y) for x,y in zip(a,b)),default=0) if len(a)==len(b) else None))
            comparisons.append(dict(variant=c["variant"],plan=c["plan"],offset=c["offset"],coarseStep=c["resolution"],fineStep=fine["resolution"],trains=trains,
                contacts={r:dict(coarsePeakCa=c["peakCalcium"][r],finePeakCa=fine["peakCalcium"][r],
                    coarseInduction=c["contacts"][r]["inducedDepression"],fineInduction=fine["contacts"][r]["inducedDepression"],
                    weightDifference=c["contacts"][r]["weight"]-fine["contacts"][r]["weight"]) for r in ROLES}))
        report.update(hostPacketInvarianceValidated=True,solverConvergenceQualified=False,resolutionComparisons=comparisons,
            interpretation="Exact arrival accounting and packet invariance required. Solver refinement differences are saved for review, not converted into physiological success. No learning constants were fitted in this experiment.")
    if write_report:(root/"cell-analysis.json").write_text(json.dumps(report,indent=2,allow_nan=False)+"\n",encoding="utf-8")
    print(f"Validated {len(summaries)} cases, {sample_rows} cell frames, {contact_rows} contact frames, {event_rows} events. Biological quality remains unqualified.")
    return report

if __name__=="__main__":analyse(Path(sys.argv[1]))

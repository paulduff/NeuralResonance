"""Strict schedule/ledger/reference/replay accounting; convergence remains for review."""
import bisect
import csv
import json
import math
from hashlib import sha256
import struct
from pathlib import Path
import sys

read=lambda p:json.loads(p.read_text(encoding="utf-8-sig"))
def rows(path):
    with path.open(newline="") as f:return [{k:float(v) for k,v in row.items()} for row in csv.DictReader(f)]
def close(a,b,tol=1e-8):assert math.isfinite(a) and math.isfinite(b) and abs(a-b)<=tol,(a,b)
def train_comparison(a,b):
    return dict(firstCount=len(a),secondCount=len(b),countDifference=len(a)-len(b),
        maximumOrdinalDifferenceMs=max((abs(x-y) for x,y in zip(a,b)),default=0) if len(a)==len(b) else None)

def analyse(root,write_report=True):
    root=Path(root);p=read(root/"protocol.json");bundle=read(root/"inputs.json");r=read(root/"results.json")
    assert p["study"]=="split-numerics" and r["completed"] and not r["physiologicallyQualified"]
    assert sha256((root/"inputs.json").read_bytes()).hexdigest()==p["bundleSha256"]
    duration=4000 if p["smoke"] else 60000;assert p["durationMs"]==duration and p["caseCount"]==80
    expected=set()
    for s in bundle["sourceCases"]:
        for energy in (False,True):
            for coupling in (.025,.0125,.00625):
                for membrane in ((.025,) if energy else (.025,.00625)):expected.add(("source",s["id"],energy,coupling,membrane))
        for tol,delta in ((1e-7,0),(1e-9,0),(1e-11,0),(1e-11,-.001),(1e-11,.001)):expected.add(("reference",s["id"],tol,delta))
    for s in bundle["replayCases"]:
        for v in ("calcium","no-entry","disconnected"):
            for h in (.125,.0625,.03125,.015625):expected.add(("replay",s["id"],v,h))
    assert len(r["cases"])==len(expected)==80
    native=read(root/"native/results.json");reference=read(root/"reference/results.json")
    assert native["completed"] and reference["completed"] and len(native["cases"])==60 and len(reference["cases"])==20
    rp=read(root/"reference/protocol.json")
    assert rp["method"]=="DOP853" and not rp["energyEnabled"] and rp["relativeTolerances"]==[1e-7,1e-9,1e-11] and rp["maxStepMs"]==.125
    assert rp["durationMs"]==duration and rp["smoke"]==p["smoke"]
    assert rp["bundleSha256"]==p["bundleSha256"]
    traces={};lookup={};sample_count=0;event_count=0;contact_count=0;baseline_matches=0;replay_summaries=[]
    for c in r["cases"]:
        kind=c["kind"];source=next(x for x in bundle["replayCases" if kind=="replay" else "sourceCases"] if x["id"]==c["sourceId"])
        k=(kind,c["sourceId"],c["useEnergy"],c["coupling"],c["membraneStep"]) if kind=="source" else (kind,c["sourceId"],c["rtol"],c["perturbationMv"]) if kind=="reference" else (kind,c["sourceId"],c["variant"],c["step"])
        assert k in expected;expected.remove(k);lookup[k]=c
        assert c["duration"]==duration
        directory=root/c["stage"]/c["key"];summary=read(directory/"summary.json");assert summary=={k:v for k,v in c.items() if k!="stage"}
        assert summary in (reference if kind=="reference" else native)["cases"]
        design=read(directory/"design.json")
        inputs=[x for x in source["inputs"] if x["arrivalMs"]<duration and (kind!="replay" or c["variant"]!="disconnected" or x["role"]!="climbing")]
        expected_inputs=sorted(inputs,key=lambda x:x["arrivalMs"]) if kind=="source" else inputs
        assert design["inputs"]==expected_inputs
        assert c["inputs"]==len(inputs)
        if kind=="reference":assert not c["energyEnabled"] and c["nfev"]>0 and design["rtol"]==c["rtol"] and design["perturbationMv"]==c["perturbationMv"]
        else:
            for field in ("sourceId","duration","key"):assert design[field]==c[field]
            for field in (("useEnergy","coupling","membraneStep") if kind=="source" else ("variant","step")):assert design[field]==c[field]
        trace=rows(directory/"trace.csv");assert len(trace)==duration//2
        for i,x in enumerate(trace):
            assert x["timeMs"]==2*(i+1) and all(math.isfinite(v) for v in x.values());sample_count+=1
            if kind=="replay":
                for j in (0,1):assert 0<=x[f"p{j}Reserve"]<=1 and abs(x[f"p{j}BalanceError"])<1e-7 and x[f"p{j}Unmet"]==0
            else:
                assert x["ca"]>=0 and all(-1e-8<=x[g]<=1+1e-8 for g in ("h","n","xs","k","l","r","s","q","ha","xa"))
                assert x["ampa"]>=0 and x["gabaD"]>=0 and x["gabaS"]>=0
                if kind=="source":
                    if c["useEnergy"]:
                        close(x["reserve"],1+x["produced"]-x["consumed"]-x["overflow"],1e-7);assert 0<=x["reserve"]<=1 and x["unmet"]==0
                        close(x["requested"],x["consumed"]+x["unmet"],1e-7);assert abs(x["balanceError"])<1e-7
                    else:assert all(x[name]==0 for name in ("reserve","activation","demandRate","produced","requested","consumed","unmet","overflow","balanceError"))
        traces[c["key"]]=trace
        if kind=="reference":
            assert c["axon"]==len(c["spikeTimes"]) and c["soma"]==len(c["somaTimes"])
            for train in (c["spikeTimes"],c["somaTimes"]):assert all(0<t<=duration for t in train) and all(a<b for a,b in zip(train,train[1:]))
            assert all(math.isfinite(v) for v in c["finalState"])
            for i,name in enumerate(("vs","vd","va","h","n","xs","k","l","r","s","q","ca","ha","xa")):
                assert c["finalState"][i]==trace[-1][name]
            continue
        declared={x["message"]["messageId"]:x for x in inputs};assert len(declared)==len(inputs)
        seen=set();spikes=[[],[]];soma=0;ids=set()
        for line in (directory/"events.jsonl").open():
            event_count+=1;e=json.loads(line);x=e["value"]
            if e["kind"] in ("arrival","learning"):
                mid=x["messageId"];assert mid in declared and mid not in seen;seen.add(mid)
                close(x["scheduledMs"],declared[mid]["arrivalMs"],1e-10);close(x["processedMs"],declared[mid]["arrivalMs"],1e-10)
                if kind=="replay":
                    message=declared[mid]["message"]
                    before={v["contactId"]:v for v in x["before"]};after={v["contactId"]:v for v in x["after"]};assert before.keys()==after.keys()
                    assert x["contactId"] in before and x["source"]==message["sourceStructure"]
                    owner=before[x["contactId"]]
                    assert owner["sourceInstance"]==message["sourceInstanceKey"] and owner["sourceNeuron"]==message["sourceNeuronId"] and owner["source"]==message["sourceStructure"]
                    if declared[mid]["role"]!="climbing":assert x["contactId"]==next(v["contactId"] for v in source["contacts"] if v["role"]==declared[mid]["role"])
                    expected_target=source["taughtCell"] if declared[mid]["role"]=="climbing" else next(v["target"] for v in source["contacts"] if v["role"]==declared[mid]["role"])
                    assert x["target"]==expected_target
                    for cid,a in after.items():
                        b=before[cid];assert a["weight"]==b["weight"],"Direct weight mutation on replay arrival"
                        for field in ("sourceInstance","source","sourceNeuron","target","synthetic"):assert a[field]==b[field]
                        if a["target"]!=x["target"]:assert a==b
            elif e["kind"]=="crossing":
                assert kind=="source" and x["soma"] in (0,1) and x["axon"] in (0,1) and x["soma"]+x["axon"]>0
                soma+=x["soma"]
                if x["axon"]:spikes[0].append(x["timeMs"])
            elif e["kind"]=="spike":
                assert kind=="replay" and x["messageId"] not in ids;ids.add(x["messageId"])
                assert x["sourceStructure"]=="PurkinjeCellLayer" and x["neurotransmitter"]=="GABA"
                assert x["sourceNeuronId"] in ("n-000","n-001");spikes[int(x["sourceNeuronId"][2:])].append(x["timestampMs"])
            else:raise AssertionError(e["kind"])
        assert seen==declared.keys()
        if kind=="source":
            assert c["soma"]==soma and c["axon"]==len(spikes[0]) and spikes[0]==c["spikeTimes"]
            if c["useEnergy"] and c["coupling"]==.025 and c["membraneStep"]==.025:
                assert c["spikeTimes"]==[t for t in source["baseline"]["spikeTimes"] if t<=duration]
                mapping=dict(oliveSoma="vs",oliveDendrite="vd",oliveAxon="va",oReserve="reserve",oProduced="produced",oRequested="requested",oConsumed="consumed",oUnmet="unmet",oOverflow="overflow")
                for b in source["baseline"]["samples"]:
                    if b["timeMs"]>duration:break
                    actual=trace[int(b["timeMs"]//2)-1]
                    for old,new in mapping.items():close(actual[new],b[old],1e-9)
                baseline_matches+=1
        else:
            assert spikes==c["spikes"]
            expected_roles={x["role"]:x["contactId"] for x in source["contacts"]};previous={};final={}
            first_times={role:min(v["arrivalMs"] for v in inputs if v["role"]==role) for role in expected_roles}
            with (directory/"contacts.csv").open(newline="") as f:
                for raw in csv.DictReader(f):
                    role=raw.pop("role");assert role in expected_roles;x={k:float(v) for k,v in raw.items()};contact_count+=1
                    assert all(math.isfinite(v) for v in x.values());t=x["timeMs"]
                    first=first_times[role]
                    assert t==previous.get(role,math.ceil(first/2)*2-2)+2;previous[role]=t
                    assert .628-1e-7<=x["weight"]<=1
                    if role!="nonparallel":
                        close(x["calcium"]+x["store"],5.05+x["entry"]-x["extruded"],1e-7)
                        assert x["calcium"]>=.05-1e-10 and 0<=x["store"]<=5+1e-10 and x["massError"]<1e-7 and 0<=x["inducedDepression"]<=.372
                        if c["variant"]=="no-entry":assert x["entry"]==0
                    else:assert x["weight"]==1 and x["calcium"]==0
                    if role in final:assert x["weight"]<=final[role]["weight"]+1e-7
                    final[role]=x
            assert previous==dict.fromkeys(expected_roles,duration)
            for role,cid in expected_roles.items():
                # C# JSON writes the shortest decimal round-tripping a float;
                # CSV promotes that same float to double. Compare its actual
                # 32-bit value, not the decimal parsed as a different double.
                wire=next(x["state"]["weight"] for x in c["contacts"] if x["contactId"]==cid)
                expected_weight=struct.unpack("f",struct.pack("f",wire))[0]
                assert final[role]["weight"]==expected_weight
            replay_summaries.append(dict(key=c["key"],sourceId=c["sourceId"],variant=c["variant"],step=c["step"],contacts=final))
    assert not expected and baseline_matches==4
    comparisons=[]
    def compare(a,b,comparison):
        ta,tb=traces[a["key"]],traces[b["key"]]
        max_voltage=max(abs(x[col]-y[col]) for x,y in zip(ta,tb) for col in ("vs","vd","va"))
        first=next((x["timeMs"] for x,y in zip(ta,tb) if max(abs(x[col]-y[col]) for col in ("vs","vd","va"))>1),None)
        source=next(s for s in bundle["sourceCases"] if s["id"]==a["sourceId"])
        edges=[0,*sorted({x["arrivalMs"] for x in source["inputs"] if x["role"]=="olive-excitation" and x["arrivalMs"]<duration}),duration]
        counts=lambda train:[bisect.bisect_left(train,end)-bisect.bisect_left(train,start) for start,end in zip(edges,edges[1:])]
        ca,cb=counts(a["spikeTimes"]),counts(b["spikeTimes"])
        comparisons.append(dict(comparison=comparison,sourceId=a["sourceId"],first=a["key"],second=b["key"],spikes=train_comparison(a["spikeTimes"],b["spikeTimes"]),maximumSampledVoltageDifferenceMv=max_voltage,firstSampledDifferenceOverOneMvMs=first,
            responseWindows=dict(edges=edges,firstCounts=ca,secondCounts=cb,changedCount=sum(x!=y for x,y in zip(ca,cb)))))
    for source in bundle["sourceCases"]:
        sid=source["id"];strict=lookup[("reference",sid,1e-11,0)]
        compare(lookup[("reference",sid,1e-7,0)],lookup[("reference",sid,1e-9,0)],"reference-tolerance")
        compare(lookup[("reference",sid,1e-9,0)],strict,"reference-tolerance")
        for delta in (-.001,.001):compare(lookup[("reference",sid,1e-11,delta)],strict,"initial-state-sensitivity")
        for coupling in (.025,.0125,.00625):
            for membrane in (.025,.00625):compare(lookup[("source",sid,False,coupling,membrane)],strict,"matched-energy-off-production-v-reference")
            compare(lookup[("source",sid,True,coupling,.025)],lookup[("source",sid,False,coupling,.025)],"energy-feedback-control-not-reference")
        for coupling in (.025,.0125):compare(lookup[("source",sid,True,coupling,.025)],lookup[("source",sid,True,coupling/2,.025)],"energy-enabled-coupling-refinement")
    replay_comparisons=[];summary_lookup={x["key"]:x for x in replay_summaries}
    for source in bundle["replayCases"]:
        for variant in ("calcium","no-entry","disconnected"):
            for h in (.125,.0625,.03125):
                a=lookup[("replay",source["id"],variant,h)];b=lookup[("replay",source["id"],variant,h/2)]
                fa,fb=summary_lookup[a["key"]]["contacts"],summary_lookup[b["key"]]["contacts"]
                replay_comparisons.append(dict(sourceId=source["id"],variant=variant,coarseStep=h,fineStep=h/2,
                    trains=[train_comparison(x,y) for x,y in zip(a["spikes"],b["spikes"])],
                    maximumSampledVoltageDifferenceMv=max(abs(x[col]-y[col]) for x,y in zip(traces[a["key"]],traces[b["key"]]) for col in ("p0Soma","p0Dendrite","p1Soma","p1Dendrite")),
                    contacts={role:dict(coarseWeight=fa[role]["weight"],fineWeight=fb[role]["weight"],coarseInduction=fa[role]["inducedDepression"],fineInduction=fb[role]["inducedDepression"],
                        coarsePeakCa=next(x["peakCa"] for x in a["contacts"] if x["contactId"]==cid),finePeakCa=next(x["peakCa"] for x in b["contacts"] if x["contactId"]==cid)) for role,cid in ((x["role"],x["contactId"]) for x in source["contacts"])}))
    report=dict(completed=True,accountingValidated=True,caseCount=80,baselineMatches=baseline_matches,traceRows=sample_count,contactRows=contact_count,eventRows=event_count,
        physiologicalCalibrationComplete=False,solverConvergenceQualified=False,physicalRecoveryAssessed=False,
        interpretation="Schedules, exact replay, baseline reproduction and accounting validated. Reference tolerance, source sensitivity and receiving-cell refinement require review. Reference excludes metabolic feedback; energy-on/off differences are controls, not numerical error estimates.",sourceComparisons=comparisons,replaySummaries=replay_summaries,replayComparisons=replay_comparisons)
    if write_report:(root/"cell-analysis.json").write_text(json.dumps(report,indent=2,allow_nan=False)+"\n",encoding="utf-8")
    print(f"Validated 80 cases, {baseline_matches} archived baseline matches, {sample_count} trace rows, {contact_count} contact rows and {event_count} events. Quality remains for review.")
    return report

if __name__=="__main__":analyse(Path(sys.argv[1]))

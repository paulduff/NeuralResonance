"""Corrupt real short captures to check replay, reference and ledger validation."""
import contextlib
from hashlib import sha256
import importlib.util
import io
import json
from pathlib import Path
import shutil
import sys
import tempfile
sys.dont_write_bytecode=True
spec=importlib.util.spec_from_file_location("a",Path(__file__).resolve().parents[1]/"analyse-split-numerics.py")
a=importlib.util.module_from_spec(spec);spec.loader.exec_module(a)
source=Path(sys.argv[1]);read=lambda p:json.loads(p.read_text(encoding="utf-8-sig"))
assert read(source/"protocol.json")["smoke"]
hashes=lambda root:{str(p.relative_to(root)):sha256(p.read_bytes()).hexdigest() for p in root.rglob("*") if p.is_file()}
before=hashes(source)
with contextlib.redirect_stdout(io.StringIO()):a.analyse(source,write_report=False)
assert before==hashes(source)
print("Valid short capture accepted; read-only audit leaves evidence unchanged.",flush=True)
changes=("source-arrival","missing-climbing","wrong-owner","wrong-presynaptic-cell","direct-learning","energy","reference-nan","reference-input","reference-energy","reference-final-state","missing-case","replay-step","no-entry","missing-contact-frame","bundle-hash")
for change in changes:
    with tempfile.TemporaryDirectory(prefix="nre-split-validator-") as temp:
        root=Path(temp)/"capture";shutil.copytree(source,root);cases=read(root/"results.json")["cases"]
        def case(kind,**where):return next(c for c in cases if c["kind"]==kind and all(c[k]==v for k,v in where.items()))
        chosen=case("source",useEnergy=True) if change in ("source-arrival","energy") else case("reference") if change.startswith("reference") else case("replay",variant="no-entry" if change=="no-entry" else "calcium")
        directory=root/chosen["stage"]/chosen["key"]
        if change in ("source-arrival","missing-climbing","wrong-owner","wrong-presynaptic-cell","direct-learning"):
            path=directory/"events.jsonl";events=[json.loads(line) for line in path.read_text().splitlines()]
            if change=="source-arrival":next(e for e in events if e["kind"]=="arrival")["value"]["processedMs"]+=.01
            else:
                e=next(e for e in events if e["kind"]=="learning" and e["value"]["source"]=="InferiorOlive")
                if change=="missing-climbing":events.remove(e)
                elif change=="wrong-owner":e["value"]["target"]=1-e["value"]["target"]
                elif change=="wrong-presynaptic-cell":
                    for side in ("before","after"):next(x for x in e["value"][side] if x["contactId"]==e["value"]["contactId"])["sourceNeuron"]="n-999"
                else:e["value"]["after"][0]["weight"]-=.01
            path.write_text('\n'.join(json.dumps(e) for e in events)+'\n')
        elif change in ("energy","reference-nan","no-entry","missing-contact-frame"):
            path=directory/("contacts.csv" if change in ("no-entry","missing-contact-frame") else "trace.csv")
            lines=path.read_text().splitlines()
            if change=="missing-contact-frame":lines.pop(2)
            else:
                columns=lines[0].split(',');row=lines[1].split(',');field={"energy":"reserve","reference-nan":"vs","no-entry":"entry"}[change]
                row[columns.index(field)]="NaN" if change=="reference-nan" else str(float(row[columns.index(field)])+.1);lines[1]=','.join(row)
                if change=="no-entry":
                    row[columns.index("extruded")]=str(float(row[columns.index("extruded")])+.1);lines[1]=','.join(row)
            path.write_text('\n'.join(lines)+'\n')
        elif change=="reference-final-state":
            for path in (root/"results.json",root/"reference/results.json",directory/"summary.json"):
                data=read(path);target=next(x for x in data["cases"] if x["key"]==chosen["key"]) if "cases" in data else data
                target["finalState"][0]+=.1;path.write_text(json.dumps(data))
        else:
            path=(directory/"design.json" if change in ("reference-input","replay-step") else root/"reference/protocol.json" if change=="reference-energy" else root/"results.json" if change=="missing-case" else root/"protocol.json")
            data=read(path)
            if change=="reference-input":data["inputs"][0]["arrivalMs"]+=1
            elif change=="reference-energy":data["energyEnabled"]=True
            elif change=="missing-case":data["cases"].pop()
            elif change=="replay-step":data["step"]/=2
            else:data["bundleSha256"]="invalid"
            path.write_text(json.dumps(data))
        rejected=False
        try:
            with contextlib.redirect_stdout(io.StringIO()):a.analyse(root,write_report=False)
        except AssertionError:rejected=True
        assert rejected,change
        print(change+": correctly rejected",flush=True)

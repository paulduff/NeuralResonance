"""Reject corrupt transport, schedules, ownership and accounting in real captures."""
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
spec=importlib.util.spec_from_file_location("analysis",Path(__file__).resolve().parents[1]/"analyse-olive-purkinje.py")
module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
source=Path(sys.argv[1]);read=lambda p:json.loads(p.read_text(encoding="utf-8-sig"))
assert read(source/"protocol.json")["quick"]
before={str(p.relative_to(source)):sha256(p.read_bytes()).hexdigest() for p in source.rglob("*") if p.is_file()}
with contextlib.redirect_stdout(io.StringIO()):module.analyse(source,write_report=False)
assert before=={str(p.relative_to(source)):sha256(p.read_bytes()).hexdigest() for p in source.rglob("*") if p.is_file()}
print("Valid short capture accepted; read-only audit leaves all evidence unchanged")
for change in ("missing-axon","wrong-owner","early-arrival","direct-candidate-decrement","energy","calcium-mass","schedule","supply","nan"):
    with tempfile.TemporaryDirectory(prefix="nre-calcium-validator-") as directory:
        root=Path(directory)/"capture";shutil.copytree(source,root)
        case=root/"calcium-paired100-0-0.125"
        if change in ("missing-axon","wrong-owner","early-arrival","direct-candidate-decrement"):
            path=case/"events.jsonl";events=[json.loads(s) for s in path.read_text().splitlines()]
            if change=="missing-axon":events.remove(next(e for e in events if e["kind"]=="climbing"))
            if change=="wrong-owner":next(e for e in events if e["kind"]=="climbing")["value"]["message"]["sourceInstanceKey"]="unrelated"
            if change=="early-arrival":next(e for e in events if e["kind"]=="learning")["value"]["processedMs"]-=20
            if change=="direct-candidate-decrement":next(e for e in events if e["kind"]=="learning")["value"]["after"][0]["weight"]-=.01
            path.write_text('\n'.join(json.dumps(e) for e in events)+'\n')
        elif change=="schedule":
            path=case/"design.json";data=read(path);data["probeTimes"].pop();path.write_text(json.dumps(data))
        else:
            path=case/("contacts.csv" if change=="calcium-mass" else "cells.csv")
            lines=path.read_text().splitlines();columns=lines[0].split(',');row=lines[20].split(',')
            name={"energy":"oReserve","calcium-mass":"store","supply":"oliveGlucose","nan":"oliveSoma"}[change]
            row[columns.index(name)]="NaN" if change=="nan" else str(float(row[columns.index(name)])+.1)
            lines[20]=','.join(row);path.write_text('\n'.join(lines)+'\n')
        rejected=False
        try:
            with contextlib.redirect_stdout(io.StringIO()):module.analyse(root,write_report=False)
        except AssertionError:rejected=True
        assert rejected,change
        print(change+": correctly rejected")

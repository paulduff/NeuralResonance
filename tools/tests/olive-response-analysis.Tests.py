"""Corruption checks against real short-probe evidence, plus full schedule checks."""
import contextlib
import importlib.util
import io
import json
from pathlib import Path
import shutil
import sys
import tempfile

sys.dont_write_bytecode=True
spec=importlib.util.spec_from_file_location("analysis",Path(__file__).resolve().parents[1]/"analyse-olive-response.py")
module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
source=Path(sys.argv[1])
read=lambda p:json.loads(p.read_text(encoding="utf-8-sig"))
write=lambda p,v:p.write_text(json.dumps(v),encoding="utf-8")
assert read(source/"protocol.json")["smoke"] and read(source/"protocol.json")["quick"]
with contextlib.redirect_stdout(io.StringIO()):module.analyse(source)
print("Valid short capture and all full schedules: accepted")
for change in ("missing-simultaneous-input","late-response-window","incorrect-latency","energy","supply","unmatched-baseline","missing-full-probe"):
    with tempfile.TemporaryDirectory(prefix="nre-olive-response-validator-") as directory:
        root=Path(directory)/"capture";shutil.copytree(source,root)
        result=read(root/"results.json")
        case=next(c for c in result["cases"] if c["challenge"]=="inhibition-4" and c["schedule"]=="throughout" and c["dt"]==.125)
        key=case["key"]
        if change=="missing-simultaneous-input":
            p=root/(key+"-inputs.json");data=read(p);data=[r for r in data if not(r["timeMs"]==2000 and r["transmitter"]=="gaba")];write(p,data)
        if change=="late-response-window":case["responses"][-1]["endMs"]+=1
        if change=="incorrect-latency":case["responses"][0]["firstAxonLatencyMs"]+=1
        if change in ("energy","supply","unmatched-baseline"):
            p=root/(key+".csv");lines=p.read_text().splitlines();row=lines[100].split(',')
            column={"energy":9,"supply":15,"unmatched-baseline":1}[change]
            row[column]=str(float(row[column])-.1);lines[100]=','.join(row);p.write_text('\n'.join(lines)+'\n')
        if change=="missing-full-probe":
            p=root/"schedule-check.json";data=read(p);data[0]["pulses"].pop();write(p,data)
        write(root/(key+"-summary.json"),case);write(root/"results.json",result)
        rejected=False
        try:
            with contextlib.redirect_stdout(io.StringIO()):module.analyse(root)
        except AssertionError:rejected=True
        assert rejected,change
        print(change+": correctly rejected")

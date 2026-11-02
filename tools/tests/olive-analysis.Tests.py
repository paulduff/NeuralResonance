"""Check rejection of corrupted saved evidence using a real smoke-run fixture."""
import contextlib
import importlib.util
import io
import json
from pathlib import Path
import shutil
import sys
import tempfile

sys.dont_write_bytecode=True
spec=importlib.util.spec_from_file_location("analysis",Path(__file__).resolve().parents[1]/"analyse-olive-calibration.py")
module=importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
source=Path(sys.argv[1])
read=lambda p:json.loads(p.read_text(encoding="utf-8-sig"))
write=lambda p,v:p.write_text(json.dumps(v),encoding="utf-8")
result=read(source/"results.json")
case=next(c for c in result["cases"] if c["variant"]=="production" and c["protocol"]=="quiet" and c["dt"]==.125)
protocol=read(source/"protocol.json")
assert protocol["smoke"]
protocol.update(caseCount=1,variants=[v for v in protocol["variants"] if v["name"]=="production"],protocols=["quiet"],stepsMilliseconds=[.125])

for change in ["valid","energy","missing-row","event-count","input-count","duplicate-case"]:
    with tempfile.TemporaryDirectory(prefix="nre-olive-analysis-") as directory:
        root=Path(directory);key=case["key"]
        for suffix in [".csv","-events.json","-summary.json"]:shutil.copy2(source/(key+suffix),root/(key+suffix))
        write(root/"protocol.json",protocol)
        result=dict(completed=True,cases=[dict(case)])
        if change in ("energy","missing-row"):
            path=root/(key+".csv");lines=path.read_text().splitlines()
            if change=="energy":
                row=lines[2].split(',');row[9]="0.1";lines[2]=','.join(row)
            else:lines.pop()
            path.write_text('\n'.join(lines)+'\n')
        if change=="event-count":write(root/(key+"-events.json"),[dict(timeMs=1,soma=0,axon=1)])
        if change=="input-count":
            result["cases"][0]["excEvents"]=1;write(root/(key+"-summary.json"),result["cases"][0])
        if change=="duplicate-case":result["cases"].append(dict(case))
        write(root/"results.json",result)
        rejected=False
        try:
            with contextlib.redirect_stdout(io.StringIO()):module.analyse(root)
        except AssertionError:rejected=True
        assert rejected==(change!="valid"),change
        print(change+": "+("correctly rejected" if rejected else "validated"))

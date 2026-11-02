"""Sequential native and independent-reference stages for an unattended window."""
import argparse
from hashlib import sha256
import json
from pathlib import Path
import subprocess
import sys

def main():
    p=argparse.ArgumentParser();p.add_argument("output",type=Path);p.add_argument("--assembly",type=Path,required=True);p.add_argument("--bundle",type=Path,required=True);p.add_argument("--dotnet",required=True);p.add_argument("--smoke",action="store_true");a=p.parse_args()
    if a.output.exists():raise ValueError("Use a new output directory")
    a.output.mkdir(parents=True)
    write=lambda path,value:path.write_text(json.dumps(value,indent=2,allow_nan=False)+"\n",encoding="utf-8")
    try:
        for label,command in (("native",[a.dotnet,str(a.assembly),str(a.output/"native"),"--split-numerics",str(a.bundle)]),
            ("reference",[sys.executable,"-B",str(Path(__file__).with_name("olive_numerical_reference.py")),str(a.output/"reference"),str(a.bundle)])):
            if a.smoke:command.append("--smoke")
            print("========== "+label.upper()+" ==========",flush=True)
            subprocess.run(command,check=True)
        cases=[]
        for label in ("native","reference"):
            r=json.loads((a.output/label/"results.json").read_text());assert r["completed"]
            cases.extend(dict(c,stage=label) for c in r["cases"])
        write(a.output/"protocol.json",dict(study="split-numerics",smoke=a.smoke,durationMs=4000 if a.smoke else 60000,
            caseCount=80,bundleSha256=sha256(a.bundle.read_bytes()).hexdigest(),physiologicallyQualified=False))
        write(a.output/"inputs.json",json.loads(a.bundle.read_text()))
        write(a.output/"results.json",dict(completed=True,physiologicallyQualified=False,cases=cases))
    except Exception as e:write(a.output/"FAILED.json",dict(error=repr(e)));raise

if __name__=="__main__":main()

"""Ensure locality assessment rejects unrelated-cell and contact contamination."""
import contextlib
import importlib.util
import io
import json
from pathlib import Path
import shutil
import sys
import tempfile

sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location("analysis", Path(__file__).resolve().parents[1]/"analyse-boundary-timing.py")
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
source = Path(sys.argv[1])
read = lambda p: json.loads(p.read_text(encoding="utf-8-sig"))
assert read(source/"protocol.json")["smoke"]
with contextlib.redirect_stdout(io.StringIO()):
    valid = module.analyse(source, write_report=False, cell_local=True)
assert valid["cellLocalityPassed"]
print("Valid candidate capture accepted.", flush=True)
for damage in ("untaught-voltage", "untaught-contact", "wrong-mode"):
    with tempfile.TemporaryDirectory(prefix="nre-cell-local-analysis-") as temp:
        root = Path(temp)/"capture"
        shutil.copytree(source, root)
        shutil.copy2(source.parent/"baseline.json", root.parent/"baseline.json")
        case = next(c for c in read(root/"results.json")["cases"] if c["boundaryCondition"] == "markers")
        replay = next(s for s in read(root/"inputs.json")["replayCases"] if s["id"] == case["sourceId"])
        target = 1-replay["taughtCell"]
        if damage == "wrong-mode":
            path = root/"protocol.json"
            data = read(path); data["cellLocalIntegration"] = False
            path.write_text(json.dumps(data))
        else:
            path = root/case["key"]/("cell-states.csv" if damage == "untaught-voltage" else "contacts.csv")
            lines = path.read_text().splitlines(); names = lines[0].split(",")
            role = next(c["role"] for c in replay["contacts"] if c["target"] == target)
            for i in range(1, len(lines)):
                row = lines[i].split(",")
                if damage == "untaught-voltage" and int(row[names.index("cell")]) == target:
                    row[names.index("voltage")] = str(float(row[names.index("voltage")])+.01)
                elif damage == "untaught-contact" and row[names.index("role")] == role:
                    row[names.index("weight")] = ".999999"
                else:
                    continue
                lines[i] = ",".join(row); break
            else:
                raise AssertionError("No target row")
            path.write_text("\n".join(lines)+"\n")
        rejected = False
        try:
            with contextlib.redirect_stdout(io.StringIO()):
                damaged = module.analyse(root, write_report=False, cell_local=True)
            rejected = not damaged["cellLocalityPassed"]
        except AssertionError:
            rejected = True
        assert rejected, damage
        print(damage+": correctly rejected", flush=True)

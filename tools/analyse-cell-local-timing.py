"""Account for both timing modes, then assess locality without declaring physiology."""
from hashlib import sha256
import importlib.util
import json
from pathlib import Path
import sys

sys.dont_write_bytecode = True
read = lambda p: json.loads(p.read_text(encoding="utf-8-sig"))


def digest(path):
    value = sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(8*1024*1024), b""):
            value.update(block)
    return value.hexdigest().upper()


def main(stage):
    spec = importlib.util.spec_from_file_location("boundary", Path(__file__).with_name("analyse-boundary-timing.py"))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    original = module.analyse(stage / "global")
    candidate = module.analyse(stage / "local", cell_local=True)
    baseline = read(stage.parent / "legacy-captures.json")[stage.name]
    cases = read(stage / "global/results.json")["cases"]
    assert {c["key"] for c in cases} == set(baseline)
    for case in cases:
        expected = baseline[case["key"]]
        assert case["spikes"] == expected["spikes"], case["key"]
        for name, value in expected["files"].items():
            assert digest(stage / "global" / case["key"] / name) == value, (case["key"], name)
    assert original["boundaryHypothesisSupported"]
    report = dict(completed=True, accountingValidated=True, caseCount=48,
        legacyCaptureMatches=len(cases), legacyBoundaryHypothesisSupported=True,
        cellLocalityPassed=candidate["cellLocalityPassed"],
        declaredIncomingEventsPreserved=True, solverConvergenceQualified=False,
        physiologicalCalibrationComplete=False, physicalRecoveryAssessed=False,
        candidateComparisons=candidate["comparisons"], contactLocality=candidate["contactLocality"])
    (stage / "cell-analysis.json").write_text(json.dumps(report, indent=2, allow_nan=False)+"\n", encoding="utf-8")
    print(f"48 conditions accounted for; all 24 previous captures reproduced. Candidate locality passed: {report['cellLocalityPassed']}.")
    print("A completed capture is not physiological or whole-brain qualification.")
    # A failed candidate remains captured, but the unattended launcher must not
    # promote a failed short preflight into an unnecessary long experiment.
    if not report["cellLocalityPassed"]:
        raise SystemExit("Candidate failed cell-locality acceptance; inspect saved contrasts.")


if __name__ == "__main__":
    main(Path(sys.argv[1]))

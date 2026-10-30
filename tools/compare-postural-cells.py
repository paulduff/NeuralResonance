"""Summarise a completed frozen-baseline/candidate run; never assign physiological qualification."""
import argparse
import json
from pathlib import Path


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("package",type=Path)
    package=parser.parse_args().package
    read=lambda p:json.loads(p.read_text(encoding="utf-8-sig"))
    old,new=read(package/"baseline/cell-analysis.json"),read(package/"study/cell-analysis.json")
    pold,pnew=read(package/"baseline/protocol.json"),read(package/"study/protocol.json")
    if not all(r["completed"] and r["accountingValidated"] and r["freshBaselinesMatched"] and not r["smoke"] for r in (old,new)):
        raise ValueError("Both full studies must pass their accounting checks")
    for field in ("durationMilliseconds","sampledCellsPerCondition","inputs","profiles","microtubules"):
        if pold[field]!=pnew[field]:raise ValueError("Mismatched paired protocol: "+field)
    key=lambda r:(r["structure"],r["pacemaker"],r["stepMilliseconds"],r["input"])
    baseline={key(r):r for r in old["results"]}
    paired=[]
    for row in new["results"]:
        if key(row) not in baseline:continue
        before=baseline[key(row)]
        paired.append({"case":row["case"],"oldMeanRateHz":before["meanRateHz"],"newMeanRateHz":row["meanRateHz"],
            "oldChangedCells":before["cellsWithDifferentStimulusSpikeTimes"],"newChangedCells":row["cellsWithDifferentStimulusSpikeTimes"],
            "oldStepDifferenceHz":before["stimulusRateDifferenceFromQuarterMillisecondHz"],
            "newStepDifferenceHz":row["stimulusRateDifferenceFromQuarterMillisecondHz"],
            "oldAisBoundaryFraction":before["aisBoundaryFraction"],"newAisBoundaryFraction":row["aisBoundaryFraction"]})
    if len(paired)!=len(baseline):raise ValueError("Incomplete paired matrix")
    report={"completed":True,"pairedConditions":len(paired),"candidateConditions":len(new["results"]),
        "candidateDynamicsVersion":pnew["modelDynamicsVersion"],"matchedProtocols":True,
        "biologicalEquivalenceQualified":False,"physicalRecoveryAssessed":False,
        "caution":"Both executables use this run's installed .NET runtime. AIS is diagnostic in the new HH cell, not its spike trigger. Numerical convergence and cerebellar physiology require review.",
        "comparison":paired,"candidateConvergence":new["results"]}
    (package/"paired-analysis.json").write_text(json.dumps(report,indent=2,allow_nan=False)+"\n",encoding="utf-8")
    print(f"Saved {len(paired)} paired comparisons and {len(new['results'])} candidate conditions. Physiological quality awaits review.")


if __name__=="__main__":main()

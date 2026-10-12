"""Regenerate the Unity-compatible peripheral circuit from its canonical source."""
import argparse
import hashlib
import re
from pathlib import Path


def generate(repository: Path) -> str:
    source = repository / "src/NRE.SimAvatar/AvatarSpinalLocomotorCircuit.cs"
    raw = source.read_bytes()
    original = raw.decode("utf-8-sig").replace("\r\n", "\n")
    circuit = original[original.index("public sealed class AvatarSpinalLocomotorCircuit"):]
    circuit = re.sub(r"double\.IsFinite\((\w+)\)",
                     r"(!double.IsNaN(\1) && !double.IsInfinity(\1))", circuit)
    header = """using System;
namespace Nre.UnityWorld {
public struct AvatarSpinalLocomotorState {
 public double LeftFlexorRecruitment,LeftExtensorRecruitment,RightFlexorRecruitment,RightExtensorRecruitment,LeftSwingSeconds,RightSwingSeconds;
 public long PhaseHandoffs;
 public AvatarSpinalLocomotorState(double lf,double le,double rf,double re,double ls,double rs,long ph){LeftFlexorRecruitment=lf;LeftExtensorRecruitment=le;RightFlexorRecruitment=rf;RightExtensorRecruitment=re;LeftSwingSeconds=ls;RightSwingSeconds=rs;PhaseHandoffs=ph;}
 public double LeftCycle=>LeftFlexorRecruitment-LeftExtensorRecruitment;
 public double RightCycle=>RightFlexorRecruitment-RightExtensorRecruitment;
}
"""
    return ("// Generated peripheral spinal circuit from NRE.SimAvatar; do not edit by hand.\n"
            f"// Source SHA256 (UTF-8, LF): {hashlib.sha256(original.encode()).hexdigest()}\n"
            + header + circuit.rstrip() + "\n\n}\n")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="Verify without modifying the port")
    args = parser.parse_args()
    repository = Path(__file__).resolve().parent.parent
    destination = repository / "UnityWorld/Assets/NRE/Runtime/SpinalCircuit.cs"
    expected = generate(repository)
    if args.check:
        if destination.read_text(encoding="utf-8-sig") != expected:
            raise SystemExit("Unity spinal port is stale; regenerate and run compatibility tests.")
        print("Unity spinal port matches the canonical source.")
    else:
        destination.write_text(expected, encoding="utf-8", newline="\n")
        print(destination)


if __name__ == "__main__":
    main()

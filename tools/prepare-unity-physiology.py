"""Generate Unity-compatible Avatar physiology without changing its equations."""
import argparse
import hashlib
import re
from pathlib import Path


def generate(root: Path) -> str:
    source = (root / "src/NRE.SimAvatar/AvatarWorldDynamics.cs").read_text(encoding="utf-8-sig")
    # Ecology/device inventory remains in its original host; port only physiology.
    source = source[:source.index("public enum AvatarDeviceRangeProfile")]
    source = source.replace("namespace NRE.SimAvatar;", "using System;\nnamespace Nre.UnityWorld {")
    pattern = r"public readonly record struct (\w+)\((.*?)\)(;|\s*\{)"

    def record(match: re.Match) -> str:
        name, arguments, ending = match.groups()
        fields = [argument.strip().split() for argument in arguments.split(",")]
        properties = "\n".join(f"    public {kind} {field} {{ get; }}" for kind, field in fields)
        parameters = ", ".join(f"{kind} {field}" for kind, field in fields)
        assignments = " ".join(f"this.{field} = {field};" for _, field in fields)
        declaration = f"public readonly struct {name}\n{{\n{properties}\n    public {name}({parameters}) {{ {assignments} }}"
        return declaration + ("\n}" if ending == ";" else "\n")

    port = re.sub(pattern, record, source, flags=re.S)
    def state_update(match: re.Match) -> str:
        field, expression = match.groups()
        arguments = [expression if name == field else f"state.{name}"
                     for name in ("StoredEnergyJoules", "HydrationFraction", "TissueIntegrityFraction")]
        return "new AvatarPhysiologyState(" + ", ".join(arguments) + ")"
    port = re.sub(r"state with\s*\{\s*(\w+)\s*=\s*(.*?)\s*\}", state_update, port, flags=re.S)
    port = re.sub(r"double\.IsFinite\(([\w.]+)\)",
                  r"(!double.IsNaN(\1) && !double.IsInfinity(\1))", port)
    host = (root / "src/NRE.WorldSim/HeadlessWorldRuntime.cs").read_text(encoding="utf-8-sig")
    nominal = re.search(r"const double NominalStoredEnergyJoules = (.*?);", host).group(1)
    arguments = re.search(r"AvatarPhysiologyOptions PhysiologyOptions = new\((.*?)\);", host, re.S).group(1)
    arguments = arguments.replace("NominalStoredEnergyJoules", nominal)
    defaults = ("\npublic static class AvatarWorldDefaults {\n"
                "    public static readonly AvatarPhysiologyOptions Options = new AvatarPhysiologyOptions("
                + arguments + ");\n}\n")
    return ("// Generated from AvatarWorldDynamics.cs and HeadlessWorldRuntime.cs; do not edit.\n"
            f"// Equations SHA256 (UTF-8, LF): {hashlib.sha256(source.encode()).hexdigest()}\n"
            + port.rstrip() + "\n" + defaults + "}\n")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    path = root / "UnityWorld/Assets/NRE/Runtime/AvatarPhysiology.cs"
    expected = generate(root)
    if args.check:
        if path.read_text(encoding="utf-8-sig") != expected:
            raise SystemExit("Unity physiology is stale; regenerate and run compatibility tests.")
        print("Unity physiology matches the canonical source and world settings.")
    else:
        path.write_text(expected, encoding="utf-8", newline="\n")
        print(path)


if __name__ == "__main__":
    main()

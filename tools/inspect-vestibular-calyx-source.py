"""Inventory two pinned COMSOL sources without evaluating any model expression.

This is source inspection, not a solver, test runner, COMSOL importer or claim of
publication equivalence. Preserve scoped definitions, disabled features and all
serialized function choices; do not infer active defaults from their file order.
"""
import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import xml.etree.ElementTree as ET
from zipfile import ZipFile


REVISION = "47d499449df66c6676ee43cbb08ea1995469a817"
MODELS = {
    "template": ("2022-12-05 VHCC Model Template.mph",
                 "01ab5babc1020f4ee2181c275456e60f520e3cca",
                 "9A3610A622D0BC032827812793524A6A338EA61BB912035FD84E58F1F80C8CEA"),
    "paper_roles": ("Draft 12 - Roles.mph",
                    "4c68f7768e1140aab83f9cf679a9d6d1390b9666",
                    "93A8DAB0954709BA02893B4D395A9D10750EFC5A9B06EBAC8AD19EBE286F6FA6"),
}
SECTIONS = (
    "ModelParam", "ExprList", "FunctionFeatureList", "PhysicsList", "CplList",
    "ModelNodeList", "GeomList", "MeshList", "StudyList", "SolverSequenceList",
)


def require(condition, message):
    if not condition:
        raise ValueError(message)


def sha(data):
    return hashlib.sha256(data).hexdigest().upper()


def read(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def write(path, value):
    # Never overwrite even a partially completed earlier inspection.
    with path.open("x", encoding="utf-8", newline="\n") as stream:
        json.dump(value, stream, indent=2, ensure_ascii=False)
        stream.write("\n")


def tree(node):
    """Retain attributes, meaningful text and ordered children without evaluation."""
    result = {"element": node.tag, "attributes": dict(node.attrib)}
    if node.text and node.text.strip():
        result["text"] = node.text
    if node.tail and node.tail.strip():
        result["tail"] = node.tail
    if len(node):
        result["children"] = [tree(child) for child in node]
    return result


def inspect(folder, label):
    name, blob, expected = MODELS[label]
    manifest = read(folder / "source-manifest.json")
    require(manifest["revision"] == REVISION, "Unexpected source revision")
    entries = [item for item in manifest["files"] if item["path"] == name]
    require(len(entries) == 1, "Missing or duplicate model provenance")
    entry = entries[0]
    original = (folder / name).read_bytes()
    require(sha(original) == expected == entry["sha256"], "Model SHA-256 mismatch")
    require(len(original) == entry["bytes"], "Model length mismatch")
    actual_blob = hashlib.sha1(b"blob " + str(len(original)).encode() + b"\0" + original).hexdigest()
    require(actual_blob == blob == entry["gitBlob"], "Model Git blob mismatch")
    with ZipFile(folder / name) as archive:
        require(archive.namelist().count("dmodel.xml") == 1, "Ambiguous XML entry")
        xml = archive.read("dmodel.xml")
    require(xml == (folder / "dmodel.xml").read_bytes(), "Extracted XML differs from archive")
    # ElementTree only parses the preserved XML. No source expressions, external
    # files, embedded functions, dependencies or COMSOL binaries are executed.
    root = ET.fromstring(xml)
    sections = {}
    for section in SECTIONS:
        nodes = root.findall(section)
        require(len(nodes) == 1, "Missing or ambiguous section: " + section)
        sections[section] = tree(nodes[0])
    definitions = []
    parameters = root.find("ModelParam")
    groups = [("parameters", parameters)] + [
        ("variables/" + group.attrib["tag"], group)
        for group in root.findall("ExprList/Expr")
    ]
    for scope, group in groups:
        for expression in group.findall("expressions"):
            definitions.append({"scope": scope, "groupName": group.get("name"),
                                **expression.attrib})
    keys = [(item["scope"], item["name"]) for item in definitions]
    require(len(keys) == len(set(keys)), "Duplicate definitions within a scope")
    features = []

    def walk(node, ancestors):
        segment = node.tag + ("[" + node.attrib["tag"] + "]" if "tag" in node.attrib else "")
        path = ancestors + [segment]
        if node.tag in ("PhysicsFeature", "StudyFeature", "FunctionFeature"):
            features.append({"path": "/".join(path), "attributes": dict(node.attrib),
                             "fields": [tree(child) for child in node if child.tag in
                                        ("entityFlags", "entityComments", "storedParent", "selection",
                                         "param", "propertyValue", "NodeDefaultValues")]})
        for child in node:
            walk(child, path)

    for section in ("PhysicsList", "StudyList", "FunctionFeatureList"):
        walk(root.find(section), [])
    provenance = dict(label=label, folder=str(folder), file=name, revision=REVISION,
                      gitBlob=blob, bytes=len(original), sha256=expected,
                      xmlSha256=sha(xml), xmlEqualsArchive=True,
                      manifestSha256=sha((folder / "source-manifest.json").read_bytes()),
                      sourceUrl=entry["url"])
    return dict(provenance=provenance, sections=sections, definitions=definitions,
                features=features, rootAttributes=dict(root.attrib))


def main(template_folder, paper_folder, output):
    for folder in (template_folder, paper_folder):
        require(not output.is_relative_to(folder) and not folder.is_relative_to(output),
                "Inspection output and frozen source must not contain one another")
    require(not output.exists(), "Use a new inspection output directory")
    models = {label: inspect(folder, label) for label, folder in
              (("template", template_folder), ("paper_roles", paper_folder))}
    # This comparison is lexical and scoped, not an algebraic simplifier or a
    # determination of which definitions participate in a selected study.
    maps = [{(row["scope"], row["name"]): row["expr"] for row in model["definitions"]}
            for model in models.values()]
    differences = []
    for key in sorted(set(maps[0]) | set(maps[1])):
        left, right = maps[0].get(key), maps[1].get(key)
        if left != right:
            differences.append(dict(scope=key[0], name=key[1], template=left, paperRoles=right))
    output.mkdir(parents=True)
    for label, model in models.items():
        write(output / (label + ".json"), model)
    write(output / "definition-differences.json", differences)
    report = dict(
        capturedUtc=datetime.now(timezone.utc).isoformat(), sourceRevision=REVISION,
        scriptSha256=sha(Path(__file__).read_bytes()),
        models={label: {"provenance": data["provenance"],
                        "definitions": len(data["definitions"]), "features": len(data["features"])}
                for label, data in models.items()},
        lexicalDefinitionDifferences=len(differences),
        modelsExecuted=False, testsExecuted=False, downloadedCodeExecuted=False,
        mathematicalEquivalenceEstablished=False, selectedFunctionDefaultsResolved=False,
        publicationEquivalenceEstablished=False, physiologicalCalibrationEstablished=False,
    )
    write(output / "inspection.json", report)
    print(json.dumps({key: report[key] for key in
                      ("lexicalDefinitionDifferences", "modelsExecuted", "testsExecuted",
                       "publicationEquivalenceEstablished")}))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("template", type=Path)
    parser.add_argument("paper_roles", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    main(args.template.resolve(), args.paper_roles.resolve(), args.output.resolve())

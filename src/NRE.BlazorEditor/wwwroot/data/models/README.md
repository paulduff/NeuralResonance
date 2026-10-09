# Brain display assets

`brain-shell.glb` is a modified copy of **human-brain (1) - copy 1** by
[mo99ghahremani](https://sketchfab.com/mo99ghahremani), supplied by the operator.
[Original model](https://sketchfab.com/3d-models/human-brain-1-copy-1-315449e9517047edb65e3b462c259b91).
Licensed under [Creative Commons Attribution 4.0](https://creativecommons.org/licenses/by/4.0/).
The source's three `Foregrou` number-label meshes were removed. Transforms were
baked, duplicate vertices welded, normals smoothed, and the source materials
replaced by a uniform transparent display material. Geometry has 36,632 triangles.
The original remains outside this asset directory; source and derivative hashes
are in `brain-shell.json`.

The editor rotates the source's anterior +X axis into atlas anterior +Z, with
superior +Y unchanged. The shell is fitted to the existing display envelope.
The 70 cortical territories are partitions of its actual folded triangles,
assigned to the nearest existing DNNE cortical anchor in the same hemisphere.
These boundaries are **illustrative functional territories**, not a validated
histological or subject-specific cortical parcellation.

`subcortical-parts.glb` contains 37 named anatomical instances derived from the
[SPL/NAC Brain Atlas](https://www.openanatomy.org/atlas-pages/atlas-spl-nac-brain.html),
developed at Brigham and Women's Hospital and Massachusetts General Hospital.
Authors include Michael Halle, Florin Talos, Marianna Jakab, Nikos Makris, Dominic
Meier, Laurence Wald, Bruce Fischl and Ron Kikinis. The original atlas includes
MRI-derived segmentations from a single healthy volunteer.

All or portions of this licensed product (such portions are the “Software”) have
been obtained under license from The Brigham and Women's Hospital, Inc. and are
subject to the following terms and conditions:
[the included complete Slicer license](SPL-Slicer-License.txt).

The source revision, exact source URLs, hashes and instance mappings are recorded
in `subcortical-parts.json`. Changes: RAS axes converted into the DNNE display
frame, outward triangle winding restored, and each shape uniformly scaled and
recentered against the existing DNNE atlas. Uniform scale preserves shape
proportions while matching the existing bounding-box volume. Native source
registration is consequently **not preserved**; this is a visual reconstruction.
Striatum uses caudate plus putamen, and MotorThalamus uses ventral anterior plus
ventral lateral meshes. Their composite provenance is shown in the inspector.
Other functional nuclei retain explicitly identified schematic geometry.

Rebuild with Node.js and Python, from the repository root:

```powershell
node tools/prepare-brain-shell.mjs ../human-brain_1_-_copy_1.glb src/NRE.BlazorEditor/wwwroot/data/models/brain-shell.glb
python tools/fetch-brain-parts.py artifacts/anatomical-source
node tools/prepare-brain-parts.mjs artifacts/anatomical-source src/NRE.BlazorEditor/wwwroot/data/models/subcortical-parts.glb
node --test tools/tests/brain-display.test.mjs
```

These assets and all their scale, selection and colour controls affect the
display only. Neural activity uses the DNNE's measured mean firing rates;
structure-wide samples are explicitly identified as shared across hemispheres.

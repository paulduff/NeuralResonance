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
The 70 cortical territories are partitions of its actual folded triangles.
Gyrus-guided annotations described below drive the current display; assignment
to the nearest existing DNNE cortical anchor remains the same-hemisphere fallback.
These boundaries are **illustrative functional territories**, not a validated
histological or subject-specific cortical parcellation.

`subcortical-parts.glb` contains 39 anatomical instances and two explicitly
schematic population envelopes derived from the
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
frame and outward triangle winding restored. Cerebral parts share one uniform
scale and translation fitted inside the selected shell's cortical envelope.
Cerebellar lobular segments, vermis and dentate nuclei share a second uniform
transform fitted inside the shell's `cereb1` envelope. Native relative positions
and size ratios are preserved **within each assembly**. Alignment between the
two assemblies and the artist-created shell remains illustrative, not a medical
registration. This replaces the earlier individually resized/recentered parts.
The cerebellum uses 58 named SPL lobular segments; vermis segments are separated
from the remaining lobules. Granule and Purkinje populations use low-opacity
scaled envelopes, explicitly labelled as schematic: the source does not resolve
their microscopic cell-layer boundaries. Other unsegmented cerebellar nuclei
remain schematic, corrected into the new cerebellar display frame. Duplicate
grey cerebellar shell geometry is hidden after successful assembly loading.
Striatum uses caudate plus putamen, and MotorThalamus uses ventral anterior plus
ventral lateral meshes. Their composite provenance is shown in the inspector.
Other functional nuclei retain explicitly identified schematic geometry.
The viewer verifies the shell and anatomical asset hashes and instance IDs
before replacing the display. Shell surfaces are rendered on both sides, with
a single transparency pass; cortical selection retains translucency.

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

`cortical-map.json` is a modified annotation derivative of
**Detailed Labeled Surface Structures**, by the Ruth Lilly Medical Library,
Indiana University School of Medicine.
[Reference model](https://sketchfab.com/3d-models/detailed-labeled-surface-structures-d91080b5af91497b822728c08804f33c).
The publisher's model was made from [this base brain](https://www.myminifactory.com/object/3d-print-brain-72477),
with consultation from Dr. Garcia and Dr. Eckel, and is described as in progress.
The derived annotation data is licensed under
[CC BY-NC-SA 4.0](https://creativecommons.org/licenses/by-nc-sa/4.0/).
This data license is separate from the viewer code and the other assets' licenses.
The source FBX/ZIP remains an operator-supplied local original.

Changes: named mesh labels grouped into 21 anatomical categories, annotation
coordinates fitted to the chosen shell's cortical envelope, and a shared
single-hemisphere template projected onto both displayed hemispheres. Functional
areas are subdivided using DNNE atlas anchors subject to those gyral constraints.
All 18,520 cortical shell triangles and all 70 runtime cortical IDs are preserved.
The two insular areas remain atlas-guided because the source has no named insula.
No display shape is deformed by these annotations. The mirrored reference and
functional subdivisions are illustrative, not validated bilateral anatomy.
The inspector and footer state these limits. Anatomy mode can show DNNE regions
or reference gyral colours; Activity mode always colours measured DNNE rates.

Rebuild from the supplied ZIP, with the shell prepared first:

```powershell
node tools/prepare-cortical-map.mjs ../detailed-labeled-surface-structures.zip src/NRE.BlazorEditor/wwwroot/data/models/cortical-map.json
```

The reference FBX hash is checked before transfer. The viewer checks the shell's
SHA-256 and validates every triangle, ID, hemisphere and gyral constraint before
replacing its partition. An unavailable or incompatible map falls back to the
existing nearest-anchor display rather than silently applying incorrect IDs.

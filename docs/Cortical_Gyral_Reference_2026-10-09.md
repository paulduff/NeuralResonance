# Labelled cortical reference — 9 October 2026

The operator supplied `detailed-labeled-surface-structures.zip`, containing
`source/brain_edit_10.fbx` (7,910,060 bytes, FBX 7400). Inspection found 79 mesh
objects; 78 contain polygon geometry, totaling 221,306 triangles. The FBX
model names identify gyri and lobes; the geometry object names are less useful.
An offline conversion preserves those model names in a local GLB and catalog.

Source: [Detailed Labeled Surface Structures](https://sketchfab.com/3d-models/detailed-labeled-surface-structures-d91080b5af91497b822728c08804f33c),
Ruth Lilly Medical Library, Indiana University School of Medicine. The publisher
describes this as a model in progress made with consultation from Dr. Garcia
and Dr. Eckel. Its license is [CC BY-NC-SA 4.0](https://creativecommons.org/licenses/by-nc-sa/4.0/).
The original FBX, inspection GLB and preview remain local reference artifacts.
The derived annotation JSON described below is included with the display assets.

Local artifacts are in `artifacts/cortical-gyral-reference-20261009/`:
`brain_edit_10.fbx`, `inspection.json`, `reference-catalog.json`,
`gyral-reference.glb`, `convert-reference.mjs` and `preview.html`. The preview
supports named-mesh isolation and lateral, medial, anterior, posterior and
superior inspection. Conversion uses the official Three.js r184 FBX importer,
bakes transforms and simplifies display materials. It preserves the source's
native coordinates; preview fitting uses a single uniform scale.

The source shows one hemisphere, with a medial cut surface. It does not supply
a complete bilateral parcellation or independent DNNE hemisphere telemetry.
Number suffixes identify separate mesh objects; they are not Brodmann numbers.
Some small fragments and generic Black/Yellow objects are present. They must
not be treated as independently identified functional territories.

Candidate correspondence for the next mapping revision:

| Reference label | DNNE display candidates | Mapping constraint |
| --- | --- | --- |
| Pre Central / PreCentral | M1 | Locate motor strip; do not equate the entire mesh to a validated BA4 border. |
| Post Central / PostCentral | S1 | Locate sensory strip; current S1 boundary still requires registration. |
| ParaCentral Lobule | M1, S1, SMA | Several territories can share a gyrus; split rather than duplicate full meshes. |
| Inferior Frontal Gyrus | Broca, nearby PFC | BA44/45 is a subset; hemisphere labels do not establish language dominance. |
| Middle / Superior Frontal | PFC, premotor, frontal eye fields | Broad labels require subdivisions. |
| Lateral / Medial Orbital Frontal | Orbitofrontal, ventromedial PFC | Candidate anatomical anchor regions. |
| Superior Temporal | Auditory and posterior temporal regions | A1 and Wernicke are not the whole superior temporal gyrus. |
| Middle / Inferior Temporal | Temporal association, inferotemporal cortex | Broad labels require subdivisions. |
| Fusiform | Fusiform Gyrus | Named candidate anchor; register to the chosen shell. |
| Temporal Pole | Temporal Pole | Named candidate anchor; register to the chosen shell. |
| Supra Marginal | Supramarginal/Angular, TPJ | Angular gyrus is not separately established by this label. |
| PreCuneus | Precuneus | Named candidate anchor; register to the chosen shell. |
| Cingulate Gyrus | ACC, midcingulate, posterior cingulate | Split by anterior/posterior position; suffixes do not establish those divisions. |
| Parahippocampal | Parahippocampal Cortex | Does not identify individual hippocampal subfields. |
| Cuneus / Lingual / Occipital | Visual cortical territories | Gyral labels alone do not establish V1–V4 boundaries. |
| Parietal Lobe | Posterior parietal candidates | Coarse lobe name, not a functional boundary. |

At the initial inspection these were review candidates, not a mapping applied to the live editor. Its 70
folded cortical areas still use the explicitly approximate nearest-anchor
partition. A stronger mapping should first register this reference to the
selected bilateral shell, check central and lateral sulci and medial landmarks,
then transfer anatomical anchor constraints and divide shared gyri. Neither
non-uniform fitting nor mirroring a hemisphere would establish exact anatomy.

Browser inspection confirmed a coloured lateral hemisphere and successful
isolation of the named Pre Central mesh. The original ZIP and all reference
artifacts are copied with SHA-256 verification to
`D:/DNNE-desktop-runs/brain-display-20261009/final-assets-and-source/`.

## Applied display mapping

The operator subsequently authorized implementation. `prepare-cortical-map.mjs`
now reads the supplied ZIP directly using the official Three.js r184 FBX importer.
It checks the inspected source hash and extracts 21 named gyral/lobar categories.
Generic, unlabelled and unmeasured objects are excluded. The derivative annotation
asset `cortical-map.json` carries source credit and CC BY-NC-SA 4.0 licensing.

Source posterior +Z is converted into display anterior +Z. Annotation coordinates
are fitted to each hemisphere's cortical envelope; the single hemisphere is used
as a shared mirrored template. Each existing cortical shell triangle obtains a
nearest reference label. Functional subdivision is restricted to the candidate
gyri listed by the generator, with surface seeds projected from the existing
DNNE anchors. This fits annotation coordinates; it does not alter the shell or
the internal anatomical models. It is not a medical registration.

All 70 DNNE cortical instance IDs and all 18,520 cortical triangles are retained.
Sixty-eight instances have gyral constraints. The two insular instances remain
atlas-guided and show an explicit missing-reference note. Shared gyri are still
divided illustratively: a whole superior temporal gyrus is not asserted to be A1,
and the inferior frontal label does not independently validate BA44/45 or language
dominance. Some subdivisions are small; projected anchor movement is recorded in
the asset, including a roughly 38 mm temporal-pole adjustment. These annotations
are useful for display organization, not proof of exact functional boundaries.

Median shell-to-reference-centroid distance is approximately 1.73 mm, with a 95th
percentile of 8.01 mm. These measure the envelope fit's geometric proximity only;
they are not estimates of anatomical accuracy or functional-map uncertainty.

The editor offers **Cortex colours: DNNE regions / Reference gyri** in Anatomy
mode. Activity colours always use DNNE mean firing rates. Selecting a cortical
area shows the reference labels and the shared-template limitation. The footer
identifies 68 gyrus-guided instances and the mirrored reference. Source hash,
triangle counts, instance identity, hemisphere and label constraints are checked
before replacing the display geometry; an invalid map retains the previous
nearest-anchor fallback.

Nine Node tests passed, including triangle conservation, unchanged source
geometry, all 70 IDs, finite vertex colours, both hemispheres' motor-before-sensory
ordering, and rejection of crossed hemispheres, incompatible labels and excess
assignments. Twelve editor host tests passed in an isolated Release build.

## Internal alignment correction

Operator review found that cerebellar populations protruded behind the supplied
shell. The old aggregate centre was approximately 31.2 mm too posterior and
10.6 mm too superior relative to this shell's cerebellar envelope. Large legacy
ellipsoids also obscured the imported anatomy. The display now uses 58 named
SPL/NAC cerebellar segments, grouped into lobules and vermis, with the dentate
nuclei kept in their source-relative positions. All cerebellar anatomical parts
share one uniform scale and translation. The cerebral imported structures share
another transform, using the atlas's cerebral white-matter envelope as a fitting
reference. Individual resizing/recentering has been removed.

There are now 39 anatomical instances and two schematic population envelopes.
Granule/Purkinje microscopic boundaries cannot be recovered from these coarse
meshes; their low-opacity envelopes are labelled accordingly. Unsegmented
cerebellar nuclei retain schematic geometry in the corrected display frame.
Alignment between the two source assemblies and the supplied shell remains
approximate. Named anatomical shapes and their relative placement are preserved
within each assembly; this does not validate registration across subjects.

Both hemispheres remain available and the default shell opacity is 12%. Imported
shell surfaces preserve double-sided rendering, and a single transparency pass
reduces folded-surface artefacts. Cortical selection is capped below opacity 1.
Nine geometry/telemetry tests pass, including shell-envelope containment of all
41 imported instances, asset hashes, focus positions and common assembly
transforms. The editor builds with no warnings or errors.
The corrected display was inspected in left and right views and in Anatomy and
Activity modes. Both sides remained enabled at 12% shell opacity, the cerebellar
outline matched the selected shell envelope, and the selector remained disabled
in Activity mode. No new browser warnings/errors appeared after the editor restart.
DNNE retained process 31460; the World started paused. Source meshes, annotation
and display assets, edited code, logs and screenshots are backed up with SHA-256
verification under `D:/DNNE-desktop-runs/gyral-map-20261009/final-assets-and-source/`.

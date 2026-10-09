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
The original, conversion and preview remain local reference artifacts; this
asset is not added to the application's distributed models directory.

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

These are review candidates, not a mapping applied to the live editor. Its 70
folded cortical areas still use the explicitly approximate nearest-anchor
partition. A stronger mapping should first register this reference to the
selected bilateral shell, check central and lateral sulci and medial landmarks,
then transfer anatomical anchor constraints and divide shared gyri. Neither
non-uniform fitting nor mirroring a hemisphere would establish exact anatomy.

Browser inspection confirmed a coloured lateral hemisphere and successful
isolation of the named Pre Central mesh. The original ZIP and all reference
artifacts are copied with SHA-256 verification to
`D:/DNNE-desktop-runs/brain-display-20261009/final-assets-and-source/`.

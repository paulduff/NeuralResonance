# Brain display upgrade — 9 October 2026

The operator's `human-brain_1_-_copy_1.glb` replaces the initial Hi3D shell.
The new source has 36,708 triangles and an explicit CC BY 4.0 attribution.
Removing its three numbered annotation meshes leaves 36,632 triangles, with
welded vertices and smoothed normals. The original download is preserved.
The display copy is approximately 0.9 MB.

All 70 cortical instances now colour areas of this model's real folded surface.
Every cortical source triangle is assigned once, within its hemisphere, to the
nearest existing DNNE cortical anchor. This is approximate functional mapping;
the source model does not provide our DNNE region boundaries. Anatomy mode uses
region colours; Activity uses the same geometry with measured rate colours.

Thirty-seven internal instances have named SPL/NAC anatomical meshes: bilateral
striatum, nucleus accumbens, GPe, GPi, STN, red nucleus, dentate nucleus, pulvinar,
mediodorsal thalamus, VPL, VPM, anterior thalamic nuclei, LGN, MGN, superior and
inferior colliculi, mammillary bodies, motor thalamus, and midline corpus callosum.
Source meshes are pinned to atlas revision
`bec24db25aad5f7600e2df3becfb1f883e61ff56`, individually hash checked and licensed.
Shapes are uniformly scaled and centered on the existing DNNE display atlas;
this preserves form, but does not preserve the source's native registration.
Exact shapes unavailable in this selection remain marked as schematic. A whole
hippocampus or amygdala has not been assigned to its individual subnuclei.

Activity now uses a labelled, adjustable linear mean-firing-rate scale, initially
0–200 Hz. Previously, mean rate alone saturated the display at 8 Hz, and
un-normalized counts further drove saturation. Counts and dispatches no longer
inflate that rate scale. Missing samples differ from silent samples. A repeated
snapshot tick cannot refresh its display age; samples older than five seconds
dim and are marked stale. Pathway traces are limited to timestamped recent
dispatches, with hemisphere-specific endpoints.

Nucleus sizes remain fixed during activity and selection. Group and hemisphere
filters, selection isolation, structure labels and a zero-opacity shell setting
make internal inspection easier. The inspector reports whether each geometry is
an anatomical mesh, a composite, a folded cortical territory or a schematic.
It also identifies aggregate telemetry shared by the displayed hemispheres.
Left/Right cameras now correspond to negative/positive atlas X, respectively;
Anterior/Posterior correspond to positive/negative Z. Superior remains positive Y.

Validation: six Node tests passed, covering actual GLB loading, finite geometry,
hemisphere-safe cortical triangle conservation, unique anatomical mappings,
uniform sizing and centers, missing/silent rate samples, repeated-snapshot age,
old dispatch removal and resource disposal. Twelve Blazor editor host tests
passed using an isolated Release build. Browser verification and final backup
details are recorded after deployment below.

The editor was restarted independently of the DNNE; brain process 31460 remained
running. WorldSim starts paused and requires the operator's Play command. This
work changes visual assets and telemetry presentation; it changes no neuronal
learning rule, authority boundary or motor policy.

Deployed browser verification confirmed 70/70 folded cortical territories and
37 named internal meshes, current DNNE telemetry, group/hemisphere filtering,
left GPe selection and isolation, an adjustable activity legend and zero-opacity
shell inspection. Complete-brain framing and the lateral cortical view were
checked. Screenshots are saved in `artifacts/brain-shell-20261009/`. The earlier
editor restart generated temporary Blazor disconnection messages; no new model
loading or rendering errors appeared after the final deployment.

The additional operator-supplied labelled-gyrus FBX is preserved as a separate
local anatomical reference. See `Cortical_Gyral_Reference_2026-10-09.md` for its
inspection and candidate correspondence. Its boundaries have not been silently
substituted for DNNE's current approximate cortical partition.

Final source files, display assets, pinned SPL/NAC source meshes, test result,
screenshots, operator-supplied originals and local gyral reference artifacts are
copied with SHA-256 verification to
`D:/DNNE-desktop-runs/brain-display-20261009/final-assets-and-source/`.
The manifest records each original and backup path, byte count and hash.

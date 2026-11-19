# From solid cupula volume to local bundle displacement

The graded canal clock now has a completed passing report. The next missing
connection is spatial: the organ model returns displaced **solid volume**, while
the sensory membrane accepts hair-bundle displacement in nanometres. The older
`AvatarVestibularBundleInput` uses an explicitly uncalibrated gain on a different
filtered rotation model. That gain is not a local mechanical reconstruction.

## Chosen reduction and its evidence

The retained [Iversen/Rabbitt 2017 paper](https://pmc.ncbi.nlm.nih.gov/articles/PMC5658742/)
uses a shear-elastic cupula approximation with profile factor `lambda_gamma =
8*pi`. The [Ifediba 2007 appendix](https://pmc.ncbi.nlm.nih.gov/articles/PMC3005417/)
uses stiffness `integral G*lambda/A² ds` and distinguishes cupula solid volume
from the fluid coordinate. Its Table A.1 thickness expression contains a square
root: `sqrt(Ac/(4*pi))`, not `Ac/(4*pi)`. No scalar table parameter is installed
as a live default here.

We derive a simple circular shear field consistent with that stiffness form:

`u(x,y) = 2 Q/A [1 - (x²+y²)/R²]`, where `A = pi R²`.

It fixes displacement at the perimeter and integrates to exactly the supplied
solid volume `Q`. Its gradient varies across the cupula. Integrating shear strain
energy gives `0.5 K Q²`, with `K = 8*pi*G*h/A²`. This is a declared quasi-static
closure, not a transcription of a detailed human finite-element field.
The existing organ mass/damping and dynamics are unchanged; no additional energy
or force is injected into them by the projection.

The [2019 review](https://pmc.ncbi.nlm.nih.gov/articles/PMC6520623/) supports
peripheral attachment and a putative kinocilium/cupula mechanical link, but
also describes the incomplete evidence on mature long-bundle micromechanics.
A newly retained [2021 three-dimensional model](https://pmc.ncbi.nlm.nih.gov/articles/PMC8050243/)
reports spatially varying crista shear. Its inferred human modulus and thermal
effects are not direct measurements and are not used as parameters here.
The Selva 2009 full text was unavailable; only its earlier retained abstract and
metadata were inspected. No claimed full-text equation or parameter comes from it.
Source hashes, factual support and our derived assumptions are recorded in
`reference-data/cupula-shear-closure.json`.

## Implementation and constraints

`AvatarCupulaShearField` requires named organ/canal identity, explicit geometry
and provenance, radius, thickness and shear modulus. It reconstructs displacement,
flow-related velocity, gradients, elastic pressure and energy. It supplies no
anatomical default or automatic left/right mapping.

Each `AvatarCupulaBundleAttachment` names a stable cell, organ/canal, placement
source, root position, bundle length and excitatory polarity. The root lies on
the lower circular boundary; the bundle extends along positive local y. This
idealized attachment geometry is explicit. Deflection is the tip's displacement
relative to the fixed root, with declared polarity and exact metre-to-nanometre
unit conversion. The implementation uses the solid cupula coordinate selected
from `AvatarCanalMechanicalPoint`; changing fluid volume alone cannot move it.
Foreign identities, outside sample points and protruding tips are rejected.
No clipping, hidden gain, reset, neural signal or firing-rate target is present.

This field is an isolated component. It has no clock owner and cannot advance a
sensory cell. A subsequent connected owner must preserve every mechanical sample
and timing, reconcile field stiffness with the chosen organ's stiffness, provide
declared placements/polarities, and test the coupled membrane/nerve response.
Small-strain validity, detailed crista morphology, flexural and poroelastic
micromechanics, attachment compliance, active bundle adaptation and physiological
calibration remain open. The reduced field must not be represented as validating
those mechanisms or Avatar balance.

## Prospective checks

Twenty-three new component cases cover disk quadrature of total volume, integrated
strain energy, fixed perimeter/central maximum, local tip reconstruction,
position/length effects, selected solid coordinates, polarity, flow/displacement
separation, immutable identity and invalid geometry. They use clearly named
synthetic geometry. All 156 prior canal/skull/clock/bundle cases are retained,
giving 179 focused cases. No full unit-suite claim follows from that subset.
Tests run only in a separate visible PowerShell window, with frozen source,
runtime preservation and verified D: backups, without live monitoring.


The first prepared cupula package `6b69cf` was not launched. Frozen preflight
caught a catalogue representation mismatch: the reviewed Git blob uses LF,
while Git archive exports CRLF. Exact comparison confirms that only 52 line
endings differ. The failed preparation, both catalogue byte versions and failure
receipt are preserved in a verified 2,055-file D: backup. No model/test ran.
The harness now retains the reviewed canonical Git blob and the archive version
separately, pins their distinct hashes and requires equal text after ordinary
line-ending decoding. It does not relabel or reuse the failed package.


## Component window launched

The corrected fresh run is `cupula-shear-20261119-8ec244`, frozen at pushed
revision `ed357966062a5d96de4519face2a8060ba0aab23`. Frozen preflight passes,
including the separate canonical/catalogue byte identities. Its initial 2,054
files plus manifest verify in `D:/DNNE-desktop-runs/UnityWorld/cupula-shear-20261119-8ec244`.
Results belong to `D:/DNNE-build-work/cupula-shear-20261119-8ec244`.
Visible independent task `DNNE-Cupula-Shear-f033c1002bc2` started successfully.
There has been no post-launch output, status or process inspection. Exactly one
final report is pending: `8ec244`; do not monitor or duplicate it. It runs 179
focused cases and preservation/backup checks, not an independent coupled sensory
trajectory study or a live brain/body run. The full biological goal stays active
and unfinished. The earlier failed preparation `6b69cf` was never launched.

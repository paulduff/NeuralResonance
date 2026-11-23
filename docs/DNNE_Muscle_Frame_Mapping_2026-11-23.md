# Lower-leg attachment frames — 23 November 2026

The existing native body's synthetic flexor/extensor paths are not a mapped
human musculature. This candidate provides an explicit conversion from
preserved reference attachment coordinates into Avatar body-local coordinates.
It changes no neuronal, learning, muscle-force or Unity equation and selects
no live anatomy.

`AvatarMuscleFrameMap` requires named source/target bodies, orthonormal
anatomical bases, origins, a positive uniform scale, source and target SHA256
identities and a scaling declaration. There is no implicit normalization of
a supplied basis or hidden geometry fit. Landmark construction rejects
degenerate segments and anterior hints with a transverse component at most
1e-8 of unit magnitude. All mapped coordinates must remain finite. Axial
vectors receive the reflection sign separately from polar vectors.

The reference uses anterior +X, superior +Y and right +Z. Avatar's captured
bind axes use anterior +Z, superior +Y and right +X; this requires explicit
handedness conversion. The source convention is documented in
[OpenSim coordinate systems](https://opensimconfluence.atlassian.net/wiki/spaces/OpenSim/pages/53089978/Coordinate+Systems).

## Retained anatomy and native landmarks

`artifacts/lower-leg-anatomy-input-20261123` preserves 11 files plus manifest,
verified on D:. Its original source is the pinned Rajagopal2016 model described
in `DNNE_Muscle_Anatomy_Reference_2026-11-23.md`. The native endpoint is the
completed, paused, isolated initial quiet endpoint from
`declared-body-mechanics-20261123-203560`, including original raw geometry and
state identities. Source and native originals remain unchanged.

Five unwrapped muscles per side are retained: tibialis anterior, soleus,
tibialis posterior, peroneus longus and peroneus brevis. All ten names and 44
ordered points remain attached to their original tibia/calcaneus identities
until explicitly mapped to the corresponding shin/foot. Soleus does not
replace the entire gastrocnemius/soleus group; wrapped muscles are excluded
from this candidate and retain their source wrapping requirements.

The source tibia origin is offset from its knee joint. Source knee and ankle
landmarks define the proximal/distal axis; an anterior hint fixes rotation
about that axis. Actual native knee and ankle anchors define the target axis.
The uniform shin scale is the ratio of these segment lengths.

The source ankle lives between tibia and talus, and its calcaneus is offset
through the subtalar joint. For the preserved zero-coordinate pose, the
paired frame orientations are equal and their relative rotations cancel.
The ankle's position in calcaneus coordinates is therefore:

`ankle-in-talus - subtalar-in-talus + subtalar-in-calcaneus`.

The foot map preserves this offset and inherits the shin's anatomical rotation.
This is permitted here because both captured native body rotations are
identity and the source relative neutral rotations cancel. The test explicitly
checks those restrictions; it does not implement arbitrary joint kinematics.
Both maps agree at their common neutral ankle landmark.

**Foot scale is an explicit proxy using the shin ratio.** We do not have a
measured toe/heel landmark pair in this capture, and collider dimensions are
not substituted for anatomical measurements. The source locked subtalar joint
and Avatar's combined ankle/roll are different mechanical reductions.
Coordinate accuracy cannot establish foot fit or joint anatomical accuracy.

## Qualification workflow

The existing independent PowerShell runner supports
`fresh_motor_epoch_study.py prepare --frame-map`: 25 new frame/identity/error
controls plus the previous 263 muscle, neuronal/motor and plant cases. An
analytic heel-offset control runs even without the optional retained fixture;
the study explicitly supplies that fixture and requires all ten output paths.
Missing anatomical output fails comparison rather than passing as a skipped
capture. Eight independent equilibrium references remain required.

`muscle_frame_reference.py` independently reconstructs every mapped point
from the original landmarks at 80 and 110 Decimal digits. References must agree
within 1e-60 m; candidate coordinates must be within 1e-12 m per coordinate.
These are arithmetic criteria for the declared map, not uncertainty bounds on
the anatomical measurements. Original point order, body identities, source
hashes and all ten path fingerprints are checked. All paths and all 44
comparison records are saved, including on an accuracy failure.

The authoring build passed with zero warnings/errors; Python/PowerShell syntax
and all seven existing/new suite inventories were checked. No tests, reference
calculations or body integration ran during authoring. Scientific results are
pending the independent visible window and Paul's final footer.

Next integration must admit named anatomical paths in the native protocol,
which currently requires 38 synthetic joint flexor/extensor paths. It must
also supply explicit muscle geometry, tendon conventions and identified
motor-unit populations; one slow unit and one tibialis geometry cannot stand
in for the entire body. Native force delivery for these new paths, wrapping,
anatomical fit, coupled accuracy, neuronal authority and standing remain open.

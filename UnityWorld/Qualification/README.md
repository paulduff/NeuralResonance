# Captured Unity sensory fixtures

These frames were measured after ten seconds of passive physics in isolated
qualification `unity-qualification-20261012-091859-6cb04a`, Unity 6000.3.19f1,
with the expanded actuator/physiology adapter. They are unchanged player JSON,
not hand-authored examples. Energy, hydration and tissue now advance with the
canonical Avatar equations. The body
has fallen: principal trunk inclination is bounded, full fallen orientation is
retained, and COM height is measured above the floor.

`UnitySensoryWireTests` deserializes them into shared contracts and invokes the
real ControlProgram input validators. This verifies encoding compatibility for
this fixture without delivering synthetic measurements to the running brain.
It does not verify every pose, neural reception or learning.

`actuator-qualification.json` is the complete successful mechanical report:
19 bidirectional avatar axes, physical rotation/limits, passive settling, authority
gate and isolation. `native-coordinate-probe.json` records the independent two-link
engine fixture: force, position, velocity and named-drive tests for both X/Y and
X/Z joints. Both configurations pack their active coordinates into native slots
0 and 1 despite exposing three slots. The head/limb distinction determines physical
Y/Z axes and named drives, not a native slot 1/2 distinction.

All 40 native samples and all 19 avatar axes passed. Standing/walking were not
assessed. These reports establish this isolated fixture's mechanical response.

`muscles-stand-0-body-frame.json` and `muscles-stand-1-body-frame.json` are the
unchanged final measured frames of `unity-muscle-comparison-20261012-104831-aeff04`.
Each contains 38 named muscles. Both pass the real body validator and generate
additional proprioceptive populations compared with their muscle-free versions
in offline transducer tests. All four physical comparison cases were finite and
settled, but neither muscle case held standing: longest intervals were 0.83/1.07 s.
These fixtures establish wire compatibility for the captured poses, not learned
standing or delivery to the running brain. The complete comparison is backed up
on D:; see `docs/Unity_Muscle_Migration_2026-10-12.md` for scope and results.

`corrected-body-standing-example.json` is the unchanged body subtree at 1.01 s in
the full-position/p12 case of `unity-balance-capability-20261012-112144-4cdfab`.
`corrected-body-fallen-example.json` is the final canonical/p0 frame. Both retain
root tilt separately from relative trunk tilt and expose measured plantar fields.
They pass real validators and recruit plantar and vestibular receptors offline.

`pre-normalization-margin-example.json` (canonical/p0 sequence 211) and
`pre-saturation-hand-example.json` (full-active/p8 sequence 911) are unchanged
measured negative regression fixtures from that run. The validator rejects them
before the sensor encoding correction and accepts their explicit projected encoding.
Original forces and support distances are preserved. Across all 2,400 saved samples,
the original encoding rejected eighteen; projecting the two encoding functions
accepts every sample. This does not replace fresh physical qualification or prove
live neuronal delivery. Detailed reports and original traces are backed up on D:.

The subsequent run `unity-balance-capability-20261012-121048-8ee25f` contains
fresh player captures with those fixes applied. All 2,400 frames passed the real
validator and offline transducer without projection, with conserved plantar loads,
38 muscles and relative trunk tilt below 0.000001 rad. All eight cases were finite
and settled, but none maintained standing. Its original traces and validation
report are verified on D:. Fresh wire compatibility does not prove live delivery.

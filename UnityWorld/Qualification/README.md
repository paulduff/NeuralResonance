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

# Captured Unity sensory fixtures

These frames were measured after ten seconds of passive physics in isolated
qualification `unity-qualification-20261012-081457-b6baaa`, Unity 6000.3.19f1,
build v3. They are unchanged player JSON, not hand-authored examples. The body
has fallen: principal trunk inclination is bounded, full fallen orientation is
retained, and COM height is measured above the floor.

`UnitySensoryWireTests` deserializes them into shared contracts and invokes the
real ControlProgram input validators. This verifies encoding compatibility for
this fixture without delivering synthetic measurements to the running brain.
It does not verify every pose, neural reception or learning.

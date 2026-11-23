# Separate motor pools sharing one Avatar

Inspection after launching `muscle-population-active-20261123-a212b5` found a
concrete integration restriction in `DescendingMotorPool.AttachMotorOutput`:
it required the total body anatomy contact count to equal the pool's cell count.
Two independently owned 147-cell pools therefore could not share one 294-unit
body anatomy. Giving each pool a separate anatomy is not equivalent: motor
batches carry the anatomy fingerprint and the body correctly rejects a partial
anatomy as a different body configuration.

Remove only the total-contact-count equality. Each owned cell still requires
exactly one contact with its original source instance, neuron and projection.
The session, actual neuronal epoch, quiet prehistory and single attachment
checks remain. Each cell's existing lane receives the complete body anatomy;
the Avatar receiver continues to take the minimum completeness time over every
declared source. Another pool's absence cannot be interpreted as silence.

No neuronal, receptor, learning, muscle, body or clock equation changes. The
existing 256-cell per-pool limit is unchanged; distinct pools can serve a larger
body. This is an isolated ownership integration change, not live promotion or
proof of physiological recruitment.

Nine new controls cover two pools of either one or 147 cells, one final missing
cell holding back body time, exact replay, missing/duplicated/altered owned
axons, foreign acknowledgements, an unattached declared pool, and rejection of
per-pool anatomy at a whole-body receiver. The receiving motor cells evolve
under explicitly prescribed quiet source completion; no input spikes are
fabricated or labelled as observed neuronal firing. The controls run with the
155 original motor/receptor/clock/transport regressions (164 total).

Use `fresh_motor_epoch_study.py prepare --shared-motor` and its frozen visible
PowerShell launcher. A clean pushed commit, exact test inventory, source change
declaration, selected runtime and D: backups are required. Authoring compilation
and syntax/inventory checks execute no tests. The pending `a212b5` study is
untouched, and no pending results are read during this work.

After both reports, evaluate the actual active-population accuracy and shared
ownership independently before building the neuronal-to-named-muscle/native
experiment. The next named-muscle package must explicitly declare this ownership
change and carry its reviewed regressions; its current older source-delta guard
intentionally does not silently accept the new motor owner. Standing still
requires the complete sensory/neuronal/muscle/body loop and calibrated anatomy.

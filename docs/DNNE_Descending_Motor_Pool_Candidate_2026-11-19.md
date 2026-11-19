# Descending motor-pool owner — work in progress

`tools/NativeMotorBridgeProbe/DescendingMotorPool.cs` is an isolated receiving
owner for the existing detailed receptor-driven motor cells. Eighteen dedicated
controls are authored and the test project builds with zero warnings/errors.
They have not yet run. No live service, editor or body selects this owner.
The prerequisite recurrent retry `a974ff` has now been reviewed: all 146 cases
pass, full observed cell states agree exactly across packet schedules, and all
original routed events reconcile with actual delivery or retained future events.

The owner declares each presynaptic source, original projection, physical
receptor contact and separate motor-cell identity. It consumes existing v2
projection packets without changing original target names or chemistry. An event
with no declared contact rejects the whole packet. Partial actual admission
failure retains its prefix and stops the pool. Several declared boutons may
receive one original axon event, but each needs its own physical contact identity.

Each motor cell uses the existing `ReceptorMotorSource`, receptor equations,
compensated membrane integration and cubic crossing observer. Only that cell's
observed crossings enter `ReceptorMotorLane`; generic nucleus messages are not
converted into motor-axon events. Output uses Avatar's current motor-axon protocol
and requires actual downstream receipts before the pool releases its frame.
Future receptor events retain their original packets after earlier frames finish.

Fresh motor cells currently start at neural time zero. A later body epoch requires
the cells to have actually evolved through their quiet prehistory; attachment
checks their current time and emitted history and does not reset them. Every cell
has a distinct source instance compatible with the existing single-cell motor
lane. The pool waits for all declared incoming source clocks and preserves fixed
motor sample grids independently of packet boundaries.

The prospective workflow contains 164 cases: 18 new controls plus all 146
unchanged recurrent/source/recipient controls. `descending_motor_pool_gate.py`
requires the operator-completed, verified, D-backed `a974ff` review before
preparation. It protects 158 prior source/control files, including the original
motor channels, membrane, receptor, crossing observer and Avatar receiver. The
visible `start-descending-motor-window.ps1` retains both original recurrent
comparison traces and all test results and backs them up on completion/failure.
Its actual preparation and launch must be recorded separately.

New controls cover missing companion inputs, exact sample-by-sample equivalence
with the existing unwrapped motor-cell path, two original sources converging on
one cell, distinct cellular targets, several boutons receiving one original
event, rejected target/chemistry changes, exact replay, output backpressure,
retained future arrivals, packet partition, partial admission failure, actual
Avatar receipts for every motor cell, quiet prehistory at body attachment,
bounded output continuation, detached snapshots and single-thread ownership.
Only transport packet counts are omitted from the packet-partition comparison;
all neuronal fields, receptor states, emitted events and numerical ledgers remain
exact. The prescribed source fixtures do not establish original source firing,
motor firing, muscle force, independent coupled accuracy or physiological tuning.

Still required before connected use:

* Freeze, preserve source/runtime, back up and run the 164 controls visibly, then
  review the operator-reported final results before interpreting them as passed.
* Declare the actual descending source anatomy before neuronal emission. The
  generic reticular/vestibular preferred target currently falls back to an
  intralaminar target name. The pool deliberately cannot repair that by renaming
  an already emitted event. An explicitly declared fresh output map or an
  anatomically specified projection must supply the correct motor contacts.
* Integrate the actual source owners and body epoch, then assess independent
  coupled accuracy, physiological firing, muscle force and moving-body feedback.

Reference motor-cell and receptor parameters are not yet human spinal/Avatar
calibrations. The new owner does not change them or claim that its presence makes
the biological motor loop usable.

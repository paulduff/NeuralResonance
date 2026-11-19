# Descending motor-pool owner — work in progress

`tools/NativeMotorBridgeProbe/DescendingMotorPool.cs` is an isolated receiving
owner for the existing detailed receptor-driven motor cells. It is authored and
builds, but it has no completed dedicated tests or qualified integration rig.
No live service, editor or body selects it. Its development was interrupted to
review the recurrent circuit's reported comparison failure; finish that review
and qualification before extending the connected motor path.

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

Still required before any use:

* Add dedicated original-payload, missing-input, multiple-cell/contact, partial
  failure, future-release, packet-partition and actual body-receipt controls.
* Check exact state equivalence with the existing unwrapped motor-cell path.
* Freeze, preserve source/runtime, back up and run those controls visibly.
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

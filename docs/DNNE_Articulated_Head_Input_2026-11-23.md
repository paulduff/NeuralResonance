# Acknowledged full-body head input — candidate

The full-body tendon bridge retains native body/contact/head measurements but
had no adapter connecting those head samples to Avatar's existing skull
kinematics. `NativeArticulatedHeadInput` now supplies that connection on the
original physical owner thread. It issues no physical command and synthesizes
neither acceleration at the initial endpoint nor an afferent event.

Attach to the fresh body at physical step zero. Read its acknowledged baseline,
perform the declared passive initialization, and read step one. Both samples
retain their native JSON/hash, the complete body/contact observation envelope,
body-state hash and original physical clock. The first derivative uses those
two original samples and is available only at the later endpoint. The existing
head-to-canal input can consume the typed `AvatarSkullObservation` without
renaming its sample identity or resetting the physical epoch.

The adapter checks all three head material points, their original hierarchy,
exact endpoint and source identity. Native float position, rotation, angular
velocity and COM velocity must equal the corresponding exact values in the
acknowledged body state (numerically equal signed zero is accepted). Decoded
observations retain the existing JSON-to-double representation. Redundant
point velocity must satisfy the original `native_skull_gate.py` observation
bounds: 1e-5 m/s absolute plus 1e-6 relative. The unchanged skull adapter owns
world-basis, material-point and acceleration calculations.

Inspecting the same endpoint returns its original receipt. Missing endpoints,
changed samples, malformed material points, another thread and an uncertain
physical command cannot manufacture new sensory progress. On a failed later
sensor, the last complete frame, attempted acknowledged reply and successfully
consumed sensor prefix remain available; the epoch cannot replay or reset.
Returned records and collections cannot mutate retained observations.

Sixteen authored `NativeArticulatedHeadInputTests` use a prescribed full-body
protocol peer. They cover baseline/prelude continuity, byte preservation,
nonmoving inspection, foreign clocks, late attachment, gaps, partial failure,
pose/velocity/basis/scale checks, detached results, uncertain commands and
original-thread ownership. They execute no Unity body or afferent neuron.
`fresh_motor_epoch_study.py prepare --head` declares these plus the 315 earlier
checks: **331 cases in 210 methods**. Run only in the independent visible
PowerShell workflow. No pass is inferred from source compilation.

The motor attachment prerequisite `526071` has been reviewed: all 315 exact
cases pass; both 2,804-file copies, frozen source, binaries and the unchanged
335-file selected runtime verify. Its four-file review plus manifest is D:
verified. That result qualifies the protocol/clock controls, not this new
adapter or native initialization.

Native delivery still needs its actual Unity file-bridge study. A later joined
run must retain the real sensory trajectories, nucleus events, descending
receptor admissions, detailed motor-cell crossings and body acknowledgments.
The generic nucleus output is not a substitute motor spike. No production
host/editor selects this candidate, and no claim of physiological firing or
standing follows from these controls.

The existing component fixtures also repeat a tibialis muscle definition; they
are not anatomical full-body muscles. In particular its reference tendon length
is 0.28875 m, while the synthetic opposing-line attachment fixtures start near
0.2 m total path length. Do not adopt that combination as anatomical activation
or compensate with a neural gain. Real muscle/path geometry and initial state
need explicit consistent definitions before full-body muscle calibration.

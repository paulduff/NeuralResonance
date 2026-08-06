# Entry 089 - Delete Avatar Scalar Agency

Date: 2026-08-06

## Purpose

This rung removes the avatar-side conventional cognition that remained after
DNNE became the sole cognitive authority. The avatar is now a peripheral
transport and actuator boundary, not a second mind between the brain and the
world.

## Deleted Host Authority

The following avatar-owned behavior has been physically deleted:

- scalar attention selection and attention output;
- affective weather, arousal, gesture, body-sound, and voice synthesis;
- host needs and rhythm computation;
- contact, pain, and sudden-sound reflex steering;
- conventional sensation, body-event, and place memory;
- host-authored self diagnostics derived from those stores;
- semantic tool signals and the world's semantic dig/build actuator;
- body-state input to the motor interpreter;
- idle motor fallback configuration;
- direct public APIs for adding or setting motor drive.

The removed reflex path was an authority violation because it could scale
forward speed and add a turn bias after neuronal motor decoding. Body pain,
contact, threat, hunger, auditory input, and outcome telemetry can no longer
modify movement inside the avatar service.

## Preserved Peripheral Boundary

The avatar service retains only infrastructure that a simulated body requires:

- bounded transport of auditory, body, outcome, object-receptor, and sight
  packets;
- coalescing of high-rate sight frames;
- integration of numeric motor-population spikes;
- bounded motor-drive decay as physical spinal persistence;
- conversion of bilateral drive into configured body kinematics;
- reset and lifecycle handling;
- read-only queue, worker, motor, and frame telemetry.

Auditory and object packets are transported in producer order. The avatar no
longer ranks them by intensity, salience, confidence, danger, or semantic
label. Selection and attention must emerge in neuronal circuits.

## Motor Invariant

`AvatarActionOutput` now contains only movement, emission time, and source.
Movement is the kinematic projection of the latest neuronal left/right motor
drive. No avatar memory, need, mood, reflex, body state, outcome, object label,
or auditory label participates in that projection.

The kinematic coefficients and simulator collision response remain physical
body/world parameters. They may map or constrain a neuronal command, but they
cannot choose its target, direction, or purpose.

## Editor Boundary

The editor's former `Avatar Self` panel has become `Avatar Transport`. It
reports worker counts, queue pressure, motor-spike counts, bilateral drive,
projected movement, and vision queue depth. It no longer presents a host mood,
need, attention target, action interpretation, sensation narrative, or body
event as if it were neuronal state.

## Causal Invariants

- Only numeric neuronal motor population spikes can create motor drive.
- Empty ticks never synthesize movement.
- Semantic motor and tool names create no body drive.
- Body and outcome packets cannot change bilateral drive or projected motion.
- Auditory intensity and object salience cannot reorder avatar transport.
- Scalar cognition types and direct motor-injection methods are absent from
  the avatar assembly.
- Missing neuronal evidence produces stillness, not a host fallback.

## Population Size

No neuron-count change was justified. The defect was duplicate host agency,
not measured neuronal capacity. Future resizing requires firing, collision,
capacity, lesion, or embodied qualification evidence.

## Verification

- Control Program Release build: passed with zero warnings.
- SimAvatar Release build: passed with zero warnings.
- WPF editor Release build: passed with zero warnings.
- Maze simulator Release build: passed with zero warnings.
- World simulator Release build: passed with zero warnings.
- Tests: 305 passed, zero failed, zero skipped.
- Cortical functional benchmark: PASS, 100% overall and in every category.

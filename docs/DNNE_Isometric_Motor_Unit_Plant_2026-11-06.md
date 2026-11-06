# Isolated axon-to-motor-unit connector

`AvatarIsometricMotorUnitPlant` joins the separately checked motor-axon receiver
and slow-unit calcium/force component. It is an isolated fixed-length, zero-
velocity reference plant, not the live Avatar controller. The active motor-cell
experiment is still awaiting the operator's report; no neuronal source is
qualified or selected by this increment.

Before adding this component, the existing `AvatarNervousSystem`, `AvatarMuscle`,
motor publisher/receiver, calcium consumer and Unity migration audit were inspected.
The live population-command interpreter, joint controller and physical muscle
remain on their existing path. Their replacement still requires a qualified
neuronal source, sensory/spinal circuitry and dynamic force/load validation.

## Event and time contract

Every anatomical motor unit requires an explicit parameter set. Missing and extra
sets fail construction. The model fingerprint includes anatomy/delays, per-unit
parameters, calcium version, scheduler version and solver step; input dictionary
order does not change it. The clock/session is reported separately. No checkpoint
or automatic epoch migration is provided.

The receiver retains its original packet, event-identity, delay, replay and
completeness rules. The plant accepts no population rate, signed motor drive,
joint target or generated event count. Missing source traffic cannot establish
silence. All declared sources must prove completeness before physical advancement.

Output endpoints are explicit integer ticks on a fixed grid anchored at the
body epoch. The default step is 0.125 ms, with declared binary refinements down
to 0.125/65536 ms. Unrepresentable clocks/ticks are rejected, never rounded into
a new clock. Future world integration must explicitly reconcile its time domain;
this is not permission to retime incoming spikes to frame boundaries.

Each unit integrates separately over that grid, splitting only at its own actual
arrival times. An arrival is delivered at its original mapped body time, with
conduction delay applied once. Events for other units do not split its solver.
Packet and frame partitioning therefore use the same unit-specific sequence of
numerical steps. No force is read before the requested complete input horizon.

The returned step retains the original arrivals and immutable unit snapshots:
calcium, release, activation, force, accounting, event count and last message ID.
It applies no additional activation filter, fatigue law, force summation, moment
arm or torque. Existing `AvatarMuscle.Advance` must not be placed after this unit's
activation as a second excitation filter during future integration.

Malformed packets and inadmissible advance requests fail before changing muscle
state. A numerical failure during advancement is fail-stop: already consumed
arrivals and partial unit states remain visible, the last fully completed tick
is retained, and further input/advancement is rejected. There is no partial retry,
reset, silent packet loss or synthesized force. Callers must serialize access.

## Checks and current status

The operator-reported `5836ab` run has now passed all 61 selected cases, with
verified complete C:/D: captures. See `DNNE_Isometric_Motor_Unit_Results_2026-11-06.md`.
The following describes the declared rig and its limits, not neuronal qualification.

The Release test-project build passes with zero warnings/errors. Fifteen focused
cases cover original fractional arrival timing/identity, direct-consumer parity,
exact frame and packet invariance, isolation from unrelated unit traffic,
multi-source arrival order, replay, missing-source completeness, rewind/clock
rejection, malformed-packet atomicity, explicit parameter coverage, proven silence,
snapshot isolation, numerical fail-stop and model fingerprints.

These tests use declared event fixtures; they cannot prove neuronal firing. The
visible PowerShell package selects those 15 cases plus the 32 transport, 13 calcium
and one solution-manifest case: **61 required cases**. The full DNNE suite is not
selected; its previously recorded consolidation failure remains open. No test
outcome may be claimed without the operator's report and saved-result review.
That review has now completed for `5836ab`. Source/binaries/results are frozen and
backed up to D: without live monitoring.

Use `prepare-motor-axon-verification.py --motor-unit-plant` and the frozen motor
window launcher. Its window is titled **DNNE isometric motor-unit integration**,
distinct from the pending active motor-cell comparison. Neither running package
is modified by this work.

## Remaining integration

The next coupled experiment must use original membrane-generated events through
the publisher, receiver and these motor units, with causal disconnection and
source-silencing controls. It must wait for source numerical/physiological review;
packet validity is not biological authenticity. Endplate success is still assumed
by the calcium component; muscle action potentials, dynamic tendon/length effects,
metabolic supply and whole-muscle recruitment remain unqualified.

Avatar's vestibular, plantar and proprioceptive transducers are preserved. No live
Avatar, Unity player or editor is restarted; worlds keep paused startup. Actual
promotion must update the canonical body, Unity generator, activity/authority
display and save/version rules together. Isometric fixture success cannot qualify
balance, recovery or a usable Avatar.

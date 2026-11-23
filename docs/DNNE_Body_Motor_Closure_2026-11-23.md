# Body and motor interval closure — 23 November 2026

## Completed component review

Paul reported `motor-closure-20261123-ab2502` complete (its repeated footer was
the same run). The read-only review verifies both 2,792-file copies, full frozen
source at `188aef7d6fd443c92aee351cdb6bbf8e1a9ff69c`, executed binaries, exact
243-case TRX inventory and 335 selected runtime files unchanged before/after.
All fourteen new scheduling controls and 229 earlier cases passed. The four-file
review and its D: backup are `artifacts/motor-closure-20261123-ab2502-review` and
`D:/DNNE-desktop-runs/UnityWorld/motor-closure-20261123-ab2502-review`.

No test/model was rerun. This qualifies the scoped component scheduling checks;
the prescribed packet fixtures are not active sensory feedback or standing.
The earlier launch/unrun statements below record authoring history.

## Source finding

`AvatarCausalMotorPlant.TryAdvance` waits for every motor source to be complete
through the next physical endpoint before it advances the body. In contrast,
`AvatarCanalAfferentInput.AdvanceNext` requires a completed mechanical interval
and refuses to fill unknown future input. Its midpoint forcing uses the retained
completed mechanical trace. Directly connecting these two owners in a loop can
therefore create a circular wait: physical advance awaits motor output, motor
output awaits sensory processing, and sensory processing awaits physical advance.

This is a source-level integration finding, not a diagnosis of the pending
native tendon experiment `e764e3`. That frozen package and its outcome remain
untouched; no post-launch output or process has been inspected.

## Explicit two-phase candidate

The existing first-order body/muscle partition already applies **start-state
tension** during a body interval. Events later within that interval only change
subsequent tension. The new explicit `CausalMotorSchedule.BodyThenMotor` permits
the same calculation in two phases:

1. `TryAdvanceBody` validates the current owner, geometry, clock and muscle
   substeps; applies the already-known start tension once; retains the actual
   completed body receipt for sensory processing. It does not consume events,
   evolve muscle state or claim neuronal silence/completeness.
2. The caller processes that body observation through its sensory/brain/motor
   owners. `TryCompleteMotorInterval` waits for every declared source, then
   consumes the original events and evolves the original muscle equations at
   their original event/substep boundaries. It closes the shared endpoint and
   permits the next physical step.

Only one body interval may be pending. Repeated begin calls return the same
receipt without moving again. A missing/uncertain physical reply faults the
owner and cannot be resent. External body changes before closure fault before
event consumption. Pause retains the pending step; resume validates the actual
advanced endpoint. A caller cannot use the atomic API on a split owner or the
split API on an atomic owner.

The default remains `MotorBeforeBody`, with its original fingerprint expression
and admission order. A split owner has a distinct fingerprint. The biological
muscle/event calculation is shared by both paths without changed equations,
gains, thresholds or time mapping. No pre-existing cell is reset or retimed.

`State.CompletedPhysicsStep` and `State.BodyTimeMs` describe the last **closed
coupled endpoint**. During a split interval `State.ObservedPhysicsStep` and
`PendingStep.After` expose the real advanced body endpoint. `CaptureMuscles`
still describes the earlier closed muscle state. A future live host/editor must
show that distinction; it must not present a pending receipt as a completed
brain/body step. No production host or editor selects this isolated mode yet.

This remains a first-order numerical partition. A later response never rewrites
the completed physical trajectory. The closed loop's partition error, active
sensory physiology and full coupled accuracy still need independent assessment.
The body prelude/attachment baseline (neural 1010 ms maps to physical 10 ms in
the current sensory fixtures) remains separate work; this change does not solve
it by mapping the source to physical zero or inventing past motor silence.

## Authored verification

`ClosedLoopMotorScheduleTests` has 14 cases, using actual motor transport,
canonical dynamic muscles and an analytic constant-inertia body. Inputs are
explicit prescribed packets, including one generated only after a completed
body observation; that fixture is not a sensory neuron.

The tests cover initial pause/no invented completeness, bounded physical lead,
all-source completeness, replayed packets, exact original-partition comparison
over 800 active steps at two muscle subdivisions, preservation of late/endpoint
events, pause/resume, out-of-band motion, lost receipts, API misuse and immutable
pending receipts. The full test project builds with zero warnings/errors at
`D:/DNNE-build-work/motor-closure-authoring-20261123`, with its sibling build log.
No model, test or numerical comparison ran in that authoring build.

The visible frozen workflow is selected using
`tools/fresh_motor_epoch_study.py prepare --closed-loop`. Its exact inventory is
**243 cases**: the 14 new controls plus 229 previous tests (155 clock/receptor/
transport/descending cases, 27 causal-body cases, 16 dynamic-muscle cases and
31 muscle-path cases). It uses the existing visible runner with isolated state,
separate binary manifests, selected runtime preservation and verified D: backups.

Passing would qualify those scheduling controls and regressions. It would not
prove an active brain–body loop, independent coupled accuracy, physiological
firing, standing or recovery. Record the frozen launch separately and await
Paul's final footer without monitoring.

## Launch record

`D:/DNNE-build-work/motor-closure-20261123-ab2502` is frozen at pushed revision
`188aef7d6fd443c92aee351cdb6bbf8e1a9ff69c`. Frozen preflight and both initial
2,203-file copies plus manifest verify. Interactive visible task
`DNNE-Motor-Closure-cc5670ccc12e` started on 23 November. No post-launch output,
status or process was inspected. Exactly two operator reports remain pending:
this scheduling run and native force observation `e764e3`. Launch is not a
passing result. Both studies retain their own frozen source and D: backups.

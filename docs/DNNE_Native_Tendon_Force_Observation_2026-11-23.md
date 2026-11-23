# Native tendon force observation — 23 November 2026

## Why this is next

Completed full-body tendon run `bb320d` reconciled the requested point forces
but demonstrated only tiny positional changes. Its saved left-elbow endpoints
also show near-zero joint motion: quiet joint position 1.0890847945699278e-10 rad,
flexor -5.990266771505048e-8 rad, extensor 3.93688651456614e-7 rad after eight
5 ms float-defined steps. These are decoded stored values, not a new model run.
The stored geometry has drive stiffness, damping and force limit all zero.
That rules out an enabled old spring drive in this capture, not every possible
constraint, force-application issue or attachment defect. Cause is unresolved.

Prescribed mechanical forces bypassed the brain entirely. Increasing brainstem
complexity cannot explain this particular observation. Continue developing the
biological pathway, but first establish useful physical response to its force
boundary. The full host/body wire and real attachment anatomy remain unfinished.

## Measurements and authoritative API semantics

`NativeTendonForceTrace` is opt-in through `--tendon-observe-native 1` in the
isolated full-body tendon qualification. It records before any application,
after each nonzero point force, after all 76 applications, and after physics:

- All 15 body identities, native coordinate indices/ranges and sleeping flags.
- World centres of mass, force/torque accumulators, joint positions/velocities.
- The complete reduced-coordinate counteracting force vector.
- Exact IEEE-754 values and the actual supplied simulation step.

Unity documents world-space force/position inputs and force/torque accumulation
for [AddForceAtPosition](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/articulationbody/addforceatposition).
[GetAccumulatedForce](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/articulationbody/getaccumulatedforce)
reports the force before simulation, with accumulators reset during a step.
[GetJointExternalForces](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/articulationbody/getjointexternalforces)
reports **counteracting** reduced forces; do not label that vector as the tendon
torque or use it as a replacement motor command. All calls explicitly use the
next physics timestep. No force mode is mixed or replaced, and no getter output
is fed back into the simulation.

## Prospective paired protocol

The new package freezes the completed original quiet and all 38 pulse captures,
checking each copied file against both completed protected copies and retaining
the original manifest. It freezes clean pushed source and the installed licensed
rig. Canonical source/neuronal kernels and all tests must be unchanged from
`7231701`; only this isolated measurement/runner and result review work is added.

Run the original exact 112 component tests, then **78 fresh native cases**:
quiet and every 40 N tendon pulse, each once unobserved and once observed. Every
case uses eight original physical steps: **624 steps / 702 endpoint records**.
Keep the original force magnitude, float timestep, geometry, damping, joint
limits, gravity and collision setup. There is no pose reset, increased strength,
direct joint-torque substitute or hidden standing controller.

Requirements, declared before capture:

1. All 112 exact tests and all 78 isolated cases complete.
2. Every original state, path, applied-point record and contact measurement is
   identical to its saved original at the same endpoint.
3. Observed and unobserved variants have identical physical evidence.
4. Native force increments equal the sum of actual float point forces; native
   torque increments equal their moments about the captured centre of mass.
   Absolute allowances are 2e-4 N and 2e-5 Nm, plus 2e-5 times the largest expected
   vector component. These cover a new float API observation only; no earlier
   scientific acceptance bound changes.
5. Before application and after physics, no queued force/torque remains within
   those declared observation allowances. Retain all values, including failures.

The analysis writes every native accumulator comparison, including failed ones,
and reports target-joint angle/speed differences from quiet. These are diagnostic
response magnitudes, not a newly invented movement pass threshold. If state
identity or accumulator checks fail, retain all output and fail the run. If they
pass while joints barely move, investigate constrained-body response next;
do not declare practical muscle control from successful accounting.

All 37 runtime C# sources API-compile against installed Unity assemblies with
zero errors and seven existing reflection-field warnings at
`D:/DNNE-build-work/tendon-force-trace-authoring-20261123-final`. That build executed
no Unity model or test. A visible run, frozen initial D: backup and final result
review are still required. No live host/editor selects this measurement mode.
Partial native observations are retained in the final case report if a later
measurement or physical step fails; the owner fault still prevents resending.
Static Python/PowerShell parsing, Unity metadata and the exact 112-test /
78-case inventory checks passed. These executed no scientific comparison.

The complete biological goal remains unchanged: original neuronal motor
authority, active sensory return, balance, falling/recovery and basic movement.

## Launch record

Frozen package `D:/DNNE-build-work/tendon-force-20261123-e764e3` uses pushed
revision `740fa633dcc92a1c5c537c44ebd27e08c8ba20b2`. Frozen preflight passed;
both initial 2,286-file copies plus manifest verify. Interactive task
`DNNE-Tendon-Force-c574d7b01ac3` started the visible workflow on 23 November.
No post-launch output/status/process has been inspected. This is the sole
pending final report; await Paul without monitoring. The clock `6889f0` and
original tendon `bb320d` results are fully reviewed, with verified D: reviews.

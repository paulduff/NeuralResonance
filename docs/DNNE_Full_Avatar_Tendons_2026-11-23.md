# Full Avatar tendon mechanics — 23 November 2026

## Actual launch

Package `D:/DNNE-build-work/avatar-tendons-20261123-bb320d` is frozen at pushed
`218054afac3165edbb1cac7fdd0a98629d786f95`. Read-only frozen preflight passes;
both initial 2,194-file copies plus manifest verify. Interactive task
`DNNE-Avatar-Tendons-d6f2862dfbd1` started its visible independent PowerShell
window. Exactly one report is pending: this package. No post-launch output,
status or process has been inspected. Await Paul's final backup/footer, with
no live monitoring or duplicate launch. No test/native result is inferred from
launch. The rest of this document retains the prospective protocol.

Paul resumed the goal after Friday's checkpoint. The next isolated native
study checks the physical boundary on the existing complete Unity rig, before
connecting `AvatarCausalMotorPlant` to it. No previous result is pending.

## Candidate and scope

The 15-link rig exposes 19 joint axes and 38 antagonistic muscle groups. The
canonical `AvatarMusclePath` and causal muscle/motor implementation were inspected
and remain unchanged, as do all neuronal equations and learning. Existing
38-group profiles supply the diagnostic moment-arm radii; the four axial groups
not yet represented in Unity are still absent.

The isolated owner now records supported native collider geometry, local shape
transforms, materials (including explicit engine-default selection), contact
offsets, filtering, collision exclusions, body inertial parameters, velocity
limits, joint drives and relevant global physics settings. Exact bit strings
retain physical scalars. Other dynamic bodies and unsupported collider kinds
reject. This is a scoped identity check for the pinned qualification scene,
not a general exporter of arbitrary Unity physics internals. Bind errors caused
by changed physical state now latch the owner fault.

`ArticulatedTendonQualification` runs only with both the explicit isolated
`--neuronal-muscle-qualify` mode and a declared `--tendon-case`. Ordinary live
stepping and the existing host route do not select it. No DNNE connection,
motor-cell execution, muscle activation simulation or standing controller runs.

Each diagnostic path has a parent-body origin and child-body insertion. Their
initial positions straddle the actual joint anchor by 0.1 m in one transverse
direction, with the existing profile's radius as the perpendicular offset.
Opposing signs give opposite initial moments about the declared physical joint
axis. These are **mechanical test attachments, not calibrated anatomical
origins/insertions**. They must not be promoted to the live body as anatomy.

All paths use existing uniform tendon tension and point-force computation.
The actual native `AddForceAtPosition` calls receive its float-boundary forces,
without another activation/fatigue multiplier, target-pose controller or direct
joint torque. Joint limits, body damping, gravity and contacts remain active.

## Prospective visible workflow

First run the unchanged exact 112-case inventory: 31 path geometry, 27 causal
motor plant, 16 dynamic muscle, 12 balance evidence, nine standing trial,
16 body sensor and one solution manifest case. Build a fresh Unity player from
frozen source in a separate writable workspace. Preserve imported build assets,
settings, player binaries and dependencies; the build cache stays outside the
evidence package.

Then run 51 fresh isolated native processes at a declared float 0.005 s step:

- One zero-tension case, eight steps.
- One zero-tension falling/contact case, 100 steps. Require more than 0.02 m
  pelvis drop and nonzero measured foot contact; this is not a recovery test.
- All 38 muscle groups individually at prescribed 40 N, eight steps each.
- Eleven rejection cases: wrong step, endpoint hash, plant identity, repeated
  effort, missing tension, negative tension, nonfinite tension, changed collider,
  changed gravity, changed contact material and changed collision exclusion.
  Require no physical advance or endpoint change on rejection and a latched
  fault. The repeat case first performs one valid zero-tension step.

Expected total: 413 physical steps and 464 endpoint records, in 51 JSONL files.
Every endpoint retains all 15 body states, 38 path measurements and all applied
point forces. Exact bit encodings protect clocks, positions and requested/applied
forces from the previously observed decimal JSON decoding issue.

The independent saved-data reader checks complete case and endpoint counts,
clock arithmetic, state hashes, finite body values, path length and velocity,
each initial signed moment arm, and all 76 application points for every step.
Requested-force absolute allowance is 1e-9 N; float conversion must match
exactly. Initial moment-arm allowance is 2e-6 m for the native float geometry.
Matched pulse/quiet comparisons require identical initial physical state and
report each final position difference. At least one must exceed 1e-8 m; locked
joint limits can legitimately prevent some individual motions. Do not call this
all-axis response, trajectory accuracy or control qualification.

All bounds are prospective. A failed case stops the workflow and retains its
inputs, native report, logs and prior endpoints. Completion, mechanical checks,
anatomical calibration, neuronal authority and standing remain separate flags.
No coupled independent ODE trajectory comparison is part of this first study.

## Preparation and remaining work

`native_avatar_tendon_study.py` freezes clean pushed Git source plus the two
already installed attributed Mike Alger model assets, checking their original
provenance hashes. It retains exact test identities, protocol, limits, pinned
Unity executable, arithmetic/serialization Python dependencies and .NET host.
The visible runner records/checks managed runtime and executed binary hashes,
verifies original package files before/after, and makes the final verified D:
backup. It stays open after its footer. Never monitor after launch; await Paul's
report before inspecting result or process state.

Authoring checks: all 36 Unity runtime C# sources API-compile with zero errors
and seven existing JSON/reflection warnings. Python/PowerShell syntax, Unity
metadata and static 112-case inventory pass. These checks executed no model,
native physics, test or numerical analysis. The initial API build error from
using a nonexistent layer-mask accessor was corrected to the actual pairwise
layer-exclusion query; the failed build remains preserved. Final authoring build:
`D:/DNNE-build-work/tendon-authoring-compile-20261123-final`.

After the native result is reviewed, finish the exact mechanical wire/receipt
adapter for `IAvatarMotorMechanicalBody`, with original causal muscle state,
motor-axon identity and the physical clock. Real attachment anatomy and whole
body trajectory qualification remain required. The active head/sensory response
also still needs independent trajectory/event-time checks and its clock join to
receiving brain populations. No live host, brain save or old failure is changed.

## Completed native result and review

Paul reported `D:/DNNE-build-work/avatar-tendons-20261123-bb320d` complete on
23 November. Frozen revision: `218054afac3165edbb1cac7fdd0a98629d786f95`.
`tools/review-avatar-tendons-result.py` verified both 3,057-file copies plus
manifest, full Git archive identity and extracted source, all executed test and
player bytes, the exact 112 passed tests, 51 native reports and 464 endpoint
records covering 413 physical steps. Canonical `src`, `Structures` and `tests`
were unchanged from the pre-study checkpoint. The original runner verified its
3,099-file managed runtime manifest before completion; the result review did
not rehash today's external runtime installation. No test/model or full force
analysis was rerun in review.

All 11 invalid-command cases rejected without an extra physical step or altered
body state and latched the owner fault. Reported maximum force arithmetic error
was 1.4210854715202004e-14 N, within the original 1e-9 N limit. Requested doubles
and the actual float inputs to Unity remain preserved in every step.

The position comparison is much weaker evidence of useful movement: 36/38
prescribed paths exceeded the original 1e-8 m observation threshold, and the
largest difference from quiet was only 1.379847526550293e-5 m (about 14 micrometres).
Neck-pitch extensor (1.862645149230957e-9 m) and trunk-yaw flexor
(1.3969838619232178e-9 m) did not exceed it. The original criterion required
at least one observed response, not useful or bidirectional control of every
joint. Do not change that criterion retrospectively, infer anatomical correctness
or treat small float-level differences as practical muscle authority. Their
cause and meaningful joint response require a separate prospective study.

The passive 100-step fall changed pelvis height from 0.8897284865379333 m to
0.32255610823631287 m, with peak foot contact 11307.396484375 N. This confirms
gravity/contact in this fixture; it is not standing, calibrated injury or recovery.

Review: `artifacts/avatar-tendons-20261123-bb320d-review`, with four files plus
manifest verified at `D:/DNNE-desktop-runs/UnityWorld/avatar-tendons-20261123-bb320d-review`.
No neuronal source executed. Synthetic attachments, independent whole-body
trajectory accuracy, real muscle recruitment, anatomy and standing remain open.
Before the mechanical bridge is joined to the sensory/descending chain, run
the fresh motor epoch regression described in `DNNE_Fresh_Motor_Epoch_2026-11-23.md`.

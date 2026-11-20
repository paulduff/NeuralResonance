# DNNE checkpoint — 20 November 2026, evening

Paul requested notes for tomorrow and a pause. Commit/push this coherent
checkpoint, pause the goal and stop; resume only on his instruction. This
supersedes today's earlier harness checkpoint and all pending-launch notes.
There are **no outstanding completion reports**. No new test, model, reference
solve or Unity execution was launched during the completed review/closing turn.

## Completed head/sensory result

`D:/DNNE-build-work/head-turn-reanalysis-20261120-2859f6` finished successfully
at frozen revision `44199c0b526a8b7f863697a5bcd4fc5d92a92f4a`. Both final
2,776-file copies, exact frozen source, executed binaries and numerical/managed
runtime dependencies verify. All 37 standing/body measurement cases and 40
analytic controls passed. The original failed capture `2e0ed3` and both of its
2,933-file copies remain unchanged. Only the two explicit JSON naming-schema
comparisons changed; no physical value, neuronal equation or limit changed.

All six saved captures pass input/state/packet accounting and conservation,
covering 2,654,208 native steps:

| Prescribed motion | 16,384 steps per interval | 32,768 steps per interval |
| --- | ---: | ---: |
| Quiet | 0 nerve events | 0 nerve events |
| Positive yaw | 0 nerve events | 0 nerve events |
| Negative yaw | 1 nerve event | 1 nerve event |

Each negative-yaw event retains its original source/cell/session identity and
zero-crossing bracket. This establishes a recorded direction-dependent response
in the isolated sensory model. It does not yet establish independent active
trajectory accuracy, calibrated physiological firing, detection by the connected
brain, corrective muscle causality or Avatar balance. The older quiet midpoint
reference is not an independent qualification of these new active trajectories.

Reviewer `tools/review-head-turn-reanalysis-result.py` verifies complete bytes,
provenance, exact case inventories and saved criteria/counts/events. It does not
rerun full accounting or a numerical model. Its four files plus manifest are
preserved at:

- `artifacts/head-turn-reanalysis-20261120-2859f6-review`
- `D:/DNNE-desktop-runs/UnityWorld/head-turn-reanalysis-20261120-2859f6-review`

## Unity mechanics authoring checkpoint

The existing Avatar implementation and canonical force path were inspected:
`src/NRE.SimAvatar/AvatarCausalMotorPlant.cs`, `AvatarMusclePath.cs`, the native
hinge owner/bridge, whole-body physical sample owner, Unity muscle profiles and
body sensors. The new `ArticulatedMusclePathOwner.cs` extends the physical
boundary to the existing 15-link, 38-muscle-group Unity body. It is unfinished
and inaccessible from ordinary live stepping.

- Requires a fresh paused body and explicit `--neuronal-muscle-qualify` mode,
  all 38 declared paths exactly once, valid local points and stated provenance.
  It does not choose anatomical attachments or manufacture motor commands.
- Reuses the canonical polyline tension/point-force calculation. All forces are
  validated before application. Legacy joint drives and spinal/muscle-controller
  stepping are excluded from this isolated path; gravity, contacts, joint limits
  and native body damping remain physical mechanics.
- Captures actual path geometry, endpoint state/bit representations, plant/body
  identities and effort identity. Rejects stale state and repeated efforts;
  uncertain execution faults the owner instead of resending a force.
- Declared physical step controls simulation, contact impulse-to-force
  conversion and physiology. Default live 10 ms behavior is unchanged.
- Stale legacy muscle state is not published in direct-force mode. The future
  bridge must publish the actual dynamic muscle/calcium state. Existing balance,
  contact and body observations remain available.

Build-only validation used `tools/compile-unity-runtime.ps1`: all **34** Unity
runtime C# sources compile against installed Unity 6000.3.19f1 assemblies under
C# 9/netstandard2.1, with zero errors and seven existing CS0649 JSON/reflection
field warnings. This includes the standing observation glue, but is not a Unity
editor/player build or a native execution. Inputs were unchanged through the
build; exact compiled source copies were hash-checked afterward. Build log,
evidence, assembly, sources and compiler script have a verified 57-file backup:

- `D:/DNNE-build-work/unity-runtime-owner-checkpoint-20261120`
- `D:/DNNE-desktop-runs/UnityWorld/unity-runtime-owner-checkpoint-20261120`

Both paths are on D:, not independent physical media. No native force result is
claimed. The mode has no driver/coroutine, wire protocol, host bridge or attachment
manifest yet. Its current mechanics fingerprint does not cover every collider
and scene setting. Supplied tensions are not authenticated as neuronal output.
Do not launch it as a working standing test or promote it into live operation.

## Next work after explicit resume

1. Finish the physical boundary before the next native test: complete the scene/
   collider fingerprint; derive and review explicit attachment anatomy from the
   existing rig; build the exact wire/receipt adapter for
   `IAvatarMotorMechanicalBody` and the existing `AvatarCausalMotorPlant`.
   Retain original muscle force, motor-cell identity and physical clock. The
   existing two-muscle hinge validator is not suitable unchanged for 38 paths.
2. Prepare visible native controls for quiet/fall, path forces, stale/duplicate
   ownership rejection and contact-clock consistency. Preserve complete endpoint
   evidence compactly; do not return to hundreds of thousands of loose files.
   Compilation alone does not qualify these behaviors.
3. Independently qualify the active head/sensory trajectories and event timing;
   preserve the original input, loaded state and limits. Then connect actual
   afferent events to the receiving brain populations and detailed motor pool.
   Audit the clock join: current sensory capture starts at a nonzero neural
   epoch, whereas the motor-pool constructor expects zero. Do not silently
   retime/reset existing cells; verify the owning source before choosing a fix.
4. Retain matched quiet and interrupted-path controls before attributing muscle
   force to the original neuronal chain. The old live population-command route
   still exists and must remain explicitly distinguished in the editor/Unity.
   Standing/recovery requires the complete physical and neuronal feedback loop.

Scope remains biological DNNE, Avatar and World/Maze; LM/Entity/Dyad are parked
and CIV is separate. Preserve all saves, failures and original bounds. Tests,
models and comparisons run only in visible independent PowerShell windows, with
Paul reporting their final backup/footer; no live monitoring. World starts
paused. Regular coherent commits and pushes remain authorized.

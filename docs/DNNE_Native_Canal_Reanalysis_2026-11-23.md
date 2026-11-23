# Saved native body/sensory comparison repair — 23 November 2026

Paul reported the final FAILED footer for
`D:/DNNE-build-work/native-canal-20261123-184323`. All six native/sensory cases
completed. The analysis stopped at the first cross-session geometry/path
equality assertion, before checking the complete sensory trajectories.

The read-only review verified all 2,940 files in each original/protected copy,
the frozen Git source, executable hashes and retained runtime manifests. The
review is `artifacts/native-canal-20261123-184323-review`, with five files plus
manifest verified under `D:/DNNE-desktop-runs/UnityWorld`. It preserves the
failure and its operands. No model or full trajectory comparison was rerun.

All 54 saved body-state and applied-force payloads match the previous capture
exactly. All six case summaries report zero afferent events. That is evidence
of completed computation, not demonstrated sensory firing or motor authority.

## Cause and bounded repair

`ArticulatedMechanicsIdentity.Capture` includes `GetInstanceID()` in each
collider/body record. These are useful within one running scene, but Unity
explicitly states that instance IDs change between player/editor sessions:
[Unity 6000.3 Object.GetInstanceID](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/object/getinstanceid).

The first failed operand contains 30 changed instance IDs: 15 body colliders
and 15 articulation bodies. Four static collider identities remain the same.
For example, the pelvis collider is -846 in the new process and -838 in the
older process. The body fingerprint hashes the full geometry, so the 38 muscle
path fingerprints also change: each path includes that body fingerprint.
These identifiers cannot be required to match across independent launches.

`native_geometry_identity.py` is a saved-evidence comparator. It permits only
the explicitly named `instance` fields in the two known record collections to
differ across sessions. It requires unique nonzero integer IDs and exact,
ordered object names. After substituting just those ID tokens, all remaining
raw geometry text must be identical. This includes every collision shape,
transform, filter, material, mass, inertia, joint setting and attachment.

Each original body SHA and each path SHA is independently reconstructed from
its own unmodified geometry and declarations, using the original .NET binary
string/double encoding. Lengths, velocities, physical states and applied
forces still require exact equality. A changed ID within one session fails.
The live Unity fingerprint/identity code is unchanged; original raw IDs and
hashes remain in the evidence, with an explicit cross-session mapping.

The original `native_canal_analysis.py`, neuronal equations, source fixture,
forcing reconstruction and numerical limits are unchanged. The declaration
checker verifies the exact limited edit to `native_canal_accounting.py` and
every other original Python file.

## Independent PowerShell study

`native_canal_reanalysis.py` freezes a new source archive and numerical
environment. `start-native-canal-reanalysis-window.ps1` runs 25 identity
controls, then the original accounting of all 516,096 saved sensory steps,
1,008 packets and 162 head receipts. Controls include the real failed operand,
valid identity renumbering, malformed/aliased identity, and rejection of
changed mechanics, attachments, hashes, path measurements, state and forces.
The final preservation stage re-verifies both complete original copies.

There is no Unity relaunch, neuronal integration, reference ODE solve or
automatic physiological qualification. The window remains open after its
final backup. No live monitoring; Paul reports completion before review.

The unfinished `AvatarMuscleInitialBalance.cs` authoring draft is explicitly
excluded from the Git archive and is not compiled or executed in this study.
The earlier isolated-view UI repair is likewise not used to rebuild the
frozen player. These are separate work items.

At preparation, Python/PowerShell syntax checks pass; the 25 controls and full
reanalysis remain pending until the independent run completes. A successful
accounting result would still leave independent connected accuracy,
physiological firing, neuronal motor authority and Avatar standing unqualified.

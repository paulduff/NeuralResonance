# Full Avatar muscle bridge — 23 November 2026

Subsequent authoring adds a separately unqualified v2 passive initialization
route; see `DNNE_Passive_Motor_Attachment_2026-11-23.md`. The launch recorded
below remains the original frozen v1, with no post-launch inspection or edit.

The qualified hinge transport only represents two paths. The full Avatar owner
already accepts the existing 38 paths, but previously had no host implementation
of `IAvatarMotorMechanicalBody`. This candidate connects those two boundaries.
It does not change neuronal, receptor, muscle, attachment-force or physics
equations, and does not resolve the weak movement in completed `bb320d`.

## Native and host ownership

`ArticulatedTendonWire.cs` is compiled verbatim into the native runtime, host
probe and test assembly. It reuses the exact IEEE scalar encoding from the
existing hinge wire; the original hinge protocol and its clock are unchanged.
The full-body protocol has its own version and an explicit positive float
physics step, at most the existing 0.01f world step. Milliseconds are derived
from that actual float, never its nominal decimal spelling.

A frozen configuration declares the session, all 38 ordered muscle identities,
attachment provenance and local coordinates. There is no implicit attachment
generator in the bridge. The existing full-body owner checks those declarations
against the actual 19 groups and their links before claiming a fresh, paused
body. Configuration text, complete mechanical geometry, body state and combined
observations have separate hashes. All previous force paths and limitations
remain; declaring a path does not calibrate it anatomically.

Native selection is explicit: `--neuronal-muscle-qualify` with
`--tendon-bridge <session-N>` and `--tendon-config <configuration.json>`.
It cannot also select `--tendon-case`. Neither a production host nor the editor
selects this mode. The world stays paused; only the declared owner can step it.
The new `tendon-bridge` directory must be fresh. Hello, request, reply and failure
files publish once by rename, without overwrite.

The host's `NativeArticulatedBody` implements the actual causal plant interface.
It accepts only the current session, endpoint, bound plant and all ordered
tensions. A step applies existing start-state axial forces through the existing
native owner, once. Receipts retain the actual step, effort identity, all path
measurements, complete body state and point applications. No second activation
filter, strength gain, joint controller or conversion to a desired pose is added.
Host and native owners reject reused efforts. An uncertain reply faults the host
epoch; reading the same published file after a sharing violation never resends
the command. The last acknowledged step remains distinct from an uncertain
native advance, including in failure evidence.

Each completed endpoint captures the existing body encoding, all fifteen contact
records and the original centre/left/right head material points. This is a single
owned sample; inspections return its original bytes and receipt timestamps.
They do not recapture wall-clock timestamps and pretend to observe another step.
The host checks all sample counters and identities against the mechanical
receipt. It retains the original payload for subsequent sensory owners; it does
not itself generate afferent spikes. Existing contact clipping, reference fields
and body-encoding limitations remain. Direct-tendon body telemetry does not
replace the host's actual dynamic muscle state.

## Component verification

`ArticulatedTendonBridgeTests` has 34 authored cases against a deterministic
protocol peer. They exercise the real host adapter: complete force delivery,
exact bits, endpoint observations, malformed/missing/reordered commands, stale
or foreign receipts, old effort reuse, lost acknowledgments, immutable retained
state and declaration rejection. One case connects the real 38-path causal
plant and motor receivers to that peer, performs body-first scheduling, and
requires completion from every declared source before closing the muscle step.
Its quiet motor packets and repeated tibialis parameters are explicit component
fixtures, not anatomical calibration or neuronal-source evidence.

The frozen component workflow uses
`tools/fresh_motor_epoch_study.py prepare --articulated`. It retains 261 earlier
cases, including all split scheduling and hinge clock controls: 295 cases in
191 methods altogether. The visible PowerShell runner builds and records its
binaries and selected runtimes, checks the exact TRX multiplicities, preserves
failure as well as success, and verifies a D: backup. No Unity or live brain is
started by that component workflow. The two pending studies `e764e3` and
`ab2502` were not inspected or modified during authoring.

Authoring builds: the test and probe projects compile with zero warnings/errors.
Unity runtime sources API-compile against the installed 6000.3.19f1 assemblies
with zero errors and seven existing reflection-field warnings. The initial .NET
name-resolution errors and their logs remain preserved. Python/PowerShell syntax
and static inventories pass without executing tests or models. No component
pass is inferred from compilation.

## Visible component launch

The component package is `D:/DNNE-build-work/tendon-wire-20261123-7ce921`,
frozen at pushed `d4069ebb59b38683913554779ff2adff2d8fd591`. Frozen preflight
passed and both initial 2,210-file copies plus manifest verified against
`D:/DNNE-desktop-runs/UnityWorld/tendon-wire-20261123-7ce921` before interactive
task `DNNE-Tendon-Wire-11e2864ef108` started. No post-launch output, status or
process was inspected. Await the operator's final backup/footer; no test pass
is inferred. This makes three pending reports, alongside `e764e3` and `ab2502`.

## Remaining integration

Completed v1 review: `tendon-wire-20261123-7ce921` passed all 295 exact cases
(34 new, 261 prior). Both 2,799-file copies, frozen Git source, executed files
and unchanged 335-file selected runtime verify. The four-file review and D:
copy are `artifacts/tendon-wire-20261123-7ce921-review` and
`D:/DNNE-desktop-runs/UnityWorld/tendon-wire-20261123-7ce921-review`.
This is host protocol/plant behavior against the declared peer; it executed no
Unity body and does not qualify v2 initialization. No test/model ran in review.

After component results and native force observations are reviewed, qualify the
actual file bridge in a separate frozen Unity study, including all paths,
nonmoving samples and uncertain-command rejection. Reuse explicit preserved
attachments as fixtures; do not relabel them anatomical. The frozen v1 full-body
owner starts at physical step zero. Joining the sensory prelude requires
explicit ownership of the actual baseline: neural 1010 ms remains physical
10 ms under the existing fixture clock. No invented prior silence or retimed
neurons solve that boundary.

Actual head-to-afferent-to-brain-to-motor coupling, continuous coupled numerical
accuracy, physiological firing, useful movement, standing and fall recovery
remain unqualified. The editor will need to distinguish pending physical and
closed coupled endpoints when this owner is eventually selected there.

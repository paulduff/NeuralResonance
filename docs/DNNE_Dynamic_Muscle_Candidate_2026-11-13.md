# Coupled slow motor units and muscle/tendon mechanics

`src/NRE.SimAvatar/AvatarDynamicMuscle.cs` is an isolated candidate for the next
part of neuronal motor authority: identified motor arrivals acting on a moving
fiber and tendon. It does not replace the live Avatar/Unity plant. The existing
calcium unit, isometric connector, motor transport and their evidence remain
unchanged. The prior owning-source inspection and pinned author parameters are
in `DNNE_Motor_Mechanics_Source_2026-11-13.md`.

## Mechanism and state

Each declared slow motor unit retains its contact/source identity, ordinal,
actual arrival time and release waveform. No rate generator, target-angle error
or scalar activation command recruits it. The numerical core checks intact
arrivals; a future transport-connected plant must additionally enforce source
completeness before advancing, as the existing isometric connector does.

One coupled RK4 state contains fiber length (meters), velocity (meters/second),
slow-unit yield, and free calcium/bound calcium/activation for every declared
unit. Every stage recomputes length-dependent binding/cooperativity/Hill
parameters, velocity-dependent yield, active force, passive force and tendon
length/force. Release decays are evaluated analytically at the same stage times.
The maximum permitted step is 0.125 ms; this cap is not an accuracy claim.

The force law uses the source slow-unit length/velocity curves and passive
elements. Constant pennation projects axial tendon force by cosine and fiber
force by cosine squared; the source geometric acceleration term is retained.
Both axial and projected forces are named separately in snapshots. Mass, length,
force and parameter-domain validation are explicit. Shortening beyond the
source slow-unit maximum of 7.88 optimal lengths/second rejects rather than
extending its force-velocity curve into negative active capacity. Signed passive
viscous force remains visible. Invalid stages/endpoints fault the candidate
without committing a partial step or resetting/clipping its state.

The initial fitted geometry is the preserved tibialis-anterior reference, with
explicit centimeter-to-meter conversion. A declared subset of slow units may
activate within its passive whole-muscle geometry. This is not all 200 author
motor units, a fast-unit/sag implementation, calibrated recruitment or a
physiological replacement for every Avatar muscle. Additional unit parameters
must explicitly declare their length gains; they do not silently inherit the
first row's gains.

The continuous coupled stages deliberately differ from the author's held-tendon,
split activation/mechanics update. The original source archive stays preserved;
its ordering and the coupled candidate need separate reference comparisons.
The original calcium implementation also stays available as the numerical control.

## Diagnostic boundary and clocks

`AdvancePrescribed` accepts a contiguous, explicitly prescribed musculotendon
length interval. Stage lengths follow that known diagnostic trajectory. It does
not interpolate unseen live frames, use receipt wall time or claim a live
Unity/DNNE clock handshake. Endpoint assignment uses the supplied absolute body
time; callers must split at actual arrival and trajectory boundaries.

Free fibers evolve under the coupled mechanical law. The constructor can instead
declare `FixedLengthDiagnostic`, an imposed clamp used only for controls. At
optimal length and zero velocity, yield remains at its equilibrium of one and
the calcium/activation calculation must reproduce the original isometric unit.
The original activation-equivalent force is compared separately: the full dynamic
force also contains the author's small activation-dependent passive-2 correction.
Do not remove that correction to manufacture exact force equality.

Snapshots are immutable copies. Calcium release/pump/binding ledgers and
independently integrated net impulse/work help detect integration/accounting
errors. The kinetic-work check includes the geometric term and is not an ATP,
whole-muscle energy-conservation or metabolic-supply model. Endplate transmission
is still assumed; active axons, NMJ/muscle action potentials and physiological
fatigue remain separate unqualified mechanisms. No legacy activation/fatigue
filter is applied a second time.

## First independent PowerShell gate

The candidate and test assembly build with zero warnings/errors. Python and
PowerShell syntax checks pass. **No tests or model were executed inline.**

The frozen component window requires **93 cases**:

| Scope | Cases |
| --- | ---: |
| New coupled muscle components | 16 |
| Original calcium unit | 13 |
| Original isometric connector | 15 |
| Motor-event transport | 32 |
| Replay/acceptance receipts | 16 |
| Solution manifest | 1 |

New controls cover the original optimal-length calcium calculation, analytic
passive equilibrium, changed calcium under fixed short/long lengths, free versus
clamped fiber response, passive stretch without recruitment, selective unit
activation, dimensional mass/force scaling, actual receiver delay, event/time
rejection, transaction failure and snapshot/declaration-order independence.
They retain representative snapshots in the test output. Motor events here are
fixtures, not newly integrated neuronal firing. The runner checks declared counts
and distinct names and preserves executed binaries, logs and all failures.

`tools/prepare-dynamic-muscle.py` requires clean pushed source, verifies the
pinned source-review C:/D: bytes and all seven unchanged owning files, then
freezes source and reference provenance. Back up the fresh package on D: before
calling its `tools/start-dynamic-muscle-window.ps1`. The launcher opens an
independent visible PowerShell window and rechecks the backup. Await the
operator's completion report without inspecting its live output.

Passing these components will permit a full trace comparison against independent
adaptive integration, event/step refinement and original source-ordering controls.
It will not establish that comparison, physiological calibration, live neuronal
authority, dynamic Unity torque or recovery. Those remain required before
promotion. The legacy muscle, sensory paths, editor authority labels, paused
startup and saved brains are unchanged.

## Component window launch

The fresh package is `dynamic-muscle-20261113-f9970f`, frozen at
`23d6a0618968d10c92cf1afaaefa7cb19354e41d`. Its 1,540 initial files plus manifest
have a verified D: copy, rechecked before launch. Independent visible PowerShell
task `DNNE-Dynamic-Muscle-7976293860f3` was started. The 93-case outcome awaits
the operator's report; no live output is inspected and launch establishes no
passing model or test result. Spindle retry `98c085` and older body-profile
fixture window `64ad44` also remain unreported and untouched.

The operator subsequently reported all three windows complete. The dynamic
candidate passes all 93 cases and full evidence review, with a separate verified
D: copy. See `DNNE_Muscle_And_Spindle_Results_2026-11-13.md`. It is no longer
pending. The independent trace rig is documented in
`DNNE_Dynamic_Muscle_Comparison_2026-11-13.md`; component success does not establish
its numerical, physiological or live integration outcome.

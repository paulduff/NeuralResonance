# Receptor-driven motor-cell candidate — 16 November 2026

The new isolated owner connects identified presynaptic events to contact-local
receptor conductance and then to the five-compartment motor membrane. This is
code awaiting tests, not demonstrated neuronal recruitment. It is not selected
by a live structure, Avatar, Unity or the editor. The three preceding studies
have now been reported complete and reviewed. The next 111-case gate is running;
its result remains uninspected until the operator reports completion.

## Membrane boundary

`MotoneuronReferenceCell.AdvanceWithReceptors` evaluates every contact at the
start, midpoint (twice), and end of each RK4 step. Its actual electrical
compartment receives `g(t) * (Erev - Vstage)` in pA. This current participates in
the existing external-charge ledger. It is not held at the start-of-step value,
spread uniformly over all dendrites, or translated into a firing rate. The
existing membrane kinetics, geometry, calcium accounting, compensation and
observational crossings remain the same equations.

The existing constant-current/conductance entry point now delegates to the
shared integrator with no receptor list. Its original arithmetic path is
retained; **bitwise reproduction is a prospective check, not yet a result**.
Once the new input path is used, the integration-version string explicitly
includes `point-receptors-stage-evaluated-v1`. No state is reset at attachment.
The owner itself only supports a fresh session at neural time zero.

## Causal owner and incoming events

`ReceptorDrivenMotoneuron` privately owns its membrane and receptor contacts.
All contacts must identify the same target neuron and distinct physical sites.
Their deterministic identity order fixes summation order. There is one thread
owner and no publicly exposed mutable cell/contact state.

Each contact lane accepts a contiguous batch sequence and a source completeness
time. The first batch includes firing time zero; later intervals are open on
the previous completeness boundary and closed on the new boundary. Each
original message is deeply copied and hashed at admission. Exact batch replay
returns the retained receipt without another release. A single original event
may fan out to different contacts, but its contents cannot change between them.

Admission validates the entire batch before committing it. Invalid routes,
wrong sessions, overlapping transmitter pulses, reused event IDs, changed
replays, missing sequences, late/out-of-interval events and capacity exhaustion
leave accepted state unchanged. The producer must retain rejected input. These
checks validate declarations; they do not authenticate a biological source.

The membrane cannot advance until every lane accounts for its requested
interval, with the declared axonal delay included. Missing input is not silence.
The owner splits at all incoming release times and transmitter pulse ends;
receptor conductance is evaluated at membrane solver stages. Receptors commit
to the same endpoint after the membrane step. An integration failure stops the
whole candidate epoch; partial failure is not a usable restart. Snapshots record
membrane/receptor state, input completeness/counts, actual steps, original
crossings and charge/calcium ledger errors. Accepted messages and batch receipts
are retained for the bounded epoch; there is no checkpoint/restart yet.

The interval scheduler uses endpoint differences, following the checked motor
clock method. After a very early fractional event it limits step growth so that
the endpoint subtraction is exact; the supplied duration equals the clock
advance. An exact-binary integer accounting check is included prospectively.
An interval too small to resolve its RK midpoint stops rather than shifting an
original event or pretending the skipped duration was integrated.
Before such a tail is created, the scheduler shortens the preceding interval
by one representable endpoint when necessary. Both intervals are integrated;
the original release/observation time and total duration stay unchanged. The
clock case includes a boundary just one ULP beyond a nominal maximum step.

The isolated owner has no soma-current injection API and does not prescribe
motor crossings. Its tests do supply explicit presynaptic events as fixtures.
An actual checked presynaptic source and its complete emission stream must still
be connected before this can support a neuronal-authority claim.

## Prospective checks and next gate

Fifteen new cases are authored in `ReceptorDrivenMotoneuronTests`:

- AMPA and GABA-A uniform passive cables compared with the independently
  integrated closed form `V=E+(V0-E)*exp(-integral(g)/C)`, with a predeclared
  `1e-7 mV` voltage bound and `1e-6 fC` charge-accounting bound.
- Silent accounted input compared bitwise with all thirty original membrane
  states on the same step grid, retaining original crossings.
- A local dendritic input, missing/incomplete inputs, packet/replay invariance,
  caller mutation, cross-contact original-event consistency, atomic rejection,
  exact pulse boundaries, capacity, clock/ownership checks and both reversal
  equilibria.

Together with the receptor, original motor-cell, motor-axon, replay-buffer,
observed-source and solution-manifest cases, the original focused inventory was
97 cases. The outgoing receptor motor source adds 14 cases, making the current
gate **111 cases**; see `DNNE_Receptor_Motor_Output_2026-11-16.md`. None were
executed during authoring. The test project builds with zero warnings/errors,
which is only compilation evidence.

The preceding receptor component `501da5` has passed its reported saved-data
review. Before a coupled numerical study, the next visible-window package must
pass the complete 111-case inventory and
rerun all five existing six-second observed-source conditions against the
original exact traces, events and delivery accounting. The old source/receptor
preparers correctly require an unchanged membrane; they must not be bypassed
or mislabel this modified source as unchanged. Use an explicit new package and
record the membrane extension and original baseline separately.

Further promotion requires an independent coupled reference on mixed/local
conductances, stage/event timing, active-cell state and crossings, with
predeclared accuracy/refinement criteria. The passive closed form and unchanged
source path are controls, not substitutes for that coupled comparison.

The receptor parameters remain the explicitly documented hippocampal reference
kinetics in `DNNE_Motor_Receptor_Contacts_2026-11-16.md`, not calibrated spinal
contacts. No live recruitment, glycinergic/NMDA input, contact-local learning,
sensory closure, physiology, GPU speedup, balance or fall recovery is qualified
by this candidate. Avatar's existing mechanisms and old saves remain preserved.

## Deferred verification rig

`tools/receptor_membrane_gate.py` and
`tools/start-receptor-membrane-window.ps1` implement the next package. The
prerequisite review is complete and the new gate has been launched; see the
execution record below.

After the operator reports the receptor component complete, the explicit
`review-component PACKAGE NEW_REVIEW --operator-reported-complete` command
checks its terminal success status, all C:/D: files, frozen Git archive, test
binaries, exact 74-case inventory and protected motor core. It writes a new
review and does not rerun tests or solve a model. A failed component needs
diagnosis; this command cannot turn its completion into a passing review.

After that review is backed up to D:, `prepare --receptor-review REVIEW_FOLDER`
requires a clean pushed checkout, the reviewed receptor result, the completed
observed-source review and the D:-verified authoring capture. It freezes the
candidate and copies the original six-second reference traces. The modified
membrane and earlier source-ownership extension are explicitly recorded as
changed source files; the original channel, clock, transport and reference-study
files are verified unchanged. The current authoring source must match its saved
capture. Preparation does not execute tests or integrations.

The independent visible launcher requires the initial verified D: copy. It
builds frozen binaries and then runs the complete 111-case inventory. Case count,
class count and individual method multiplicity are checked against both the
frozen source and the configuration. Only then does it build/run the five full
source conditions and independently audit all saved original traces, spike
identities and motor deliveries. Source and binary integrity are checked again,
including on a failed exit, before final backup. PowerShell stays visible and
the operator reports its outcome; there is no agent monitoring.

Passing this gate would establish the component controls and preservation of
the old source trajectory. Its status deliberately leaves independent coupled
accuracy, physiological calibration and Avatar recovery unqualified. It runs
no Unity player and selects no live brain.

## Execution record

`receptor-membrane-20261116-f2c9a7` was frozen at
`2453007fad2c1acdfff476fb7a70dc2ff46f5d2f`. Its initial 1,712-file D: copy plus
manifest was verified before launching the independent visible PowerShell task
`DNNE-Receptor-Membrane-5ad21bf3811c`. It runs 111 cases and five original source
conditions. No post-launch status, output or process has been inspected.
Await the operator's completion report before `review-gate`; this launch does
not constitute a passing result. The numerical comparison remains deferred.
The three preceding reviews are recorded in
`DNNE_Body_Spindle_Receptor_Results_2026-11-16.md`.

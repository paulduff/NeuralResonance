# Receptor-driven motor-cell candidate — 16 November 2026

The new isolated owner connects identified presynaptic events to contact-local
receptor conductance and then to the five-compartment motor membrane. This is
code awaiting tests, not demonstrated neuronal recruitment. It is not selected
by a live structure, Avatar, Unity or the editor. The three previously launched
studies remain frozen and are not inspected while awaiting operator reports.

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
observed-source and solution-manifest cases, the next focused inventory is
**97 cases**. None were executed during authoring. The test project builds with
zero warnings/errors, which is only compilation evidence.

Before a coupled numerical study, review the pending receptor gate `501da5`.
The next visible-window package must run the complete 97-case inventory and
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

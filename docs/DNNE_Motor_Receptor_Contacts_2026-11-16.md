# Isolated motor receptor contacts — 16 November 2026

The observed motor-source gate reproduced the checked membrane's spikes under
somatic current injection. It did not demonstrate that identified presynaptic
neurons can recruit that motor cell through physical synaptic contacts. This
increment supplies an isolated receptor-conductance component for that missing
connection. It changes no selected live circuit, Avatar, editor, or old save.

## Source and biological scope

The candidate independently implements the two-state kinetic equation described
by Destexhe, Mainen and Sejnowski and illustrated in
[ModelDB 18500](https://modeldb.science/18500). The inspected source is pinned at
[ModelDBRepository/18500 revision 937ed5d](https://github.com/ModelDBRepository/18500/tree/937ed5d0d04519d4cd7af474a0f6c53a6e437e06).
`README`, `ampa.mod`, and `gabaa.mod` were checked against their Git blob hashes;
the complete tree and commit metadata are retained in
`artifacts/motor-receptor-source-20261116`. No upstream model was executed.

The equation is `dr/dt = alpha C (1-r) - beta r`. The two reference parameter
sets are AMPA (`alpha=1.1 /ms/mM`, `beta=0.19 /ms`, reversal `0 mV`) and GABA-A
(`alpha=5 /ms/mM`, `beta=0.18 /ms`, reversal `-80 mV`). A release supplies a
`1 mM`, `1 ms` transmitter pulse. These source kinetics were fitted to
hippocampal recordings; **they are not calibrated spinal parameters**. Maximum
conductance and axonal delay must be declared explicitly. The fixture uses
`2 nS` and `0.25 ms`; these are test choices, not anatomical estimates.

The implementation uses conductance in nS and inward-positive current
`g (Erev - V)` in pA. This explicitly converts the upstream micromho convention.
It uses the mathematical closed form with direct exponentials and a small-time
series, preserving positive tails beyond the upstream exponential table's
cutoff. It accepts identified events instead of the upstream voltage polling
and dead-time trigger. Thus it is an implementation of the stated equations and
declared release convention, not an exact reproduction of that NEURON program.

## Ownership and limits

`MotorReceptorContact` gives each contact a stable source/projection/synapse,
target neuron and physical site identity. Different sites can share an
electrical compartment while retaining independent occupancy. A fresh neural
session is explicit and starts with closed receptors. A complete original
event is hashed and receipted; exact replay is inert, while conflicting replay,
wrong source/session, synthetic background, and early/late delivery are rejected.
Packet metadata is not converted into arbitrary conductance amplitude.

Each contact has one thread owner. New releases must occur at the exact
source-time-plus-delay boundary. Overlapping pulses are unsupported and rejected
before mutation; the calling owner must retain that input and stop, not drop or
merge it. The bounded epoch ledger similarly stops before forgetting replay
history. There is no checkpoint/restart implementation. Invalid caller clocks
do not advance or repair accepted state.

Observation partitions do not repeatedly approximate the receptor trajectory:
values derive from the last accepted release anchor. Pure future evaluations
describe only the known release history. They are **not an input-completeness
watermark**. A future neural owner must split at release/pulse-end boundaries,
wait for complete upstream input, and evaluate conductance at every membrane
solver stage. That coupling is not implemented in this increment.

This component does not implement presynaptic excitability, NMDA or glycine,
release probability, vesicle depletion, receptor trafficking, local learning,
metabolic support, motor recruitment, or a closed sensory loop. The checked
motor membrane, channel kinetics and Avatar motor-event transport are preserved
and compared with the completed observed-source capture during preparation.

## Prospective visible-window checks

`prepare-motor-receptor.py` freezes a clean pushed revision, checked source
evidence, the completed motor-source review and core-preservation hashes.
`start-motor-receptor-window.ps1` verifies the initial D: copy, builds the frozen
suite, runs the exact declared method/case inventory, preserves evidence and
backs up its result. It opens independent visible PowerShell and is not monitored.

The 74 cases consist of 14 new receptor checks and 60 existing motor-cell,
motor-axon, replay-buffer and solution-manifest checks. The receptor cases cover
analytic rise/decay, tails, observation partition invariance, physical current
units, distinct contacts, original event replay, conditional preview, rejection
without mutation, unsupported overlap, capacity, clock domain and ownership.
These are component checks; no independent coupled neuronal comparison runs.
Passing them would not qualify spinal physiology, live brain authority or Avatar
recovery. A build or a completed process cannot stand in for those results.

At preparation, the two pending operator reports remain observed/native body
`bb10dc` and fixed-length spindle `18ec5e`. Neither output is inspected here.
Consult the subsequent launch record before expecting this third window.

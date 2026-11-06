# Cortical contact replay and numerical cadence diagnosis — 6 November 2026

The recipient comparison established delivery/locality but also changed cue
postsynaptic firing from 37 to 57 updates. Its larger final weight cannot separate
direct tag capture from the effect of that different activity. This diagnostic
holds the recorded event history fixed and measures the direct intervention.

## Evidence and scope

Input comes only from the operator-reported, reviewed and D:-verified capture
`cortical-memory-20261106-2be2a4`, revision
`aeb40be364e2e44dd4d83e47f9a9641d326c6765`. Four histories are selected: continuous
colocated, neighbour, original simultaneous and unmodulated conditions. They
contain 654, 145, 145 and 150 cue-contact updates respectively. Static review found
no gaps between successive recorded before/after states.

The preparer verifies the review against its D: copy/manifest, every selected
original learning log against the original C:/D: manifest, and byte identity of
the four owning learning/state/evidence source files. It preserves exact selected
JSON lines with their original line numbers and complete-file hashes. Only a new
package is written. No unreported experiment, live service or old save is read.

The existing MemoryConsolidationProbe gets an explicit `--contact-replay` entry.
It restores the captured initial contact fields and calls the unchanged
ContactPlasticity methods. This is a replay of a reduced synaptic rule; it does
not rerun a membrane, recurrent network or Avatar. It is not a general checkpoint
restore: fields irrelevant to this rule, such as feedback provenance and Purkinje
chemistry, are outside this evidence format. Inputs are restricted to the actual
non-synthetic Entorhinal cue on cell 166.

## Required reproduction and interventions

Each of the four histories has these four conditions, giving sixteen replays:

1. **Recorded:** every before/after contact field must reproduce exactly. A
   mismatch fails the run before any successful diagnostic claim.
2. **NoTagCapture:** the continuous modulation call uses STDP without tag capture;
   spike calls remain unchanged. All call times and trace decay calls remain.
3. **NoDopamine:** recorded dopamine is zeroed only at the modulation calculation.
4. **NoAcetylcholine:** recorded acetylcholine is zeroed only there.

These are software interventions on the current rule, not calibrated receptor
antagonists. They hold presynaptic/postsynaptic events, activity, teaching and
initial contact state fixed. The independent output audit also requires exact
preservation of the resulting pre/post, eligibility/tag and theta traces. Every
event and weight/budget change is retained. It requires the current-rule identity
of NoAcetylcholine and NoTagCapture outcomes, and invariance of histories with no
modulation calls. It does not require potentiation or a preferred effect size.

The final weight difference within one history estimates the direct intervention
under fixed activity. It includes subsequent weight dependence and competition
for the existing plasticity budget, so it need not equal the sum of the original
modulation increments. It does not estimate the total closed-loop biological
effect of modulation. The original full-network difference remains distinct.

## Cadence diagnostic

The current `ApplyCadenceInvariantBudget` rejects raw updates at or below 1e-7,
then scales by 0.02 and adds to a float weight. Both mechanisms can suppress
sufficiently small continuous changes. Their effect on this contact must be
measured before selecting a numerical repair or interpreting weak capture as a
biological effect.

Twenty-seven additional cases use pre/post, post/pre and unpaired fresh contacts
under constant normalized ACh/DA for one second, at nine steps from 4 to 0.015625
ms. Pair events occur at 10 and 20 ms using tag-only learning, so the initial
weight stays 1. This is a deliberately prescribed contact fixture, not neuronal
firing qualification. No new rates or conductance parameters are fitted.

For each paired case, an explicit closed-form continuous limit is calculated from
the recorded initial tag, 8-second tag decay, the existing capture factor and
learning scale. It excludes float rounding and implementation dead zones. It is
not an independent biological reference. Its discrepancy is reported without
turning a chosen tolerance into a new acceptance claim. Blank unpaired contacts
must remain unchanged. The audit reconstructs float values from their JSON
round-trip spellings when checking exact weight-change accounting.

## Biological direction after this diagnosis

[He et al. (2015)](https://pmc.ncbi.nlm.nih.gov/articles/PMC4660261/) studied layer
II/III pyramidal synapses in visual and prefrontal cortex. Their results support
distinct, receptor-dependent potentiating and depressing eligibility processes;
under their visual-cortex protocol, NE and serotonin had different effects,
whereas dopamine and cholinergic agonist did not convert the traces. That evidence
does not calibrate human temporal association cortex. It argues against treating
the present generic ACh/DA multiplier as a universal cortical memory mechanism.

A subsequent learning candidate should declare its cell/pathway and receptor
scope, separate induction from later conversion, use actual connected activity
and locally arriving signals, and qualify timing, specificity, retention and
interference. Fix numerical cadence separately from selecting that mechanism.
The current ensemble consolidation score remains derived bookkeeping; no new
biochemical consolidation state is introduced here.

## Operation and status

Build/static checks precede freezing clean pushed source. The independent visible
PowerShell window builds the probe, executes the 16 replays and 27 cadence cases,
audits saved output and verifies its D: backup. There is no live monitoring. Await
the operator's report before reading its output. `DIAGNOSTICS_COMPLETE` means
recorded-state reproduction and complete diagnostic evidence, not repaired
learning, a passing full unit suite, physiological validity or Avatar recovery.
The earlier random consolidation assertion remains unchanged.

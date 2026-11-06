# Isolated cortical contact precision candidate — 6 November 2026

This addresses the measured defect in `DNNE_Cortical_Contact_Results_2026-11-06.md`.
`CorticalContactNumerics` is a separate, internal calculation of the existing
reduced excitatory STDP/tag-capture rule. No StructureEngine, host, Avatar, editor
or persisted brain selects it. The original implementation and assertion remain
unchanged, and every prior save/capture is preserved.

## Numerical changes and limits

Weight, pre/post traces, eligibility, tag, theta, limiter balance and accumulated
charge use double precision. Trace decay uses the declared exponential without
the old 1e-6 trace truncation. Nonzero increments are no longer discarded by the
old 1e-7 per-update cutoff. Values of existing float parameters are promoted to
double; rates, trace time constants, pair coefficients, concentration gate,
weight bounds and limiter parameters are not retuned. The continuous modulation
calculation remains a right-endpoint step, so finite-step error must converge;
the candidate does not claim exact time-partition invariance.

All state is immutable outside the integrator; transitions validate the complete
next state before committing it. Invalid times/non-finite inputs fail without
changing accepted state. Only nonsynthetic glutamatergic contact evidence can
initialize this candidate through the legacy-evidence adapter. Its version is
`isolated-cortical-contact-double-v1`. It is neither a new checkpoint schema nor
a silent conversion of an existing brain.

This is arithmetic repair of a reduced rule, not a biochemical learning model.
The old normalized ACh/DA multiplier, per-contact limiter and saturation rules
remain provisional. The raw update counter remains an operation count and must
not be promoted as a consolidation measure. Double precision is still finite;
the checks cover the declared input/step domain rather than every possible tiny
increment. Limiter charge at saturated weight bounds is not newly calibrated.

## Fixed experiment and gates

The new package freezes the completed review and full old replay results. It
first reruns the old sixteen replays and twenty-seven cadence fixtures, requiring
the complete saved baseline to reproduce exactly. Its owning learning files must
have the same archived bytes as the original. It then runs the candidate on the
same four captured contact histories and four interventions, preserving initial
state, event identity, timestamps and activity. Every double-state transition is
saved. No desired sign or magnitude is imposed on those recorded histories.

The candidate repeats the twenty-seven controlled cadence fixtures: positive
pairing, negative pairing and blank unpaired contacts; one second at steps
4, 2, 1, 0.5, 0.25, 0.125, 0.0625, 0.03125 and 0.015625 ms. The closed form uses
the candidate's measured initial tag and the exact represented parameter values.
It describes the current rule in this unsaturated, non-depleted regime; it is not
a physiological reference. C# and the independent Python output audit compute it.

Predetermined gates:

* Maximum absolute weight error 3e-8 at every step; finest error at most 1e-9.
  The coarsest bound covers the right-endpoint discretization error at 4 ms and
  is far below the complete disappearance measured in the legacy fixture.
* Each refinement error is at most max(1e-11, 1.1 times the previous error).
* Correct sign for both pair orders; blank contacts unchanged.
* No limiter charge without a stored weight change in these cadence cases;
  positive remaining limiter balance and interior weight confirm that the
  closed form's assumptions hold.
* Three invalid-input controls preserve accepted state. All sixteen histories
  retain identity, timing and their declared starting state; counterfactuals
  preserve the same traces and the expected ACh/capture algebraic identity.

Do not change these bounds after seeing the outcome. An isolated pass would
permit integration work with explicit model identity, persistence and actual
cell-event checks; it would not establish receptor biology or whole-brain memory.

## Operation

Prepare with `tools/prepare-cortical-contact-replay.py --numerical` from clean
pushed source, verify the initial D: package, then use its visible independent
PowerShell launcher. The existing short replay runs before the numerical
candidate and output audit. No live monitoring; the operator reports completion.
Build/static checks precede packaging. Numerical/physiological success is not
claimed at launch, and no full-suite pass is inferred from this isolated study.

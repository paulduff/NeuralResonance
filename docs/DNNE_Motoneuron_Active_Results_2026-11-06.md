# Active motor-cell numerical result — 6 November 2026

The operator reported `motoneuron-active-20261106-df5b5b` failed. Frozen source
`1ef1b6a9426bd319d9866dbcc39e096af8d06035` completed at 12:44:01 UTC. All ten
focused/manifest tests passed; all 30 RK4 conditions and 30 adaptive references
completed. The original numerical criteria failed and remain failed.

`tools/review-motoneuron-active.py` re-read saved traces without integration,
recomputed every saved metric/comparison and original decision, and verified
1,789 files (1,400,126,554 bytes) per C:/D: copy, including 1,291 packaged files
and frozen binaries. Report:
`artifacts/motoneuron-active-review-20261106/review.json`.

## What agreed and what failed

Every candidate/reference spike count agrees within each scenario: quiet 0;
soma 1/3/6 nA: 0/13/28; brief 20 nA pulse: 0; dendritic 86; inhibited dendritic
74; without fast sodium 0; without CaL 14; without KCa 623. All retained state
arrays are finite. Strong stimulation exercises action potentials and calcium.
These statements do not establish physiological calibration.

Four of ten scenarios meet every original criterion: soma 3/6 nA and the two
dendritic conditions. The remaining failures have three distinct forms:

* Quiet, soma 1 nA, brief pulse and no-fast-sodium fail calcium refinement ratios,
  although all absolute bounds pass. Quiet calcium errors at successively finer
  steps are 2.93e-17, 4.55e-17 and 1.11e-16 mM. No-fast-sodium reaches 5.89e-15 mM.
  Increasing accumulated roundoff is a hypothesis, not yet a demonstrated cause.
* Without CaL, maximum charge-accounting error approximately doubles with each
  halving of the step: 3.94e-6, 7.91e-6 and 1.58e-5 fC. The finest exceeds the
  unchanged 1e-5 fC bound. Its trajectory and reference comparisons pass.
* Without KCa, ordinary versus tight DOP853 gate disagreement is 8.12e-5,
  exceeding 5e-5. Tight DOP853 versus Radau is 4.26e-6 and passes. All finest
  candidate absolute comparisons pass; reference qualification blocks acceptance.

The reported SciPy overflow warnings occur in quiet/Radau Jacobian estimation;
later cases continued. Saved finite trajectories do not prove all intermediate
arithmetic was warning-free. Structural zero ledger columns and adaptive
perturbation-factor growth are a plausible trigger, not an instrumented causal
reproduction. The previously prepared bounded-Jacobian checks remain unrun.

## Next numerical candidate

Test compensated addition of RK4 state increments against retained naive
addition, with an analytic passive charge control. Use tighter adaptive
references and the bounded Jacobian while retaining the original inputs,
membrane equations and all acceptance bounds. Preserve this failed capture and
record the new solver protocol explicitly. Do not solve these failures by
changing conductances, clipping states or relaxing bounds. A future numerical
pass still requires separate source physiology and neuronal/body integration.

# Retaining intrafusal population shape during motion

The reviewed `spindle-transport-20261116-ba9648` run shows that repeated
projection to uniform bins diffuses the attached cross-bridge population.
Near-exact total mass and first-moment force can hide that changing shape.
The replacement candidate therefore retains material intervals between moves.
It remains isolated Python numerical work; no live Avatar, Unity, DNNE sensory
source or editor selects it.

Each initial bin is a separate uniformly populated interval. The candidate
retains its original coordinates, density and surviving extent, together with
separate left/right outflow by cohort. The owner supplies absolute displacement,
assuming a monotone movement segment between successive supplied positions.
This avoids accumulating rounded displacement increments. A motion reversal
must be supplied explicitly: no sampled endpoint can reveal an unobserved
intermediate excursion.

The finite numerical domain remains absorbing. Leaving it removes part of an
interval and records that mass; returning cannot recreate it. Negative source
roundoff remains visible, with no population normalization or positivity repair.
Grid observations integrate the retained distribution without becoming the next
state. Force and first/second moments come from interval integrals, not bin-centre
approximations. At initialization, symmetric uniform cells retain the original
first-moment force convention. This does not establish a physiological boundary
or identify numerical outflow as biological detachment.

The prospective visible-window study requires:

- Twelve controls for known shifts/outflow, readout independence, preserved
  return shape, irreversible boundary loss and invalid-input rejection.
- All 25,308 saved-state translations compared with the separate interval-overlap
  reference, including its exact retained first moment.
- Continuity around all sign-changing finite-support jumps in the 31 original
  no-root states, without a new root search or selected equilibrium.
- All 144 original rectangle histories, varying both numerical range and grid
  width, movement subdivisions and forward/reverse motion. Compare every
  endpoint with an analytic continuous rectangle, preserving population arrays,
  surviving material intervals, force and first/second moments and outflow.
- Ninety-six movement-cadence contrasts at shared physical positions. The
  candidate must preserve shape, force, second moment and outflow when the same
  path is subdivided further.

The original 1e-10 population/conservation, 1e-7 Pa force and 1e-8 domain bounds
remain. The history population criterion now covers total L1 shape error,
and the force criterion covers the continuous interval moment, rather than just
matching another rebinner. The additional second-moment bound is 1e-8 nm².
These limits are declared before execution; the earlier passing results and
their larger shape/force errors stay preserved. No limits will be fitted to the
new output.

The preparer requires the D:-verified original force and completed transport
reviews, unchanged original parser/reference implementation and numerical
dependencies, and a clean pushed Git revision. It only reads saved inputs and
freezes the package. The launcher owns every control and comparison in an
independent visible PowerShell window, with no live monitoring and a verified
D: backup. Completion must be operator-reported before review.

Successful transport would permit an isolated coupled-kinetics candidate. That
next step must update attachment/detachment, actin activation and filament overlap
without silently projecting retained cohorts back to uniform bins. Attachment
would introduce new material populations, and kinetic rates must act at their
actual strain coordinates. Their representation, integration accuracy, domain
dependence and cost still need qualification. No complete intrafusal transition,
slack trajectory, sensory firing or whole-Avatar recovery is established here.

The study is launched as `spindle-characteristics-20261116-7c6db6`, frozen at
`683b0cf0eb4774b935558b557438c78b60add02c`, visible task
`DNNE-Spindle-Characteristics-2619cda0839d`. All 1,755 initial files plus manifest
were D: verified. Authoring parsed Python and PowerShell syntax; preparation
read reported, immutable MAT inputs and checked provenance/dependencies only.
No control, transport calculation, history or numerical comparison ran inline.
The operator report is pending. No post-launch outputs, status or processes were
inspected. The committed implementation is a candidate awaiting qualification.

## Completed numerical review

The operator subsequently reported completion. The saved-data review verifies
2,093 files and 98,635,159 bytes per C:/D: copy, 1,754 protected package files
and 1,710 frozen Git files, with unchanged numerical dependencies. The separate
`artifacts/spindle-characteristics-review-20261116` review is D: verified.
No transport, history or kinetic model was executed during review.

All twelve controls, 25,308 saved translations, 31 continuity probes, 144
histories (8,208 saved endpoints) and 96 cadence contrasts pass. The review
reconciles population arrays with saved intervals, exact interval moments,
outflow/conservation, monotone loss of surviving intervals and shared-position
comparisons. Maximum history population L1 error is 5.6455e-16; maximum force
error is 5.8208e-11 Pa and second-moment error is 1.4211e-14 nm². Movement
subdivision changes neither population shape nor force at shared positions;
maximum outflow-ledger difference is 1.1103e-16.

This repairs the demonstrated transport diffusion for these isolated histories.
The previous rebinner's maximum history L1 error was about 0.16505 and force
error 6,283.14 Pa. The absorbing-domain dependence remains deliberately present:
this result does not identify a physical detachment boundary or qualify moving
reaction kinetics, slack equilibrium, sensory spikes or live Avatar recovery.
The run is no longer pending. Work pauses at the operator's request; see
`DNNE_Checkpoint_2026-11-16.md` for the next steps.

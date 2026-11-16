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

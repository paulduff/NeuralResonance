# Active muscle and shared motor results

Paul confirmed both windows finished. The read-only reviews executed no model
or test and verified both complete original/backup file inventories, Git sources,
executed binaries, selected runtime records and exact managed test inventories.

| Study | Frozen revision | Verified files per copy | Outcome |
|---|---|---:|---|
| `muscle-population-active-20261123-a212b5` | `3ac6ecb089eff59ee96b1e7edb2c4a2d41b0eb36` | 3,097 | All eight conditions, 24 native traces and 24 independent references pass |
| `shared-motor-anatomy-20261123-5001cd` | `bc7898940d43cf83093e0c4368671e8bd22c01d5` | 2,869 | All 164 managed cases pass, including nine new shared-anatomy controls |

The seven-file active review and four-file shared review each have a verified
D: backup under their matching `*-review` names. Their original results remain
unchanged. Neither window is pending.

## What the active result establishes

All 147 distinct original slow-unit states were retained in each muscle, with
one coupled fiber/tendon state and the exact previously checked initial state.
The 379 managed regressions, 108 scalar/vector field controls and six bounded
Jacobian controls passed. Each of the 24 native traces contains 801 complete
samples; source event counts, release waveforms, physical domains, calcium and
mechanical ledgers pass the original criteria. The three reference methods and
native step refinement pass without changing bounds.

The largest finest-native-step error is 0.0451033554 of its allowance; the
largest reference cross-method discrepancy is 0.4046991214 of its separate
allowance. These ratios cover the declared sampled observables, not arbitrary
inputs, anatomical validity or whole-body integration.

| Condition | Peak active fiber force (N) | Peak axial tendon force (N) | Minimum fiber/optimal length |
|---|---:|---:|---:|
| Left quiet | 0 | 1.512867 | 1.003945 |
| Right quiet | 0 | 1.512922 | 1.003947 |
| Left first source unit | 0.241519 | 1.646971 | 1.002427 |
| Left last slow source unit | 0.183223 | 1.580959 | 1.003158 |
| Left mixed | 132.251208 | 130.035204 | 0.910264 |
| Right mixed | 132.253891 | 130.039157 | 0.910265 |
| Left mixed stretch | 112.768717 | 113.027853 | 0.904780 |
| Left diagnostic fiber clamp | 184.381591 | 1.512867 | 1.003945 |

The diagnostic clamp holds fiber and total length fixed; its internal active
force is not an external-body tendon-force result. The first/last source rows
are identities, not a ranking by twitch strength. Inputs are prescribed arrivals,
not observed motor-neuron discharges. Fast fibers, physiological recruitment,
native coupling and standing remain unqualified.

## What the shared result establishes

Separate pools, including two 147-cell pools, attach using the same whole-body
anatomy. Original per-cell identity and prehistory remain intact. A missing pool
or final cell holds back body time; an acknowledgement from another pool and a
partial per-pool anatomy cannot release body ownership. This fixes an integration
restriction without increasing the 256-cell per-pool limit or changing equations.
The new controls evolve motor cells under declared quiet presynaptic completion;
they do not demonstrate recruited firing or population muscle force.

## Standing coverage discovered alongside these results

`capture_standing_muscle_coverage.py` decoded the pinned anatomical source and
completed mapping/native evidence only. Its output in
`artifacts/standing-muscle-coverage-20261123` and D: backup retain source topology,
all 80 source muscles, original joint/constraint definitions and the ten mapped
paths. No model, joint transform, force or moment arm was evaluated.

The new named native paths contain 44 attachments on shins and feet. Traversing
the actual body tree shows only the `left_foot` and `right_foot` child joints
between their attachments: no hip or knee crossing. This is a finding about the
new named-path candidate. The existing `AvatarArticulatedBody` still defines
hip, knee, ankle, trunk and arm muscle groups; that older body/control system
must be preserved and consulted, not mistaken for absence of the anatomy.

All eight source quadriceps subdivisions require both path wrapping and patella
attachments. Source patellofemoral CustomJoints use SimmSpline transforms and
CoordinateCouplerConstraints linking each patellar coordinate to knee angle.
The current 15-link native body has no separate patella, and the checked path
implementation uses fixed material attachment/via-point polylines. Flattening
those source paths would lose their prescribed mechanism. Source tree crossings
alone do not establish torque direction, moment-arm size or ability to support
weight. The source's muscle force capacities/dynamics are not automatically
imported by this catalog.

## Next integration decisions

1. Couple the passing active TA populations to their actual named Unity paths,
   preserving measured body-length feedback, original event ownership and the
   body/motor scheduling boundary. Numerical accuracy of the moving-body
   combination remains a separate requirement. Carry the reviewed shared-pool
   change explicitly into its source declaration and regression inventory.
2. Extend the anatomical mapping to hip/knee support. Preserve the existing
   Avatar groups, source wrap routing and knee/patella coupling. A reduced
   patellar mechanism is possible only with explicit force/work accounting;
   a fixed attachment substitution or a copied TA population is not calibration.
3. Close the neuronal sensory-to-motor loop using observed cellular events and
   demonstrate standing and recovery in a paused-on-open, observable world.
   This remains the goal; neither of today's passing component studies proves it.

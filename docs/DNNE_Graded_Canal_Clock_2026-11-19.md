# Resolving canal transients at physical input boundaries — 19 November 2026

The operator reported the completed FAILED footer for `canal-clock-20261119-274f31`.
Its 121 tests, original clock/input accounting, energy accounting and independent
references pass. The native trajectory accuracy and refinement criteria fail.
The failure remains preserved, with all 3,198 files verified in both copies.
The separate four-file review plus manifest has a verified D: backup. Saved
analysis is reproduced exactly; no test, model or reference solve ran in review.

## What failed

All three worst errors are in cupula-PC flow at the first substep after held
input step 163 becomes available. The native solver does not resolve this fast
transient at the uniform resolutions tried. Normalized errors are fractions of
the unchanged allowance, so values above one fail:

| Substeps per interval | All substeps | Common 256 grid | Physical endpoints |
| --- | ---: | ---: | ---: |
| 256 | 887.87733254 | 887.87733254 | 0.00674708 |
| 512 | 991.23729381 | 118.67866635 | 0.00084417 |
| 1024 | 960.35425762 | 1.23617776 | 0.00010557 |

Errors fall strongly at matching times. Maxima across the different first-step
times do not fall monotonically. Checking only the physical endpoints would hide
the failure; those endpoint passes do not qualify the complete trajectory.
The independent reference's worst fraction is 0.00423428 and both precision
checks pass. This evidence supports a boundary-resolution repair, without
changing source parameters, biological equations or input timing.

## Separate graded candidate

`AvatarCanalStepSchedule` declares immutable positive dyadic tick widths covering
exactly one physical interval. `AvatarClockedGradedCanal` applies the unchanged
two-stage Radau core with a cached factorization for each declared width.
The original uniform owner remains byte-for-byte preserved. The new owner has
its own version/fingerprint, including the complete schedule. No live selector,
brain, Unity or editor flow changes.

The base partition has 65,536 ticks: 32 steps of each width 1, 2, 4, ..., 1024,
then one explicit 32-tick remainder. It has 353 steps. Refinements 2 and 4 split
every step equally, preserving nested endpoints and the original outer clock.
At the original 9.999999776482582 ms interval, the first step is approximately
0.152588 microseconds at base and 0.038147 microseconds at finest resolution.
The largest steps are approximately 156.25 and 39.0625 microseconds respectively.
This is a declared numerical partition, not an altered biological time constant,
input smoothing, state reset or synthetic firing rule.

The owner retains delayed held-input causality, exact latest retry behavior,
one-interval acknowledgement, integer clock identity, compensated work/loss
ledgers, whole-interval commit and attempted-prefix fault evidence. Physical
and numerical damping remain separately recorded. Uniform partition controls
must reproduce the original owner's transitions exactly.

## Prospective independent PowerShell qualification

The same 201 original head receipts drive 199 intervals at each of three
resolutions: 491,729 native steps total. Every accepted substep is retained and
compared, including the initial boundary transients. There are 156 exact focused
cases: all previous 121 plus 35 new schedule/owner cases. New controls cover
coverage, nesting, immutability, rejection, unequal durations, preserved uniform
transitions and schedule identity, as well as the inherited handoff/clock cases.

The reference grid is the union of finest graded endpoints and all original
uniform-1024 anchors: 2,308 endpoints per interval, 459,293 rows including the
initial state. Independent 60/90-digit Taylor matrix exponentials and SciPy
exponentials use separately cached operators for each gap. Their source inputs
come from the already saved independent angular reconstructions, never native
mechanical states. The six analytic reference controls run first. New references
must also agree with the preserved original uniform reference at every anchor.

The original candidate bounds (1e-18 m³, 1e-17 m³/s, 1e-5 relative), reference
bounds (1e-20 m³, 1e-19 m³/s, 1e-7 relative), Decimal precision limits, refinement
requirement and energy/stage limits remain unchanged. Saved Radau stage
relations additionally verify each unequal duration with componentwise backward
error at most 5e-10. Finest accuracy, refinement, reference agreement and all
clock/input/energy accounting must pass. A failed criterion stays failed.

The rig preserves 24 earlier source/reference/test files and the original failed
status, comparison and references. Source freezing, exact test inventory,
dependency fingerprints and verified D: backups follow the existing workflow.
Both work and backup are on D:, not independent physical drives. Builds and
static authoring checks pass; no test or numerical evolution ran during authoring.
All execution belongs to a visible independent PowerShell window without live
monitoring. Successful execution or numerical agreement would still leave
continuous-motion accuracy, anatomical mapping, local hair-bundle deformation,
sensory physiology and Avatar balance/recovery unqualified.


## Graded qualification launched

The new run is `canal-graded-20261119-7880d2`, frozen at pushed revision
`b06a811b57d11a5d94adac2c73ae8883889525a6`. Results belong to
`D:/DNNE-build-work/canal-graded-20261119-7880d2`; its initial 2,055 files plus
manifest were verified at `D:/DNNE-desktop-runs/UnityWorld/canal-graded-20261119-7880d2`.
Frozen preflight passed. Visible independent task
`DNNE-Canal-Graded-8f60d7eb0b2d` started successfully. There has been no
post-launch output, status or process inspection. Exactly one final report is
pending: `7880d2`. Do not duplicate or monitor it. Review all original limits,
refinement, reference anchors and substep accounting after the operator reports
its final backup/footer. The full brain/body/world goal remains active and
unfinished; this candidate has not yet been numerically qualified.

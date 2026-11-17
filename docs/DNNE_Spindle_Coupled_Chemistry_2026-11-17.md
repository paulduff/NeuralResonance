# Retained spindle population with attachment and detachment

The completed retained transport study (`spindle-characteristics-20261116-7c6db6`)
preserves shape during isolated movement. The next candidate adds the pinned
Bag/Chain attachment, detachment and cooperative actin equations. This is isolated
Python development; the live Avatar transducer still uses reduced normalized
length/velocity stimuli. No live sensory implementation or editor qualification
has changed.

## State and chemistry

The source is Neuromechanics-Lab/spindleModelTutorial revision
`721ba03cb775261548766ed1349c4bf2e0e35385`, with its AGPL-3.0 license and preserved
original/canonical MATLAB sources. For detached myosin D, attached density n,
total attached B, active actin A and overlap F, the source uses

```
attachment density = f(x) D (A - B)
detachment density = g(x) n(x)
actin on  = 8e7 * 10^(-pCa) * (F - A) * (1 + A/F)
actin off = off_rate * (A - B) * (2 - A/F)
```

Although the source calls A `f_actin_on_unbound`, its equations subtract B from
that state. The candidate therefore treats A as all active sites, including
occupied sites. Both D+B and inactive+A retain separate ledgers. Rates, calcium
drive, passive stress, compliance and overlap follow the pinned source. Zero or
invalid overlap is rejected because those kinetics are not defined there.

Strain rates remain piecewise constant on declared 0.5 nm cells; this is an
explicit spatial approximation. The candidate splits retained material intervals
at rate-cell faces and keeps each existing density. During a fixed-geometry
reaction, an old interval decays exponentially at its local detachment rate;
new attachment adds uniform density within its rate cell. A small ODE integrates
new births, detached myosin and actin. Its total bound mass includes analytically
surviving old attachments. This avoids obtaining a tiny birth population by
subtracting two nearly equal total populations. It does not average an old shape
back onto the rate grid.

Movement uses absolute displacement equal to half the imposed half-sarcomere
length change, following the source compliance. Exact interval intersections
retain in-domain material. Numerical boundary outflow transfers to D, with left
and right outflow recorded separately. That accounting convention is not evidence
that the finite boundary represents physiological detachment. A returning motion
cannot restore an attachment which escaped. New chemistry can form new attachments.

The moving update is a symmetric reaction-half / transport / reaction-half
composition. Geometry stays fixed during each reaction substep; overlap changes
with the supplied movement. Calcium changes and motion reversals must coincide
with supplied boundaries. The diagnostic trajectories prescribe length, not
force equilibrium. Decreasing overlap can make a previously admissible active
state inadmissible; no instantaneous actin reset is invented. Such a failure is
retained for review. The chosen short excursions do not qualify large shortening,
zero overlap, a slack controller or branch selection.

Intervals are never merged, even if tiny or currently equal in density. A declared
100,000-interval capacity rejects further expansion. Exact faces, densities,
physical endpoints, accepted reaction states and case costs are saved. The
potential growth in representation must be assessed before native implementation.

## Prospective visible-window checks

- Twelve controls cover zero-duration preservation, analytic actin activation,
  analytic detachment and attachment, shifted density, an independent full-density
  Radau solve, numerical outflow/return, observation purity, passive force,
  invalid-input rejection and capacity rejection.
- Eighteen fixed-length cases cover both fibers, three original calcium protocols
  and 1,000/500/250 microsecond steps. Compare against all three already reviewed
  independent traces, giving 54 contrasts. The original 1e-5 state, 1 Pa force,
  1e-10 conservation and 1e-8 domain limits remain unchanged. No historical
  reference is overwritten or rerun.
- Thirty-six moving cases cover both fibers, stretch, stretch-return and
  shorten-return, two strain-domain half-ranges and three time steps. Each has a
  40 ms duration, a 4 nm length excursion and pCa 6.5. Preserve every completed
  distribution and both reaction substeps, including accepted solver states.
  Require conservation and admissibility. Record 24 temporal and 18 range
  differences using exact interval integrals of the absolute density difference,
  as well as force, scalar states and observed bins. Bin-total agreement can hide
  internal shape error. Do not treat self-agreement as independent moving accuracy.

Analytic state checks use 1e-9; the passive-force check uses 1e-7 Pa. Those are
declared before execution. Moving cases have no fitted convergence pass threshold:
the output explicitly leaves temporal/spatial convergence and independent moving
accuracy unqualified. The next decision depends on the complete distributions,
reaction states, domain sensitivity and representation cost, not force alone.
Elapsed time is diagnostic; a concurrent study means it is not a performance
benchmark or a CPU/CUDA speed claim.

Preparation requires clean pushed source, D:-verified completed reviews,
unchanged numerical dependencies and preserved upstream licensing. It performs
only provenance checks and copying. All controls and integrations run in an
independent visible PowerShell window. Do not inspect that window's outputs or
processes until the operator reports completion. A passing component run would
still leave moving numerical accuracy, sensory membrane firing, physiological
calibration, native integration and Avatar recovery to demonstrate.

## Launch record

The study is launched as `spindle-coupling-20261117-85ae1d`, frozen at
`0398904f04e58ec99e588cf05b841f799eea32ba`, visible task
`DNNE-Spindle-Coupling-0ed3065e5ae7`. All 1,790 initial files plus manifest
were D: verified. Python/PowerShell syntax was parsed; no control, derivative,
transport or integration executed during authoring/preparation. This run and
`receptor-cubic-20261117-94d05d` await operator completion reports. Neither was
monitored after launch. Do not infer a test pass or running/completed state from
this launch record. The full goal remains active and unfinished.

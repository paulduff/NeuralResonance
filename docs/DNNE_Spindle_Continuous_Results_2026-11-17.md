# Continuous spindle reference: completed numerical review

The operator reported `spindle-eulerian-20261117-f0020c` complete. Review
`spindle-eulerian-review-20261117` verifies 2,184 files per C:/D: copy, frozen Git
source, preserved input provenance and unchanged numerical dependencies. It
reconciles 9,168 sampled endpoints and 47,179 accepted solver nodes across six
fixed and 42 moving cases, plus all saved temporal/spatial/range and candidate
contrasts. The twelve original controls passed; eight saved analytic traces
were rechecked against their saved expectations. No control, derivative, model
or test was rerun during this review. The review itself is D:-verified.

| Comparison | Maximum population L1 difference | Maximum scalar difference | Maximum stress difference (Pa) | Original gate |
| --- | ---: | ---: | ---: | --- |
| Twelve temporal/method contrasts | 1.49314e-12 | 6.00631e-14 | 1.01572e-8 | Passed |
| Six finest spatial pairs (32 to 64 subdivisions) | 0.00483166 | 1.40940e-5 | 44.9948 | Failed |
| Six doubled-domain contrasts | 3.52918e-16 | 9.99201e-16 | 5.82077e-11 | Passed |
| Eighteen saved candidate/reference contrasts | 0.0156439 | 4.18474e-5 | 43.6338 | Not eligible for candidate qualification |

The maximum conservation residual is 2.99761e-15 and maximum sampled/internal
domain excursion is 1.63064e-202. The six fixed reductions pass their eighteen
original independent-reference comparisons. Conservation and fixed reduction
do not remove the moving spatial error.

The failure lies in spatial discretization at the tested resolutions, rather
than the tested ODE tolerance or boundary range. The first-order upwind model
still changes substantially when its spatial cells are halved. All six finest
pairs fail the original spatial criteria: population L1 <= 1e-5, scalar <= 1e-6
and stress <= 0.01 Pa. The successive stress differences fall approximately by
half under refinement, consistent with its known first-order transport error;
this does not prove an exact convergence order or continuum solution.

The original reference design deliberately anticipated this limitation; see
`DNNE_Spindle_Continuous_Reference_2026-11-17.md`. Do not call its finest grid
ground truth, attribute the candidate discrepancy to biology, or relax the
bounds after seeing the result. Further reducing the ODE tolerance is not the
evidence-led next change.

Next improve the spatial transport representation with an independently checked
method, preserving the piecewise source kinetics, full distribution, positivity
and boundary mass ledger. Test pure translation, reversed motion and discontinuous
rate boundaries before repeating coupled moving cases. The retained-interval
candidate also needs its independent moving accuracy resolved. No calibrated
sensory firing, coupled slack trajectory, live integration or Avatar recovery
has been established. No new spindle calculation was launched during review.

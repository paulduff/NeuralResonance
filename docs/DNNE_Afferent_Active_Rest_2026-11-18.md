# Active connected sensory nerve resting-state comparison

Status: **launched visibly**, awaiting the operator's completion report.
Package `afferent-rest-20261118-62949f` is frozen at
`2923baf4ede902ad174ba72deb5d2f202e030ed7`; all 1,936 initial files plus manifest
are verified on D:. Task: `DNNE-Afferent-Rest-ebea164525b3`. No post-launch
outputs, status or process inspected. Its stationary component prerequisite
`54d343` is operator-reported and fully reviewed: 2,472 C:/D: files, 196 focused
cases and exact saved-analysis reproduction. The review folder
`artifacts/afferent-stationary-20261118-54d343-review` has a verified D: copy.
No model or test reran during that review.

## Connected question

The earlier sealed sensory roots omit the active nerve's membrane current.
This study includes the actual reciprocal calyx/cable attachment in every root
trial. It keeps the reviewed hair-cell, cleft, calyx, six-gate axonal membrane,
conductance graph, source tables and stationary-search candidate unchanged.
The declared distal boundary remains sealed. Synthetic geometry, held bath and
axonal ionic reservoirs remain explicit reductions, not calibrated anatomy or
complete metabolic support.

There are eighteen cases: one, two and eight cleft patches, uniform and graded
geometries, each with original, hyperpolarized and depolarized starting guesses.
The nerve has one finite-volume cell in each of the seven declared regions.
Its voltage guesses shift with the sensory voltage guesses. This is not spatial
convergence evidence or a choice of physiological resting voltage.

`AfferentRestStudy` captures the native stationary search without advancing a
cell or touching live state. `afferent_rest_reference.py` separately solves the
same connected equations using independent Decimal derivatives and SciPy's
trust-region least-squares method. Unknown coordinates are binary64, derivative
evaluation uses 60 decimal digits, and 90-digit witnesses check those evaluated
states. This is not a claim of a 60-digit root location. Native points never seed
the independent search. Actual native gates are retained for their own independent
derivative and physical-residual checks.

The search uses equilibrium gates only to eliminate stationary gate equations.
Local stability then restores every free gate coordinate: 61, 72 and 138 physical
state variables for the three cleft sizes. It uses three central-difference
stencils and Richardson comparison, checks the full gate block against `-1/tau`,
and retains matrices, scales, state, eigenvalues and eigenvectors. Saved-only
analysis checks the retained eigenvector residuals, conditioning and reported
classification without another eigensolve. Five analytic spectral controls cover
decay, growth, a neutral direction, oscillatory decay and a defective matrix.

All earlier residual, derivative, precision, root-distance and local-matrix
criteria are preserved. Alternative roots and failed searches remain visible.
The calculation does not add a leak, force a stable solution, reset gates during
integration, prescribe an axon current or broaden bounds to obtain a pass.
A growing linear mode can be meaningful in a firing cell; it is not automatically
a bad numerical result. A decaying estimate does not prove nonlinear stability,
physiological rest or normal afferent firing. Eigenvalue uncertainty is an
empirical numerical estimate, not a rigorous enclosure.

## Execution and evidence

`afferent_rest_gate.py prepare REVIEW_FOLDER` requires the passing reported
stationary-component review and its D: backup, preserves fifty source files,
and freezes a clean pushed revision. `start-afferent-rest-window.ps1` then runs
196 focused checks, eighteen native searches, eighteen independent searches,
captured-point witnesses and saved-result analysis in a visible PowerShell
window. Final evidence receives a verified D: backup on success or failure.
No live monitoring, Unity player, live brain access or time integration.

Authoring probe build passed with zero warnings/errors at
`D:/DNNE-build-work/afferent-rest-authoring-20261118`. Four Python AST/import
graphs, eighteen fixture/state identity layouts, the 196-case inventory, fifty
preserved paths and PowerShell syntax passed static checks. No new equation,
root, eigensolve, analytic control or test executed inline.

Pending motor `b224a6` and fast nerve trajectory `cf2f38` have not been inspected.
Longer coupled trajectories, perturbation responses, actual emitted/propagated
sensory events and causal DNNE delivery remain required before Avatar balance
and recovery. There is no live Avatar/Unity/editor selection change.

# Joint hair-cell, cleft and calyx time evolution

`AvatarVestibularCleftDynamics` is a new isolated time integrator around the
reviewed `AvatarVestibularCleftMembranes` equations. The derivative review is
`artifacts/cleft-membranes-review-20261118/review.json`: 108 focused cases, 216
independent complete-derivative comparisons and 816 byte-identical earlier
records pass, with both C:/D: copies verified. Those results qualify the
instantaneous equations; they cannot qualify this newly authored time integrator.

The authoring test-project build passes with zero warnings/errors. Twelve new
component cases are authored, for 120 cases with the earlier components. No
new model, numerical control or test has run inline. All actual execution belongs
to the separate visible PowerShell workflow; there is no live monitoring.

## One complete evolving state

The state contains the shared hair potential and, for each identified patch,
cleft/calyx potentials, cleft K/Na and seven actual channel gates. Initial values
and gates must be provided explicitly. Construction evaluates no model, resets
no gate to its target and makes no stationary-state claim. The two bath potentials
are held for the cell's lifetime. Bundle displacement and every identified axon
current must be supplied for each held interval. A zero axon current is an
explicit sealed terminal, not an implemented afferent cable.

The selected method is the two-stage, second-order L-stable SDIRK method with
`gamma = 1 - 1/sqrt(2)`, `a21 = 1-gamma` and weights `(1-gamma,gamma)`. Both
stages solve all voltages, ions and gates simultaneously. Newton uses the actual
coupled derivative, a central finite-difference Jacobian (one-sided within state
boundaries), pivoted dense elimination and a bounded in-domain line search.
This is a numerical choice for the fast electrical coupling, not a change to
biological conductances or kinetics.

The step and nonlinear tolerances are explicit fingerprinted settings. The
default residual scales are `1e-9 mV`, `1e-11 mM`, `1e-12` for gates and `1e-11`
relative. The API caps the requested step at `.05 ms`; this is not an accuracy
guarantee. Independent temporal refinement remains required. There is no hidden
adaptive retry, state clipping, diagonal regularization or substituted integrator.
Dense Newton is an initial CPU candidate; no speed or CUDA claim is made.

Accepted states use the common Runge–Kutta stage quadrature with compensated
addition. The local clock is an integer step count plus an explicit epoch. One
thread owns advancement/capture. Changing the outer call's packet size does not
reset the solver, gates, clock or compensation.

## Electrical and chemical accounting

The same stage weights integrate separate transfer ledgers for:

- One MET current and both externally supplied bath holding currents.
- Each patch's hair-to-cleft, calyx-to-cleft, outer-calyx and axon charge, and
  its hair/inner-calyx potassium and sodium amounts.
- Each spatial edge's cleft charge, calyx axial charge and K/Na amounts.
- Each opening's charge and K/Na transfer to the held bath.

After each step, capacitor charge changes and local K/Na amount changes are
reconciled with those signed transfers. Charge differences are computed directly
from voltage differences against the initial state to avoid subtracting large
absolute capacitor charges. The maximum residuals are retained as measurements;
they do not stand in for an independently accurate trajectory. Calcium remains
electrical-only and unspecified leak/other-ion currents acquire no invented
species. The model still has the previously declared fixed intracellular/Ca
reservoirs, source outer reversals and room-temperature kinetics.

## Failure and capture

A failed step preserves the entire accepted prefix: state, compensation,
ledgers, ledger compensation and clock. Its phase, stage, iteration, stage base,
trial state, available residual and forcing are retained. A faulted cell cannot
silently resume. Bad public arguments are rejected before advancing and do not
poison an otherwise valid cell. Capture returns detached arrays, including the
initial state and complete field labels. This is a diagnostic snapshot; a
checkpoint restore API has not been implemented or qualified.

## Prospective checks and subsequent qualification

The twelve cases cover explicit unconditioned initialization; analytic relaxation
of all seven gates in a passive fixed-potential control at two resolutions;
second-order gate refinement; exact chunk/order invariance; local charge/ion and
terminal accounting; distinct bundle/axon responses; numeric-failure prefix
preservation; invalid inputs; snapshot isolation; numerical/source fingerprints;
and owner/clock rules. These use controlled fixtures. They do not establish
anatomical geometry or afferent physiology.

`cleft_dynamics_gate.py` requires the reviewed complete-derivative capture and
its verified D: copy, preserves all seven biological source files and earlier
tests, pins the exact managed runtime and freezes a new package. The visible
`start-cleft-dynamics-window.ps1` builds from that source, runs exactly 120 cases,
retains test/binary/runtime evidence, records stages and verifies a final D: copy.
The independent coupled trajectory and stationary initialization flags remain
false even if those component checks pass.

After component review, capture full active voltage/ion/gate/ledger trajectories
with declared input transitions and compare against independently integrated
equations at several candidate steps and reference tolerances. Establish an
actual stationary solution and its stability rather than calling a fixed quiet
duration equilibrium. Then add the supported afferent mechanism, qualify its
firing and connect measured head motion. No live Avatar/World/editor selection
or brain sensory stream changes in this stage; balance and recovery remain open.

The 120-case component workflow is launched as `cleft-dynamics-20261118-ca2f31`,
frozen at pushed revision `b4f0d57fa93fab7f935547345d8e078a05270608`. All 1,847
initial files plus manifest are D: verified. Visible task:
`DNNE-Cleft-Dynamics-9c501d09763f`. The launcher returned successfully; no
post-launch output, status or process has been inspected. Await the operator's
completion report before reviewing or claiming component success.

The operator has since reported completion. The review in
`artifacts/cleft-dynamics-review-20261118` verifies all 2,391 C:/D: files, source/
binaries/runtime and all 120 focused cases. Saved analysis reproduces exactly;
the review is separately D: verified. No test/model was rerun during review.
The next independent active-trajectory workflow is authored in
`DNNE_Cleft_Trajectories_2026-11-18.md`. The component pass does not replace that
comparison or stationary-state/physiological qualification.

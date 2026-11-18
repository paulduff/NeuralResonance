# Connected sensory nerve stationary search

Status: native search and its guarded component workflow authored; build and
static checks pass. **No new model, analytic control or test has run.** The
active loaded-root comparison and full free-gate stability study remain to be
authored. Prerequisite dynamics `0e0e99` is now reported/reviewed and passing;
its review is D: verified, so this component workflow can be prepared next.
No stationary package or window has been started. Current pending windows
are fast nerve trajectories `cf2f38` and saved motor comparison `b224a6`.

The reviewed sealed sensory roots cannot simply initialize an attached active
nerve. Cable membrane current loads the calyx, changing its voltage and the
cleft's coupled charge balance. The relevant stationary problem contains all
these unknowns together. A stationary solution may be unstable or nonunique;
finding one does not qualify physiological rest or sensory firing.

## Native candidate

`src/NRE.SimAvatar/AvatarAfferentStationarySearch.cs` adds an isolated algebraic
search over the hair-cell voltage, each cleft/calyx voltage and K/Na
concentration, and every identified cable voltage. Voltage coordinates are
relative to the explicitly held bath; ion coordinates are natural logarithms.
The existing complete joint derivative supplies the residual for every trial,
with reciprocal same-state cable/calyx current and no prescribed axonal load.

At an algebraic stationary point, each finite-positive-time-constant gate
satisfies `g = g_inf(V)`. The search uses this exact elimination only while
solving the stationary equations. It returns actual sensory and axonal gate
states with the candidate solution. Free time integration still evolves every
gate by its existing differential equation. The search never advances, settles,
resets or edits a live cell or an existing dynamic instance.

The Newton/line-search method follows the earlier sealed stationary candidate
in a separate class. Existing kernels, solver, source conventions and frozen
evidence remain unchanged. Stable identities determine canonical unknowns;
input-list ordering does not assign a voltage to a different compartment.
Acceptance checks all physical voltage/ion derivatives and every gate
derivative, rather than accepting only an optimizer status or small step.

Every accepted iterate, scaled physical residual, final trial, failure phase
and numerical work count is returned. Concentrations are transformed, never
clipped. Singular equations are not repaired with an invented leak, diagonal
term or pseudo-inverse. A nonconverged result remains explicitly unqualified.
Root residuals do not establish uniqueness or local stability. The held bath,
axonal ionic reservoirs, room-temperature kinetics and synthetic geometry
remain the earlier declared reduction, not calibrated anatomy.

## Component controls and execution

`AfferentStationarySearchTests` declares fifteen cases, 196 with the existing
component inventory. Numerical fixtures include an analytic connected passive
cable root, a Kv1-only nerve at its potassium reversal potential, convergence
from two perturbation directions, reciprocal load before convergence, and a
severed passive cable with an unanchored mean. Channel removals are diagnostic
controls, never proposed biological defaults.

Other controls cover identity permutation, bath reference shifts, constructing
a new dynamic cell from a known root, preserving an existing cell and caller
guesses, invalid inputs, failed channel-domain trials, bounded search work,
nonunique passive roots and independent returned arrays. A disconnected passive
cable need not possess a unique root; the control does not demand one.

`afferent_stationary_gate.py prepare REVIEW_FOLDER` requires a passing,
operator-reported, fully reviewed and D:-verified **joint dynamics** result.
It preserves 46 earlier source files and freezes clean pushed Git source,
runtime, source decisions and the exact 196-case inventory. Initial verified
D: backup precedes `start-afferent-stationary-window.ps1`; final evidence and
backup remain visible in its independent PowerShell window. No live monitoring.
`review-afferent-stationary.py` reviews saved evidence only after operator
completion. This workflow has not been prepared or launched.

Authoring build of the full test project succeeded with zero warnings/errors at
`D:/DNNE-build-work/afferent-stationary-authoring-20261118`. Static Python and
PowerShell syntax, the 15/196-case inventory and preserved source paths pass.
Source inspection finds callers only in the new test class. No live Avatar,
Unity, DNNE host or editor selection changed.

## Following evidence required

The fast joint trajectory comparison remains the next time-integration study
after the pending dynamics review. The stationary controls can be qualified
separately from it. An active loaded-root study must then use independent
equations/searches and multiple starting guesses, retaining alternative roots
and nonconvergence. Full local stability must include actual free gate state,
not the gate-eliminated algebraic Jacobian. Retain eigenvectors as well as
eigenvalues and matrices so subsequent reviews can independently check residuals
and conditioning; the earlier sealed study lacked those vector witnesses.

Longer free trajectories, perturbation responses, emitted/propagated sensory
events and causal delivery to identified DNNE contacts remain necessary before
claiming live sensory return, Avatar balance or fall recovery.

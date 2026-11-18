# Explicit stationary initialization search

`AvatarCleftStationarySearch` is an isolated initializer for the declared coupled
hair/cleft/calyx equations. It does not modify a running cell, choose a live model
or treat elapsed quiet time as equilibrium. The existing dynamic integrator and
biological channels/transport are unchanged.

The unknowns are the shared hair voltage and each patch's cleft voltage, calyx
voltage, potassium and sodium. Voltages are relative to the held bath; positive
concentrations use natural-log coordinates. At a stationary point,
`dg/dt=(g_inf-g)/tau=0` with finite positive tau permits exact algebraic elimination
of the seven gates per patch. This elimination is exclusively for root finding;
time integration still evolves every gate.

The search evaluates the complete membrane/spatial owner, uses a finite-difference
Newton Jacobian with row-scaled pivoted elimination, and accepts only a decreasing
scaled physical-rate residual. No diagonal regularization, pseudo-inverse,
concentration clipping or invented constraint hides underdetermination. Defaults:
voltage rate `1e-9 mV/ms`, ion rate `1e-11 mM/ms`, gate rate `1e-12/ms`, at most
40 Newton steps and 24 line-search halvings. Initial guesses, explicit terminal
currents, settings, source fingerprint, accepted iterates, final trial/error and
failure phase are retained. Exhausted work is a failed search.

A stationary point can be unstable, nonunique or physiologically implausible.
Those qualification flags remain false even on root-search success. A future
stability check must use the **full voltage/ion/gate dynamic Jacobian**, not the
gate-eliminated stationary Jacobian. Multiple active starting guesses and
independent root residuals are also required before choosing physiological rest.

Twelve new component cases cover an analytic hair-KL/calyx-leak root, perturbed
guesses, opposite nonzero terminal flows, canonical identity, initialization of
a separate dynamic cell, invalid ports/ions, numerical failure, work exhaustion,
nonunique passive equilibria and nonmutation of caller/live state. Channel
removals are analytic fixtures, never proposed biological defaults. The earlier
120 component cases remain in the 132-case inventory. The test-project authoring
build passed; tests execute only in visible frozen PowerShell.

The finer sensory workflow includes these 132 cases. Its trajectory initial
states remain the original prescribed states: it does **not** use stationary
search to soften the initial transient or improve the numerical comparison.
`cleft_stationary_gate.py` and its launcher also support a separate component-only
study, but no separate stationary window has been launched at this checkpoint.

The completed finer study `884934` now verifies all twelve stationary controls,
within its 132-case suite and complete C:/D: saved-data review. This qualifies the
analytic component controls, not active-model stationary roots or their stability.
Next stationary work remains independent active root checks and the full dynamic
Jacobian/perturbation study, with the current channel equations preserved.

The next active-root and full-gate local-Jacobian workflow is now authored in
`DNNE_Cleft_Active_Rest_2026-11-18.md`. Eighteen native and independent searches
retain failed/nonunique/unstable outcomes. Local linear stability is evaluated
separately from root residuals; no perturbation trajectories or live initialization
are performed yet. The existing search and biological kernels are unchanged.

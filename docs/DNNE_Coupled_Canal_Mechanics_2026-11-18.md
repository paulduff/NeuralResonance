# Coupled canal mechanical candidate — 18 November 2026

The existing Avatar labyrinth and isolated physical-clock mechanics were reviewed
before this addition. Their inherited 45 ms/5 s filters produce a reduced
angular-velocity coordinate, not measured cupula displacement. The new candidate
represents fluid inertia, viscous coupling and elastic cupula deformation from
the retained Ifediba 2007 right human labyrinth-1 specimen. It is an isolated
mechanical component; it is not selected by Avatar, Unity, the editor or DNNE.

## Equations, source and units

`AvatarCoupledCanalParameters.PublishedRightLabyrinth1()` reads the hash-bound
catalog `docs/reference-data/vestibular-ifediba-2007.json`, embedded in SimAvatar.
The retained source audit is `DNNE_Vestibular_Mechanical_Source_Audit_2026-11-18.md`.
No downloaded source is executed. The original printed values and exact SI
conversion strings remain available beside the candidate implementation.

The model is `M Q'' + C Q' + K Q = F`, with six volume coordinates ordered as
endolymph HC/AC/PC followed by cupula HC/AC/PC. Volumes are m³, flow is m³/s,
time is seconds and forcing is Pa. Source mass, damping and stiffness matrices
multiply by 100,000 for SI; source forcing coefficients multiply by 0.1.
The parameters are copied, exposed read-only and included in a source-labelled
fingerprint. Mass and damping must be symmetric positive definite. This candidate
explicitly requires the published cupula-only diagonal stiffness topology.

Input is the three declared source-prime angular-acceleration components in
rad/s² used by equation 26. The rounded, nonorthogonal prime directions are not
normalized or treated as Cartesian axes. No source-prime/skull transform,
bilateral mirroring or local cupula-to-bundle deformation map has been chosen.
Those conventions require separate controls against the source's geometric
definition. This specimen is not calibrated Avatar anatomy. The rigid mechanics
also does not qualify the high-frequency mechanics of impact or falls.

The organ mechanics is non-neuronal tissue upstream of sensory transduction.
It does not replace neuronal activity with a controller, impose spikes, alter
learning or conflate cupula relaxation with sensory adaptation. Otolith mechanics
requires a separate source-based model and remains unchanged.

## Numerical method and energy accounting

`AvatarCoupledCanalDynamics` uses two-stage, third-order Radau IIA for a held
input. Its tableau is `[[5/12,-1/12],[3/4,1/4]]`, with nodes `[1/3,1]`
and weights `[3/4,1/4]`. The coupled twelve-unknown stage acceleration system is
solved with row-scaled pivoted LU. Each solution retains its componentwise
backward error and rejects error greater than 1e-12. This is a linear-solve
criterion, not a trajectory-accuracy or physiological criterion.

The method is L-stable, useful for the very different mechanical timescales.
It has numerical damping, which must remain visible separately from viscosity.
Each immutable transition records both stages, the before/after state, work,
stored energy, physical damping, numerical damping and the energy residual.
Stored energy is `(v' M v + q' K q)/2`. Physical dissipation uses the Radau
quadrature of `v' C v`. The additional numerical energy loss is
`h²/32 * ((v1-v2)' K (v1-v2) + (a1-a2)' M (a1-a2))`.
This follows from the Radau algebraic-stability matrix
`[[1,-1],[-1,1]]/16`; it is not assigned to a biological mechanism.

The core is stateless: the caller receives a complete immutable transition and
owns accepted time and input. A failed call cannot mutate the caller's accepted
point. Physical sample ownership, retry handling and held-input timing remain
the responsibility of a future explicit adapter. This core is not a replacement
for the existing physical-clock owner, and no such owner is bypassed live.

## First qualification window

The new `CoupledCanalMechanicsTests` class declares 23 cases: source units,
immutable parameter identity, matrix validation, finite seconds, zero rest,
the dimensional force equation, coupled response, sign reversal, passivity,
separate physical/numerical loss, work balance, rejected-input preservation,
stiff-mode damping and refinement against two independent analytic solutions.
The analytic controls are synthetic diagonal mechanics: viscous free flow and
a critically damped spring. Their h, h/2, h/4 errors must stay below 2e-6 and
reduce by at least a factor of five at each refinement. They are not human
physiological fixtures or an independent trajectory check of the source specimen.

The visible window also runs the existing 16 physical-clock, 12 skull-coordinate
and 25 bundle-input regressions: 76 total. Frozen pre-candidate Avatar, protocol,
neuronal and Unity kernels are byte-preserved. Source equations, managed runtime,
numerical environment, binaries and exact test inventory are recorded.

Authoring builds of SimAvatar and the full test project succeeded without
warnings or errors. No tests or models were run inline. The freezer/launcher is
`coupled_canal_gate.py` / `start-coupled-canal-window.ps1`. Work and its verified
backup use D: to preserve C: space for the pending sensory retry. These two new
copies share a physical drive. Earlier raw/source evidence retains its C:/D:
copies. Every test executes in a separate visible PowerShell window, with no
live monitoring; results await the operator's final report.

After component review, independently compare the coupled source-specimen
trajectories across fast and slow regimes before physiological mapping. The
coordinate conversion, physical sample adapter, local bundle mechanics, afferent
firing and connected Avatar balance/recovery remain unqualified. No existing
model default, live control, old save, failed result or acceptance bound changes.

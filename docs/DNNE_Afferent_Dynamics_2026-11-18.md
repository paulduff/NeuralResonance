# Joint sensory nerve time evolution

Status: **launched visibly**, awaiting the operator's completion report.
Package `afferent-dynamics-20261118-0e0e99` is frozen at
`f7c0ae8697e21f9f0f1107abc9d9c107f0fec501`; initial 1,915 files plus manifest
have a verified D: copy. Task: `DNNE-Afferent-Dynamics-2761f19f203e`.
No post-launch results, status or process were inspected. The prerequisite
derivative comparison `4ab20e` is reviewed/passing; see
`DNNE_Afferent_Numerical_Results_2026-11-18.md`.

## Why this is needed

Reinspection of `ControlProgram/PhysicalBodyTransducerRuntime.cs` and the
`/api/v1/admin/input/body-frame` endpoint confirms that the live vestibular
route still produces frame-driven receptor stimuli. Its own source comment
correctly distinguishes those messages from recorded afferent membrane spikes.
The endpoint supplies the brain's current timestamp to that conversion.
`StructureEngine` then integrates those inputs into receiving cells, but that
does not turn the original stimuli into observed sensory nerve firings. The
earlier audit `DNNE_Sensory_Boundary_Audit_2026-11-09.md` remains applicable to
this legacy path. The new physical sampling/kinematics studies are separate
isolated improvements; this inspection does not claim their results repaired
the currently selected ingress route.

Before replacing that route, the identified sensory membrane and nerve must
actually evolve together. A cable current changes both its own voltage and the
calyx voltage that supplied it. Advancing one side while holding the other's
current would introduce a new coupling approximation.

## Implementation

`src/NRE.SimAvatar/AvatarVestibularAfferentDynamics.cs` owns the joint state:
one hair-cell voltage; eleven variables per cleft/calyx patch (two voltages,
two concentrations, seven gates); seven variables per axonal compartment
(voltage and six actual gates). Each variable has a stable named position in
saved snapshots. No gate is replaced by a voltage-conditioned equilibrium.

The new owner uses two-stage SDIRK2, with every nonlinear evaluation calling
the complete `AvatarVestibularCleftAfferent` derivative. Sensory and cable
voltages/gates therefore share the same implicit stage. Bundle displacement
is an explicitly held input; there is no prescribed axon-current parameter.
The bath potential and the previously declared axonal ionic reservoirs remain
held. This is a reduced model, not anatomical/physiological calibration.

The existing sensory-only solver, biological kernels, derivative references,
live host, Unity selection and editor behavior are unchanged. The new solver
follows the existing SDIRK2 implementation in a separate class so that earlier
frozen trajectories and their owner remain intact. Shared solver extraction
is deferred until the joint candidate has independent numerical evidence.

An integer local step counter owns time. Grouping calls cannot choose a new
step size or re-anchor the epoch. Construction only copies explicit initial
state; it does not settle, evaluate or claim a stationary solution. A sealed
calyx resting state cannot be assumed to remain stationary with an active
nerve attached. State, time and compensated flux sums commit together only
after a complete accepted step. Numerical failure retains the accepted prefix
and the failing stage; there is no restart, clipping, hidden smaller step or
fallback model.

Ledgers retain the earlier sensory charge/K/Na transfers and add each cable
membrane's outgoing charge, each cable edge's directed charge and each
terminal's charge. Capacitor changes are checked per cable compartment; each
calyx patch's outgoing charge is reconciled with its attached terminal charges.
The existing `perilymphHoldingChargeFc` entry describes the sensory subsystem;
it is not renamed or reported as the combined sensory-plus-axon bath current.
No axonal concentration, ATP, total energy or physiological reserve conservation
is inferred from this electrical accounting. Residual maxima are observations;
numerical acceptance requires the separate declared comparison bounds.

## Prospective component checks

`VestibularAfferentDynamicsTests` declares fifteen cases, 181 including the
earlier component suite. They cover explicit initial state, analytic passive
gates, analytic two-compartment cable discharge with temporal refinement,
input ordering and call grouping, local charge/ion/interface accounting,
reciprocal calyx loading and severed contacts, electrical reference shifts,
invalid/domain-failing input, preserved snapshots, fingerprints and owner clock.

The passive cable control has two equal capacitors, a 0.5 nS connecting edge
and a severed external terminal. Its voltage difference decays with time
constant `C/(2*g)`; these constants and the expected convergence ratio are
declared before execution. The out-of-domain sodium extrapolation is an
explicit failure control, not a physical initial condition to be repaired.

`tools/review-afferent-numerics.py` requires an operator-reported completed
package and verifies all C:/D: files, Git source, binaries/runtime, 166 tests
and exact reproduction of saved derivative comparisons. It performs no model
or reference evaluation. Its fresh review folder needs a verified D: copy.

`tools/afferent_dynamics_gate.py prepare REVIEW_FOLDER` then freezes a clean,
pushed source revision while preserving 39 reviewed source files. The new
`start-afferent-dynamics-window.ps1` requires the initial D: backup and runs all
181 component cases visibly, with full final preservation. No live monitoring;
the operator reports completion before results are inspected.

Authoring evidence: probe and full test-project builds passed with zero
warnings/errors at `D:/DNNE-build-work/afferent-dynamics-authoring-20261118`.
Python syntax, all 181 case declarations, preserved source paths and PowerShell
syntax passed static checks. None of the new tests, equations or analytic
controls has run. Active trajectory accuracy, loaded stationary state,
spike-event production/propagation, source-complete delivery into declared DNNE
contacts and usable Avatar balance/recovery remain required.

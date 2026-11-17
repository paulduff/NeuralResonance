# Isolated physical-clock vestibular mechanics candidate

## Reason for the candidate

The canonical Avatar and ControlProgram paths were inspected before this change,
including `VestibularLabyrinthTransducer`, `PhysicalBodyTransducerRuntime`, the
physical-sample clock/producer/receiver, and the migration audit. The existing
bilateral canal axes and reduced cupula, adaptation and otolith dynamics remain
useful starting points. They do not establish a calibrated labyrinth, hair-cell
membrane or individually observed vestibular-nerve firing.

`VestibularLabyrinthTransducer.Transduce` currently chooses 1/30 second when no
positive elapsed interval is supplied, including the first observation. Positive
intervals are clamped to 1 ms through 1 s. Repeated or reversed sequence/time can
reset the transducer's source state. The surrounding body transducer rejects
some repeated/reversed frames, so this is a property of the organ implementation,
not evidence that every such reset occurs in live use. Its canal implementation
also advances adaptation before the cupula, using the already-updated adaptation
throughout the cupula interval. These are explicit timing/numerical issues to
separate from physiological calibration.

The new `src/NRE.SimAvatar/AvatarClockedVestibularMechanics.cs` is an **isolated,
unexecuted candidate**. No existing live Avatar, Unity, ControlProgram or editor
selector uses it. No neuronal model, historical save or completed capture changes.

## Physical ownership and causal convention

An instance belongs to one thread and one immutable `PhysicalSampleClock`, with
the existing stable session/body/source/sample identities. It consumes every
successive integer physics step, retaining fractional physical and neural epoch
mapping. The nominal clock step controls elapsed mechanics; neither integer
request timestamps nor wall time nor packet delivery cadence supplies an
integration interval.

Inputs are explicit skull pitch/yaw/roll angular velocities in rad/s and the
gravito-inertial vector in skull right/down/forward coordinates in m/s^2. Each
input carries the original physical observation's SHA-256. That hash is a
provenance reference, **not verification of the body-to-skull transformation or
authentication**. This component does not decode an observation or establish
how/when a measured acceleration was available. Those steps require their own
validated adapter before any live use.

The first sample establishes zero canal/adaptation state and initializes the
otolith filter to its observed vector, at the declared initial epoch. This is
an explicit initial condition, not reconstructed prehistory or elapsed evolution.
Subsequent transitions hold the **previous** endpoint's measured input over the
following physical interval. The newly received endpoint becomes the next held
input only after that interval completes. Thus two histories differing only in
the new endpoint have identical mechanics through the completed past interval.

This causal zero-order hold is an approximation to intervening physical motion.
An analytic transition under a held input does not recover a continuously moving
head trajectory, nor establish adequate sampling. Real endpoint reconstruction,
availability time and sampling refinement remain separate work.

An exact replay of the last sample returns the same immutable receipt and state.
A changed last payload, an older sample, a gap, a foreign identity or invalid
measurement is rejected before changing completed state. There is no neutral
fill, implicit restart, extrapolated future sample or elapsed-time clamp. A
nonrepresentable numerical transition faults the epoch and retains the last
completed mechanics and held input. That fault cannot be reset or resumed.

## Reduced equations and units

Constants are inherited as declared double values from the existing reduced
model: cupula time constant 0.045 s, adaptation 5 s and otolith 0.080 s. The
bilateral directions retain yaw/-yaw, LARP = (pitch-roll)/sqrt(2) and
RALP = (pitch+roll)/sqrt(2), with opposing partners negated. These directions
are inherited conventions, not newly measured human canal geometry.

For a held directional velocity `u`, adaptation `a` and cupula coordinate `c`:

```text
da/dt = (u - a) / tau_a
dc/dt = (u - a - c) / tau_c

e_a = exp(-h/tau_a)
e_c = exp(-h/tau_c)
a_next = a + (u-a) * (1-e_a)
c_next = c*e_c + (u-a) * tau_a/(tau_a-tau_c) * (e_a-e_c)
```

The implementation evaluates `e_a-e_c` as
`e_a * (1-exp(-h*(1/tau_c-1/tau_a)))` and uses a small-argument polynomial for
`1-exp(-x)` to limit cancellation. This is the analytic transition of the stated
linear equations in exact arithmetic, implemented in double precision. It is
not a statement that floating-point results or the biological model are exact.
`a` and `c` have rad/s units; the inherited name **Cupula denotes a reduced filter
coordinate, not measured membrane displacement**.

Each otolith component follows `do/dt = (v-o)/tau_o`, with its analytic held-input
transition. It retains m/s^2 units. The candidate provides no tanh conversion,
normalized population activation, firing-rate conversion, artificial spike train,
posture command or motor response. Actual receptor membrane, synaptic release and
afferent axonal events must be modeled and qualified separately.

## Authored checks and remaining execution

`ClockedVestibularMechanicsTests` contains sixteen authored cases:

* Initial conditions without invented elapsed time; new endpoint causality.
* Exact retry, conflicting retry, missing/reversed step and foreign epoch.
* Finite identified input, single-thread ownership and fractional epoch mapping.
* Constant-input subdivision, unclamped long interval and directional pairing.
* Static gravity, unnormalized mechanical coordinates and fault preservation.

The test project builds in Release with zero warnings and errors. **These tests
have not executed.** No numerical calculation or model ran inline. No third
PowerShell study was launched for this candidate while the rubral replacement
and continuous spindle reference await operator reports.

The next vestibular qualification must run visibly in a frozen, D:-backed
PowerShell capture. It should execute these ownership cases and an independent
integration of the coupled linear equations, preserving complete inputs/states
and declared bounds. Include step/stop/reversal, otolith vector changes, small
and large physical intervals and held-input subdivision. An independent matrix
exponential or error-controlled ODE reference must establish its own accuracy;
the candidate's self-consistency alone is insufficient. Validate the actual
body-to-skull transform and sampling availability before coupling a receptor.

Even a numerical pass will not qualify hair-cell physiology, vestibular-nerve
firing, learned vestibular circuits, balance or Avatar recovery. The source
reviews in `DNNE_Vestibular_Source_Candidates_2026-11-16.md` and
`DNNE_Afferent_Source_Gap_2026-11-16.md` still apply. Do not substitute generic
population-generated spikes for the missing receptor/afferent qualification.

## Comparison draft checkpoint

An unlaunched comparison draft now exists in `VestibularMechanicsStudy.cs`,
`vestibular_numerics_protocol.py`, `analyse-vestibular-numerics.py` and
`prepare-vestibular-numerics.py`. It specifies 62 component/regression cases,
twenty prescribed histories, two native delivery groups, six reference controls,
a matrix exponential and two DOP853 tolerances. All accepted ODE nodes and native
inputs/receipts would be retained. The numerical bounds are declared in the
protocol; none has been exercised or adjusted against results.

The reference uses the documented general matrix-exponential algorithm and a
separately written derivative, rather than copying the candidate's scalar
transition. See the official SciPy [matrix exponential documentation](https://docs.scipy.org/doc/scipy-1.16.0/reference/generated/scipy.linalg.expm.html)
and [ODE integration documentation](https://docs.scipy.org/doc/scipy-1.16.0/reference/generated/scipy.integrate.solve_ivp.html).
The installed numerical environment remains pinned separately to NumPy 2.2.6
and SciPy 1.16.3.

**The visible launcher is not yet authored, and preparation explicitly refuses
to create a package without it.** The operator's completed spindle/rubral
reports and request for simpler explicit synaptic wiring took priority. Finish
and review this draft before claiming an executable vestibular comparison;
no vestibular study or live selection has been launched.

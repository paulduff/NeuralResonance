# Separate olive accuracy and fixed-input Purkinje refinement

The connected timing review found exact packet-cadence invariance but substantial
source-trajectory differences under solver refinement. An early retained LTD
maximum concealed later input divergence. This rig separates source numerical
accuracy from receiving-cell refinement before changing biological parameters.
No production runtime, learning constants, Avatar physiology, editor interface,
save fingerprint or live service is changed by this work.

## Immutable stimuli and provenance

`prepare-split-numerics-inputs.py` reads the completed
`connected-timing-20261102-26272f` capture without modifying it. Four source
schedules preserve actual olive arrival times: ordinary paired stimulation and
inhibition, each at offsets 0/60 ms. The original four excitatory contacts,
separate inhibitory contacts, fractional event times, quanta and identities are
retained. Source stimulation lasts 60 s with 58 probes; inhibition is 10–30 s.
Glucose withdrawal is not included: the independent reference does not yet
include metabolic feedback.

Two complete recorded olive trains from the finest prior paired condition are
frozen for Purkinje replay, one per offset. These are diagnostic stimuli, not
certified physiological outputs. Every original spike message, emission time,
conduction delay, parallel-fibre stimulus and receiving contact identity is
retained. There is no resampling, merging, regenerated source train or fitted
teaching pulse. Production receiving engines use the same isolated instance
identity and two fresh cells, retaining active, late, dormant, other-cell and
nonparallel contacts.

The bundle also carries the archived default-resolution olive spike times and
20 ms voltage/energy samples. Each matching default production control must
reproduce those archived values and counts. File hashes identify all source
evidence. Neither the old saves nor any completed capture is loaded as active
neural state.

## 80 full conditions

| Component | Conditions | Purpose |
|---|---:|---|
| Production olive, metabolic feedback omitted | 24 | Four schedules × three coupling ceilings × two membrane ceilings |
| Production olive, normal energy feedback | 12 | Four schedules × three coupling ceilings; normal membrane ceiling |
| Independent reference | 20 | Four schedules × three accuracy tolerances, plus two initial-state perturbations at the strictest tolerance |
| Fixed recorded input to Purkinje | 24 | Two recorded trains × three variants × four receiving solver ceilings |

Each full condition lasts 60 biological seconds. The independent PowerShell
window first runs a four-second technical preflight, then the complete study.
The preflight uses a prefix of the immutable schedule, so it does not reach the
10–30 s inhibition challenge. Separate analytic receptor checks exercise GABA
before launch. Samples are saved every 2 ms; all source crossings, delivered
messages and outgoing Purkinje events are recorded separately.

The production source outer grid stays at 0.125 ms and splits at actual input
arrivals. Coupling ceilings are 0.025, 0.0125 and 0.00625 ms. Membrane ceilings
are independently 0.025 and 0.00625 ms in the energy-omitted controls; energy-on
controls retain 0.025 ms. The membrane can still choose smaller steps under its
existing conductance/slope limits. `SourceNumericalStepper` is tool-only; its
default path is checked against the unchanged production wrapper, including
both energy modes. Energy-on/off differences are diagnostic mechanism contrasts,
not numerical error estimates against the reference.

Purkinje variants are intact calcium learning, blocked calcium entry, and
disconnected climbing input with the original parallel inputs retained. Receiving
ceilings are 0.125, 0.0625, 0.03125 and 0.015625 ms at fixed 0.125 ms host cadence.
All variants begin fresh; the production calcium and expression mechanisms,
including the retained-maximum approximation, remain unchanged.

## Independent numerical reference and its limits

`olive_numerical_reference.py` expresses the same implemented 14-state olive
equations and four receptor states per contact independently in Python. It
advances them together with [SciPy's DOP853 solver](https://docs.scipy.org/doc/scipy/reference/generated/scipy.integrate.solve_ivp.html).
No production slope or solver routine is called. Piecewise-constant transmitter
release starts at each archived arrival and ends exactly one millisecond later;
integration restarts at every release boundary. Per-contact receptor occupancy
is not replaced by one combined saturating contact.

Relative tolerances are 1e-7, 1e-9 and 1e-11. Absolute tolerances are scaled by
state type: voltage `rtol*0.01`, gates `rtol*1e-4`, membrane calcium
`rtol*0.001`, receptor calcium `rtol*1e-5`. Maximum accepted-step ceiling is
0.125 ms. The reference itself must be assessed across tolerances; using a
mature solver does not certify convergence of a long driven trajectory.
Upward soma/axon zero crossings use the solver's root interpolation. Production
crossings are labelled at the end of their enclosing outer interval, so up to
0.125 ms of time-label difference can arise without a different spike trajectory.
Counts between actual stimulus arrivals accompany ordinal comparisons; equal
totals do not establish matching burst membership.

At the strictest tolerance, separate diagnostic cases perturb initial soma
voltage by ±0.001 mV. This replaces an initially considered ±1e-7 mV perturbation,
which was too small relative to the observed reference-tolerance uncertainty to
be a useful separate control. No live cell is perturbed or reset. Growth of
perturbations can indicate sensitivity; it does not alone prove a chaotic regime
or excuse an unconverged numerical method.

The independent reference omits metabolic potassium feedback and the energy
ledger. Its direct numerical comparisons are therefore **only** with matched
production energy-omitted controls. This is an isolated diagnostic boundary,
not permission to remove metabolism from DNNE. Energy-enabled source refinement
is reported separately. The reference does not validate biological parameter
values, human physiology, synaptic learning, gap junctions or a whole brain.

[Numba](https://numba.readthedocs.io/en/stable/user/5minguide.html) compiles the
reference arithmetic without fast-math or persistent JIT caches. The isolated
environment uses NumPy 2.2.6, SciPy 1.16.3, Numba 0.61.2 and llvmlite 0.44.0.
Wheels, package licenses, runtime versions and the copied environment are retained
with the experiment. The Python base interpreter and system .NET installation
remain local runtime dependencies; version records are saved.

## Validation and interpretation

- The diagnostic default source stepper matches production state and spike
  counts with and without energy feedback.
- Four independent-reference derivative vectors match production. Compiled and
  ordinary Python arithmetic agree. AMPA and fast GABA match closed-form receptor
  responses, with a separate AMPA decay check and positive inhibitory calcium.
- Every replayed arrival must retain its exact time and identity. Ownership,
  no direct arrival-triggered calcium-weight decrement, missing/duplicate events,
  complete matrices, finite state and energy/calcium ledgers are checked.
- Default source controls reproduce the archived source output. Reference
  schedules and declared tolerances must match the frozen bundle and protocol.
- Deliberate corruption checks exercise arrival timing, missing climbing events,
  ownership, presynaptic cell identity, direct weight changes, energy, nonfinite
  reference values, reference schedules/scope/final state, missing cases, replay
  resolution, entry ablation, missing contact samples and bundle identity. All
  15 damaged captures are rejected. A read-only audit leaves captures unchanged.

The initial 76-condition short pilot passed its accounting checks. For one
zero-offset energy-omitted source, coupling refinement reduced maximum sampled
voltage difference from the 1e-9 reference from approximately 0.617 to 0.207 to
0.037 mV, while spike counts matched. This motivated retaining independent
coupling controls; it does not establish the full 60 s result. The final matrix
adds a stricter reference level and uses the larger diagnostic perturbation,
bringing the final preflight and study to 80 conditions.

The final 80-condition preflight passed: four archived baseline matches, 160,000
voltage/energy trace rows, 233,568 contact rows and 13,588 recorded events. The
reference arithmetic checks and the 15 deliberate-corruption checks also passed.
These establish the rig's short-run accounting, not the full study's outcome.

The final package is committed, source-frozen and backed up to a unique D:
directory. It inherits all protected older files and checks package/protected
hashes before and after stages. The visible independent PowerShell window has a
24-hour execution ceiling and no live monitoring. FINISHED means that the matrix
and accounting completed; source accuracy, reference convergence, sensitivity
and fixed-input Purkinje refinement require review afterwards. Biological
calcium-dose calibration and repeated teaching remain subsequent work.

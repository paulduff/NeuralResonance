# Rubral input recipient diagnosis and explicit wiring candidate

## Completed experiment

The operator reported `rubral-motor-20261117-844975` complete. The separate
`rubral-motor-review-20261117` verifies all 2,360 C:/D: files, frozen source,
executable manifests, all 136 focused cases, the managed runtime and the saved
analysis. All 48,000 source frames and twelve motor branches reconcile. No model
or test was rerun during review. The original failed output-lifecycle capture
`e42efc` remains unchanged.

The quiet condition emitted zero source events. Quarter, unit and four-quanta
fixtures emitted 261, 1,178 and 5,710 original rubral events, respectively. They
came from 155, 172 and 172 source cells and reached 107, 119 and 119 declared
outgoing target labels. **None was addressed to the selected motor target**,
`auto-SpinalCordMotor-002`. Its sole presynaptic cell, `n-081`, showed zero
synaptic current and zero emitted events in every condition. All twelve motor
branches consequently received zero input and emitted zero motor events. This
experiment did not test an evoked motor response or its learned strength.

## Setup error, not demonstrated structural plasticity

The follow-up `rubral-input-routing-review-20261117` checks the saved receiving
contact states against all three completed input fixtures. Each has 256 contacts
and 5,120 original input events, but only **172 actual recipient cells**. There
are 101 singly innervated, 58 doubly innervated and 13 triply innervated recipients
under this fixture; 84 cells receive none. All three drive strengths have exactly
the same contact/recipient mapping. No named target index matches the actual
recipient in this particular fixture. The event labelled for `n-081` is received
by `n-034`; no contact reaches `n-081`.

The experiment's assumption was wrong: these legacy target labels are routing
hints, not assured literal cell addresses. `StructureEngine.ResolveInboundNeuronIndex`
delegates this pathway to `BrainstemCranialCircuitKernel`. `ResolveSignalIndex`
prefers the source suffix; `ProjectChannel` applies the existing channel map and
stable jitter. Receiving contacts then belong to that resolved cell. Ordinary
synaptic strength learning can occur there, but the identical recipient mapping
does not demonstrate plastic rewiring. Increasing fixture strength cannot correct
which cell owns the synapse.

Both reviews are separately D:-verified. Rounded population observations are not
complete neuronal numerical trajectories; no upstream physiological conclusion
follows from this diagnosis.

## Simpler explicit connection rule

The operator requested simplification while staying close to biology. The next
isolated experiment gives each incoming synaptic contact one explicit receiving
cell. Strength plasticity retains its existing equations and works on that
contact. Structural rewiring would require an explicit anatomy change, not a
silent reinterpretation of an event's destination.

Reuse `IsolatedSynapticTargets`, already used by isolated cortical studies,
instead of adding a second remapping algorithm. Its constructor guard now permits
fresh, full-size isolated RedNucleus state as well. Public service construction
still supplies no map. Existing live routing and the default isolated rubral
mode remain unchanged. The map participates in the model/save fingerprint;
nonempty state directories are rejected and old saves are preserved.

The declared rubral owner checks every fixture event against the exact contact
map before creating neuronal state. Missing contacts or disagreement between a
named and declared target fail instead of falling back to the legacy mapping.
The receiving engine records original event bytes, receiving contact identity,
named and actual cell, scheduled arrival and actual application time after
receptor integration and before learning. Receipts remain in the immutable
source frame until acknowledgement; failed-step receipts remain available for
diagnosis. A repeated event or an unintended actual recipient faults the source.

This is explicit **fixture anatomy**, not a fitted human corticorubral arbor.
The generic existing rubral Izhikevich/STDP parameters, substep event timing and
their numerical/physiological qualifications remain open. The source's original
outgoing routes and the preselected motor cell stay unchanged. The receptor's
fixed diagnostic conductance is still not learned source efficacy.

## Next visible comparison

`rubral_motor_gate.py prepare-declared` freezes a distinct experiment. It runs
150 focused cases: the original 136, eight existing declared-contact cases and
six new rubral delivery/ownership cases. The four source conditions retain the
same original fixture identities, 1,500 ms horizon, 0.25/1/4-quanta stimuli and
twenty pulses. All four declare the same 256 contacts, including the silent
condition. The motor target, 50 nS receptor, delays, solver and packet/disabled
branches are unchanged.

The audit requires each of the 15,360 active fixture events to appear exactly
once in an actual delivery receipt, with its original bytes and literal intended
receiving cell. Final receiving contact states must agree. Each active condition
must cover all 256 declared cells. The complete quiet population observations
and energy diagnostics must match the preserved prior quiet capture exactly;
this is a saved-observation regression, not full-state numerical qualification.

Source/receiver accounting and actual input delivery are separate from the
`ObservedNeuronalMotorResponse` flag. If correctly delivered input still produces
no motor response, retain that result and investigate the neuronal/contact
mechanism. Do not move the target after observing activity, increase gains to
force a pass or fabricate output events. No live brain, Unity body, muscle,
sensory feedback, balance or recovery is qualified by this experiment.

Authoring builds and syntax/inventory checks execute no tests or neurons. Launch
only clean frozen pushed source with an initial verified D: backup in visible
PowerShell, and inspect output only after the operator reports completion.

## Launch record

Launched as `rubral-declared-20261117-3e3db0`, frozen at
`149e5ea778878dded50bb8d867292c9f5a367a6d`, visible task
`DNNE-Rubral-Declared-35778cc76b5f`. The initial 1,755 files plus manifest are
verified on D:. Test and probe authoring builds have zero warnings/errors;
Python/PowerShell syntax and the 150-case source inventory passed without
executing a test or model inline. This is the only pending operator report.
No post-launch output, status or process has been inspected. The vestibular
comparison remains an unlaunched draft, and no further spindle study has started.

## Completed declared-recipient review

The operator reported `3e3db0` complete. The extended saved-data reviewer verifies
all 2,384 C:/D: files, frozen Git source, both binary manifests, unchanged managed
runtime and all 150 focused cases. The saved analysis reproduces exactly, including
48,000 original source frames, twelve motor branches, all 15,360 actual fixture
deliveries to their declared cells, and preserved original quiet observations.
The separate `artifacts/rubral-declared-review-20261117/review.json` is D: verified.
No neuron, numerical integration or test executed during review.

The wiring correction works for this fixture. Selected cell n-081 now fires
five original events under unit drive and 23 under four-quanta drive. Every one
reaches the declared motor contact in both packet-cadence branches. Quarter
drive reaches the cell and depolarizes it but produces no selected event.

The motor cell is not electrically unresponsive. Its sampled soma voltage differs
from the disconnected control by up to 1.22035 mV and 2.86817 mV in the two active
conditions. The stimulated second dendritic compartment differs by up to 6.68124
and 10.09266 mV. Maximum sampled soma voltages after 1,000 ms are -66.35669 and
-64.70887 mV. The unchanged cell records zero crossings of its -20 mV observation
threshold. Receptor occupancy becomes nonzero; quiet and quarter-drive motor
trajectories remain equal to the disconnected control. The motor-output flag
therefore means no emitted motor spike, not no postsynaptic electrical effect.

This single 50 nS diagnostic contact produces a subthreshold response under the
specified source activity. That does not establish defective plasticity, calibrated
spinal physiology, or that every single afferent should trigger a motor spike.
The model's source study examines nonlinear responses to combinations of synaptic
drive; it does not prescribe this artificial rubral anatomy or contact strength
([Powers and Heckman, 2017](https://pubmed.ncbi.nlm.nih.gov/28053245/)).

The operator requested ten converging neuronal inputs next. Declare that anatomy
before activity, retain the existing n-081 single-input control and disconnected
control, and keep each contact at 50 nS. Original events must be generated with
their declared recipients; do not rewrite completed messages or duplicate one
source spike into ten independent neurons. This is a prospective isolated
connectivity experiment, not an approved live wiring or physiological calibration.

## Ten-input implementation

`IsolatedRubralProjection` declares cells n-081 through n-090 to spinal target
`auto-SpinalCordMotor-002` before source construction. This includes the original
n-081 contact and nine additional cells selected by index, without activity-based
selection. Other primary routes stay unchanged. Each firing cell generates its
original event with the declared target, projection and synapse. No completed
message is rewritten, copied into another neuron or assigned a synthetic time.
The immutable projection participates in the fresh source's save fingerprint;
public/live construction does not select it and old state cannot be loaded.

Each contact retains 50 nS maximum conductance, the same unit transmitter pulse,
0.125 ms axonal delay, and the existing `1 + sourceIndex % 4` dendritic placement.
This gives three contacts on dendrites 2/3 and two on dendrites 1/4. Receptor,
membrane, plasticity and upstream input parameters are unchanged. This indexed
anatomy and synchronized presynaptic fixture are diagnostic approximations.

The new `--rubral-ten-inputs` probe retains the four source conditions and adds
a single-contact branch alongside delivery groups 1/17 and disconnected control:
sixteen motor branches total. The audit checks complete original event identity,
all declared arrivals, original source observations/firing times against the
completed declared capture, and byte-identical single-contact trajectories against
that capture. The ten-input branches must agree across packet cadence; motor
spiking remains a separately reported outcome, never a mandatory forced pass.

Five new focused tests cover immutable bounded anatomy, fingerprints, preservation
of the original/other routes, actual neuronal event targets and preserved saves.
The 155-case source inventory, Python/PowerShell syntax, and Release test/probe
builds pass with zero compiler warnings/errors. No test or model has executed
inline. Freeze and D:-verify a clean pushed capture before its visible launch.

The ten-input experiment is launched as `rubral-ten-20261117-0419e8`, frozen at
`9621023d3cd2569fe5cf355b5091f4bfbb5baa30`, visible task
`DNNE-Rubral-Ten-c3f078c97378`. All 1,770 initial files plus manifest are verified
on D:. Preparation only checked metadata and copied previously reported evidence.
No post-launch output, status or process has been inspected. This is the only
pending operator report; the completed vestibular comparison is separately
reviewed. The 155 cases and neuronal response are not yet assessed for this run.

## Completed ten-input review

The operator reported `0419e8` complete. All 2,423 files on C: and D:, frozen
Git source, executed binaries, managed runtime and 155 exact test cases reconcile.
The saved audit reproduces exactly from 48,000 original frames and all 16 motor
branches. Source observations and original firing times match the earlier
declared-input experiment; its single-contact trajectories remain byte-identical.

| Source drive | Selected original inputs | Ten-contact motor spikes | Single-contact spikes | Disconnected spikes |
| --- | ---: | ---: | ---: | ---: |
| Quiet | 0 | 0 | 0 | 0 |
| Quarter | 0 | 0 | 0 | 0 |
| Unit | 50 | 1 | 0 | 0 |
| Four | 230 | 12 | 0 | 0 |

Each motor spike arrives at the isolated motor-unit receiver exactly once.
Both packet cadences produce identical trajectories and emissions. The unit
condition fires at 1223.7222994456993 ms; the stronger condition's twelve spikes
span 1031.018596925541 to 1469.660520732152 ms. Sampled soma peaks are
10.50777821282527 and 12.843047465952496 mV. The 50 nS individual contact strength,
membrane equations and crossing criterion were unchanged.

This is evidence of neuronal summation causing motor output in the declared
isolated circuit. The previous single contact was insufficient under these
conditions. It does not establish a plasticity defect, ten as an anatomical
optimum, physiological rubrospinal calibration or live brain authority. The
upstream source is still a reduced DNNE population under prescribed input;
moving muscles/body and sensory return were not executed. Next motor work should
carry these original emissions through the existing qualified muscle/body path,
preserving disconnected controls and assessing numerical accuracy separately.

The authoritative review is `artifacts/rubral-ten-review-20261117-v2/review.json`.
The first review's optional population summary was overwritten by its traversal
of the copied single-contact baseline. Its main analysis and motor counts were
correct; v2 restricts summaries to the current study and checks all ten named
sources. Both review artifacts are preserved. No neuron, model or test was rerun
during either saved-data review.

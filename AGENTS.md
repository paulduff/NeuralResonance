# Avatar and environment work

DNNE's biological mechanisms are a correctness requirement, not a naming or
presentation theme. Learning must be implemented at individual synaptic contacts
using the actual connected neurons' activity, biological timing, appropriate
cell/circuit-specific plasticity and locally arriving modulation. Preserve stable
cell/connection identity across processes and checkpoints. Do not describe a
pathway-shared weight, a sender-only postsynaptic proxy, service health or visible
movement as evidence of biologically equivalent Hebbian learning. Use mechanistic
tests and explicitly state unvalidated approximations. The current foundation
review is `docs/DNNE_Learning_And_Structure_Review_2026-10-19.md`.
Its implemented contact/event repair and remaining acceptance work are recorded in
`docs/DNNE_Biological_Foundation_Repair_2026-10-19.md`. Schema-4 saves are learned-state
warm starts, not exact brain continuation. Preserve incompatible legacy saves;
use explicit isolated state for fresh experiments, never a silent schema upgrade.
The subsequent cell/timing repair is documented in
`docs/DNNE_Cell_Dynamics_Repair_2026-10-30.md`. Its dynamics version changes the
model fingerprint; preserve earlier schema-4 saves too. HH is now a reference
dynamic-gate membrane, not a physiologically calibrated human cerebellar cell.
The completed comparison and biological pacemaker clock correction are recorded
in `docs/DNNE_Cell_Comparison_And_Circuit_Return_2026-10-30.md`; the clock version
also participates in the save fingerprint. The next 14/20-instance comparison
is isolated and does not qualify whole-brain balance or physical recovery.
Its completed results are in `docs/DNNE_Connected_Circuit_Results_2026-10-30.md`.
Reserve/fatigue and potassium clamps in the subsequent cell probe are artificial
diagnostic interventions only; they are not approved replacements for biology.
Their completed causal findings and energy/recovery repair requirements are in
`docs/DNNE_Support_State_Diagnosis_2026-10-30.md`. Native quiet firing collapses;
held energy restores sustained activity but does not establish recovery after
inhibition. Do not promote the diagnostic clamps or pulse into live control.
The substrate-limited energy and channel recovery candidate is documented in
`docs/DNNE_Energy_And_Recovery_Repair_2026-10-30.md`. Dynamics v3 invalidates older
model fingerprints without modifying their saves. Normalized energy, reduced
Avatar supply proxies and hybrid membrane parameters remain uncalibrated.
Missing/stale live body supply must remain visible; isolated probes use an explicit
reference bath. Channel ablations are diagnostic only. Do not confuse successful
accounting or recovered firing with a calibrated olive or physical balance.
The completed 120-condition findings are in
`docs/DNNE_Energy_Recovery_Results_2026-11-02.md`. Candidate cells sustain energy and
recover, but recovery survives removal of both added channel mechanisms at the
higher drive; the lower drive still loses Purkinje recovery. Do not attribute
success to HCN alone. The fast tonic olive remains uncalibrated; establish its
cell-specific physiology before interpreting connected LTD or physical recovery.
The v4 olive candidate is documented in
`docs/DNNE_Olivary_Calibration_2026-11-02.md`. It uses a three-compartment reference
with endogenous oscillations, per-contact receptor conductance and explicit
somatic/axonal spike accounting. Do not reinstate a tonic sine-wave drive or demand
spontaneous spiking from every isolated olive cell. Evoked responsiveness, slow
oscillations, timing, inhibition and supply recovery must be assessed separately.
Reference conductances and input-density conversion are not fitted human anatomy;
gap junctions and connected teaching remain unqualified. Preserve v3 saves too.
The completed 180-condition findings are in
`docs/DNNE_Olivary_Results_2026-11-02.md`. The reference olive oscillates near 8.2 Hz
without tonic spikes, responds to stronger input and restores its baseline waveform
after challenges. All outer-step traces match. CaT removal abolishes oscillation;
HCN removal does not. Restored oscillations are not proof of recovered evoked output:
test matched input before/during/after challenges before connected qualification.
The weak paired cases cannot rank output timing; correct their unequal endpoint
counts in a future matched comparison. Do not overwrite the completed package.
The next matched response rig is documented in
`docs/DNNE_Olivary_Response_Recovery_2026-11-02.md`: fixed production channels,
identical four-contact probes before/during/after challenges, matched sham,
rest-during-challenge controls, complete windows and preserved off-window spikes.
Assess recovered evoked output and inhibitory sensitivity before connecting the
cerebellar loop. These diagnostic probes are not a live restart mechanism.
The completed matched response findings are in
`docs/DNNE_Olivary_Response_Results_2026-11-02.md`. Late outgoing responses return
to 95–100% versus 95% in matched sham, with matching outer-step traces. Stronger
inhibition roughly halves burst output but does not monotonically reduce hit
probability. Brief delayed spikes must remain visible; no off-window activity
persists into late recovery. Next assess actual olive-to-Purkinje event delivery
and per-contact learning in an isolated circuit, preserving all axonal events.
The operator subsequently authorized the calcium learning change described in
`docs/DNNE_Purkinje_Calcium_Candidate_2026-11-02.md`. Purkinje cells now have an
explicit climbing AMPA compartment and contact-local voltage/IP3/calcium LTD.
Its reference-inspired parameters, passive electrical reduction, peak-induction
approximation and missing LTP remain unqualified. Do not call it a complete
Purkinje model or assume it is better because it contains calcium. The isolated
128-condition comparison uses matched legacy learning and entry/release ablations,
all real olive axonal events, fresh state, and no live monitoring. Old Purkinje
saves have incompatible fingerprints and must stay preserved. Other nuclei's
learning and the unresolved connected decoder issue are not repaired by this change.
The completed 128-condition review is recorded in
`docs/DNNE_Purkinje_Calcium_Results_2026-11-02.md`. The candidate's stored-weight
effect depends on calcium entry and gains contact contrast from internal release,
but is tiny and includes dormant-contact depression. Retaining only the maximum
historical calcium dose can obscure later teaching; simply waiting longer or
increasing a learning multiplier is not a demonstrated repair. The connected
host also has outer-step sensitivity upstream of learning. Next establish event
and voltage/calcium convergence, then calibrate local dose-to-LTD and repeated
teaching. The agreed sequence is Purkinje learning, fastigial/vestibular integration,
reliable balance readout, then Avatar recovery; this result does not yet qualify
advancing to learned fastigial/postural control. Preserve all completed evidence.
The subsequent timing repair is documented in
`docs/DNNE_Connected_Timing_Repair_2026-11-02.md`. Arrival boundaries no longer
act backward over elapsed intervals; calcium uses start/end voltage. Integration
version `event-aligned-calcium-v1` invalidates older StructureEngine fingerprints,
without modifying their saves. The isolated 120-condition numerical study varies
host packet cadence independently of solver resolution, retaining fractional
arrival times and all olive events. Exact packet invariance is not a convergence
proof. Review successive refinements before fitting learning strength or replacing
the maximum-history induction approximation. Physiology and balance remain open.
The completed timing findings are in
`docs/DNNE_Connected_Timing_Results_2026-11-02.md`: all 60 packet-cadence pairs
match exactly, but finer solvers change olive burst counts substantially and
non-monotonically. The active LTD target is established early, so similar final
weights conceal later source divergence. Do not claim source convergence from
packet invariance, short regressions or similar learning totals. Next isolate
olive numerical accuracy against an independent error-controlled reference and
test Purkinje refinement with one fixed recorded olive train. Distinguish solver
error from nonlinear trajectory sensitivity before fitting biological parameters.
The source/reference and fixed-input replay rig is documented in
`docs/DNNE_Split_Numerical_Qualification_2026-11-02.md`. It leaves production
runtime and biological parameters unchanged. Its 80-condition study separates
source coupling/membrane refinement, an independent Python/DOP853 reference,
initial-state sensitivity and Purkinje replay of exact saved olive events.
The independent reference omits metabolic feedback: compare it directly only
with matched energy-omitted production controls, never as a validation of the
energy-enabled brain. Reference tolerances and diagnostic perturbations need
their own review. Preserved spike identity/timing and complete accounting are
necessary checks, not physiological or long-trajectory convergence claims.
The completed split-study review is in
`docs/DNNE_Split_Numerical_Results_2026-11-06.md`. Fixed-input calcium metrics
improve with refinement, but receiving spike timing remains unresolved. The
independent olive reference agrees closely under inhibition yet diverges later
under paired stimulation; do not declare its strictest long trajectory exact
or infer chaos. Global arrival boundaries split every receiving cell's step,
providing a numerical route for unrelated-event effects. Test no-op boundaries
and complete-state short-interval accuracy before tuning learning. Purkinje
still has provisional imposed drives; endogenous pacemaking is unqualified.
The next causal diagnostic is documented in
`docs/DNNE_Boundary_Timing_Study_2026-11-06.md`: connected/disconnected input and
disconnected input retaining no-op arrival boundaries. Its fresh isolated-only
hook releases no transmitter and is not a production fix. The eight-second
preflight supports the shared-boundary mechanism and reproduces 16 archived
baselines; review the full 60-second result before advancing. The full test suite
has one cortical-consolidation failure also reproduced with the pre-change binary;
do not call the whole suite green or conceal that separate unresolved issue.

Before adding or changing Avatar functionality or its Unity adapter, inspect the
corresponding existing `src/NRE.SimAvatar` implementation, receptor path and tests.
The operator explicitly requires this: **always check Avatar; substantial work
has already gone into it.** Use `docs/Unity_Avatar_Migration_Audit_2026-10-12.md` as
the migration map, and update it when preserving, replacing or omitting a feature.

DNNE is the brain and action authority. Avatar is the body/nervous system; World
and Maze are environments. Dyad/Entity provide interaction. CIV is a separate
experiment. Diagnostic commands/controllers must remain explicitly isolated from
live DNNE operation and must never be reported as neuronal or learned behavior.

The continuing neuronal-development objective and acceptance gates are recorded in
`docs/DNNE_Neuronal_Development_2026-11-06.md`. The operator authorizes justified
missing populations and mechanism repairs, with regular commits and pushes, until
Avatar usability is demonstrated or the operator stops the work. This is not
permission to claim human-brain equivalence or promote unqualified candidates.
The isolated Golgi channel implementation is described in
`docs/DNNE_Golgi_Channels_2026-11-06.md`. It is not wired to ModelNeuron or a host.
Its voltage-clamp/SK2 checks do not establish free-cell firing, a functioning
granular-layer circuit, or human physiology. Review the operator-reported results
before advancing to coupled membrane/cable dynamics; preserve the pinned source
convention and explicitly documented corrections.
The completed channel results are in
`docs/DNNE_Golgi_Channel_Results_2026-11-06.md`: 16 focused tests and 36 clamp
conditions passed, with independent SK2 propagation agreement. This permits
candidate coupled-membrane work, not live Golgi integration. The source-level
motor path audit is `docs/DNNE_Motor_Authority_Path_Audit_2026-11-06.md`; both the
population-derived output and joint error controller require neuronal replacements.
Do not equate ownership/freshness gates or generated pseudo-spikes with preserved
motor-neuron events. The completed boundary-timing result is recorded in
`docs/DNNE_Boundary_Timing_Results_2026-11-06.md`: all eight untouched-cell
contrasts exactly match when only the removed timing boundaries are restored.
Unrelated arrivals can change electrical/support states and even a disconnected
cell's spike count. The isolated repair candidate is described in
`docs/DNNE_Cell_Local_Timing_2026-11-06.md`; it advances each cell at its own real
arrivals and the regular grid. Its tests/comparison are awaiting operator-reported
completion. Its first run stopped before comparison: all 15 timing unit cases
passed, but a solution-manifest failure and incorrect expected counts exposed
a packaging mistake. The replacement builds under frozen `source/artifacts`
so source-reading tests cannot climb into the working repository. Its failed,
operator-closed capture is retained in `artifacts/cell-local-closed-review-20261106`
and on D:. The earlier cortical-consolidation case passed on this occasion;
that alone does not resolve its earlier failure. It is not enabled in service hosts and does not qualify general
recurrent scheduling, arbitrary host endpoints, solver accuracy or physiology.

Tests and experiments run independently in a visible PowerShell window. Do not live
monitor them; inspect saved results after the operator reports completion. Builds
and static inspection can run during development. Live
worlds start paused and wait for the operator to press Play. Preserve existing
brain state and verified D: backups. Commit and push applied changes once stable.

The next isolated Golgi candidate is described in
`docs/DNNE_Golgi_Coupled_Cell_2026-11-06.md`: free soma/cable voltage, separate
HVA/LVA calcium pools and somatic crossing events, without imposed pacemaking.
Its first-order split solver is compared with independent passive and fully
coupled active BDF references. No host selects it; a passive axon is not qualified
active propagation, and an unlimited reference bath is not metabolic calibration.
Review its operator-reported results before granule-layer integration. The
declared analytic/singularity/HCN choices differ from native deposited tables;
solver agreement is not native NEURON reproduction or physiological equivalence.

The isolated motor-event boundary is documented in
`docs/DNNE_Motor_Axon_Transport_2026-11-06.md`. It preserves emitted message/cell/
projection/source-synapse identity and firing time through declared motor-unit
contacts. Per-source sequence and completeness gates prevent treating missing
traffic as silence; the body clock cannot outrun the least-complete source.
No live host publishes this lane and no muscle consumes it yet. Unit fixtures
are not neuronal-source or contraction qualification. Retain the current path
until the actual source, event-driven muscle response and connected spinal
replacement qualify together; keep its population-derived status visible.
The bounded transport rig also needs durable replay/backpressure and explicit
epoch/checkpoint handling before cluster deployment. Do not silently reset an
epoch, discard events or use wall-clock receipt time as biological firing time.

The first coupled Golgi result is recorded in
`docs/DNNE_Golgi_Coupled_Results_2026-11-06.md`: 25 unit cases, clamps, passive cable
and accounting passed. All candidate/reference spike counts agree, but four of
six reference voltage contrasts exceed the original 0.05 mV bound. Candidate
spike timing also remains step-dependent. Review the tighter BDF/cross-method
Radau comparison before changing physiology or promoting the cell; keep the
original limits. The first motor-transport window was interrupted during build,
before tests. Its RUNNING status was stale; both original captures and D: copies
are preserved. The replacement window retries transport tests then refines the
Golgi reference, with no live monitoring. A process footer is not test success.

The next isolated motor-unit consumer is documented in
`docs/DNNE_Motor_Unit_Calcium_2026-11-06.md`. Identified arrivals drive a slow-unit
calcium/binding/activation model at fixed optimal length, using pinned Nagamori
parameters. It is not connected to a live neuron or Avatar/Unity muscle plant.
Its focused tests and DOP853/Radau comparison are now launched in the independent
`motor-calcium-20261106-7cde4b` PowerShell window; await the operator's report. Do not
double-apply the old muscle's activation/fatigue or import a rate-based spike
generator as neuronal authority. Endplate transmission is assumed; muscle APs,
metabolic supply, dynamic length/tendon behavior and physical recovery remain
unqualified. Keep the current windows frozen and wait for reported completion.

The operator-reported replacement timing result is recorded in
`docs/DNNE_Cell_Local_Timing_Results_2026-11-06.md`. Both stages reproduce all 24
original cases; all 15 timing tests and the isolated cell/contact-locality checks
pass. The full suite is 1,074/1,075, retaining the known cortical-consolidation
failure. Finer steps still change spike counts/timing, and dormant contacts still
depress. This is evidence for the isolated scheduling repair, not convergence,
selective learning, general recurrent scheduling or live adoption. The new review
rehashes the completed capture and D: copy without modifying their files.

The reported motor-calcium result is in
`docs/DNNE_Motor_Unit_Calcium_Results_2026-11-06.md`: all 46 transport/calcium/
manifest cases and all 21 numerical conditions pass, with verified C:/D: copies.
This qualifies the isolated calculation, not a neuronal source, physiological
calibration or a dynamic body plant. The next source candidate is described in
`docs/DNNE_Motoneuron_Reference_2026-11-06.md`: an isolated five-compartment
Powers/Heckman reference using pinned cat MG parameters. The first verification
covers channels and passive coupling only; active firing is still unassessed.
It is not selected by ModelNeuron or live spinal/Avatar/Unity services. Preserve
its explicit fixed reversals, local calcium-pool convention, passive endpoint
topology and unqualified metabolic/reference-bath assumptions. No invented spike
generator or motor policy may substitute for the subsequent source qualification.
The operator-reported channel/passive outcome is reviewed in
`docs/DNNE_Motoneuron_Passive_Results_2026-11-06.md`: all 10 focused/manifest tests,
96 clamps and nine passive traces passed. Its post-check backup failed at a
260-character Windows path; extended-path I/O repaired and verified the full C:/D:
capture while preserving the original FAILED status and frozen source. No test
was rerun. Active-cell comparison is now the next gate, not live motor adoption.
The active rig is documented in
`docs/DNNE_Motoneuron_Active_Comparison_2026-11-06.md`: ten fixed six-second inputs
at three RK4 steps, independently integrated with ordinary/tight DOP853 and Radau.
No membrane parameter changes or live deployment accompany it. Require reference
agreement, complete-state accounting, exercised action potentials and refinement;
then assess physiology separately. Do not substitute quiet-trace agreement for
active verification or retune a failed acceptance bound after seeing the result.
The isolated axon-to-unit connector is documented in
`docs/DNNE_Isometric_Motor_Unit_Plant_2026-11-06.md`. Its immutable snapshots and
per-unit grid preserve actual arrivals without frame/packet or unrelated-unit
solver interference. Fifteen new fixture checks join 46 existing component and
manifest cases in a separate visible window. No outcome is claimed before the
operator's report. Do not confuse this fixed-length reference with a live muscle,
add a second activation filter, or connect an unqualified source to body authority.

The operator-reported isometric result is reviewed in
`docs/DNNE_Isometric_Motor_Unit_Results_2026-11-06.md`: all 61 focused/component/
manifest cases passed and both complete captures were verified. Its fixture force
is not neuronal-source or whole-body qualification. The operator subsequently
reported SciPy Jacobian-overflow warnings in the still-running active motor-cell
study, not that isometric capture. A bounded derivative candidate is documented
in the active-comparison report; its numerical checks have not yet run. Do not
change, monitor or restart the original frozen experiment on this evidence alone.

The separate cortical failure diagnosis is described in
`docs/DNNE_Cortical_Memory_Diagnosis_2026-11-06.md`. It records actual per-contact
updates and compares the original gapped fixture with continuous/spaced controls.
The original assertion and learning parameters are unchanged. Diagnostic capture
completion must not be reported as a passing full suite or qualified biological
consolidation. Review the operator-reported capture before choosing a repair.
The completed result is in `docs/DNNE_Cortical_Memory_Results_2026-11-06.md`:
the original assertion still fails, but the five-case capture and C:/D: review
complete. Cue cell 166 receives no local ACh/DA; the intended modulators all reach
cell 100 after cortical remapping. Correct recipient identity before interpreting
modulation-dependent learning. The derived ensemble score is not a molecular
consolidation measure and can rise while the cue weight falls. Do not solve this
by boosting gain, broadcasting modulation globally or forcing monotonic scores.
The isolated explicit-recipient candidate and seven-condition follow-up are in
`docs/DNNE_Cortical_Recipient_Comparison_2026-11-06.md`. Exact declared contacts
bypass generic population remapping only in fresh isolated cortex, with map-specific
save fingerprints. It requires original five-control reproduction and measured
local ACh/DA at the cue versus neighbouring-cell exclusion; it does not require
potentiation or change the original assertion. No service enables this map.
Actual receptor/arbor calibration, long-term consolidation and deployment remain
open even if the recipient comparison passes.

The reported recipient result is in
`docs/DNNE_Cortical_Recipient_Results_2026-11-06.md`: all ten selected tests pass,
five controls reproduce, and local modulation reaches the cue only when declared
there. The original assertion passed this invocation but remains intermittent;
different postsynaptic firing prevents attributing all weight improvement to
direct modulatory capture. No live promotion or biological consolidation claim.
The active motor-cell result is in
`docs/DNNE_Motoneuron_Active_Results_2026-11-06.md`: all spike counts agree, but
the original numerical gate fails on calcium refinement, finest charge accounting
and one reference gate contrast. Preserve its failed status and original bounds;
test accumulation/reference accuracy before changing physiology.
The isolated next candidate is documented in
`docs/DNNE_Motoneuron_Numerical_Repair_2026-11-06.md`: explicit compensated RK4
accumulation, analytic passive charge controls and tighter adaptive references
with the bounded Jacobian. Old naive callers and failed captures are preserved;
inputs and acceptance bounds stay fixed. Its independent visible window must
pass twelve focused/manifest tests before active comparisons. Do not monitor it
or claim numerical/physiological qualification before operator-reported completion.
The independent cortical follow-up is documented in
`docs/DNNE_Cortical_Contact_Replay_2026-11-06.md`: replay the reviewed recipient
capture with fixed spike/activity histories and selective modulation interventions.
Require exact original contact-state reproduction before interpreting direct
effects. Its cadence sweep measures the current rule's dead-zone/float behavior;
diagnostic completion is not numerical or biological qualification. It does not
change learning, the original assertion, live brain/Avatar/editor or old saves.
Await its visible PowerShell outcome without live monitoring.
The replay outcome is in `docs/DNNE_Cortical_Contact_Results_2026-11-06.md`:
all recorded states reproduce, direct capture is small under fixed spikes, and
fine-step weak learning disappears through float rounding and the per-update
cutoff. Do not interpret that as biological insensitivity or increase gain.
The isolated double-state candidate is described in
`docs/DNNE_Cortical_Contact_Numerics_2026-11-06.md`. It preserves the old complete
baseline and requires closed-form accuracy, sign and refinement at fixed bounds.
It is not wired into live cells, persistence, Avatar or the editor. Its limiter
and receptor reductions remain unqualified even if the numerical comparison passes.
The operator-reported precision result is in
`docs/DNNE_Cortical_Contact_Numerics_Results_2026-11-06.md`: 27/27 numerical cases
pass, sixteen fixed-history replays complete and the old baseline reproduces.
The full C:/D: capture is verified. Actual-cell integration and precision across
float storage/transmission remain open. At the operator's request, work pauses
after committing/pushing this review; see `docs/DNNE_Continuation_2026-11-06.md`.
Do not start or monitor further work during that pause.

The operator resumed work on 9 November and changed the active scope to DNNE,
Avatar and World. Park LM/Entity and Dyad development, preserving their code and
saves; language interaction is no longer a completion prerequisite for this scope.
The editor remains the observation interface. See
`docs/DNNE_Compute_Direction_2026-11-09.md` for the inspected RTX 5060 platform and
the proposed measured CPU/CUDA execution work. GPU execution must retain neuronal
identity, biological timing, contact-local learning and required precision.
Performance improvement does not qualify physiology. Preserve the outstanding
experiment windows and await operator-reported outcomes without live monitoring.

The first CPU cost study is documented in
`docs/DNNE_Compute_Baseline_2026-11-09.md`. Its internal timing collector is limited
to fresh isolated runs; no host enables it. Six current-model workloads and the
existing isometric motor plant measure costs with paired evidence checks. Generated
message UUIDs are excluded from neural cross-run equality; connected identities,
times and payloads remain compared. It measures neither live network transport,
Unity physics nor CUDA speedup and does not qualify physiology. Review the saved
operator-reported outcome before choosing a GPU port from its timings.

The reported CPU cost run failed only after completing all 42 neural runs and
53 focused tests; its verified partial evidence and serializer repair are in
`docs/DNNE_Compute_Failure_Review_2026-11-09.md`. All eighteen neural observation
pairs match. Motor-plant evidence encoding encountered a queue comparator's native
delegate pointer. The retry preserves comparator method/closure identity, requires
three additional regressions (56 total), and reproduces all earlier neural hashes.
Do not claim the motor study or entire performance study passed from this partial
result. Preserve the failed capture and review on C: and D:.
The separately prepared `docs/DNNE_Cuda_Interop_2026-11-09.md` records a pinned
NVRTC dependency and standalone driver/float64 probe. It builds but has not run;
no GPU backend is selected by the brain, Avatar or Unity. Test it only through
the visible PowerShell workflow with preserved source/dependencies and outcomes.

The replacement CPU study is reviewed in `docs/DNNE_Compute_Results_2026-11-09.md`:
56 focused tests, all 27 paired comparisons and all 42 previous neural hashes pass,
with the full C:/D: capture verified. Olive/Purkinje cell calculations dominate
these sparse-contact fixtures; this does not establish dense-network costs or
whole-brain throughput. The standalone CUDA interoperability package now has a
frozen visible PowerShell launcher and independent saved-byte audit. Review its
operator-reported result before building on the native execution route. No model
or live host uses it; numerical and biological acceptance gates remain separate.

The isolated CPU membrane-batch candidate is documented in
`docs/DNNE_Olive_CPU_Batch_2026-11-09.md`. Its original olive runtime reference is
unchanged. Explicit instance/cell identity, per-cell clocks and transactional
staging protect reordered/parallel work. Eleven focused/manifest cases and six
complete binary-trace comparisons are prepared but not yet run. Hold this window
until the operator reports the CUDA interoperability retry outcome; do not monitor the
existing window. No native GPU route, receptor/metabolic coupling, live scheduler,
physiology or Avatar recovery is qualified by the build or layout implementation.

The first CUDA outcome is reviewed in `docs/DNNE_Cuda_Load_Repair_2026-11-09.md`:
the device was identified, but NVRTC failed to open its present, verified helper
DLL; no arithmetic case ran. The explicit absolute helper preload and saved
loaded-module verification are isolated repairs. Protocol v3 retains all six
arithmetic criteria; await the fresh visible PowerShell retry without monitoring.

The operator has now reported the CUDA retry and both older cell studies. Their
review is `docs/DNNE_Compute_And_Cell_Results_2026-11-09.md`: CUDA's six saved-byte
cases pass; the motor-cell experiment retains FAILED with eight of ten scenario
gates passing (all finest absolute checks pass, three tiny calcium-refinement
contrasts fail); the Golgi run is stopped/incomplete with eleven of twelve solves.
Its stale RUNNING file is preserved, and the complete partial capture is sealed
inside the new review with a verified D: backup. Do not infer missing-reference success.

`docs/DNNE_Motor_Replay_Buffer_2026-11-09.md` records the isolated bounded source
queue and matching receiver/plant acceptance receipts. These are in-memory,
trusted protocol components, not durable delivery or neuronal-source proof.
The next fresh olive CPU batch package expands its selected checks from eleven
to 87, covering the new interface and existing transport/calcium/plant cases.
No live brain/Avatar/Unity lane is enabled. Review its reported outcome before
porting the membrane calculation to CUDA, retaining numerical/biological gates.

The separate missing Golgi reference solve is packaged by
`docs/DNNE_Golgi_Reference_Continuation_2026-11-09.md`. Reuse the eleven verified
completed arrays and original reference modules; compute only no-SK/Radau with
the original settings. Require exact reproduction of the ten recorded reference
and thirty candidate contrasts before solving, then audit all twelve reference
and 36 candidate contrasts. Never infer candidate solver accuracy, physiology or
Avatar recovery from a passing reference. Await each independent PowerShell
window's operator-reported completion without inspecting its live results.

The completed Golgi continuation is reviewed in
`docs/DNNE_Golgi_Reference_Results_2026-11-09.md`: twelve reference contrasts pass
their original bounds and 36 candidate contrasts reproduce, with verified full
C:/D: captures. Candidate spike counts match, but finest timing errors still reach
1.93 ms and no-SK refinement remains non-monotonic. Repair the coupled numerical
integrator before physiological retuning or live adoption.

`docs/DNNE_Motor_Fixed_State_Precision_2026-11-09.md` prepares a separate read-only
RHS/arithmetic diagnosis of the reported motor failure. Preserve its source and
all original bounds. CSV states are not full compensated-integrator checkpoints;
do not infer accumulated-roundoff causation or a repaired trajectory from local
decimal comparisons. No production host selects the diagnostic entry point.

`docs/DNNE_Golgi_Coupled_Solver_2026-11-09.md` describes the isolated coupled
SDIRK2 candidate and its 32-case component window. It retains the original split
cell, biological definitions and fixed physical inputs. Formal second order and
stage convergence do not establish six-second spike timing or physiology. Review
component results before running the full saved-reference/refinement comparison;
include nonlinear-tolerance sensitivity and preserve all failed cases. No live
host selects the candidate, and failed stages must not reset, clip or invent events.

The full coupled comparison is prepared in
`docs/DNNE_Golgi_Coupled_Comparison_2026-11-09.md`: 30 conditions, full physical
traces, existing active BDF/Radau references, saved passive references and
prospective absolute/refinement/nonlinear-sensitivity gates. It has not run.
Require the operator-reported component outcome and its verified 32-case review
before freezing/launching it. The full window repeats those cases, preserves
all partial failures and runs without live monitoring. Its declared small-error
refinement floor does not alter any previous failed result or acceptance bound.

The operator has reported all three pending windows. Their verified review is
`docs/DNNE_Batch_And_Component_Results_2026-11-09.md`: 87 olive/transport/body
tests and twelve complete CPU batch contrasts pass exactly; 32 Golgi component
tests pass. CUDA membrane development and the full Golgi comparison may proceed.
The motor fixed-state diagnosis reproduces 396 states with identical C#/Python
calcium derivatives, but does not repair or causally explain the original failed
refinement gate. Preserve that failure and its bounds. All original captures and
reviews have verified D: copies; none qualifies live neuronal/body function.

The full Golgi comparison is now launched as `golgi-sdirk-20261109-440bb8`,
revision `fb6c6e1028748d0b965d544ac9858cc0f269db25`, task
`DNNE-Golgi-Sdirk-620aa678d3f8`, with verified initial D: backup. Await the
operator's completion report without inspecting its live results. The three
preceding windows are reviewed; they are no longer pending. CUDA membrane work
can proceed independently against the passing CPU batch reference.

The first resident CUDA olive candidate is implemented in
`docs/DNNE_Olive_CUDA_2026-11-09.md`. It retains the qualified CPU equations,
double state, explicit cell clocks/identities and whole-batch numerical commit.
The prepared comparison reuses every original CPU frame with prospective state
bounds and exact per-frame crossing counts/clocks, plus ownership/failure guards.
Run only in the visible frozen PowerShell workflow and await operator-reported
completion. C# builds do not establish successful CUDA compilation or execution.
No host selects it; measure end-to-end speed only after its numerical outcome,
and keep receptor/metabolic integration and physiological qualification separate.

The CUDA membrane comparison is launched as `olive-cuda-20261109-cd1fee`,
revision `f8de323a5ef989219d10be8906f9f46aa13cbdbf`, task
`DNNE-Olive-Cuda-b2e2befffce3`, with verified initial D: backup. Its result and
the full Golgi result are pending operator-reported completion. Neither window
may be monitored live. No CUDA speedup or live backend is qualified by launch.

The operator subsequently reported the olive CUDA window complete. Its verified
review is `docs/DNNE_Olive_CUDA_Results_2026-11-09.md`: eleven focused tests, six
interop cases, six GPU control groups and all six complete membrane comparisons
pass, with 461,250 cell-frame records and verified C:/D: captures. Counts/clocks
agree exactly; this establishes the tested arithmetic port, not speedup or
biological/Avatar qualification. The full Golgi window remains pending its own
completion report. Avoid competing studies during CPU/GPU timing. The cortical
receptor/storage precision integration remains outstanding and has not been edited.

The isolated timing protocol is `docs/DNNE_Olive_CUDA_Timing_2026-11-09.md`:
six populations, two solver steps, CPU worker limits 1/8/16 and CUDA, with 288
trials including 48 warmups. It links unchanged reviewed arithmetic and compares
all saved reports/final states to the old CPU bytes. Time the complete batch API,
including transfers and synchronization; do not claim whole-brain speedup. Wait
for the operator to report the other studies finished before launching, then
use the visible frozen PowerShell window without monitoring. No live promotion
or biological qualification follows from this preparation.

Timing package `olive-cuda-timing-20261109-eb7469` is prepared at revision
`c4f80f22c6cd16b94f188cc558cba65002d1e07e`, with 1,431 initial files verified
on D: plus manifest. Build/syntax checks pass; no timing test has launched.
An asynchronous question asks whether `golgi-sdirk-20261109-440bb8` has finished.
Keep the package held until that report; do not infer completion from elapsed time
or inspect Golgi live. After a finished report, review that evidence and launch
the frozen timing runner with `-OtherStudiesFinished`, without live monitoring.

The operator has now reported the full Golgi comparison finished with FAILED.
Its verified review is `docs/DNNE_Golgi_Coupled_Results_2026-11-09.md`: all thirty
conditions, 32 focused tests and physical/accounting checks complete, but five
firing protocols fail absolute state/timing bounds. Counts, refinement and Newton
sensitivity pass. Timing errors shrink roughly fourfold per timestep halving;
do not retune physiology or loosen criteria. Preserve the failed capture. Improve
integration order/error control and assess event interpolation before live use.
The earlier timing hold is now satisfied by the operator's report; launch the
already frozen olive timing package and await completion without monitoring.

The timing package `olive-cuda-timing-20261109-eb7469` is now launched, task
`DNNE-Olive-Timing-ef9e3d7627f9`, using its unchanged frozen c4f80f2 revision.
Await operator-reported completion; do not inspect live outputs. Avoid builds,
new model runs or heavy offline audits that would compete with its CPU timing.
Source inspection and coding can continue independently. The Golgi capture is
reviewed and no longer pending; the timing outcome is the current pending window.

The operator has now reported the timing window complete. Its verified review is
`docs/DNNE_Olive_CUDA_Timing_Results_2026-11-09.md`: eleven focused tests and all
288 accuracy-checked trials complete, with full C:/D: verification. CUDA is slower
at the current 192-cell size; it is 1.31–1.36x faster than the best measured CPU
batch only at 3,072/12,288 cells with the finer step. Keep CPU production defaults;
do not enlarge biological populations to force a GPU win. Profile costs before
optimization and keep precision/bounds. The timing hold is over; builds and the
next visible independent study may proceed. Whole-brain/Avatar speed is unmeasured.

The cortical integration candidate is described in
`docs/DNNE_Cortical_Precision_Integration_2026-11-09.md`: authoritative double
contact state through actual recurrent cortical cells, receiver-local efficacy,
and explicit isolated schema-5 learned warm starts. No live host selects it and
schema 4 remains unchanged. Do not truncate precise state through legacy mirrors,
suppress recurrence, or infer all firings from tick-end IsActive. The frozen
68-test/seven-control/seven-scenario PowerShell rig requires verified baseline
copies and preserves failed evidence. Await operator completion without monitoring.
This repairs numerical integration only; reduced learning/receptor calibration,
the original aggregate assertion, full-suite status and Avatar recovery remain open.

The cortical precision window is now launched as
`cortical-precision-20261109-67008c`, frozen revision
`4fefd12c27c027fb86b048d37b25481f9fc00d1b`, task
`DNNE-Cortical-Precision-f3c50e797d62`, with 1,450 initial files verified on D:.
Await the operator's completion report; do not inspect live logs or status.
The olive timing and Golgi comparison are reviewed and no longer pending.

The operator has now reported the first cortical precision window failed. Its
verified C:/D: capture contains 67 passing tests and one fixture-timing failure;
the two longer studies never started. The test inspected at 1 ms before its
Entorhinal tract's minimum 3 ms delay. It now verifies absence before arrival and
exact event/contact membership through the existing 24 ms upper delivery bound.
The runner also uses the determinism class's actual global-namespace identity.
Model code and numerical criteria are unchanged for the retry; preserve the first
failure. See the outcome section in the cortical integration document. Golgi's
higher-order solver was being investigated but has not been implemented or run.

The corrected cortical window is launched as `cortical-precision-20261109-bae439`,
revision `5e2c3a55c021b25c5745190ebce4dc0e88f9db25`, task
`DNNE-Cortical-Precision-5738a87a440c`, with 1,451 initial files verified on D:.
It is the only pending window. Await the operator's completion report without
inspecting live results; the failed first attempt has been reviewed and preserved.

The operator has now reported the corrected cortical window complete. Its verified
review is `docs/DNNE_Cortical_Precision_Results_2026-11-09.md`: 68 tests, seven
legacy controls, 2,613 precise learning events, 840 receptor deliveries and six
learned warm starts pass the declared checks. Twenty-six modulation updates survive
below float resolution. Only two recurrent precise contacts are exercised by each
new fixture; do not claim dense whole-cortex qualification. All C:/D: bytes and
binaries verify. No window is pending. Live schema 4, biochemical calibration,
the full unit suite and Avatar recovery remain open. Golgi order/timing repair is
the next numerical task; its original failed bounds remain unchanged.

The explicit third-order Golgi candidate is described in
`docs/DNNE_Golgi_Third_Order_2026-11-09.md`. The common coupled stage solve and
biological equations are unchanged; all three stages contribute to independent
ledgers. Second order remains the default control. Do not clip negative stage
right-hand sides, bypass physical-domain failures, loosen the old full-comparison
bounds, or infer event-interpolation order from state order. The 43-case visible
component window must be reported and reviewed before a long comparison. Short
self-refinement and coefficient order conditions are not physiological or Avatar
qualification. No live host selects this candidate.

The third-order component window is launched as
`golgi-sdirk3-components-20261109-8660b2`, revision
`70c2e48c914dd55d5737a0f9420f71a0d44646a9`, task
`DNNE-Golgi-Sdirk3-Components-e4fe068fba0d`, with 1,420 initial files verified on D:.
It is the only pending window. The cortical integration outcome is reviewed;
await the new component report without inspecting live logs or status.

The full third-order rig is coded in
`docs/DNNE_Golgi_Third_Order_Comparison_2026-11-09.md`: 38 fixed conditions, one
additional finer step, separate reporting at the old finest resolution, unchanged
absolute/refinement/accounting gates and reused verified passive references.
The shared analyzer reproduces the old failed 30-case result exactly. Builds and
syntax checks pass; no new simulation ran inline. Do not freeze or launch until
the operator reports the 43-case component window and its C:/D:/TRX review passes.
The preparer requires the same coupled solver source that passed those components.

The operator has reported the third-order component window complete. Its verified
review is `artifacts/golgi-sdirk3-components-review-20261109/review.json`, with
43 passing tests, sixteen saved refinement rows and all 1,811 C:/D: files verified.
Every short-refinement error is below its numerical floor, so these checks do not
establish observed cubic convergence or sustained firing accuracy. The component
review also has a verified D: copy. No window is now pending; the clean-source
preparer may freeze the 38-condition full comparison with unchanged acceptance
limits. Launch it visibly in PowerShell and await operator-reported completion.

The full third-order comparison is launched as `golgi-sdirk3-20261109-210c3d`,
revision `faac10d04c87d48155e33946edf6a6cdb1ded763`, task
`DNNE-Golgi-Sdirk3-c7ed345c12dc`, with 1,524 initial files plus manifest verified
on D:. This is the only pending window. Do not inspect live output; await the
operator's completion report. Independent CUDA profiling code can be prepared,
but timing must wait until the operator reports this CPU study complete.

The CUDA cost rig is coded in `docs/DNNE_Olive_CUDA_Cost_2026-11-09.md`: separate
uninstrumented, host-timed and host/device-event variants, six cases and five
rotated rounds (90 measured trajectories plus 90 warmups). Optional partial hooks
vanish from other probes; removing their marked lines reproduces the reviewed
execution source. Numerical equations/kernel remain unchanged. Preserve exact
cross-variant reports/state as well as the earlier CPU tolerances. Device event
time includes scheduling and must not be added to host phases or called pure
arithmetic time. Builds/source/syntax checks pass, but no model test or benchmark
has run. Prepare from clean pushed source, back up on D:, and hold launch until
the operator reports the pending Golgi comparison complete. Do not monitor it.

The cost package is prepared as `olive-cuda-cost-20261109-06f78b`, frozen revision
`f8561578320143a30f0fd0a95c9c914d72e711b0`, with 1,441 initial files plus manifest
verified on D:. It has not launched. The Golgi window remains the only pending
run; await its operator report before reviewing it and launching the frozen cost
runner with `-OtherStudiesFinished`. Keep numerical/heavy work out of that timing
window. The active brain/body/world goal remains incomplete.

The next motor diagnosis is `docs/DNNE_Motor_Clock_Diagnosis_2026-11-09.md`.
The original harness integrates requested dt, accumulates a rounded absolute clock,
then shortens later dt from that clock. The supplied durations need not sum exactly
to sampled timestamps. A six-case clock-only C#/Python reconstruction records exact
binary duration totals and compares an endpoint-difference scheduling candidate.
No neuron is integrated and no runtime/harness behavior is changed. Build/syntax
checks pass; this has not established the cause of the calcium failure. Run visibly
without monitoring, preserve original numerical bounds, and require a later causal
trajectory comparison before claiming repair. CUDA cost timing must wait for every
additional study to be reported complete as well as the pending Golgi comparison.

The clock-only diagnosis is launched as `motor-clock-20261109-6aaa98`, revision
`167ddd6203ec362b46e713a6b1f6efcbfa145349`, task `DNNE-Motor-Clock-a56a8dac458d`,
with 1,434 initial files plus manifest verified on D:. There are now two pending
windows: this clock diagnosis and `golgi-sdirk3-20261109-210c3d`. Do not inspect
either live output. The frozen `olive-cuda-cost-20261109-06f78b` remains held until
both completion reports arrive. Neither launch qualifies a neuronal/body repair.

The operator has now reported the motor-clock window complete. The verified
`artifacts/motor-clock-review-20261109/review.json` covers all 1,565 C:/D: files
and recomputes saved-row analysis. The original schedule's maximum discrepancy
reaches 2.0177e-7 ms (about 0.2 ns); endpoint differences conserve represented
elapsed time exactly at every sample. This confirms the scheduling discrepancy,
not its causal contribution to calcium errors or Avatar behaviour. Only Golgi
remains pending; CUDA cost timing is still held for that report. Prepare a causal
neuronal comparison with unchanged equations, controls and original bounds.

The causal motor scheduling rig is coded in
`docs/DNNE_Motor_Boundary_Comparison_2026-11-09.md`: thirty freshly rerun original
controls must reproduce the old state bytes/results, followed by thirty conditions
using the qualified endpoint-difference helper. The cell/channel equations and
all original bounds remain unchanged; exact elapsed duration is checked during
actual cell integration. The analyzer reproduces all old scenario analyses exactly
from saved arrays, including failures. Build/syntax checks pass, not runtime tests.
Freeze only with the verified clock review. Hold launch until the prepared CUDA
cost window has been reported finished, then use `-CostStudyFinished`. Golgi is
still the only pending window; do not inspect its live output.

The motor scheduling comparison is prepared as `motor-boundary-20261109-a79744`,
revision `2465fdb8026768f2348e7bda53a4375ff203eb28`, with 1,480 initial files plus
manifest verified on D:. It has not launched. The execution order is: await and
review Golgi's reported completion; run the existing frozen CUDA cost package
without competing work; after that completion report, launch the motor comparison
with `-CostStudyFinished`. Clock diagnosis is reviewed and no longer pending.

The next isolated source/body integration is coded in
`docs/DNNE_Somatic_Motor_Unit_Circuit_2026-11-09.md`. Eight conditions connect
actual five-compartment somatic crossings to the existing motor transport and
isometric plant, retaining the entire qualified source trace, original identities,
fractional times, explicit completeness and pending tail. Packet/retry controls,
quiet/cut controls, muscle refinement and three independent calcium references
have prospective gates. This cat-MG/human-TA pairing is a numerical interface
experiment, not a matched physiological motor unit; no active axon, NMJ or live
Avatar authority is qualified. The 88-test PowerShell rig is coded but has not
run or been frozen. Only a passing, operator-reported and C:/D:-verified motor
boundary comparison permits its preparer. Preserve the current Golgi → CUDA cost
→ motor boundary execution order. Do not inspect pending runs or start this
candidate merely because its build passes. Existing Avatar/Unity/editor behavior
and saved brains are unchanged.

The sensory-side source audit is
`docs/DNNE_Sensory_Boundary_Audit_2026-11-09.md`. Current Unity frames carry wall
time; transducer derivatives use capture intervals, while generated inputs take
the coordinator's current neural timestamp. `AddPopulation` creates messages on
each body frame, not from afferent membrane crossings. Muscle-specific spindle
channels rectify length deviation/velocity; duplicate/order resets and omitted
InputSource in event identities also need repair. Do not mistake these findings
for measured causation of falls or discard Avatar's existing sensors. The next
isolated sensory boundary must preserve physical samples, explicit body/neural
epochs, completeness and exact retries before a qualified mechanistic afferent
source can close the spinal loop. No live behavior or pending run changes here.

The isolated physical-sample candidate is documented in
`docs/DNNE_Physical_Sample_Transport_2026-11-09.md`. It preserves signed original
body observations on a declared integer physics grid, maps physical/neural epochs
explicitly and retains immutable packets until exact acceptance receipts arrive.
Completeness covers captured samples only, never unobserved events or afferent
silence. Bounded queues require the producer to hold physics before losing a
sample; no live producer, receptor neuron, network endpoint or durable restart
selects this lane yet. Existing Avatar/Unity/editor behavior and saves remain
unchanged. The 23-case focused rig runs visibly without monitoring; await the
operator's report before claiming transport checks pass. It may overlap the
pending Golgi accuracy study, but both completion reports must precede CUDA cost
timing. Preserve the later motor-boundary and source/unit prerequisites.

The physical-sample window is launched as `physical-sample-20261109-44584f`,
revision `0cff80e63bf0fea24626c1fc1f3a84e46a7188d4`, task
`DNNE-Physical-Sample-6d9a4329c8a6`, with 1,453 initial files plus manifest
verified on D:. There are now two pending windows: this 23-case transport rig
and `golgi-sdirk3-20261109-210c3d`. Do not inspect their live status or logs.
The frozen CUDA cost package `olive-cuda-cost-20261109-06f78b` must wait for both
operator reports; the motor scheduling package `motor-boundary-20261109-a79744`
still follows the reported completion of that timing run. No new transport,
afferent physiology or Avatar recovery result is claimed by launch.

The operator has now reported both windows complete. Physical-sample transport
passed all 23 cases; `artifacts/physical-sample-review-20261109/review.json`
verifies all 1,844 C:/D: files and executed binaries. No neuronal model ran.
Golgi SDIRK3 completed 38 conditions and 43 tests but still fails voltage/gate/
calcium accuracy in all five firing protocols. Its 2,114-file C:/D: review is
`artifacts/golgi-sdirk3-review-20261109/review.json`; saved analysis reproduces
exactly with unchanged bounds. Set numerical thread variables before importing
SciPy/NumPy in offline reviews. Finest voltage errors are 0.0938–0.2537 mV
against 0.05 mV; last-halving reductions near eightfold support one further
halving, not a qualification or relaxed limit. Both windows are no longer pending.
Finish preserving these reviews, then launch the existing frozen CUDA cost
package with `-OtherStudiesFinished`; keep builds/heavy work out of its timing.
The prepared motor scheduling comparison still follows that operator report.

The pinned spindle-source review is `docs/DNNE_Spindle_Source_Review_2026-11-09.md`.
Original author code at `da0ed89078a948167b4e2b511480787ddb681892` and its license
are retained unmodified in `artifacts/spindle-source-20261109`, with verified D:
backup. Static inspection found forward-difference timing, cached detached-pool
and passive-stress/slack-root issues, and a threshold/reset example rather than
the paper's conductance-based passive membrane. No model was executed or adopted.
Do not silently repair the reference or feed extrafusal Avatar force directly
as intrafusal stress/current. Establish reproducible mechanics and a sourced
afferent membrane, then qualify their causal connection to physical samples.

Both completed-study reviews and the spindle source snapshot now have verified
D: backups, and source/documentation are committed and pushed. The frozen CUDA
cost package `olive-cuda-cost-20261109-06f78b` is launched with
`-OtherStudiesFinished`, task `DNNE-Olive-Cost-101087822647`, source revision
`f8561578320143a30f0fd0a95c9c914d72e711b0`. The launcher reverified its initial
1,441 files plus manifest on C:/D:. This is the only pending window. Await the
operator report without inspecting live output or starting builds, numerical
studies or heavy audits. The motor scheduling package remains held until that
report. Golgi's finer-step follow-up is justified but has not launched.

The operator reported the CUDA cost attempt failed. The verified
`artifacts/olive-cost-startup-review-20261109/review.json` covers 2,201 C:/D: files,
11 passing tests and six passing CUDA controls in each of three variants. Zero
timed trials ran: the launcher rejected its schedule because Windows PowerShell
5.1 nested the deserialized JSON array inside `@(...)`. Assigning it directly
and checking all 90 entries before build/CUDA setup repairs that source defect;
the fresh window must record the old/new shape check. No cost result is qualified.
Preserve the failure, back up this review and retry a new frozen package before
the prepared motor scheduling comparison. The old timing window is no longer
pending. Do not let a retry overlap other studies or builds.

The finer Golgi rig in `docs/DNNE_Golgi_Finer_Comparison_2026-11-09.md` is coded
but not frozen or launched. Fourteen new cases at 0.0009765625 ms retain all
38 verified old cases and unchanged bounds; the old failed analysis must first
reproduce exactly from the copied input subset. Mechanism sources and the
integration/sampling body match the completed comparison. Build and syntax
checks pass, not runtime tests or analysis regression. Its preparation/hashing,
tests and simulations must wait until the corrected CUDA timing window is
operator-reported complete. Preserve the motor scheduling gate and old captures.

The corrected CUDA cost retry is now launched as `olive-cuda-cost-20261109-fedff9`,
revision `0d94dd2cd7d0b402451e03c508ef253cacd93c26`, task
`DNNE-Olive-Cost-e5d41eb4882c`, with 1,475 initial files plus manifest verified
on D: and rechecked before launch. This is the only pending window. Await its
operator-reported completion without live monitoring or competing builds/heavy
work. Neither schedule-regression success nor timing results are established
yet. Motor scheduling remains prepared/held; finer Golgi remains coded/unfrozen.

The operator has now reported the corrected CUDA cost window complete. Its review
is `docs/DNNE_Olive_CUDA_Cost_Results_2026-11-09.md`: 11 tests, 18 CUDA control
groups and all 180 trajectories pass; saved analysis reproduces exactly and all
3,319 C:/D: files verify. Profiling median differences stay below 1%. GPU execution/
synchronization dominates; packing/report processing matters in large batches,
while transfer calls are a small share. This does not qualify whole-brain speed
or physiology. Keep CPU defaults. Back up the review and launch the already frozen
motor-boundary package with `-CostStudyFinished`; no CUDA study remains pending.
The finer Golgi rig remains unlaunched and the somatic motor-unit circuit still
requires a passing, reported motor-boundary outcome. Preserve original failures.

The CUDA cost review now has a verified D: backup. The frozen motor-boundary
comparison `motor-boundary-20261109-a79744` is launched at unchanged revision
`2465fdb8026768f2348e7bda53a4375ff203eb28`, task
`DNNE-Motor-Boundary-c3550f7fed43`, after rechecking its 1,480 initial C:/D:
files plus manifest. It is the only pending window. Await operator-reported
completion without live monitoring. No numerical, neuronal-source or body
qualification is inferred from launch; finer Golgi remains unlaunched.

The isolated host-workspace candidate is described in
`docs/DNNE_Olive_CUDA_Host_Workspace_2026-11-09.md`. It reuses private scratch and
precomputes ordinal identity ranks while retaining dynamic end-time order, fresh
returned reports, arbitrary subsets, all validation and transactional state.
The original CUDA implementation/kernel/driver remain unchanged. Both variants
build; runtime checks are pending. Its accuracy-only PowerShell rig has 11
focused tests, twelve control groups per variant and six complete recorded-byte
comparisons per variant. It can overlap motor accuracy work, but no speed claim
or later timing run is allowed until both operator reports and the accuracy
review. No live neuronal/body path selects this candidate.

The CUDA host accuracy comparison is launched as `olive-cuda-host-20261109-aeb5f2`,
revision `91c53eab1ae51f9270a732e051eb0cc30782d352`, task
`DNNE-Olive-Host-336733e41879`, with 1,495 initial files plus manifest verified
on D:. Await its operator report without monitoring. The operator has separately
reported motor-boundary completion; inspect only that reported capture and keep
the host accuracy window untouched. No performance result is inferred from launch.

The completed motor scheduling review is
`docs/DNNE_Motor_Boundary_Results_2026-11-09.md`. All 12 tests and all ten
endpoint-scheduled scenario gates pass at unchanged bounds. Thirty original
controls reproduce exactly, including the two failed calcium-refinement gates.
All 2,057 C:/D: files and executed binaries verify, and saved analysis reproduces
exactly. The separate review has a verified D: backup. This permits freezing and
running the already coded somatic motor-unit circuit with that exact source;
it does not qualify physiology, active axons or live Avatar authority. The CUDA
host accuracy window remains pending; do not inspect it without its own report.

The somatic motor-unit circuit is now launched as
`motor-unit-circuit-20261109-83e7ee`, revision
`e12fff09af30f078e229ed2f56c71b4af93886ec`, task
`DNNE-Motor-Unit-Circuit-89e8edcebbda`, with 1,483 initial files plus manifest
verified on D:. It uses the unchanged passing motor source and reviewed Avatar
transport/calcium/plant components. Two windows await operator reports: this
circuit (88 tests/eight conditions/three muscle references) and CUDA host accuracy
`olive-cuda-host-20261109-aeb5f2`. Neither is monitored live; neither measures
speed. Motor boundary and CUDA cost are reviewed and no longer pending. Finer
Golgi remains unlaunched. Do not infer physiology or Avatar recovery from launch.

Both operator reports have now arrived and both captures pass review. See
`docs/DNNE_Somatic_Motor_Unit_Results_2026-11-09.md` and
`docs/DNNE_Olive_CUDA_Host_Results_2026-11-09.md`. Motor: 88 focused tests,
eight conditions, 28 actual somatic emissions delivered, exact packet/retry
invariance, zero force after disconnection, unchanged numerical gates passed.
CUDA host: 11 focused tests, 12 control groups per variant, six complete workloads
exact against reference and recorded evidence. Both full C:/D: captures and
executed binaries verify; both saved analyses reproduce exactly, with separate
verified D: review copies. Neither window remains pending. This is isolated motor
integration and host numerical agreement, not matched human physiology, live
authority, speedup or recovery. Finer Golgi is the next prepared window; host
timing must await its operator report, without competing experiments or builds.

On 13 November the operator resumed after the 9 November pause. The already
prepared finer Golgi package `golgi-sdirk3-fine-20261109-4b5c0f`, frozen revision
`69bc40f1593ec5cab71883be705cdbe2a8cfbec4`, is now launched as independent visible
PowerShell task `DNNE-Golgi-Sdirk3-Fine-b5e096c0065f`. Its 3,089 initial files plus
manifest were verified on D: before the pause and rechecked by the launcher.
This is the only pending window: 43 focused tests, exact old-analysis preflight,
14 new fine cases and 38 retained traces. Await the operator report; do not read
live outputs. CUDA host timing remains unlaunched and must wait for this run.
See the launch section of `docs/DNNE_Golgi_Finer_Comparison_2026-11-09.md`.

The operator reported that finer Golgi window failed at environment preflight.
The full failed C:/D: capture verifies: 3,481 files, all 43 focused tests passed,
zero new cases. Exactly three base Python binary hashes changed, with unchanged
versions and all other 2,433 dependency files. See
`docs/DNNE_Golgi_Runtime_Transition_2026-11-13.md`. Do not overwrite the failed
package or silently waive its guard. The retry requires a verified environment
review, permits only the explicitly recorded three-binary transition, and must
reproduce the entire old analysis exactly before finer simulations. Numerical
bounds and solver equations remain unchanged. The failed window is no longer
pending; the corrected window has not yet launched. CUDA timing stays held.

The corrected finer Golgi window is now launched as
`golgi-sdirk3-fine-20261113-ca813f`, revision
`ad68cc7a3c5e1f854d1c0bfe489602a9caad37f4`, task
`DNNE-Golgi-Sdirk3-Fine-2db991578748`. Its 3,094 initial files plus manifest were
verified on D: and rechecked before launch. This is the only pending experiment;
await the operator report without reading live output. The nine transition
controls and exact old-analysis replay are gates, not results inferred from
launch. CUDA timing remains held. Current handoff, prior reviewed outcomes and
the preserved but unfinished spindle-source work are recorded in
`docs/DNNE_Continuation_2026-11-13.md`.

The matched host timing rig is coded in
`docs/DNNE_Olive_Host_Timing_2026-11-13.md`. It uses the unchanged reviewed
reference/candidate batch implementations, six workloads and six paired rounds
(72 processes, 144 trajectories), exact recorded bytes and all 24 control groups.
Both variants build with zero warnings/errors; syntax/source checks pass. It has
not run and is not frozen. The only pending experiment remains Golgi retry
`golgi-sdirk3-fine-20261113-ca813f`; do not inspect its outputs before the operator
report. Host timing preparation/launch requires that report and review first,
then exclusive timing without competing heavy work. A valid timing result may
show no benefit; neither successful execution nor reduced allocations prove speed.

The completed spindle mechanics source review is
`docs/DNNE_Spindle_Mechanics_Review_2026-11-13.md`. All 1,066 extracted files in
four pinned archives verify without upstream code execution. The 2026 author
tutorial fixes particular older detached-state/slack-root issues, but its
separate population representations, slack flags, overlap boundaries, time
labels and offline filtering require explicit reproduction. Its threshold/reset
generator and receptor-potential fitting do not qualify a conductance afferent.
Use it only as the next isolated mechanics reference candidate; do not wire it
to Avatar or replace signed observations with invented events. Preserve the
2023 downloads and their failed source assumptions. No extra study was launched;
Golgi retry `golgi-sdirk3-fine-20261113-ca813f` remains the sole pending window,
with host timing held until the operator report and review.
The new tutorial source capture and separate source review have verified D:
backups (65 and one files respectively, each plus manifest).

The isolated step/capture owner in
`docs/DNNE_Physical_Sample_Producer_2026-11-13.md` now builds. It captures an
initial state, checks bounded capacity before synchronous physics, requires
exact completed counters/durations and captures every endpoint. A failed capture
after movement faults the epoch; no skipped or later substituted sample. It
preserves existing Avatar observations, starts paused and has no live caller.
Sixteen new producer tests plus 22 transport and one manifest case are prepared
for an independent PowerShell window. They are fixtures/recorded observations,
not Unity physics, afferent or recovery qualification. If launched, await both
this report and the still-pending Golgi report before CUDA host timing.

The producer window is now launched as `physical-producer-20261113-efe4af`,
revision `af9faf305e9c18215b5d3c3670e63b240467eb93`, task
`DNNE-Physical-Producer-50ce8b4cc5d5`, after verifying 1,494 initial files plus
manifest on D: and rechecking them at launch. Two windows now await operator
reports: this 39-case producer/transport check and finer Golgi retry
`golgi-sdirk3-fine-20261113-ca813f`. Do not inspect their live outputs. CUDA
host timing stays unlaunched until both reports and evidence reviews are complete.

The physical producer window is now reported complete and reviewed: all 39
checks pass (16 producer, 22 transport, one manifest), with all 1,885 C:/D:
files, executed binaries and declared test identities verified. Git source
comparison records only exact LF-to-CRLF archive conversion; frozen byte hashes
stay strict. The review has its own verified D: backup. See the result section
of `docs/DNNE_Physical_Sample_Producer_2026-11-13.md`. This qualifies the tested
ownership/transport behavior, not live Unity, sensory physiology or recovery.
The operator explicitly confirms Golgi retry `ca813f` is still running; the
re-pasted `4b5c0f` failure is the old package. Golgi alone remains pending.
Leave its outputs untouched and continue holding CUDA timing until its report.

The actual Unity physical-sample adapter is documented in
`docs/DNNE_Unity_Physical_Samples_2026-11-13.md`. It reuses the unchanged reviewed
producer/outbox/receiver against a dedicated isolated Unity child. Scoped step
ownership prevents competing Unity advances; every request/reply is preserved.
The study requires 201 delivered body observations, 201 actual advances with
the last capture deliberately failed, completeness only through step 200,
backpressure/paused-body holds and actual finite motion/contact evidence. Raw
per-link contacts are preserved but not delivered by the body-only lane. The
declared neural epoch is isolated, not a live handshake. .NET builds succeed;
Unity compile/runtime checks remain prospective in an independent PowerShell
window. No live integration, afferent or recovery qualification is inferred.
If launched, await its report/review as well as Golgi before CUDA host timing.

The actual Unity sample study is now launched as
`unity-physical-sample-20261113-1ff02e`, frozen revision
`b41dc3afc69ca1a7e0ca7afda48e8492392b561e`, task
`DNNE-Unity-Samples-7a2ebffbef3c`. Its 1,506 initial files plus manifest have a
verified D: copy, rechecked before launch. Two reports now remain pending: this
39-test/native-Unity physical study and corrected finer Golgi `ca813f`, which
the operator explicitly said was still running. Do not inspect either window's
live output. Starting the task establishes no build, physical or test result.
Wait for both reports and evidence reviews before CUDA host timing; no live
brain/body service was deployed or changed by this launch.

The combined body/contact candidate is documented in
`docs/DNNE_Body_Contact_Observations_2026-11-13.md`. It declares immutable contact
sites/source identities/encoding with the physical clock and requires a complete
body plus all contact sites at every endpoint, including unloaded sites. Missing
contact after movement faults the producer without advancing completeness; valid
older samples remain drainable. The existing body-only protocol is unchanged.
Unity's explicit combined adapter uses one retained reply and does not infer its
site list from received frames. Existing saturation/contact aggregation and
unloaded metadata are preserved and remain unqualified. Both .NET builds pass;
the 65-check rig (26 new, 38 body regressions, one manifest) is prepared but has
not run. If launched, await this operator report as well as Golgi and native
Unity before CUDA timing. No live neuronal/Avatar/editor path is promoted.

The combined body/contact checks are now launched as
`physical-observation-20261113-8c18b7`, frozen revision
`f263abf83675c53f26a804648794adf8bcffc767`, task
`DNNE-Physical-Observations-026be2d85734`, after verifying 1,512 initial files
plus manifest on D: and rechecking both copies. Three operator reports now await
review: corrected Golgi `ca813f`, actual Unity body sampling `1ff02e` and these
65 combined/regression checks `8c18b7`. Do not inspect live outputs. Await every
report and review before CUDA timing. The new window runs no Unity or neuronal
model; no result or physiological qualification is inferred from launch.

The operator has now reported Unity `1ff02e` and combined checks `8c18b7`, and
both captures are reviewed in `artifacts/unity-contact-review-20261113` with its
own verified D: copy. All 2,827 Unity and 1,903 contact capture files per copy,
Git source, executed binaries and test identities verify. Unity's 39 checks and
201 actual steps pass, delivering samples 0–200 exactly and stopping after the
deliberate capture failure at 201. Saved analysis reproduces exactly offline.
The combined suite has 41 passes/24 failures; all failures share the computed
getter-only `NeuralSubstrateSupply.IsValid` required-JSON defect. All 39 old body
regressions/manifest checks passed. Neither completed window remains pending.
See the result sections of `docs/DNNE_Unity_Physical_Samples_2026-11-13.md` and
`docs/DNNE_Body_Contact_Observations_2026-11-13.md`.

The narrow combined-protocol repair excludes only that derived validity flag,
requiring every actual measurement and rejecting forged flags. Two added supply
cases bring its prospective retry to 67 tests. Both .NET builds pass; no tests
run inline. Preserve the failed capture and launch a new frozen PowerShell
package. Corrected Golgi `ca813f` remains unreported and uninspected. CUDA host
timing stays held until every report and review. The spindle Octave harness and
portable runtime are prepared but unexecuted; its freezer/launcher/analysis
remain unfinished. No new afferent source or live neuronal path is qualified.

The corrected combined-observation retry is now launched as
`physical-observation-20261113-014b85`, revision
`8a273337542e83433ee92be53a6fb07fee00d233`, task
`DNNE-Physical-Observations-4fc27636ac75`. Its 1,514 initial files plus manifest
are verified on D: and rechecked before launch. It requires 67 tests and no
models. Await its operator report without live monitoring. Prepared package
`205752` was rejected before task creation by a stale count guard, ran no tests,
and is preserved with its setup rejection/D: backup. Exactly two reports now
remain outstanding: this contact retry `014b85` and corrected Golgi `ca813f`.
The Unity body study passed review; the old contact failure is fully reviewed.
All pending reports and reviews still gate CUDA host timing.

The operator has now reported both corrected Golgi `ca813f` and contact retry
`014b85`; the re-pasted `8c18b7` failure is old. Both current captures pass full
review. See `docs/DNNE_Golgi_Finer_Results_2026-11-13.md` and the contact result
section. Golgi: 43 tests, nine environment controls, fourteen new cases, eight
protocol assessments, unchanged bounds and exact saved-analysis reproduction.
All 3,638 C:/D: files per copy verify. Contacts: 67 tests and all 1,905 files per
copy verify. Frozen Git sources, binaries and exact case identities match both.
The three-file separate review has a verified D: backup. Neither is pending.

Proceed with exclusive CUDA host timing after freezing clean pushed source and
verifying its initial backup. No competing experiments, builds or heavy hashing
after launch until its operator report. The spindle native-reference rig is
coded and Python/PowerShell syntax checked, but remains unfrozen/unlaunched;
Octave compatibility and numerical results are untested. Its scope is in
`docs/DNNE_Spindle_Native_Reference_2026-11-13.md`. Combined actual Unity delivery,
afferent physiology, connected Golgi function and recovery remain unqualified.

The exclusive CUDA host timing study is now launched as
`olive-host-timing-20261113-ab15ac`, revision
`8a3c63c1c8cc1f4c4a5337fbb5b56001c1f87417`, task
`DNNE-Olive-Host-Timing-e6d1b659eff2`. Its 1,547 initial files plus manifest are
verified on D: and rechecked at launch. This is the only outstanding report.
No live monitoring, competing experiments, builds or heavy evidence hashing
until the operator reports completion. Its scope is 11 tests, 24 control groups,
72 processes and 144 trajectories comparing original/candidate CUDA host work;
no speed result is inferred from launch. Spindle and combined Unity studies
remain unlaunched. Preserve all old results and await the timing report.

The operator reported CUDA host timing `ab15ac` complete. Its full review passes:
all 3,059 C:/D: files per copy, frozen Git source, binaries and eleven test
identities verify; all 144 trajectories and 24 controls reproduce exact bytes;
saved analysis reproduces exactly. Larger workloads save 18.71–27.87% time and
meet the declared rule; small workloads save 2.43–2.66% and miss its 5% threshold.
See `docs/DNNE_Olive_Host_Timing_Results_2026-11-13.md`. The separate review has a
verified D: backup. No report remains pending at this point and timing no longer
blocks other work. CPU stays the live default; this is not a new CPU/GPU crossover.

During timing, source-only inspection found Unity omits eight shared finger/grip
fields, which otherwise become defaults. The candidate in
`docs/DNNE_Body_Observation_Profile_2026-11-13.md` adds explicit unavailable and
reference fields to immutable layout identity and versions combined transport
as v2. Old combined v1 packets reject; old captures/body-only v1/saves stay intact.
Both .NET builds pass after the completion report; the new 70-case test window
has not run. Prepare it and the already coded native spindle study in independent
PowerShell windows. Native complete body/contact integration, afferent physiology,
live neuronal authority and Avatar recovery remain unqualified.

Two new independent visible PowerShell windows are now launched, both at
revision `d3f148e5e281f3354b4c391f0f2b160768807abb`:
`physical-observation-20261113-64ad44` (70 cases, task
`DNNE-Physical-Observations-50120d406ffc`, 1,523 initial files plus manifest
verified on D:) and `spindle-reference-20261113-9a5090` (30 native trajectories
plus two boundary records, task `DNNE-Spindle-Reference-cebf3230ec3d`, 1,590 initial
files plus manifest verified on D:). Preparation also verified every one of the
59,532 Octave runtime files and pinned C:/D: archives without model execution.
Await both reports without live monitoring. No result/native compatibility is
inferred from launch. CUDA timing is reviewed and no longer pending. Complete
body/contact actual Unity integration still needs its own physical study.

The actual combined Unity study is now coded in
`docs/DNNE_Unity_Body_Contacts_2026-11-13.md`. It selects the complete v2 producer,
strictly validates native body omissions before shared defaults, and requires
201 delivered bodies with all 3,015 site observations. Its explicit post-step-201
adapter intervention preserves the full native reply, drops only the head contact
in a separate copy, and must stop completeness at 200. Four native-schema checks
join the original 70 in its PowerShell gate. Both .NET builds pass; no test or
native result is inferred. Existing body-only evidence and live services/saves
are unchanged. Earlier profile `64ad44` and spindle `9a5090` still await clear
operator reports; a reused FAILED footer without a folder did not identify a new
failure. Do not inspect those pending outputs. The combined rig can repeat its
own frozen prerequisites before native execution, without live monitoring.

The operator reported native spindle `9a5090` and combined Unity `162523` failed.
Both are fully reviewed in `artifacts/spindle-unity-failure-review-20261113` with
verified D: backup. Spindle: all 32 cases fail at the first ode23 callback because
Octave forwards [] as an extra derivative parameter; no integration step completed.
Unity: 73/74 tests pass, including all four native schema cases; one stale fixture
expects current protocol v2 to reject. No Unity build/physics ran. Preserve both
failed captures. Their docs record the full hashes/source/binary verification.
The fixture now rejects current+1, legacy v1 and zero. The spindle retry preserves
original source and declares a separately hashed two-call [] -> odeset() adapter,
with analytic/default-call controls before fibers. Neither repair establishes a
passing retry. Prepare fresh independent PowerShell packages without monitoring.
Older body-profile `64ad44` remains unreported and uninspected; these reported
failures are no longer pending. Live services/saves and biological parameters
remain unchanged.

Two fresh retries are now launched at `c0ce3e9e7a42a25d19821819b0a47e7933cc2e1d`:
Unity body/contact `unity-body-contact-20261113-7800c5`, task
`DNNE-Unity-Body-Contacts-d3394556f5c3`, 1,531 initial files plus manifest on D:;
Octave-adapted spindle `spindle-reference-20261113-98c085`, task
`DNNE-Spindle-Reference-25be52c4f713`, 1,615 initial files plus manifest on D:.
Both backups were rechecked at launch. Preparation verified all 59,532 runtime
files and the separately declared compatibility copy, with no model execution.
Await both reports without monitoring. Old profile `64ad44` still lacks an
unambiguous completion report and stays uninspected; its original fixture coverage
is also included in the new 74-case native Unity gate. The reported `9a5090` and
`162523` failures and their separate full review are preserved and no longer pending.

The operator reported corrected Unity `7800c5` complete. Its full review passes:
74 exact test identities, 201 bodies and all 3,015 contacts match original native
replies; the explicit missing-head intervention stops completeness at 200 while
the original step-201 reply remains complete. All 2,855 C:/D: files per copy,
frozen Git source, compiled Unity sources/player and executed binaries verify.
Saved analysis reproduces exactly without model execution; the separate review
has a verified D: copy. See `docs/DNNE_Unity_Body_Contacts_2026-11-13.md`.
The re-pasted `162523` FAILED footer is old, not a new retry failure. Unity is
no longer pending. Spindle retry `98c085` and older profile window `64ad44` still
lack their own reports and remain uninspected. The latter's scope is covered by
the passing native gate, but its own outcome must not be inferred.

The source-only motor follow-up is
`docs/DNNE_Motor_Mechanics_Source_2026-11-13.md`. Existing Avatar/Unity muscle,
calcium/unit and generator sources were inspected first. The pinned reference
and exact parameters have a separate verified C:/D: capture. Dynamic extension
needs length-dependent calcium/activation, velocity-dependent yield and explicit
fiber/tendon coupling; do not just scale the fixed-length unit's force or apply
legacy activation/fatigue twice. Native source holds tendon length across its
mechanical RK stages, so original-ordering reproduction and fully coupled
numerical qualification must be distinguished. No dynamic implementation or
physiological result is claimed yet. Preserve old isometric evidence and live
mechanisms; implement and qualify an isolated candidate before Unity migration.

The isolated coupled candidate is now implemented in
`docs/DNNE_Dynamic_Muscle_Candidate_2026-11-13.md`. Slow-unit calcium, yield and
fiber/tendon motion share RK stages; explicit arrivals retain identity and time.
Prescribed total-length intervals and a fixed-fiber clamp are diagnostics only,
not live body interpolation or neuronal control. Original isometric/transport
classes remain unchanged. The component/test assembly builds with zero warnings
or errors; Python/PowerShell syntax checks pass. Its 93-case visible-window rig
is coded but not yet launched. Do not infer a passing numerical or physical
result from build success. Require operator-reported component review, then
independent adaptive traces, source-ordering/refinement checks and causal source/
body integration before Unity promotion. A kinetic-work ledger is not metabolic
calibration; first-row TA parameters are not anatomy for every muscle. Existing
live paths/editor/saves and pending spindle/profile captures remain untouched.

The dynamic muscle component window is now launched as
`dynamic-muscle-20261113-f9970f`, frozen revision
`23d6a0618968d10c92cf1afaaefa7cb19354e41d`, task
`DNNE-Dynamic-Muscle-7976293860f3`. All 1,540 initial files plus manifest have a
verified D: copy, rechecked at launch. Its 93 tests await the operator report;
do not inspect live outputs or infer success from launch. Three reports are
outstanding: this new window, spindle retry `98c085` and old profile `64ad44`.
Independent source/reference rig development can proceed, but its longer muscle
comparison must wait for reported and reviewed component evidence. No live
brain/body/Unity/editor path or old save changed.

The operator has now reported dynamic `f9970f`, then all remaining windows
complete. All three captures pass full evidence review in
`docs/DNNE_Muscle_And_Spindle_Results_2026-11-13.md`: muscle 93/93; older profile
69/70 with the already-repaired current-version fixture; spindle thirty finite
trajectories/two boundaries and exact reproduction of twenty descriptive
comparisons. All C:/D: files, Git source and relevant binaries verify, and the
separate three-file review has a verified D: copy. No prior window is pending.
Spindle stretch leaves reported cached populations inconsistent with authoritative
`y`; force and ODE use `y`, so do not attribute an ODE error merely to that cache.
Exact-zero overlap retains history and the kinetics divide by overlap; assigning
zero alone is not a biological repair. Preserve the native-adapted baseline.

The subsequent muscle rig is coded in
`docs/DNNE_Dynamic_Muscle_Comparison_2026-11-13.md`: unchanged candidate, 33 traces,
33 adaptive references, fifteen explicitly adapted source-ordering controls and
replay of all 28 preserved actual somatic arrivals. The C# probe builds; Python/
PowerShell syntax checks precede freezing. Component review permits its preparer,
not a numerical accuracy claim. Require clean pushed source and verified initial
D: backup, launch independently in visible PowerShell, then await the operator
without monitoring. Independent reference bounds and legacy-control accuracy
must not be relaxed after results. No live source/body/Unity/editor promotion.

The coupled muscle comparison is now launched as
`dynamic-muscle-comparison-20261113-cd1577`, frozen revision
`66e136dc464254d7936c77332085a6ae6f75bb9d`, task
`DNNE-Dynamic-Muscle-Comparison-90d60cea3207`. All 1,555 initial files plus manifest
have a verified D: copy, rechecked at launch. It is the only pending operator
report. Do not inspect its live outputs. All prior reports are reviewed; spindle
state/overlap repair may proceed independently without changing the preserved
native-adapted source or the running comparison. No timing study is pending.

The operator reported coupled-muscle `cd1577` FAILED. Full offline review verifies
all 2,259 C:/D: files per copy, frozen Git source, executed binaries and all 93
passing test identities. All 33 candidate traces, 21 adaptive references and nine
ordering controls completed; reference recording failed at an empty dense-output
query between 500.015625 and 500.03125 ms. Preserved internal states show that
interval integrated successfully. See `docs/DNNE_Dynamic_Muscle_Comparison_2026-11-13.md`.
The separate review has a verified D: copy. Overall numerical acceptance remains
incomplete; preserve FAILED and unchanged equations/inputs/tolerances/bounds.
The recorder now guards only interpolation, always carrying state/checks through
unsampled intervals. Three analytic boundary tests with both DOP853 and Radau
are mandatory in the fresh visible-window retry before the full 93/33/33/15 rig.
Syntax checks pass; do not claim those regressions pass until operator-reported
execution. No previous report remains pending. No live Avatar/Unity/editor or
brain-save changes accompany this harness repair.

The fresh full comparison is now launched as
`dynamic-muscle-comparison-20261113-95e59f`, frozen revision
`cc84abe349f7fbd4a442b2da2ad7e120611ba272`, independent visible PowerShell task
`DNNE-Dynamic-Muscle-Comparison-f76ff58863e5`. All 1,557 initial files plus manifest
have a verified D: copy, rechecked at launch. This is the only pending report;
do not read its status/logs until the operator reports completion. Its three
analytic cases/six method checks and full 93/33/33/15 comparison are not yet
qualified. Spindle source work can proceed independently; no timing window is
pending. The original failed capture and separate full review remain preserved.

The operator reported muscle retry `95e59f` complete. Full review verifies all
2,299 C:/D: files, source and binaries and exactly reproduces the full saved
analysis. All 93 tests, six analytic method checks and 33 candidate conditions
pass; all 33 adaptive references and fifteen ordering diagnostics complete.
Recorded fixed-fiber calcium/force match exactly. Separate review has a verified
D: copy; see `docs/DNNE_Dynamic_Muscle_Comparison_2026-11-13.md`. No muscle report
is pending. This is isolated numerical qualification, not calibration or live
motor/body authority. Build causal physical coupling on these preserved controls.

The spindle canonical-state candidate and comparison are now coded in
`docs/DNNE_Spindle_State_Candidate_2026-11-13.md`. Read-only population views derive
from y; scalar initialization and exact geometry remove state/reporting faults.
Zero-overlap kinetics remain explicitly unsupported and reject before evolving;
do not call that a physiological boundary repair or insert epsilon/clipping.
A pure trial-stress function uses authoritative populations and returns detached
mass without mutation. Original source/license and completed captures stay
preserved. Python/PowerShell syntax checks pass; Octave class/trajectory/control
results await a fresh visible window. No live Avatar/Unity/editor/save changed.

Initial spindle-state preparation `c9a66b` rejected before launch because an
unrelated imported verifier required a motor protocol outside isolated Python's
search path. The preparer now verifies selected C:/D: files directly. The frozen
setup rejection is preserved with a verified D: copy (1,639 files plus manifest),
ran no model/test, and is not pending. Use a fresh package after committing.

Corrected canonical spindle comparison `spindle-state-20261113-3468db` is now
launched at `cd0c193d44426704dce0a03b3d27aa0864088d95`, task
`DNNE-Spindle-State-0f7f73e4c0ec`. Preparation verified all 59,532 runtime files
and the separate 18-file transformed execution tree. All 1,697 initial files
plus manifest have a verified D: copy, rechecked at launch. This is the only
pending operator report; no live monitoring. No numerical/biological pass is
inferred from launch. All older reports, including muscle `95e59f`, are reviewed.

The operator reported spindle `3468db` FAILED. All thirty trajectories/two boundary
records complete; full review verifies C:/D: files, source and runtime fingerprints,
exact state/post-initial-stress agreement and repaired state views/scalar force/
overlap geometry. The first subsequent unloaded-root assertion fails; its root
value was not saved, so cause is unestablished. Review has a verified D: copy.
The next diagnostic preserves initial states, direct force probes and root/solver
results before unchanged assertions, preflight and after the matrix. No tolerance
relaxation or physiological/root repair is inferred. See the spindle-state doc.

An unbuilt motor/body coupling draft is preserved in stash
`7c38cb3e111d59bd1bec9aa612def30e50fd9b62` and D:-verified
`artifacts/causal-motor-draft-20261113` (three files plus manifest). Restore after
freezing the next spindle window; do not lose or treat it as validated code.
Continuation notes describe the start-held physical partition and required
preflight time-grid fix/tests/reference comparison. Live paths/saves are untouched.

Spindle root-diagnostic retry `spindle-state-20261113-ae7ae4` is launched at
`b60ee5b95908e5c866b5ed83014947bf14dbb0af`, task
`DNNE-Spindle-State-7f9eefcd0c40`. All 1,698 initial files plus manifest have a
verified D: copy, rechecked at launch; all pinned runtime files verify. This is
the only pending operator report. No live monitoring or inferred root pass.
Restore the preserved causal-motor draft for independent development as above;
it is not built, tested or selected live.
# Causal motor/body candidate, 13 November

`docs/DNNE_Causal_Motor_Body_2026-11-13.md` records the isolated causal motor-to-body
owner and its explicit start-held tension/geometry partition. The qualified
muscle core is unchanged except for the declared same-time boundary observation.
27 new cases plus 93 regressions are prepared for a visible PowerShell package;
authoring compilation is not a test result. Do not promote this first-order
coupling without independent full mechanical trajectory refinement. Preserve
measured metre/newton units, matched force/path geometry, actual fractional
arrivals, all-source completeness and one physics owner. Neither this candidate
nor the spindle candidate is live-selected. See the continuation document for
run identifiers; inspect outcomes only after the operator reports completion.

Causal motor component package `causal-motor-20261113-848e50` is launched from
`4c234d5` in visible task `DNNE-Causal-Motor-a847a7d5df52`; initial D: backup
verified 1,563 files plus manifest. Its 120 test outcomes are not yet inspected.
Together with spindle root package `ae7ae4`, it awaits the operator's completion
report. Do not monitor either package. Native/live coupling remains unselected.

Both `ae7ae4` and `848e50` were subsequently reported complete and reviewed.
`docs/DNNE_Spindle_State_Candidate_2026-11-13.md` explains the finite scalar-search
range failure and corrected explicit initial-geometry bracket with unchanged
root/stress bounds. All 120 causal motor cases pass; the frozen muscle core and
current coupling source match. `docs/DNNE_Causal_Motor_Comparison_2026-11-13.md`
records the prospective 40-condition/24-reference coupled study. Its probe builds
and Python source parses, but analytic reference controls, frozen preparation
and the visible runner are still required. No numerical comparison has run.
Both review artifacts have verified D: copies. Consult later launch records
before identifying pending work; these two reported packages are no longer pending.

Corrected spindle root package `spindle-state-20261113-a43f1a`, frozen `b9937e9`,
is launched in visible task `DNNE-Spindle-State-83574529f0c3`. All 1,712 initial
files plus manifest are D: verified; it is the only pending run. Do not monitor
it. The coupled motor comparison's analytic reference controls are now authored
and syntax checked, but unexecuted. Its frozen preparer and visible runner are
still required. No coupled numerical qualification or live promotion is claimed.

The coupled comparison's frozen preparer and visible runner are now implemented
and syntax checked: require the verified 120-case review, unchanged qualified
muscle/reference source and dependencies, eight reference controls, then all
40 candidate conditions and 24 adaptive references. The controls preserve actual
and analytic traces on failure. Authoring executed no test or model. Consult the
comparison/continuation launch records before identifying pending packages.

Spindle `a43f1a` was reported complete/FAILED at analysis, then fully reviewed.
Its native matrix and controls completed; the adapter lacked its existing
`vector` and `one` parsers. Adding only those helpers lets the unchanged saved-data
analysis pass all canonical-state criteria: 30 exact state/post-initial-force
contrasts and passing root/force/population controls. Original FAILED evidence
is preserved; `artifacts/spindle-state-review-20261113` has the corrected analysis
and a verified D: copy. No model rerun is needed for this adapter correction.
The spindle run is no longer pending. Independent numerical/physiological and
live-body requirements remain open; see later launch records for motor work.

Coupled comparison `causal-motor-comparison-20261113-2acbfd`, frozen `910a1d6`,
is now the only pending run. Visible task `DNNE-Causal-Motor-Comparison-cf2a28bca82f`
was launched after verifying all 1,580 initial files plus manifest on D:. It gates
40 candidate/24 reference conditions on 120 regressions and eight analytic/
quadrature controls. Do not monitor; await the operator's completion report.
No numerical qualification, live selection or Avatar recovery is claimed.

Coupled comparison `2acbfd` was subsequently reported complete and fully reviewed:
120 regressions, eight analytic/quadrature controls, 40 candidate conditions and
24 independent references pass the original criteria. The full C:/D: capture,
source/binaries and reproduced analysis verify; derived review is D: backed.
This qualifies the isolated fixture at 0.125 ms physical steps, not native Unity
or physiology. No older run remains pending at this checkpoint.

`docs/DNNE_Muscle_Path_Geometry_2026-11-13.md` records the isolated fixed-attachment
geometry candidate and its 31-case analytic/parity suite plus 44 regressions.
Length, speed and every attachment/via reaction must derive from the same path;
do not use normalized legacy joint lengths as calibrated metre-valued anatomy.
Generate the Unity copy using `prepare-unity-muscle-paths.py`. The library does
not authenticate native poses/clocks; an eventual adapter must preserve the
existing clock, session, effort receipt and sole physical owner. No live path
is selected. Total musculotendon length and spindle-sensed fiber state remain
distinct. See later launch records; compilation alone is not qualification.

Muscle geometry package `muscle-paths-20261113-2d0fa4`, frozen `31a6f45`, is now
launched in visible task `DNNE-Muscle-Paths-7a1d62f9f57f`. All 1,581 initial files
plus manifest are D: verified and rechecked at launch. This is the only pending
operator report. The 75 test outcomes remain uninspected; no live monitoring.
Native Unity forces, anatomy and recovery are not qualified by source parity.

Geometry `2d0fa4` was subsequently reported complete and fully reviewed. All 75
cases and full C:/D:/source/binary/method checks pass; derived review is D: backed.
It is no longer pending. `docs/DNNE_Native_Muscle_Paths_2026-11-13.md` specifies
the next separate native point-force fixture: 24 cases, 48 physical steps,
one owner and original float-boundary bounds. The empty qualification scene
contains no live Avatar/DNNE. Free-root impulse checks cannot qualify constrained
joints, anatomy or whole-body motion. See later launch records; do not monitor.

Native point-force `native-muscle-paths-20261113-d9097a`, frozen `c9eff12`, is now
launched in visible task `DNNE-Native-Muscle-Paths-ea468e8fcb13`. All 1,589 initial
files plus manifest are D: verified and rechecked at launch. This is the only
pending operator completion report. No build/native outcome has been inspected;
do not monitor. All preceding spindle, coupled motor and geometry reports are reviewed.

Native point-force `d9097a` was subsequently reported FAILED and fully reviewed:
the native player/24 cases/48 physical steps and 75 regressions pass. PowerShell
typed decimal literals and mixed Decimal/Double equality caused checker failures.
The repaired saved-data checks and independent geometric/impulse reconstruction
pass original bounds; all C:/D:/source/binaries verify. Original failed evidence
is intact and derived review `native-muscle-path-review-20261113-verified` is D:
backed. No model rerun was needed and no older report remains pending.

Work resumed on 16 November after the operator's pause. The separate constrained
hinge candidate is documented in `docs/DNNE_Native_Muscle_Hinge_2026-11-16.md`:
24 cases/72 steps, generalized inertia, analytic moment arm/power, native impulse,
joint-angle/pose/velocity consistency and force clearing. Fixed-root diagnostics
are isolated fixtures, never live balance mechanisms. Independent saved-data
analysis and frozen visible runner are implemented; consult later launch records.

Hinge package `native-muscle-hinge-20261116-de7da9`, frozen `301fbfd`, is now
launched in visible task `DNNE-Native-Muscle-Hinge-c0f66b95c7c7`. All 1,598 initial
files plus manifest are D: verified and rechecked at launch. This is the only
pending operator report. Do not monitor; no build/native outcome is inspected.

Hinge `de7da9` subsequently reported FAILED and is fully reviewed: 75 regressions
pass; a malformed 33-character editor-script GUID prevented Unity from finding
`MuscleHingeBuild`. No native physics ran. All original capture/source/test data
verify; `native-hinge-build-review-20261116` preserves compiler evidence with a
verified D: copy. The corrected GUID and `tools/unity_metadata.py` preflight
protect native muscle packages. Require valid unique asset identities and C#
sidecars; do not alter mechanical bounds for this import failure. Consult later
launch records; this failed package is no longer pending.

Corrected hinge `native-muscle-hinge-20261116-5a98f8`, frozen `200d804`, is now
launched in visible task `DNNE-Native-Muscle-Hinge-1fb99fb6333b`. Metadata preflight
passes; 1,601 initial files plus manifest have a verified D: copy, rechecked at
launch. This is the only pending completion report. No live monitoring.

Corrected hinge `5a98f8` subsequently completed and is reviewed: all 75 regressions,
24 native cases and 72 physical steps pass original bounds. Full capture/source/
binaries and D: copies verify; `native-hinge-review-20261116` is D: backed.
See the completed section of `docs/DNNE_Native_Muscle_Hinge_2026-11-16.md`.
Single-step hinge mechanics pass; coupled native trajectory accuracy, anatomy,
physiology and Avatar recovery remain open. Use the actual promoted Unity float
step for coupling; nominal 0.125 ms is slightly over the muscle maximum and needs
substeps. No completion report remains pending at this review.

The next isolated native motor/body bridge is described in
`docs/DNNE_Native_Motor_Bridge_2026-11-16.md`. Existing muscle/calcium/causal cores
are unchanged. A sole Unity owner applies start-held axial tendon forces to two
synthetic opposing paths and returns complete native geometry/effort receipts.
Native float clock, state and identity checks prevent unowned stepping or stale
force application. Four causal cases and eight native rejection controls are
prepared; they use fixture events, not a running neuronal source. No live body or
saved brain is selected. Individual impulse and qualitative response criteria
cannot qualify complete coupled trajectories, physiology or Avatar recovery.
Consult later launch records; run only in independent visible PowerShell with no
live monitoring and preserve verified D: evidence.

Native motor/body bridge `native-motor-bridge-20261116-223098`, frozen `3d87882`,
is launched in visible task `DNNE-Native-Motor-Bridge-c5b38826821e`. Its 1,617
initial files plus manifest are D: verified and rechecked at launch. This is the
only pending completion report. No post-launch outcome has been inspected;
wait for the operator and do not monitor.

Native motor/body bridge `223098` subsequently reported FAILED and is reviewed:
75 regressions and Unity build passed; nine quiet steps completed. Request 33
was rejected before physics by the effort/endpoint check despite an exactly
correct saved timestamp and fresh GUID. Native decoded bits were not recorded,
so decimal parsing is suspected, not confirmed. All C:/D:/source/binary evidence
verifies; `native-motor-bridge-failure-review-20261116` is D: backed. The candidate
v2 wire preserves exact binary64 bits and first reproduces the original parser
failure with zero physical bodies, then checks 1,040 exact scalar round trips
and ten malformed encodings before any physical retry. No timing/physical bound
or muscle model changed. See `docs/DNNE_Native_Motor_Bridge_2026-11-16.md` and later
launch records; the failed `223098` report is no longer pending.

Exact-scalar bridge retry `native-motor-bridge-20261116-bd7079`, frozen `46eaa51`,
is launched in visible task `DNNE-Native-Motor-Bridge-769bbf16dc61`. All 1,622
initial files plus manifest are D: verified and rechecked at launch. This is the
only pending operator report; native decimal diagnosis, codec and physical
outcomes have not been inspected. Do not monitor. Preserve original bounds and
require the native zero-body preflight before interpreting the physical retry.

Bridge `bd7079` subsequently FAILED and is fully reviewed. Native preflight
confirms the original decimal parser was one ULP wrong; all 1,040 exact-wire
round trips and ten malformed encodings per side pass. Quiet/left complete;
right native step 240 completes but its inspect reply is locked on host read,
leaving plant step 239. Do not repeat that physical command. All C:/D: evidence
verifies; `native-bridge-read-failure-review-20261116` is D: backed. The locking
process is unknown. Shared bounded immutable-file reading now retries only
missing/sharing-conflict reads, never force commands; other errors stay fatal.
Six file checks raise the focused suite to 81. A deliberate completed-reply
lock in the quiet case must recover with exactly one native step. Builds/static
checks pass; execution awaits the next launch record. No live Avatar selection
or dynamics/bounds changed. The failed `bd7079` report is no longer pending.

Read-recovery retry `native-motor-bridge-20261116-e7ddb6`, frozen `ea47f66`, is
launched in visible task `DNNE-Native-Motor-Bridge-de39adaad238`. All 1,627 initial
files plus manifest are D: verified and rechecked at launch. This is the only
pending operator report. No post-launch outcome has been inspected; do not
monitor. Require the 81-case suite, exact-wire preflight and deliberate locked
completed-reply recovery before interpreting the full physical matrix.

Bridge `e7ddb6` subsequently completed and is fully reviewed: all 81 tests, four
physical cases / 4,096 steps, eight native rejection controls, exact-wire checks
and deliberate completed-reply lock recovery pass. Full C:/D:/source/binary
evidence verifies; `native-motor-bridge-review-20261116` is D: backed. No older
report remains pending. This qualifies the isolated bridge, not full trajectories,
physiology, neuronal-source operation or recovery. The next captured-trajectory
comparison is in `docs/DNNE_Native_Hinge_Comparison_2026-11-16.md`: independent
nonlinear geometry and continuous muscles, 49 geometry controls, six mechanical
method controls and 12 adaptive references reusing saved native data. Physical
and muscle step refinement remain explicitly unassessed. Scripts parse and saved
inputs decode; no new solve has run. Consult later launch records, use visible
PowerShell and no live monitoring, and preserve the original bounds/evidence.

Captured native trajectory comparison `native-hinge-comparison-20261116-967542`,
frozen `9d6aca6`, is launched in visible task
`DNNE-Native-Hinge-Comparison-553670a7b85c`. All 1,657 initial files plus manifest
are D: verified and rechecked at launch. This is the only pending operator
report. No reference control/solve/analysis has been inspected; do not monitor.
It reuses completed native captures and cannot qualify physical/muscle step
refinement, live neuronal authority, physiology or Avatar recovery.

Comparison `967542` subsequently completed and is fully reviewed: all 49 geometry
controls, six mechanical controls, three control contrasts, 12 references and
four native comparisons pass original bounds. All C:/D:/source/dependency evidence
verifies; `native-hinge-comparison-review-20261116` is D: backed. No older report
is pending. The next native resolution matrix is specified in
`docs/DNNE_Native_Hinge_Refinement_2026-11-16.md`: five independent physical/muscle
combinations, 93 tests, 20 physical cases and 40 rejection controls, reusing the
qualified references on exact common grids. Actual float steps, source identities
and one physical owner remain mandatory. Fine half-step reference accuracy is
unassessed; do not inflate sampled convergence into physiology or recovery.
Builds/static checks pass; no new test or native matrix has run. Consult the next
launch record, use visible PowerShell without live monitoring, preserve evidence
and keep the original prospective bounds.

Native refinement `native-hinge-refinement-20261116-421387`, frozen `e59d9ea`,
is launched in visible task `DNNE-Native-Hinge-Refinement-4fa325232e75`. All 1,713
initial files plus manifest are D: verified and rechecked at launch. This is
the only pending operator completion report. No post-launch output has been
inspected; do not monitor. Require the 93 focused tests, all five complete
native matrices and the original common-grid/reference/refinement criteria.

While awaiting `421387`, the observed neuronal source adapter and prospective
source gate are prepared in `docs/DNNE_Observed_Motor_Source_2026-11-16.md`.
They preserve the checked motor cell, endpoint scheduler and Avatar transport,
with cell-owned advancement, original fractional crossings and retained-batch
backpressure/retry. Six new ownership tests and five full source-reproduction
conditions are authored; builds/static provenance are not test execution.
No new model/test/native run has been launched. The source gate must reproduce
all 30 state traces and exact event identities/timing before the adapter reaches
native moving muscles; it does not qualify live spinal or human motor authority.
The only pending operator report remains `421387`; do not inspect its outputs.

The operator has now reported refinement `421387` complete. Its saved status
reports all 93 tests and the full native resolution matrix passing. A read-only
full C:/D:/binary and saved-analysis review is in progress using
`tools/review-native-hinge-refinement-20261116.py`; consult the later results
record before claiming this review complete. The observed motor-source gate has
its own frozen preparer, 66-test/five-condition visible PowerShell launcher and
independent saved-event audit, documented in
`docs/DNNE_Observed_Motor_Source_2026-11-16.md`. It uses already reviewed neuronal
reference traces and starts no Unity or live brain. Consult later launch records;
no newly launched study may be monitored before the operator reports completion.

Refinement `421387` is now fully reviewed: all 93 tests, 20 physical cases /
26,624 steps and 40 rejection controls pass. All five command audits and the
saved convergence analysis reproduce exactly, with full C:/D:/source/binary
verification. Eight physical and eight muscle contrasts pass unchanged bounds;
all four baseline observed-state arrays repeat bitwise. The derived
`native-hinge-refinement-review-20261116` is D: backed. See
`docs/DNNE_Native_Hinge_Refinement_Results_2026-11-16.md`. This supports sampled
convergence, not fine half-step reference accuracy, physiology or recovery.

Observed source gate `observed-motor-source-20261116-badc1b`, frozen `3cd9c72`,
is launched in visible task `DNNE-Observed-Motor-Source-6619bb55f4a0`. All 1,649
initial files plus manifest are D: verified and rechecked at launch. This is
the only pending operator report; no post-launch output has been inspected.
Require the 66 focused tests, five complete source conditions and independent
saved-event audit before attaching the newly factored adapter to moving muscles.
Do not monitor or infer success from process completion. No live body is selected.

Source gate `badc1b` is now reported complete and fully reviewed: all 66 tests,
five complete 30-state/six-second source traces, exact original crossings and
delivery pass. Each active condition preserves 28 events; all 85 retries are
exact. Full C:/D:/source/binary evidence verifies and the saved analysis reproduces;
`observed-motor-source-review-20261116` is D: backed. See
`docs/DNNE_Observed_Motor_Source_Results_2026-11-16.md`. No older report is pending.
The next plan is `docs/DNNE_Observed_Native_Body_2026-11-16.md`: an explicit quiet
one-second prehistory attaches unchanged neuronal state to a fresh paused native
body, followed by seven conditions / 14,336 steps and 12 independent references.
The new ownership extension requires 160 focused tests and the full six-second
source gate before native connection. Source lookahead is allowed only for this
declared open-loop reference; it does not implement a closed sensory loop or live
DNNE authority. Consult later launch records, preserve original bounds and old
saves, run only in visible PowerShell without live monitoring.

Observed neuronal/native study `observed-native-body-20261116-bb10dc`, frozen
`f9322f1`, is launched in visible task `DNNE-Observed-Native-Body-b834d2a0e6db`.
All 1,666 initial files plus manifest are D: verified and rechecked at launch.
This is the only pending operator report; no post-launch output has been
inspected. Require all 160 tests, the five full source conditions, seven native
conditions and 12 independent references with original bounds before interpreting
this coupled candidate. Never treat running reference cells under injection as
qualified live DNNE recruitment, sensory feedback, physiology or recovery.

While awaiting `bb10dc`, the fixed-length spindle accuracy comparison is prepared
in `docs/DNNE_Spindle_Fixed_Length_Accuracy_2026-11-16.md`. It reuses 18 completed
binary MAT captures from the fully reviewed canonical-state `a43f1a`, preserving
original equations, fibre-specific constants, licenses and old evidence. Six
analytic controls, an analytic-Jacobian check and 18 continuous reference solves
are prospective; no new solve/test ran during authoring. All 18 saved captures
parse and their force reconstruction agrees within 5.83e-11 Pa. The authoring
review is D: backed. Consult the subsequent launch record before expecting a
window. Keep all original bounds; fixed-length accuracy does not qualify moving
or slack mechanics, afferent membrane firing, live sensory integration or recovery.
The existing Avatar sensor path is preserved. Do not monitor `bb10dc`.

Fixed-length spindle comparison `spindle-fixed-20261116-18ec5e`, frozen
`7cce124`, is launched in visible task `DNNE-Spindle-Fixed-98ac11033347`.
All 1,714 initial files plus manifest are D: verified and rechecked at launch.
No post-launch outputs have been inspected. Two operator reports are pending:
this comparison and `observed-native-body-20261116-bb10dc`. Do not monitor either.
Require six analytic controls, the Jacobian check, 18 independent solves,
18 reference contrasts and 54 native/reference comparisons with original bounds.
No live body, sensory transducer, brain, editor selection or old save was changed.

The follow-up Ia source audit is `docs/DNNE_Afferent_Source_Gap_2026-11-16.md`.
Public ModelDB catalogues now returned 1,931 entries; four pinned author trees
and six Git-blob-verified Kim model files were inspected. The Housley Ia membrane
was not located (absence is not proven); Dryad's public download returned 403.
Kim's inspected Ia inputs are preset/sinusoidal conductances, not a sensory
membrane, and are not adopted. Preserve the biological sensory objective rather
than substitute those inputs or generic HH/rate encoding. The 30-file source
review is D: backed; no models/tests ran and neither pending output was read.
Do not repeat identical source searches without new evidence. The two reports
remain `bb10dc` and `18ec5e`; no third window has launched.

The next isolated increment is documented in
`docs/DNNE_Motor_Receptor_Contacts_2026-11-16.md`. A contact-owned two-state
AMPA/GABA-A conductance component accepts identified original presynaptic
events, preserves exact replay and conditional analytic state, and rejects
unsupported overlap before mutation. The inspected ModelDB 18500 kinetics
are hippocampal reference values, not calibrated spinal physiology. It does
not yet couple to the checked motor membrane or change a live selected circuit.
The checked motor membrane/channels/transport remain unchanged. A build passes
with zero warnings/errors; 14 new cases plus 60 regressions are prospective,
not executed. The 11-file authoring/source capture is D: verified. Consult the
following launch record before expecting its window. The two pending reports
remain `bb10dc` and `18ec5e`; do not inspect either output or monitor new runs.

Motor receptor gate `motor-receptor-20261116-501da5`, frozen `74e1c62`, is
launched in visible task `DNNE-Motor-Receptor-0dcbf8a904cc`. All 1,673 initial
files plus manifest are D: verified and rechecked at launch. Three operator
completion reports are now pending: this 74-case component/regression gate,
observed/native body `bb10dc`, and fixed-length spindle `18ec5e`. No post-launch
outputs or processes have been inspected. Do not monitor. No coupled neuronal
recruitment, independent receptor reference, physiology or recovery is qualified
by preparing or launching this component. Consult the receptor document and
inspect saved evidence only after the operator reports completion.

The receptor-to-membrane candidate is now authored in
`docs/DNNE_Receptor_Driven_Motor_Cell_2026-11-16.md`. A private neuronal owner
retains original input batches, explicit completeness and per-contact state,
splits release boundaries, and evaluates point currents at all RK4 stages.
The shared motor-cell integrator has an explicit new input path; do not claim
the source file is unchanged or reuse an unchanged-core preparer on this HEAD.
The old input arithmetic is retained, but complete five-condition source-trace
reproduction and the 97-case inventory are prospective. Fifteen new cases are
authored; builds pass with zero warnings/errors. No test/model ran during this
authoring and no fourth window was opened. Review pending receptor `501da5`
before launching the next explicit candidate package. The other pending reports
remain observed/native `bb10dc` and spindle `18ec5e`; do not monitor any of them.

The final authoring capture is `receptor-membrane-authoring-20261116-r2`
(9 files plus manifest, D: verified); the earlier 11-file authoring capture
is retained. The new scheduler derives exact endpoint differences and limits
early interval growth; its exact-binary accounting test remains prospective.
All 97 method/case declarations were counted statically, supporting source
files and parameter declarations are preserved, and the latest build passes.
This is compilation/source evidence only, not a passed neuronal experiment.

The deferred receptor/membrane verification rig is now authored in
`tools/receptor_membrane_gate.py` and `tools/start-receptor-membrane-window.ps1`.
After the operator reports receptor `501da5` complete, use the explicit
`review-component` acknowledgement path; do not run it early. A passing review
and its verified D: copy are mandatory inputs to `prepare`. The new package
will run all 97 cases and all five full original source conditions. It declares
the changed membrane and source-ownership extension, preserves the other
source/reference files and retains original exact-trace requirements. No new
package/window was created. Native probe build and syntax checks pass; the
TRX reader was checked only against already reported/reviewed `badc1b` (66
saved passing cases), without rerunning tests. The five-file
`receptor-membrane-rig-authoring-20261116` capture is D: verified. Pending reports
remain `bb10dc`, `18ec5e`, and `501da5`; none of their outputs were read.

The independent coupled comparison is authored in
`docs/DNNE_Receptor_Membrane_Numerics_2026-11-16.md`: six conditions at three
candidate resolutions and three independent reference settings, retaining all
accepted endpoints, 648 contact deliveries and original membrane bounds. The
reference integrates receptor occupancies as ODE states instead of using the
candidate's analytic evaluator. Six analytic and 36 Jacobian controls are
prospective; no new ODE/test ran. The soma stress input is a numerical fixture,
not an anatomical default. A comparison package/visible launcher is still to
be prepared after the prerequisite 97-case/five-source-condition gate passes.

Source review also found a one-ULP tail hazard in the new owner scheduler.
It now repartitions the preceding interval so both RK midpoints are representable,
without shifting an input boundary or losing duration; the existing clock case
now covers that scenario. The 97-case inventory is unchanged. Its latest
authoring capture is `receptor-membrane-authoring-20261116-r3` (7 files plus
manifest, D: verified), and `receptor_membrane_gate.py` now requires r3. Older
captures are preserved. Test-project and recorder builds pass with zero warnings
or errors. `receptor-coupled-authoring-20261116` (11 files plus manifest) is also
D: verified; it records syntax/input checks and reconstruction of 30 identities
from already completed `badc1b`, not new tests. No new study package or window
was launched; the three pending operator reports remain unchanged. Do not monitor.

The deferred coupled comparison now has a preparer and visible launcher:
`tools/prepare-receptor-membrane-comparison.py` and
`tools/start-receptor-membrane-comparison-window.ps1`. They require the successful,
operator-reported 97-case/five-source gate review, its D: copy and unchanged
compiled inputs. `receptor_membrane_gate.py review-gate` must not be called before
that future report. The comparison reuses the exact gate probe and retains its
source archive; it does not rebuild or upgrade dependencies. Managed runtime
metadata is captured before/after the gate and compared with the later study.
The pinned numerical environment, upstream sources, all accepted endpoint traces
and original bounds are protected. The scope remains fixture presynaptic input
to an isolated motor cell, not live neuronal authority or spinal physiology.

`receptor-comparison-rig-authoring-20261116` records a zero-warning/error build,
syntax checks, 97 source case declarations and five preserved independent
reference files; it is D: backed. No tests/models were executed and no new
comparison package or fourth window was created. Pending operator reports remain
observed/native `bb10dc`, spindle `18ec5e`, and receptor component `501da5`.
None of their outputs or processes has been inspected. See the numerical plan
for the explicit report/review/prepare/backup/launch sequence; do not monitor.

The isolated receptor-driven motor source now has an outgoing transport owner:
`Structures/_SharedRuntime/ReceptorMotorSource.cs`. It retains actual observed
crossings, preserves one cell's identity and complete quiet prehistory, and uses
the existing Avatar receiver/protocol without changing them. Its fixed source
grid is independent of publication/retry; bounded packets can drain a completed
prefix without concealing retained crossings. Diagnostic channel blocks require
explicit opt-in, and the passive transport controls do not qualify active firing.
See `docs/DNNE_Receptor_Motor_Output_2026-11-16.md` for scope and remaining gates.

The next deferred membrane gate now requires **111 cases**, adding 14 source/
Avatar transport cases to the old 97, followed by the same five original source
conditions. Both preparers, the reviewer and visible launcher use this expanded
inventory. Preserve the older 97-case authoring captures; the preparer also
requires `receptor-motor-source-authoring-20261116`, D: verified. Its build passes
with zero warnings/errors; no test or model executed. No live selection or new
window was made. Pending operator reports are still `bb10dc`, `18ec5e`, `501da5`;
their outputs/processes were not inspected. Do not monitor or promote the new
source to physiological/live authority before the required independent checks.

The deferred numerical comparison now includes a full active source/Avatar
receiver phase, documented in `docs/DNNE_Active_Receptor_Motor_Output_2026-11-16.md`.
The 18 raw candidate conditions are followed by 36 fresh active source runs at
two packet cadences, then saved-data audits and the unchanged independent
numerical controls/reference. Require all 540,036 sampled observations, original
crossings, 1,296 input-contact deliveries, wire receipts and receiver timing.
The source never installs baseline state or prescribes baseline crossings.
Retain any arrivals beyond the horizon; no fabricated completeness or tail flush.

The new recorder, protocol, independent saved-data audit and frozen launcher
extension compile/parse; no test, neuron, new package or window ran during
authoring. `receptor-active-output-authoring-20261116` is D: verified. The actual
cell/receptor/transport code and numerical equations/bounds remain unchanged.
The prerequisite remains the reported/reviewed 111-case/five-source gate, which
itself awaits receptor `501da5`. Other pending reports remain native-body `bb10dc`
and spindle `18ec5e`. None was monitored. Active output agreement, independent
numerical accuracy, presynaptic neuronal authority, native muscle coupling,
physiology and Avatar recovery remain distinct requirements.

The operator has now reported `bb10dc`, `18ec5e` and `501da5` complete. The receptor
component's 74 cases and full C:/D:/source/binary evidence verify; its review is
`motor-receptor-review-20261116`, D: backed. The spindle's fixed-length saved
analysis reproduces with all 54 comparisons passing; `spindle-fixed-review-20261116`
is D: backed. The 89,458-file native-body evidence review is still in progress.
See `docs/DNNE_Body_Spindle_Receptor_Results_2026-11-16.md` and later records.
The verified receptor review now permits preparation of the 111-case gate; no
new body's result is presumed by that dependency. Subsequent launched studies
still require their own completion reports; do not monitor them.

All three reported captures are now reviewed and their separate reviews D:
verified. The native-body `bb10dc` evidence verifies all 89,458 files per copy,
160 cases, seven physical conditions, 14,336 steps and twelve independent
references. Saved source/native analyses reproduce exactly; 14 delivered events
drive the isolated native hinge over approximately 128 ms per condition. Packet
cadence/retries preserve state bitwise. This is injected-current reference-cell
causality, not live DNNE authority, physiological calibration or Avatar recovery.
The review's nominal-duration assertion was corrected to verify the actual
binary32 Unity clock in every case; no numerical comparison bound changed.

The next 111-case/five-source gate is now launched:
`receptor-membrane-20261116-f2c9a7`, frozen revision
`2453007fad2c1acdfff476fb7a70dc2ff46f5d2f`, visible task
`DNNE-Receptor-Membrane-5ad21bf3811c`. Its initial 1,712 files plus manifest were
D: verified before launch. This is the only pending operator completion report.
No post-launch outputs/status/processes were inspected; do not monitor it.
The 18 raw/36 active-output/18 independent-reference comparison remains deferred
until this gate is reported complete, successfully reviewed and D: backed.

The next independent spindle work is documented in
`docs/DNNE_Active_Spindle_Slack_2026-11-16.md`. Avatar's existing muscle transducer
and legacy/dynamic muscle boundaries were inspected first. Twelve previously
reported moving MAT traces retain populations but permit negative imposed stress;
their experiment deliberately omitted slack handling. The new pure trial-force
component enumerates all roots on the source's positive-overlap domain, keeping
finite-support discontinuities and inadmissible/multiple roots explicit. It does
not select an equilibrium, clip force, reset state or install a sensory membrane.
Six analytic controls and all 2,812 saved-state comparisons belong to the new
visible-window rig; authoring/preparation executes no controls or new kinetics.
Do not infer active slack trajectories or biological afference from a successful
root enumeration. Preserve original bounds and the separate receptor gate.

The active spindle force window is now launched as
`spindle-slack-roots-20261116-527839`, frozen at
`cd5b775a4090f18ced1697ae025c7b71fd1f51ea`, visible task
`DNNE-Spindle-Slack-9f7199654b99`. All 1,741 initial files plus manifest were D:
verified before launch. Its first preparation `9e00e9` stopped on a report-field
name mismatch after parsing all saved traces; that partial package is preserved
and D: backed, with no experiment run. The corrected preparer uses a fresh
package. Six-file authoring evidence and the two-file preparer correction are D:
verified. No model, root control or search executed inline during authoring.
There are now two pending operator reports: spindle force `527839` and
receptor/motor `f2c9a7`. No post-launch output/status/process was inspected for
either; do not monitor. Source/physics correctness remains separate from
diagnostic completion and the unfinished biological brain/body/world goal.

The operator reported receptor gate `f2c9a7` and spindle force `527839` complete.
The receptor gate review verifies all 111 cases and five exact source conditions,
2,282 C:/D: files per copy and 1,679 Git files; its two-file review is D: backed.
The larger receptor comparison is now launched as
`receptor-membrane-comparison-20261116-f7ece6`, revision
`ade8c0252749a204dee4715cb88ab850d1492fb5`, visible task
`DNNE-Receptor-Comparison-3fc1a1c87fae`, with 1,729 initial files plus manifest
D: verified. No post-launch output/process was inspected; do not monitor.

The spindle's six controls passed, but its first matrix stopped at query 1,556
on step 48 of `chain-stretch-shorten-500us`. A small nonzero knot residual was
counted alongside its adjacent interval root, while the reference merged them.
The D:-verified `spindle-slack-review-20261116` preserves full failure provenance,
1,767 C:/D: files and saved-record checks without a root rerun. See
`docs/DNNE_Receptor_And_Slack_Results_2026-11-16.md`. Version 2 uses exact rational
near-knot classification, retains distinct close roots and keeps all original
bounds. Its new eight-control/full-matrix run must be separately launched and
operator-reported before reading it; authoring does not constitute a pass.

The independent timing/vestibular source inspection is recorded in
`docs/DNNE_Vestibular_Source_Candidates_2026-11-16.md`. Its 67-file pinned source
capture is D: backed; no downloaded code or model ran. ModelDB 244202 is somatic
with synthetic input and unresolved version/license details; the calyx source
lead requires COMSOL and was inspected only at tree/README/license level. Do not
substitute either for the missing qualified afferent, or claim open-loop source
lookahead works unchanged in a closed brain/body sensory loop.

The version-2 spindle window is now launched as
`spindle-slack-roots-20261116-ba3c0c`, frozen at
`e09d37db87b91a0e8df43f6db4fd66124be9228c`, visible task
`DNNE-Spindle-Slack-2bb1c5df9d5a`. All 1,746 initial files plus manifest were D:
verified. The seven-file `spindle-knot-repair-authoring-20261116` capture is also
D: verified, with parsed syntax and unchanged bounds but no inline controls or
queries. Exact near-knot interval classification retains roots even when their
floating representation lands on a knot; the separate reference reconstructs
near-knot affine force from two rational interior queries. Never use a distance
threshold to merge mathematically distinct roots.
Only two reports are now pending: receptor comparison `f7ece6` and spindle force
replacement `ba3c0c`. Neither output/status/process was inspected after launch;
do not monitor. The failed `527839` and successful `f2c9a7` captures are reviewed,
preserved and no longer pending.

The operator subsequently reported spindle force replacement `ba3c0c` complete.
Its saved-record review verifies 1,777 C:/D: files per copy, 1,687 frozen Git
files, unchanged numerical dependencies, eight passing controls and all 2,812
queries. `spindle-slack-v2-review-20261116` is separately D: backed. There are 418
single-root, 2,363 multiple-root and 31 no-root saved states; all 31 no-root states
contain sign-changing finite-support force jumps. See the completed section in
`docs/DNNE_Receptor_And_Slack_Results_2026-11-16.md`. This qualifies the pinned
force enumeration, not a slack controller or sensory physiology. Next isolate
cross-bridge distribution translation/boundaries and unloading history; do not
choose an arbitrary root, clip stress or relax bounds. Only receptor comparison
`f7ece6` now awaits an operator report; it remains uninspected and unmonitored.
No new experiment was launched during the review.

The additional `vestibular-calyx-source-20261116` capture is also D: backed. It
retains the pinned MPH template, supplementary PDF, stimulus, license/README and
read-only XML/text extraction. No imported code or COMSOL model ran. Active XML
feature correspondence and visual equation review remain outstanding; do not
treat the extracted text as a qualified portable sensory implementation.

Receptor comparison `f7ece6` is now operator-reported and reviewed, no longer
pending. `receptor-comparison-review-20261116` verifies 1,975 C:/D: files,
5,405,631,249 bytes, 18 passing absolute candidate/reference contrasts each and
all 36 source/receiver cases. One of 12 refinement checks fails: distributed
excitation crossing time at 0.0125 -> 0.00625 ms. Saved reference-endpoint
decomposition shows coarse cancellation of trajectory and linear-secant error;
do not loosen limits or rewrite the original FAILED status. See
`docs/DNNE_Receptor_Crossing_Diagnosis_2026-11-16.md`. The window's source-audit
flag was not reached after reference failure; its saved output audit actually
passes and has been independently reproduced. The new cubic crossing diagnostic
must run in its own visible window and await an operator report. No current
C# membrane/event implementation has changed.

`tools/spindle_population_transport.py` is an authored, unexecuted candidate,
with separate interval-overlap reference and explicit boundary outflow. Its
cell-mass interpretation changes the source's interpolation convention. The
required comparison rig remains unfinished; do not call the candidate tested or
promote it to Avatar. Requirements and preserved Avatar boundaries are in
`docs/DNNE_Spindle_Population_Transport_2026-11-16.md`.

The saved-endpoint crossing diagnostic is now launched as
`receptor-crossing-20261116-c099e3`, frozen at
`8a6b4c4431d62d2715258ef46bd7bbece1b3d3ad`, visible task
`DNNE-Receptor-Crossing-03729f91bef1`. All 1,713 initial files plus manifest are
D: verified; the eight-file authoring capture is separately D: verified. It
checks five analytic controls and all 27 saved brackets from 18 conditions,
evaluating derivatives but advancing no neuron/ODE or live body. This is the
only pending operator report. No post-launch output/status/process was inspected;
do not monitor it. The spindle transport rig remains unfinished and unlaunched.

The spindle transport rig is now authored: ten analytic controls, 25,308 saved
state translations, probes of every sign-changing jump in the 31 no-root states,
and 144 analytic histories with 36 spatial-refinement contrasts. Its source and
requirements are in `docs/DNNE_Spindle_Population_Transport_2026-11-16.md`.
Preparation only parses reported records; all numerical execution belongs to
the separate visible launcher. The candidate changes the boundary interpretation
to cell mass with explicit outflow, so do not claim exact old-rule preservation,
physical detachment calibration, complete actin/overlap state evolution, a
selected slack equilibrium or sensory firing from arithmetic success. The
receptor crossing window `c099e3` remains pending and must not be monitored.

Both subsequent windows are now operator-reported and reviewed. Crossing
`c099e3` verifies 1,718 C:/D: files, five controls, 27 brackets/54 polynomials,
18 conditions and twelve passing original refinement checks. Its D:-backed
`receptor-crossing-review-20261116` qualifies saved-endpoint interpolation only;
some coarse absolute errors worsen when cancellation is removed. Next implement
an explicitly versioned optional native estimator with preserved linear-mode
regressions and qualify actual event production/causal delivery. The original
full comparison remains FAILED; no current native implementation changed.

Spindle transport `ba9648` was launched from `6c7788c6f6f4e2734702069486bf0d2704c5b20a`
as visible task `DNNE-Spindle-Transport-326b0f581194`, initially 1,740 D:-verified
files. Its completed review verifies 2,110 C:/D: files, ten controls, 25,308
translations, 31 continuity probes, 144 histories and 36 spatial comparisons.
The review `spindle-transport-review-20261116` is D: backed. Same-grid arithmetic
passes, but force-moment bias, repeated-remapping diffusion and strong range
sensitivity remain. Even tiny force error can hide a distorted population.
See the completed transport document; investigate retained characteristics/
moving intervals and explicit domain/kinetic accounting before live promotion.
Both experiments were inspected only after their operator reports. There are
now no pending completion reports. The full brain/body/world goal is unfinished.

The optional native cubic crossing candidate is now authored. See
`docs/DNNE_Native_Crossing_Gate_2026-11-16.md`. It uses the actual accepted endpoint
derivative, changes only event localization, and rejects nonmonotone polynomials.
Linear mode remains the default with its original fingerprint and event IDs;
cubic mode has distinct cell/source versions. The prospective gate has 124 cases
and five exact original source regressions. Preparation and source/build checks
are not test passes; only the visible PowerShell launcher may execute them.
No live StructureEngine/Avatar/Unity selection changed. The full 18-condition
receptor numerical comparison and 36 source/receiver cases remain outstanding
for the new mode, even if this narrower gate passes. Do not overwrite or relabel
the original failed comparison, and do not monitor any launched window.

The native crossing gate is now launched as `native-crossing-20261116-0b35ae`,
revision `492c2a491340a7b1b7b3c29ce0464a5d095e850b`, visible task
`DNNE-Native-Crossing-119c16e5b6f9`. Its 1,714 initial files plus manifest were
D: verified. Source/build/syntax checks completed without inline test or model
execution. This is the only pending operator report; no post-launch output,
status or process was inspected. Wait for the user's report before review.

The operator has now reported native crossing `0b35ae` complete. Its review
`native-crossing-review-20261116` verifies 2,284 C:/D: files per copy, 1,704 Git
files, all 124 cases and five exactly reproduced original source conditions.
The two-file review is D: verified; no model/test ran during review. The optional
cubic component gate passes, but the full receptor/reference/source-output
matrix for cubic mode is still required. No completion report is now pending.

The independent retained-interval spindle candidate and its comparison rig are
authored in `tools/spindle_characteristic_transport.py` and
`docs/DNNE_Spindle_Retained_Transport_2026-11-16.md`. Grid observation never becomes
state; movement retains material-coordinate intervals and explicit absorbing
outflow. The prospective run has 12 controls, 25,308 saved translations, 144
exact-shape histories and 96 movement-cadence comparisons, plus the 31 original
no-root-state continuity probes. No kinetics, live sensory source or root branch
selection is implemented. Even success requires separate coupled attachment,
detachment, actin/overlap and sensory-membrane qualification. No test/comparison
has executed during authoring; launch separately and await the operator report.

The retained-interval study is now launched as
`spindle-characteristics-20261116-7c6db6`, revision
`683b0cf0eb4774b935558b557438c78b60add02c`, visible task
`DNNE-Spindle-Characteristics-2619cda0839d`. All 1,755 initial files plus manifest
were D: verified. This is the only pending operator report. No post-launch
output/status/process was inspected, and no controls or numerical comparisons
ran inline. The new transport remains an unqualified candidate. Native crossing
`0b35ae` is already reported/reviewed; do not ask for that result again.

The operator reported retained transport `7c6db6` complete. Its saved-array review
verifies 2,093 C:/D: files per copy, 1,710 Git files, twelve controls, all 25,308
queries, 31 continuity probes, 144 histories/8,208 endpoints and 96 cadence pairs.
History shape/force errors are at roundoff level; shared-position shape/force
are identical across tested movement subdivisions. The one-file
`spindle-characteristics-review-20261116` review is D: verified. This qualifies
isolated transport, not coupled kinetics, physiological boundaries or firing.
No reports are pending. The operator requested notes and a pause until tomorrow;
`docs/DNNE_Checkpoint_2026-11-16.md` records the source findings and next spindle/
native-timing work. Finish committing/pushing this checkpoint and pause the goal;
do not start another run until the operator resumes.

On 17 November the operator explicitly resumed the goal; it is active again.
The clean checkpoint was inspected. The next authored work is the full native
cubic receptor/motor comparison in `docs/DNNE_Cubic_Receptor_Comparison_2026-11-17.md`.
Only two probe adapters select the existing optional timing mode; the neuron and
receptor equations are unchanged. Its single visible window must rebuild/pass
124 cases and five original regressions, preserve all 18 original raw state CSVs
exactly, then run 36 source/receiver cases and the full independent comparison.
All 27 native event times must also match the reviewed endpoint cubic within the
prospective 1e-9 ms consistency limit; original numerical bounds are unchanged.
Build/syntax checks are complete, with no inline tests or model integration.
Spindle coupled attachment/detachment remains open. No current reports are pending
until this new comparison is separately launched; do not infer success from setup.

The full cubic comparison is now launched as `receptor-cubic-20261117-94d05d`,
frozen at `b0e011be405c85eee437966e1954e45ea730a454`, visible task
`DNNE-Receptor-Cubic-061aa8e40ec1`. Its 1,747 initial files plus manifest were
D: verified. This is the only pending operator report. No post-launch outputs,
status or processes were inspected. Do not monitor it or assume any stage passed.
The operator's resume remains effective; the goal stays active. Independent
spindle coupled-kinetics source/design work can continue without inspecting this
window. No live brain/body/world was started by this isolated comparison.

The independent coupled spindle candidate is now authored in
`tools/spindle_coupled_population.py`; see
`docs/DNNE_Spindle_Coupled_Chemistry_2026-11-17.md`. It retains interval shape,
splits at declared strain-rate faces, integrates attachment births with detached
myosin/active actin, and accounts separately for absorbing numerical outflow.
Its prospective visible-window study has twelve controls, eighteen fixed cases
against 54 saved independent contrasts, and 36 moving cases. Complete interval
shape, physical endpoints and accepted reaction states are retained. Temporal
and range differences are diagnostic; moving independent accuracy, physiology,
sensory firing and Avatar recovery stay unqualified. No live selector changed.
Source/syntax review is not execution. Keep the original reference bounds and
licenses, freeze clean pushed source and D: backups, then launch separately.
The receptor cubic report `94d05d` is still pending; do not inspect its outputs.

The coupled spindle study is now launched as `spindle-coupling-20261117-85ae1d`,
revision `0398904f04e58ec99e588cf05b841f799eea32ba`, visible task
`DNNE-Spindle-Coupling-0ed3065e5ae7`. Its 1,790 initial files plus manifest
were D: verified. Python/PowerShell syntax was checked; no tests or model work
executed inline. There are now TWO pending operator reports: this spindle run
and receptor cubic `94d05d`. Neither output/status/process has been inspected
after launch. Review only on operator completion. The whole goal stays active;
moving spindle accuracy, physiology, live neural authority and Avatar recovery
remain unqualified. Next decisions must use full saved distributions, not force
or grid totals alone, and keep frozen failures/reference limits unchanged.

The operator has now reported spindle coupling `85ae1d` complete. Review
`spindle-coupling-review-20261117` verifies 1,992 C:/D: files, 1,720 Git files,
twelve controls, eighteen fixed cases/54 independent contrasts and 36 moving
cases. All 7,614 physical endpoints and 32,561 accepted reaction nodes reconcile;
ledger errors are below 9.33e-15. Fixed state/stress errors are at most 2.37e-12
and 4.10e-7 Pa. The one-file review is D: verified; no model was rerun.
Moving full-distribution discrepancy is still up to 0.01497 (about 3.37% of
the attached population in the worst shape case); force discrepancy reaches
20.92 Pa across tested time steps. Doubling the domain barely changes these
short trials. Next establish an independent continuous-movement reference and
its discretization accuracy; do not tune biology or promote the finest sampled
step on self-agreement alone. See the completed coupled-chemistry document.
Only receptor cubic `94d05d` remains pending; do not inspect it without its own
operator completion report. The full goal is active and unfinished.

The operator has now reported receptor cubic `94d05d` complete. Review
`receptor-cubic-review-20261117` verifies 2,560 C:/D: files, 1,715 Git files,
124 focused cases, five exact legacy source regressions and eighteen byte-exact
original accepted-state trajectories. All 27 cubic event predictions match
exactly; all eighteen candidate/reference, eighteen independent-reference and
twelve original refinement checks pass without changed bounds. The 36 source/
Avatar receiver cases reproduce their saved audits. The one-file review is D:
verified; no model/root/test reran. The original `f7ece6` remains FAILED.
There are now NO pending operator reports. See the completed cubic comparison
document and `docs/DNNE_Neuronal_Motor_Input_Review_2026-11-17.md` for the next
source-ownership gap: actual upstream DNNE events, explicit completeness and
faithful per-contact semantics are still required. Do not promote the isolated
reference cells or infer whole-brain authority/physiology from this numerical
pass. The spindle's independent moving reference also remains outstanding.

The actual DNNE rubral source/isolated motor rig is now authored; see
`docs/DNNE_Rubral_Motor_Connection_2026-11-17.md`. A private fresh 256-cell existing
Izhikevich/STDP RedNucleus emits original messages, with acknowledged immutable
frames and explicit completeness, into the preserved cubic receptor/motor source
and actual Avatar receiver. Four source conditions each feed two packet cadences
and a contact-disabled branch; prospective focused inventory is 136 cases.
Source substep timing, fixed unit receptor efficacy and reference bath remain
explicit approximations. Preserve all original payloads, failed prefixes and
pending events beyond the proven horizon; never invent silence to empty queues.
No live host/Avatar/editor selector changed, and the upstream source's numerical
accuracy, physiology and learned efficacy are not qualified by this connection.
Build/syntax checks are not execution. Freeze clean pushed source and D: backups,
launch visibly and await the operator; no live monitoring. A completed accounting
study with ObservedNeuronalMotorResponse=false is not motor-authority success.

The rubral experiment is now launched as `rubral-motor-20261117-e42efc`, frozen
at `4cffbcc2a05e8e8233131c01c7c22b7e0975fee9`, visible task
`DNNE-Rubral-Motor-f79577aed07e`. Its 1,734 initial files plus manifest are D:
verified. Authoring builds and syntax/inventory checks completed without inline
model/test execution. This is the only pending operator report. No post-launch
output/status/process has been inspected; do not monitor it. The actual source
uses 0.25/1/4 fixture quanta, identical anatomical/input identities across fresh
conditions, and original source substep times. Full payloads, terminal pending
events and disabled-contact evidence must be reviewed after completion. The
moving spindle reference and full neuronal brain/body/world goal remain open.

The operator reported rubral `e42efc` FAILED. The D:-verified
`rubral-output-failure-review-20261117` verifies 2,294 files per copy, all 136
focused cases and the complete quiet prefix: 12,000 source frames, three
15,001-state motor branches, zero events and byte-identical trajectories.
None of the stimulated conditions executed. The failure was `File.OpenRead`
attempting a checksum while its trajectory writer was still open. Close all
completed branch writers before hashing; preserve the old FAILED capture and
all numerical/model parameters. The correction builds without warnings/errors;
it has not executed inline. No report remains pending until the fresh replacement
is launched. Independent moving-spindle work remains open; do not infer motor
authority from the quiet prefix or change physiological parameters for this I/O bug.

The corrected rubral replacement is now launched as
`rubral-motor-20261117-844975`, revision
`621109a7604c95bd4278a5a9c2efe779a41be2fb`, visible task
`DNNE-Rubral-Motor-5b35bbfc07b0`. The 1,735 initial files plus manifest are D:
verified. This is the only pending operator report. No post-launch output/status/
process was inspected; do not monitor it. Old `e42efc` is already reported and
reviewed, not another pending result. The full goal remains active and unfinished.

The independent continuous-motion spindle reference candidate is authored in
`tools/spindle_eulerian_reference.py`; see
`docs/DNNE_Spindle_Continuous_Reference_2026-11-17.md`. It evolves fixed-grid
cell masses with simultaneous upwind transport, attachment/detachment and actin
kinetics, preserving the same piecewise biological rate surface and absorbing
boundary accounting. Upwind diffusion is explicitly unresolved: require its
own temporal/spatial/range comparisons, never label it exact by construction.
The visible rig has twelve controls, six fixed reductions/18 saved contrasts,
42 moving solves, 12 temporal/18 spatial/six range contrasts, and 18 comparisons
with saved retained-interval candidates. No candidate rerun or live selection
change. Syntax checks are not numerical execution. Freeze clean pushed source
and D: backups before launch. Report reference discretization separately from
diagnostic completion; candidate accuracy, sensory physiology and recovery stay
unqualified. Rubral replacement `844975` remains pending and must not be monitored.

The continuous spindle study is now launched as
`spindle-eulerian-20261117-f0020c`, revision
`857518d9048c0c50a81d43540e7f95cac461bb8a`, visible task
`DNNE-Spindle-Eulerian-b17aecc18cf3`. All 1,859 initial files plus manifest are
D: verified. Preparation checked source/evidence/dependency metadata only;
Python/PowerShell syntax checks executed no numerical work. TWO reports are
pending: this spindle study and rubral replacement `844975`. Neither window's
post-launch output/status/process has been inspected. Do not monitor them or
infer completion from elapsed time. Assess the reference's full spatial and
temporal errors before interpreting candidate differences or physiological
mechanisms; preserve all historical bounds and failures. The full goal remains
active and unfinished.

An isolated physical-clock vestibular mechanics candidate is now authored in
`src/NRE.SimAvatar/AvatarClockedVestibularMechanics.cs`; see
`docs/DNNE_Clocked_Vestibular_Mechanics_2026-11-17.md` and the updated Avatar
migration audit. It preserves existing reduced constants/axes but removes
invented initial elapsed time, applies a causal previous-endpoint hold, solves
the stated coupled linear filters analytically, and rejects gaps/replays that
would reset state. Its Cupula coordinate has rad/s units, not displacement;
the observation hash does not validate the skull transformation. No hair-cell
membrane, neural firing, live host or editor selector is added. Sixteen tests
are authored and the Release test project builds cleanly, but NONE of those
new tests or comparisons has run. Prepare an independent numerical comparison
in a future visible, frozen, D:-backed window before considering promotion.
No third study has launched. Rubral `844975` and spindle `f0020c` remain the two
pending operator reports; do not read their post-launch output/status/process.
No paused goal or completion is implied; the full usability goal stays active.

The operator has now reported BOTH rubral `844975` and continuous spindle
`f0020c` complete. Their separate reviews are D: verified. Rubral: 2,360 files
per copy, 136 cases, 48,000 frames and twelve branches reconcile; active source
event counts are 261/1178/5710, but zero events reach the selected motor target.
The independently D:-verified routing review shows 256 input contacts reach only
172 cells; 84 receive no input. Named target n-081 actually routes to n-034,
and selected source n-081 has no receiving contact or synaptic current. All
three strengths retain the same recipient mapping. This was a fixture targeting
assumption error, not demonstrated structural plasticity or motor-cell failure.
See `docs/DNNE_Rubral_Input_Recipients_2026-11-17.md`. The operator explicitly
accepts simpler mechanisms that remain close to biology: each synaptic contact
should have an explicit receiving neuron while ordinary strength learning stays
local; structural rewiring must be a distinct, explicit anatomy change.

Spindle: 2,184 files per copy, 9,168 sampled endpoints and 47,179 accepted nodes
reconcile. Temporal and domain-range contrasts pass, all six finest spatial
pairs fail: population L1 up to .00483166 and stress 44.9948 Pa, with unchanged
1e-5/.01 bounds. This first-order upwind reference is not accurate enough to
qualify the retained-interval candidate. Improve spatial transport rather than
tightening the ODE or changing physiology/bounds. See the completed continuous
results document. No new spindle model has run.

The next rubral candidate reuses IsolatedSynapticTargets for a fresh full-size
RedNucleus. Default/live routing and biological equations remain unchanged.
Its owner rejects missing/contradictory contact anatomy and retains actual
original-event/contact/cell/arrival/application receipts through acknowledgement
or failure. The four-condition declared rig keeps the original fixture identities,
stimulation, selected output target and motor parameters, requires all 15,360
active inputs to reach their exact 256 recipients, and compares complete quiet
observations with the original capture. Authoring builds pass; 150 focused cases
are prospective and must run only in a new visible frozen D:-backed window.
No result is pending until that launch is recorded. Never imply live routing,
upstream physiological calibration or Avatar recovery is repaired by the fixture.

The vestibular comparison draft is saved in the probe and three Python tools;
its visible launcher remains unauthored, preparation deliberately refuses it,
and no vestibular experiment ran. Finish/review it later; the operator's completed
results and explicit wiring simplification took priority. No pending reports
remain from the old two windows. The full goal remains active and unfinished.

The declared-recipient rubral comparison is now launched as
`rubral-declared-20261117-3e3db0`, revision
`149e5ea778878dded50bb8d867292c9f5a367a6d`, visible task
`DNNE-Rubral-Declared-35778cc76b5f`. All 1,755 initial files plus manifest are
D: verified. Test/probe builds and syntax/source-inventory checks passed without
inline execution. This is the ONE pending operator report. No post-launch
output/status/process has been inspected; do not monitor it. The vestibular
comparison draft has no launcher and remains unlaunched. The next spindle work
must address spatial transport error; the current upwind reference is unqualified.
No whole-brain, physiological or Avatar usability success is implied.

The vestibular comparison draft now has its visible launcher,
`tools/start-vestibular-numerics-window.ps1`. It runs the existing prospective
62-case inventory, six controls, twenty histories/two native delivery groups,
and matrix-exponential plus two ODE references. All 60 reference, 60 candidate
and twelve subdivision contrasts retain their original numerical bounds.
Source/executable/build/runtime hashes, accepted nodes, complete inputs/receipts
and failure prefixes are preserved with final D: verification. The unchanged
mechanical candidate is still isolated; no model/test has executed inline.
Freeze/push/backup before launch and await the operator's report without polling.
Rubral declared `3e3db0` remains pending; no output/status/process was inspected.

The operator reported declared rubral `3e3db0` complete, then paused for a harness
update and explicitly resumed the goal. Its completed saved-data review now
verifies 2,384 files per C:/D: copy, 150 tests, all 15,360 literal input deliveries,
48,000 frames and twelve motor branches. n-081 emits five/23 original spikes in
the two stronger conditions; all reach the motor receptor. Sampled soma response
is 1.22035/2.86817 mV above the disconnected control, but no motor spike occurs.
This is subthreshold excitation, not zero electrical response. The one-file
rubral-declared-review-20261117 is D: verified; no model/test was rerun in review.
All prior reports are reviewed. The operator requested TEN converging neuronal
inputs, interpreted explicitly as ten distinct sources with unchanged per-contact
strength and single-input/disconnected controls. Declare anatomy before firing;
preserve original emitted events and old captures. No live projection is changed.
The vestibular launcher Python/PowerShell syntax and 62-case static inventory
passed; it remains unlaunched until a clean pushed frozen package is backed up.

Vestibular `f21f4b` launched from 36df4f0b5425319d086a1de9a1becfafd8304563,
visible task DNNE-Vestibular-Numerics-8ef9874a48a1, with 1,755 initial files D:
verified. No monitoring occurred. The operator has now reported completion
(twice, the same capture): 20 conditions/40 native/60 references, 2,506 final
files. Its saved-data review is pending and is authorized by that report.

The user requested ten distinct converging neuronal inputs. The fresh isolated
projection fixes n-081..n-090 to original spinal cell 2 before activity; preserves
the original n-081 contact and all unrelated routes; fingerprints the anatomy;
and emits original correctly addressed messages. No live map or biological
parameter changes. Four source conditions now have four branches (16 total):
ten inputs with packets1/17, disconnected and the original single contact.
The audit requires preserved original source observations/firing times and
byte-identical original single-contact trajectories, 50 nS per contact and full
arrival/receiver accounting. No firing threshold/gain is tuned for a pass.
Five new cases plus the 150 existing cases are prospective. Static inventory,
Python/PowerShell syntax and both authoring builds pass; no tests/models run
inline. Freeze/push/D-backup and launch visibly before awaiting the operator.

Ten-input experiment launched as rubral-ten-20261117-0419e8, revision
9621023d3cd2569fe5cf355b5091f4bfbb5baa30, visible task
DNNE-Rubral-Ten-c3f078c97378. All 1,770 initial files plus manifest are D:
verified. This is the ONE pending report; do not inspect post-launch outputs,
status or process until the operator reports completion. The 155 cases and
actual motor response remain unassessed; no live host/Unity session was started.

Vestibular f21f4b is now fully reviewed: all 2,506 C:/D: files, frozen Git,
binaries/runtime/dependencies, 62 tests, 7,280 original records and 5,549 accepted
ODE nodes reconcile. The saved analysis reproduces exactly with numerical
execution functions disabled. All 60 reference/60 candidate/12 subdivision
contrasts pass unchanged bounds; maxima 1.82058e-11/1.81684e-11/1.45717e-14.
The separate vestibular-numerics-review-20261117 is D: verified. This supports
only tested isolated reduced mechanics/held-input timing, not head transforms,
hair cells, afferent firing, physiology or Avatar balance. Both rubral-declared
3e3db0 and vestibular f21f4b are completed/reviewed, not pending windows.
The full DNNE/Avatar/World goal remains active and unfinished.

Independent work while ten-input 0419e8 awaits the operator: the head-coordinate
candidate is in AvatarSkullKinematics plus a dormant Unity SkullMotionCapture
helper. Existing Avatar/Unity/body/transducer code was inspected first. Differentiate
actual material-point world velocities before projecting into measured skull axes;
do not mistake rotating pelvis components for inertial acceleration. First-sample
acceleration is unavailable, so optional later mechanics initialization retains
the SAME original clock, with a distinct version/fingerprint and no invented time.
Default mechanics must reproduce the reviewed f21f4b bytes. See the skull document
and migration audit. Twelve new cases plus the original 62 form a prospective
74-case visible regression; all 61 original default output files must match exactly.
No new tests/models run inline; authoring builds and static syntax/inventory pass.
The Unity helper has not been built/executed and native head sampling/afferent
physiology/balance are unqualified. No live/editor activation or old schema change.
The rubral-ten capture remains pending and must not be monitored.

Skull component/default regression launched as skull-kinematics-20261117-f740bd,
revision 5aa5f28c2fc0a2252c64a02defa098902a141e26, visible task
DNNE-Skull-Kinematics-92195589d752. All 1,826 initial files plus manifest are D:
verified. The 74-case static inventory, Python/PowerShell syntax and .NET test/
probe builds pass; no tests/models were executed inline. TWO reports now remain:
this skull run and rubral-ten-20261117-0419e8. No post-launch outputs/status/
processes were read. Native Unity head capture remains a future isolated physical-
owner qualification; the helper has no live caller and was not built/executed.
Do not inspect pending captures without their operator reports, or equate a
component pass with afferent physiology, live brain authority or Avatar recovery.

Both reported Nov17 captures are now reviewed. Skull f740bd: all 2,430 C:/D:
files, frozen source/binaries/runtime, 74 exact cases and all 61 original default
vestibular files reconcile. Its separate skull-kinematics-review-20261117 is D:
verified. Ten-input rubral 0419e8: 2,423 files, 155 cases, 48,000 original frames
and 16 branches reconcile. Quiet/quarter produce no motor spikes; unit gives one,
four gives twelve; single-contact and disconnected controls remain silent.
Packet cadences and the original single-contact trajectories match exactly.
Individual strength remains 50 nS. This is isolated neuronal summation/output,
not calibrated anatomy, learned efficacy, live authority or Avatar control.
Use rubral-ten-review-20261117-v2: the first review's auxiliary population summary
was overwritten by traversal of the copied baseline; v2 restricts summaries to
the current capture. Both are preserved; the core saved analysis was unchanged.

Next native skull candidate: optional separate sidecars in the existing isolated
physical-owner bridge, actual head centre and +/-0.0625 m local-X points, 603
adapter receipts, independent quaternion/point-velocity/projection audit and
preserved pause/backpressure/failure checks. No live or editor selection. The
reviewed component gate is a preparation prerequisite; 74 tests and Unity must
execute only in a fresh frozen, D:-verified visible PowerShell window. No native
outcome is known before the operator report. Prospective bounds and the exact
CoreCLR subset dependency check are in the skull document/native_skull_gate.py.
Continuous-motion accuracy, point anatomy, afferent firing and balance remain open.

Native head study launched: unity-skull-20261117-733e7c, source revision
8734689b82aee169dd346911e3bb354b0e8dcda8, visible task
DNNE-Unity-Skull-c31b288fda04. Initial 1,770 files plus manifest are D: verified.
Authoring build/syntax/static 74-case inventory passed; tests and native execution
belong to the independent window only. This is the ONE pending operator report.
Do not inspect its outputs/status/process until reported complete. Both previous
skull f740bd and rubral-ten 0419e8 are complete/reviewed; the authoritative ten-input
v2 review and preserved earlier review are separately D: backed. The full goal
remains active, with actual muscle/body use of the new motor output still open.

Operator reported native skull 733e7c FAILED. All 3,403 C:/D: files, frozen/built
source/player, 74 tests and unchanged managed runtime are reviewed. It failed the
lossyScale guard after physical step 178; 178 endpoints/534 point receipts survived
through step 177. The failed scale/ancestor scales were not saved. Retained
coordinate/velocity errors are inside the unchanged bounds; float composition is
plausible, not proven for the missing endpoint. No pending window remains here.
The v2 repair records head/chest/pelvis local scales, requires each exactly one,
and preserves computed lossyScale plus old-guard outcome. This changes the scale
acceptance policy explicitly; it does not declare the old guard passed. Thirty-
three native hierarchy controls must reject all eighteen actual non-unit cases.
No body dynamics, coordinates or accuracy bounds change. Review is
native-skull-failure-review-20261117; the retry requires it D: verified and the
physical sources unchanged. Compile/static checks only until visible launch.
Motor-to-body continuation remains next; this turn inspected its existing Avatar
and native bridge implementations but has not authored or launched that study.

Replacement skull capture launched: unity-skull-20261117-1e2f21, revision
3c385fcb04f39a4eacb68dbd3d401b32c316c37f, visible task
DNNE-Unity-Skull-3bad563dbfa3. Initial 1,771 files plus manifest are D: verified.
No post-launch output/status/process inspected. This is the ONE pending report;
do not inspect it until the operator reports completion. Native hierarchy controls
and sampling remain unqualified. Failed 733e7c and its separate review are preserved
and D: verified. Main goal stays active and unfinished.

Independent motor continuation while skull 1e2f21 awaits the operator: inspected
the existing observed-neuron/native body, Avatar receiver/causal muscle plant and
native hinge implementations. New isolated RubralMotorCircuit owns the actual
ten-input source and receptor motor cell together, retains quiet priming, binds
an explicit body epoch and holds both neurons on unacknowledged output. No live
host or biological model changes. Its source gate compares ten fresh conditions
against the completed authoritative ten-input capture: 120,000 source frames,
150,010 motor CSV rows and all original events; 161 focused cases are prospective.
See DNNE_Rubral_Body_Ownership_2026-11-17.md. No model/test executes inline; use a
fresh frozen D:-verified visible window and await its operator report. Native
muscle execution, sensory return and recovery remain later gates. Never inspect
the pending head retry as part of motor preparation.

Rubral circuit/body-clock gate launched as rubral-circuit-20261117-7fc785,
revision fb9fb5aad960c7c5c203759efc993828a0c1119f, visible task
DNNE-Rubral-Circuit-2f631e84a0fd. Initial 1,796 files plus manifest D: verified.
Both authoring builds passed with zero warnings/errors, plus static syntax and
161-case inventory; tests/model comparisons execute only in the new window.
TWO reports are pending: this gate and native skull retry unity-skull-20261117-
1e2f21. No post-launch output/status/process inspected for either. Await their
operator reports; preserve previous captures. The next motor stage is original
rubral events through the existing muscle/native hinge path once ownership is
qualified. No moving muscles, live authority, physiology or recovery claim yet.

While both reports remain pending, authored RubralNativeBodyStudy and a complete
RubralCircuitTrace recorder plus analyse-rubral-native-body.py. Seven prospective
conditions carry fresh original rubral/receptor motor events through the existing
Avatar calcium/dynamic-muscle/causal plant into actual native point forces. The
opposing source is an actually integrated quiet neuron. Preserve the full 1500 ms
neural horizon; 7999 unchanged fine native steps stop below body 500 ms, before the
next step would exceed source coverage. The independent audit checks all original
source/arrival data, start-state tendon forces, native impulses, packet/retry
invariance and 15 continuous reference solves under existing bounds. All remain
UNEXECUTED. Release probe build is clean; Python syntax only was parsed. No pending
run output/status/process was read. See DNNE_Rubral_Native_Muscles_2026-11-17.md.
Before launch, review the operator-reported 7fc785 gate and finish frozen packaging,
combined exact focused-test inventory, dependency/core preservation and visible
PowerShell pipeline with D: verification. Current CLI gate checks do not replace
that missing packaging/provenance work. Both pending reports remain unchanged.

The pending native muscle packaging is now authored: review-rubral-circuit.py
requires an explicit operator-complete flag and verifies all C:/D: evidence,
Git/binaries, exact tests and reproduced saved audit without model execution.
rubral_native_gate.py prepare requires that NEW D:-verified passing review, clean
pushed source and preserved neuronal/body/reference sources. It freezes original
references and verifies numerical dependencies. start-rubral-native-window.ps1
then verifies initial backup, builds/checks managed runtime, runs the exact 281
cases (21 classes), builds a separate Unity player, executes seven native trials
and fifteen independent references, checks all endpoints and backs up even on
failure. Syntax/static inventory pass; all 20 reused body/reference sources were
read against the already reported bb10dc capture and remain unchanged. The only
decoder extension is the documented explicit 7999-step acceptance; old defaults
are unchanged. NO reviewer, preparation, test, model or launcher was executed
for pending runs. No pending outputs/status/processes were inspected. Still only
skull 1e2f21 and circuit 7fc785 await the operator. Full goal remains active.

The operator has now reported circuit 7fc785 complete. All 2,451 C:/D: files,
1,769 Git source files, binaries/runtime, 161 exact tests and the full saved audit
reconcile. Ten conditions preserve 120,000 source frames and 150,010 motor CSV
rows. Unit/strong produce 1/12 motor events at both cadences; quiet/quarter/cut
produce zero. Deliberate blocked advances/retries preserve state and once-only
delivery. Separate rubral-circuit-review-20261117 is D: verified. No model/test
rerun during review. Native muscle packaging may now proceed from clean pushed
source. Skull 1e2f21 is still pending; no output/status/process inspected.

Independent vestibular source review: DNNE_Vestibular_Equation_Audit_2026-11-17.md
records visual PDF/XML discrepancies, disabled adaptation/quantal branches and
temperature/species limits. Both pinned template and original Draft 12 Roles
models have 279 scoped definitions; 15 lexical differences do not establish
model equivalence. The static inspector executes no source equations. The
48-file vestibular-calyx-equations-20261117 artifact is D: verified, with all
32 page renders (only pages 2, 8–22, 26 visually reviewed), source hashes, original
paper model/license, full selected XML sections and immutable review snapshots.
No model/test/new sensory experiment or live implementation ran. Do not mix
printed and stored channel equations or assume function defaults were resolved.

Native preparation at c192c8c stopped before package creation: the primary
Python executable/python3.dll/python312.dll hashes changed, although versions
and all 2,433 other dependency hashes match. Five-file metadata evidence in
rubral-native-preparation-20261117 is D: verified; no test/model ran. The native
packager now requires an explicit --environment-review for this transition,
preserving historical inventory and checking the complete declared new inventory
before/after the run. No relaxed version-only comparison. Ten transition-control
fixtures and unchanged 49 geometry/six mechanical/three cross-method controls
must run in the visible window before native work; both full-reference solve
and analysis require their evidence. All original biological/numerical bounds
and reference/core sources remain unchanged. See native-muscle doc; preparation
must use clean pushed source and the D:-verified declaration. Skull 1e2f21 is
still the only pending report until the new native package is actually launched.

Native rubral/muscle trial is now launched as rubral-native-20261117-4cdba5,
revision 827f04554fb61191ae2cf489b3aa54618f76c4c0, visible task
DNNE-Rubral-Native-149a80212e4b. Initial 1,819 files plus manifest D: verified.
Preparation/static syntax passed; all controls, 281 tests, seven native trials
and fifteen full reference integrations belong to the independent window.
No post-launch output/status/process inspected. TWO reports now await the
operator: this run and native skull retry 1e2f21. Circuit 7fc785 is completed,
fully reviewed and no longer pending. Full goal remains active and unfinished;
source-preserving clock attachment is qualified, moving-muscle numerical/physical
outcomes are not yet known. Preserve all failed packages and old saves.

The next isolated sensory candidate is AvatarVestibularHairCell, documented in
DNNE_Vestibular_Hair_Cell_2026-11-17.md. It independently expresses the reconciled
MET/K,L/Ca/HCN1/pump subset of the pinned source, with explicit fixed bath and
room-temperature kinetics. Physical displacement is in nm; the existing cupula
output is rad/s and cannot be passed through as though equivalent. No live
Avatar/editor/calyx/afferent selection or generated sensory spike is introduced.
Clocked RK4 preserves local integer steps, channel probabilities without clipping,
ionic charge ledgers and rejected failure candidates. Gate-conditioned initial
state is NOT a settled electrical equilibrium. No checkpoint loader exists.

The complete prospective rig is authored: 47 exact tests, 7,260 clamp arithmetic
cases, six histories at three native steps, twelve independent controls and
eighteen DOP853/tighter/Radau references. Preserve every accepted node, original
input boundary, charge state and prospective bound. Radau has an explicit
complex-step Jacobian rather than numerical perturbation growth in charge
columns. Reference comparisons cover every state; refinement cannot be inferred
from host partition invariance. Preparation freezes current runtime as an
explicit new baseline; it never claims equivalence to older Python binaries.
Original source/licensing evidence and old Avatar paths are D:-verified and
preserved. Both authoring builds, syntax and static inventory pass, but no new
model/test has executed inline. Freeze/push/back up before the visible window;
inspect only after the operator reports completion. The native skull 1e2f21 and
rubral-native 4cdba5 outputs/status/processes remain untouched and pending.

Hair-cell study is now frozen/launched as vestibular-hair-cell-20261117-959475,
revision 59b586bdb85a190797b077a9644b2e88496012f0, visible task
DNNE-Hair-Cell-47ad90abec01. Initial 1,801 files plus manifest D: verified.
Preparation ran metadata/static checks only; all models/tests belong to that
independent window. No post-launch outputs/status/processes inspected. THREE
reports now await the operator: hair cell 959475, native skull retry 1e2f21,
rubral/native muscle 4cdba5. Circuit 7fc785 remains completed and reviewed.
Do not poll these windows. The full goal remains active and unfinished.

The operator reported hair-cell 959475 complete. Full review now verifies all
2,472 files per C:/D: copy, 1,787 Git files, binaries/runtime, 47 exact cases and
the saved comparison without ODE/test execution. Eighteen native captures and
eighteen references (103,366 accepted nodes) pass all original bounds/refinements.
Maximum finest voltage error is 2.0021e-6 mV; charge ledger is 1.1801e-9 fC.
Separate vestibular-hair-cell-review-20261117 is D: verified. See result document.
This fixed-bath receptor component is numerically qualified, not a live afferent.

The next isolated AvatarCalyxKernel and complete visible study are authored.
Separate inner/outer voltages and gates retain Kv7.4, HCN2, pumps, inner KCC and
outer unassigned leak. Choose the deposited model's 3.7 nS HCN2/sign convention
explicitly; do not call it printed-supplement equivalence. Preserve its literal
outer reversal constants and room-temperature/fixed-reservoir assumptions.
KCC cancels electrically but its K/Cl flux must remain visible. No free voltage,
shared cleft, axon or event generator exists in this kernel. Before coupled use,
review 23 tests, 15,984 independent current cases and 224 analytic voltage clamps.
Both authoring builds and static syntax/inventory pass; no new calyx test/model
executed inline. Preparation requires the reviewed/D:-verified hair-cell pass,
clean pushed source and initial backup. See DNNE_Calyx_Membranes_2026-11-17.md.

The operator provided progress text for rubral-native 4cdba5 and explicitly said
IT HAS NOT FINISHED. Do not inspect its outputs/status/process. Native skull
retry 1e2f21 also still awaits a completion report. Hair-cell 959475 is completed
and reviewed, no longer pending. No calyx window is launched until preparation
and backup are finished. Full DNNE/Avatar/World goal remains active.

Calyx study now launched as calyx-membrane-20261117-920328, frozen revision
635508a1d6d8dcaba156004d50d3e67e620b9acd, visible task
DNNE-Calyx-Membrane-ed9d3e8d93fa. Initial 1,812 files plus manifest D: verified.
No post-launch output/status/process inspected. THREE reports now await the
operator: calyx 920328, native skull 1e2f21, rubral-native 4cdba5. The operator
explicitly confirmed the rubral run still running; do not infer completion
from the seven printed condition names. Hair-cell 959475 is fully reviewed.
The next sensory implementation requires shared cleft/membrane charge balance
and actual afferent dynamics; do not promote current/clamp success to that scope.

The operator has now reported the final FAILED footer/D: backup for rubral-native
4cdba5. Saved status records 281 passing focused tests, successful native driver
and seven cases/55,993 steps. The comparison failed before any independent solve:
native_audit reused the historical observed-body loader, which rejects the already
declared three-Python-binary transition accepted by the rubral preflight. Preserve
the historical loader and capture. New rubral-specific load_reference reuses the
strict declared-transition preflight/control checks; equations/bounds are unchanged.
See DNNE_Rubral_Native_Muscles_2026-11-17.md. A separate reference-only continuation
verifies all 360,428 C:/D: files, Git/binaries/tests before reading original native
data, tests the actual repaired loader and old failure, then solves fifteen missing
references. Full preservation is reverified afterward. All controls/solves run in
its visible PowerShell window, no live monitoring. No native rerun or candidate
change is needed. Until launch the continuation is authored only. Calyx 920328 and
native skull 1e2f21 still await reports; do not inspect them. Full goal is unfinished.

Reference-only continuation launched: rubral-comparison-20261117-a6b77a, revision
509c3b8400a54ec43a51f535990d916b2a7aa47f, visible task
DNNE-Rubral-Comparison-fccfb9b34200. Initial 1,846 files plus manifest D: verified.
No post-launch outputs/status/process read. Await this report, calyx 920328 and
native skull 1e2f21. Original native 4cdba5 is completed/failed and preserved;
its complete C:/D: review runs in the continuation window before any references.

Independent work while these three reports await the operator: authored pure
AvatarCleftEdgeKernel and AvatarCleftChargeBalance with prospective conservation,
direction, equilibrium, capacitance and energy tests. No new test/model/window
or pending output inspection. Source material uses K/Na drift/diffusion plus
600 nS/um other-ion conductivity; do not double-count the K/Na contribution by
adding the full 1,000 value. Explicit face/volume geometry is not anatomical
calibration. Hair apical endolymph and exposed perilymph potentials/charges stay
separate; simultaneous local capacitance is required. No time integration,
spatial assembly, live/editor caller or afferent event source is introduced.
See DNNE_Cleft_Coupling_2026-11-17.md. Review calyx 920328 before coupled use;
next author independent high-precision capture/reference, then spatial assembly
and coupled trajectory qualification. Goal remains active and unfinished.

Cleft constitutive authoring now includes 25 component cases (26 with solution
manifest), CleftCouplingStudy/--cleft-coupling native capture, 468 prospective
edge inputs and 60 charge inputs. Independent Decimal reference uses direct
exponentials and full five-node capacitance graph/pivoted solve at 60/90 digits,
retaining every expected value with original prospective bounds. Test/probe
authoring builds both pass with zero warnings/errors; Python syntax/static
inventory pass. NO test, candidate evaluation, reference or new window ran.
Frozen preparation/visible pipeline still need authoring after the calyx report.
Do not mistake pure constitutive equations for a spatial/time-integrated receptor
or emitted sensory afferent. All three pending reports remain untouched.

Cleft packaging/review continuation is now authored. review-calyx-membrane.py
requires the operator-completed flag and full C:/D:/Git/binary/runtime/test
verification, then reproduces the original summary from all saved independent
current/clamp values without evaluating channels or clamps. Its NEW D:-verified
review is required by prepare-cleft-coupling.py, which preserves existing Avatar/
hair/calyx sources and requires exact reviewed runtime equality. The frozen
start-cleft-coupling-window.ps1 owns 26 tests, 468 edge/60 capacitance captures,
60/90-digit references and final backups. Syntax/static inventories pass; none
of the reviewer, preparer, launcher, tests or equations has run. Calyx 920328's
completion is requested asynchronously; no answer yet. Rubral comparison a6b77a
and skull 1e2f21 also await reports. Do not inspect any pending outputs/processes.
The full goal remains active; spatial receptor/afferent integration is unfinished.

Next independent authoring adds AvatarCleftSpatialCoupling: immutable named
compartments/edges, conservative internal K/Na transfer, calyx axial current and
one shared hair-cell capacitance solve. Actual membrane currents are explicit
ports; no time/gate integration or live sensory selection exists. Open-apex bath
boundary/ion ledger and anatomical mesh still need implementation/qualification.
No duplicate apical capacitance, invented afferent spike or geometry fit.
The unlaunched cleft protocol is explicitly v2: old 468 edge/60 local vectors and
all bounds unchanged, plus 96 spatial inputs and 14 unit cases (40 total with
manifest). Independent 60/90-digit reference builds the entire capacitor graph
and uses dense elimination. Native capture, preparer and visible pipeline now
require this full scope. Test/probe authoring builds pass with zero warnings/
errors; static input preservation, inventories and syntax pass. No test/model/
reference/launcher executed; no pending capture inspected. Calyx 920328 report
remains requested but unanswered; rubral a6b77a and skull 1e2f21 also await reports.

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

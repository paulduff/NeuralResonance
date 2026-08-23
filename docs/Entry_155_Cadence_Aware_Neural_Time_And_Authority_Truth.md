# Entry 155 - Cadence-Aware Neural Time and Authority Truth

Date: 2026-08-22

## Status

Implemented and deterministically verified. The full DNNE stack remains
gracefully stopped. A fresh-network live acceptance run is still required; no
movement or authority claim is made from deterministic tests alone.

## Triggering evidence

The primary report was:

`C:\Users\User\AppData\Local\NeuralResonanceEngine\world-runs\world-run-92b05f02fb3f415dbf565f2947b48446-0000266373-stopped-20260822T155133630Z.json`

The predators-suspended HandSpace run lasted 8,822.769 seconds, or about 2
hours 27 minutes. It completed 266,373 world ticks with no world tick failures.
The physical loop remained viable and accepted 156,891 body frames and
1,255,028 somatic frames.

The behavioral result was nevertheless complete voluntary silence:

- distance travelled: 0 m;
- terrain cells visited: 1;
- locomotor dispatches: 0;
- interaction attempts and successes: 0;
- food consumed: 0;
- seven metabolic deaths and respawns;
- head-yaw, head-pitch, and stand populations remained active, but every
  articulated voluntary locomotor and limb channel remained at zero.

Thirty-five somatic requests required a retry. Thirty-one recovered, while
four exhausted the old two-second HTTP timeout and were correctly retained as
body-input failures. The final report could show the final action state, but it
could not prove whether a brief striatal recruitment or authority event had
occurred earlier in the run.

## Root cause

The stable laptop scheduler intentionally load-sheds structure services. A
fast-lane structure is selected periodically rather than on every one
millisecond coordinator pass. Before this entry, the selected service still
received a one-millisecond integration duration. Biological time skipped while
that service waited was therefore discarded.

This disproportionately harmed membrane mechanisms that require temporal
integration. A striatal MSN selected once every three coordinator passes
integrated only one millisecond of dynamics instead of the approximately three
milliseconds that had elapsed. The convergent arbor from Entry 154 was present,
but its membrane and up-state evolution ran on a shortened local clock.

This was not a reason to increase receptor gain. A gain increase would tune the
model to a scheduler artifact and would behave differently on the RTX machine
or cluster.

## Cadence-aware repair

The coordinator now maintains a successful integration timestamp for every
service instance:

1. A selected service receives the actual elapsed simulation time since its
   previous successful step.
2. A failed request does not consume that elapsed biological interval.
3. Recovery is bounded to 100 ms so a long outage cannot cause an unbounded
   numerical jump.
4. Each structure divides elapsed time into integration substeps of at most
   four milliseconds.
5. Intrinsic drive, membrane evolution, spiking, and local reflex output run on
   every substep; diagnostics are sampled after the final substep.

The scheduler still decides when CPU work is affordable. It no longer changes
how much biological time has passed for a selected neuronal population.

## Striatal convergence proof

The sparse acceptance assay uses a one-millisecond global clock, selects
Striatum once every three coordinator ticks, and supplies one rotating
corticostriatal spike every four milliseconds. Both D1 and D2 populations must
develop non-zero synaptic current, up-state, activation, and emitted activity.

A separate topology assay proves that independent cortical axons:

- preserve their action lane;
- preserve their D1 or D2 receptor class;
- terminate on distinct primary MSNs;
- overlap on at least one MSN in the same convergent ensemble.

No host action preference, scripted command, ML policy, or authority bypass was
introduced.

## Authority history

Action authority is now accumulated across the complete brain run rather than
being represented only by the terminal frame. The live frame, brain snapshot,
and world-run report retain:

- sampled and circuit-observed ticks;
- total authority-granted ticks and distinct grant episodes;
- first and last grant ticks;
- per-channel selected and granted tick counts;
- peak proposal, D1, D2, hyperdirect, thalamic, and score values;
- minimum output-nucleus inhibition;
- peak active D1/D2 neuron counts and up-state.

Authority is counted from the basal-ganglia channel trace's own
`AuthorityGranted` evidence. Generic motor activity, head orienting, righting,
or withdrawal cannot be mistaken for voluntary authority. World-run schema
`dnne.world-run.v8` carries the latest cumulative brain record into the final
physical report.

## Somatic resilience

The somatic client timeout is increased from two to five seconds. The retry
boundary remains explicit:

- a transient first failure increments retry telemetry;
- a successful retry increments recovery telemetry and is not counted as a
  rejected body frame;
- only an exhausted retry or explicit rejection increments somatic rejection
  and body-input failure.

This changes transport tolerance only. It does not synthesize contact, alter
pain, or hide a genuinely lost afferent frame.

## Deterministic acceptance

The focused Entry 155 suite passes 15/15 tests. It covers:

- elapsed biological time across skipped scheduler selections;
- failed-step recovery without consuming elapsed time;
- bounded catch-up after a long interruption;
- sparse live-cadence D1 and D2 recruitment;
- independent corticostriatal convergence;
- transient authority grant and episode preservation;
- duplicate-tick rejection and restart clearing;
- cumulative authority preservation in the world-run report.

The complete test suite passes 820/820 tests. The complete solution, including
all structure services, ControlProgram, WorldSim, Blazor and WPF tools, builds
in Release with zero warnings and errors.

## Live acceptance ladder

The next observation should use fresh experimental weights, predators
suspended, and the same HandSpace start:

1. Confirm all 119 structure types are healthy and scheduler timing reports
   bounded per-instance cadence.
2. Require non-zero D1 and D2 current and up-state under ordinary sparse live
   input, followed by GPi/SNr modulation and Motor Thalamus relay.
3. Compare terminal action state with the cumulative v8 authority history; a
   brief grant must no longer disappear from the evidence.
4. Confirm somatic retries recover without false rejected-frame counts.
5. Only after the complete action loop grants authority should locomotion,
   reach, grasp, intake, or learned behavior be evaluated.

The desired outcome is not forced movement. The desired outcome is truthful
neuronal time: silence when the circuit cannot resolve an action, and an
inspectable biological path to authority when it can.

# Folded Archive Entry 118: Blazor WorldSim Embodied Death Experiment

Date: 2026-08-09

## Purpose

Record the first substantial embodied run of the authoritative headless
WorldSim through the Blazor DNNE Editor, preserve the evidence observed before
the avatar respawned, and define the corrections required for the next run.

The experiment retained the neuronal-only authority boundary:

`brain -> avatar motor populations -> physical world -> sensory frames -> brain`

No machine-learning policy, host steering, scripted survival behavior, or
semantic action authority was introduced.

## Pre-Respawn Evidence

The final captured state before the physical body reset reported:

| Measure | Result |
| --- | ---: |
| World elapsed time | 548.2 sec |
| Brain tick | 17,160 |
| Registered structures | 90 |
| Distance travelled | 1,413.5 world units |
| Average path speed | 2.58 units/sec |
| Terrain cells visited | 877 of 14,663 (5.98%) |
| Neuronal motor dispatches | 36,728 (67.0/sec) |
| Sensory frames | 6,679 (12.18/sec) |
| Final captured position | 65.4, 2.5, -57.9 |
| Energy | 52% |
| Hydration | 100% |
| Tissue integrity before terminal decline | 77% |
| Food consumed | 0 |
| Devices collected | 0 |
| Shelter occupied | No |

The operator directly observed tissue integrity subsequently fall to zero,
followed by the avatar returning to its initial body position and restored
physiology. This was an automatic physical death and body respawn, not a loss
of brain connectivity.

## Runtime Stability

The distributed brain and transport remained healthy throughout the relevant
host session:

- 27,016 recorded ControlProgram responses all returned HTTP 200;
- median response latency was 26.2 ms;
- p95 latency was 77.1 ms and p99 was 149.4 ms;
- the maximum response latency was 1.10 sec, with one response over one sec;
- the Blazor host stderr log was empty;
- there were no host exceptions, connection failures, or WorldSim tick faults;
- measured world throughput was approximately 31.3 ticks/sec.

The experiment therefore did not fail because of laptop CPU, memory,
ControlProgram transport, or missing neuronal activity.

## Causal Finding

At 52% energy and 100% hydration, neither metabolic energy stress nor
dehydration could reduce tissue under the configured physiological thresholds.
Predator contact was the only active tissue-damage path.

Each predator currently applies 3.5% tissue damage per second while its
horizontal distance from the avatar is at most 0.65 world units. The 23
percentage-point loss visible in the captured state represents approximately
6.6 equivalent single-predator contact-seconds. Full tissue depletion requires
approximately 28.6 single-predator contact-seconds, or less when contacts from
multiple predators stack.

At 8% tissue integrity the body becomes incapacitated and motor capacity falls
to zero. A predator that remains in contact can then continue damaging the
immobile body until tissue reaches zero. WorldSim increments the physical death
counter and respawns only the body, restoring tissue to 100% and energy and
hydration to 75%. World time, learned neuronal state, and accumulated world
experience continue.

## Additional Behavioural Findings

- Left and right motor drive were 143.38 and 52.61, a 2.73:1 imbalance.
- Path length was approximately 15.5 times net displacement, showing energetic
  but inefficient turning and wandering.
- Considerable locomotion produced no food, device, or shelter success.
- The avatar reached the edge of the rendered terrain and appeared airborne.
- Excessive motor-to-speed scaling and missing vertical physics made valid
  neuronal output produce physically invalid locomotion.

## Corrections Completed Before This Entry

The authoritative headless world now includes:

- a 1.8 units/sec forward-speed cap and bounded reverse speed;
- acceleration and deceleration limits;
- gravity, vertical velocity, terminal velocity, grounded state, and landing;
- terrain-height support and step-height checks;
- world-boundary, static-obstacle, and shelter collision checks;
- browser gait animation that cannot accumulate false root height;
- deterministic shelter sites on graded, dry, level foundations;
- clear shelter interiors and entrance corridors;
- static and runtime entity exclusion from shelter footprints and approaches.

These corrections address the extreme speed, airborne presentation, boundary
escape, inaccessible shelter entrances, and scenery inside shelters observed
during the experiment. They do not yet correct all predator-contact behavior.

## Required Corrections

### P0 - Predator contact physics

1. Replace horizontal-only predator contact with a three-dimensional body or
   capsule overlap test that includes vertical separation.
2. Prevent ground predators from damaging an avatar that is physically above
   or below their contact volume.
3. Add an explicit neuronal-world attack cadence or refractory interval so a
   single overlap does not become an uninterrupted strike every tick.
4. Bound or explicitly model simultaneous predator damage rather than allowing
   accidental linear stacking from overlapping predators.
5. Add collision separation or knockback so predators and the avatar cannot
   occupy the same physical space indefinitely.
6. Make predator movement respect terrain steps, static obstacles, shelter
   walls, and entrances; predators must not pass through shelter geometry.
7. Reassess terminal incapacitation so the body transition is physically
   coherent while preserving genuine consequences and neuronal authority.

### P0 - Durable experiment telemetry

1. Write a bounded JSONL world timeline at approximately 1 Hz with session ID,
   world tick, body state, position, velocity, grounded state, motor drives,
   dispatch counters, sensory counters, physiology, contacts, and failures.
2. Write immediate event records for world start, pause, resume, reset, predator
   contact, incapacitation, death, respawn, collision, and interaction outcome.
3. Capture the complete terminal body state before `ResetBodyCore` restores it.
4. Record a death cause and contributing contacts in the public world snapshot.
5. Rotate logs by size and retention count and flush them during graceful stop.

### P1 - Editor observability

1. Display physical death count, last death cause, current contact count,
   grounded state, vertical velocity, and collision count in the World tab.
2. Distinguish manual world reset from automatic physical body respawn.
3. Show a compact event timeline so terminal physiology can be inspected after
   the body has already respawned.

### P1 - Regression coverage

1. Verify airborne bodies cannot receive ground-contact predator damage.
2. Verify attack cadence and bounded multi-predator damage deterministically.
3. Verify overlap separation and shelter collision for predators.
4. Verify death increments once, preserves world counters, resets only the
   physical body, and clears stale motor output.
5. Verify the terminal pre-respawn state and death cause are durably recorded.
6. Verify maximum speed, acceleration, gravity, grounding, world bounds,
   shelter accessibility, and clear shelter interiors in one embodied test.

## Next Qualification Run

After the P0 corrections, repeat seed 317 with the brain, authoritative Blazor
WorldSim, and Editor together. The run should confirm:

- 90 healthy neuronal structures and a continuously fresh brain link;
- no tick failures or legacy WPF simulator processes;
- speed never exceeds the physical cap;
- the body remains grounded except during genuine terrain transitions;
- no out-of-world movement or scenery inside shelters;
- predator contacts correspond to real three-dimensional overlap;
- attack cadence, damage, incapacitation, death, and respawn are visible and
  recoverable from the durable event log;
- food, device, shelter, and predator outcomes remain consequences of neuronal
  action rather than host policy.

## Follow-up threat-memory probe

A later live probe repeated an animal attack and reset only the physical world,
leaving the distributed brain and its synapses intact. Immediately after the
first attack, the neuronal affect decoder reported defensive drive `0.0923`,
negative valence `0.0607`, arousal `0.4212`, a five-member memory ensemble, an
engram strength of `0.2935`, and ten learned synapses. A second lethal attack
increased the physical-death counter and produced defensive drive `0.1039`,
negative valence `0.0687`, arousal `0.4730`, and a seven-member ensemble.

Thirty seconds after the world reset, with the nearest predator approximately
39.8 world units away, defensive drive remained `0.8128`, negative valence
`0.6844`, and arousal `0.3880`; the dominant decoded state was defense. This is
strong evidence that the neuronal brain preserved a threat-related internal
state across body/world reset. It is not yet proof of an attack-specific episodic
memory: the next qualification run must compare matched predator-free controls,
novel contexts, and re-exposure while tracking the same learned synapses and
engram ensemble.

## Verdict

This was a successful experiment. It proved that the complete neuronal embodied
loop can sustain substantial activity, exploration, physiological consequence,
physical death, and body respawn while the distributed brain remains healthy.
It also exposed a precise next target: predator contact must become physically
credible, and embodied evidence must survive resets. All DNNE and simulator
processes were stopped at the end of the session.

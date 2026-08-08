# Folded Archive Entry 114: Extended WorldSim Run Analysis

Date: 2026-08-08

## Purpose

Gracefully stop and analyse the extended visible WorldSim and complete local
DNNE run begun in Entry 113. The run used world seed 317 with the editor and
MazeSim closed.

## Graceful Shutdown Evidence

- WorldSim accepted its normal WPF close request and wrote a final runtime
  state with `running=false`.
- All 180 bilateral structure services accepted their neuronal shutdown
  request and exited after draining and saving synapses.
- The Control launcher and ControlProgram then stopped through the repository
  shutdown script.
- No DNNE process or configured DNNE listener remained after shutdown.
- All 180 expected current bilateral synapse files were freshly saved; none
  were missing or unexpected.

## Run Summary

| Measure | Result |
| --- | ---: |
| Duration | 85 min 59.8 sec |
| Final brain tick | 198,069 |
| Average simulation throughput | 38.39 ticks/sec |
| Registered structures | 90 |
| Non-OK structures before shutdown | 0 |
| WorldSim tick failures | 0 |
| Neuronal motor dispatches | 9,007 |
| Locomotor dispatches | 6,026 |
| Manipulator dispatches | 2,981 |
| Distance travelled | 4.977 m |
| Distance per locomotor dispatch | 0.826 mm |
| Visited terrain cells | 4 of 7,628 (0.052%) |
| Interaction attempts | 354 |
| Interaction successes | 0 |
| Retinal frames accepted | 4,108 |
| Cochlear frames accepted | 4,023 |
| Physical-body frames accepted | 3,984 |
| Somatic frames accepted | 3,985 |
| Food, water, shelter, and weapon successes | 0 |
| Final energy reserve | 0 J |
| Final tissue integrity | 0% |
| Final hydration | 63.7% |

## Findings

### Runtime stability

The distributed brain remained healthy for the complete run. There were no
structure restarts, reported backpressure events, critical log records,
WorldSim tick failures, or process crashes. Nine visual-frame HTTP requests
ended with incomplete request bodies when WorldSim cancelled timed-out uploads.
The adaptive input backoff recovered each time, and the final state still
reported a live brain connection. This is a recoverable transport-pressure
symptom rather than a brain failure.

### Movement and embodied behaviour

The longer run proves that neuronal locomotion is real: the avatar eventually
travelled almost five metres and exceeded the earlier short qualification's
one-metre threshold. It remains far too weak and repetitive for useful embodied
behaviour. Each locomotor dispatch produced only 0.826 mm of displacement on
average, and the avatar explored only 0.052% of available terrain.

The manipulation pathway also emitted sustained output, but 354 interaction
attempts yielded no successful contact with food, water, shelter, devices, or
predators. The next behavioural diagnosis should therefore separate targeting,
approach, contact range, and manipulator timing instead of treating this as a
missing-output problem.

### Physiology

The avatar began with 6,000,000 J and burns 33,600 J/sec at the default awake
metabolic rate, exhausting the reserve in approximately 179 seconds without
food. Energy stress then reduces tissue integrity. The current world has no
terminal incapacitation, death, or automatic respawn transition when energy or
tissue reaches zero. Consequently, the avatar remained awake and continued
acting for most of this run with zero energy and zero tissue integrity. This is
a simulation-state defect that should be fixed before survival learning is
evaluated.

### Logging and persistence

The ControlProgram information log reached 3.07 GiB, or 36.56 MiB/min. It
recorded about 6.26 million outbound structure requests, approximately 1,214
requests/sec, with several information records per request. This volume creates
unnecessary disk pressure and obscures useful evidence. Routine HTTP client and
request logging should be suppressed to warning level, periodic aggregate
telemetry should replace per-request records, and logs should use bounded
rotation.

The 180 freshly saved synapse files totalled 282.8 MiB. V1, S1, and A1 alone
held 875,668 synapses and approximately 266.5 MiB, about 94% of persisted bytes.
Most sensory synapses had a null stable key, while most had nevertheless been
updated at least once. Before longer runs, sensory synapse creation should be
checked for reusable identity, capped or pruned by age/usefulness, and moved to
a compact binary or database representation where practical.

## Verdict

The infrastructure passed an extended stability test and shut down without
state loss. The principal failures are now behavioural and representational:
weak locomotor effect, no successful object interaction, an incomplete
physiological terminal-state model, excessive per-request logging, and rapid
high-cardinality sensory synapse growth. These are concrete next-rung targets;
the evidence does not indicate that laptop CPU or RAM caused the behavioural
failure.

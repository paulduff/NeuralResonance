# Folded Archive Entry 116: Extended WorldSim Corrections

Date: 2026-08-08

## Purpose

Record the extended neuronal brain and WorldSim run following Entry 115, then
correct the faults demonstrated by that run without adding machine learning,
host action policy, or scripted behavior.

## Run Result

The visible WorldSim session ran for 4,339 seconds, approximately 72.3 minutes,
with seed 317. The physical body remained viable and the brain continued to
drive the avatar throughout the session.

Measured results:

- 22.14 metres travelled;
- 14 of 7,628 terrain cells visited;
- 6,023 motor dispatches, including 4,031 locomotor and 1,992 manipulator
  dispatches;
- 145 collision contacts and no tick failures;
- 1,976 retinal, 3,356 cochlear, 3,041 physical-body, and 3,126 somatic input
  deliveries;
- final energy 55.6%, hydration 64.9%, tissue integrity 100%, and no deaths;
- 212 physical interaction attempts, all rejected as out of reach, with the
  last candidate 6.23 metres away.

Movement improved materially over Entry 114. Distance was 4.45 times greater,
and distance per locomotor dispatch improved from approximately 0.826 mm to
5.492 mm. After a slow startup interval, the engine recovered to approximately
39.5 ticks per second.

All 180 bilateral structure instances accepted graceful shutdown and persisted
their synapses. The active bilateral synapse set was 117.67 MiB, down from
282.8 MiB before homeostatic retention. The active V1, S1, and A1 files held
338,376 inbound entries, approximately 61.4% fewer than the prior 875,668.

## Findings

### Unreachable bootstrap affordance

The manipulator's physical reach was 1.20 metres. The general spawn-clearance
routine rejected every placement closer than 1.25 metres, while Entry 115's
food placement candidates began at 3.0 metres. Therefore the intended first
contact opportunity could never be reached from the initial pose. The 212
failed manipulator outputs were valid neuronal activity confronting impossible
initial geometry.

### Stale Debug launcher path

The stack launcher used `dotnet run --no-build` without selecting a build
configuration. `dotnet run` consequently launched the Debug Control binary,
while structure hosts used Release. The Debug binary predated the framework-log
filters, producing a 2.455 GiB Control stdout log at approximately 34.76 MiB per
minute.

The launcher's stale-build check compared the project file timestamp but did
not inspect edited C#, XAML, JSON, resources, or referenced projects. This
allowed a changed source file to remain newer than the executable without a
refresh.

### Healthy bounded stores

The snapshot store was inspected and already rotates at 256 MiB with bounded
retention. The 194 MiB active snapshot file was below that threshold, so no
snapshot-store change was justified. Seventy-five old unsuffixed synapse files
remain historical artifacts; they are not active bilateral state and were not
deleted automatically.

## Corrections

The stack launcher now:

- defaults explicitly to `Release`, with `Debug` available as an intentional
  option;
- uses the selected configuration for Control, structure prebuilds, structure
  child hosts, and the WPF editor;
- checks project source inputs and recursively follows project references;
- refreshes missing dependency outputs as well as stale outputs;
- never silently falls back to the default Debug configuration.

WorldSim now places one food object at the first collision-free position among
0.90, 1.08, and 1.176 metres from the body, all within the 1.20 metre effector
reach. This placement has a dedicated geometry check because the general
object-spawn clearance is intentionally larger than manipulator reach.

This is an environmental affordance only. The host does not move the avatar,
activate the effector, consume the food, select a target, or assign a symbolic
action. Contact still requires a neuronal manipulator burst, and its somatic
and metabolic consequences return through neuronal input pathways. WorldSim
logs whether placement succeeded so the next run can verify the premise.

Control also suppresses routine ASP.NET result-execution information logs in
addition to HTTP-client, hosting, and endpoint framework logs.

## Verification

- full solution Release build: zero warnings and zero errors;
- full regression suite: 420 passed, zero failed, zero skipped;
- neuronal-only causal and authority preflight: 100 passed;
- circuit audit: every listed structure reported `OK`;
- cortical functional benchmark: `PASS`, with 100% stream separation,
  learning, persistence, and adaptive output gating;
- Release Control smoke test: 25 state requests, zero framework request-log
  lines, and zero stderr bytes;
- PowerShell launcher syntax parse: passed;
- all temporary smoke processes stopped and their ports cleared.

Qualification remains `PREFLIGHT_PASS_LIVE_REQUIRED` because successful food
contact must be observed in a complete embodied run. The next run should
compare time to first contact, successful versus failed manipulator outputs,
food consumption, somatic contact feedback, energy response, locomotor
efficiency, startup recovery, and Control log growth. These measurements must
continue to arise from neuronal output and physical consequences alone.

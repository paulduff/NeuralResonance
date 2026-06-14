# Distributed Deployment

This branch adds a first packaging layer for running DNNE as separate deployables. The goal is to keep the interactive pieces on machine 1 while structure services can move onto worker machines without changing the biological connectome or reducing the 32 x 32 x 32 engine density.

## Deployable Groups

- `machine1-control-avatar`: Control Program, WPF World Sim, WPF Editor, and WPF Maze Sim.
- `visual-perception`: Retina, V1, V2, V4, MT, TemporalAssociation, Pulvinar, SuperiorColliculus.
- `auditory`: Cochlea, CochlearNucleus, SuperiorOlive, InferiorColliculus, A1.
- `body-motor-cerebellum`: body state, motor cortex, spinal/brainstem motor, and cerebellar timing/correction.
- `memory-navigation`: hippocampal and medial temporal/navigation structures.
- `attention-language-executive`: PFC, PPC, thalamic, callosal, and language loops.
- `limbic-basal-homeostasis`: motivation, basal ganglia, homeostasis, affect, and neuromodulatory systems.

The group manifest lives at `deploy/distributed/dnne-deploy.manifest.json`.

## Package

From the repository root:

```powershell
.\tools\package-dnne-distributed.ps1 -Configuration Release -Clean -Zip
```

For a quick metadata/script check without publishing binaries:

```powershell
.\tools\package-dnne-distributed.ps1 -Deployable visual-perception -NoPublish -Clean
```

The output is written to `artifacts/distributed/<deployable-name>`. Each folder contains:

- `deployable.json`: the apps, structures, entry points, and ports owned by that bundle.
- `start-deployable.ps1`: starts the bundle's processes and records PIDs.
- `stop-deployable.ps1`: stops the processes recorded by the bundle.
- `apps/`: published app or structure folders.

The packager also writes `artifacts/distributed/service-instances.template.json`. Replace each `<host-for-name>` token with the machine name or IP that will run that deployable, then put the `ServiceInstances` array into a Control Program appsettings override for distributed runs.

## Start Order

1. Copy each deployable folder or zip to its target machine.
2. Start the structure deployables first:

```powershell
.\start-deployable.ps1 -ControlBaseUrl http://machine1:5080
```

3. Start `machine1-control-avatar` on the interactive/control machine:

```powershell
.\start-deployable.ps1 -ControlBaseUrl http://machine1:5080
```

4. Stop a bundle with:

```powershell
.\stop-deployable.ps1
```

## Control Program Notes

The start script sets `StructureProcessHost__AutoStartEnabled=false` for the packaged control app. In distributed mode, the control program should probe remote endpoints and route spikes to them, but the remote machines should own starting their local structure processes.

Left hemisphere structure ports use the canonical `ServiceRegistry` ports from `ControlProgram/appsettings.json`. Right hemisphere ports use the existing `HemisphereHosting:RightPortOffset` value, currently `+1000`.

## Machine Sizing

For the current development stage, keep machine 1 for the Control Program, Editor, and World Sim. Put the heavier structure bundles on workers. If using low-end machines, start with fewer deployables per worker and avoid putting `body-motor-cerebellum` and `visual-perception` on the same 4 GB RAM box.

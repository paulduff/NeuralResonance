# Distributed Deployment

This packaging layer runs DNNE as separate deployables without changing the biological connectome or reducing the 32 x 32 x 32 engine density. The manifest owns all 119 concrete registered structures and validates 238 left/right service ports before packaging.

## Deployable Groups

- `machine1-control-avatar`: Control Program, authoritative WPF WorldSim, and optional WPF Editor. Windows only.
- `machine1-maze-diagnostic`: optional WPF MazeSim diagnostic. It is not part of the authoritative embodiment path.
- `visual-perception`: Retina, V1, V2, V4, MT, TemporalAssociation, Pulvinar, SuperiorColliculus.
- `auditory`: Cochlea, CochlearNucleus, SuperiorOlive, InferiorColliculus, A1.
- `body-motor-cerebellum`: body state, motor cortex, spinal/brainstem motor, and cerebellar timing/correction.
- `memory-navigation`: hippocampal and medial temporal/navigation structures.
- `attention-language-executive`: PFC, PPC, thalamic, callosal, and language loops.
- `limbic-basal-homeostasis`: motivation, basal ganglia, homeostasis, affect, and neuromodulatory systems.

The group manifest lives at `deploy/distributed/dnne-deploy.manifest.json`.

Structure bundles and the Control Program can target Windows or Linux. WPF apps remain on the interactive Windows machine. The example Tartarus assignment is `deploy/distributed/tartarus.inventory.example.json`; replace its address-block placeholder with the reserved 20-address range before deployment.

## Validate

Validate registry coverage, project mappings, unique ports, platform compatibility, and the inventory before publishing:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\test-dnne-distributed-deployment.ps1 `
  -InventoryPath .\deploy\distributed\tartarus.inventory.example.json
```

The packager runs manifest validation automatically.

## Package

From the repository root:

```powershell
.\tools\package-dnne-distributed.ps1 -Configuration Release -Clean -Zip
```

Publish one Ubuntu worker bundle from the Windows development machine:

```powershell
.\tools\package-dnne-distributed.ps1 `
  -Deployable visual-perception `
  -Runtime linux-x64 `
  -Configuration Release `
  -Clean `
  -Zip
```

For a quick metadata/script check without publishing binaries:

```powershell
.\tools\package-dnne-distributed.ps1 -Deployable visual-perception -NoPublish -Clean
```

The output is written to `artifacts/distributed/<deployable-name>`. Each folder contains:

- `deployable.json`: the apps, structures, entry points, and ports owned by that bundle.
- `start-deployable.ps1`: starts the bundle's processes and records PIDs.
- `stop-deployable.ps1`: stops the processes recorded by the bundle.
- `test-node.ps1`: verifies platform, runtime, memory, disk, entry points, ports, DNS, clock, and shared-secret readiness.
- `apps/`: published app or structure folders.

The packager also writes `artifacts/distributed/service-instances.template.json`. Replace each `<host-for-name>` token with the machine name or IP that will run that deployable, then put the `ServiceInstances` array into a Control Program appsettings override for distributed runs.

## Start Order

1. Copy each deployable folder or zip to its target machine.
2. Install .NET 8 or later and PowerShell 7 (`pwsh`) on Ubuntu workers.
3. Run node preflight on every target. Use `-RequireControl` once the control host is online:

```powershell
pwsh ./test-node.ps1 `
  -ControlBaseUrl http://dyad-control.tartarus:5080 `
  -SharedSecret $env:NRE_STRUCTURE_SHARED_SECRET
```

4. Start the structure deployables first:

```powershell
pwsh ./start-deployable.ps1 `
  -ControlBaseUrl http://dyad-control.tartarus:5080 `
  -ListenHost 0.0.0.0 `
  -SharedSecret $env:NRE_STRUCTURE_SHARED_SECRET
```

5. Start `machine1-control-avatar` on the interactive/control machine:

```powershell
.\start-deployable.ps1 `
  -ControlBaseUrl http://dyad-control.tartarus:5080 `
  -ListenHost 0.0.0.0 `
  -SharedSecret $env:NRE_STRUCTURE_SHARED_SECRET
```

6. Stop a bundle with:

```powershell
.\stop-deployable.ps1
```

## Control Program Notes

The start script sets `StructureProcessHost__AutoStartEnabled=false` for the packaged control app. In distributed mode, the control program should probe remote endpoints and route spikes to them, but the remote machines should own starting their local structure processes.

Left hemisphere structure ports use the canonical `ServiceRegistry` ports from `ControlProgram/appsettings.json`. Right hemisphere ports use the existing `HemisphereHosting:RightPortOffset` value, currently `+1000`.

## Machine Sizing

For the current development stage, keep machine 1 for the Control Program, Editor, and World Sim. Put the heavier structure bundles on workers. If using low-end machines, start with fewer deployables per worker and avoid putting `body-motor-cerebellum` and `visual-perception` on the same 4 GB RAM box.

Use the i7 Ubuntu machine for DNS and a structure bundle only after measuring its actual free RAM under load. Keep DNS, NTP, and the control host on stable addresses. The 2.5 Gb backbone reduces transport contention, but synchronized clocks and bounded queue latency remain qualification requirements.

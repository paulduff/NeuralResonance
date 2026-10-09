# Avatar contact mechanics and Unity comparison — 9 October 2026

Ordinary newly acquired foot support could damage tissue. The injury function used total contact impulse, including the support force integrated over the 50 ms sensory window, as impact momentum. It also charged impact damage on repeated samples throughout the first 150 ms of contact. For example, 720 N of support over 50 ms produces 36 Ns, exceeding the 24 Ns foot threshold despite there being no collision impact.

The correction gives each physical contact a separate `ImpactImpulseNewtonSeconds`. Ordinary ground support explicitly supplies zero. Obstacle sweeps estimate the normal momentum removed from proposed motion, separately from muscle holding force. Sliding measurements continue to use accepted motion. Initial collision injury is counted on the first physical contact sample only; prolonged non-plantar pressure retains its existing consequences. This is still an estimate made by a collision-only kinematic rig, not a calibrated full-body dynamics solver.

Vertical touchdown contributes the lumped body's landing momentum, distributed over its measured ground contacts. Because the muscle plant reflects the previous airborne step on touchdown, pending landing momentum survives until those contacts are available and is then consumed once. Force and total impulse are also conveyed to contact receptors. Reset and respawn clear pending landing momentum.

The recorded right-foot damage in the previous desktop run may include spurious support impacts. That report did not record the newly separated impact quantity, so this fix does not establish how much historical damage was erroneous. A run of the updated world is needed for that attribution. The currently running editor process retains its previously loaded assemblies; these changes take effect on its next rebuilt launch.

## Verification

165 targeted tests passed, including world physiology, severe heel and forearm impacts, support with different sensory windows, counting initial impact once, a hard landing whose muscle feedback arrives one step later, reset, obstacle collision, articulation, spinal locomotion, somatic/body transduction and simulator authority boundaries. The new probe builds with zero warnings and errors. Tests/builds use isolated outputs under `artifacts/collision-qualification-20261009` to avoid replacing DLLs loaded by the current brain/editor. The solution manifest includes the probe and excludes generated artifact projects from repository membership checks.

## Offline physical comparison

`tools/AvatarMechanicsProbe` supplies fixed tonic recruitment to an isolated body. It connects to neither DNNE nor a live Avatar and does not train anything. It applies planar inertia, collision resolution, reconciliation into the muscle plant, external contact feedback and tissue assessment. It compares 7.5 seconds of gait at 20, 25 and 50 ms steps under two conditions:

- The existing separate ground-support mechanism with an empty obstacle scene.
- The same mechanism combined with an explicit Bepu floor collider.

An exploratory 25 ms explicit-floor case produced left clearance of about 69 mm, right clearance below 1 mm and about 21 mm forward progress. This case failed bilateral gait qualification. An attempted change to support offsets also destabilized balance and was discarded. The failing exploratory case has been moved out of ordinary regression tests into this explicit comparative probe: full Bepu ground dynamics are not the current world's implemented ground system. The probe records that limitation openly rather than treating it as qualified.

Each comparison reports bilateral swing, clearance, progress, constrained leg samples, falling samples and tissue integrity. `Qualified` requires both feet to swing, more than 0.25 m progress, no falling samples and intact tissue. These are short mechanical checks, not proof of learned or sustained autonomous walking. Console completion can succeed while physical qualification fails.

Launch with `tools/start-avatar-mechanics-probe-window.ps1`. It builds into a new artifact directory, runs in an independent visible PowerShell window, writes `results.json` and `window-status.json`, and hashes its report/log backups on D:. It requires no agent monitoring. Existing DNNE/world processes remain available. It uses a one-shot interactive scheduled task solely to escape the calling tool's process lifetime, then unregisters that task.

## Unity body prototype

A Unity trial should initially contain one physical humanoid on a flat floor. A rigged FBX with a complete skeleton and a T-pose, clear hands and feet, modest mesh complexity and permission to modify it provides a visual starting point. Unity's humanoid bone mapping is described in its [import documentation](https://docs.unity.com/en-us/engine/7000.0/manual/assets-and-media/asset-types/models/importing/configuringthe-avatar). The skeletal mesh does not supply calibrated masses, physical joint limits, friction or torque capacities; these need separate setup and measurement.

Unity's [Articulation Body](https://docs.unity.com/en-us/engine/6000.5/manual/physics-section/physics-overview/articulations-section/class-articulation-body) supports body mass, physical joints, limits and force/torque drives. Those capabilities make it a candidate for replacing the simplified physical plant. Selecting the mesh is not a migration of the brain or language model.

DNNE remains the brain and action authority; Avatar is the physical body and nervous system; World is the environment. Dyad and Entity remain the interaction path. CIV stays separate. The Unity adapter would carry neuronal motor recruitment into physical drives and measured contact, vestibular, joint and retinal feedback back into DNNE. Walking animation can serve as a measurement reference, but the live body must express DNNE output through its physical actuators.

First qualify passive gravity, joint limits, contacts and controlled actuator response. Then assess standing, weight transfer and bilateral stepping through DNNE's feedback loop. Expand the environment after the body qualifies. Unity would provide a stronger physical test bed; it would not itself prove or teach neuronal coordination.

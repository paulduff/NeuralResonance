# First desktop brain–body–world run — 9 October 2026

The next runtime experiment is DNNE plus the authoritative headless WorldSim hosted by the Blazor editor on this desktop. It establishes the actual machine capacity before moving nuclei to the cluster. The WPF simulators are mutually exclusive diagnostics; do not run them alongside the authoritative world.

The authoritative editor starts WorldSim paused. DNNE can initialise and the editor can display the environment, but world time, body metabolism and environmental sensory sampling wait for the operator to press **Play / Resume**. `HeadlessWorldOptions.StartPaused` controls this initial state; the editor explicitly enables it. Repeated calls to the runtime's `Start` method do not resume a paused world. Automated standalone fixtures retain their existing running default unless they request paused startup.

The desktop measurement window waits for operator Resume before starting its 30-minute timer. Its samples and console output now distinguish LIVE from PAUSED and record brain-frame latency, safety-pause counts and actual distance travelled. A new safety pause fails runtime qualification. Failure counters are compared against the saved world snapshot at measurement start, including failures before the first slower resource sample. Manual pause remains available throughout observation.

Paused-startup, editor-host and closed-neuronal-loop qualification passed 53 tests using an isolated build output, leaving the running session undisturbed. The startup regression verifies that physical time, energy, hydration and environmental sampling stay unchanged before explicit Resume, and that repeated `Start` calls do not override a pause. The editor and runtime changes take effect at the next normal rebuilt launch; the current session was preserved.

After the current Entity training window reports **FINISHED**, invoke:

```powershell
& C:\Users\duff_\source\repos\NeuralResonance\tools\start-desktop-brain-world-window.ps1
```

This opens an independent visible PowerShell window. It checks the saved training completion status once, qualifies the relevant software, builds/starts DNNE nuclei, checks startup health, opens the browser world, and records a 30-minute baseline. There is no agent monitoring and no training watcher. The run refuses to start if training did not finish successfully or the DNNE/editor ports are occupied. It preserves existing saves and does not perform a clean-start kill.

The default baseline measures DNNE and WorldSim with Entity generation disabled. The completed Entity candidate is assessed separately before selecting it. A later comparison can use `-WithEntity` once the Entity API is running and `NRE_ENTITY_CHECKPOINT_PATH` explicitly selects a reviewed candidate. This option uses the existing configuration; it does not promote the training output automatically. GPU inference reduces Entity's CPU demand, while tokenisation, API handling and system memory still consume shared resources.

Artifacts are saved under `artifacts/desktop-brain-world-<unique run>/`, with verified copies under `D:\DNNE-desktop-runs`. They contain hardware and Git identity, console output, startup health, initial/latest world snapshots, sampled ticks, service health, sensory/body/motor counters, DNNE/world CPU consumption, summed process working sets, failures and a summary. Summed working sets can include shared pages and are not a measurement of unique committed RAM. CPU percentages describe the DNNE/world processes as a fraction of total machine CPU capacity.

`COMPLETE.json` means measurement completed; inspect `summary.json` for runtime checks. The checks require brain/world advancement, retinal and body traffic, brain motor output, connectivity, healthy services and no new tick/body failures or sample errors. Movement is reported separately. A healthy transport loop does not prove language comprehension, useful behaviour, or a human-equivalent brain. Language comprehension is explicitly marked unassessed.

DNNE and WorldSim remain running after measurement for observation. A startup failure leaves logs and `RUN_FAILED.json` for diagnosis. The existing stop tools remain available when observation is finished.

The launcher uses a one-off interactive Windows task so the window outlives the originating Codex tool call. It passes absolute Git and .NET locations and adds their directories only to the child window's PATH. A Windows PowerShell 5.1 check with a stripped system-only PATH verifies tool resolution without starting services. The initial attempt failed before DNNE startup because the task-service environment could not find Codex's bundled Git; the corrected launcher addresses that failure directly.

Script parsing and side-effect-free `-WhatIf` checks are complete. Live qualification and performance measurement begin only after Entity training finishes; they run independently in the visible window.

If startup created healthy DNNE and WorldSim services but the measurement launcher failed afterwards, use `start-desktop-brain-world-window.ps1 -UseRunningStack`. This mode verifies that both listeners belong to this checkout, checks the existing world connection, and measures without rebuilding or restarting either process. It cannot enable Entity in an existing process. Open `http://localhost:5090/editor` for the live editor and world view.

The second startup attempt passed all 65 preflight tests and started both services, then encountered a launcher return-stream defect: native build text was returned together with the Process object. The shared launcher now sends build output to the console and returns one Process, retaining its handle for reliable exit status on Windows PowerShell. `tools/test-dnne-project-launcher.ps1` exercises a real build and a short-lived hidden console process on Windows PowerShell 5.1; exactly one Process, accessible HasExited, and exit code zero are verified. Generated startup profiles now stay in each experiment's artifact directory. The profile generated by the failed attempt was preserved locally and hash-verified on D: before restoring the tracked source profile.

The first live desktop world advanced 6,679 ticks (220.59 simulated seconds) and then safety-paused after three brain-frame requests exceeded four seconds; the last took 4,910 ms. It had travelled zero distance at that point and recorded five body-input failures. The brain continued emitting motor traffic while physical time was stopped. After the operator pressed Play, a saved snapshot showed 1.877 metres travelled, world tick 9,538, zero world tick failures and no additional safety pauses or body-input failures. This demonstrates that the live neuronal output can move the physical body; it does not establish purposeful exploration. The 43 existing motor-bridge, articulated-body and spinal-locomotor tests passed. Diagnostic snapshots and the test report are in `artifacts/avatar-idle-20261009/`.

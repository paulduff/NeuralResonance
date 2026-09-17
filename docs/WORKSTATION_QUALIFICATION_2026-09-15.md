# RTX workstation qualification — 15 September 2026

Latest update: the D: recovery source is now restored on the replacement SSD. The complete build, 836-test suite and cortical benchmark passed on 17 September after the scheduler correction recorded below. Fresh-network live behavioural qualification remains pending.

EntityLLM, CUDA, Dyad and DNNE are one integrated system. The complete restart plan and run evidence are recorded in the peer EntityLLM repository's `WORKSTATION_RESTART_2026-09-15.md` and `artifacts/workstation-2026-09-15/`.

## Completed here

- Initial fresh-install DNNE suite: 830/831 tests passed. The sole failure was a literal LF comparison against a CRLF JavaScript checkout, not a missing runtime statement.
- Changed that assertion in `BlazorEditorHostTests.WorldWorkspaceDoesNotOverrideGlobalBrainRuntimeSummary` to normalize line endings.
- Static circuit audit: 119 registered structures, 119 OK, 238 bilateral instances. Report: `artifacts/workstation-2026-09-15/circuit-audit.md`.
- Peer Dyad wire contracts are byte-identical.

## Current blocker and remaining work

Before the planned DNNE rebuild and short fresh-network HandSpace observation, Core Temp revealed CPU temperatures near 100 C on the RTX workstation. Paul confirmed they persist even without training. The Entity CUDA experiment was stopped; no live DNNE services were started.

After cooling is corrected, rerun the affected test/full suite with bounded build parallelism, run the cortical benchmark, then follow Entry 153's ordered circuit, quiescence, reach, locomotion, survival and predator acceptance ladder. Use a new `NRE_SYNAPSE_STATE_DIR` and preserve run telemetry. Entry 158's historical live result must not be substituted for a fresh-install acceptance run. The live HandSpace reach/grasp/consumption gate and later stages remain unverified on this installation.

## Cooling update — 17 September 2026

After Paul replaced the cooling, a roughly one-minute Core Temp baseline showed CPU cores at 24.5–31.5 C and the GPU at 32–33 C, with no training/runtime workload launched. The previous near-100 C idle condition was absent. This qualifies the idle baseline only; a short monitored workload and the pending runtime checks still precede sustained training. Full sensor evidence and restart instructions are in EntityLLM's workstation restart note.

The subsequent authorized short CUDA check passed: the rebuilt Entity CUDA host exited cooperatively within 2.221 seconds of a stop-file request, and 12 complete synthetic benchmark passes included 11 repeats over about three minutes. Peak sampled CPU/GPU temperatures were **36.5 C / 57 C**, below the retained **85 C / 80 C** stop limits. The 336 synthetic updates and generation benchmarks completed without reported non-finite values. This clears the short workload check after cooling replacement; sustained training and the pending DNNE tests/live acceptance stages remain unverified. No DNNE service or corpus training run was started during this check. See EntityLLM's `artifacts/workstation-2026-09-17/cooling-load-check-summary.json` and updated workstation restart note.

The requested training continuation then stopped before any training updates: the frozen corpus hash check hit a Windows CRC read error. Windows recorded repeated bad-block events on Disk 1, the C: ADATA SU630 SSD. Training and DNNE workload checks remain stopped pending storage diagnosis/remediation. Changed source files and notes from both repositories, Entity experiment artifacts and frozen inputs were copied and hash-verified on the separate D: disk at `D:\EntityLLM-recovery-2026-09-17`. Three unreadable Entity working files were recovered there from verified Git LFS objects. See that directory's README and manifests and EntityLLM's full restart note. No drive repair or reboot was performed.

## Restored source and scheduler validation — 17 September 2026

Paul confirmed D: as the latest recovery source and requested application, validation, commit and push. The saved baseline matches GitHub revision `040f6b8c0aaa32145ea29e034017615f4f9bb2a3`. Both recovered DNNE files were restored into `C:\Users\duff_\source\repos\NeuralResonance` with SHA-256 verification. The source backup remains unchanged. The previous ADATA storage fault is historical; this validation ran on the replacement Samsung SSD.

The initial Release build passed, but the test run exposed a separate scheduler defect. With a configured request concurrency of 48 and only three available service instances, `SensorimotorTickScheduler.Select` passed a minimum greater than the maximum to `Math.Clamp`. The background tick service threw and stopped the ControlProgram. This caused 47 endpoint failures; the other 784 tests passed.

The scheduler now bounds concurrency to the available service count before calculating its pressure-dependent budget. Five regression cases cover startup and steady operation, low and high pressure, and an oversized integer budget. All available services remain selected when the worker budget exceeds the population; fast/general counts and cursors remain consistent. Neuronal dynamics and authority gates are unchanged.

Final validation:

- Complete 131-project Release solution: **zero warnings and errors**.
- Complete DNNE suite: **836/836 passed**, zero skipped, including the restored line-ending test and all five scheduler regressions.
- Circuit audit: **119/119 OK**, 238 bilateral service instances.
- Cortical benchmark, 24 epochs: **100%** overall, stream separation, learning, persistence and adaptive output gating.
- Peer Dyad wire manifest: byte-identical, SHA-256 `85c5dc56f6fc5353fc18b2299b49af019df2ba2f0ecdcc9fe6b67c4cebdcba0b`.
- Sequential bounded monitoring across both repos recorded sampled CPU/GPU maxima of **62.5 C / 41 C**, below the retained 85 C / 80 C stop limits. The Entity CPU and CUDA suites also passed, and managed CUDA cancellation returned 130 within an observed 2.61 seconds.

Evidence, including the original failed run and the scheduler exception, is retained in ignored `artifacts/recovery-2026-09-17/`. The corrected suite is `test-results/dnne-verified.trx`; the rebuilt solution and test monitors are `dnne-build-02` and `dnne-tests-02`. Test-owned processes exited after validation.

This completes restored-source and offline validation. It does not substitute for Entry 153's fresh-network circuit, quiescence, HandSpace reach/grasp/intake, locomotion, survival and predator ladder. Use a new synapse-state directory for that work and retain the current neuronal authority and physical evidence requirements.

# Entry 051 - NRE.WpfEditor Audit & Improvement Plan

Date: 2026-05-31

## Purpose

Focused audit of the WPF editor (`src/NRE.WpfEditor`). The editor polls the Control Program at ~10 Hz for frames, renders 3D brain meshes, captures webcam + microphone input, runs SAPI speech output, and shows telemetry. Several partial classes split `MainWindow` logic across `.Visuals`, `.Camera`, `.Speech`, `.Microphone`, `.Brain3D.*`, `.Telemetry`, `.Webcam`, `.Health`, `.Frames`, `.ControlPanels`, `.EndpointResolution`.

Current-state note: Entry 102 later deletes the editor SAPI and host phrase
pipeline. The description above records the editor as it existed during this
historical audit, not the present language boundary.

This entry captures the items found and marks each as the work lands.

## What was already good

- Single `HttpClient` shared via `NreHttpClientFactory` (no per-request `new HttpClient`).
- Render worker uses `PeriodicTimer` with `Interlocked` re-entrancy guard.
- Mesh `Freezable.Freeze()` is consistently applied to immutable meshes via `TryFreeze`.
- Webcam/microphone stop paths use cooperative cancellation with a bounded wait.
- Frame stream loop has proper backoff, premature-end detection, and a "disable for session" escape valve.
- `PaneWorker` collapses backlog to latest-only — correct pattern for telemetry coalescing.

## Improvement List

### Bugs

1. **Async-void event handlers without top-level try/catch.**
   - Status: DONE (2026-05-31).
   - Added `SafeHandlerAsync(Func<Task>, string)` helper on MainWindow that logs failures via `AddOutputLog` and swallows `OperationCanceledException`. All 8 ControlPanels button handlers, `ToggleMicrophoneInputButton_OnClick`, `ToggleWebcamInputButton_OnClick`, `SendLanguageInputButton_OnClick`, `InputGatesCheckBox_OnChanged`, and the three DispatcherTimer / InvokeAsync lambdas in MainWindow.xaml.cs now wrap their work through it. `FileOpen/FileSaveNetworkMenuItem_OnClick` already self-protect with try/catch and were left as-is.

2. **Webcam CTS double-dispose race.**
   - Status: DONE (2026-05-31).
   - Replaced `_webcamCts?.Cancel(); _webcamCts?.Dispose();` in `ToggleWebcamInputAsync` with `await StopWebcamInputAsync()` when an existing CTS is non-null. `StopWebcamInputAsync` already awaits `_webcamTask` before disposal, so the worker can never observe a disposed token.

3. **Microphone CTS same race.**
   - Status: DONE (2026-05-31).
   - Same pattern: routed through `StopMicrophoneInputAsync` instead of cancelling+disposing inline.

4. **Speech COM leak on exception.**
   - Status: DONE (2026-05-31).
   - Wrapped the entire `SpeechWorkerLoop` body in `try/finally`. The COM release block runs unconditionally, including the case where the inner loop throws mid-`Speak`. A defensive inner `try/catch` around `FinalReleaseComObject` also prevents the finalizer from re-throwing.

5. **Microphone `recordingStoppedUnexpectedly` cross-thread read.**
   - Status: DONE (2026-05-31).
   - Replaced the captured `bool` local with an `int` flag accessed via `Volatile.Read`/`Volatile.Write`. Closure capture promotes the local to a shared field, so a memory barrier on each access guarantees the UI-thread write is visible to the capture-loop thread.

6. **Frame-stream pending flag never reset on exception.**
   - Status: DONE (2026-05-31).
   - The `Task.Run` body now tracks a `dispatched` flag; only the failure path resets `_streamFrameUiApplyScheduled`. On success, `ProcessPendingStreamFramePayload`'s own finally retains ownership of the flag (which can be re-set if a new frame is pending), so the outer reset no longer clobbers a fresh reschedule.

### Perf

7. **Brain3D reference-mesh brushes not frozen.**
   - Status: DONE (2026-05-31).
   - Added `Freeze()` to the three pairs of diffuse/emissive `SolidColorBrush` instances in `AddBrainMeshShell`, `AddCorticalGyrusSurface`, and `AddCorpusCallosumPathwayScaffold`. WPF can now hand these to the render thread without taking the freezable lock per frame.

8. **Webcam preview `new byte[byteCount]` per frame.**
   - Status: WON'T FIX AT THIS SCOPE.
   - The pixel buffer is retained by `_avatarService.PostSightInputFrame` (handed to a command queue and held). Pooling without ref-counted release would corrupt the pool on the next frame. The allocation is only ~8 Hz; the safer fix needs `AvatarSightFrame` to own its buffer lifecycle.

9. **Status / reticle `new SolidColorBrush(...)` per UI update.**
   - Status: DONE (2026-05-31).
   - `MainWindow.Health.cs`: four frozen `(Fill, Stroke)` brush pairs cached as static fields keyed by `InputHealthState` (Healthy / Warning / Failed / Idle). `SetInputHealthIndicator` now just assigns the cached pair instead of allocating per call.
   - `MainWindow.Webcam.cs`: `UpdateWebcamAttentionReticle` now reuses the existing `Stroke` and `Fill` brushes on the Shape and mutates their `Color` property instead of allocating a new `SolidColorBrush` per call.
   - The badge construction at `xaml.cs:807` runs only once per service at startup; allocation is not in a hot path and was left as-is.

10. **`TryGetProperty` Normalize allocates a lowercased string per JSON access.**
    - Status: DEFERRED.
    - Refactoring the normalize behavior touches every property lookup across `App.xaml.cs`, `MainWindow.xaml.cs`, and the partial-class consumers. Worth its own focused pass with benchmarking.

### Cleanup

11. **Duplicated `TryGetProperty` / `Normalize` / `GetInt` / `GetLong` / `GetDouble`** between `MainWindow.xaml.cs` and `App.xaml.cs`.
    - Status: DEFERRED.
    - Pairs with item 10. Extracting to a shared helper makes sense in the same pass that addresses the normalization perf.

12. **Speech `BlockingCollection` race**: `TryAdd` then `TryTake` to drop oldest is not atomic.
    - Status: DEFERRED.
    - Speculative per the audit; the queue is small and the worst-case is a single dropped phrase. Replacing `BlockingCollection<string>` with a bounded `Channel<string>` using `BoundedChannelFullMode.DropOldest` would be the clean fix, but it requires reworking the consumer loop and is not in scope for this pass.

13. **Editor JSON parsing across multiple partial classes** uses untyped `JsonDocument`.
    - Status: WON'T FIX AT THIS SCOPE.
    - Adopting source-gen DTOs is a wider change that needs DTO design for the heterogeneous frame schema.

## Progress

2026-05-31 - First slice landed.
- Items 1, 2, 3, 4, 5, 6 (bugs) and 7, 9 (perf) all DONE.
- Items 8, 10, 11, 12 deferred or marked won't-fix with rationale.
- Item 13 marked won't-fix at scope.
- DNNE solution builds clean (0 warnings, 0 errors); 136/136 tests pass.

All editor bugs identified in the audit are resolved. The remaining items are either explicitly scoped out (10 / 11 / 13) or speculative-and-low-impact (8 / 12).

## Second slice (2026-05-31): editor speed pass

Two additional improvements landed after a follow-up question about putting panels on their own threads. WPF visual-tree thread affinity rules out true multi-thread UI, but two architectural improvements help:

### Adaptive frame rate (was item #6 on the suggested list)

- Status: DONE.
- `RenderWorkerLoopAsync` no longer uses a fixed `PeriodicTimer`. It picks `ActiveRenderInterval` (100 ms / 10 Hz) when any spike brush, pathway visual, or corpus-callosum activity is animating, and `IdleRenderInterval` (250 ms / 4 Hz) when everything has decayed. The state is tracked by a `_visualActivity` flag set by `MarkVisualDirty()` from spike-brush ignition, pathway activation, and corpus callosum activity, and cleared by `ApplyVisualDecay` when the active lists drain to empty. Reduces idle UI-thread work by ~60% without changing animation behavior under load.

### Spike ingest off the UI thread (was item #1 on the suggested list)

- Status: DONE.
- Extracted the heavy per-frame structure walk and brush-op resolution into `PrepareNeuronHighlights` (pure CPU on POCO data + read-only access to `_structureVisualsByBaseId` / `StructureVisual.SpikeNeuronBrushes`). This now runs on a thread pool worker via `Task.Run` awaited from `ProcessSnapshotFramePayloadAsync`. The remaining `ApplyPreparedNeuronHighlights` mutates the brushes on the dispatcher.
- Plumbed `async Task` through `ProcessFramePayloadAsync` / `ProcessPendingStreamFramePayloadAsync` and the three caller sites in `MainWindow.Frames.cs`. The dispatcher invoke uses `await await Dispatcher.InvokeAsync(..., DispatcherPriority.Background, ...)` so exceptions propagate properly.
- Net effect: per-frame UI-thread cost in the snapshot apply path drops by the cost of the structure walk + neuron-id resolution + dispatch-set construction (the dominant CPU). The brush mutations and decision logic remain on UI.

### Verification
- DNNE solution builds clean (0 warnings, 0 errors).
- All 136 tests pass.

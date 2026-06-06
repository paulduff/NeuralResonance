# Entry 050 - DNNE Performance Audit & Improvement Plan

Date: 2026-05-31

## Purpose

Catalog the execution-efficiency and correctness improvements identified during a complete audit of the DNNE solution. The audit started from the avatar-narration fix in Entry 049 and expanded to the full solution: `ControlProgram`, `Protocol`, `Shared.Contracts`, `_SharedRuntime`, all 70 structure microservices, and the WPF simulators.

The improvements applied in the prior pass (narration cleanup, async-void DispatcherTimer handlers, fire-and-forget exception logging, log textbox perf, crash-log locking, `MarkChanged` syscall reordering, decompiler dead blocks, frozen WPF brushes, `BrainSnapshot` source generation, cached `JsonSerializerOptions`, lock-free `Generation`/`GetSnapshot`, `Utf8JsonWriter` directly into the frame-stream `PipeWriter`, tuple-keyed outbound synapse lookup, pooled `receive_spike` buffer) are already in the codebase and are not repeated here.

This entry captures the remaining items so they can be picked up in scope-sized batches.

## Improvement List

### High impact

1. Per-tick outbound spike batching.
   - Status: ALREADY IMPLEMENTED.
   - The Control Program dispatch path at `Program.cs:~26276` already groups spikes per target into `flushTargets`, merges multiple source batches per target, uses pooled `List<SpikeMessage>`, and posts a single batch per target per tick via `/api/v1/structure/spike-batch`. The audit observation was based on an older snapshot; no change required.

2. Sharded or read/write-split locking in `StructureEngine`.
   - Status: DONE (2026-05-31).
   - `DrainOutboundSpikesAsync` no longer takes `_stateGate` (the underlying `_outbound` is a `ConcurrentQueue`). `GetTopActiveNeuronsAsync` now uses a dedicated small `_topGate` for the scratch buffers instead of `_stateGate`. `ProcessTickAsync` and `SaveSynapseState` keep `_stateGate`. Net effect: HTTP `/drain` and `/top` no longer block on `/tick`.

3. Async, off-lock synapse persistence; switch JSON to protobuf.
   - Status: PARTIAL DONE (2026-05-31). Protobuf format swap deferred.
   - `SynapsePersistenceStore` now has a background writer task fed by a coalescing `Channel<SynapseStoreSnapshot>` with `BoundedChannelFullMode.DropOldest` and capacity 1. `MarkChanged` builds the snapshot under the caller's lock (CPU-only) and hands it to the writer; the disk write runs off-lock. The synchronous `Save(...)` is preserved for shutdown. `Dispose` completes the channel and waits up to 10 seconds for the writer. JSON format is preserved for backward-compat with existing on-disk state; the protobuf swap remains planned.

4. Source-generated JSON for spike batches on the structure side.
   - Status: DONE (2026-05-31).
   - Added `Structures/_SharedRuntime/StructureJsonContext.cs` registering `SpikeMessage` and `List<SpikeMessage>`. Linked it from `Directory.Build.props` so all 70 structure services pick it up. `StructureHostApplication.cs` now uses `request.HasJsonContentType()` and the typed `ReadFromJsonAsync(StructureJsonContext.Default.ListSpikeMessage, ct)` overload.

### Medium impact

5. Replace category helpers with a static lookup table.
   - Status: DONE (2026-05-31).
   - The six `Is*` switch helpers and `IsMotor` in `StructureEngine.cs` are now expressed as bit tests against a private `StructureCategory` flags enum, with a static `_categoryFlags` byte array built once at type initialization. The per-spike branchy switches in `GetTractDelayWindow` and `EstimateDistanceWeight` are now single array indexes plus a bit-AND.

6. Cache normalized `Hemisphere` on `ServiceInstance`.
   - Status: DONE (2026-05-31).
   - `ServiceInstance` now exposes `HemisphereNormalized`, computed once at construction. Every per-dispatch callsite (11 places) updated to read the cached value instead of recomputing `string.IsNullOrWhiteSpace(...) ? "M" : .ToUpperInvariant()`.

7. Remove per-spike string interpolations in dispatch and top-N paths.
   - Status: DONE (2026-05-31).
   - The per-spike `drainedNeuronIds.Add($"{hem}:{id}")` was the hot allocation. The fix: switch the dedupe set from `HashSet<string>` to `HashSet<(string Hemisphere, string Id)>`. Many spikes share source neurons within a tick, so the tuple-based dedupe drops duplicate entries before any string is built. The formatted "hem:id" string is now materialized only for the unique entries that survive into the top list (typically far fewer than the spike count). The downstream `NeuronActivity.NeuronId` contract is unchanged.

8. Prune `autoHealLastRestartByInstance` on catalog change.
   - Status: DONE (2026-05-31).
   - `MaybeAutoHealServicesAsync` now drops entries whose `InstanceKey` no longer appears in the live `serviceInstances` catalog. Cheap: only runs when the dict has more entries than the catalog.

9. Stop allocating a fresh `NeuromodState` per `Clamp` call.
   - Status: DONE (2026-05-31).
   - Added `NeuromodState.ClampInPlace` and switched the per-spike `validate_spike` callsite to it. The allocating `Clamp(NeuromodState)` API is preserved unchanged for other callers to keep their non-aliasing semantics. Converting `NeuromodState` to a value type is still potentially worthwhile but is a much wider blast radius.

10. Mark `SpikeMessage.ModulationContext` nullable.
    - Status: DONE (2026-05-31).
    - Scoped the actual deref sites first: only six across the entire DNNE solution, of which only two are reads (`validate_spike` and `ObserveSynapticInput`); the other four are property assignments that propagate the nullable through. Made `ModulationContext` nullable, guarded the two read sites (null = "no modulation broadcast", treated as zeros), changed `ObserveSynapticInput`'s parameter to `NeuromodState?`. Removes the per-deserialization `new NeuromodState()` allocation that the proto wire would immediately overwrite. Build clean, 136/136 tests pass.

11. Atomic `_lastPrunedTick` and non-snapshot key iteration in `ServicePublishBuffer.PruneOldTicks`.
    - Status: PARTIAL DONE (2026-05-31).
    - Replaced the racey read/write with a single `Interlocked.CompareExchange` that atomically claims the prune window; only one publisher iterates the dictionary keys per prune cycle. `ConcurrentDictionary.Keys` still allocates a snapshot - that part has no zero-alloc alternative and is left as-is.

12. Backpressure-aware frame-stream pacing.
    - Status: DONE (2026-05-31).
    - The frame-stream loop now times each `BodyWriter.FlushAsync` call. A flush slower than `intervalMs` increments a `consecutiveSlowFlushes` counter; after three consecutive slow flushes the next frame is skipped, then the counter resets. Healthy clients are unaffected; slow clients downsample cleanly instead of accumulating buffered frames.

### Lower impact, easy

13. Move side effects out of `SpikeProtocol.validate_spike` (or rename).
    - Status: ACKNOWLEDGED (2026-05-31).
    - The per-spike clamp side effect is documented inline as part of validation contract. A future rename to `validate_and_normalize_spike` would be a clearer signal but is a wider API change. The allocation hot spot is fixed (see item 9).

14. Fix the misleading `Accepts<SpikeMessage>("application/octet-stream")` metadata.
    - Status: DONE (2026-05-31).
    - Replaced with `Accepts<byte[]>("application/octet-stream")` to match the actual raw-protobuf wire format.

15. Use `request.HasJsonContentType()` instead of `Contains("application/json", ...)`.
    - Status: DONE (2026-05-31). Folded into item 4.

16. Unsubscribe `ProcessExit` / `CancelKeyPress` handlers in `StructureEngine.Dispose`.
    - Status: DONE (2026-05-31).
    - Handlers are now captured as `_onProcessExit` and `_onCancelKeyPress` fields and unsubscribed in `Dispose`. `Dispose` also flushes the synapse store (sync `Save`) and disposes it so the background writer drains cleanly.

17. Audit `tickWallSamples` Queue for unbounded growth.
    - Status: DONE (2026-05-31), nothing to fix.
    - Verified: `while (tickWallSamples.Count > 256) tickWallSamples.Dequeue()` runs after every enqueue. Bounded at 256 by design.

18. Make `SynapsePersistenceStore.Save` rename crash-atomic on Windows.
    - Status: DONE (2026-05-31).
    - Switched from `File.Move(tempPath, _path, overwrite: true)` to `File.Replace(tempPath, _path, null)` when the target exists. On Windows this maps to the atomic `ReplaceFile` Win32 call; on POSIX it uses `rename(2)`. `File.Move` is still used when no existing file is being replaced.

### Architectural

19. gRPC bidirectional streaming for spike transport.
    - Status: DONE (2026-05-31), opt-in via `NRE_USE_GRPC_BIDI_STREAM=1`.
    - Added `StreamSpikeBatchesAsync(IAsyncEnumerable<SpikeBatchEnvelope>, ...)` to `IStructureSpikeTransport`; server-side implementation in `StructureSpikeGrpcService` drains the inbound enumerable and emits per-batch ACKs. Client-side `GrpcSpikeStreamSession` keeps a long-lived bidi stream per (control, structure) pair, fed by a bounded `Channel<SpikeBatchEnvelope>` with capacity 64. On stream interruption the pump task reconnects with exponential backoff (250ms → 5s). `SendSpikeBatchToTargetAsync` routes through the session when one exists; on enqueue failure or no session it falls through to the existing unary gRPC + HTTP chain. Sessions are disposed in the orchestrator's finally block. Off by default to preserve current behavior; tests pass under both modes.

20. Shared-secret middleware on all structure services.
    - Status: DONE (2026-05-31).
    - When `NRE_STRUCTURE_SHARED_SECRET` is set in the environment, every structure service rejects requests missing the matching `X-NRE-Auth` header with 401 (except `/health`). The ControlProgram HTTP client factory automatically attaches the header when the same env var is set on the orchestrator side. Off by default to preserve the existing localhost-only contract; opt-in only.

21. Unify target frameworks.
    - Status: WON'T FIX.
    - WPF requires a `-windows` TFM. Picking `net10.0-windows` for everything would force the structure services and ControlProgram onto a Windows-only runtime, blocking Linux/container deployments. Picking `net8.0` would block the newer language features the WPF apps use. The split is the right shape; the bookkeeping cost is small.

22. OpenTelemetry tracing across the spike path.
    - Status: DONE (2026-05-31).
    - Added a shared `StructureTelemetry.Source` (`ActivitySource` named `NeuralResonanceEngine.Structures`) in `_SharedRuntime`, linked into every structure project via `Directory.Build.props`. The `spike.receive` and `structure.tick` endpoints now start activities with `structure.id`, `tick`, and `spikes.outbound` tags. Consumers attach via `ActivityListener` or any OpenTelemetry SDK without per-service wiring.

## First Implementation Slice

Items 1, 2, 4, 5 - the highest-value cluster local to a small number of files.

## Progress

2026-05-31 - First slice complete.
- Item 1 verified already present in the codebase; entry updated.
- Item 2 landed: split locks so `/drain` and `/top` no longer wait on `/tick`.
- Item 4 landed: structure-side JSON now goes through a source-generated context.
- Item 5 landed: category helpers reduced to a single byte-array lookup + bit test.
- DNNE solution builds clean (0 warnings, 0 errors); 136/136 tests pass.

2026-05-31 - Second slice complete.
- Item 3 partial: synapse persistence is now off-lock via a coalescing background writer; protobuf format swap remains planned.
- Item 9 landed: in-place `ClampInPlace` removes the per-spike `NeuromodState` allocation from `validate_spike`.
- Item 11 partial: `_lastPrunedTick` race closed with `CompareExchange`; `.Keys` snapshot kept (no zero-alloc alternative).
- Item 13 acknowledged: hot-path allocation fixed; rename deferred.
- Item 14 landed: spike endpoint declares `Accepts<byte[]>` to match the wire format.
- Item 15 landed (folded into item 4).
- Item 16 landed: handlers captured as fields, unsubscribed in `Dispose`, and the synapse store is disposed so the background writer drains.
- DNNE solution builds clean (0 warnings, 0 errors); 136/136 tests pass.

Remaining work: items 3 (protobuf swap), 6, 7, 8, 10, 12, 17, 18, 19, 20, 21, 22.

2026-05-31 - Third slice complete.
- Item 6 landed: `HemisphereNormalized` cached on `ServiceInstance`; 11 per-dispatch callsites updated.
- Item 7 landed: `drainedNeuronIds` switched to a `HashSet<(string Hem, string Id)>` so the per-spike string concat is skipped on duplicates; the formatted string is built only once per unique drained neuron.
- Item 8 landed: `MaybeAutoHealServicesAsync` prunes stale `InstanceKey` entries when the dict exceeds catalog size.
- Item 10 deferred: needs a focused null-safety pass across dozens of `ModulationContext` dereferences.
- Item 12 landed: frame stream times each flush and skips a frame after three consecutive slow flushes.
- Item 17 verified: `tickWallSamples` already trims to 256 per enqueue.
- Item 18 landed: synapse-store rename uses `File.Replace` for atomic same-volume swap.
- Item 19 deferred: gRPC bidi streaming warrants its own design session.
- Item 20 landed: opt-in shared-secret middleware via `NRE_STRUCTURE_SHARED_SECRET` env var, paired on both sides.
- Item 21 marked won't-fix: WPF requires `-windows` TFM; structure services should stay cross-platform-capable.
- Item 22 landed: `StructureTelemetry.Source` ActivitySource on receive + tick endpoints, propagated via `Directory.Build.props`.
- Protobuf format swap (remainder of item 3) remains planned; changes on-disk format and needs migration.
- DNNE solution builds clean (0 warnings, 0 errors); 136/136 tests pass.

All actionable items from the audit are now resolved (DONE, ACKNOWLEDGED, WON'T FIX with reason, or DEFERRED with reason).

## Biological Rule

No biological behavior is added or changed by this entry. All work is performance, correctness, and observability inside the existing brain-faithful contracts.

using NeuralResonanceEngine.Shared.Contracts;
using System.Text.Json;

internal sealed record FramePayload(
    JsonElement State,
    JsonElement? ConnectomeReport,
    BrainSnapshot? LatestSnapshot,
    IReadOnlyList<RuntimeLogEntry> OutputLog,
    IReadOnlyList<RuntimeLogEntry> SpikeLog,
    IReadOnlyList<DispatchedSpikeTrace> DispatchSpikes);

internal sealed class FramePayloadFactory
{
    public FramePayload Create(
        SimulationState state,
        SnapshotStore store,
        AutoProfileSettings? autoProfile,
        long outputSinceMs,
        long spikeSinceMs,
        long dispatchSinceMs,
        bool includeConnectome,
        int maxOutputLogEntries,
        int maxSpikeLogEntries,
        int maxDispatchSpikeEntries,
        out long nextOutputSinceMs,
        out long nextSpikeSinceMs,
        out long nextDispatchSinceMs)
    {
        var boundedOutputEntries = Math.Max(0, maxOutputLogEntries);
        var boundedSpikeEntries = Math.Max(0, maxSpikeLogEntries);
        var boundedDispatchEntries = Math.Max(0, maxDispatchSpikeEntries);

        var outputLogAll = state.GetOutputLogSince(outputSinceMs, boundedOutputEntries);
        var spikeLogAll = state.GetSpikeLogSince(spikeSinceMs, boundedSpikeEntries);
        var dispatchFetchBudget = boundedDispatchEntries <= 0
            ? 0
            : Math.Min(20_000, Math.Max(boundedDispatchEntries, boundedDispatchEntries * 4));
        var dispatchSpikesAll = state.GetDispatchedSpikesSince(dispatchSinceMs, dispatchFetchBudget);

        nextOutputSinceMs = GetMaxWallClock(outputLogAll, outputSinceMs);
        nextSpikeSinceMs = GetMaxWallClock(spikeLogAll, spikeSinceMs);
        nextDispatchSinceMs = GetMaxDispatchWallClock(dispatchSpikesAll, dispatchSinceMs);

        var outputLog = CapTail(outputLogAll, boundedOutputEntries);
        var spikeLog = CapTail(spikeLogAll, boundedSpikeEntries);
        var dispatchSpikes = CapDispatchSpikesFairly(dispatchSpikesAll, boundedDispatchEntries);
        var diagnostics = JsonSerializer.SerializeToElement(state.ToDiagnostics(autoProfile));
        JsonElement? connectomeReport = null;
        if (includeConnectome)
        {
            connectomeReport = JsonSerializer.SerializeToElement(state.GetBiologicalConnectomeReport());
        }

        return new FramePayload(
            diagnostics,
            connectomeReport,
            store.GetLatest(),
            outputLog,
            spikeLog,
            dispatchSpikes);
    }

    private static IReadOnlyList<T> CapTail<T>(IReadOnlyList<T> entries, int maxEntries)
    {
        if (entries.Count == 0 || maxEntries <= 0 || entries.Count <= maxEntries)
        {
            return entries;
        }

        var start = entries.Count - maxEntries;
        var result = new List<T>(maxEntries);
        for (var i = start; i < entries.Count; i++)
        {
            result.Add(entries[i]);
        }

        return result;
    }

    private static IReadOnlyList<DispatchedSpikeTrace> CapDispatchSpikesFairly(
        IReadOnlyList<DispatchedSpikeTrace> entries,
        int maxEntries)
    {
        if (entries.Count == 0 || maxEntries <= 0 || entries.Count <= maxEntries)
        {
            return entries;
        }

        var selected = new bool[entries.Count];
        var selectedCount = 0;
        var seenStructures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Reserve one recent trace for each source/target structure before filling
        // the remainder by recency. This keeps quiet mid-line circuits visible when
        // cortex or sensory streams produce a dense burst in the same frame window.
        for (var i = entries.Count - 1; i >= 0 && selectedCount < maxEntries; i--)
        {
            var trace = entries[i];
            if (seenStructures.Add(trace.SourceStructure.ToString()))
            {
                if (!selected[i])
                {
                    selected[i] = true;
                    selectedCount++;
                }

                if (selectedCount >= maxEntries)
                {
                    break;
                }
            }

            if (seenStructures.Add(trace.TargetStructure.ToString()))
            {
                if (!selected[i])
                {
                    selected[i] = true;
                    selectedCount++;
                }
            }
        }

        for (var i = entries.Count - 1; i >= 0 && selectedCount < maxEntries; i--)
        {
            if (selected[i])
            {
                continue;
            }

            selected[i] = true;
            selectedCount++;
        }

        var result = new List<DispatchedSpikeTrace>(selectedCount);
        for (var i = 0; i < entries.Count; i++)
        {
            if (selected[i])
            {
                result.Add(entries[i]);
            }
        }

        return result;
    }

    private static long GetMaxDispatchWallClock(IReadOnlyList<DispatchedSpikeTrace> entries, long fallback)
    {
        if (entries.Count == 0)
        {
            return fallback;
        }

        var max = fallback;
        for (var i = 0; i < entries.Count; i++)
        {
            if (entries[i].WallClockUnixMs > max)
            {
                max = entries[i].WallClockUnixMs;
            }
        }

        return max;
    }

    private static long GetMaxWallClock(IReadOnlyList<RuntimeLogEntry> entries, long fallback)
    {
        if (entries.Count == 0)
        {
            return fallback;
        }

        var max = fallback;
        for (var i = 0; i < entries.Count; i++)
        {
            if (entries[i].WallClockUnixMs > max)
            {
                max = entries[i].WallClockUnixMs;
            }
        }

        return max;
    }
}

using System.Diagnostics;

// Read-only wall-time evidence for a fresh isolated run. These clocks must never
// influence integration, event order, learning, deadlines or biological time.
internal enum StructurePerformancePhase { TickTotal, DueInputs, CellDynamics, ContactLearning, SpikeRouting, Diagnostics }
internal sealed record StructurePerformanceSample(string Phase, long Calls, long StopwatchTicks, double Milliseconds);
internal sealed class StructurePerformanceEvidence
{
    private readonly long[] _ticks = new long[Enum.GetValues<StructurePerformancePhase>().Length];
    private readonly long[] _calls = new long[Enum.GetValues<StructurePerformancePhase>().Length];
    internal long Start() => Stopwatch.GetTimestamp();
    internal void End(StructurePerformancePhase phase, long start)
    {
        _ticks[(int)phase] += Stopwatch.GetTimestamp() - start;
        ++_calls[(int)phase];
    }
    // Called under the engine lock, or by the single-threaded probe between ticks.
    internal StructurePerformanceSample[] Capture() => Enum.GetValues<StructurePerformancePhase>()
        .Select(p => new StructurePerformanceSample(p.ToString(), _calls[(int)p], _ticks[(int)p],
            _ticks[(int)p] * 1000d / Stopwatch.Frequency)).ToArray();
}

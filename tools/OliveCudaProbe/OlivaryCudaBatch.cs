// Isolated resident membrane candidate. No live scheduler, receptor, ATP model
// or distributed event publisher selects this class. A failed lane prevents the
// entire public batch from committing; no automatic CPU fallback or state reset.
internal sealed partial class OlivaryCudaBatch : IDisposable
{
    internal const string Version = "isolated-olive-cuda-double-v1";
    internal const double Guard = -12345.625;
    private readonly CudaDriverProbe _cuda;
    private readonly nint _kernel;
    private readonly int _owner = Environment.CurrentManagedThreadId;
    private readonly OlivaryCellIdentity[] _ids;
    private readonly OlivaryParameters[] _parameters;
    private readonly Dictionary<OlivaryCellIdentity,int> _lanes = new();
    private readonly double[] _time, _load, _synaptic, _parameterData;
    private readonly long[] _soma, _axon;
    private double[] _lastInputs;
    private ulong _state, _work, _parameterBuffer, _inputBuffer, _reportBuffer;
    private bool _disposed;
    internal int Count => _ids.Length;

    internal OlivaryCudaBatch(CudaDriverProbe cuda, nint kernel, IReadOnlyList<OlivaryCellSeed> seeds)
    {
        if (seeds.Count < 1 || seeds.Count > 32768) throw new ArgumentOutOfRangeException(nameof(seeds));
        // Reuse the already qualified seed validator; it performs no integration.
        var validated = new OlivaryCpuBatch(seeds);
        var cells = seeds.Select(s => validated.Snapshot(s.Identity)).ToArray();
        _cuda = cuda; _kernel = kernel;
        _ids = cells.Select(c => c.Identity).ToArray(); _parameters = cells.Select(c => c.Parameters).ToArray();
        _time = cells.Select(c => c.TimeMs).ToArray(); _load = cells.Select(c => c.AdditionalIonicLoad).ToArray();
        _synaptic = cells.Select(c => c.SynapticCurrent).ToArray(); _soma = cells.Select(c => c.SomaticSpikes).ToArray();
        _axon = cells.Select(c => c.AxonalSpikes).ToArray();
        var state = Buffer(14); _parameterData = Buffer(5); _lastInputs = Buffer(9);
        for (int lane = 0; lane < Count; ++lane)
        {
            _lanes.Add(_ids[lane], lane);
            for (int component = 0; component < 14; ++component) state[At(component,lane)] = cells[lane].State[component];
            var p = _parameters[lane]; double[] values = [p.LowCalcium,p.HighCalcium,p.Hcn,p.CalciumPotassium,p.AxonalPotassium];
            for (int component = 0; component < 5; ++component) _parameterData[At(component,lane)] = values[component];
        }
        try
        {
            _state = Allocate(state); _work = Allocate(state); _parameterBuffer = Allocate(_parameterData);
            _inputBuffer = Allocate(_lastInputs); _reportBuffer = Allocate(Buffer(5));
        }
        catch { Dispose(); throw; }
    }

    private int At(int component,int lane) => 8 + component * Count + lane;
    private double[] Buffer(int components)
    {
        var result = new double[checked(components * Count + 16)];
        Array.Fill(result,Guard,0,8); Array.Fill(result,Guard,result.Length-8,8); return result;
    }
    private ulong Allocate(double[] data)
    {
        ulong address = _cuda.Allocate(data.Length);
        try { _cuda.Upload(address,data); return address; }
        catch { _cuda.Free(address); throw; }
    }
    private void Require()
    {
        if (Environment.CurrentManagedThreadId != _owner) throw new InvalidOperationException("CUDA batch belongs to its creating thread.");
        ObjectDisposedException.ThrowIf(_disposed,this);
    }
    private static void CheckGuards(double[] data)
    {
        if (data.Take(8).Concat(data.TakeLast(8)).Any(v => BitConverter.DoubleToInt64Bits(v) != BitConverter.DoubleToInt64Bits(Guard)))
            throw new InvalidOperationException("CUDA buffer guard changed.");
    }

    internal OlivaryCellSeed[] SnapshotAll()
    {
        Require(); var data = _cuda.Download(_state); CheckGuards(data);
        var result = new OlivaryCellSeed[Count];
        for (int lane = 0; lane < Count; ++lane)
        {
            var state = new double[14];
            for (int component = 0; component < 14; ++component) state[component] = data[At(component,lane)];
            result[lane] = new(_ids[lane],_time[lane],_parameters[lane],state,_soma[lane],_axon[lane],_load[lane],_synaptic[lane]);
        }
        return result;
    }

    internal void VerifyReadOnlyBuffersAndGuards()
    {
        Require();
        foreach (ulong address in new[] {_state,_work,_parameterBuffer,_inputBuffer,_reportBuffer}) CheckGuards(_cuda.Download(address));
        if (!_cuda.Download(_parameterBuffer).AsSpan().SequenceEqual(_parameterData))
            throw new InvalidOperationException("Immutable device parameters changed.");
        if (!_cuda.Download(_inputBuffer).AsSpan().SequenceEqual(_lastInputs))
            throw new InvalidOperationException("Read-only device inputs changed.");
    }

    internal OlivaryBatchAdvance[] Advance(IReadOnlyList<OlivaryBatchStep> request)
    {
        CostBegin(); // COST_HOOK
        Require(); var steps = request.ToArray();
        if (steps.Length > Count) throw new ArgumentException("Too many requested cells.");
        var input = Buffer(9); var lanes = new int[steps.Length]; var seen = new HashSet<int>();
        for (int i = 0; i < steps.Length; ++i)
        {
            var step = steps[i]; step.Input.Validate();
            if (!_lanes.TryGetValue(step.Identity,out int lane) || !seen.Add(lane)) throw new ArgumentException("Unknown or repeated identity.");
            double end = step.FromMs + step.DurationMs;
            if (step.FromMs != _time[lane] || !double.IsFinite(step.DurationMs) || step.DurationMs <= 0 || step.DurationMs > 1 ||
                !double.IsFinite(end) || end <= step.FromMs || !double.IsFinite(step.MaximumInternalStepMs) ||
                step.MaximumInternalStepMs <= 0 || step.MaximumInternalStepMs > OlivaryMembrane.MaximumInternalStepMs)
                throw new ArgumentException("Stale, gapped or invalid cell interval.");
            lanes[i] = lane; var v = step.Input;
            double[] values = [1,step.DurationMs,step.MaximumInternalStepMs,v.DendriticAmpa,v.DendriticGaba,v.SomaticGaba,
                v.SomaticCurrent,v.DendriticCurrent,v.MetabolicPotassium];
            for (int component = 0; component < 9; ++component) input[At(component,lane)] = values[component];
        }
        if (steps.Length == 0) return [];
        CostMark(0); // COST_HOOK
        _cuda.Upload(_inputBuffer,input);
        _lastInputs = input;
        CostMark(1); // COST_HOOK
        _cuda.LaunchOlive(_kernel,_state,_work,_parameterBuffer,_inputBuffer,_reportBuffer,Count);
        CostMark(2); // COST_HOOK
        var raw = _cuda.Download(_reportBuffer);
        CostMark(3); // COST_HOOK
        CheckGuards(raw);
        for (int lane = 0; lane < Count; ++lane)
            if (raw[At(0,lane)] != 0) throw new InvalidOperationException($"CUDA membrane condition failed at {_ids[lane]}: code {raw[At(0,lane)]:R}; public state not committed.");
        var results = new OlivaryBatchAdvance[steps.Length]; var soma = new long[steps.Length]; var axon = new long[steps.Length];
        for (int i = 0; i < steps.Length; ++i)
        {
            int lane = lanes[i]; double s = raw[At(1,lane)], a = raw[At(2,lane)], load = raw[At(3,lane)], current = raw[At(4,lane)];
            if (!double.IsFinite(s) || s < 0 || s > 100000 || Math.Truncate(s) != s || a is not (0 or 1) ||
                !double.IsFinite(load) || load < 0 || !double.IsFinite(current)) throw new InvalidOperationException("Invalid GPU interval report.");
            soma[i] = checked(_soma[lane] + (long)s); axon[i] = checked(_axon[lane] + (long)a);
            results[i] = new(steps[i].Identity,steps[i].FromMs,steps[i].FromMs+steps[i].DurationMs,new((long)s,(long)a,load,current,a==1));
        }
        var ordered = results.OrderBy(r => r.EndMs).ThenBy(r => r.Identity.Instance,StringComparer.Ordinal)
            .ThenBy(r => r.Identity.Cell,StringComparer.Ordinal).ToArray();
        // Commit only after every lane/report and counter has been checked.
        (_state,_work) = (_work,_state);
        for (int i = 0; i < steps.Length; ++i)
        {
            int lane = lanes[i]; _time[lane] = results[i].EndMs; _soma[lane] = soma[i]; _axon[lane] = axon[i];
            _load[lane] = results[i].Metrics.AdditionalIonicLoad; _synaptic[lane] = results[i].Metrics.SynapticCurrent;
        }
        CostMark(4); // COST_HOOK
        return ordered;
    }

    partial void CostBegin(); // COST_HOOK
    partial void CostMark(int phase); // COST_HOOK

    public void Dispose()
    {
        if (_disposed) return;
        if (Environment.CurrentManagedThreadId != _owner) throw new InvalidOperationException("Dispose on the creating thread.");
        _disposed = true;
        foreach (ulong address in new[] {_state,_work,_parameterBuffer,_inputBuffer,_reportBuffer}) if (address != 0) _cuda.Free(address);
    }
}

// Diagnostic membrane boundary only. Receptors, contacts, energy and the brain
// scheduler remain outside it. No runtime host constructs this type.
internal readonly record struct OlivaryCellIdentity(string Instance,string Cell)
{
    internal void Validate()
    {
        if(string.IsNullOrWhiteSpace(Instance)||string.IsNullOrWhiteSpace(Cell))
            throw new ArgumentException("Explicit structure instance and neuron identity required.");
    }
}

internal sealed record OlivaryCellSeed(OlivaryCellIdentity Identity,double TimeMs,OlivaryParameters Parameters,
    double[] State,long SomaticSpikes,long AxonalSpikes,double AdditionalIonicLoad,double SynapticCurrent)
{
    internal static OlivaryCellSeed Capture(OlivaryCellIdentity identity,double time,OlivaryMembrane cell)=>
        new(identity,time,cell.Parameters,cell.Snapshot(),cell.SomaticSpikes,cell.AxonalSpikes,cell.AdditionalIonicLoad,cell.SynapticCurrent);
}

internal readonly record struct OlivaryBatchStep(OlivaryCellIdentity Identity,double FromMs,double DurationMs,
    OlivaryInput Input,double MaximumInternalStepMs=OlivaryMembrane.MaximumInternalStepMs);
internal readonly record struct OlivaryBatchAdvance(OlivaryCellIdentity Identity,double FromMs,double EndMs,OlivaryStepMetrics Metrics);

internal sealed class OlivaryCpuBatch
{
    internal const string Version="isolated-olive-cpu-batch-v1";
    readonly int _owner=Environment.CurrentManagedThreadId;
    readonly OlivaryCellIdentity[] _ids;
    readonly OlivaryParameters[] _parameters;
    readonly Dictionary<OlivaryCellIdentity,int> _lanes=new();
    // Structure-of-arrays: component * cell count + lane. Never cell-averaged.
    readonly double[] _state,_work,_time,_load,_synaptic;
    readonly long[] _soma,_axon;
    internal int Count=>_ids.Length;

    internal OlivaryCpuBatch(IReadOnlyList<OlivaryCellSeed> cells)
    {
        if(cells.Count<1||cells.Count>1_000_000)throw new ArgumentOutOfRangeException(nameof(cells));
        _ids=new OlivaryCellIdentity[cells.Count];_parameters=new OlivaryParameters[cells.Count];
        _state=new double[checked(cells.Count*OlivaryBatchKernel.Count)];_work=new double[_state.Length];
        _time=new double[cells.Count];_load=new double[cells.Count];_synaptic=new double[cells.Count];
        _soma=new long[cells.Count];_axon=new long[cells.Count];
        for(int lane=0;lane<cells.Count;lane++)
        {
            var cell=cells[lane];cell.Identity.Validate();cell.Parameters.Validate();
            if(!double.IsFinite(cell.TimeMs)||cell.TimeMs<0||cell.State.Length!=OlivaryBatchKernel.Count||
                cell.SomaticSpikes<0||cell.AxonalSpikes<0||!double.IsFinite(cell.AdditionalIonicLoad)||
                cell.AdditionalIonicLoad<0||!double.IsFinite(cell.SynapticCurrent))throw new ArgumentException("Invalid membrane seed.");
            if(!_lanes.TryAdd(cell.Identity,lane))throw new ArgumentException("Duplicate cell identity.");
            for(int component=0;component<OlivaryBatchKernel.Count;component++)
            {
                double value=cell.State[component];
                if(!double.IsFinite(value)||component==11&&value<0||component>=3&&component!=11&&(value< -1e-8||value>1+1e-8))
                    throw new ArgumentException("Invalid membrane state.");
                _state[component*cells.Count+lane]=value;
            }
            _ids[lane]=cell.Identity;_parameters[lane]=cell.Parameters;_time[lane]=cell.TimeMs;
            _soma[lane]=cell.SomaticSpikes;_axon[lane]=cell.AxonalSpikes;
            _load[lane]=cell.AdditionalIonicLoad;_synaptic[lane]=cell.SynapticCurrent;
        }
    }

    void RequireOwner()
    {
        if(Environment.CurrentManagedThreadId!=_owner)throw new InvalidOperationException("Batch state belongs to its creating thread.");
    }
    internal OlivaryCellSeed Snapshot(OlivaryCellIdentity identity)
    {
        RequireOwner();int lane=_lanes[identity];var state=new double[OlivaryBatchKernel.Count];
        for(int component=0;component<state.Length;component++)state[component]=_state[component*Count+lane];
        return new(identity,_time[lane],_parameters[lane],state,_soma[lane],_axon[lane],_load[lane],_synaptic[lane]);
    }

    // All requested lanes validate first. Numerical failure leaves every public
    // state, clock and counter unchanged; staging bytes are not observable state.
    // Each lane receives its own interval and constant input, as Advance requires.
    internal OlivaryBatchAdvance[] Advance(IReadOnlyList<OlivaryBatchStep> requested,int workers=1)
    {
        RequireOwner();if(workers<1||workers>64)throw new ArgumentOutOfRangeException(nameof(workers));
        var steps=requested.ToArray();if(steps.Length>Count)throw new ArgumentException("Too many cell intervals.");
        var lanes=new int[steps.Length];var seen=new HashSet<int>();
        for(int index=0;index<steps.Length;index++)
        {
            var step=steps[index];step.Input.Validate();
            if(!_lanes.TryGetValue(step.Identity,out int lane))throw new ArgumentException("Unknown cell identity.");
            if(!seen.Add(lane))throw new ArgumentException("A cell occurs twice in this batch.");
            double end=step.FromMs+step.DurationMs;
            if(step.FromMs!=_time[lane]||!double.IsFinite(step.DurationMs)||step.DurationMs<=0||step.DurationMs>1||
                !double.IsFinite(end)||end<=step.FromMs||!double.IsFinite(step.MaximumInternalStepMs)||
                step.MaximumInternalStepMs<=0||step.MaximumInternalStepMs>OlivaryMembrane.MaximumInternalStepMs)
                throw new ArgumentException("Interval is stale, gapped, non-finite or outside the membrane bounds.");
            lanes[index]=lane;
        }
        var results=new OlivaryBatchAdvance[steps.Length];var soma=new long[steps.Length];var axon=new long[steps.Length];
        void Process(int index)
        {
            int lane=lanes[index];var step=steps[index];Span<double> state=stackalloc double[OlivaryBatchKernel.Count];
            for(int component=0;component<state.Length;component++)state[component]=_state[component*Count+lane];
            var metrics=OlivaryBatchKernel.Advance(state,step.DurationMs,_parameters[lane],step.Input,step.MaximumInternalStepMs);
            soma[index]=checked(_soma[lane]+metrics.SomaticSpikes);axon[index]=checked(_axon[lane]+metrics.AxonalSpikes);
            for(int component=0;component<state.Length;component++)_work[component*Count+lane]=state[component];
            results[index]=new(step.Identity,step.FromMs,step.FromMs+step.DurationMs,metrics);
        }
        if(workers==1)for(int index=0;index<steps.Length;index++)Process(index);
        else Parallel.For(0,steps.Length,new ParallelOptions{MaxDegreeOfParallelism=workers},Process);
        for(int index=0;index<steps.Length;index++)
        {
            int lane=lanes[index];var result=results[index];
            for(int component=0;component<OlivaryBatchKernel.Count;component++)_state[component*Count+lane]=_work[component*Count+lane];
            _time[lane]=result.EndMs;_soma[lane]=soma[index];_axon[lane]=axon[index];
            _load[lane]=result.Metrics.AdditionalIonicLoad;_synaptic[lane]=result.Metrics.SynapticCurrent;
        }
        // Execution order must not become event order. These are membrane interval
        // reports, NOT interpolated spike times or distributed SpikeMessages.
        Array.Sort(results,(a,b)=>
        {
            int order=a.EndMs.CompareTo(b.EndMs);if(order!=0)return order;
            order=StringComparer.Ordinal.Compare(a.Identity.Instance,b.Identity.Instance);
            return order!=0?order:StringComparer.Ordinal.Compare(a.Identity.Cell,b.Identity.Cell);
        });
        return results;
    }
}

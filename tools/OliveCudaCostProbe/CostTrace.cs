#if PROFILE_HOST
using System.Diagnostics;
using System.Runtime.InteropServices;

// Compiled only into the host/device diagnostic variants. In control and all
// existing probes the optional partial methods and their calls are erased.
internal sealed partial class OlivaryCudaBatch
{
    internal CostTrace? CostTrace { get; set; }
    partial void CostBegin() => CostTrace?.Begin();
    partial void CostMark(int phase) => CostTrace?.Mark(phase);
}

internal sealed unsafe partial class CudaDriverProbe
{
    internal CostTrace? CostTrace { get; set; }
    partial void CostKernelBegin() => CostTrace?.KernelBegin();
    partial void CostKernelLaunched() => CostTrace?.KernelLaunched();
    partial void CostKernelSynchronized() => CostTrace?.KernelSynchronized();
#if PROFILE_DEVICE
    internal nint CreateCostEvent()
    { Require(); DeviceCheck(CostEvents.Create(out nint value,0),"cuEventCreate"); return value; }
    internal void RecordCostEvent(nint value)
    { Require(); DeviceCheck(CostEvents.Record(value,0),"cuEventRecord default stream"); }
    internal float ReadCostEvents(nint start,nint end)
    {
        Require(); DeviceCheck(CostEvents.Elapsed(out float ms,start,end),"cuEventElapsedTime");
        if(!float.IsFinite(ms) || ms<0)throw new InvalidOperationException("Invalid event elapsed time.");
        return ms;
    }
    internal void DestroyCostEvent(nint value)
    {
        // Reclaim owned events even while unwinding a failed device operation.
        if(Environment.CurrentManagedThreadId!=_thread || _context==0)
            throw new InvalidOperationException("Cost event context no longer owned.");
        Check(CostEvents.Destroy(value),"cuEventDestroy_v2");
    }
    private static class CostEvents
    {
        [DllImport("nvcuda.dll",EntryPoint="cuEventCreate")] internal static extern int Create(out nint value,uint flags);
        [DllImport("nvcuda.dll",EntryPoint="cuEventRecord")] internal static extern int Record(nint value,nint stream);
        [DllImport("nvcuda.dll",EntryPoint="cuEventElapsedTime")] internal static extern int Elapsed(out float ms,nint start,nint end);
        [DllImport("nvcuda.dll",EntryPoint="cuEventDestroy_v2")] internal static extern int Destroy(nint value);
    }
#endif
}

internal sealed class CostTrace : IDisposable
{
    private readonly CudaDriverProbe _cuda;
    private readonly long[,] _phases, _kernel;
    private long _last;
    private int _frame=-1, _phase=-1;
#if PROFILE_DEVICE
    private readonly nint[] _events;
#endif
    internal int EventsCreated { get; private set; }
    internal int EventsDestroyed { get; private set; }
    internal CostTrace(CudaDriverProbe cuda,int frames)
    {
        _cuda=cuda; _phases=new long[frames,5]; _kernel=new long[frames,3];
#if PROFILE_DEVICE
        _events=new nint[checked(frames*2)];
        try { for(int i=0;i<_events.Length;++i){_events[i]=cuda.CreateCostEvent();++EventsCreated;} }
        catch { Dispose(); throw; }
#endif
    }
    internal void Begin()
    {
        if((_frame>=0 && _phase!=4) || ++_frame>=_phases.GetLength(0))
            throw new InvalidOperationException("Incomplete/extra profiled call.");
        _phase=-1; _last=Stopwatch.GetTimestamp();
    }
    internal void Mark(int phase)
    {
        long now=Stopwatch.GetTimestamp();
        if(phase!=_phase+1 || phase<0 || phase>4)throw new InvalidOperationException("Unexpected phase order.");
        _phases[_frame,phase]=now-_last; _last=now; _phase=phase;
    }
    internal void KernelBegin()
    {
        _kernel[_frame,0]=Stopwatch.GetTimestamp();
#if PROFILE_DEVICE
        _cuda.RecordCostEvent(_events[_frame*2]);
#endif
    }
    internal void KernelLaunched()
    {
#if PROFILE_DEVICE
        _cuda.RecordCostEvent(_events[_frame*2+1]);
#endif
        _kernel[_frame,1]=Stopwatch.GetTimestamp();
    }
    internal void KernelSynchronized() => _kernel[_frame,2]=Stopwatch.GetTimestamp();
    internal object[] Capture()
    {
        if(_frame!=_phases.GetLength(0)-1 || _phase!=4)throw new InvalidOperationException("Incomplete cost trace.");
        return Enumerable.Range(0,_phases.GetLength(0)).Select(f=>
        {
            double? deviceMs=null;
#if PROFILE_DEVICE
            deviceMs=_cuda.ReadCostEvents(_events[f*2],_events[f*2+1]);
#endif
            return (object)new {frame=f,phaseTicks=Enumerable.Range(0,5).Select(p=>_phases[f,p]).ToArray(),
                launchTicks=_kernel[f,1]-_kernel[f,0],syncTicks=_kernel[f,2]-_kernel[f,1],deviceEventMs=deviceMs};
        }).ToArray();
    }
    public void Dispose()
    {
#if PROFILE_DEVICE
        List<Exception>? errors=null;
        for(int i=0;i<_events.Length;++i)if(_events[i]!=0)
        {
            try {_cuda.DestroyCostEvent(_events[i]);++EventsDestroyed;}
            catch(Exception ex){(errors??=[]).Add(ex);}
            finally {_events[i]=0;}
        }
        if(errors is not null)throw new AggregateException("Cost event cleanup failed.",errors);
#endif
    }
}
#endif

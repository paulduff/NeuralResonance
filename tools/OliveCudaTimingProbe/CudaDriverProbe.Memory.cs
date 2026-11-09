using System.Runtime.InteropServices;

// Timing-probe-only observation; does not change the qualified arithmetic path.
internal sealed unsafe partial class CudaDriverProbe
{
    internal object MemoryEvidence()
    {
        Require(); DeviceCheck(MemoryApi.GetInfo(out nuint free,out nuint total),"cuMemGetInfo_v2");
        if (free>total || total==0) throw new InvalidOperationException("Invalid device memory report.");
        return new {freeBytes=(ulong)free,totalBytes=(ulong)total,
            ownedBufferBytes=_allocations.Values.Sum(n=>(long)n*8),sampledUtc=DateTime.UtcNow};
    }
    internal void RequireMemoryHeadroom(long requiredBytes)
    {
        Require(); DeviceCheck(MemoryApi.GetInfo(out nuint free,out _),"cuMemGetInfo_v2");
        if (requiredBytes<0 || (ulong)free<(ulong)requiredBytes+1073741824UL)
            throw new InvalidOperationException("Timing study requires one GiB of reported free headroom beyond its buffers.");
    }
    private static class MemoryApi
    {
        [DllImport("nvcuda.dll",EntryPoint="cuMemGetInfo_v2")]
        internal static extern int GetInfo(out nuint free,out nuint total);
    }
}

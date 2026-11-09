using System.Runtime.InteropServices;
using System.Text;

[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.System32)]

internal sealed record CudaDeviceEvidence(string Name, int Ordinal, int DriverVersion, int Architecture, ulong TotalMemoryBytes);

// Deliberately probe-local, synchronous and thread-owned. No application host
// selects it. One explicit context is destroyed on exit; no device reset.
internal sealed unsafe partial class CudaDriverProbe : IDisposable
{
    private readonly int _thread = Environment.CurrentManagedThreadId;
    private readonly nint _previous;
    private nint _context, _module, _kernelFunction;
    private readonly Dictionary<ulong,int> _allocations = new();
    private bool _faulted;
    internal CudaDeviceEvidence Device { get; }
    internal CudaDriverProbe(int ordinal)
    {
        if (!OperatingSystem.IsWindows() || !Environment.Is64BitProcess) throw new PlatformNotSupportedException("Windows x64 required.");
        Check(Cu.Init(0),"cuInit"); Check(Cu.DeviceCount(out int count),"cuDeviceGetCount");
        if(ordinal<0 || ordinal>=count) throw new ArgumentOutOfRangeException(nameof(ordinal));
        Check(Cu.Device(out int device,ordinal),"cuDeviceGet"); Check(Cu.DriverVersion(out int driver),"cuDriverGetVersion");
        var name=new StringBuilder(256); Check(Cu.DeviceName(name,name.Capacity,device),"cuDeviceGetName");
        Check(Cu.Attribute(out int major,75,device),"compute capability major"); Check(Cu.Attribute(out int minor,76,device),"compute capability minor");
        Check(Cu.TotalMemory(out nuint memory,device),"cuDeviceTotalMem");
        Device=new(name.ToString(),ordinal,driver,checked(major*10+minor),(ulong)memory);
        Check(Cu.Current(out _previous),"cuCtxGetCurrent");
        Check(Cu.Create(out _context,0,device),"cuCtxCreate_v2");
    }
    private static void Check(int result,string operation)
    {
        if(result==0)return;
        Cu.ErrorName(result,out var name); Cu.ErrorString(result,out var detail);
        throw new InvalidOperationException($"{operation}: CUDA {result} {Marshal.PtrToStringAnsi(name)}: {Marshal.PtrToStringAnsi(detail)}");
    }
    private void Require()
    {
        if(Environment.CurrentManagedThreadId!=_thread)throw new InvalidOperationException("CUDA probe belongs to its creating OS thread.");
        ObjectDisposedException.ThrowIf(_context==0,this);
        if(_faulted)throw new InvalidOperationException("Faulted CUDA probe cannot continue.");
        Check(Cu.Current(out var current),"cuCtxGetCurrent");
        if(current!=_context)throw new InvalidOperationException("Current CUDA context changed outside its owner.");
    }
    private void DeviceCheck(int result,string operation)
    {
        if(result!=0)_faulted=true;
        Check(result,operation);
    }
    internal nint LoadKernel(byte[] ptx,string name)
    {
        Require(); if(_module!=0)throw new InvalidOperationException("One module per probe context.");
        if(ptx.Length==0 || ptx[^1]!=0)throw new ArgumentException("NUL-terminated PTX required.");
        fixed(byte* data=ptx)DeviceCheck(Cu.LoadModule(out _module,(nint)data),"cuModuleLoadData");
        DeviceCheck(Cu.Function(out var function,_module,name),"cuModuleGetFunction");_kernelFunction=function;return function;
    }
    internal ulong Allocate(int doubles)
    {
        Require();
        if(doubles<1 || doubles>1_000_000 || _allocations.Values.Sum(x=>(long)x)+doubles>8_000_000)
            throw new ArgumentOutOfRangeException(nameof(doubles),"Probe memory bound exceeded.");
        DeviceCheck(Cu.Allocate(out ulong address,checked((nuint)doubles*8)),"cuMemAlloc");
        _allocations.Add(address,doubles);return address;
    }
    internal void Upload(ulong address,double[] data)
    {
        Require();if(!_allocations.TryGetValue(address,out int size)||size!=data.Length)throw new ArgumentException("Whole owned buffer required.");
        fixed(double* pointer=data)DeviceCheck(Cu.Upload(address,(nint)pointer,checked((nuint)size*8)),"cuMemcpyHtoD");
    }
    internal double[] Download(ulong address)
    {
        Require();if(!_allocations.TryGetValue(address,out int size))throw new ArgumentException("Unknown buffer.");
        var result=new double[size];
        fixed(double* pointer=result)DeviceCheck(Cu.Download((nint)pointer,address,checked((nuint)size*8)),"cuMemcpyDtoH");
        return result;
    }
    internal void Launch(nint function,ulong address,int elements)
    {
        Require();if(function!=_kernelFunction||!_allocations.TryGetValue(address,out int size)||elements<1||size!=elements+16)throw new ArgumentException("Require owned kernel and element buffer with both guard regions.");
        nint* arguments=stackalloc nint[2];arguments[0]=(nint)(&address);arguments[1]=(nint)(&elements);
        DeviceCheck(Cu.Launch(function,checked((uint)((elements+255)/256)),1,1,256,1,1,0,0,(nint)arguments,0),"cuLaunchKernel");
        DeviceCheck(Cu.Synchronize(),"cuCtxSynchronize");
    }
    internal void Free(ulong address)
    {
        // Preserve the original failed device call; context destruction will
        // reclaim remaining allocations when the exception unwinds.
        if(_faulted)return;
        Require();if(!_allocations.ContainsKey(address))throw new ArgumentException("Unknown or freed buffer.");
        DeviceCheck(Cu.Free(address),"cuMemFree");_allocations.Remove(address);
    }
    public void Dispose()
    {
        if(_context==0)return;
        if(Environment.CurrentManagedThreadId!=_thread)throw new InvalidOperationException("Dispose the CUDA context on its creating thread.");
        // Context destruction releases its modules and outstanding allocations,
        // including after a failed launch. Restore the previously current context.
        int destroyed=Cu.Destroy(_context);_context=0;_module=0;_allocations.Clear();
        int restored=Cu.SetCurrent(_previous);
        Check(destroyed,"cuCtxDestroy");Check(restored,"restore previous CUDA context");
        Check(Cu.Current(out var current),"verify restored CUDA context");
        if(current!=_previous)throw new InvalidOperationException("CUDA context restoration mismatch.");
    }
    private static class Cu
    {
        private const string Dll="nvcuda.dll";
        [DllImport(Dll,EntryPoint="cuInit")] internal static extern int Init(uint flags);
        [DllImport(Dll,EntryPoint="cuDriverGetVersion")] internal static extern int DriverVersion(out int version);
        [DllImport(Dll,EntryPoint="cuDeviceGetCount")] internal static extern int DeviceCount(out int count);
        [DllImport(Dll,EntryPoint="cuDeviceGet")] internal static extern int Device(out int device,int ordinal);
        [DllImport(Dll,EntryPoint="cuDeviceGetName",CharSet=CharSet.Ansi)] internal static extern int DeviceName(StringBuilder name,int length,int device);
        [DllImport(Dll,EntryPoint="cuDeviceGetAttribute")] internal static extern int Attribute(out int value,int attribute,int device);
        [DllImport(Dll,EntryPoint="cuDeviceTotalMem_v2")] internal static extern int TotalMemory(out nuint bytes,int device);
        [DllImport(Dll,EntryPoint="cuCtxGetCurrent")] internal static extern int Current(out nint context);
        [DllImport(Dll,EntryPoint="cuCtxSetCurrent")] internal static extern int SetCurrent(nint context);
        [DllImport(Dll,EntryPoint="cuCtxCreate_v2")] internal static extern int Create(out nint context,uint flags,int device);
        [DllImport(Dll,EntryPoint="cuCtxDestroy_v2")] internal static extern int Destroy(nint context);
        [DllImport(Dll,EntryPoint="cuCtxSynchronize")] internal static extern int Synchronize();
        [DllImport(Dll,EntryPoint="cuModuleLoadData")] internal static extern int LoadModule(out nint module,nint ptx);
        [DllImport(Dll,EntryPoint="cuModuleGetFunction",CharSet=CharSet.Ansi)] internal static extern int Function(out nint function,nint module,string name);
        [DllImport(Dll,EntryPoint="cuMemAlloc_v2")] internal static extern int Allocate(out ulong address,nuint bytes);
        [DllImport(Dll,EntryPoint="cuMemFree_v2")] internal static extern int Free(ulong address);
        [DllImport(Dll,EntryPoint="cuMemcpyHtoD_v2")] internal static extern int Upload(ulong destination,nint source,nuint bytes);
        [DllImport(Dll,EntryPoint="cuMemcpyDtoH_v2")] internal static extern int Download(nint destination,ulong source,nuint bytes);
        [DllImport(Dll,EntryPoint="cuLaunchKernel")] internal static extern int Launch(nint function,uint gx,uint gy,uint gz,uint bx,uint by,uint bz,uint shared,nint stream,nint arguments,nint extra);
        [DllImport(Dll,EntryPoint="cuGetErrorName")] internal static extern int ErrorName(int error,out nint name);
        [DllImport(Dll,EntryPoint="cuGetErrorString")] internal static extern int ErrorString(int error,out nint text);
    }
}

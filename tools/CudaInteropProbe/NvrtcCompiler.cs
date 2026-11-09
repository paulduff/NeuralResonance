using System.Runtime.InteropServices;
using System.Text;

// Probe-local binding. Absolute DLL paths only; the caller verifies their bytes
// against the frozen NVIDIA dependency manifest before constructing this class.
internal sealed class NvrtcCompiler : IDisposable
{
    private nint _library;
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int VersionDelegate(out int major, out int minor);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int CreateDelegate(out nint program, [MarshalAs(UnmanagedType.LPUTF8Str)] string source,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name, int headers, nint headerSources, nint headerNames);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int CompileDelegate(nint program, int count, nint options);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int SizeDelegate(nint program, out nuint size);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int DataDelegate(nint program, [Out] byte[] data);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int DestroyDelegate(ref nint program);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int ArchitectureCountDelegate(out int count);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int ArchitecturesDelegate([Out] int[] architectures);
    internal (int Major, int Minor) Version { get; }
    internal int[] SupportedArchitectures { get; }

    internal NvrtcCompiler(string absolutePath)
    {
        if (!OperatingSystem.IsWindows() || !Environment.Is64BitProcess || !Path.IsPathFullyQualified(absolutePath))
            throw new PlatformNotSupportedException("This probe requires Windows x64 and a verified absolute compiler path.");
        _library = NativeLibrary.Load(absolutePath, typeof(NvrtcCompiler).Assembly,
            DllImportSearchPath.UseDllDirectoryForDependencies | DllImportSearchPath.System32);
        try
        {
            Check(Get<VersionDelegate>("nvrtcVersion")(out int major, out int minor), "nvrtcVersion"); Version = (major,minor);
            Check(Get<ArchitectureCountDelegate>("nvrtcGetNumSupportedArchs")(out int count), "nvrtcGetNumSupportedArchs");
            if (count <= 0 || count > 1024) throw new InvalidOperationException("Invalid supported-architecture count.");
            SupportedArchitectures = new int[count]; Check(Get<ArchitecturesDelegate>("nvrtcGetSupportedArchs")(SupportedArchitectures), "nvrtcGetSupportedArchs");
        }
        catch { Dispose(); throw; }
    }
    private T Get<T>(string name) where T : Delegate => Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(_library,name));
    private static void Check(int result, string operation) { if (result != 0) throw new InvalidOperationException(operation+" returned NVRTC "+result); }

    internal (byte[] Ptx, string Log, string[] Options) Compile(string source, int architecture)
    {
        ObjectDisposedException.ThrowIf(_library == 0, this);
        if (!SupportedArchitectures.Contains(architecture)) throw new ArgumentException("Compiler does not declare this device architecture.");
        string[] options = [$"--gpu-architecture=compute_{architecture}", "--std=c++17", "--fmad=false", "--ftz=false", "--prec-div=true", "--prec-sqrt=true"];
        Check(Get<CreateDelegate>("nvrtcCreateProgram")(out var program,source,"interop.cu",0,0,0),"nvrtcCreateProgram");
        var strings = new List<nint>(); nint pointers = 0;
        try
        {
            foreach (var option in options) strings.Add(Marshal.StringToCoTaskMemUTF8(option));
            pointers = Marshal.AllocHGlobal(checked(nint.Size * strings.Count)); Marshal.Copy(strings.ToArray(),0,pointers,strings.Count);
            int result=Get<CompileDelegate>("nvrtcCompileProgram")(program,strings.Count,pointers);
            byte[] Read(string prefix)
            {
                Check(Get<SizeDelegate>(prefix+"Size")(program,out nuint size),prefix+"Size");
                var bytes=new byte[checked((int)size)]; Check(Get<DataDelegate>(prefix)(program,bytes),prefix); return bytes;
            }
            string log=Encoding.UTF8.GetString(Read("nvrtcGetProgramLog")).TrimEnd('\0');
            if(result!=0) throw new InvalidOperationException($"NVRTC compilation failed ({result}): {log}");
            return (Read("nvrtcGetPTX"),log,options);
        }
        finally
        {
            foreach(var pointer in strings) Marshal.FreeCoTaskMem(pointer);
            if(pointers!=0) Marshal.FreeHGlobal(pointers);
            Check(Get<DestroyDelegate>("nvrtcDestroyProgram")(ref program),"nvrtcDestroyProgram");
        }
    }
    public void Dispose() { if(_library!=0) {NativeLibrary.Free(_library);_library=0;} }
}

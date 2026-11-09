using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

if(args.Length!=2 || !Path.IsPathFullyQualified(args[0]) || !Path.IsPathFullyQualified(args[1]) || Directory.Exists(args[1]))
    throw new ArgumentException("Supply verified dependency directory and a new absolute output directory.");
string dependency=args[0],output=args[1];Directory.CreateDirectory(output);
var json=new JsonSerializerOptions {WriteIndented=true};
void Save(string name,object value)=>File.WriteAllText(Path.Combine(output,name),JsonSerializer.Serialize(value,json));
const string expectedArchive="8C50A52467826167E0DBE99936140C52D62272BFC5849FE2D6587D050C8C5D29";
var cases=new List<object>();
Save("protocol.json",new {version="cuda-double-interop-v2",counts=new[]{1,255,256,257,4097,65537},
    launchesPerBuffer=7,guardsPerEnd=8,exactArithmeticRequired=true,nativeContextCleanupRequired=true,
    biologicalModelExecuted=false,speedupMeasured=false});
try
{
    string archivePath=Path.Combine(dependency,"nvrtc-13.0.88.zip");
    using(var file=File.OpenRead(archivePath))
        if(file.Length!=318076161 || Convert.ToHexString(SHA256.HashData(file))!=expectedArchive)throw new InvalidOperationException("Unverified NVIDIA compiler archive.");
    using var archive=ZipFile.OpenRead(archivePath);
    var libraries=new List<object>();string compiler="";
    foreach(string name in new[]{"nvrtc64_130_0.dll","nvrtc-builtins64_130.dll"})
    {
        var entry=archive.Entries.Single(e=>Path.GetFileName(e.FullName)==name);
        string path=Path.GetFullPath(Path.Combine(dependency,"unpacked",entry.FullName));
        if(!path.StartsWith(Path.GetFullPath(dependency)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Compiler path outside dependency.");
        using var original=entry.Open();using var local=File.OpenRead(path);
        string hash=Convert.ToHexString(SHA256.HashData(local));
        if(hash!=Convert.ToHexString(SHA256.HashData(original)))throw new InvalidOperationException("Extracted compiler bytes differ from pinned archive.");
        libraries.Add(new {path,sha256=hash});if(name.StartsWith("nvrtc64_"))compiler=path;
    }
    const string source="""
    extern "C" __global__ void add_small_double(double* state, int count) {
        unsigned int i=blockIdx.x*blockDim.x+threadIdx.x;
        if(i<(unsigned int)count) state[i+8]+=((double)(1+(i%7)))*0x1p-40;
    }
    """;
    File.WriteAllText(Path.Combine(output,"interop.cu"),source);
    using var cuda=new CudaDriverProbe(0);
    Save("device.json",cuda.Device);
    using var nvrtc=new NvrtcCompiler(compiler);
    if(nvrtc.Version!=(13,0))throw new InvalidOperationException("Unexpected compiler version.");
    var compiled=nvrtc.Compile(source,cuda.Device.Architecture);
    File.WriteAllBytes(Path.Combine(output,"interop.ptx"),compiled.Ptx);File.WriteAllText(Path.Combine(output,"compiler.log"),compiled.Log);
    Save("compiler.json",new {libraries,archiveSha256=expectedArchive,
        nvrtcMajor=nvrtc.Version.Major,nvrtcMinor=nvrtc.Version.Minor,nvrtc.SupportedArchitectures,compiled.Options,
        ptxSha256=Convert.ToHexString(SHA256.HashData(compiled.Ptx))});
    var kernel=cuda.LoadKernel(compiled.Ptx,"add_small_double");
    foreach(int count in new[]{1,255,256,257,4097,65537})
    {
        var data=Enumerable.Repeat(-12345.625,count+16).ToArray();Array.Fill(data,1d,8,count);
        ulong buffer=cuda.Allocate(data.Length);
        try
        {
            cuda.Upload(buffer,data);
            var roundtrip=cuda.Download(buffer);
            File.WriteAllBytes(Path.Combine(output,$"transfer-{count}.bin"),System.Runtime.InteropServices.MemoryMarshal.AsBytes(roundtrip.AsSpan()).ToArray());
            if(!roundtrip.AsSpan().SequenceEqual(data))throw new InvalidOperationException("Double transfer mismatch.");
            for(int launch=0;launch<7;++launch)cuda.Launch(kernel,buffer,count);
            var actual=cuda.Download(buffer);var expected=(double[])data.Clone();
            File.WriteAllBytes(Path.Combine(output,$"result-{count}.bin"),System.Runtime.InteropServices.MemoryMarshal.AsBytes(actual.AsSpan()).ToArray());
            for(int i=0;i<count;++i)expected[i+8]+=7*(1+i%7)*Math.ScaleB(1d,-40);
            for(int i=0;i<actual.Length;++i)
                if(BitConverter.DoubleToInt64Bits(actual[i])!=BitConverter.DoubleToInt64Bits(expected[i]))
                    throw new InvalidOperationException($"Float64 arithmetic or guard mismatch: count {count}, index {i}.");
            var row=new {count,launches=7,passed=true,first=actual[8],last=actual[count+7],guardsPassed=true,
                resultSha256=Convert.ToHexString(SHA256.HashData(System.Runtime.InteropServices.MemoryMarshal.AsBytes(actual.AsSpan())))};
            cases.Add(row);Save("cases-so-far.json",cases);Console.WriteLine($"Double buffer {count}: passed");
        }
        finally {cuda.Free(buffer);}
    }
    // Disposal is part of success, so the result cannot precede it.
    nvrtc.Dispose();cuda.Dispose();
    Save("results.json",new {completed=true,interopPassed=true,cases,contextDisposed=true,biologicalModelExecuted=false,
        speedupMeasured=false,numericalSolverQualified=false,avatarRecoveryAssessed=false});
}
catch(Exception error){Save("FAILED.json",new {error=error.ToString(),completedCases=cases});throw;}

using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

if (args.Length!=3 || args.Any(p=>!Path.IsPathFullyQualified(p)) || Directory.Exists(args[2]))
    throw new ArgumentException("Supply verified compiler, reviewed CPU baseline and new output directories.");
CultureInfo.CurrentCulture=CultureInfo.InvariantCulture; CultureInfo.CurrentUICulture=CultureInfo.InvariantCulture;
string dependency=args[0],baseline=args[1],output=args[2]; Directory.CreateDirectory(output);
var json=new JsonSerializerOptions {WriteIndented=true,PropertyNameCaseInsensitive=true};
void Save(string name,object value)=>File.WriteAllText(Path.Combine(output,name),JsonSerializer.Serialize(value,json));
string Hash(string path) {using var stream=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(stream));}
const int frames=1024;
double[] bounds=[1e-6,1e-6,1e-6,1e-9,1e-9,1e-9,1e-9,1e-9,1e-9,1e-9,1e-9,1e-6,1e-9,1e-9,0,1e-6,1e-6];
Save("protocol.json",new {version="olive-cuda-membrane-v1",candidateVersion=OlivaryCudaBatch.Version,frames,
    counts=new[]{1,32,192},membraneStepsMs=new[]{.025,.00625},absoluteBounds=bounds,exactPerFrameCountersAndClocks=true,
    requiredControlGroups=6,guardsPerEnd=8,kernelMaximumSubsteps=100000,maximumPopulation=32768,
    bytesPerCellFrame=152,traceLayout="14 float64 states, time/load/synaptic float64, soma/axon int64; frame then original identity order; little endian",
    speedupMeasured=false,independentSolverQualified=false,physiologyQualified=false,avatarRecoveryAssessed=false});
var completed=new List<object>();
try
{
    using var cpuProtocol=JsonDocument.Parse(File.ReadAllText(Path.Combine(baseline,"protocol.json")));
    if(cpuProtocol.RootElement.GetProperty("version").GetString()!="olive-cpu-batch-v1" || cpuProtocol.RootElement.GetProperty("frames").GetInt32()!=frames)
        throw new InvalidOperationException("Unexpected CPU reference protocol.");
    var libraries=CompilerDependency.Verify(dependency);
    using var cuda=new CudaDriverProbe(0); Save("device.json",cuda.Device);
    using var compiler=new NvrtcCompiler(libraries.Compiler,libraries.Builtins);
    if(compiler.Version!=(13,0))throw new InvalidOperationException("Unexpected NVRTC version.");
    string source=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"olivary.cu"));
    File.WriteAllText(Path.Combine(output,"olivary.cu"),source);
    var compiled=compiler.Compile(source,cuda.Device.Architecture);
    File.WriteAllBytes(Path.Combine(output,"olivary.ptx"),compiled.Ptx); File.WriteAllText(Path.Combine(output,"compiler.log"),compiled.Log);
    Save("compiler.json",new {archiveSha256=CompilerDependency.ArchiveSha256,compiler.CompilerPath,compiler.BuiltinsPath,
        compilerSha256=Hash(compiler.CompilerPath),builtinsSha256=Hash(compiler.BuiltinsPath),compiled.Options,
        nvrtcMajor=compiler.Version.Major,nvrtcMinor=compiler.Version.Minor,ptxSha256=Hash(Path.Combine(output,"olivary.ptx")),
        sourceSha256=Hash(Path.Combine(output,"olivary.cu")),basenameHandlesVerified=true});
    nint kernel=cuda.LoadKernel(compiled.Ptx,"advance_olive");
    Save("controls.json",BatchControls.Run(cuda,kernel,rows=>Save("controls-so-far.json",rows))); Console.WriteLine("Six GPU ownership/atomicity control groups passed.");
    foreach(int count in new[]{1,32,192}) foreach(double maximumStep in new[]{.025,.00625})
    {
        string key=$"cells-{count}-h-{maximumStep}"; Console.WriteLine("CASE "+key);
        string from=Path.Combine(baseline,key), to=Path.Combine(output,key); Directory.CreateDirectory(to);
        var design=JsonSerializer.Deserialize<Design>(File.ReadAllText(Path.Combine(from,"design.json")),json) ?? throw new InvalidOperationException("Missing design.");
        if(design.Count!=count || design.MaximumStep!=maximumStep || design.Seeds.Length!=count || design.Seeds.Any(s=>s.TimeMs!=0))
            throw new InvalidOperationException("Invalid fixed CPU design.");
        using var originalResult=JsonDocument.Parse(File.ReadAllText(Path.Combine(from,"result.json")));
        string expectedHash=originalResult.RootElement.GetProperty("hashes").GetProperty("reference").GetString()!;
        if(Hash(Path.Combine(from,"reference.bin"))!=expectedHash)throw new InvalidOperationException("CPU reference bytes changed.");
        File.Copy(Path.Combine(from,"design.json"),Path.Combine(to,"design.json"));
        var maxima=new double[17]; var locations=new object?[17]; bool matchingCounters=true; object? firstCounterDifference=null;
        int samples=0; string? failure=null;
        try
        {
            using var expected=new BinaryReader(File.OpenRead(Path.Combine(from,"reference.bin")));
            using var writer=new BinaryWriter(new FileStream(Path.Combine(to,"gpu.bin"),FileMode.CreateNew));
            using var batch=new OlivaryCudaBatch(cuda,kernel,design.Seeds.Reverse().ToArray());
            void Sample(int frame)
            {
                var byId=batch.SnapshotAll().ToDictionary(c=>c.Identity);
                for(int lane=0;lane<count;++lane)
                {
                    var actual=byId[design.Seeds[lane].Identity];
                    double[] row=[..actual.State,actual.TimeMs,actual.AdditionalIonicLoad,actual.SynapticCurrent];
                    for(int component=0;component<17;++component)
                    {
                        double target=expected.ReadDouble(), value=row[component]; writer.Write(value);
                        if(!double.IsFinite(value))throw new InvalidOperationException("Non-finite GPU trace.");
                        double error=Math.Abs(value-target);
                        if(!double.IsFinite(error))throw new InvalidOperationException("Non-finite comparison error.");
                        if(error>maxima[component]){maxima[component]=error;locations[component]=new {frame,lane,actual.Identity};}
                    }
                    long soma=expected.ReadInt64(),axon=expected.ReadInt64(); writer.Write(actual.SomaticSpikes);writer.Write(actual.AxonalSpikes);
                    if(soma!=actual.SomaticSpikes || axon!=actual.AxonalSpikes)
                    { matchingCounters=false;firstCounterDifference??=new {frame,lane,actual.Identity,expectedSoma=soma,expectedAxon=axon,actual.SomaticSpikes,actual.AxonalSpikes}; }
                }
                ++samples;
            }
            Sample(0);
            for(int frame=0;frame<frames;++frame)
            {
                var steps=design.Seeds.Select((s,lane)=>new OlivaryBatchStep(s.Identity,frame*(lane%2==0?.125:.0625),lane%2==0?.125:.0625,
                    new(DendriticAmpa:frame%16==lane%16?.12:0,SomaticGaba:frame%23==lane%23?.08:0,
                        DendriticCurrent:frame>=256&&frame<768?8+lane%3:0,MetabolicPotassium:.02*(lane%3)),maximumStep)).ToArray();
                var reports=batch.Advance(frame%2==0?steps:steps.Reverse().ToArray()).ToDictionary(r=>r.Identity);
                foreach(var step in steps)
                {
                    var report=reports[step.Identity];
                    if(report.FromMs!=step.FromMs||report.EndMs!=step.FromMs+step.DurationMs||report.Metrics.Fired!=(report.Metrics.AxonalSpikes==1))
                        throw new InvalidOperationException("Identity/time interval report differs.");
                }
                Sample(frame+1);
            }
            if(expected.BaseStream.Position!=expected.BaseStream.Length)throw new InvalidOperationException("Unread CPU trace data.");
            batch.VerifyReadOnlyBuffersAndGuards();
        }
        catch(Exception error) {failure=error.ToString();Console.WriteLine("CASE FAILED: "+error.Message);}
        string path=Path.Combine(to,"gpu.bin");
        bool passed=failure is null && samples==1025 && matchingCounters && maxima.Zip(bounds).All(p=>p.First<=p.Second);
        var row=new {key,count,maximumStep,completed=failure is null,passed,samples,maxima,locations,matchingCounters,firstCounterDifference,
            error=failure,referenceSha256=expectedHash,gpuSha256=File.Exists(path)?Hash(path):null};
        completed.Add(row); Save(Path.Combine(key,"result.json"),row); Save("completed-cases.json",completed);
    }
    compiler.Dispose(); cuda.Dispose();
    Save("results.json",new {completed=true,cases=completed,contextDisposed=true,oldSavesLoaded=false,liveBrainAccessed=false,
        speedupMeasured=false,independentSolverQualified=false,physiologyQualified=false,avatarRecoveryAssessed=false});
    Console.WriteLine("All GPU conditions attempted; saved-byte analysis determines numerical agreement.");
}
catch(Exception error) {Save("FAILED.json",new {error=error.ToString(),completedCases=completed});throw;}

internal sealed record Design(int Count,double MaximumStep,OlivaryCellSeed[] Seeds);

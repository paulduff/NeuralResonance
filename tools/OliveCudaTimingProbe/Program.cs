using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

if(args.Length!=3 || args.Any(p=>!Path.IsPathFullyQualified(p)) || Directory.Exists(args[2]))
    throw new ArgumentException("Supply verified compiler, CPU baseline and new timing output directories.");
CultureInfo.CurrentCulture=CultureInfo.InvariantCulture; CultureInfo.CurrentUICulture=CultureInfo.InvariantCulture;
if(Environment.GetEnvironmentVariable("DOTNET_TieredCompilation")!="0")
    throw new InvalidOperationException("Use the declared timing runner with tiered compilation disabled.");
string dependency=args[0],baseline=args[1],output=args[2]; Directory.CreateDirectory(output);
var json=new JsonSerializerOptions {WriteIndented=true,PropertyNameCaseInsensitive=true};
void Save(string name,object value)=>File.WriteAllText(Path.Combine(output,name),JsonSerializer.Serialize(value,json));
string Hash(string path) {using var stream=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(stream));}
const int startFrame=256,frames=128,repeats=5;
int[] counts=[1,32,192,768,3072,12288]; string[] modes=["cpu-1","cpu-8","cpu-16","cuda"];
double[] bounds=[1e-6,1e-6,1e-6,1e-9,1e-9,1e-9,1e-9,1e-9,1e-9,1e-9,1e-9,1e-6,1e-9,1e-9,0,1e-6,1e-6];
Save("protocol.json",new {version="olive-cuda-timing-v1",candidateVersion=OlivaryCudaBatch.Version,
    counts,membraneStepsMs=new[]{.025,.00625},modes,startFrame,frames,repeats,warmupsPerModeAndCase=1,
    sampleOrder="warmup round then five measured rounds; rotate modes by round modulo four",
    absoluteBounds=bounds,cpuExact=true,minimumFreeHeadroomBytes=1073741824L,
    stateRecordBytes=152,reportRecordBytes=48,reportLayout="from/end/load/current float64, interval soma/axon int64; frame then original lane; gzip little endian",
    timingBoundary="128 complete Advance calls; includes validation, allocations, sorting, uploads, launch/sync and report downloads; excludes construction, input generation, full snapshots, validation and file I/O",
    populationDesign="repeat reviewed 192-cell states/inputs with unique identities above 192; no cell interaction",
    liveBrainAccessed=false,oldSavesLoaded=false,physiologyQualified=false,avatarRecoveryAssessed=false});
Save("host.json",new {Environment.ProcessorCount,os=RuntimeInformation.OSDescription,runtime=RuntimeInformation.FrameworkDescription,
    stopwatchFrequency=Stopwatch.Frequency,Stopwatch.IsHighResolution,tieredCompilation=false,
    gcServer=System.Runtime.GCSettings.IsServerGC});
var completed=new List<object>();
try
{
    var libraries=CompilerDependency.Verify(dependency);
    using var cuda=new CudaDriverProbe(0); Save("device.json",cuda.Device);
    Save("memory-before.json",cuda.MemoryEvidence());
    using var compiler=new NvrtcCompiler(libraries.Compiler,libraries.Builtins);
    if(compiler.Version!=(13,0))throw new InvalidOperationException("Unexpected NVRTC version.");
    string source=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"olivary.cu"));
    File.WriteAllText(Path.Combine(output,"olivary.cu"),source);
    var compilation=Stopwatch.StartNew(); var compiled=compiler.Compile(source,cuda.Device.Architecture); compilation.Stop();
    File.WriteAllBytes(Path.Combine(output,"olivary.ptx"),compiled.Ptx); File.WriteAllText(Path.Combine(output,"compiler.log"),compiled.Log);
    Save("compiler.json",new {archiveSha256=CompilerDependency.ArchiveSha256,compiler.CompilerPath,compiler.BuiltinsPath,
        compilerSha256=Hash(compiler.CompilerPath),builtinsSha256=Hash(compiler.BuiltinsPath),compiled.Options,
        nvrtcMajor=compiler.Version.Major,nvrtcMinor=compiler.Version.Minor,compilationMs=compilation.Elapsed.TotalMilliseconds,
        ptxSha256=Hash(Path.Combine(output,"olivary.ptx")),sourceSha256=Hash(Path.Combine(output,"olivary.cu")),basenameHandlesVerified=true});
    nint kernel=cuda.LoadKernel(compiled.Ptx,"advance_olive");
    Save("controls.json",BatchControls.Run(cuda,kernel,rows=>Save("controls-so-far.json",rows)));
    foreach(int count in counts) foreach(double step in new[]{.025,.00625})
    {
        string key=$"cells-{count}-h-{step}"; string folder=Path.Combine(output,key); Directory.CreateDirectory(folder);
        int prototypes=Math.Min(count,192); string reference=Path.Combine(baseline,$"cells-{prototypes}-h-{step}");
        var design=JsonSerializer.Deserialize<Design>(File.ReadAllText(Path.Combine(reference,"design.json")),json)!;
        if(design.Count!=prototypes || design.Seeds.Length!=prototypes || design.MaximumStep!=step)throw new InvalidOperationException("Unexpected baseline design.");
        using var oldResult=JsonDocument.Parse(File.ReadAllText(Path.Combine(reference,"result.json")));
        if(Hash(Path.Combine(reference,"reference.bin"))!=oldResult.RootElement.GetProperty("hashes").GetProperty("reference").GetString())
            throw new InvalidOperationException("Archived CPU bytes changed.");
        var expected=ReadReference(Path.Combine(reference,"reference.bin"),prototypes,startFrame,frames);
        var seeds=Enumerable.Range(0,count).Select(lane=>
        {
            int p=lane%prototypes; var seed=design.Seeds[p]; var state=expected[0][p];
            return new OlivaryCellSeed(new(seed.Identity.Instance+"/copy-"+(lane/prototypes).ToString("D4"),seed.Identity.Cell),
                state.Values[14],seed.Parameters,state.Values.Take(14).ToArray(),state.Soma,state.Axon,state.Values[15],state.Values[16]);
        }).ToArray();
        var requests=Enumerable.Range(startFrame,frames).Select(frame=>seeds.Select((seed,lane)=>
        {
            int p=lane%prototypes; double dt=p%2==0?.125:.0625;
            return new OlivaryBatchStep(seed.Identity,frame*dt,dt,
                new(DendriticAmpa:frame%16==p%16?.12:0,SomaticGaba:frame%23==p%23?.08:0,
                    DendriticCurrent:frame>=256&&frame<768?8+p%3:0,MetabolicPotassium:.02*(p%3)),step);
        }).ToArray()).ToArray();
        int[] ordered=Enumerable.Range(0,count).OrderBy(i=>requests[0][i].FromMs+requests[0][i].DurationMs)
            .ThenBy(i=>seeds[i].Identity.Instance,StringComparer.Ordinal).ThenBy(i=>seeds[i].Identity.Cell,StringComparer.Ordinal).ToArray();
        Save(Path.Combine(key,"design.json"),new {count,step,prototypes,startFrame,frames,
            seeds=seeds.Select((s,lane)=>new {lane,prototype=lane%prototypes,s.Identity}),
            referenceSha256=Hash(Path.Combine(reference,"reference.bin"))});
        // Each mode gets an unmeasured complete trajectory before five measured
        // trajectories. Construction is fresh, but JIT/native contexts stay warm.
        for(int round=0;round<=repeats;++round) for(int position=0;position<modes.Length;++position)
        {
            string mode=modes[(position+round)%modes.Length]; bool measured=round!=0;
            string trial=$"{mode}-round-{round}"; Console.WriteLine($"{key} {trial} {(measured?"measured":"warmup")}");
            var reports=new OlivaryBatchAdvance[frames][]; OlivaryCellSeed[]? final=null; string? error=null;
            double elapsed=0,setupMs=0,processCpuMs=0; long allocated=0; int completedFrames=0;
            int[] collections=new int[3]; var stopwatch=new Stopwatch(); object? memoryDuring=null;
            OlivaryCudaBatch? gpu=null; OlivaryCpuBatch? cpu=null;
            try
            {
                cuda.RequireMemoryHeadroom((47L*count+80)*8);
                var setup=Stopwatch.StartNew();
                if(mode=="cuda")gpu=new(cuda,kernel,seeds);else cpu=new(seeds);
                setup.Stop(); setupMs=setup.Elapsed.TotalMilliseconds; memoryDuring=cuda.MemoryEvidence();
                var initial=gpu is not null?gpu.SnapshotAll():seeds.Select(s=>cpu!.Snapshot(s.Identity)).ToArray();
                for(int lane=0;lane<count;++lane)
                {
                    var a=initial[lane]; var b=seeds[lane];
                    if(a.Identity!=b.Identity || a.TimeMs!=b.TimeMs || a.Parameters!=b.Parameters ||
                        !a.State.AsSpan().SequenceEqual(b.State) || a.SomaticSpikes!=b.SomaticSpikes || a.AxonalSpikes!=b.AxonalSpikes ||
                        a.AdditionalIonicLoad!=b.AdditionalIonicLoad || a.SynapticCurrent!=b.SynapticCurrent)
                        throw new InvalidOperationException("Initial state transfer differs from the fixed CPU seed.");
                }
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                long before=GC.GetTotalAllocatedBytes(true); int[] gc=Enumerable.Range(0,3).Select(GC.CollectionCount).ToArray();
                using var process=Process.GetCurrentProcess(); double cpuBefore=process.TotalProcessorTime.TotalMilliseconds;
                int workers=mode=="cuda"?0:int.Parse(mode.AsSpan(4)); DateTime begin=DateTime.UtcNow;
                stopwatch.Start();
                for(int frame=0;frame<frames;++frame)
                {
                    reports[frame]=gpu is not null?gpu.Advance(requests[frame]):cpu!.Advance(requests[frame],workers);
                    ++completedFrames;
                }
                stopwatch.Stop(); elapsed=stopwatch.Elapsed.TotalMilliseconds;
                processCpuMs=process.TotalProcessorTime.TotalMilliseconds-cpuBefore; allocated=GC.GetTotalAllocatedBytes(true)-before;
                for(int g=0;g<3;++g)collections[g]=GC.CollectionCount(g)-gc[g];
                Save(Path.Combine(key,trial+"-clock.json"),new {beginUtc=begin,endUtc=DateTime.UtcNow,elapsedMs=elapsed});
                final=gpu is not null?gpu.SnapshotAll():seeds.Select(s=>cpu!.Snapshot(s.Identity)).ToArray();
                gpu?.VerifyReadOnlyBuffersAndGuards();
            }
            catch(Exception ex) {stopwatch.Stop();error=ex.ToString();}
            finally {gpu?.Dispose();}
            // Preserve all returned data before checking accuracy; even a failed
            // trial retains its partial reports. No validation or writing is timed.
            string reportPath=Path.Combine(folder,trial+"-reports.bin.gz");
            using(var writer=Compressed(reportPath))
            {
                for(int f=0;f<completedFrames;++f)
                {
                    var row=reports[f];
                    if(row.Length!=count)throw new InvalidOperationException("Report population changed.");
                    var byLane=new OlivaryBatchAdvance[count];
                    for(int j=0;j<count;++j)
                    {
                        int lane=ordered[j]; if(row[j].Identity!=seeds[lane].Identity)throw new InvalidOperationException("Report identity/order changed.");
                        byLane[lane]=row[j];
                    }
                    foreach(var r in byLane)
                    {writer.Write(r.FromMs);writer.Write(r.EndMs);writer.Write(r.Metrics.AdditionalIonicLoad);writer.Write(r.Metrics.SynapticCurrent);writer.Write(r.Metrics.SomaticSpikes);writer.Write(r.Metrics.AxonalSpikes);}
                }
            }
            if(final is not null)using(var writer=Compressed(Path.Combine(folder,trial+"-final.bin.gz")))
            {
                if(final.Length!=count)throw new InvalidOperationException("Final population changed.");
                for(int lane=0;lane<count;++lane)
                {
                    var c=final[lane]; if(c.Identity!=seeds[lane].Identity || c.Parameters!=seeds[lane].Parameters)throw new InvalidOperationException("Final identity/parameters changed.");
                    foreach(double value in c.State)writer.Write(value);
                    writer.Write(c.TimeMs);writer.Write(c.AdditionalIonicLoad);writer.Write(c.SynapticCurrent);writer.Write(c.SomaticSpikes);writer.Write(c.AxonalSpikes);
                }
            }
            var result=new {key,count,step,mode,round,measured,completedFrames,error,elapsedMs=elapsed,setupMs,processCpuMs,
                allocatedBytes=allocated,gcCollections=collections,memoryDuring,bufferBytes=(47L*count+80)*8,
                reportsSha256=Hash(reportPath),finalSha256=final is null?null:Hash(Path.Combine(folder,trial+"-final.bin.gz")),
                identityOrderChecked=true,initialStateChecked=final is not null,matchingParameters=final is not null};
            completed.Add(result); Save(Path.Combine(key,trial+".json"),result); Save("completed-trials.json",completed);
            if(error is not null)throw new InvalidOperationException("Timing trial failed; preserved partial evidence: "+error);
        }
    }
    Save("memory-after.json",cuda.MemoryEvidence()); compiler.Dispose();cuda.Dispose();
    Save("results.json",new {completed=true,trials=completed,contextDisposed=true,speedupMeasured=true,
        liveBrainAccessed=false,oldSavesLoaded=false,physiologyQualified=false,avatarRecoveryAssessed=false});
    Console.WriteLine("All timing trials saved. Independent byte analysis determines accuracy and speed ratios.");
}
catch(Exception ex) {Save("FAILED.json",new {error=ex.ToString(),completedTrials=completed.Count});throw;}

static BinaryWriter Compressed(string path)=>new(new GZipStream(new FileStream(path,FileMode.CreateNew),CompressionLevel.Fastest));
static ReferenceState[][] ReadReference(string path,int count,int start,int frames)
{
    using var reader=new BinaryReader(File.OpenRead(path));
    if(reader.BaseStream.Length!=152L*count*1025)throw new InvalidOperationException("Incomplete CPU trace.");
    reader.BaseStream.Position=152L*count*start; var result=new ReferenceState[frames+1][];
    for(int f=0;f<=frames;++f)
    {
        result[f]=new ReferenceState[count];
        for(int c=0;c<count;++c)result[f][c]=new(Enumerable.Range(0,17).Select(_=>reader.ReadDouble()).ToArray(),reader.ReadInt64(),reader.ReadInt64());
    }
    return result;
}
internal sealed record Design(int Count,double MaximumStep,OlivaryCellSeed[] Seeds);
internal sealed record ReferenceState(double[] Values,long Soma,long Axon);

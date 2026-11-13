using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

#if HOST_CANDIDATE
const string mode="candidate";
#else
const string mode="reference";
#endif
CultureInfo.CurrentCulture=CultureInfo.InvariantCulture; CultureInfo.CurrentUICulture=CultureInfo.InvariantCulture;
if(args.Length!=5 || !Path.IsPathFullyQualified(args[0]) || !Path.IsPathFullyQualified(args[1]) || Directory.Exists(args[1]))
    throw new ArgumentException("Supply frozen package, new output, population (or controls), step and round.");
if(Environment.GetEnvironmentVariable("DOTNET_TieredCompilation")!="0")throw new InvalidOperationException("Disable tiered compilation for the declared comparison.");
string package=args[0],output=args[1]; Directory.CreateDirectory(output);
var json=new JsonSerializerOptions {WriteIndented=true,PropertyNameCaseInsensitive=true};
void Save(string name,object value)=>File.WriteAllText(Path.Combine(output,name),JsonSerializer.Serialize(value,json));
string Hash(string path){using var stream=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(stream));}
string runtimeDirectory=Path.GetDirectoryName(typeof(object).Assembly.Location)!;
var runtimeFiles=new[]{"System.Private.CoreLib.dll","coreclr.dll","clrjit.dll","hostpolicy.dll"}
    .Select(name=>new {name,sha256=Hash(Path.Combine(runtimeDirectory,name))}).ToArray();
if(typeof(OlivaryCudaBatch).GetMethod("CostBegin",BindingFlags.Instance|BindingFlags.NonPublic)is not null)
    throw new InvalidOperationException("Profiling hooks must not be compiled into the timing variants.");
Save("host.json",new {mode,version=OlivaryCudaBatch.Version,runtimeFiles,Environment.ProcessorCount,os=RuntimeInformation.OSDescription,runtime=RuntimeInformation.FrameworkDescription,
    stopwatchFrequency=Stopwatch.Frequency,Stopwatch.IsHighResolution,tieredCompilation=false,
    costHooksPresent=typeof(OlivaryCudaBatch).GetMethod("CostBegin",BindingFlags.Instance|BindingFlags.NonPublic)is not null,
    gcServer=System.Runtime.GCSettings.IsServerGC});
try
{
    using var cuda=new CudaDriverProbe(0); Save("device.json",cuda.Device);
    using var priorDevice=JsonDocument.Parse(File.ReadAllText(Path.Combine(package,"timing-evidence/device.json")));
    using var compiler=JsonDocument.Parse(File.ReadAllText(Path.Combine(package,"timing-evidence/compiler.json")));
    if(cuda.Device.Architecture!=priorDevice.RootElement.GetProperty("Architecture").GetInt32())throw new InvalidOperationException("Reviewed PTX architecture differs.");
    string ptx=Path.Combine(package,"timing-evidence/olivary.ptx");
    if(Hash(ptx)!=compiler.RootElement.GetProperty("ptxSha256").GetString())throw new InvalidOperationException("Reviewed PTX changed.");
    nint kernel=cuda.LoadKernel(File.ReadAllBytes(ptx),"advance_olive");
    if(args[2]=="controls")
    {
        Save("controls.json",BatchControls.Run(cuda,kernel,rows=>Save("controls-so-far.json",rows)));
        Save("host-controls.json",HostControls.Run(cuda,kernel,rows=>Save("host-controls-so-far.json",rows)));
        Save("memory-after.json",cuda.MemoryEvidence());cuda.Dispose();
        Save("result.json",new {completed=true,mode,controls=true,contextDisposed=true});return;
    }
    int count=int.Parse(args[2]),round=int.Parse(args[4]);double step=double.Parse(args[3]);
    if(!new[]{192,3072,12288}.Contains(count) || step is not (.025 or .00625) || round<0 || round>=6)
        throw new ArgumentException("Undeclared host timing case.");
    const int start=256,frames=128,prototypes=192;
    string baseline=Path.Combine(package,$"cpu-baseline/cells-{prototypes}-h-{step}");
    var design=JsonSerializer.Deserialize<Design>(File.ReadAllText(Path.Combine(baseline,"design.json")),json)!;
    if(design.Count!=prototypes || design.Seeds.Length!=prototypes || design.MaximumStep!=step)throw new InvalidOperationException("Unexpected CPU design.");
    string reference=Path.Combine(baseline,"reference.bin");
    using var old=JsonDocument.Parse(File.ReadAllText(Path.Combine(baseline,"result.json")));
    if(Hash(reference)!=old.RootElement.GetProperty("hashes").GetProperty("reference").GetString())throw new InvalidOperationException("CPU trace changed.");
    var original=new OlivaryCellSeed[prototypes];
    using(var reader=new BinaryReader(File.OpenRead(reference)))
    {
        if(reader.BaseStream.Length!=152L*prototypes*1025)throw new InvalidOperationException("Incomplete CPU trace.");
        reader.BaseStream.Position=152L*prototypes*start;
        for(int p=0;p<prototypes;++p)
        {
            double[] state=Enumerable.Range(0,14).Select(_=>reader.ReadDouble()).ToArray();
            double time=reader.ReadDouble(),load=reader.ReadDouble(),current=reader.ReadDouble();
            original[p]=new(design.Seeds[p].Identity,time,design.Seeds[p].Parameters,state,reader.ReadInt64(),reader.ReadInt64(),load,current);
        }
    }
    var preparation=Stopwatch.StartNew();
    var seeds=Enumerable.Range(0,count).Select(lane=>original[lane%prototypes] with
        {Identity=new(original[lane%prototypes].Identity.Instance+"/copy-"+(lane/prototypes).ToString("D4"),original[lane%prototypes].Identity.Cell)}).ToArray();
    var requests=Enumerable.Range(start,frames).Select(frame=>seeds.Select((seed,lane)=>
    {
        int p=lane%prototypes;double dt=p%2==0?.125:.0625;
        return new OlivaryBatchStep(seed.Identity,frame*dt,dt,new(DendriticAmpa:frame%16==p%16?.12:0,
            SomaticGaba:frame%23==p%23?.08:0,DendriticCurrent:8+p%3,MetabolicPotassium:.02*(p%3)),step);
    }).ToArray()).ToArray();
    // Match the accuracy experiment: alternate caller order outside the timer.
    for(int f=0;f<frames;++f)if((start+f)%2==1)Array.Reverse(requests[f]);
    preparation.Stop();
    int[] order=Enumerable.Range(0,count).OrderBy(i=>requests[0][i].FromMs+requests[0][i].DurationMs)
        .ThenBy(i=>seeds[i].Identity.Instance,StringComparer.Ordinal).ThenBy(i=>seeds[i].Identity.Cell,StringComparer.Ordinal).ToArray();
    Save("design.json",new {mode,count,step,round,startFrame=start,frames,prototypes,referenceSha256=Hash(reference),
        requestPreparationMs=preparation.Elapsed.TotalMilliseconds,alternateRequestOrder=true,
        seeds=seeds.Select((s,lane)=>new {lane,prototype=lane%prototypes,s.Identity})});
    for(int trial=0;trial<2;++trial)
    {
        string name=trial==0?"warmup":"measured";bool measured=trial==1;
        Console.WriteLine($"{count} cells, {step:R} ms, {mode}, round {round}, {name}");
        cuda.RequireMemoryHeadroom((47L*count+80)*8);
        var setup=Stopwatch.StartNew();using var gpu=new OlivaryCudaBatch(cuda,kernel,seeds);setup.Stop();
        var initial=gpu.SnapshotAll();
        for(int lane=0;lane<count;++lane)RequireSame(initial[lane],seeds[lane]);
        var reports=new OlivaryBatchAdvance[frames][];
        GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
        int[] gc=Enumerable.Range(0,3).Select(GC.CollectionCount).ToArray();
        using var process=Process.GetCurrentProcess();double cpu=process.TotalProcessorTime.TotalMilliseconds;
        int completedFrames=0;
        long allocation=GC.GetTotalAllocatedBytes(true);
        DateTime begin=DateTime.UtcNow;long beginTicks=Stopwatch.GetTimestamp();
        try
        {
            for(int f=0;f<frames;++f){reports[f]=gpu.Advance(requests[f]);++completedFrames;}
        }
        catch(Exception ex)
        {
            long failedTicks=Stopwatch.GetTimestamp()-beginTicks;
            Save(name+"-partial.json",new {completedFrames,failedTicks,error=ex.ToString(),reports=reports.Take(completedFrames).ToArray()});
            throw;
        }
        long elapsedTicks=Stopwatch.GetTimestamp()-beginTicks;DateTime end=DateTime.UtcNow;
        long allocatedBytes=GC.GetTotalAllocatedBytes(true)-allocation;double processCpuMs=process.TotalProcessorTime.TotalMilliseconds-cpu;
        int[] collections=Enumerable.Range(0,3).Select(g=>GC.CollectionCount(g)-gc[g]).ToArray();
        // Snapshots, accuracy checks, compression and file I/O are outside the timer.
        var final=gpu.SnapshotAll();gpu.VerifyReadOnlyBuffersAndGuards();
        string reportPath=Path.Combine(output,name+"-reports.bin.gz"),finalPath=Path.Combine(output,name+"-final.bin.gz");
        using(var writer=Compressed(reportPath))foreach(var row in reports)
        {
            if(row.Length!=count)throw new InvalidOperationException("Incomplete report population.");
            var byLane=new OlivaryBatchAdvance[count];
            for(int j=0;j<count;++j)
            {int lane=order[j];if(row[j].Identity!=seeds[lane].Identity)throw new InvalidOperationException("Report identity/order changed.");byLane[lane]=row[j];}
            foreach(var r in byLane)
            {writer.Write(r.FromMs);writer.Write(r.EndMs);writer.Write(r.Metrics.AdditionalIonicLoad);writer.Write(r.Metrics.SynapticCurrent);writer.Write(r.Metrics.SomaticSpikes);writer.Write(r.Metrics.AxonalSpikes);}
        }
        using(var writer=Compressed(finalPath))for(int lane=0;lane<count;++lane)
        {
            var cell=final[lane];if(cell.Identity!=seeds[lane].Identity || cell.Parameters!=seeds[lane].Parameters)throw new InvalidOperationException("Final identity/parameters changed.");
            foreach(double v in cell.State)writer.Write(v);writer.Write(cell.TimeMs);writer.Write(cell.AdditionalIonicLoad);
            writer.Write(cell.SynapticCurrent);writer.Write(cell.SomaticSpikes);writer.Write(cell.AxonalSpikes);
        }
        foreach(string kind in new[]{"reports","final"})
        {
            using var actual=new GZipStream(File.OpenRead(Path.Combine(output,name+"-"+kind+".bin.gz")),CompressionMode.Decompress);
            using var recorded=new GZipStream(File.OpenRead(Path.Combine(package,"recorded",$"cells-{count}-h-{step}","measured-"+kind+".bin.gz")),CompressionMode.Decompress);
            if(!SHA256.HashData(actual).AsSpan().SequenceEqual(SHA256.HashData(recorded)))
                throw new InvalidOperationException("Recorded accuracy bytes differ: "+kind);
        }
        Save(name+".json",new {mode,count,step,round,measured,recordedBytesMatch=true,completedFrames=frames,elapsedTicks,
            elapsedMs=elapsedTicks*1000d/Stopwatch.Frequency,beginUtc=begin,endUtc=end,setupMs=setup.Elapsed.TotalMilliseconds,
            processCpuMs,allocatedBytes,gcCollections=collections,
            reportsSha256=Hash(reportPath),finalSha256=Hash(finalPath),initialStateChecked=true,identityOrderChecked=true,
            memoryDuring=cuda.MemoryEvidence(),bufferBytes=(47L*count+80)*8});
    }
    Save("memory-after.json",cuda.MemoryEvidence());cuda.Dispose();
    Save("result.json",new {completed=true,mode,count,step,round,trials=2,contextDisposed=true,
        liveBrainAccessed=false,oldSavesLoaded=false,physiologyQualified=false,avatarRecoveryAssessed=false});
}
catch(Exception ex){Save("FAILED.json",new {error=ex.ToString(),mode});throw;}

static BinaryWriter Compressed(string path)=>new(new GZipStream(new FileStream(path,FileMode.CreateNew),CompressionLevel.Fastest));
static void RequireSame(OlivaryCellSeed a,OlivaryCellSeed b)
{
    if(a.Identity!=b.Identity || a.TimeMs!=b.TimeMs || a.Parameters!=b.Parameters || !a.State.AsSpan().SequenceEqual(b.State) ||
        a.SomaticSpikes!=b.SomaticSpikes || a.AxonalSpikes!=b.AxonalSpikes || a.AdditionalIonicLoad!=b.AdditionalIonicLoad || a.SynapticCurrent!=b.SynapticCurrent)
        throw new InvalidOperationException("Initial state transfer differs.");
}
internal sealed record Design(int Count,double MaximumStep,OlivaryCellSeed[] Seeds);

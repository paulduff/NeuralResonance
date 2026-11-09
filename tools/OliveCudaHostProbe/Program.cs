using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

#if HOST_CANDIDATE
const string mode="candidate";
#else
const string mode="reference";
#endif
CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;CultureInfo.CurrentUICulture=CultureInfo.InvariantCulture;
if(args.Length!=2||args.Any(p=>!Path.IsPathFullyQualified(p))||Directory.Exists(args[1]))
    throw new ArgumentException("Supply frozen package and new accuracy output.");
string package=args[0],output=args[1];Directory.CreateDirectory(output);
var json=new JsonSerializerOptions{WriteIndented=true,PropertyNameCaseInsensitive=true};
void Save(string name,object value)=>File.WriteAllText(Path.Combine(output,name),JsonSerializer.Serialize(value,json));
string Hash(string p){using var s=File.OpenRead(p);return Convert.ToHexString(SHA256.HashData(s));}
var completed=new List<object>();
try
{
    using var cuda=new CudaDriverProbe(0);
    Save("device.json",cuda.Device);
    using var oldDevice=JsonDocument.Parse(File.ReadAllText(Path.Combine(package,"timing-evidence/device.json")));
    using var compiler=JsonDocument.Parse(File.ReadAllText(Path.Combine(package,"timing-evidence/compiler.json")));
    if(JsonSerializer.Serialize(cuda.Device)!=JsonSerializer.Serialize(oldDevice.RootElement))throw new InvalidOperationException("Reviewed device metadata differs.");
    string ptx=Path.Combine(package,"timing-evidence/olivary.ptx");
    if(Hash(ptx)!=compiler.RootElement.GetProperty("ptxSha256").GetString())throw new InvalidOperationException("Reviewed PTX changed.");
    nint kernel=cuda.LoadKernel(File.ReadAllBytes(ptx),"advance_olive");
    Save("controls.json",BatchControls.Run(cuda,kernel,rows=>Save("controls-partial.json",rows)));
    Save("host-controls.json",HostControls.Run(cuda,kernel,rows=>Save("host-controls-partial.json",rows)));
    foreach(int count in new[]{192,3072,12288})foreach(double step in new[]{.025,.00625})
    {
        string key=$"cells-{count}-h-{step}";string folder=Path.Combine(output,key);Directory.CreateDirectory(folder);
        string baseline=Path.Combine(package,$"cpu-baseline/cells-192-h-{step}");
        var design=JsonSerializer.Deserialize<Design>(File.ReadAllText(Path.Combine(baseline,"design.json")),json)!;
        if(design.Count!=192||design.Seeds.Length!=192||design.MaximumStep!=step)throw new InvalidOperationException("Unexpected baseline.");
        var original=new OlivaryCellSeed[192];
        using(var reader=new BinaryReader(File.OpenRead(Path.Combine(baseline,"reference.bin"))))
        {
            if(reader.BaseStream.Length!=152L*192*1025)throw new InvalidOperationException("Incomplete CPU trace.");
            reader.BaseStream.Position=152L*192*256;
            for(int p=0;p<192;++p)
            {
                double[] state=Enumerable.Range(0,14).Select(_=>reader.ReadDouble()).ToArray();
                double time=reader.ReadDouble(),load=reader.ReadDouble(),current=reader.ReadDouble();
                original[p]=new(design.Seeds[p].Identity,time,design.Seeds[p].Parameters,state,reader.ReadInt64(),reader.ReadInt64(),load,current);
            }
        }
        var seeds=Enumerable.Range(0,count).Select(lane=>original[lane%192] with
            {Identity=new(original[lane%192].Identity.Instance+"/copy-"+(lane/192).ToString("D4"),original[lane%192].Identity.Cell)}).ToArray();
        cuda.RequireMemoryHeadroom((47L*count+80)*8);
        using(var batch=new OlivaryCudaBatch(cuda,kernel,seeds))
        {
            var initial=batch.SnapshotAll();
            for(int lane=0;lane<count;++lane)RequireSame(initial[lane],seeds[lane]);
            // Preserve complete reports before comparisons. No timers: a CPU
            // numerical study may be running, so these are accuracy data only.
            using(var writer=Compressed(Path.Combine(folder,"reports.bin.gz")))
            for(int f=256;f<384;++f)
            {
                var requests=Enumerable.Range(0,count).Select(lane=>
                {
                    int p=lane%192;double dt=p%2==0?.125:.0625;
                    return new OlivaryBatchStep(seeds[lane].Identity,f*dt,dt,new(DendriticAmpa:f%16==p%16?.12:0,
                        SomaticGaba:f%23==p%23?.08:0,DendriticCurrent:8+p%3,MetabolicPotassium:.02*(p%3)),step);
                }).ToArray();
                // Alternate request permutation; preserved lane identity must
                // recover the original serialized population regardless.
                if(f%2==1)Array.Reverse(requests);
                var reports=batch.Advance(requests);
                var expectedOrder=requests.OrderBy(r=>r.FromMs+r.DurationMs).ThenBy(r=>r.Identity.Instance,StringComparer.Ordinal)
                    .ThenBy(r=>r.Identity.Cell,StringComparer.Ordinal).Select(r=>r.Identity);
                if(!reports.Select(r=>r.Identity).SequenceEqual(expectedOrder))throw new InvalidOperationException("Report identity/order differs.");
                var byId=reports.ToDictionary(r=>r.Identity);
                foreach(var seed in seeds)
                {
                    var r=byId[seed.Identity];writer.Write(r.FromMs);writer.Write(r.EndMs);
                    writer.Write(r.Metrics.AdditionalIonicLoad);writer.Write(r.Metrics.SynapticCurrent);
                    writer.Write(r.Metrics.SomaticSpikes);writer.Write(r.Metrics.AxonalSpikes);
                }
            }
            using(var writer=Compressed(Path.Combine(folder,"final.bin.gz")))
            {
                var final=batch.SnapshotAll();
                for(int lane=0;lane<count;++lane)
                {
                    var c=final[lane];if(c.Identity!=seeds[lane].Identity||c.Parameters!=seeds[lane].Parameters)throw new InvalidOperationException("Final identity/parameters differ.");
                    foreach(double v in c.State)writer.Write(v);writer.Write(c.TimeMs);writer.Write(c.AdditionalIonicLoad);
                    writer.Write(c.SynapticCurrent);writer.Write(c.SomaticSpikes);writer.Write(c.AxonalSpikes);
                }
            }
            batch.VerifyReadOnlyBuffersAndGuards();
        }
        foreach(string name in new[]{"reports","final"})
        {
            using var actual=Decompressed(Path.Combine(folder,name+".bin.gz"));
            using var expected=Decompressed(Path.Combine(package,"recorded",key,"measured-"+name+".bin.gz"));
            if(!SHA256.HashData(actual).AsSpan().SequenceEqual(SHA256.HashData(expected)))throw new InvalidOperationException("Original cost bytes differ: "+key+"/"+name);
        }
        var row=new {key,count,step,frames=128,recordedBytesMatch=true,
            reportsSha256=Hash(Path.Combine(folder,"reports.bin.gz")),finalSha256=Hash(Path.Combine(folder,"final.bin.gz"))};
        Save(key+"/result.json",row);completed.Add(row);Save("completed-cases.json",completed);
        Console.WriteLine(mode+" "+key+": original reports/state reproduced");
    }
    Save("memory-after.json",cuda.MemoryEvidence());cuda.Dispose();
    Save("result.json",new {completed=true,mode,version=OlivaryCudaBatch.Version,cases=completed.Count,contextDisposed=true,
        accuracyPassed=true,speedMeasured=false,physiologyQualified=false,avatarRecoveryAssessed=false});
}
catch(Exception e){Save("FAILED.json",new{mode,error=e.ToString(),completedCases=completed.Count});throw;}

static BinaryWriter Compressed(string path)=>new(new GZipStream(new FileStream(path,FileMode.CreateNew),CompressionLevel.Fastest));
static Stream Decompressed(string path)=>new GZipStream(File.OpenRead(path),CompressionMode.Decompress);
static void RequireSame(OlivaryCellSeed a,OlivaryCellSeed b)
{
    if(a.Identity!=b.Identity||a.TimeMs!=b.TimeMs||a.Parameters!=b.Parameters||!a.State.AsSpan().SequenceEqual(b.State)||
       a.SomaticSpikes!=b.SomaticSpikes||a.AxonalSpikes!=b.AxonalSpikes||a.AdditionalIonicLoad!=b.AdditionalIonicLoad||a.SynapticCurrent!=b.SynapticCurrent)
        throw new InvalidOperationException("Initial transfer differs.");
}
internal sealed record Design(int Count,double MaximumStep,OlivaryCellSeed[] Seeds);

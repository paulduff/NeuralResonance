using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

if(args.Length!=1||!Path.IsPathFullyQualified(args[0])||Directory.Exists(args[0]))throw new ArgumentException("Use a new absolute output directory.");
CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;CultureInfo.CurrentUICulture=CultureInfo.InvariantCulture;
string output=args[0];Directory.CreateDirectory(output);
var json=new JsonSerializerOptions{WriteIndented=true};
void Save(string name,object value)=>File.WriteAllText(Path.Combine(output,name),JsonSerializer.Serialize(value,json));
string Digest(string path){using var file=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(file));}
const int frames=1024;
var completed=new List<object>();
Save("protocol.json",new{version="olive-cpu-batch-v1",frames,counts=new[]{1,32,192},membraneStepsMs=new[]{.025,.00625},
    methods=new[]{"reference","serial-batch","parallel-batch"},parallelWorkers=4,exactStateAndIntervalReportsRequired=true,
    stateComponents=new[]{"vs","vd","va","h","n","xs","k","l","r","s","q","ca","ha","xa"},
    traceLayout="For each frame (initial included), then each identity in design order: 14 float64 states, float64 time/load/synaptic current, int64 soma/axon counts; little endian.",
    bytesPerCellFrame=152,biologicalModelScope="isolated membrane; no receptors, energy feedback, learning or recurrent scheduling",
    gpuExecuted=false,speedupMeasured=false,physiologyQualified=false,avatarRecoveryAssessed=false});
try
{
    foreach(int count in new[]{1,32,192})foreach(double maximumStep in new[]{.025,.00625})
    {
        string key=$"cells-{count}-h-{maximumStep}";Console.WriteLine("CASE "+key);
        string directory=Path.Combine(output,key);Directory.CreateDirectory(directory);
        var ids=Enumerable.Range(0,count).Select(i=>new OlivaryCellIdentity("isolated-olive-"+(i%2),"N_"+(i/2))).ToArray();
        var parameters=Enumerable.Range(0,count).Select(i=>(i%4) switch{1=>new OlivaryParameters(Hcn:0),2=>new OlivaryParameters(LowCalcium:0),3=>new OlivaryParameters(HighCalcium:0),_=>new OlivaryParameters()}).ToArray();
        var seeds=ids.Select((id,i)=>OlivaryCellSeed.Capture(id,0,new(parameters[i]))).ToArray();
        File.WriteAllText(Path.Combine(directory,"design.json"),JsonSerializer.Serialize(new{key,count,maximumStep,seeds,
            inputRule="per frame i: AMPA .12 if i%16==lane%16; somatic GABA .08 if i%23==lane%23; dendritic current 8+lane%3 at frames 256..767; metabolic K .02*(lane%3)",
            intervalRule="even lanes .125 ms; odd lanes .0625 ms; each advances from its own current clock"},json));
        var hashes=new Dictionary<string,string>();long axonEvents=0,somaEvents=0;
        foreach(string method in new[]{"reference","serial-batch","parallel-batch"})
        {
            string path=Path.Combine(directory,method+".bin");
            using(var writer=new BinaryWriter(File.Create(path)))
            {
                var originals=parameters.Select(p=>new OlivaryMembrane(p)).ToArray();var times=new double[count];
                var batch=method=="reference"?null:new OlivaryCpuBatch(seeds.Reverse().ToArray());
                void WriteState(OlivaryCellSeed state)
                {
                    foreach(double value in state.State)writer.Write(value);
                    writer.Write(state.TimeMs);writer.Write(state.AdditionalIonicLoad);writer.Write(state.SynapticCurrent);
                    writer.Write(state.SomaticSpikes);writer.Write(state.AxonalSpikes);
                }
                foreach(var seed in seeds)WriteState(seed);
                for(int frame=0;frame<frames;frame++)
                {
                    var steps=ids.Select((id,lane)=>new OlivaryBatchStep(id,times[lane],lane%2==0?.125:.0625,
                        new(DendriticAmpa:frame%16==lane%16?.12:0,SomaticGaba:frame%23==lane%23?.08:0,
                            DendriticCurrent:frame>=256&&frame<768?8+lane%3:0,MetabolicPotassium:.02*(lane%3)),maximumStep)).ToArray();
                    OlivaryBatchAdvance[]? reports=batch?.Advance(frame%2==0?steps:steps.Reverse().ToArray(),method=="parallel-batch"?4:1);
                    var byIdentity=reports?.ToDictionary(r=>r.Identity);
                    for(int lane=0;lane<count;lane++)
                    {
                        var step=steps[lane];times[lane]+=step.DurationMs;
                        if(batch is null)
                        {
                            long previousSoma=originals[lane].SomaticSpikes,previousAxon=originals[lane].AxonalSpikes;
                            bool fired=originals[lane].Advance(step.DurationMs,step.Input,step.MaximumInternalStepMs);
                            if(fired!=(originals[lane].AxonalSpikes>previousAxon))throw new InvalidOperationException("Reference spike flag mismatch.");
                            somaEvents+=originals[lane].SomaticSpikes-previousSoma;axonEvents+=originals[lane].AxonalSpikes-previousAxon;
                            WriteState(OlivaryCellSeed.Capture(ids[lane],times[lane],originals[lane]));
                        }
                        else
                        {
                            var report=byIdentity![ids[lane]];
                            if(report.FromMs!=step.FromMs||report.EndMs!=times[lane]||report.Metrics.Fired!=(report.Metrics.AxonalSpikes==1))throw new InvalidOperationException("Batch interval report mismatch.");
                            WriteState(batch.Snapshot(ids[lane]));
                        }
                    }
                }
            }
            hashes[method]=Digest(path);
        }
        if(hashes.Values.Distinct().Count()!=1)throw new InvalidOperationException("Batch state traces differ from original CPU membrane: "+key);
        var result=new{key,count,maximumStep,frames,hashes,axonEvents,somaEvents,exact=true};
        File.WriteAllText(Path.Combine(directory,"result.json"),JsonSerializer.Serialize(result,json));completed.Add(result);Save("completed-cases.json",completed);
    }
    Save("results.json",new{completed=true,cases=completed,gpuExecuted=false,speedupMeasured=false,physiologyQualified=false,avatarRecoveryAssessed=false});
}
catch(Exception error){Save("FAILED.json",new{error=error.ToString(),completedCases=completed});throw;}

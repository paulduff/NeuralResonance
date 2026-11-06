using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

internal static class BoundaryTimingProbe
{
    static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web){Converters={new JsonStringEnumConverter()}};
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static readonly string[] Fields={"_ampaCurrent","_nmdaCurrent","_gabaACurrent","_gabaBCurrent","_metabotropicGlutamateCurrent",
        "_basalDendriteVoltage","_apicalDendriteVoltage","_phase","_astrocyteGlutamateLoad","_astrocytePotassiumLoad","_astrocyteLactateSupport",
        "_homeostaticActivityTrace","_homeostaticExcitabilityBias","_homeostaticInhibitoryBias"};
    static readonly FieldInfo[] StateFields=Fields.Select(x=>typeof(ModelNeuron).GetField(x,Private)??throw new MissingFieldException(x)).ToArray();
    static readonly FieldInfo Hh=typeof(ModelNeuron).GetField("_hh",Private)!;
    internal static void WriteStateHeader(StreamWriter w)=>w.WriteLine("timeMs,cell,voltage,m,h,n,q,p,l,"+string.Join(',',Fields)+",reserve,activation,demandRate,produced,requested,consumed,unmet,overflow,balanceError");
    internal static void WriteState(StreamWriter w,double time,int cell,ModelNeuron neuron)
    {
        var hh=(HodgkinHuxleyMembrane)Hh.GetValue(neuron)!;var e=neuron.Energy;
        var row=new[]{time,cell,hh.Voltage,hh.M,hh.H,hh.N,hh.Q,hh.P,hh.L}
            .Concat(StateFields.Select(f=>Convert.ToDouble(f.GetValue(neuron),CultureInfo.InvariantCulture)))
            .Concat(new[]{e.Reserve,e.ProductionActivation,e.DemandRate,e.Produced,e.Requested,e.Consumed,e.Unmet,e.Overflow,e.BalanceError});
        w.WriteLine(string.Join(',',row.Select(x=>x.ToString("R",CultureInfo.InvariantCulture))));
    }
    public static async Task<int> Run(string[] args)
    {
        if(args.Length<3||args[1]!="--boundary-timing"||args.Skip(3).Any(x=>x!="--smoke"))throw new ArgumentException("new-output --boundary-timing bundle.json [--smoke]");
        string output=Path.GetFullPath(args[0]);if(Directory.Exists(output))throw new InvalidOperationException("Use a new output directory.");Directory.CreateDirectory(output);
        void Save(string name,object value)=>File.WriteAllText(Path.Combine(output,name),JsonSerializer.Serialize(value,Json));
        try
        {
            var bundle=JsonSerializer.Deserialize<SplitNumericsProbe.Bundle>(File.ReadAllText(args[2]),Json)!;
            bool smoke=args.Contains("--smoke");int duration=smoke?8000:bundle.DurationMs;
            if(bundle.ReplayCases.Length!=2||bundle.DurationMs!=60000)throw new InvalidOperationException("Use the frozen two-train 60 s bundle.");
            Save("protocol.json",new{study="boundary-timing",smoke,durationMs=duration,caseCount=24,hostStepMs=.125,
                conditions=new[]{"connected","disconnected","markers"},steps=new[]{.125,.0625,.03125,.015625},
                bundleSha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(args[2]))),
                interpretation="Diagnostic boundaries consume no spike and release no transmitter. This study tests a scheduling mechanism; physiology and solver convergence remain unqualified."});
            File.Copy(args[2],Path.Combine(output,"inputs.json"));
            var cases=new List<object>();
            foreach(var replay in bundle.ReplayCases)foreach(double step in new[]{.125,.0625,.03125,.015625})foreach(string condition in new[]{"connected","disconnected","markers"})
            {
                string key=$"boundary-{replay.Id}-{condition}-h{step.ToString(CultureInfo.InvariantCulture)}";
                double[]? boundaries=condition=="markers"?replay.Inputs.Where(x=>x.Role=="climbing"&&x.ArrivalMs<duration).Select(x=>x.ArrivalMs).Distinct().Order().ToArray():null;
                Console.WriteLine($"CASE {cases.Count+1}/24: {key}");
                cases.Add(await SplitNumericsProbe.Replay(output,key,replay,duration,condition=="connected"?"calcium":"disconnected",step,boundaries,condition));
            }
            Save("results.json",new{completed=true,physiologicallyQualified=false,cases});return 0;
        }
        catch(Exception e){Save("FAILED.json",new{error=e.ToString()});Console.Error.WriteLine(e);return 1;}
    }
}

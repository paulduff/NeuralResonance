using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

internal static class SplitNumericsProbe
{
    static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web){Converters={new JsonStringEnumConverter()}};
    static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    internal sealed record Input(double ArrivalMs,SpikeMessage Message,string Role,int DelayMs);
    internal sealed record Contact(string Role,Guid ContactId,int Target);
    internal sealed record SourceCase(string Id,string Plan,int Offset,Input[] Inputs);
    internal sealed record ReplayCase(string Id,int Offset,Input[] Inputs,Contact[] Contacts,int TaughtCell);
    internal sealed record Bundle(SourceCase[] SourceCases,ReplayCase[] ReplayCases,int DurationMs);
    static void Save(string path,object value)=>File.WriteAllText(path,JsonSerializer.Serialize(value,Json));
    static void Row(StreamWriter writer,IEnumerable<double> values)=>writer.WriteLine(string.Join(',',values.Select(v=>v.ToString("R",CultureInfo.InvariantCulture))));
    static ModelNeuron[] Cells(StructureEngine e)=>(ModelNeuron[])typeof(StructureEngine).GetField("_neurons",Private)!.GetValue(e)!;
    static Dictionary<Guid,SynapseState> Contacts(StructureEngine e)=>(Dictionary<Guid,SynapseState>)typeof(StructureEngine).GetField("_inboundSynapses",Private)!.GetValue(e)!;
    delegate void Slope(ReadOnlySpan<double> state,Span<double> derivative,OlivaryInput input,out double load,out double current);

    public static async Task<int> Run(string[] args)
    {
        if(args.Length<3||args[1]!="--split-numerics"||args.Skip(3).Any(x=>x!="--smoke"&&x!="--self-test"))throw new ArgumentException("new-output --split-numerics bundle.json [--smoke] [--self-test]");
        var output=Path.GetFullPath(args[0]);if(Directory.Exists(output))throw new InvalidOperationException("Use new output, never reuse neural state.");Directory.CreateDirectory(output);
        try
        {
            if(args.Contains("--self-test")){SelfTest(output);return 0;}
            var bundle=JsonSerializer.Deserialize<Bundle>(File.ReadAllText(args[2]),Json)!;
            bool smoke=args.Contains("--smoke");int duration=smoke?4000:bundle.DurationMs;
            var cases=new List<object>();
            Save(Path.Combine(output,"protocol.json"),new{study="split-numerics",smoke,durationMs=duration,productionSourceCases=36,replayCases=24,
                sourceCouplingSteps=new[]{.025,.0125,.00625},sourceMembraneSteps=new[]{.025,.00625},replaySteps=new[]{.125,.0625,.03125,.015625},
                sourceOuterStep=.125,referenceScope="Membrane/receptor equations without metabolic potassium only; energy-enabled controls are separate.",
                physicalRecoveryAssessed=false,physiologicallyQualified=false});
            foreach(var source in bundle.SourceCases)
                foreach(bool energy in new[]{false,true})foreach(double coupling in new[]{.025,.0125,.00625})foreach(double membrane in energy?new[]{.025}:new[]{.025,.00625})
                {
                    string key=$"source-{source.Id}-e{energy}-c{coupling.ToString(CultureInfo.InvariantCulture)}-m{membrane.ToString(CultureInfo.InvariantCulture)}";
                    Console.WriteLine($"CASE {cases.Count+1}/60: {key}");cases.Add(Source(output,key,source,duration,energy,coupling,membrane));
                }
            foreach(var replay in bundle.ReplayCases)foreach(string variant in new[]{"calcium","no-entry","disconnected"})foreach(double step in new[]{.125,.0625,.03125,.015625})
            {
                string key=$"replay-{replay.Id}-{variant}-h{step.ToString(CultureInfo.InvariantCulture)}";
                Console.WriteLine($"CASE {cases.Count+1}/60: {key}");cases.Add(await Replay(output,key,replay,duration,variant,step));
            }
            Save(Path.Combine(output,"results.json"),new{completed=true,physiologicallyQualified=false,cases});return 0;
        }
        catch(Exception e){Save(Path.Combine(output,"FAILED.json"),new{error=e.ToString()});Console.Error.WriteLine(e);return 1;}
    }

    static object Source(string root,string key,SourceCase source,int duration,bool useEnergy,double coupling,double membraneStep)
    {
        string dir=Path.Combine(root,key);Directory.CreateDirectory(dir);
        var cell=new OlivaryMembrane();var synapses=new OlivarySynapticContacts();var energy=useEnergy?new NeuralEnergyBudget():null;
        var inputs=source.Inputs.Where(x=>x.ArrivalMs<duration).OrderBy(x=>x.ArrivalMs).ToArray();
        Save(Path.Combine(dir,"design.json"),new{key,kind="source",sourceId=source.Id,duration,useEnergy,coupling,membraneStep,inputs});
        using var trace=new StreamWriter(Path.Combine(dir,"trace.csv"));using var events=new StreamWriter(Path.Combine(dir,"events.jsonl"));
        trace.WriteLine("timeMs,vs,vd,va,h,n,xs,k,l,r,s,q,ca,ha,xa,ampa,gabaD,gabaS,reserve,activation,demandRate,produced,requested,consumed,unmet,overflow,balanceError");
        int next=0;double time=0;long soma=0,axon=0;var spikeTimes=new List<double>();
        void Event(string kind,object value)=>events.WriteLine(JsonSerializer.Serialize(new{kind,value},Json));
        while(time<duration)
        {
            while(next<inputs.Length&&inputs[next].ArrivalMs<=time){var x=inputs[next++];synapses.Release(x.Message,x.Message.VesicleQuanta);Event("arrival",new{x.Message.MessageId,scheduledMs=x.ArrivalMs,processedMs=time});}
            double end=Math.Min(duration,(Math.Floor(time/.125)+1)*.125);if(next<inputs.Length)end=Math.Min(end,inputs[next].ArrivalMs);
            SourceNumericalStepper.Advance(end-time,cell,synapses,energy,coupling,membraneStep);time=end;
            if(cell.SomaticSpikes!=soma||cell.AxonalSpikes!=axon){Event("crossing",new{timeMs=time,soma=cell.SomaticSpikes-soma,axon=cell.AxonalSpikes-axon});if(cell.AxonalSpikes>axon)spikeTimes.Add(time);}
            soma=cell.SomaticSpikes;axon=cell.AxonalSpikes;
            if(time%2==0)
            {
                var g=synapses.Conductances;
                Row(trace,new[]{time}.Concat(cell.Snapshot()).Concat(new[]{g.DendriticAmpa,g.DendriticGaba,g.SomaticGaba,energy?.Reserve??0,energy?.ProductionActivation??0,energy?.DemandRate??0,energy?.Produced??0,energy?.Requested??0,energy?.Consumed??0,energy?.Unmet??0,energy?.Overflow??0,energy?.BalanceError??0}));
            }
        }
        if(next!=inputs.Length||spikeTimes.Count!=axon)throw new InvalidOperationException("Source event accounting failed.");
        var summary=new{key,kind="source",sourceId=source.Id,duration,useEnergy,coupling,membraneStep,inputs=next,soma,axon,spikeTimes};Save(Path.Combine(dir,"summary.json"),summary);return summary;
    }

    internal static async Task<object> Replay(string root,string key,ReplayCase replay,int duration,string variant,double step,
        double[]? noOpBoundaries=null,string? boundaryCondition=null)
    {
        string dir=Path.Combine(root,key);Directory.CreateDirectory(dir);
        string? oldDir=Environment.GetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR"),oldId=Environment.GetEnvironmentVariable("SERVICE_INSTANCE"),oldMode=Environment.GetEnvironmentVariable("NRE_MICROTUBULE_MODE");
        try
        {
            Environment.SetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR",Path.Combine(dir,"state"));Environment.SetEnvironmentVariable("SERVICE_INSTANCE","isolated-purkinje");Environment.SetEnvironmentVariable("NRE_MICROTUBULE_MODE","classical");
            using var engine=new StructureEngine(new(StructureId.PurkinjeCellLayer,"HH",PurkinjeCalciumPlasticity.Rule,"Fixed recorded train",new DelayWindow(10,15)),2,step,noOpBoundaries);
            engine.DiagnosticBlockPurkinjeCalciumEntry=variant=="no-entry";
            var inputs=replay.Inputs.Where(x=>x.ArrivalMs<duration&&(variant!="disconnected"||x.Role!="climbing")).ToArray();
            var declared=inputs.ToDictionary(x=>x.Message.MessageId);var received=new HashSet<Guid>();
            using var events=new StreamWriter(Path.Combine(dir,"events.jsonl"));
            void Event(string kind,object value)=>events.WriteLine(JsonSerializer.Serialize(new{kind,value},Json));
            int boundariesProcessed=0;
            engine.DiagnosticBoundaryObserver=(scheduled,processed)=>{boundariesProcessed++;Event("boundary",new{scheduledMs=scheduled,processedMs=processed});};
            engine.ContactLearningObserver=o=>{if(!declared.TryGetValue(o.MessageId,out var input)||!received.Add(o.MessageId)||o.ProcessedMs!=input.ArrivalMs||o.ScheduledMs!=input.ArrivalMs)throw new InvalidOperationException("Replay time, identity or count changed.");Event("learning",o);};
            foreach(var c in replay.Contacts)if(engine.ResolveContactId(replay.Inputs.First(x=>x.Role==c.Role).Message)!=c.ContactId)throw new InvalidOperationException("Replay contact identity changed.");
            Save(Path.Combine(dir,"design.json"),new{key,kind="replay",sourceId=replay.Id,duration,variant,step,inputs,contacts=replay.Contacts,boundaryCondition,noOpBoundaries});
            using var trace=new StreamWriter(Path.Combine(dir,"trace.csv"));trace.WriteLine("timeMs,p0Soma,p0Dendrite,p1Soma,p1Dendrite,p0Reserve,p1Reserve,p0BalanceError,p1BalanceError,p0Unmet,p1Unmet");
            using var contacts=new StreamWriter(Path.Combine(dir,"contacts.csv"));contacts.WriteLine("timeMs,role,weight,ip3,calcium,store,integratedCalcium,inducedDepression,entry,extruded,massError");
            int next=0;var ordered=inputs.OrderBy(x=>x.Message.TimestampMs).ToArray();var spikes=new[]{new List<double>(),new List<double>()};var cells=Cells(engine);
            using var stateTrace=boundaryCondition is null?null:new StreamWriter(Path.Combine(dir,"cell-states.csv"));
            if(stateTrace is not null)BoundaryTimingProbe.WriteStateHeader(stateTrace);
            for(int n=1;n<=duration/.125;n++)
            {
                double time=n*.125;
                while(next<ordered.Length&&ordered[next].Message.TimestampMs<=time-.125)await engine.EnqueueSpikeAsync(ordered[next++].Message);
                await engine.ProcessTickAsync(new(n,time,.125,new NeuromodState(),new Dictionary<BrainRhythm,double>(),0){NeuralSubstrates=NeuralSubstrateSupply.ReferenceBath});
                foreach(var m in await engine.DrainOutboundSpikesAsync()){spikes[int.Parse(m.SourceNeuronId[2..])].Add(m.TimestampMs);Event("spike",m);}
                if(time%2!=0)continue;
                if(stateTrace is not null)for(int i=0;i<cells.Length;i++)BoundaryTimingProbe.WriteState(stateTrace,time,i,cells[i]);
                Row(trace,new[]{time,cells[0].MembranePotentialMillivolts,cells[0].LearningDendriteVoltageMv,cells[1].MembranePotentialMillivolts,cells[1].LearningDendriteVoltageMv,cells[0].Energy.Reserve,cells[1].Energy.Reserve,cells[0].Energy.BalanceError,cells[1].Energy.BalanceError,cells[0].Energy.Unmet,cells[1].Energy.Unmet});
                foreach(var c in replay.Contacts)
                {
                    if(!Contacts(engine).TryGetValue(c.ContactId,out var contact))continue;var ca=contact.PurkinjeCalcium;
                    contacts.Write(time.ToString("R",CultureInfo.InvariantCulture)+","+c.Role+",");Row(contacts,new[]{contact.VesicleQuanta,ca?.Ip3Um??0,ca?.CalciumUm??0,ca?.StoreUm??0,ca?.IntegratedCalciumUm??0,ca?.InducedDepression??0,ca?.EntryUm??0,ca?.ExtrudedUm??0,ca?.MaximumMassError??0});
                }
            }
            if(next!=inputs.Length||received.Count!=inputs.Length||boundariesProcessed!=(noOpBoundaries?.Length??0))throw new InvalidOperationException("Incomplete replay or no-op boundary accounting.");
            var summary=new{key,kind="replay",sourceId=replay.Id,duration,variant,step,inputs=received.Count,spikes,boundaryCondition,boundariesProcessed,contacts=Contacts(engine).Select(x=>new{contactId=x.Key,state=ContactLearningSnapshot.From(x.Value),peakCa=x.Value.PurkinjeCalcium?.PeakCalciumUm??0}).ToArray()};Save(Path.Combine(dir,"summary.json"),summary);return summary;
        }
        finally{Environment.SetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR",oldDir);Environment.SetEnvironmentVariable("SERVICE_INSTANCE",oldId);Environment.SetEnvironmentVariable("NRE_MICROTUBULE_MODE",oldMode);}
    }

    static void SelfTest(string output)
    {
        foreach(bool useEnergy in new[]{false,true})
        {
            var a=new OlivaryMembrane();var b=new OlivaryMembrane();var sa=new OlivarySynapticContacts();var sb=new OlivarySynapticContacts();var ea=useEnergy?new NeuralEnergyBudget():null;var eb=useEnergy?new NeuralEnergyBudget():null;
            for(int i=0;i<1600;i++)
            {
                if(i is 120 or 400)for(int c=0;c<4;c++){var m=new SpikeMessage{SourceInstanceKey="self",SourceNeuronId=c.ToString(),Neurotransmitter=i==120?NTEnum.GLUTAMATE:NTEnum.GABA};sa.Release(m,1);sb.Release(m,1);}
                OlivaryIntegration.Advance(.125,a,sa,ea,NeuralSubstrateSupply.ReferenceBath);
                SourceNumericalStepper.Advance(.125,b,sb,eb,.025,.025);
                if(!a.Snapshot().SequenceEqual(b.Snapshot())||a.AxonalSpikes!=b.AxonalSpikes||ea?.Reserve!=eb?.Reserve)throw new InvalidOperationException("Diagnostic default differs from production.");
            }
        }
        var cell=new OlivaryMembrane();var slope=typeof(OlivaryMembrane).GetMethod("Slope",Private)!.CreateDelegate<Slope>(cell);var vectors=new List<object>();
        foreach(double voltage in new[]{-60d,-25d,0d,20d})
        {
            var y=cell.Snapshot();y[0]=voltage;y[1]=voltage-5;y[2]=voltage+3;
            var input=new OlivaryInput(.12*.3,.1*.2,.05*.4);var f=new double[14];slope(y,f,input,out var load,out var current);
            vectors.Add(new{state=y,derivative=f,load,current});
        }
        Save(Path.Combine(output,"self-tests.json"),new{completed=true,defaultStepperMatchesProduction=true,initialState=cell.Snapshot(),vectors});
        Console.WriteLine("Diagnostic default matches production, with and without energy; derivative fixtures exported.");
    }
}

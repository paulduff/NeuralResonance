using System.Globalization;
using System.Text.Json;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

internal static class OliveCalibrationProbe
{
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web){WriteIndented=true};
    static void Save(string dir,string name,object value)=>File.WriteAllText(Path.Combine(dir,name),JsonSerializer.Serialize(value,Json));
    static readonly (string Name,OlivaryParameters Parameters,bool Energy)[] Variants =
    [ ("production",new(),true),("reference-bath-membrane",new(),false),
      ("low-cat",new(LowCalcium:.7),true),("high-cat",new(LowCalcium:1.5),true),
      ("no-hcn",new(Hcn:0),true),("no-cat",new(LowCalcium:0),true) ];
    static readonly string[] Protocols=["quiet","excitation","volley-4","volley-16","inhibition","pair-50","pair-100","pair-150","glucose","oxygen"];
    static readonly double[] Steps=[1,.25,.125];
    public static int Run(string[] args)
    {
        if(args.Length<1||args.Skip(1).Any(a=>a is not ("--olive-calibration" or "--smoke" or "--pilot")))throw new ArgumentException("new-output --olive-calibration [--smoke|--pilot]");
        var output=Path.GetFullPath(args[0]);
        if(Directory.Exists(output))throw new InvalidOperationException("Use a fresh output directory.");
        Directory.CreateDirectory(output);
        bool smoke=args.Contains("--smoke"),pilot=args.Contains("--pilot");
        int duration=smoke?250:pilot?5000:60000;
        var variants=pilot?Variants.Take(2).ToArray():Variants;
        var protocols=pilot?new[]{"quiet","excitation","volley-4","volley-16","inhibition"}:Protocols;
        var summaries=new List<object>();
        Environment.SetEnvironmentVariable("NRE_MICROTUBULE_MODE","classical");
        try
        {
            Save(output,"protocol.json",new{study="olivary-isolated-calibration",smoke,pilot,durationMilliseconds=duration,
                caseCount=variants.Length*protocols.Length*Steps.Length,cellsPerCondition=1,deterministic=true,
                dynamics=ModelNeuron.DynamicsVersion,membrane=OlivaryMembrane.Version,
                stepsMilliseconds=Steps,tracePeriodMilliseconds=2,
                scope="Fresh deterministic isolated cell per condition; phase-varying repeated inputs. No gap junctions, brain, learning, body or physical recovery.",
                inputs="1 ms release per contact, quanta=1. Full study: baseline 0-10s; excitation 20Hz 10-20s (1,4,16 distinct contacts), inhibition 100Hz 20-30s; paired single-contact GABA then GLU every 997ms 10-40s at stated delay. Fuel removed 10-30s and restored 30-60s. Pilot challenge 1-2s.",
                variants=variants.Select(v=>new{v.Name,v.Parameters,v.Energy}),protocols});
            foreach(var variant in variants)foreach(var protocol in protocols)foreach(double dt in Steps)
            {
                string key=$"{variant.Name}-{protocol}-{dt.ToString(CultureInfo.InvariantCulture)}";
                var cell=new ModelNeuron(0,"HH",StructureCircuitProfile.For(StructureId.InferiorOlive),variant.Parameters);
                var bare=new OlivaryMembrane(variant.Parameters);var synapses=new OlivarySynapticContacts();
                var membrane=variant.Energy?cell.Olive!:bare;
                var events=new List<object>();
                var segments=new List<object>();
                long lastSoma=0,lastAxon=0;double minimum=double.PositiveInfinity,maximum=double.NegativeInfinity,minReserve=1,maxBalance=0;
                int excEvents=0,inhEvents=0;
                using var trace=new StreamWriter(Path.Combine(output,key+".csv"));
                trace.WriteLine("timeMs,somaMv,dendriteMv,axonMv,calciumUm,hcn,catActivation,catAvailability,kca,reserve,produced,requested,consumed,unmet,overflow");
                for(int step=0;step<duration/dt;step++)
                {
                    double time=step*dt;
                    double start=pilot?1000:10000,stop=pilot?2000:20000;
                    bool exc=(protocol=="excitation"||protocol.StartsWith("volley-"))&&time>=start&&time<stop&&(time-start)%50==0;
                    bool inh=protocol=="inhibition"&&time>=start+(pilot?0:10000)&&time<stop+(pilot?0:10000)&&(time-start)%10==0;
                    if(protocol.StartsWith("pair-"))
                    {
                        int delay=int.Parse(protocol[5..]);
                        inh=time>=10000&&time<40000&&(time-10000)%997==0;
                        exc=time>=10000+delay&&time<40000&&(time-10000-delay)%997==0;
                    }
                    if(exc||inh)
                    {
                        int contacts=protocol.StartsWith("volley-")?int.Parse(protocol[7..]):1;
                        excEvents+=exc?contacts:0;inhEvents+=inh?contacts:0;
                        for(int contact=0;contact<contacts;contact++)
                        {
                            var spike=new SpikeMessage{SourceStructure=StructureId.InferiorOlive,TargetStructure=StructureId.InferiorOlive,
                                SourceInstanceKey="isolated-receptor-probe",SourceNeuronId="stimulus-"+contact,TimestampMs=time,
                                Neurotransmitter=exc?NTEnum.GLUTAMATE:NTEnum.GABA,VesicleQuanta=1};
                            if(variant.Energy)cell.Integrate(spike,dt);else synapses.Release(spike,1);
                        }
                    }
                    bool withdrawal=time>=10000&&time<30000;
                    cell.SetSubstrateSupply(new(protocol=="glucose"&&withdrawal?0:1,protocol=="oxygen"&&withdrawal?0:1,1,"explicit-isolated-bath"));
                    long beforeSoma=membrane.SomaticSpikes,beforeAxon=membrane.AxonalSpikes;
                    if(variant.Energy)cell.Step(dt);
                    else OlivaryIntegration.Advance(dt,bare,synapses,null,NeuralSubstrateSupply.ReferenceBath);
                    if(membrane.SomaticSpikes!=beforeSoma||membrane.AxonalSpikes!=beforeAxon)
                        events.Add(new{timeMs=time+dt,soma=membrane.SomaticSpikes-beforeSoma,axon=membrane.AxonalSpikes-beforeAxon});
                    minimum=Math.Min(minimum,membrane.SomaVoltage);maximum=Math.Max(maximum,membrane.SomaVoltage);
                    minReserve=Math.Min(minReserve,cell.Energy.Reserve);maxBalance=Math.Max(maxBalance,Math.Abs(cell.Energy.BalanceError));
                    if((step+1)%(2/dt)==0)
                    {
                        double[] row=[time+dt,membrane.SomaVoltage,membrane.DendriteVoltage,membrane.AxonVoltage,membrane.CalciumMicromolar,
                            membrane.HcnActivation,membrane.LowCalciumActivation,membrane.LowCalciumAvailability,membrane.CalciumPotassiumActivation,
                            cell.Energy.Reserve,cell.Energy.Produced,cell.Energy.Requested,cell.Energy.Consumed,cell.Energy.Unmet,cell.Energy.Overflow];
                        trace.WriteLine(string.Join(',',row.Select(v=>v.ToString("R",CultureInfo.InvariantCulture))));
                    }
                    if((step+1)%(10000/dt)==0||step+1==duration/dt)
                    {
                        segments.Add(new{endMs=time+dt,soma=membrane.SomaticSpikes-lastSoma,axon=membrane.AxonalSpikes-lastAxon,
                            minimumSoma=minimum,maximumSoma=maximum,minReserve,maxBalance});
                        lastSoma=membrane.SomaticSpikes;lastAxon=membrane.AxonalSpikes;
                        minimum=double.PositiveInfinity;maximum=double.NegativeInfinity;minReserve=1;
                    }
                }
                Save(output,key+"-events.json",events);
                var result=new{key,variant=variant.Name,protocol,dt,energyEnabled=variant.Energy,segments,
                    somaticSpikes=membrane.SomaticSpikes,axonalSpikes=membrane.AxonalSpikes,excEvents,inhEvents,
                    finalVoltage=membrane.SomaVoltage,finalReserve=cell.Energy.Reserve,unmet=cell.Energy.Unmet,maxBalance};
                summaries.Add(result);Save(output,key+"-summary.json",result);
                Console.WriteLine($"{key}: soma={membrane.SomaticSpikes}, axon={membrane.AxonalSpikes}, final Vm={membrane.SomaVoltage:F3} mV; {summaries.Count}/{variants.Length*protocols.Length*Steps.Length}");
            }
            Save(output,"results.json",new{completed=true,physiologicallyQualified=false,smoke,pilot,cases=summaries});
            return 0;
        }
        catch(Exception ex){Save(output,"FAILED.json",new{completed=false,error=ex.ToString(),completedCases=summaries.Count});Console.Error.WriteLine(ex);return 1;}
    }
}

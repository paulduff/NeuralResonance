using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

internal static class OlivePurkinjeProbe
{
    static readonly string[] Variants=["legacy","calcium","no-entry","no-release"];
    static readonly string[] Plans=["paired100","paired20","reversed100","early500","disconnected","quiet-olive","inhibition","glucose"];
    static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web){Converters={new JsonStringEnumConverter()}};
    static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static ModelNeuron[] Cells(StructureEngine e)=>(ModelNeuron[])typeof(StructureEngine).GetField("_neurons",Flags)!.GetValue(e)!;
    static Dictionary<Guid,SynapseState> Contacts(StructureEngine e)=>(Dictionary<Guid,SynapseState>)typeof(StructureEngine).GetField("_inboundSynapses",Flags)!.GetValue(e)!;
    static int Target(StructureEngine e,SpikeMessage m)=>(int)typeof(StructureEngine).GetMethod("ResolveInboundNeuronIndex",Flags)!.Invoke(e,[m])!;
    static int Delay(StructureEngine e,SpikeMessage m)=>(int)typeof(StructureEngine).GetMethod("ComputeConductionDelayMs",Flags)!.Invoke(e,[m])!;
    static void Save(string path,object value)=>File.WriteAllText(path,JsonSerializer.Serialize(value,Json));
    static void Row(StreamWriter w,params double[] values)=>w.WriteLine(string.Join(',',values.Select(v=>v.ToString("R",CultureInfo.InvariantCulture))));
    static SpikeMessage Fixture(string role,StructureId source,StructureId target,int index,double time,bool synthetic=false,NTEnum nt=NTEnum.GLUTAMATE)=>new(){
        MessageId=StableNeuralIdentity.Create("olive-purkinje-fixture",role,index,time),TimestampMs=time,
        SynapseId=StableNeuralIdentity.Create("olive-purkinje-projection",source,target,nt),
        ProjectionId=StableNeuralIdentity.Create("olive-purkinje-projection",source,target,nt),
        SourceInstanceKey="isolated-fixture-"+role,SourceNeuronId=$"n-{index:000}",TargetNeuronId="auto-000",
        SourceStructure=source,TargetStructure=target,Neurotransmitter=nt,VesicleQuanta=1,ReuptakeRate=8,
        SpikeType=SpikeTypeEnum.ACTION_POTENTIAL,IsSyntheticBackground=synthetic};

    public static async Task<int> Run(string[] args)
    {
        if(args.Length<1||args.Skip(1).Any(a=>a is not("--olive-purkinje" or "--smoke" or "--quick"))||args.Contains("--quick")&&!args.Contains("--smoke"))
            throw new ArgumentException("new-output --olive-purkinje [--smoke [--quick]]");
        string root=Path.GetFullPath(args[0]);
        if(Directory.Exists(root))throw new InvalidOperationException("Use a new isolated output directory.");
        Directory.CreateDirectory(root);
        bool smoke=args.Contains("--smoke"),quick=args.Contains("--quick");
        int duration=smoke?4000:240000;int[] offsets=quick?[0]:[0,60];double[] steps=[1,.125];
        var cases=new List<object>();
        string? oldState=Environment.GetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR"),oldInstance=Environment.GetEnvironmentVariable("SERVICE_INSTANCE"),oldMode=Environment.GetEnvironmentVariable("NRE_MICROTUBULE_MODE");
        try
        {
            Environment.SetEnvironmentVariable("NRE_MICROTUBULE_MODE","classical");
            Save(Path.Combine(root,"protocol.json"),new{study="olive-purkinje-calcium",smoke,quick,durationMs=duration,
                variants=Variants,plans=Plans,offsets,steps,caseCount=Variants.Length*Plans.Length*offsets.Length*steps.Length,
                calciumVersion=PurkinjeCalciumPlasticity.Version,climbingCompartmentVersion=PurkinjeClimbingCompartment.Version,dynamics=ModelNeuron.DynamicsVersion,samplePeriodMs=20,
                scope="One olive and two Purkinje production cells in fresh diagnostic StructureEngines. Production mapping, delays, ingress and axonal output. Fixture PF bursts, not a granule population. No dropped/merged olive events; disconnected control records undelivered events. Same new climbing conductance compartment in all variants; existing HH soma and generic dendritic model remain uncalibrated. Not whole-brain balance or a fitted calcium/complex-spike model.",
                calciumScope="Voltage-driven P-type channel reporter, local mGluR/IP3 and finite-store calcium, slow calcium-dependent LTD. No direct CF weight decrement, no generic plasticity budget in candidate LTD, no LTP. Channel density/buffering, receptor mapping, store parameters and consolidation reduction provisional.",
                controls="Legacy coincidence rule; calcium candidate; diagnostic P-channel entry or internal-store release block. PF leads olive stimulus by 100/20/500ms or follows by100ms. Actual arrival delays retained. Quiet olive, disconnected, 10-30s inhibition/fuel interruption. Four-minute observation includes 180s after last probes.",
                freshOnly=true,liveBrainAccessed=false,physicalRecoveryAssessed=false,physiologicallyQualified=false});
            foreach(string variant in Variants)foreach(string plan in Plans)foreach(int offset in offsets)foreach(double dt in steps)
            {
                string key=$"{variant}-{plan}-{offset}-{dt.ToString(CultureInfo.InvariantCulture)}";
                Console.WriteLine($"CASE {cases.Count+1}/{Variants.Length*Plans.Length*offsets.Length*steps.Length}: {key}");
                var result=await Case(root,key,variant,plan,offset,dt,duration,smoke);cases.Add(result);
                Save(Path.Combine(root,"progress.json"),new{completedCases=cases.Count,lastCase=key});
            }
            Save(Path.Combine(root,"results.json"),new{completed=true,physiologicallyQualified=false,cases});return 0;
        }
        catch(Exception e){Save(Path.Combine(root,"FAILED.json"),new{error=e.ToString(),completedCases=cases.Count});Console.Error.WriteLine(e);return 1;}
        finally{Environment.SetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR",oldState);Environment.SetEnvironmentVariable("SERVICE_INSTANCE",oldInstance);Environment.SetEnvironmentVariable("NRE_MICROTUBULE_MODE",oldMode);}
    }

    static StructureEngine Engine(string directory,StructureId id,string key,string rule,int count)
    {
        Environment.SetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR",directory);
        Environment.SetEnvironmentVariable("SERVICE_INSTANCE",key);
        return new(new(id,"HH",rule,"Isolated calcium comparison",new DelayWindow(10,15)),count);
    }
    static async Task<object> Case(string root,string key,string variant,string plan,int offset,double dt,int duration,bool smoke)
    {
        string dir=Path.Combine(root,key);Directory.CreateDirectory(dir);
        using var olive=Engine(Path.Combine(dir,"olive-state"),StructureId.InferiorOlive,"isolated-olive","STDP",1);
        using var purkinje=Engine(Path.Combine(dir,"purkinje-state"),StructureId.PurkinjeCellLayer,"isolated-purkinje",variant=="legacy"?"CerebellarLTD":PurkinjeCalciumPlasticity.Rule,2);
        purkinje.DiagnosticBlockPurkinjeCalciumEntry=variant=="no-entry";purkinje.DiagnosticBlockPurkinjeStoreRelease=variant=="no-release";
        var cells=Cells(olive).Concat(Cells(purkinje)).ToArray();var membrane=cells[0].Olive!;
        using var events=new StreamWriter(Path.Combine(dir,"events.jsonl"));
        void Event(string kind,object value)=>events.WriteLine(JsonSerializer.Serialize(new{kind,value},Json));
        purkinje.ContactLearningObserver=o=>Event("learning",o);
        olive.ContactLearningObserver=o=>{
            if(o.After.Any(c=>c.Weight!=1))throw new InvalidOperationException("Fixture receptor weight changed.");
            Event("olive-delivery",new{o.MessageId,o.ContactId,o.ScheduledMs,o.ProcessedMs,o.Target});};
        using var trace=new StreamWriter(Path.Combine(dir,"cells.csv"));
        trace.WriteLine("timeMs,oliveSoma,oliveDendrite,oliveAxon,pc0Soma,pc0Dendrite,pc1Soma,pc1Dendrite,oReserve,oProduced,oRequested,oConsumed,oUnmet,oOverflow,p0Reserve,p0Produced,p0Requested,p0Consumed,p0Unmet,p0Overflow,p1Reserve,p1Produced,p1Requested,p1Consumed,p1Unmet,p1Overflow,oliveGlucose");
        using var chemistry=new StreamWriter(Path.Combine(dir,"contacts.csv"));
        chemistry.WriteLine("timeMs,role,weight,preTrace,ip3,calcium,store,integratedCalcium,inducedDepression,entry,extruded,massError");
        var probeTimes=smoke?new[]{1000+offset,2000+offset,3000+offset}:Enumerable.Range(0,58).Select(i=>2000+offset+i*997).ToArray();
        int start=smoke?2000:10000,end=smoke?3000:30000;
        var climbTemplate=Fixture("climb-template",StructureId.InferiorOlive,StructureId.PurkinjeCellLayer,0,0);
        int taughtCell=Target(purkinje,climbTemplate);
        string[] roles=["active","late","dormant","other-cell","nonparallel"];
        var fixtures=new Dictionary<string,SpikeMessage>();
        foreach(string role in roles)
        {
            var source=role=="nonparallel"?StructureId.PontineNuclei:StructureId.CerebellarGranule;
            int target=role=="other-cell"?1-taughtCell:taughtCell;
            var fixture=Enumerable.Range(0,2048).Select(i=>Fixture(role,source,StructureId.PurkinjeCellLayer,i,100)).First(m=>Target(purkinje,m)==target);
            fixtures.Add(role,fixture);
        }
        var scheduled=new List<(string Role,SpikeMessage Message)>();
        foreach(var f in fixtures)scheduled.Add((f.Key,f.Value));
        int pfShift=plan=="paired20"?-20:plan=="reversed100"?100:plan=="early500"?-500:-100;
        foreach(int time in probeTimes)
        {
            if(plan!="quiet-olive")for(int i=0;i<4;i++)scheduled.Add(("olive-excitation",Fixture("olive-excitation-"+i,StructureId.PontineNuclei,StructureId.InferiorOlive,0,time,true)));
            foreach(string role in roles.Where(r=>r!="dormant"))for(int burst=0;burst<5;burst++)
            {
                var f=fixtures[role];double at=time+(role=="late"?300:pfShift)+burst*10;
                scheduled.Add((role,Fixture(role,f.SourceStructure,f.TargetStructure,int.Parse(f.SourceNeuronId[2..]),at)));
            }
        }
        if(plan=="inhibition")for(int t=start;t<end;t+=10)for(int i=0;i<4;i++)
            scheduled.Add(("olive-inhibition",Fixture("olive-inhibition-"+i,StructureId.FastigialNucleus,StructureId.InferiorOlive,0,t,true,NTEnum.GABA)));
        scheduled=scheduled.OrderBy(x=>x.Message.TimestampMs).ThenBy(x=>x.Role,StringComparer.Ordinal).ToList();
        if(scheduled.Any(x=>x.Message.TimestampMs<0||x.Message.TimestampMs+100>=duration))throw new InvalidOperationException("Incomplete schedule.");
        var contactRoles=fixtures.ToDictionary(f=>purkinje.ResolveContactId(f.Value),f=>f.Key);
        Save(Path.Combine(dir,"design.json"),new{key,variant,plan,offset,dt,duration,probeTimes,pfShift,challengeStart=start,challengeEnd=end,taughtCell,
            contacts=fixtures.Select(f=>new{role=f.Key,contactId=purkinje.ResolveContactId(f.Value),target=Target(purkinje,f.Value),fixture=f.Value}),
            inputs=scheduled.Select(x=>new{x.Role,x.Message,delayMs=Delay(x.Message.TargetStructure==StructureId.InferiorOlive?olive:purkinje,x.Message)})});
        int next=0,outgoing=0,delivered=0,pcSpikes=0;long soma=0,axon=0;double maxEnergyError=0;
        var pending=new Dictionary<Guid,SpikeMessage>();
        // Count actual received climbing inputs after the complete production learning path.
        var oldObserver=purkinje.ContactLearningObserver;
        purkinje.ContactLearningObserver=o=>{oldObserver!(o);if(o.Source==StructureId.InferiorOlive){if(!pending.Remove(o.MessageId))throw new InvalidOperationException("Unknown/duplicate climbing delivery.");delivered++;}};
        for(int n=1;n<=duration/dt;n++)
        {
            double time=n*dt;
            while(next<scheduled.Count&&scheduled[next].Message.TimestampMs<=time-dt)
            {
                var input=scheduled[next++];var e=input.Message.TargetStructure==StructureId.InferiorOlive?olive:purkinje;
                await e.EnqueueSpikeAsync(input.Message);Event("input",new{input.Role,input.Message});
            }
            var tick=new TickSignal(n,time,dt,new NeuromodState(),new Dictionary<BrainRhythm,double>(),0){NeuralSubstrates=NeuralSubstrateSupply.ReferenceBath};
            var supply=plan=="glucose"&&time>start&&time<=end?new NeuralSubstrateSupply(0,1,1,"isolated-glucose-withdrawal"):NeuralSubstrateSupply.ReferenceBath;
            await olive.ProcessTickAsync(tick with{NeuralSubstrates=supply});
            var output=await olive.DrainOutboundSpikesAsync();
            if(membrane.SomaticSpikes!=soma||membrane.AxonalSpikes!=axon)Event("olive-crossing",new{timeMs=time,soma=membrane.SomaticSpikes-soma,axon=membrane.AxonalSpikes-axon});
            if(output.Count!=membrane.AxonalSpikes-axon)throw new InvalidOperationException("Axonal event dropped.");
            soma=membrane.SomaticSpikes;axon=membrane.AxonalSpikes;
            foreach(var message in output)
            {
                bool connected=plan!="disconnected";outgoing++;
                if(Target(purkinje,message)!=taughtCell)throw new InvalidOperationException("Climbing target changed.");
                Event("climbing",new{message,connected,delayMs=Delay(purkinje,message)});
                if(connected){pending.Add(message.MessageId,message);await purkinje.EnqueueSpikeAsync(message);}
            }
            await purkinje.ProcessTickAsync(tick);
            foreach(var spike in await purkinje.DrainOutboundSpikesAsync()){Event("purkinje-spike",spike);pcSpikes++;}
            foreach(var cell in cells){maxEnergyError=Math.Max(maxEnergyError,Math.Abs(cell.Energy.BalanceError));if(cell.Energy.Unmet>1e-10)throw new InvalidOperationException("Unexpected unmet demand.");}
            if(time%20==0)
            {
                var row=new List<double>{time,membrane.SomaVoltage,membrane.DendriteVoltage,membrane.AxonVoltage,cells[1].MembranePotentialMillivolts,cells[1].LearningDendriteVoltageMv,cells[2].MembranePotentialMillivolts,cells[2].LearningDendriteVoltageMv};
                foreach(var cell in cells)row.AddRange([cell.Energy.Reserve,cell.Energy.Produced,cell.Energy.Requested,cell.Energy.Consumed,cell.Energy.Unmet,cell.Energy.Overflow]);row.Add(supply.GlucoseAvailability);Row(trace,row.ToArray());
                foreach(var contact in Contacts(purkinje).Values.Where(c=>contactRoles.ContainsKey(c.SynapseId)))
                {
                    var ca=contact.PurkinjeCalcium;double[] values=[contact.VesicleQuanta,contact.PreTrace,ca?.Ip3Um??0,ca?.CalciumUm??0,ca?.StoreUm??0,ca?.IntegratedCalciumUm??0,ca?.InducedDepression??0,ca?.EntryUm??0,ca?.ExtrudedUm??0,ca?.MaximumMassError??0];
                    chemistry.WriteLine(time.ToString("R",CultureInfo.InvariantCulture)+","+contactRoles[contact.SynapseId]+","+string.Join(',',values.Select(v=>v.ToString("R",CultureInfo.InvariantCulture))));
                }
            }
        }
        if(next!=scheduled.Count||pending.Count!=0)throw new InvalidOperationException("Undelivered scheduled events at end.");
        var result=new{key,variant,plan,offset,dt,duration,inputs=next,oliveSoma=soma,oliveAxon=axon,outgoing,delivered,pcSpikes,maxEnergyError,
            contacts=Contacts(purkinje).Values.Select(c=>new{state=ContactLearningSnapshot.From(c),role=contactRoles.GetValueOrDefault(c.SynapseId,"climbing"),peakCalcium=c.PurkinjeCalcium?.PeakCalciumUm??0,massError=c.PurkinjeCalcium?.MaximumMassError??0}).ToArray()};
        Save(Path.Combine(dir,"summary.json"),result);return result;
    }
}

using System.Globalization;
using System.Text.Json;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

// Isolated receptor challenges only. This never adds restart pulses to a service.
internal static class OliveResponseProbe
{
    const int ResponseWindowMs=250;
    static readonly string[] Challenges=["sham","inhibition-1","inhibition-4","glucose","oxygen"];
    static readonly string[] Schedules=["throughout","before-after"];
    static readonly int[] Offsets=[0,15,30,45,60,75,90,105];
    static readonly double[] Steps=[1,.125];
    static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web){WriteIndented=true};
    record Pulse(int Index,int TimeMs,string Phase);
    record Spike(double TimeMs,long Soma,long Axon);
    record Input(int TimeMs,string Transmitter,int Contacts);
    record Before(double SomaMv,double DendriteMv,double AxonMv,double CalciumUm,double Reserve);
    static void Save(string root,string name,object data)=>File.WriteAllText(Path.Combine(root,name),JsonSerializer.Serialize(data,Json));

    static Pulse[] Pulses(bool smoke,int offset,string schedule)
    {
        var times=new List<(int Time,string Phase)>();
        if(smoke)
        {
            times.Add((1000+offset,"before"));
            if(schedule=="throughout")times.Add((2000+offset,"during"));
            times.Add((3000+offset,"after-early"));
        }
        else
        {
            for(int i=0;i<8;i++)times.Add((2000+offset+i*997,"before"));
            if(schedule=="throughout")for(int i=0;i<20;i++)times.Add((10000+offset+i*997,"during"));
            for(int i=0;i<30;i++)times.Add((30000+offset+i*997,i<10?"after-early":i<20?"after-middle":"after-late"));
        }
        var result=times.Select((t,i)=>new Pulse(i,t.Time,t.Phase)).ToArray();
        int start=smoke?2000:10000,end=smoke?3000:30000,duration=smoke?4000:60000;
        foreach(var p in result)
        {
            if(p.TimeMs+ResponseWindowMs>duration)throw new InvalidOperationException("Incomplete response window.");
            if(p.Phase=="before"&&p.TimeMs+ResponseWindowMs>start||p.Phase=="during"&&(p.TimeMs<start||p.TimeMs+ResponseWindowMs>end)||p.Phase.StartsWith("after-")&&p.TimeMs<end)
                throw new InvalidOperationException("A response window straddles a challenge boundary.");
        }
        if(result.Zip(result.Skip(1)).Any(p=>p.First.TimeMs+ResponseWindowMs>=p.Second.TimeMs))throw new InvalidOperationException("Overlapping response windows.");
        return result;
    }
    static void Release(ModelNeuron cell,int time,int contacts,bool excitatory,double dt)
    {
        for(int contact=0;contact<contacts;contact++)cell.Integrate(new SpikeMessage{
            SourceStructure=StructureId.InferiorOlive,TargetStructure=StructureId.InferiorOlive,
            SourceInstanceKey="isolated-response-receptor-probe",SourceNeuronId=(excitatory?"exc-":"inh-")+contact,
            TimestampMs=time,Neurotransmitter=excitatory?NTEnum.GLUTAMATE:NTEnum.GABA,VesicleQuanta=1},dt);
    }
    public static int Run(string[] args)
    {
        if(args.Length<1||args.Skip(1).Any(a=>a is not ("--olive-response" or "--smoke" or "--quick"))||args.Contains("--quick")&&!args.Contains("--smoke"))
            throw new ArgumentException("new-output --olive-response [--smoke [--quick]]");
        string root=Path.GetFullPath(args[0]);
        if(Directory.Exists(root))throw new InvalidOperationException("Use a fresh output directory.");
        Directory.CreateDirectory(root);
        bool smoke=args.Contains("--smoke"),quick=args.Contains("--quick");
        int duration=smoke?4000:60000,start=smoke?2000:10000,end=smoke?3000:30000;
        int[] offsets=quick?[0]:Offsets;
        var cases=new List<object>();
        Environment.SetEnvironmentVariable("NRE_MICROTUBULE_MODE","classical");
        try
        {
            // Exercise the full schedule even in a short preflight, so an end
            // cutoff cannot silently remove a matched input in the long run.
            Save(root,"schedule-check.json",Offsets.SelectMany(offset=>Schedules.Select(schedule=>new{
                offsetMs=offset,schedule,pulses=Pulses(false,offset,schedule),smokePulses=Pulses(true,offset,schedule)})).ToArray());
            Save(root,"protocol.json",new{study="olivary-evoked-response-recovery",smoke,quick,durationMs=duration,challengeStartMs=start,challengeEndMs=end,
                responseWindowMs=ResponseWindowMs,excitatoryContacts=4,quanta=1,inhibitionPeriodMs=10,tracePeriodMs=2,
                caseCount=Challenges.Length*Schedules.Length*offsets.Length*Steps.Length,challenges=Challenges,schedules=Schedules,offsetsMs=offsets,stepsMs=Steps,
                dynamics=ModelNeuron.DynamicsVersion,membrane=OlivaryMembrane.Version,parameters=new OlivaryParameters(),
                scope="Fresh deterministic isolated production cell per condition; fixed reference channels. Onset offsets sample different initial oscillator states, not independent biological subjects. No learning, live brain, Avatar, gap junctions or physical recovery.",
                scheduleDescription="Full: 8 baseline probes at 2000+offset+i*997ms; optionally 20 during at 10000+offset+i*997; 30 after at 30000+offset+i*997, grouped into three blocks of ten. Every probe is 4 contacts, quanta 1, 1ms release. Smoke: one before, optional one during, one after. All response windows complete and nonoverlapping.",
                interpretation="Window-associated spikes are not automatically causal. Save background/off-window spikes and pre-probe state. Report suppression and restoration against matched sham; do not treat numerical completion as physiological qualification."});
            foreach(string challenge in Challenges)foreach(string schedule in Schedules)foreach(int offset in offsets)foreach(double dt in Steps)
            {
                string key=$"{challenge}-{schedule}-offset{offset}-{dt.ToString(CultureInfo.InvariantCulture)}";
                var pulses=Pulses(smoke,offset,schedule);
                var before=new Dictionary<int,Before>();var events=new List<Spike>();var inputs=new List<Input>();
                var cell=new ModelNeuron(0,"HH",StructureCircuitProfile.For(StructureId.InferiorOlive));var membrane=cell.Olive!;
                int pulseIndex=0,excEvents=0,inhEvents=0;
                double minimumReserve=1,maximumError=0;
                using var trace=new StreamWriter(Path.Combine(root,key+".csv"));
                trace.WriteLine("timeMs,somaMv,dendriteMv,axonMv,calciumUm,hcn,catActivation,catAvailability,kca,reserve,produced,requested,consumed,unmet,overflow,glucose,oxygen,perfusion");
                for(int step=0;step<duration/dt;step++)
                {
                    double time=step*dt;bool challenged=time>=start&&time<end;
                    var supply=new NeuralSubstrateSupply(challenge=="glucose"&&challenged?0:1,challenge=="oxygen"&&challenged?0:1,1,"explicit-isolated-response-bath");
                    cell.SetSubstrateSupply(supply);
                    if(pulseIndex<pulses.Length&&time==pulses[pulseIndex].TimeMs)
                    {
                        var pulse=pulses[pulseIndex++];
                        before[pulse.Index]=new(membrane.SomaVoltage,membrane.DendriteVoltage,membrane.AxonVoltage,membrane.CalciumMicromolar,cell.Energy.Reserve);
                        Release(cell,pulse.TimeMs,4,true,dt);excEvents+=4;inputs.Add(new(pulse.TimeMs,"glutamate",4));
                    }
                    // Independent branch: simultaneous excitation and inhibition
                    // must both be delivered, never selected by an either/or.
                    if(challenged&&challenge.StartsWith("inhibition-")&&(time-start)%10==0)
                    {
                        int contacts=int.Parse(challenge[11..]);
                        Release(cell,(int)time,contacts,false,dt);inhEvents+=contacts;inputs.Add(new((int)time,"gaba",contacts));
                    }
                    long soma=membrane.SomaticSpikes,axon=membrane.AxonalSpikes;
                    bool fired=cell.Step(dt);
                    if(fired!=(membrane.AxonalSpikes>axon))throw new InvalidOperationException("Outgoing event mismatch.");
                    if(membrane.SomaticSpikes!=soma||membrane.AxonalSpikes!=axon)events.Add(new(time+dt,membrane.SomaticSpikes-soma,membrane.AxonalSpikes-axon));
                    minimumReserve=Math.Min(minimumReserve,cell.Energy.Reserve);maximumError=Math.Max(maximumError,Math.Abs(cell.Energy.BalanceError));
                    if((step+1)%(2/dt)==0)
                    {
                        double[] row=[time+dt,membrane.SomaVoltage,membrane.DendriteVoltage,membrane.AxonVoltage,membrane.CalciumMicromolar,
                            membrane.HcnActivation,membrane.LowCalciumActivation,membrane.LowCalciumAvailability,membrane.CalciumPotassiumActivation,
                            cell.Energy.Reserve,cell.Energy.Produced,cell.Energy.Requested,cell.Energy.Consumed,cell.Energy.Unmet,cell.Energy.Overflow,
                            supply.GlucoseAvailability,supply.OxygenAvailability,supply.PerfusionAvailability];
                        trace.WriteLine(string.Join(',',row.Select(v=>v.ToString("R",CultureInfo.InvariantCulture))));
                    }
                }
                if(pulseIndex!=pulses.Length)throw new InvalidOperationException("An input was not delivered.");
                var responses=pulses.Select(p=>
                {
                    var window=events.Where(e=>e.TimeMs>p.TimeMs&&e.TimeMs<=p.TimeMs+ResponseWindowMs).ToArray();
                    var preceding=events.Where(e=>e.TimeMs>p.TimeMs-ResponseWindowMs&&e.TimeMs<=p.TimeMs).ToArray();
                    return new{p.Index,p.TimeMs,p.Phase,endMs=p.TimeMs+ResponseWindowMs,pre=before[p.Index],
                        soma=window.Sum(e=>e.Soma),axon=window.Sum(e=>e.Axon),
                        firstSomaLatencyMs=window.Where(e=>e.Soma>0).Select(e=>(double?)(e.TimeMs-p.TimeMs)).FirstOrDefault(),
                        firstAxonLatencyMs=window.Where(e=>e.Axon>0).Select(e=>(double?)(e.TimeMs-p.TimeMs)).FirstOrDefault(),
                        precedingSoma=preceding.Sum(e=>e.Soma),precedingAxon=preceding.Sum(e=>e.Axon)};
                }).ToArray();
                var outside=events.Where(e=>!pulses.Any(p=>e.TimeMs>p.TimeMs&&e.TimeMs<=p.TimeMs+ResponseWindowMs)).ToArray();
                var result=new{key,challenge,schedule,offsetMs=offset,dt,pulses,excEvents,inhEvents,responses,
                    somaticSpikes=membrane.SomaticSpikes,axonalSpikes=membrane.AxonalSpikes,
                    outsideSoma=outside.Sum(e=>e.Soma),outsideAxon=outside.Sum(e=>e.Axon),minimumReserve,finalReserve=cell.Energy.Reserve,
                    unmet=cell.Energy.Unmet,maximumError,finalSomaMv=membrane.SomaVoltage};
                Save(root,key+"-events.json",events);Save(root,key+"-inputs.json",inputs);Save(root,key+"-summary.json",result);cases.Add(result);
                Console.WriteLine($"{key}: {responses.Count(r=>r.axon>0)}/{responses.Length} probe windows with axonal response; {cases.Count}/{Challenges.Length*Schedules.Length*offsets.Length*Steps.Length}");
            }
            Save(root,"results.json",new{completed=true,physiologicallyQualified=false,smoke,cases});return 0;
        }
        catch(Exception error){Save(root,"FAILED.json",new{completed=false,error=error.ToString(),completedCases=cases.Count});Console.Error.WriteLine(error);return 1;}
    }
}

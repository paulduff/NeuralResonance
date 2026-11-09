using System.Reflection;
using System.Text.Json;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

// Actual StructureEngine/ModelNeuron experiment. Postsynaptic events are never
// injected or fabricated. This is a numerical integration qualification, not
// an assertion of biochemical consolidation or live cortical deployment.
internal static class CorticalPrecisionStudy
{
    internal static async Task Run(string output)
    {
        if(!Path.IsPathFullyQualified(output)||Directory.Exists(output))throw new ArgumentException("Require a new absolute output directory.");
        Directory.CreateDirectory(output);
        var json=new JsonSerializerOptions(JsonSerializerDefaults.Web){WriteIndented=true};
        var lines=new JsonSerializerOptions(JsonSerializerDefaults.Web);
        void Save(string name,object value)=>File.WriteAllText(Path.Combine(output,name),JsonSerializer.Serialize(value,json));
        var scenarios=new[] {
            new Scenario("legacy-paired",false,"STDP+SynapticTaggingCapture",166,false),
            new Scenario("precise-paired",true,"STDP+SynapticTaggingCapture",166,false),
            new Scenario("precise-neighbor",true,"STDP+SynapticTaggingCapture",165,false),
            new Scenario("precise-no-modulation",true,"STDP+SynapticTaggingCapture",null,false),
            new Scenario("precise-stdp",true,"STDP",166,false),
            new Scenario("precise-capture",true,"SynapticTaggingCapture",166,false),
            new Scenario("precise-synthetic",true,"STDP+SynapticTaggingCapture",166,true)
        };
        Save("protocol.json",new {version="cortical-precision-integration-v1",candidateVersion=CorticalPrecisionMode.Version,
            numericalVersion=CorticalContactNumerics.Version,scenarios,ticks=550,tickMs=1,cycles=10,cuesPerCycle=12,
            stateAbsoluteTolerance=2e-12,budgetAbsoluteTolerance=2e-13,receptorGainAbsoluteTolerance=1e-11,
            cueTarget=166,neighborTarget=165,referenceBath=true,warmStartTested=true,exactCheckpoint=false,
            legacyAggregateScoresAreNotBiochemistry=true,liveBrainAccessed=false,biologicalConsolidationQualified=false,avatarRecoveryAssessed=false});
        string[] names=["NRE_SYNAPSE_STATE_DIR","SERVICE_INSTANCE","NRE_MICROTUBULE_MODE"];
        var old=names.ToDictionary(n=>n,Environment.GetEnvironmentVariable);var completed=new List<object>();
        try
        {
            Environment.SetEnvironmentVariable("SERVICE_INSTANCE","isolated-cortical-precision-study");
            Environment.SetEnvironmentVariable("NRE_MICROTUBULE_MODE","classical");
            foreach(var scenario in scenarios)
            {
                Console.WriteLine("CASE "+scenario.Key);
                string folder=Path.Combine(output,scenario.Key);Directory.CreateDirectory(folder);
                Environment.SetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR",Path.Combine(folder,"state"));
                var profile=new StructureProfile(StructureId.TemporalAssociation,"Izhikevich",scenario.Rule,
                    "isolated cortical precision integration",new DelayWindow(0,0));
                int count=StructureCircuitProfile.For(profile.StructureId).NeuronCount;
                var template=Input(0,0,NTEnum.GLUTAMATE,scenario.Synthetic);
                var entries=new List<IsolatedSynapticTarget>{IsolatedSynapticTarget.From(template,166)};
                if(scenario.ModulationTarget is { } target)
                    entries.AddRange(new[]{NTEnum.ACETYLCHOLINE,NTEnum.DOPAMINE}.Select(nt=>IsolatedSynapticTarget.From(Input(0,0,nt,false),target)));
                var map=new IsolatedSynapticTargets(entries,count);
                Save(Path.Combine(scenario.Key,"declared-targets.json"),new {map.Fingerprint,map.Entries});
                using var learning=new StreamWriter(Path.Combine(folder,"learning.jsonl"));
                using var legacy=new StreamWriter(Path.Combine(folder,"legacy-learning.jsonl"));
                using var receptors=new StreamWriter(Path.Combine(folder,"receptors.jsonl"));
                using var ticks=new StreamWriter(Path.Combine(folder,"ticks.jsonl"));
                using var input=new StreamWriter(Path.Combine(folder,"inputs.jsonl"));
                using var firing=new StreamWriter(Path.Combine(folder,"firings.jsonl"));
                long pre=0,post=0,modulation=0,smallChanges=0,deliveries=0;int targetFirings=0;
                CorticalPrecisionContact[] final;MemoryContactEvidence[] oldFinal;
                using(var engine=new StructureEngine(profile,null,diagnosticSynapticTargets:map,diagnosticCorticalPrecision:scenario.Precise))
                {
                    // Read-only observation of the actual cell used by the engine;
                    // no test mutation of neuron state or invocation of LearnPost.
                    var cells=(ModelNeuron[])typeof(StructureEngine).GetField("_neurons",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(engine)!;
                    engine.CorticalNeuronFiringObserver=e=>{firing.WriteLine(JsonSerializer.Serialize(e,lines));if(e.Cell==166)++targetFirings;};
                    engine.CorticalPrecisionLearningObserver=e=>
                    {
                        learning.WriteLine(JsonSerializer.Serialize(e,lines));
                        if(e.Kind=="pre")++pre;else if(e.Kind=="post")++post;else if(e.Kind=="modulation")++modulation;
                        if(e.Before.State.Weight!=e.After.State.Weight&&(float)e.Before.State.Weight==(float)e.After.State.Weight)++smallChanges;
                    };
                    engine.CorticalPrecisionInputObserver=e=>{receptors.WriteLine(JsonSerializer.Serialize(e,lines));++deliveries;};
                    engine.MemoryLearningObserver=e=>legacy.WriteLine(JsonSerializer.Serialize(e,lines));
                    for(int tick=1;tick<=550;++tick)
                    {
                        if((tick-1)%55==0)
                        {
                            int cycle=(tick-1)/55;
                            var arrivals=Enumerable.Range(0,12).Select(i=>Input(cycle*100+i,tick-1,NTEnum.GLUTAMATE,scenario.Synthetic)).ToList();
                            if(scenario.ModulationTarget is not null)
                                arrivals.AddRange(new[]{Input(cycle,tick-1,NTEnum.ACETYLCHOLINE,false),Input(cycle,tick-1,NTEnum.DOPAMINE,false)});
                            input.WriteLine(JsonSerializer.Serialize(new {tick,arrivals},lines));
                            await engine.EnqueueSpikeBatchAsync(arrivals);
                        }
                        var signal=new TickSignal(tick,tick,1,new NeuromodState(),new Dictionary<BrainRhythm,double>(),0)
                            {NeuralSubstrates=NeuralSubstrateSupply.ReferenceBath};
                        var ack=await engine.ProcessTickAsync(signal);var emitted=await engine.DrainOutboundSpikesAsync();
                        ticks.WriteLine(JsonSerializer.Serialize(new {tick,targetFired=cells[166].IsActive,neighborFired=cells[165].IsActive,
                            receptors=cells[166].CaptureCorticalReceptors(),precise=engine.CaptureCorticalPrecisionContacts(),
                            legacy=engine.CaptureMemoryContactEvidence(),diagnostics=ack.SynapticLearningDiagnostics,emitted},lines));
                    }
                    final=engine.CaptureCorticalPrecisionContacts();oldFinal=engine.CaptureMemoryContactEvidence();
                }
                var statePath=Path.Combine(folder,"state","isolated-cortical-precision-study.synapses.json");
                File.Copy(statePath,Path.Combine(folder,"trained-state.json"));
                Save(Path.Combine(scenario.Key,"final.json"),new {precise=final,legacy=oldFinal});
                if(scenario.Precise)
                {
                    using(var restored=new StructureEngine(profile,null,diagnosticSynapticTargets:map,
                        diagnosticCorticalPrecision:true,diagnosticCorticalWarmStart:true))
                    {
                        var before=restored.CaptureCorticalPrecisionContacts();
                        var signal=new TickSignal(1,1001,1,new NeuromodState(),new Dictionary<BrainRhythm,double>(),0)
                            {NeuralSubstrates=NeuralSubstrateSupply.ReferenceBath};
                        await restored.ProcessTickAsync(signal);
                        Save(Path.Combine(scenario.Key,"warm.json"),new {before,after=restored.CaptureCorticalPrecisionContacts(),
                            explicitNewOriginMs=1000,membraneRestored=false,queuedEventsRestored=false});
                    }
                    File.Copy(statePath,Path.Combine(folder,"warm-state.json"));
                }
                var row=new {scenario.Key,scenario.Precise,completed=true,pre,post,modulation,smallChanges,deliveries,targetFirings};
                completed.Add(row);Save(Path.Combine(scenario.Key,"result.json"),row);Save("completed-cases.json",completed);
            }
            Save("results.json",new {completed=true,cases=completed,originalConsolidationTestQualified=false,
                biologicalConsolidationQualified=false,liveBrainAccessed=false,avatarRecoveryAssessed=false});
        }
        catch(Exception error){Save("FAILED.json",new {error=error.ToString(),completed});throw;}
        finally{foreach(var pair in old)Environment.SetEnvironmentVariable(pair.Key,pair.Value);}
    }

    private static SpikeMessage Input(int id,double time,NTEnum nt,bool synthetic)=>new()
    {
        MessageId=StableNeuralIdentity.Create("precision-study-event",id,nt),TimestampMs=time,
        SourceStructure=nt==NTEnum.ACETYLCHOLINE?StructureId.NucleusBasalis:nt==NTEnum.DOPAMINE?StructureId.Vta:StructureId.EntorhinalCortex,
        SourceInstanceKey="isolated-precision-source",SourceNeuronId="source-cell-0",TargetStructure=StructureId.TemporalAssociation,
        TargetNeuronId="declared-target",SynapseId=StableNeuralIdentity.Create("precision-study-projection",nt),
        IsSyntheticBackground=synthetic,Neurotransmitter=nt,VesicleQuanta=1.15f,ReuptakeRate=8,SpikeType=SpikeTypeEnum.ACTION_POTENTIAL
    };
    private sealed record Scenario(string Key,bool Precise,string Rule,int? ModulationTarget,bool Synthetic);
}

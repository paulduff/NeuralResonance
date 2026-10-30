using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

internal static class EnergyRecoveryProbe
{
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
    static readonly JsonSerializerOptions Compact = new(JsonSerializerDefaults.Web);
    static readonly StructureId[] Profiles = [StructureId.CerebellarVermis, StructureId.PurkinjeCellLayer, StructureId.InferiorOlive];
    static readonly string[] Controls = ["candidate", "no-hcn", "no-slow-inward", "reference-channels", "prior-drive"];
    static readonly string[] Scenarios = ["quiet", "receptor-challenge", "oxygen-withdrawal", "glucose-withdrawal"];
    static void Save(string path, object value) => File.WriteAllText(path, JsonSerializer.Serialize(value, Json));
    public static int Run(string[] args)
    {
        if (args.Skip(1).Any(a => a is not ("--energy-recovery" or "--smoke" or "--quick"))) throw new ArgumentException("Unknown probe argument");
        var output = Path.GetFullPath(args[0]);
        if (Directory.Exists(output)) throw new InvalidOperationException("Use a new isolated output.");
        Directory.CreateDirectory(output);
        bool smoke = args.Contains("--smoke"), quick = args.Contains("--quick");
        int duration = smoke ? 600 : quick ? 6000 : 60000, count = smoke ? 2 : quick ? 4 : 32;
        int phaseMs = duration / 6;
        var old = Environment.GetEnvironmentVariable("NRE_MICROTUBULE_MODE");
        Environment.SetEnvironmentVariable("NRE_MICROTUBULE_MODE", "classical");
        try
        {
            Save(Path.Combine(output, "protocol.json"), new {
                Study = "substrate-energy-hcn-recovery", Smoke = smoke, Quick = quick, CaseCount = 120,
                DurationMilliseconds = duration, PhaseMilliseconds = phaseMs, SampledCellsPerCondition = count,
                IntegrationStepsMilliseconds = new[]{.25,.125}, Scenarios, Profiles, Controls,
                ModelDynamicsVersion = ModelNeuron.DynamicsVersion, ClockVersion = IntrinsicCircuitDriveTopology.ClockVersion,
                ReceptorSchedule = "Glutamate every 50 ms during phase 1; GABA every 10 ms during phase 3, challenge only. Phases are zero-based. No restart pulse.",
                FuelSchedule = "Reference bath throughout except withdrawal phases 1 and 2. Restore at phase 3. Oxygen withdrawal leaves glycolysis available; glucose withdrawal stops both pathways.",
                ChannelParameters = Profiles.Select(id=>new { Profile=id, Hcn=HcnChannelProfile.For(id), SlowInward=SlowInwardChannelProfile.For(id) }),
                DriveConversionMicroampPerCm2 = 12,
                ChannelControls = "Candidate; HCN blocked; slow inward current blocked; both blocked (reference channels); prior normalized-drive conversion (10 instead of 12). Fresh membranes only. No support clamps or restart pulses.",
                Scope = "Reduced hybrid cell mechanisms; normalized energy units, provisional parameters, not a calibrated cerebellar cell, connected brain, walking or physical recovery.",
                FreshCellsOnly = true, LiveBrainAccessed = false, OldWeightsLoaded = false, BiologicalEquivalenceQualified = false
            });
            var results = new List<object>();
            foreach (var id in Profiles)
            foreach (string control in Controls)
            foreach (double dt in new[]{.25,.125})
            foreach (string scenario in Scenarios)
            {
                var circuit = StructureCircuitProfile.For(id);
                var cells = Enumerable.Range(0,count).Select(i => new ModelNeuron(i*37%circuit.NeuronCount,"HH",circuit)).ToArray();
                bool hcn = control is not ("no-hcn" or "reference-channels");
                bool inward = control is not ("no-slow-inward" or "reference-channels");
                if (!hcn || !inward)
                    foreach(var cell in cells) typeof(ModelNeuron).GetField("_hh",BindingFlags.Instance|BindingFlags.NonPublic)!
                        .SetValue(cell,new HodgkinHuxleyMembrane(cell.MembranePotentialMillivolts, HcnChannelProfile.For(id) with { Conductance = hcn ? HcnChannelProfile.For(id).Conductance : 0 }, SlowInwardChannelProfile.For(id) with { Conductance = inward ? SlowInwardChannelProfile.For(id).Conductance : 0 }));
                string name = $"{id}-{control}-{dt.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture).Replace(".","")}-{scenario}";
                var membranes = cells.Select(c => (HodgkinHuxleyMembrane)typeof(ModelNeuron).GetField("_hh",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(c)!).ToArray();
                var phases = Enumerable.Range(0,6).Select(_=>cells.Select(c=>new Stats{CellIndex=c.Index}).ToArray()).ToArray();
                long excitatoryEvents=0,inhibitoryEvents=0;
                var excitation = new SpikeMessage { SourceStructure=StructureId.VestibularAfferents,TargetStructure=id,SourceInstanceKey="isolated-energy-probe",SourceNeuronId="imposed-input",Neurotransmitter=NTEnum.GLUTAMATE,VesicleQuanta=1 };
                var inhibition = new SpikeMessage { SourceStructure=StructureId.PurkinjeCellLayer,TargetStructure=id,SourceInstanceKey="isolated-energy-probe",SourceNeuronId="imposed-input",Neurotransmitter=NTEnum.GABA,VesicleQuanta=1 };
                using(var trace = new StreamWriter(Path.Combine(output,name+".jsonl")))
                for(int step=0;step<duration/dt;step++)
                {
                    double start=step*dt,end=start+dt; int phase=(int)(start/phaseMs);
                    bool glut=scenario=="receptor-challenge" && phase==1 && (start-phaseMs)%50==0;
                    bool gaba=scenario=="receptor-challenge" && phase==3 && (start-3*phaseMs)%10==0;
                    var supply = phase is 1 or 2 && scenario=="oxygen-withdrawal" ? new NeuralSubstrateSupply(1,0,1,"oxygen-withdrawal")
                        : phase is 1 or 2 && scenario=="glucose-withdrawal" ? new NeuralSubstrateSupply(0,1,1,"glucose-withdrawal")
                        : NeuralSubstrateSupply.ReferenceBath;
                    for(int i=0;i<count;i++)
                    {
                        var cell=cells[i]; cell.SetSubstrateSupply(supply);
                        if(glut){excitation.TimestampMs=start;cell.Integrate(excitation,dt);excitatoryEvents++;}
                        if(gaba){inhibition.TimestampMs=start;cell.Integrate(inhibition,dt);inhibitoryEvents++;}
                        IntrinsicCircuitDriveTopology.Resolve(id,cell.Index,(long)Math.Ceiling(end),out var exc,out var inh);
                        cell.IntegratePacemakerDrive(control=="prior-drive" ? exc*10f/12f : exc,inh); cell.Step(dt);
                        var s=phases[phase][i]; s.Steps++; s.Spikes+=cell.IsActive?1:0;
                        if(cell.IsActive){s.FirstSpikeMs ??= end;s.LastSpikeMs=end;}
                        s.MinimumReserve=Math.Min(s.MinimumReserve,cell.Energy.Reserve);s.EndReserve=cell.Energy.Reserve;
                        s.MinimumVoltage=Math.Min(s.MinimumVoltage,cell.MembranePotentialMillivolts);s.MaximumVoltage=Math.Max(s.MaximumVoltage,cell.MembranePotentialMillivolts);
                        s.MaximumBalanceError=Math.Max(s.MaximumBalanceError,Math.Abs(cell.Energy.BalanceError));
                        s.Finite &= double.IsFinite(cell.MembranePotentialMillivolts) && double.IsFinite(cell.Energy.Reserve);
                    }
                    if(end%100==0) trace.WriteLine(JsonSerializer.Serialize(new { TimestampMs=end,Phase=phase, Supply=supply,
                        Cells=cells.Select((c,i)=>new {c.Index,Voltage=c.MembranePotentialMillivolts,HcnGate=membranes[i].Q,InwardActivation=membranes[i].P,InwardAvailability=membranes[i].L,MetabolicPotassium=c.Energy.PotassiumConductance,c.Energy.Reserve,c.Energy.ProductionActivation,c.Energy.DemandRate,c.Energy.Produced,c.Energy.Requested,c.Energy.Consumed,c.Energy.Unmet,c.Energy.Overflow,c.Energy.BalanceError})},Compact));
                }
                results.Add(new {Name=name,Profile=id,Control=control,HcnEnabled=hcn,SlowInwardEnabled=inward,DtMs=dt,Scenario=scenario,ExcitatoryEvents=excitatoryEvents,InhibitoryEvents=inhibitoryEvents,Phases=phases,
                    Energy=cells.Select(c=>new{c.Index,c.Energy.Reserve,c.Energy.Produced,c.Energy.Requested,c.Energy.Consumed,c.Energy.Unmet,c.Energy.Overflow,c.Energy.BalanceError}).ToArray()});
                Console.WriteLine($"Completed {results.Count}/120: {name}; final rate {phases[5].Average(s=>s.Spikes)*1000/phaseMs:F2} Hz, reserve {cells.Average(c=>c.Energy.Reserve):F3}");
            }
            Save(Path.Combine(output,"results.json"),new{Completed=true,Cases=results});
            return 0;
        }
        catch(Exception e){Save(Path.Combine(output,"FAILED.json"),new{Error=e.ToString()});throw;}
        finally{Environment.SetEnvironmentVariable("NRE_MICROTUBULE_MODE",old);}
    }
    sealed class Stats
    {
        public int CellIndex{get;set;} public int Steps{get;set;} public int Spikes{get;set;}
        public double MinimumReserve{get;set;}=1; public double EndReserve{get;set;}
        public double MinimumVoltage{get;set;}=double.MaxValue; public double MaximumVoltage{get;set;}=double.MinValue;
        public double? FirstSpikeMs{get;set;} public double? LastSpikeMs{get;set;}
        public double MaximumBalanceError{get;set;} public bool Finite{get;set;}=true;
    }
}

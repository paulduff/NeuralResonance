#nullable enable
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using NeuralResonanceEngine.Protocol;
using NRE.SimAvatar;
using UnitRow=MusclePopulationInitialStudy.UnitRow;

// Diagnostic arrivals only: this qualifies a population's continuous muscle
// equations, not the neuronal source, recruitment or live body integration.
internal static class MusclePopulationDynamicsStudy
{
    internal sealed record Event(int UnitIndex,double OffsetMs);
    internal sealed record Knot(double OffsetMs,double LengthOffsetMeters);
    internal sealed record Case(string Key,DynamicMuscleGeometry Geometry,FiberBoundary Boundary,
        double InitialTimeMs,double InitialFiberLengthMeters,double InitialMusculotendonLengthMeters,
        double DurationMs,Knot[] LengthKnots,Event[] Events);
    internal sealed record Input(string Version,string ParameterSha256,UnitRow[] ParameterRows,
        double[] StepsMs,double SampleMs,Case[] Cases);
    internal static void Run(string inputFile,string output)
    {
        if(!Path.IsPathFullyQualified(inputFile)||!Path.IsPathFullyQualified(output)||Directory.Exists(output))
            throw new ArgumentException("Supply a preserved absolute input and a NEW output directory.");
        var json=new JsonSerializerOptions(JsonSerializerDefaults.Web){WriteIndented=true};json.Converters.Add(new JsonStringEnumConverter());
        var compact=new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var input=JsonSerializer.Deserialize<Input>(File.ReadAllText(inputFile),json)??throw new ArgumentException("Missing population dynamics input.");
        if(input.Version!="named-ta-full-slow-population-dynamics-v1"||
            input.ParameterSha256!="01123aec76d1acc18b306ae1bd9daabfd29fe9287b54212aef5db3f15f719ebd"||
            input.ParameterRows.Length!=147||!input.ParameterRows.Select(r=>r.Row).SequenceEqual(Enumerable.Range(0,147))||
            !input.ParameterRows.Select(r=>r.SourceRow).SequenceEqual(Enumerable.Range(1,147))||
            input.ParameterRows[0].Calcium!=MotorUnitCalciumParameters.NagamoriSlowUnit1||
            !input.StepsMs.SequenceEqual(new[]{.125,.0625,.03125})||input.SampleMs!=.25||input.Cases.Length!=8||
            input.Cases.Select(c=>c.Key).Distinct(StringComparer.Ordinal).Count()!=8)
            throw new ArgumentException("Require the complete original slow population and prescribed comparison inventory.");
        Directory.CreateDirectory(output);File.Copy(inputFile,Path.Combine(output,"input.json"));
        var results=new List<object>();int passed=0;
        foreach(var scenario in input.Cases)
        foreach(var step in input.StepsMs)
        {
            string key=scenario.Key+"-h"+step.ToString("R",CultureInfo.InvariantCulture);
            string folder=Path.Combine(output,key);Directory.CreateDirectory(folder);
            AvatarDynamicMuscle? muscle=null;int samples=0,delivered=0;
            void Save(string name,object value)=>File.WriteAllText(Path.Combine(folder,name),JsonSerializer.Serialize(value,json));
            try
            {
                Console.WriteLine("FULL SLOW POPULATION "+key);
                if(scenario.DurationMs!=200||!double.IsFinite(scenario.InitialTimeMs)||scenario.InitialTimeMs<0||
                    !Enum.IsDefined(scenario.Boundary)||scenario.LengthKnots.Length<2||scenario.LengthKnots[0].OffsetMs!=0||
                    scenario.LengthKnots[^1].OffsetMs!=scenario.DurationMs||scenario.LengthKnots[0].LengthOffsetMeters!=0||
                    scenario.LengthKnots.Any(k=>!double.IsFinite(k.OffsetMs)||!double.IsFinite(k.LengthOffsetMeters))||
                    scenario.LengthKnots.Zip(scenario.LengthKnots.Skip(1)).Any(p=>p.First.OffsetMs>=p.Second.OffsetMs)||
                    scenario.Events.Any(e=>e.UnitIndex<0||e.UnitIndex>=147||!double.IsFinite(e.OffsetMs)||e.OffsetMs<=0||e.OffsetMs>scenario.DurationMs)||
                    !scenario.Events.SequenceEqual(scenario.Events.OrderBy(e=>e.OffsetMs).ThenBy(e=>e.UnitIndex))||
                    scenario.Events.Distinct().Count()!=scenario.Events.Length)
                    throw new ArgumentException("Invalid prospective event/length protocol.");
                var clock=new MotorAxonClock(StableNeuralIdentity.Create("slow-population-diagnostic",scenario.Key),0,scenario.InitialTimeMs);
                var definitions=input.ParameterRows.Select(r=>new SlowMotorUnitDefinition(
                    MotorAxonContact.Create("isolated-population-fixture-"+scenario.Key,"alpha-"+r.SourceRow.ToString("D3"),
                        StableNeuralIdentity.Create("slow-population-fixture-projection",scenario.Key,r.SourceRow),
                        "unit-"+r.SourceRow.ToString("D3"),0),r.Calcium,r.LengthGainAboveOptimal,r.LengthGainBelowOptimal)).ToArray();
                var ordinals=new long[147];
                var arrivals=scenario.Events.Select(e=>
                {
                    var c=definitions[e.UnitIndex].Contact;long ordinal=++ordinals[e.UnitIndex];
                    return new AvatarMotorAxonArrival(clock.SessionId,c.SourceInstanceKey,
                        StableNeuralIdentity.Create("slow-population-fixture-event",scenario.Key,e.UnitIndex,ordinal),ordinal,
                        c.SourceNeuronId,c.ProjectionId,StableNeuralIdentity.Create("slow-population-fixture-synapse",scenario.Key,e.UnitIndex),
                        c.ContactId,c.MotorUnitId,e.OffsetMs,clock.ToBodyMs(e.OffsetMs));
                }).ToArray();
                Save("declared.json",new{clock,definitions,arrivals,scenario,stepMs=step,
                    prescribedArrivals=true,neuronalModelExecuted=false,liveBodyAccessed=false});
                muscle=new AvatarDynamicMuscle(scenario.Geometry,definitions,clock,scenario.InitialFiberLengthMeters,
                    scenario.InitialMusculotendonLengthMeters,boundary:scenario.Boundary);
                string[] core={"timeMs","totalLengthMeters","fiberLengthMeters","fiberVelocityMetersPerSecond","yieldFraction",
                    "activeFiberForceNewtons","passiveFiberForceNewtons","tendonAxialForceNewtons","projectedTendonForceNewtons",
                    "projectedFiberForceNewtons","geometricForceNewtons","accelerationMetersPerSecondSquared","impulseNewtonSeconds",
                    "workJoules","momentumLedger","kineticLedger"};
                string[] unitNames={"releaseDrive","freeCalcium","boundCalcium","activation","released","recovered","netBound","calciumLedger","arrivals"};
                var columns=core.Concat(definitions.SelectMany((_,i)=>unitNames.Select(n=>"u"+i+"_"+n))).ToArray();
                long expectedSamples=checked((long)(scenario.DurationMs/input.SampleMs)+1);
                Save("trace-format.json",new{version="little-endian-f64-muscle-population-v1",magic="NRMUSC01",
                    columns,expectedSamples,initialTimeMs=clock.BodyEpochMs,sampleMs=input.SampleMs});
                using var compressed=new GZipStream(File.Create(Path.Combine(folder,"trace.f64.gz")),CompressionLevel.Fastest);
                using var writer=new BinaryWriter(compressed,Encoding.UTF8,leaveOpen:true);
                writer.Write(Encoding.ASCII.GetBytes("NRMUSC01"));writer.Write(columns.Length);writer.Write(expectedSamples);
                using var events=new StreamWriter(Path.Combine(folder,"arrivals.jsonl"));
                double LengthAt(double time)
                {
                    double relative=time-clock.BodyEpochMs;int index=0;
                    while(index+1<scenario.LengthKnots.Length-1&&relative>scenario.LengthKnots[index+1].OffsetMs)index++;
                    var a=scenario.LengthKnots[index];var b=scenario.LengthKnots[index+1];
                    return scenario.InitialMusculotendonLengthMeters+a.LengthOffsetMeters+
                        (b.LengthOffsetMeters-a.LengthOffsetMeters)*(relative-a.OffsetMs)/(b.OffsetMs-a.OffsetMs);
                }
                void Advance(double until)
                {
                    while(muscle.TimeMs<until)
                    {
                        double end=until;
                        foreach(var knot in scenario.LengthKnots)
                        {
                            double boundary=clock.BodyEpochMs+knot.OffsetMs;
                            if(boundary>muscle.TimeMs&&boundary<end)end=boundary;
                        }
                        var before=muscle.Capture();
                        muscle.AdvancePrescribed(new(before.TimeMs,end,before.MusculotendonLengthMeters,LengthAt(end)));
                    }
                }
                void Sample()
                {
                    var s=muscle.Capture();var f=s.Forces;
                    var values=new List<double>{s.TimeMs,s.MusculotendonLengthMeters,s.FiberLengthMeters,s.FiberVelocityMetersPerSecond,
                        s.Yield,f.ActiveFiberForceNewtons,f.PassiveFiberForceNewtons,f.TendonAxialForceNewtons,f.ProjectedTendonForceNewtons,
                        f.ProjectedFiberForceNewtons,f.GeometricForceNewtons,f.AccelerationMetersPerSecondSquared,
                        s.IntegratedNetImpulseNewtonSeconds,s.IntegratedNetFiberWorkJoules,s.MaximumMomentumLedgerError,s.MaximumKineticLedgerError};
                    foreach(var u in s.Units)values.AddRange(new[]{u.ReleaseDrive,u.FreeCalcium,u.BoundCalcium,u.Activation,
                        u.ReleasedCalcium,u.RecoveredCalcium,u.NetBoundCalcium,u.MaximumCalciumLedgerError,(double)u.Arrivals});
                    if(values.Count!=columns.Length||values.Any(v=>!double.IsFinite(v)))throw new InvalidOperationException("Invalid complete population sample.");
                    foreach(double value in values)writer.Write(value);samples++;
                }
                Sample();long ticks=checked((long)(scenario.DurationMs/step));long sampleTicks=checked((long)(input.SampleMs/step));
                if(ticks*step!=scenario.DurationMs||sampleTicks*step!=input.SampleMs)throw new ArgumentException("Nonintegral declared grid.");
                for(long tick=1;tick<=ticks;tick++)
                {
                    double end=clock.BodyEpochMs+tick*step;
                    while(delivered<arrivals.Length&&arrivals[delivered].ArrivalAtBodyMs<=end)
                    {
                        var arrival=arrivals[delivered];Advance(arrival.ArrivalAtBodyMs);muscle.Receive(arrival);
                        events.WriteLine(JsonSerializer.Serialize(arrival,compact));delivered++;
                    }
                    Advance(end);if(tick%sampleTicks==0)Sample();
                }
                if(delivered!=arrivals.Length||samples!=expectedSamples)throw new InvalidOperationException("Incomplete event/sample inventory.");
                Save("final.json",muscle.Capture());passed++;
                results.Add(new{key,completed=true,delivered,samples});
            }
            catch(Exception error)
            {
                if(muscle is not null)Save("failure-state.json",muscle.Capture());
                Save("failure.json",new{key,error=error.ToString(),delivered,samples});
                results.Add(new{key,completed=false,error=error.ToString(),delivered,samples});Console.WriteLine(error);
            }
            File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new{completed=results.Count==24,
                successfulCases=passed,cases=results,prescribedArrivals=true,neuronalSourceExecuted=false,
                nativeUnityExecuted=false,physiologyQualified=false,avatarStandingQualified=false},json));
        }
        if(passed!=24)throw new InvalidOperationException("Population trajectories failed; retain all traces and original limits.");
    }
}

#nullable enable
using System.Text.Json;
using System.Text.Json.Serialization;
using NeuralResonanceEngine.Protocol;
using NRE.SimAvatar;

// Fresh passive initialization only. No evolved tissue, spike generator, live
// neuron or body is accepted. Original source rows remain distinct motor units.
internal static class MusclePopulationInitialStudy
{
    internal sealed record UnitRow(int Row,int SourceRow,MotorUnitCalciumParameters Calcium,
        double LengthGainAboveOptimal,double LengthGainBelowOptimal);
    internal sealed record Case(string Key,string MuscleId,string Shape,DynamicMuscleGeometry Geometry,
        double MeasuredPathLengthMeters,double PhysicalTimeMs,double LowerFiberMeters,double UpperFiberMeters);
    internal sealed record Input(string Version,string ParameterSha256,UnitRow[] ParameterRows,Case[] Cases);
    internal static void Run(string inputFile,string output)
    {
        if(!Path.IsPathFullyQualified(inputFile)||!Path.IsPathFullyQualified(output)||Directory.Exists(output))
            throw new ArgumentException("Supply a preserved absolute input and a NEW absolute output directory.");
        var json=new JsonSerializerOptions(JsonSerializerDefaults.Web){WriteIndented=true};
        json.Converters.Add(new JsonStringEnumConverter());
        var input=JsonSerializer.Deserialize<Input>(File.ReadAllText(inputFile),json)??throw new ArgumentException("Missing population input.");
        if(input.Version!="named-ta-slow-population-initial-v1"||
            input.ParameterSha256!="01123aec76d1acc18b306ae1bd9daabfd29fe9287b54212aef5db3f15f719ebd"||
            input.ParameterRows.Length!=147||!input.ParameterRows.Select(r=>r.Row).SequenceEqual(Enumerable.Range(0,147))||
            !input.ParameterRows.Select(r=>r.SourceRow).SequenceEqual(Enumerable.Range(1,147))||
            input.ParameterRows[0].Calcium!=MotorUnitCalciumParameters.NagamoriSlowUnit1||input.Cases.Length!=4||
            input.Cases.Select(c=>c.Key).Distinct(StringComparer.Ordinal).Count()!=4)
            throw new ArgumentException("Require all original slow rows and four explicitly declared initial conditions.");
        Directory.CreateDirectory(output);
        File.Copy(inputFile,Path.Combine(output,"input.json"));
        var results=new List<object>();bool passed=true;
        foreach(var condition in input.Cases)
        {
            try
            {
            Console.WriteLine("FULL SLOW POPULATION INITIAL BALANCE "+condition.Key);
            var session=StableNeuralIdentity.Create("population-initial-session",condition.Key);
            var clock=new MotorAxonClock(session,0,condition.PhysicalTimeMs);
            var units=input.ParameterRows.Select(r=>new SlowMotorUnitDefinition(
                MotorAxonContact.Create("isolated-population-initial-"+condition.Key,"alpha-"+r.SourceRow.ToString("D3"),
                    StableNeuralIdentity.Create("population-initial-projection",condition.Key,r.SourceRow),
                    condition.MuscleId+"/unit-"+r.SourceRow.ToString("D3"),0),r.Calcium,
                r.LengthGainAboveOptimal,r.LengthGainBelowOptimal)).ToArray();
            var balance=AvatarMuscleInitialBalance.Solve(condition.MuscleId,condition.Geometry,units,clock,
                condition.MeasuredPathLengthMeters,condition.LowerFiberMeters,condition.UpperFiberMeters,
                MuscleInitialBalanceLimits.Diagnostic);
            var state=balance.Candidate;
            bool quiet=state.TimeMs==clock.BodyEpochMs&&state.FiberVelocityMetersPerSecond==0&&state.Yield==1&&
                state.Forces.ActiveFiberForceNewtons==0&&state.Units.Count==147&&state.Units.All(u=>
                    u.FreeCalcium==0&&u.BoundCalcium==0&&u.Activation==0&&u.ReleaseDrive==0&&u.Arrivals==0);
            bool intact=balance.Definition is not null&&balance.Definition.Units.SequenceEqual(units);
            bool good=balance.Balanced&&quiet&&intact;passed&=good;
            var row=new{condition.Key,condition.MuscleId,condition.Shape,condition.Geometry,Result=balance,
                DeclaredUnits=147,QuietOriginalState=quiet,OriginalDefinitionsIntact=intact,Passed=good};
            File.WriteAllText(Path.Combine(output,condition.Key+".json"),JsonSerializer.Serialize(row,json));results.Add(row);
            }
            catch(Exception error)
            {
                passed=false;var failed=new{condition.Key,Passed=false,Error=error.ToString()};
                File.WriteAllText(Path.Combine(output,condition.Key+"-failure.json"),JsonSerializer.Serialize(failed,json));
                results.Add(failed);Console.WriteLine(error);
            }
        }
        File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new{Completed=true,Passed=passed,Cases=results,
            NeuronalSourceExecuted=false,MuscleTrajectoryExecuted=false,NativeUnityExecuted=false,
            AnatomicalCalibrationQualified=false,AvatarStandingQualified=false},json));
        if(!passed)throw new InvalidOperationException("Some declared population initial conditions failed; retain original inputs and bounds.");
    }
}

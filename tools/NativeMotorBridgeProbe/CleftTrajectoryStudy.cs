#nullable enable
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using NRE.SimAvatar;

internal static class CleftTrajectoryStudy
{
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
    private static readonly JsonSerializerOptions FailureJson=new(JsonSerializerDefaults.Web) { NumberHandling=JsonNumberHandling.AllowNamedFloatingPointLiterals };
    internal sealed record Segment(long EndTick,double BundleDisplacementNanometers,AvatarCleftAxonCurrent[] AxonCurrents);
    internal sealed record Case(string Key,Guid CellId,CleftMembraneStudy.Case Fixture,Segment[] Segments,long TotalTicks);
    internal sealed record Settings(double VoltageAbsoluteToleranceMv,double IonAbsoluteToleranceMm,double GateAbsoluteTolerance,
        double RelativeTolerance,int MaximumNewtonIterations,int MaximumLineSearchHalvings);
    internal sealed record Protocol(string Version,string KernelVersion,Guid SessionId,int TicksPerMs,int SampleTicks,int[] StepTicks,
        int ExpectedCases,int ExpectedNativeRuns,int ExpectedReferences,Case[] Cases,Settings NonlinearSettings);

    internal static void Run(string input,string output)
    {
        if (!Path.IsPathFullyQualified(input) || !Path.IsPathFullyQualified(output) || Directory.Exists(output) || File.Exists(output))
            throw new ArgumentException("Absolute frozen protocol and new output required.");
        var p=JsonSerializer.Deserialize<Protocol>(File.ReadAllText(input),Json);
        bool clock=p is not null && ((p.Version=="joint-cleft-trajectory-comparison-v1" && p.TicksPerMs==2000 && p.SampleTicks==20) ||
            (p.Version=="joint-cleft-trajectory-refinement-v1" && p.TicksPerMs==4000 && p.SampleTicks==40));
        if (p is null || !clock || p.KernelVersion!=AvatarVestibularCleftDynamics.Version ||
            p.SessionId==Guid.Empty || !p.StepTicks.SequenceEqual(new[] { 4,2,1 }) ||
            p.ExpectedCases!=7 || p.ExpectedNativeRuns!=21 || p.ExpectedReferences!=21 || p.Cases.Length!=7)
            throw new ArgumentException("Unexpected complete-trajectory protocol.");
        Directory.CreateDirectory(output);WriteNew(Path.Combine(output,"input.json"),p);int completed=0;var failures=new List<string>();
        foreach (var c in p.Cases)
        {
            if (string.IsNullOrWhiteSpace(c.Key) || c.Key.Any(x=>!char.IsAsciiLetterOrDigit(x) && x!='-') || c.CellId==Guid.Empty ||
                c.Segments.Length!=4 || c.Segments[^1].EndTick!=c.TotalTicks || c.TotalTicks<=0 || c.TotalTicks%p.SampleTicks!=0)
                throw new ArgumentException("Invalid named trajectory case.");
            foreach (int ticks in p.StepTicks)
            {
                string name=c.Key+"-ticks"+ticks;Console.WriteLine("COUPLED SENSORY TRAJECTORY "+name);
                string folder=Path.Combine(output,name);Directory.CreateDirectory(folder);AvatarVestibularCleftDynamics? cell=null;
                try
                {
                    var f=c.Fixture;var t=f.Topology;var spatial=new AvatarCleftSpatialCoupling(t.HairCellId,t.HairApicalPf,t.HairPerilymphPf,t.Material,t.Compartments,t.Connections);
                    var kernel=new AvatarVestibularCleftMembranes(new(spatial,t.Reservoir,t.Faces),f.HairParameters,f.CalyxParameters);
                    var s=p.NonlinearSettings;double step=(double)ticks/p.TicksPerMs;
                    cell=new(p.SessionId,c.CellId,kernel,new(step,s.VoltageAbsoluteToleranceMv,s.IonAbsoluteToleranceMm,s.GateAbsoluteTolerance,
                        s.RelativeTolerance,s.MaximumNewtonIterations,s.MaximumLineSearchHalvings),f.HairMv,f.PerilymphMv,
                        f.States,f.Inputs.Select(x=>new AvatarCleftGateState(x.Id,x.HairGates,x.CalyxGates)).ToArray());
                    var initial=cell.Capture();WriteNew(Path.Combine(folder,"initial.json"),initial);
                    var forcing=c.Segments[0];var currents=forcing.AxonCurrents.ToDictionary(x=>x.Id,x=>x.OutgoingPa,StringComparer.Ordinal);
                    WriteNew(Path.Combine(folder,"initial-derivative.json"),kernel.Evaluate(f.HairMv,f.PerilymphMv,forcing.BundleDisplacementNanometers,
                        f.States,f.Inputs.Select(x=>x with { AxonOutgoingPa=currents[x.Id] }).ToArray()));
                    using var stream=new FileStream(Path.Combine(folder,"trajectory.csv"),FileMode.CreateNew,FileAccess.Write,FileShare.Read);
                    using var writer=new StreamWriter(stream,new UTF8Encoding(false)) { AutoFlush=true };
                    var header=new List<string> { "tick","step","timeMs" };header.AddRange(initial.StateLabels);
                    header.AddRange(initial.StateLabels.Select(x=>"stateError/"+x));header.AddRange(initial.LedgerLabels);
                    header.AddRange(initial.LedgerLabels.Select(x=>"ledgerError/"+x));
                    header.AddRange(new[] { "maximumChargeResidualFc","maximumIonResidualAttomoles","nonlinearEvaluations","maximumNewtonIterations","maximumAcceptedScaledResidual" });
                    writer.WriteLine(string.Join(',',header));WriteRow(writer,0,initial);long previous=0;
                    foreach (var segment in c.Segments)
                    {
                        if (segment.EndTick<=previous || segment.EndTick%ticks!=0) throw new ArgumentException("Input intervals must align with the declared clock.");
                        while (previous<segment.EndTick)
                        {
                            cell.AdvanceHeld(1,segment.BundleDisplacementNanometers,segment.AxonCurrents);previous+=ticks;
                            var state=cell.Capture();
                            if (state.CompletedSteps!=previous/ticks || state.TimeMs!=state.CompletedSteps*step || state.Failure is not null)
                                throw new InvalidOperationException("Trajectory clock/state mismatch.");
                            WriteRow(writer,previous,state);
                        }
                    }
                    WriteNew(Path.Combine(folder,"final.json"),cell.Capture());completed++;
                }
                catch (Exception error)
                {
                    failures.Add(name);WriteNew(Path.Combine(folder,"failure.json"),new { error=error.ToString(),state=cell?.Capture() },FailureJson);
                    Console.WriteLine("TRAJECTORY FAILED "+name+": "+error.Message);
                }
            }
        }
        WriteNew(Path.Combine(output,"results.json"),new { completed=completed==21,nativeRuns=completed,failedRuns=failures,
            independentCoupledTrajectoryQualified=false,stationaryInitializationQualified=false,physiologyQualified=false,avatarRecoveryAssessed=false });
        if (completed!=21) throw new InvalidOperationException("Some coupled trajectories failed; all available prefixes are preserved.");
    }

    private static void WriteRow(StreamWriter writer,long tick,AvatarCleftDynamicsState s)
    {
        var row=new List<string> { tick.ToString(CultureInfo.InvariantCulture),s.CompletedSteps.ToString(CultureInfo.InvariantCulture),Number(s.TimeMs) };
        row.AddRange(s.Values.Select(Number));row.AddRange(s.AdditionResiduals.Select(Number));row.AddRange(s.Ledgers.Select(Number));row.AddRange(s.LedgerAdditionResiduals.Select(Number));
        row.AddRange(new[] { Number(s.MaximumChargeResidualFc),Number(s.MaximumIonResidualAttomoles),s.NonlinearEvaluations.ToString(CultureInfo.InvariantCulture),
            s.MaximumNewtonIterations.ToString(CultureInfo.InvariantCulture),Number(s.MaximumAcceptedScaledResidual) });writer.WriteLine(string.Join(',',row));
    }
    private static string Number(double x)=>x.ToString("R",CultureInfo.InvariantCulture);
    private static void WriteNew(string path,object value,JsonSerializerOptions? options=null)
    {
        using var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None);JsonSerializer.Serialize(stream,value,options??Json);
    }
}

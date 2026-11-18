#nullable enable
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using NRE.SimAvatar;

internal static class AfferentLoadedRestStudy
{
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
    private static readonly JsonSerializerOptions FailureJson=new(JsonSerializerDefaults.Web) { NumberHandling=JsonNumberHandling.AllowNamedFloatingPointLiterals };
    internal sealed record Segment(long EndTick,double BundleDisplacementNanometers);
    internal sealed record Case(string Key,Guid CellId,AfferentNumericalStudy.JointCase Fixture,Segment[] Segments,long TotalTicks);
    internal sealed record Settings(double VoltageAbsoluteToleranceMv,double IonAbsoluteToleranceMm,double GateAbsoluteTolerance,
        double RelativeTolerance,int MaximumNewtonIterations,int MaximumLineSearchHalvings);
    internal sealed record Protocol(string Version,string KernelVersion,Guid SessionId,int TicksPerMs,int SampleTicks,int[] StepTicks,
        int ExpectedCases,int ExpectedNativeRuns,int ExpectedReferences,Case[] Cases,Settings NonlinearSettings,bool RestInitialized);

    internal static void Run(string input,string output)
        =>Run(input,output,AvatarSodiumActivationContinuation.EndpointHold);
    internal static void Run(string input,string output,AvatarSodiumActivationContinuation continuation)
    {
        if(continuation!=AvatarSodiumActivationContinuation.EndpointHold)throw new ArgumentOutOfRangeException(nameof(continuation));
        if (!Path.IsPathFullyQualified(input) || !Path.IsPathFullyQualified(output) || Directory.Exists(output) || File.Exists(output))
            throw new ArgumentException("Absolute frozen protocol and new output required.");
        var p=JsonSerializer.Deserialize<Protocol>(File.ReadAllText(input),Json);
        bool clock=p is not null && p.Version=="joint-sensory-nerve-loaded-rest-v1" && p.TicksPerMs==262144 && p.SampleTicks==256 && p.RestInitialized;
        if (p is null || !clock || p.KernelVersion!=AvatarVestibularAfferentDynamics.Version ||
            p.SessionId==Guid.Empty || !p.StepTicks.SequenceEqual(new[] { 256,128,64 }) ||
            p.ExpectedCases!=4 || p.ExpectedNativeRuns!=12 || p.ExpectedReferences!=12 || p.Cases.Length!=4 || p.Cases.Select(c=>c.Key).Distinct(StringComparer.Ordinal).Count()!=4)
            throw new ArgumentException("Unexpected complete-trajectory protocol.");
        Directory.CreateDirectory(output);WriteNew(Path.Combine(output,"input.json"),p);int completed=0;var failures=new List<string>();
        if(continuation!=AvatarSodiumActivationContinuation.DepositedFixedValue)
            WriteNew(Path.Combine(output,"activation-continuation.json"),new { version=AvatarAfferentMembraneKernel.EndpointHoldVersion,
                policy=continuation.ToString(),originalDefaultChanged=false,physiologicalCalibrationQualified=false });
        foreach (var c in p.Cases)
        {
            if (string.IsNullOrWhiteSpace(c.Key) || c.Key.Any(x=>!char.IsAsciiLetterOrDigit(x) && x!='-') || c.CellId==Guid.Empty ||
                c.Segments.Length!=4 || c.Segments[^1].EndTick!=c.TotalTicks || c.TotalTicks!=1048576 || c.TotalTicks%p.SampleTicks!=0)
                throw new ArgumentException("Invalid named trajectory case.");
            foreach (int ticks in p.StepTicks)
            {
                string name=c.Key+"-ticks"+ticks;Console.WriteLine("JOINT SENSORY NERVE TRAJECTORY "+name);
                string folder=Path.Combine(output,name);Directory.CreateDirectory(folder);AvatarVestibularAfferentDynamics? cell=null;
                try
                {
                    var f=c.Fixture.Sensory;var t=f.Topology;
                    if(f.Inputs.Any(x=>x.AxonOutgoingPa!=0))throw new ArgumentException("Actual nerve owns calyx loading; no prescribed terminal current.");
                    var spatial=new AvatarCleftSpatialCoupling(t.HairCellId,t.HairApicalPf,t.HairPerilymphPf,t.Material,t.Compartments,t.Connections);
                    var sensory=new AvatarVestibularCleftMembranes(new(spatial,t.Reservoir,t.Faces),f.HairParameters,f.CalyxParameters);
                    var cable=new AvatarAfferentCable(c.Fixture.CableTopology.Nodes,c.Fixture.CableTopology.Edges,c.Fixture.CableTopology.Terminals,continuation);
                    var kernel=new AvatarVestibularCleftAfferent(sensory,cable,c.Fixture.Attachments);
                    var s=p.NonlinearSettings;double step=(double)ticks/p.TicksPerMs;
                    cell=new(p.SessionId,c.CellId,kernel,new(step,s.VoltageAbsoluteToleranceMv,s.IonAbsoluteToleranceMm,s.GateAbsoluteTolerance,
                        s.RelativeTolerance,s.MaximumNewtonIterations,s.MaximumLineSearchHalvings),f.HairMv,f.PerilymphMv,
                        f.States,f.Inputs.Select(x=>new AvatarCleftGateState(x.Id,x.HairGates,x.CalyxGates)).ToArray(),c.Fixture.CableStates);
                    var initial=cell.Capture();WriteNew(Path.Combine(folder,"initial.json"),initial);
                    var forcing=c.Segments[0];
                    WriteNew(Path.Combine(folder,"initial-derivative.json"),kernel.Evaluate(f.HairMv,f.PerilymphMv,forcing.BundleDisplacementNanometers,
                        f.States,f.Inputs.Select(x=>new AvatarCleftGateState(x.Id,x.HairGates,x.CalyxGates)).ToArray(),c.Fixture.CableStates));
                    using var stream=new FileStream(Path.Combine(folder,"trajectory.csv"),FileMode.CreateNew,FileAccess.Write,FileShare.Read);
                    using var writer=new StreamWriter(stream,new UTF8Encoding(false)) { AutoFlush=true };
                    var header=new List<string> { "tick","step","timeMs" };header.AddRange(initial.StateLabels);
                    header.AddRange(initial.StateLabels.Select(x=>"stateError/"+x));header.AddRange(initial.LedgerLabels);
                    header.AddRange(initial.LedgerLabels.Select(x=>"ledgerError/"+x));
                    header.AddRange(new[] { "maximumChargeResidualFc","maximumIonResidualAttomoles","maximumCableChargeResidualFc","maximumInterfaceChargeResidualFc","nonlinearEvaluations","maximumNewtonIterations","maximumAcceptedScaledResidual" });
                    writer.WriteLine(string.Join(',',header));WriteRow(writer,0,initial);long previous=0;
                    foreach (var segment in c.Segments)
                    {
                        if (segment.EndTick<=previous || segment.EndTick%ticks!=0) throw new ArgumentException("Input intervals must align with the declared clock.");
                        while (previous<segment.EndTick)
                        {
                            cell.AdvanceHeld(1,segment.BundleDisplacementNanometers);previous+=ticks;
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
        WriteNew(Path.Combine(output,"results.json"),new { completed=completed==12,nativeRuns=completed,failedRuns=failures,
            restInitialized=true,independentCoupledTrajectoryQualified=false,stationaryInitializationQualified=false,physiologyQualified=false,avatarRecoveryAssessed=false });
        if (completed!=12) throw new InvalidOperationException("Some coupled trajectories failed; all available prefixes are preserved.");
    }

    private static void WriteRow(StreamWriter writer,long tick,AvatarAfferentDynamicsState s)
    {
        var row=new List<string> { tick.ToString(CultureInfo.InvariantCulture),s.CompletedSteps.ToString(CultureInfo.InvariantCulture),Number(s.TimeMs) };
        row.AddRange(s.Values.Select(Number));row.AddRange(s.AdditionResiduals.Select(Number));row.AddRange(s.Ledgers.Select(Number));row.AddRange(s.LedgerAdditionResiduals.Select(Number));
        row.AddRange(new[] { Number(s.MaximumChargeResidualFc),Number(s.MaximumIonResidualAttomoles),Number(s.MaximumCableChargeResidualFc),Number(s.MaximumInterfaceChargeResidualFc),s.NonlinearEvaluations.ToString(CultureInfo.InvariantCulture),
            s.MaximumNewtonIterations.ToString(CultureInfo.InvariantCulture),Number(s.MaximumAcceptedScaledResidual) });writer.WriteLine(string.Join(',',row));
    }
    private static string Number(double x)=>x.ToString("R",CultureInfo.InvariantCulture);
    private static void WriteNew(string path,object value,JsonSerializerOptions? options=null)
    {
        using var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None);JsonSerializer.Serialize(stream,value,options??Json);
    }
}

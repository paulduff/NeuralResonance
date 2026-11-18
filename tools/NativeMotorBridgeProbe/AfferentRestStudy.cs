using System.Text.Json;
using System.Text.Json.Serialization;
using NRE.SimAvatar;

internal static class AfferentRestStudy
{
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web)
    { NumberHandling=JsonNumberHandling.AllowNamedFloatingPointLiterals };
    internal sealed record Case(string Key,string GeometryKey,string Seed,AfferentNumericalStudy.JointCase Fixture);
    internal sealed record Protocol(string Version,string KernelVersion,int ExpectedCases,Case[] Cases,AvatarCleftStationarySettings NativeSettings);

    internal static void Run(string input,string output)
    {
        if(!Path.IsPathFullyQualified(input)||!Path.IsPathFullyQualified(output)||Directory.Exists(output)||File.Exists(output))
            throw new ArgumentException("Require frozen absolute input and NEW connected stationary output.");
        var p=JsonSerializer.Deserialize<Protocol>(File.ReadAllText(input),Json);
        if(p is null||p.Version!="joint-sensory-nerve-stationary-comparison-v1"||p.KernelVersion!=AvatarAfferentStationarySearch.Version||
            p.ExpectedCases!=18||p.Cases.Length!=18||p.Cases.Select(c=>c.Key).Distinct(StringComparer.Ordinal).Count()!=18)
            throw new ArgumentException("Unexpected connected stationary protocol.");
        p.NativeSettings.Validate();Directory.CreateDirectory(output);Write(Path.Combine(output,"input.json"),p);
        var rows=new List<object>();var failures=new List<string>();int roots=0;
        foreach(var c in p.Cases)
        {
            if(string.IsNullOrWhiteSpace(c.Key)||c.Key.Any(x=>!char.IsAsciiLetterOrDigit(x)&&x!='-'))throw new ArgumentException("Invalid case key.");
            Console.WriteLine("CONNECTED SENSORY NERVE STATIONARY SEARCH "+c.Key);
            string folder=Path.Combine(output,c.Key);Directory.CreateDirectory(folder);
            try
            {
                var f=c.Fixture;var s=f.Sensory;var t=s.Topology;
                if(s.BundleDisplacementNanometers!=0||s.Inputs.Any(i=>i.AxonOutgoingPa!=0))
                    throw new ArgumentException("Quiet bundle and actual reciprocal axon current required.");
                var spatial=new AvatarCleftSpatialCoupling(t.HairCellId,t.HairApicalPf,t.HairPerilymphPf,t.Material,t.Compartments,t.Connections);
                var sensory=new AvatarVestibularCleftMembranes(new(spatial,t.Reservoir,t.Faces),s.HairParameters,s.CalyxParameters);
                var cable=new AvatarAfferentCable(f.CableTopology.Nodes,f.CableTopology.Edges,f.CableTopology.Terminals);
                var kernel=new AvatarVestibularCleftAfferent(sensory,cable,f.Attachments);
                var result=new AvatarAfferentStationarySearch(kernel,p.NativeSettings).Find(s.HairMv,s.PerilymphMv,s.States,
                    f.CableStates.Select(n=>new AvatarAfferentStationaryGuess(n.Id,n.PotentialMv)).ToArray(),s.BundleDisplacementNanometers);
                Write(Path.Combine(folder,"result.json"),result);if(result.StationaryCriteriaPassed)roots++;
                rows.Add(new { key=c.Key,completed=true,stationaryCriteriaPassed=result.StationaryCriteriaPassed,result.Outcome,result.Evaluations });
                Console.WriteLine(c.Key+": "+result.Outcome);
            }
            catch(Exception error)
            {
                failures.Add(c.Key);Write(Path.Combine(folder,"failure.json"),new { error=error.ToString() });
                rows.Add(new { key=c.Key,completed=false,stationaryCriteriaPassed=false });
            }
        }
        Write(Path.Combine(output,"results.json"),new { completed=failures.Count==0,cases=rows,attempted=18,rootsFound=roots,
            executionFailures=failures,timeIntegrated=false,localStabilityAssessed=false,physiologicalRestQualified=false,
            afferentFiringQualified=false,avatarRecoveryAssessed=false });
        if(failures.Count!=0)throw new InvalidOperationException("Some connected stationary captures failed; all available results retained.");
    }
    private static void Write(string path,object value)
    {
        using var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None);JsonSerializer.Serialize(stream,value,Json);
    }
}

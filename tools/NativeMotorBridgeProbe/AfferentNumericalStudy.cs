using System.Text;
using System.Text.Json;
using NRE.SimAvatar;

internal static class AfferentNumericalStudy
{
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
    internal sealed record MembraneCase(string Key,AvatarAfferentMembraneParameters Parameters,double VoltageMv,AvatarAfferentGates Gates);
    internal sealed record ClampCase(string Key,double VoltageMv,double ElapsedMs,AvatarAfferentGates Gates);
    internal sealed record CableTopology(AvatarAfferentCableNode[] Nodes,AvatarAfferentCableEdge[] Edges,AvatarAfferentCableTerminal[] Terminals);
    internal sealed record CableCase(string Key,CableTopology Topology,double PerilymphMv,AvatarAfferentCableState[] States,AvatarAfferentTerminalState[] TerminalStates);
    internal sealed record JointCase(string Key,CleftMembraneStudy.Case Sensory,CableTopology CableTopology,AvatarCleftAfferentAttachment[] Attachments,AvatarAfferentCableState[] CableStates);
    internal sealed record Protocol(string Version,string MembraneVersion,string CableVersion,string JointVersion,Dictionary<string,int> ExpectedCounts,
        MembraneCase[] Membranes,ClampCase[] Clamps,CableCase[] Cables,JointCase[] Joints);
    internal static void Run(string input,string output)
    {
        if(!Path.IsPathFullyQualified(input)||!Path.IsPathFullyQualified(output)||Directory.Exists(output)||File.Exists(output))throw new ArgumentException("Require frozen input and NEW native output.");
        var p=JsonSerializer.Deserialize<Protocol>(File.ReadAllText(input),Json);
        if(p is null||p.Version!="sensory-afferent-derivative-comparison-v1"||p.MembraneVersion!=AvatarAfferentMembraneKernel.Version||
            p.CableVersion!=AvatarAfferentCable.Version||p.JointVersion!=AvatarVestibularCleftAfferent.Version)throw new ArgumentException("Unexpected neuronal numerical protocol.");
        if(p.ExpectedCounts.Count!=4||p.ExpectedCounts["membranes"]!=p.Membranes.Length||p.ExpectedCounts["clamps"]!=p.Clamps.Length||
            p.ExpectedCounts["cables"]!=p.Cables.Length||p.ExpectedCounts["joints"]!=p.Joints.Length||p.ExpectedCounts.Values.Any(n=>n<=0))throw new ArgumentException("Incomplete case inventory.");
        foreach(var keys in new[] { p.Membranes.Select(c=>c.Key),p.Clamps.Select(c=>c.Key),p.Cables.Select(c=>c.Key),p.Joints.Select(c=>c.Key) })
            if(keys.Any(string.IsNullOrWhiteSpace)||keys.Distinct(StringComparer.Ordinal).Count()!=keys.Count())throw new ArgumentException("Unique case identities required.");
        Directory.CreateDirectory(output);Write(Path.Combine(output,"input.json"),p);
        Write(Path.Combine(output,"timing-tables.json"),new { AvatarAfferentMembraneKernel.SodiumActivationTable,AvatarAfferentMembraneKernel.SodiumInactivationTable });
        Capture("membranes",p.Membranes,c=>
        {
            var k=new AvatarAfferentMembraneKernel(c.Parameters);return new { input=c,k.Fingerprint,evaluation=k.Evaluate(c.VoltageMv,c.Gates) };
        });
        Capture("clamps",p.Clamps,c=>new { input=c,evaluation=AvatarAfferentMembraneKernel.RelaxVoltageClamp(c.Gates,c.VoltageMv,c.ElapsedMs) });
        Capture("cables",p.Cables,c=>
        {
            var k=new AvatarAfferentCable(c.Topology.Nodes,c.Topology.Edges,c.Topology.Terminals);
            return new { input=c,k.Fingerprint,evaluation=k.Evaluate(c.PerilymphMv,c.States,c.TerminalStates) };
        });
        Capture("joints",p.Joints,c=>
        {
            var f=c.Sensory;var t=f.Topology;if(f.Inputs.Any(i=>i.AxonOutgoingPa!=0))throw new ArgumentException("Joint current is owned by the actual cable, not a fixture override.");
            var spatial=new AvatarCleftSpatialCoupling(t.HairCellId,t.HairApicalPf,t.HairPerilymphPf,t.Material,t.Compartments,t.Connections);
            var sensory=new AvatarVestibularCleftMembranes(new(spatial,t.Reservoir,t.Faces),f.HairParameters,f.CalyxParameters);
            var cable=new AvatarAfferentCable(c.CableTopology.Nodes,c.CableTopology.Edges,c.CableTopology.Terminals);
            var k=new AvatarVestibularCleftAfferent(sensory,cable,c.Attachments);
            return new { input=c,k.Fingerprint,evaluation=k.Evaluate(f.HairMv,f.PerilymphMv,f.BundleDisplacementNanometers,f.States,
                f.Inputs.Select(i=>new AvatarCleftGateState(i.Id,i.HairGates,i.CalyxGates)).ToArray(),c.CableStates) };
        });
        Write(Path.Combine(output,"results.json"),new { completed=true,counts=p.ExpectedCounts,timeIntegrated=false,afferentFiringQualified=false,physiologyQualified=false,avatarRecoveryAssessed=false });
        void Capture<T>(string name,T[] cases,Func<T,object> evaluate)
        {
            using var stream=new FileStream(Path.Combine(output,name+".jsonl"),FileMode.CreateNew,FileAccess.Write,FileShare.Read);
            using var writer=new StreamWriter(stream,new UTF8Encoding(false)) { AutoFlush=true };
            Console.WriteLine("NATIVE SENSORY NERVE "+name+" "+cases.Length);
            foreach(var c in cases)writer.WriteLine(JsonSerializer.Serialize(evaluate(c),Json));
        }
    }
    private static void Write(string path,object value)
    {
        using var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None);JsonSerializer.Serialize(stream,value,Json);
    }
}

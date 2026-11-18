namespace NRE.SimAvatar;

public sealed record AvatarAfferentCableNode(string Id,double LengthMicrometers,double RadiusMicrometers,AvatarAfferentMembraneParameters Membrane);
public sealed record AvatarAfferentCableEdge(string Id,string LeftId,string RightId,double CrossSectionSquareMicrometers,
    double CenterDistanceMicrometers,double ConductivityNsPerMicrometer);
// A zero conductance explicitly seals a terminal. Every terminal still requires
// its own input record; omitted inputs are not interpreted as zero or silence.
public sealed record AvatarAfferentCableTerminal(string Id,string NodeId,double ConductanceNs);
public readonly record struct AvatarAfferentCableState(string Id,double PotentialMv,AvatarAfferentGates Gates);
public readonly record struct AvatarAfferentTerminalState(string Id,double PotentialMv);
public readonly record struct AvatarAfferentCableNodeEvaluation(string Id,double AreaSquareMicrometers,double CapacitancePf,
    AvatarAfferentMembraneEvaluation Membrane,double MembraneOutwardPa,double NetAxialOutwardPa,double ChargeRatePa,double PotentialMvPerMs);
public readonly record struct AvatarAfferentCableEdgeEvaluation(string Id,string LeftId,string RightId,double ConductanceNs,double LeftToRightPa,double DissipationFw);
public readonly record struct AvatarAfferentCableTerminalEvaluation(string Id,string NodeId,double NodeToTerminalPa,double DissipationFw);
public sealed record AvatarAfferentCableEvaluation(AvatarAfferentCableNodeEvaluation[] Nodes,AvatarAfferentCableEdgeEvaluation[] Edges,
    AvatarAfferentCableTerminalEvaluation[] Terminals,double TotalMembraneOutwardPa,double TotalTerminalOutwardPa,double TotalChargeRatePa,
    double ChargeResidualPa,double StoredEnergyAttojoules,double StoredEnergyRateFw,double MembranePowerFw,double TerminalPowerOutwardFw,
    double AxialDissipationFw,double PowerResidualFw);

public sealed class AvatarAfferentCable
{
    public const string Version="vestibular-identified-finite-volume-axon-v1";
    private readonly AvatarAfferentCableNode[] _nodes;
    private readonly AvatarAfferentCableEdge[] _edges;
    private readonly AvatarAfferentCableTerminal[] _terminals;
    private readonly AvatarAfferentMembraneKernel[] _kernels;
    private readonly Dictionary<string,int> _indices;
    private readonly double[] _areas,_capacitances,_conductances;
    public IReadOnlyList<AvatarAfferentCableNode> Nodes { get; }
    public IReadOnlyList<AvatarAfferentCableEdge> Edges { get; }
    public IReadOnlyList<AvatarAfferentCableTerminal> Terminals { get; }
    public string Fingerprint { get; }
    public AvatarAfferentCable(IEnumerable<AvatarAfferentCableNode> nodes,IEnumerable<AvatarAfferentCableEdge> edges,IEnumerable<AvatarAfferentCableTerminal> terminals)
    {
        ArgumentNullException.ThrowIfNull(nodes);ArgumentNullException.ThrowIfNull(edges);ArgumentNullException.ThrowIfNull(terminals);
        _nodes=nodes.OrderBy(x=>x.Id,StringComparer.Ordinal).ToArray();_edges=edges.OrderBy(x=>x.Id,StringComparer.Ordinal).ToArray();_terminals=terminals.OrderBy(x=>x.Id,StringComparer.Ordinal).ToArray();
        if(_nodes.Length is <1 or >4096||_terminals.Length==0)throw new ArgumentException("Explicit finite cable and terminals required.");
        static void Ids(IEnumerable<string> ids) { var a=ids.ToArray();if(a.Any(string.IsNullOrWhiteSpace)||a.Distinct(StringComparer.Ordinal).Count()!=a.Length)throw new ArgumentException("Unique nonempty identities required."); }
        Ids(_nodes.Select(n=>n.Id));Ids(_edges.Select(e=>e.Id));Ids(_terminals.Select(t=>t.Id));
        _indices=_nodes.Select((n,i)=>(n.Id,i)).ToDictionary(x=>x.Id,x=>x.i,StringComparer.Ordinal);
        _kernels=new AvatarAfferentMembraneKernel[_nodes.Length];_areas=new double[_nodes.Length];_capacitances=new double[_nodes.Length];_conductances=new double[_edges.Length];
        for(int i=0;i<_nodes.Length;i++)
        {
            var n=_nodes[i];CleftNumbers.Positive(n.LengthMicrometers);CleftNumbers.Positive(n.RadiusMicrometers);_kernels[i]=new(n.Membrane);
            _areas[i]=2*Math.PI*n.RadiusMicrometers*n.LengthMicrometers;_capacitances[i]=_areas[i]*n.Membrane.SpecificCapacitancePfPerSquareMicrometer;
            CleftNumbers.Positive(_areas[i]);CleftNumbers.Positive(_capacitances[i]);
        }
        for(int i=0;i<_edges.Length;i++)
        {
            var e=_edges[i];if(e.LeftId==e.RightId||!_indices.ContainsKey(e.LeftId)||!_indices.ContainsKey(e.RightId))throw new ArgumentException("Unknown or self cable edge.");
            CleftNumbers.Positive(e.CrossSectionSquareMicrometers);CleftNumbers.Positive(e.CenterDistanceMicrometers);CleftNumbers.Positive(e.ConductivityNsPerMicrometer);
            _conductances[i]=e.ConductivityNsPerMicrometer*e.CrossSectionSquareMicrometers/e.CenterDistanceMicrometers;CleftNumbers.Positive(_conductances[i]);
        }
        foreach(var t in _terminals) { if(!_indices.ContainsKey(t.NodeId))throw new ArgumentException("Unknown terminal node.");CleftNumbers.Nonnegative(t.ConductanceNs); }
        var reached=new HashSet<string>(StringComparer.Ordinal) { _nodes[0].Id };bool changed;
        do { changed=false;foreach(var e in _edges) { if(reached.Contains(e.LeftId))changed|=reached.Add(e.RightId);if(reached.Contains(e.RightId))changed|=reached.Add(e.LeftId); } }while(changed);
        if(reached.Count!=_nodes.Length)throw new ArgumentException("Cable contains disconnected nodes.");
        Nodes=Array.AsReadOnly(_nodes);Edges=Array.AsReadOnly(_edges);Terminals=Array.AsReadOnly(_terminals);
        Fingerprint=CleftNumbers.Fingerprint(new { Version,Nodes,Edges,Terminals,MembraneVersions=_kernels.Select(k=>k.Fingerprint).ToArray(),
            Sign="outward membrane/terminal current removes intracellular charge",Geometry="cylindrical membrane 2*pi*r*L; axial sigma*area/distance",
            Bath="single explicitly held perilymph potential",TimeIntegrated=false });
    }
    public AvatarAfferentCableEvaluation Evaluate(double perilymphMv,IReadOnlyList<AvatarAfferentCableState> states,IReadOnlyList<AvatarAfferentTerminalState> terminalStates)
    {
        CleftNumbers.Finite(perilymphMv);ArgumentNullException.ThrowIfNull(states);ArgumentNullException.ThrowIfNull(terminalStates);
        if(states.Count!=_nodes.Length||terminalStates.Count!=_terminals.Length)throw new ArgumentException("Complete identified cable state and terminal inputs required.");
        var named=states.ToDictionary(s=>s.Id,StringComparer.Ordinal);var boundary=terminalStates.ToDictionary(t=>t.Id,StringComparer.Ordinal);
        var v=new double[_nodes.Length];var outgoing=new double[v.Length];var membrane=new AvatarAfferentMembraneEvaluation[v.Length];
        for(int i=0;i<v.Length;i++)
        {
            if(!named.TryGetValue(_nodes[i].Id,out var state))throw new ArgumentException("Unknown or missing cable state.");
            CleftNumbers.Finite(state.PotentialMv);v[i]=state.PotentialMv;membrane[i]=_kernels[i].Evaluate(v[i]-perilymphMv,state.Gates);
        }
        var edgeResults=new AvatarAfferentCableEdgeEvaluation[_edges.Length];double dissipation=0;
        for(int i=0;i<_edges.Length;i++)
        {
            var e=_edges[i];int a=_indices[e.LeftId],b=_indices[e.RightId];double difference=v[a]-v[b],flow=_conductances[i]*difference,power=flow*difference;
            CleftNumbers.Finite(flow);CleftNumbers.Nonnegative(power);outgoing[a]+=flow;outgoing[b]-=flow;dissipation+=power;
            edgeResults[i]=new(e.Id,e.LeftId,e.RightId,_conductances[i],flow,power);
        }
        var terminalResults=new AvatarAfferentCableTerminalEvaluation[_terminals.Length];double totalTerminal=0,terminalPower=0;
        for(int i=0;i<_terminals.Length;i++)
        {
            var t=_terminals[i];if(!boundary.TryGetValue(t.Id,out var state))throw new ArgumentException("Unknown or missing terminal state.");
            CleftNumbers.Finite(state.PotentialMv);int a=_indices[t.NodeId];double difference=v[a]-state.PotentialMv,flow=t.ConductanceNs*difference,power=flow*difference;
            CleftNumbers.Finite(flow);CleftNumbers.Nonnegative(power);outgoing[a]+=flow;totalTerminal+=flow;dissipation+=power;terminalPower+=flow*(state.PotentialMv-perilymphMv);
            terminalResults[i]=new(t.Id,t.NodeId,flow,power);
        }
        var results=new AvatarAfferentCableNodeEvaluation[v.Length];double totalMembrane=0,chargeRate=0,energy=0,energyRate=0,membranePower=0;
        for(int i=0;i<v.Length;i++)
        {
            double current=_areas[i]*membrane[i].Currents.TotalOutwardPaPerSquareMicrometer,dq=-current-outgoing[i],dv=dq/_capacitances[i],vm=v[i]-perilymphMv;
            foreach(double x in new[] { current,dq,dv })CleftNumbers.Finite(x);
            totalMembrane+=current;chargeRate+=dq;energy+=.5*_capacitances[i]*vm*vm;energyRate+=vm*dq;membranePower+=vm*current;
            results[i]=new(_nodes[i].Id,_areas[i],_capacitances[i],membrane[i],current,outgoing[i],dq,dv);
        }
        double chargeResidual=chargeRate+totalMembrane+totalTerminal,powerResidual=energyRate+membranePower+terminalPower+dissipation;
        foreach(double x in new[] { totalMembrane,chargeRate,totalTerminal,energy,energyRate,membranePower,terminalPower,dissipation,chargeResidual,powerResidual })CleftNumbers.Finite(x);
        return new(results,edgeResults,terminalResults,totalMembrane,totalTerminal,chargeRate,chargeResidual,energy,energyRate,membranePower,terminalPower,dissipation,powerResidual);
    }
    // A spatial mesh hypothesis, not fitted anatomy or a spatial convergence
    // claim. Every compartment boundary stays at its declared source location.
    public static AvatarAfferentCable ReferenceChain(int cellsPerRegion)
    {
        if(cellsPerRegion is <1 or >512)throw new ArgumentOutOfRangeException(nameof(cellsPerRegion));
        double[] boundaries=[0,8,9,80,82,382,384,425];string[] names=["unmyelinated","heminode","internode1","node1","internode2","node2","distal"];
        AvatarAfferentMembraneParameters[] parameters=[AvatarAfferentMembraneParameters.UnmyelinatedReference,AvatarAfferentMembraneParameters.NodeReference,
            AvatarAfferentMembraneParameters.MyelinatedReference,AvatarAfferentMembraneParameters.NodeReference,AvatarAfferentMembraneParameters.MyelinatedReference,
            AvatarAfferentMembraneParameters.NodeReference,AvatarAfferentMembraneParameters.MyelinatedReference];
        var nodes=new List<AvatarAfferentCableNode>();var edges=new List<AvatarAfferentCableEdge>();const double radius=1.5,conductivity=1000;
        for(int region=0;region<7;region++)for(int j=0;j<cellsPerRegion;j++)
        {
            var next=new AvatarAfferentCableNode(names[region]+"-"+j.ToString("D3",System.Globalization.CultureInfo.InvariantCulture),
                (boundaries[region+1]-boundaries[region])/cellsPerRegion,radius,parameters[region]);
            if(nodes.Count>0) { var previous=nodes[^1];edges.Add(new("edge-"+nodes.Count.ToString("D4",System.Globalization.CultureInfo.InvariantCulture),previous.Id,next.Id,
                Math.PI*radius*radius,(previous.LengthMicrometers+next.LengthMicrometers)/2,conductivity)); }
            nodes.Add(next);
        }
        return new(nodes,edges,[new("calyx",nodes[0].Id,conductivity*Math.PI*radius*radius/(nodes[0].LengthMicrometers/2)),new("distal-sealed",nodes[^1].Id,0)]);
    }
}

namespace NRE.SimAvatar;

public sealed record AvatarCleftAfferentAttachment(string TerminalId,string CleftCompartmentId);
public readonly record struct AvatarCleftAfferentTransfer(string TerminalId,string CleftCompartmentId,double CalyxToCablePa,double CableToCalyxPa);
public sealed record AvatarVestibularCleftAfferentEvaluation(AvatarVestibularCleftEvaluation Sensory,AvatarAfferentCableEvaluation Cable,
    AvatarCleftAfferentTransfer[] Transfers,double InterfaceChargeResidualPa,double InterfacePowerResidualFw);

// One SAME-STATE derivative owner. Cable boundary currents load the calyx that
// generated them, with the opposite sign. No delayed command or scripted spike
// substitutes for a coupled neuronal trajectory. Time integration is separate.
public sealed class AvatarVestibularCleftAfferent
{
    public const string Version="joint-cleft-afferent-current-owner-v1";
    public AvatarVestibularCleftMembranes Sensory { get; }
    public AvatarAfferentCable Cable { get; }
    public IReadOnlyList<AvatarCleftAfferentAttachment> Attachments { get; }
    public string Fingerprint { get; }
    private readonly Dictionary<string,string> _targets;
    public AvatarVestibularCleftAfferent(AvatarVestibularCleftMembranes sensory,AvatarAfferentCable cable,IEnumerable<AvatarCleftAfferentAttachment> attachments)
    {
        ArgumentNullException.ThrowIfNull(sensory);ArgumentNullException.ThrowIfNull(cable);ArgumentNullException.ThrowIfNull(attachments);
        Sensory=sensory;Cable=cable;var a=attachments.OrderBy(x=>x.TerminalId,StringComparer.Ordinal).ToArray();
        if(a.Length==0||a.Select(x=>x.TerminalId).Distinct(StringComparer.Ordinal).Count()!=a.Length)throw new ArgumentException("Explicit unique calyx attachments required.");
        var terminals=cable.Terminals.ToDictionary(t=>t.Id,StringComparer.Ordinal);
        var cleft=sensory.Coupling.Spatial.Compartments.Select(n=>n.Id).ToHashSet(StringComparer.Ordinal);
        foreach(var link in a)if(!terminals.ContainsKey(link.TerminalId)||!cleft.Contains(link.CleftCompartmentId))throw new ArgumentException("Unknown cable terminal or calyx patch.");
        _targets=a.ToDictionary(x=>x.TerminalId,x=>x.CleftCompartmentId,StringComparer.Ordinal);
        if(cable.Terminals.Any(t=>t.ConductanceNs!=0&&!_targets.ContainsKey(t.Id)))throw new ArgumentException("An unconnected terminal must be explicitly sealed.");
        foreach(var n in cable.Nodes)
        {
            var p=n.Membrane;
            if(p.OutsidePotassiumMm!=5||p.OutsideSodiumMm!=140||p.InsidePotassiumMm!=150||p.InsideSodiumMm!=12||p.ThermalVoltageMv!=26)
                throw new ArgumentException("Joint reduction requires matching declared chemical reservoirs.");
        }
        Attachments=Array.AsReadOnly(a);Fingerprint=CleftNumbers.Fingerprint(new { Version,Sensory=sensory.Fingerprint,Cable=cable.Fingerprint,
            Attachments,Interpretation="same-state conservative electrical coupling; held ionic reservoirs; no time integration or firing qualification" });
    }
    public AvatarVestibularCleftAfferentEvaluation Evaluate(double hairMv,double perilymphMv,double bundleDisplacementNanometers,
        IReadOnlyList<AvatarCleftCompartmentState> cleftStates,IReadOnlyList<AvatarCleftGateState> cleftGates,IReadOnlyList<AvatarAfferentCableState> cableStates)
    {
        ArgumentNullException.ThrowIfNull(cleftStates);ArgumentNullException.ThrowIfNull(cleftGates);ArgumentNullException.ThrowIfNull(cableStates);
        if(cleftStates.Count!=Sensory.Coupling.Spatial.Compartments.Count||cleftGates.Count!=cleftStates.Count)throw new ArgumentException("Complete named sensory state required.");
        var states=cleftStates.ToDictionary(s=>s.Id,StringComparer.Ordinal);var gates=cleftGates.ToDictionary(g=>g.Id,StringComparer.Ordinal);
        foreach(var n in Sensory.Coupling.Spatial.Compartments)if(!states.ContainsKey(n.Id)||!gates.ContainsKey(n.Id))throw new ArgumentException("Unknown or missing sensory identity.");
        var boundary=Cable.Terminals.Select(t=>new AvatarAfferentTerminalState(t.Id,_targets.TryGetValue(t.Id,out var id)?states[id].CalyxMv:perilymphMv)).ToArray();
        var cable=Cable.Evaluate(perilymphMv,cableStates,boundary);
        var currents=states.Keys.ToDictionary(id=>id,_=>0.0,StringComparer.Ordinal);var transfers=new List<AvatarCleftAfferentTransfer>();
        double interfaceCharge=0,calyxPower=0;
        foreach(var t in cable.Terminals)
        {
            if(!_targets.TryGetValue(t.Id,out var id))continue;
            double calyxOutgoing=-t.NodeToTerminalPa;currents[id]+=calyxOutgoing;
            CleftNumbers.Finite(currents[id]);interfaceCharge+=calyxOutgoing+t.NodeToTerminalPa;calyxPower+=calyxOutgoing*(states[id].CalyxMv-perilymphMv);
            transfers.Add(new(t.Id,id,calyxOutgoing,t.NodeToTerminalPa));
        }
        var inputs=Sensory.Coupling.Spatial.Compartments.Select(n=>new AvatarCleftLocalMembraneInput(n.Id,gates[n.Id].Hair,gates[n.Id].Calyx,currents[n.Id])).ToArray();
        var sensory=Sensory.Evaluate(hairMv,perilymphMv,bundleDisplacementNanometers,cleftStates,inputs);
        double power=calyxPower+cable.TerminalPowerOutwardFw;CleftNumbers.Finite(interfaceCharge);CleftNumbers.Finite(power);
        return new(sensory,cable,transfers.ToArray(),interfaceCharge,power);
    }
}

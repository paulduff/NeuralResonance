namespace NRE.SimAvatar;

public sealed record AvatarCleftCompartment(string Id, double VolumeCubicMicrometers,
    double HairCleftPf, double CalyxInnerPf, double CalyxOuterPf);
public sealed record AvatarCleftConnection(string Id, string LeftId, string RightId,
    double CleftFaceAreaSquareMicrometers, double CenterDistanceMicrometers,
    double CalyxAxialConductanceNs);
public readonly record struct AvatarCleftCompartmentState(string Id, double CleftMv,
    double CalyxMv, double PotassiumMm, double SodiumMm);

// The actual membrane evaluators must supply these local whole-patch currents.
// Positive ionic/total membrane currents leave their intracellular compartment.
// K/Na chemical ledgers and total electrical current stay separate: omitted
// ionic species must not be invented from the net current.
public readonly record struct AvatarCleftMembranePort(string Id, double HairToCleftPa,
    double CalyxToCleftPa, double CalyxToPerilymphPa, double CalyxToAxonPa,
    double HairPotassiumPa, double HairSodiumPa, double CalyxPotassiumPa, double CalyxSodiumPa);
public readonly record struct AvatarCleftCompartmentDerivative(string Id, double CleftMvPerMs,
    double CalyxMvPerMs, double PotassiumMmPerMs, double SodiumMmPerMs,
    double CleftChargeFc, double CalyxChargeFc, double CleftChargeRatePa, double CalyxChargeRatePa);
public readonly record struct AvatarCleftConnectionTransfer(string Id, string LeftId, string RightId,
    AvatarCleftEdgeFlux Cleft, double CalyxCurrentLeftToRightPa);
public sealed record AvatarCleftSpatialEvaluation(double HairMvPerMs,
    double HairChargeFc, double EndolymphChargeFc, double PerilymphChargeFc,
    double HairChargeRatePa, double EndolymphChargeRatePa, double PerilymphChargeRatePa,
    double StoredEnergyAttojoules, double StoredEnergyRateFw,
    IReadOnlyList<AvatarCleftCompartmentDerivative> Compartments,
    IReadOnlyList<AvatarCleftConnectionTransfer> Transfers);

// Shared equipotential hair cell, spatial cleft/calyx. This assembles actual
// electrical and chemical transfers but does not integrate time or select an
// anatomical mesh. Individual calyx voltages couple through declared axial
// conductances; cleft sites couple through conservative electrodiffusion.
public sealed class AvatarCleftSpatialCoupling
{
    public const string Version = "shared-hair-spatial-cleft-calyx-coupling-v1";
    public string HairCellId { get; }
    public double HairApicalPf { get; }
    public double HairPerilymphPf { get; }
    public AvatarCleftMaterial Material { get; }
    public IReadOnlyList<AvatarCleftCompartment> Compartments { get; }
    public IReadOnlyList<AvatarCleftConnection> Connections { get; }
    public string Fingerprint { get; }
    private readonly Dictionary<string, int> _indices;
    private readonly double[] _calyxTotal, _innerRatio, _cleftDiagonal, _hairRatio;
    private readonly double _hairSchur;
    private readonly Edge[] _edges;
    private sealed record Edge(AvatarCleftConnection Definition, int Left, int Right, AvatarCleftEdgeKernel Kernel);

    public AvatarCleftSpatialCoupling(string hairCellId, double hairApicalPf, double hairPerilymphPf,
        AvatarCleftMaterial material, IEnumerable<AvatarCleftCompartment> compartments,
        IEnumerable<AvatarCleftConnection> connections)
    {
        ArgumentNullException.ThrowIfNull(material); ArgumentNullException.ThrowIfNull(compartments);
        ArgumentNullException.ThrowIfNull(connections); Identity(hairCellId);
        CleftNumbers.Positive(hairApicalPf); CleftNumbers.Nonnegative(hairPerilymphPf); material.Validate();
        HairCellId = hairCellId; HairApicalPf = hairApicalPf; HairPerilymphPf = hairPerilymphPf; Material = material;
        var nodes = compartments.ToArray();
        if (nodes.Length == 0 || nodes.Any(n => n is null)) throw new ArgumentException("At least one explicit compartment is required.");
        foreach (var n in nodes)
        {
            Identity(n.Id); CleftNumbers.Positive(n.VolumeCubicMicrometers);
            CleftNumbers.Positive(n.HairCleftPf); CleftNumbers.Positive(n.CalyxInnerPf); CleftNumbers.Positive(n.CalyxOuterPf);
        }
        nodes = nodes.OrderBy(n => n.Id, StringComparer.Ordinal).ToArray();
        if (nodes.Select(n => n.Id).Distinct(StringComparer.Ordinal).Count() != nodes.Length)
            throw new ArgumentException("Duplicate spatial compartment identity.");
        Compartments = Array.AsReadOnly(nodes);
        _indices = nodes.Select((n,i) => (n.Id,i)).ToDictionary(p => p.Id,p => p.i,StringComparer.Ordinal);
        var links = connections.ToArray();
        if (links.Any(e => e is null)) throw new ArgumentException("Missing spatial connection.");
        var pairs = new HashSet<(string,string)>();
        for (int i = 0; i < links.Length; i++)
        {
            var e = links[i]; Identity(e.Id); Identity(e.LeftId); Identity(e.RightId);
            CleftNumbers.Positive(e.CleftFaceAreaSquareMicrometers); CleftNumbers.Positive(e.CenterDistanceMicrometers);
            CleftNumbers.Nonnegative(e.CalyxAxialConductanceNs);
            if (!_indices.ContainsKey(e.LeftId) || !_indices.ContainsKey(e.RightId) || e.LeftId == e.RightId)
                throw new ArgumentException("Every edge must join two different declared compartments.");
            if (StringComparer.Ordinal.Compare(e.LeftId,e.RightId) > 0)
                e = e with { LeftId = e.RightId, RightId = e.LeftId };
            if (!pairs.Add((e.LeftId,e.RightId))) throw new ArgumentException("A compartment face must not be counted twice.");
            links[i] = e;
        }
        links = links.OrderBy(e => e.Id,StringComparer.Ordinal).ToArray();
        if (links.Select(e => e.Id).Distinct(StringComparer.Ordinal).Count() != links.Length)
            throw new ArgumentException("Duplicate spatial connection identity.");
        Connections = Array.AsReadOnly(links);
        _edges = links.Select(e => new Edge(e,_indices[e.LeftId],_indices[e.RightId],new(material,
            new(e.CleftFaceAreaSquareMicrometers,e.CenterDistanceMicrometers,
                nodes[_indices[e.LeftId]].VolumeCubicMicrometers,nodes[_indices[e.RightId]].VolumeCubicMicrometers)))).ToArray();
        _calyxTotal = new double[nodes.Length]; _innerRatio = new double[nodes.Length];
        _cleftDiagonal = new double[nodes.Length]; _hairRatio = new double[nodes.Length];
        double denominator = hairApicalPf + hairPerilymphPf;
        CleftNumbers.Positive(denominator);
        for (int i = 0; i < nodes.Length; i++)
        {
            var n = nodes[i]; _calyxTotal[i] = n.CalyxInnerPf + n.CalyxOuterPf;
            CleftNumbers.Positive(_calyxTotal[i]); _innerRatio[i] = n.CalyxInnerPf / _calyxTotal[i];
            CleftNumbers.Positive(_innerRatio[i]);
            double effectiveGround = _innerRatio[i] * n.CalyxOuterPf;
            CleftNumbers.Positive(effectiveGround);
            _cleftDiagonal[i] = n.HairCleftPf + effectiveGround; CleftNumbers.Positive(_cleftDiagonal[i]);
            _hairRatio[i] = n.HairCleftPf / _cleftDiagonal[i]; CleftNumbers.Positive(_hairRatio[i]);
            denominator += _hairRatio[i] * effectiveGround;
        }
        CleftNumbers.Positive(denominator); _hairSchur = denominator;
        Fingerprint = CleftNumbers.Fingerprint(new { Version, hairCellId, hairApicalPf, hairPerilymphPf,
            material, compartments = nodes, connections = links, BathConvention = "held endolymph/perilymph; one shared hair voltage" });
    }

    public AvatarCleftSpatialEvaluation Evaluate(double hairMv, double endolymphMv, double perilymphMv,
        double hairToEndolymphPa, double hairToPerilymphPa,
        IReadOnlyList<AvatarCleftCompartmentState> states, IReadOnlyList<AvatarCleftMembranePort> membranePorts)
    {
        ReadOnlySpan<double> global = [hairMv,endolymphMv,perilymphMv,hairToEndolymphPa,hairToPerilymphPa];
        foreach (double value in global) CleftNumbers.Finite(value);
        ArgumentNullException.ThrowIfNull(states); ArgumentNullException.ThrowIfNull(membranePorts);
        int count = Compartments.Count;
        if (states.Count != count || membranePorts.Count != count) throw new ArgumentException("Complete named spatial input required.");
        var ordered = new AvatarCleftCompartmentState[count]; var ports = new AvatarCleftMembranePort[count];
        var seen = new bool[count];
        foreach (var s in states)
        {
            int i = Index(s.Id); if (seen[i]) throw new ArgumentException("Duplicate spatial state."); seen[i] = true;
            new AvatarCleftSite(s.CleftMv,s.PotassiumMm,s.SodiumMm).Validate(); CleftNumbers.Finite(s.CalyxMv); ordered[i] = s;
        }
        Array.Clear(seen);
        foreach (var p in membranePorts)
        {
            int i = Index(p.Id); if (seen[i]) throw new ArgumentException("Duplicate membrane port."); seen[i] = true;
            ReadOnlySpan<double> values = [p.HairToCleftPa,p.CalyxToCleftPa,p.CalyxToPerilymphPa,p.CalyxToAxonPa,
                p.HairPotassiumPa,p.HairSodiumPa,p.CalyxPotassiumPa,p.CalyxSodiumPa];
            foreach (double value in values) CleftNumbers.Finite(value); ports[i] = p;
        }
        var rs = new double[count]; var rc = new double[count]; var dk = new double[count]; var dna = new double[count];
        double rh = -(hairToEndolymphPa + hairToPerilymphPa);
        for (int i = 0; i < count; i++)
        {
            var p = ports[i]; double volume = Compartments[i].VolumeCubicMicrometers;
            rh -= p.HairToCleftPa;
            rs[i] = p.HairToCleftPa + p.CalyxToCleftPa;
            rc[i] = -(p.CalyxToCleftPa + p.CalyxToPerilymphPa + p.CalyxToAxonPa);
            dk[i] = (p.HairPotassiumPa + p.CalyxPotassiumPa) / AvatarCleftEdgeKernel.ChargePaPerAttomolePerMs / volume;
            dna[i] = (p.HairSodiumPa + p.CalyxSodiumPa) / AvatarCleftEdgeKernel.ChargePaPerAttomolePerMs / volume;
        }
        var transfers = new AvatarCleftConnectionTransfer[_edges.Length];
        for (int i = 0; i < _edges.Length; i++)
        {
            var e = _edges[i]; var l = ordered[e.Left]; var r = ordered[e.Right];
            var f = e.Kernel.Evaluate(new(l.CleftMv,l.PotassiumMm,l.SodiumMm),new(r.CleftMv,r.PotassiumMm,r.SodiumMm));
            double axial = e.Definition.CalyxAxialConductanceNs * (l.CalyxMv - r.CalyxMv); CleftNumbers.Finite(axial);
            rs[e.Left] -= f.TotalCurrentPa; rs[e.Right] += f.TotalCurrentPa;
            rc[e.Left] -= axial; rc[e.Right] += axial;
            dk[e.Left] += f.ConcentrationRates.LeftPotassiumMmPerMs; dk[e.Right] += f.ConcentrationRates.RightPotassiumMmPerMs;
            dna[e.Left] += f.ConcentrationRates.LeftSodiumMmPerMs; dna[e.Right] += f.ConcentrationRates.RightSodiumMmPerMs;
            transfers[i] = new(e.Definition.Id,e.Definition.LeftId,e.Definition.RightId,f,axial);
        }
        // Eliminate each calyx voltage, then its cleft voltage, leaving ONE
        // shared hair-cell equation. The apical/exposed capacitance occurs once.
        var local = new double[count]; double numerator = rh;
        for (int i = 0; i < count; i++)
        {
            CleftNumbers.Finite(rs[i]); CleftNumbers.Finite(rc[i]); CleftNumbers.Finite(dk[i]); CleftNumbers.Finite(dna[i]);
            local[i] = rs[i] + _innerRatio[i] * rc[i]; numerator += _hairRatio[i] * local[i];
        }
        double dh = numerator / _hairSchur; CleftNumbers.Finite(dh);
        double qApical = HairApicalPf * (hairMv-endolymphMv), qExposed = HairPerilymphPf * (hairMv-perilymphMv);
        double qh = qApical + qExposed, qp = -qExposed;
        double dqh = (HairApicalPf+HairPerilymphPf)*dh, dqp = -HairPerilymphPf*dh;
        double energy = .5*(qApical*(hairMv-endolymphMv)+qExposed*(hairMv-perilymphMv));
        var derivatives = new AvatarCleftCompartmentDerivative[count]; double powerOther = 0;
        for (int i = 0; i < count; i++)
        {
            var n = Compartments[i]; var s = ordered[i];
            double ds = local[i]/_cleftDiagonal[i]+_hairRatio[i]*dh;
            double dc = rc[i]/_calyxTotal[i]+_innerRatio[i]*ds;
            CleftNumbers.Finite(ds); CleftNumbers.Finite(dc);
            double qHair = n.HairCleftPf*(hairMv-s.CleftMv), qInner = n.CalyxInnerPf*(s.CalyxMv-s.CleftMv), qOuter = n.CalyxOuterPf*(s.CalyxMv-perilymphMv);
            double dqHair = n.HairCleftPf*(dh-ds), dqInner = n.CalyxInnerPf*(dc-ds), dqOuter = n.CalyxOuterPf*dc;
            double qs = -qHair-qInner, qc = qInner+qOuter;
            qh += qHair; qp -= qOuter; dqh += dqHair; dqp -= dqOuter;
            energy += .5*(qHair*(hairMv-s.CleftMv)+qInner*(s.CalyxMv-s.CleftMv)+qOuter*(s.CalyxMv-perilymphMv));
            powerOther += qs*ds+qc*dc;
            ReadOnlySpan<double> values = [qHair,qInner,qOuter,qs,qc,dqHair,dqInner,dqOuter,-dqHair-dqInner,dqInner+dqOuter];
            foreach (double value in values) CleftNumbers.Finite(value);
            derivatives[i] = new(n.Id,ds,dc,dk[i],dna[i],qs,qc,-dqHair-dqInner,dqInner+dqOuter);
        }
        double power = qh*dh+powerOther;
        ReadOnlySpan<double> totals = [rh,qApical,qh,qp,dqh,dqp,energy,power];
        foreach (double value in totals) CleftNumbers.Finite(value);
        return new(dh,qh,-qApical,qp,dqh,-HairApicalPf*dh,dqp,energy,power,
            Array.AsReadOnly(derivatives),Array.AsReadOnly(transfers));
    }

    private static void Identity(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value != value.Trim()) throw new ArgumentException("Explicit stable identity without surrounding whitespace required.");
    }
    private int Index(string id)
    {
        Identity(id);
        return _indices.TryGetValue(id,out int index) ? index : throw new ArgumentException("Unknown spatial compartment identity: "+id);
    }
}

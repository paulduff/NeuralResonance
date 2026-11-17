namespace NRE.SimAvatar;

// One held perilymph reservoir. Its voltage is the same declared perilymph
// reference used by outer membranes/capacitors; only K/Na are explicit species.
public sealed record AvatarCleftPerilymph(string Id, double PotassiumMm, double SodiumMm);
public sealed record AvatarCleftApexFace(string Id, string CompartmentId,
    double AreaSquareMicrometers, double CenterToBathDistanceMicrometers);
public readonly record struct AvatarCleftApexTransfer(string Id, string CompartmentId,
    string ReservoirId, AvatarCleftFaceFlux Outgoing);

// Holding current is supplied externally INTO the held reservoir. Direct
// currents arrive from cells/cleft. Capacitive charge rate is separate.
// Chemical amounts here cover the cleft opening only: the caller's outer
// membrane ports do not supply a complete reservoir ionic-species ledger.
public readonly record struct AvatarCleftReservoirExchange(double CleftPotassiumAttomolesPerMs,
    double CleftSodiumAttomolesPerMs, double CleftCurrentPa, double MembraneCurrentPa,
    double CapacitiveChargeRatePa, double HoldingCurrentPa);
public sealed record AvatarCleftOpenEvaluation(AvatarCleftSpatialEvaluation Spatial,
    IReadOnlyList<AvatarCleftApexTransfer> ApexTransfers, AvatarCleftReservoirExchange Perilymph,
    double EndolymphHoldingCurrentPa);

// Dirichlet boundary at the opening, finite-volume state at the neighbouring
// centre. No forced clamp of a finite compartment and no invented bath volume.
// This remains a derivative evaluator: no anatomical fit or time integrator.
public sealed class AvatarCleftOpenCoupling
{
    public const string Version = "shared-hair-open-cleft-perilymph-v1";
    public AvatarCleftSpatialCoupling Spatial { get; }
    public AvatarCleftPerilymph Reservoir { get; }
    public IReadOnlyList<AvatarCleftApexFace> Faces { get; }
    public string Fingerprint { get; }
    private readonly Face[] _faces;
    private sealed record Face(AvatarCleftApexFace Definition, double PotassiumMobility,
        double SodiumMobility, double OtherConductance);

    public AvatarCleftOpenCoupling(AvatarCleftSpatialCoupling spatial,
        AvatarCleftPerilymph reservoir, IEnumerable<AvatarCleftApexFace> faces)
    {
        ArgumentNullException.ThrowIfNull(spatial); ArgumentNullException.ThrowIfNull(reservoir);
        ArgumentNullException.ThrowIfNull(faces); Identity(reservoir.Id);
        CleftNumbers.Positive(reservoir.PotassiumMm); CleftNumbers.Positive(reservoir.SodiumMm);
        if (reservoir.Id == spatial.HairCellId || spatial.Compartments.Any(n => n.Id == reservoir.Id))
            throw new ArgumentException("The reservoir must have its own identity.");
        Spatial = spatial; Reservoir = reservoir;
        var definitions = faces.ToArray();
        if (definitions.Length == 0 || definitions.Any(f => f is null))
            throw new ArgumentException("An open cleft requires explicit apex faces.");
        var ids = new HashSet<string>(spatial.Connections.Select(e => e.Id),StringComparer.Ordinal);
        var nodes = new HashSet<string>(spatial.Compartments.Select(n => n.Id),StringComparer.Ordinal);
        foreach (var f in definitions)
        {
            Identity(f.Id); Identity(f.CompartmentId);
            if (!ids.Add(f.Id) || !nodes.Contains(f.CompartmentId))
                throw new ArgumentException("Every apex face requires a unique identity and declared compartment.");
            CleftNumbers.Positive(f.AreaSquareMicrometers); CleftNumbers.Positive(f.CenterToBathDistanceMicrometers);
        }
        definitions = definitions.OrderBy(f => f.Id,StringComparer.Ordinal).ToArray();
        Faces = Array.AsReadOnly(definitions);
        _faces = definitions.Select(f =>
        {
            double ratio = f.AreaSquareMicrometers / f.CenterToBathDistanceMicrometers;
            CleftNumbers.Positive(ratio); var m = spatial.Material;
            return new Face(f,AvatarCleftEdgeKernel.Scale(m.PotassiumDiffusionSquareMicrometersPerMs,ratio),
                AvatarCleftEdgeKernel.Scale(m.SodiumDiffusionSquareMicrometersPerMs,ratio),
                AvatarCleftEdgeKernel.Scale(m.OtherIonConductivityNsPerMicrometer,ratio));
        }).ToArray();
        Fingerprint = CleftNumbers.Fingerprint(new { Version, Spatial = spatial.Fingerprint,
            reservoir, faces = definitions, Boundary = "held perilymph at face; explicit centre-to-bath distance" });
    }

    public AvatarCleftOpenEvaluation Evaluate(double hairMv, double endolymphMv, double perilymphMv,
        double hairToEndolymphPa, double hairToPerilymphPa,
        IReadOnlyList<AvatarCleftCompartmentState> states, IReadOnlyList<AvatarCleftMembranePort> membranePorts)
    {
        ArgumentNullException.ThrowIfNull(states); ArgumentNullException.ThrowIfNull(membranePorts);
        // Dictionary construction rejects duplicate names; the spatial owner
        // checks complete input coverage and every remaining value below.
        var named = states.ToDictionary(s => s.Id,StringComparer.Ordinal);
        var bath = new AvatarCleftSite(perilymphMv,Reservoir.PotassiumMm,Reservoir.SodiumMm); bath.Validate();
        var transfers = new AvatarCleftApexTransfer[_faces.Length];
        var outgoing = new CleftBoundaryOutgoing[_faces.Length];
        double k = 0, na = 0, current = 0;
        for (int i = 0; i < _faces.Length; i++)
        {
            var f = _faces[i];
            if (!named.TryGetValue(f.Definition.CompartmentId,out var s)) throw new ArgumentException("Missing apex compartment state.");
            var flow = AvatarCleftEdgeKernel.EvaluateFace(Spatial.Material,f.PotassiumMobility,f.SodiumMobility,f.OtherConductance,
                new(s.CleftMv,s.PotassiumMm,s.SodiumMm),bath);
            transfers[i] = new(f.Definition.Id,s.Id,Reservoir.Id,flow);
            outgoing[i] = new(s.Id,flow.PotassiumAttomolesPerMs,flow.SodiumAttomolesPerMs,flow.TotalCurrentPa);
            k += flow.PotassiumAttomolesPerMs; na += flow.SodiumAttomolesPerMs; current += flow.TotalCurrentPa;
        }
        var result = Spatial.EvaluateWithBoundary(hairMv,endolymphMv,perilymphMv,hairToEndolymphPa,hairToPerilymphPa,states,membranePorts,outgoing);
        double membrane = hairToPerilymphPa;
        // Canonical order also fixes the reservoir accumulation order.
        foreach (var p in membranePorts.OrderBy(p => p.Id,StringComparer.Ordinal)) membrane += p.CalyxToPerilymphPa;
        double hold = result.PerilymphChargeRatePa - membrane - current;
        double holdEndolymph = result.EndolymphChargeRatePa - hairToEndolymphPa;
        ReadOnlySpan<double> values = [k,na,current,membrane,hold,holdEndolymph];
        foreach (double value in values) CleftNumbers.Finite(value);
        return new(result,Array.AsReadOnly(transfers),new(k,na,current,membrane,result.PerilymphChargeRatePa,hold),holdEndolymph);
    }

    private static void Identity(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || id != id.Trim()) throw new ArgumentException("Explicit stable boundary identity required.");
    }
}

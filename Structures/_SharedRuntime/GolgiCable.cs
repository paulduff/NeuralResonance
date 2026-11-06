// Solinas et al., doi:10.3389/neuro.03.002.2007, ModelDB 112685 pinned in
// docs/DNNE_Golgi_Channels_2026-11-06.md. Morphology follows Golgi_template.hoc.
// Independent implementation; unconnected reference-bath candidate only.
internal readonly record struct GolgiCableNode(string Name, int Parent, double AxialConductanceNs,
    double CapacitancePf, double AreaUm2, double LeakConductanceNs);

internal sealed class GolgiCable
{
    public const string Version = "golgi-reference-cable-v1";
    private readonly GolgiCableNode[] _nodes;
    private readonly double[] _diagonal, _rhs;
    public int Count => _nodes.Length;
    public GolgiCableNode Node(int index) => _nodes[index];
    public GolgiCableNode[] CopyNodes() => (GolgiCableNode[])_nodes.Clone();
    public double TotalCapacitancePf => _nodes.Sum(n => n.CapacitancePf);
    public int ElectrodeIndex => _nodes.Length - 1;

    public GolgiCable()
    {
        var nodes = new List<GolgiCableNode>();
        void Segment(string name, int parent, double length, double diameter, double capacitanceDensity,
            double axialResistanceOhms, bool leak = true)
        {
            double area = Math.PI * diameter * length;
            nodes.Add(new(name, parent, parent < 0 ? 0 : 1e9 / axialResistanceOhms,
                area * .01 * capacitanceDensity, area, leak ? area * 10 * .000021 : 0));
        }
        Segment("soma", -1, 27, 27, 1, 0);
        // Shared, massless section endpoints: all three dendrites and the seal
        // meet at soma(1). Giving each a separate soma half-resistance would
        // alter their electrical coupling. Soma(0) connects to the axon.
        double somaHalf = HalfResistanceOhms(27, 27, 100);
        nodes.Add(new("soma-distal-junction", 0, 1e9 / somaHalf, 0, 0, 0));
        nodes.Add(new("soma-proximal-junction", 0, 1e9 / somaHalf, 0, 0, 0));
        for (int branch = 0; branch < 3; ++branch)
            for (int segment = 0; segment < 10; ++segment)
                Segment($"dendrite-{branch}-{segment}", segment == 0 ? 1 : nodes.Count - 1,
                    11.3, 3, 1, HalfResistanceOhms(11.3, 3, 100) * (segment == 0 ? 1 : 2));
        for (int segment = 0; segment < 100; ++segment)
            Segment($"axon-{segment}", segment == 0 ? 2 : nodes.Count - 1,
                12, 2.4, 1, HalfResistanceOhms(12, 2.4, 100) * (segment == 0 ? 1 : 2));
        Segment("seal", 1, 1, 3, .0001, HalfResistanceOhms(1, 3, 1), leak: false);
        Segment("electrode", nodes.Count - 1, 1000, 3, .0015,
            HalfResistanceOhms(1, 3, 1) + HalfResistanceOhms(1000, 3, 36), leak: false);
        _nodes = nodes.ToArray();
        _diagonal = new double[Count]; _rhs = new double[Count];
    }

    private static double HalfResistanceOhms(double lengthUm, double diameterUm, double resistivityOhmCm) =>
        resistivityOhmCm * lengthUm * .5e-4 / (Math.PI * Math.Pow(diameterUm * .5e-4, 2));

    // Backward Euler for a frozen-conductance passive tree. Hines elimination
    // keeps the zero-capacitance branch points in Kirchhoff balance. Membrane
    // conductance is nS, its reversal-weighted source is pA, injection is pA.
    // The first-order coupling error is measured by refinement; this is not an
    // error-controlled whole-cell solver. Scratch arrays belong to this cell.
    public void Solve(ReadOnlySpan<double> previous, double elapsedMs, ReadOnlySpan<double> conductanceNs,
        ReadOnlySpan<double> reversalSourcePa, ReadOnlySpan<double> injectedPa, Span<double> next)
    {
        if (previous.Length != Count || conductanceNs.Length != Count || reversalSourcePa.Length != Count ||
            injectedPa.Length != Count || next.Length != Count || !double.IsFinite(elapsedMs) || elapsedMs <= 0)
            throw new ArgumentException("Require one finite positive interval and every cable node.");
        for (int i = 0; i < Count; ++i)
        {
            if (!double.IsFinite(previous[i]) || !double.IsFinite(conductanceNs[i]) || conductanceNs[i] < 0 ||
                !double.IsFinite(reversalSourcePa[i]) || !double.IsFinite(injectedPa[i]))
                throw new ArgumentException("Invalid cable input; no clipping.");
            var node = _nodes[i];
            _diagonal[i] = node.CapacitancePf / elapsedMs + conductanceNs[i] + node.AxialConductanceNs;
            _rhs[i] = node.CapacitancePf / elapsedMs * previous[i] + reversalSourcePa[i] + injectedPa[i];
        }
        for (int i = 1; i < Count; ++i) _diagonal[_nodes[i].Parent] += _nodes[i].AxialConductanceNs;
        for (int i = Count - 1; i > 0; --i)
        {
            double diagonal = _diagonal[i], axial = _nodes[i].AxialConductanceNs;
            if (!double.IsFinite(diagonal) || diagonal <= 0) throw new InvalidOperationException("Invalid cable pivot.");
            int parent = _nodes[i].Parent;
            _diagonal[parent] -= axial * axial / diagonal;
            _rhs[parent] += axial * _rhs[i] / diagonal;
        }
        if (!double.IsFinite(_diagonal[0]) || _diagonal[0] <= 0) throw new InvalidOperationException("Invalid soma pivot.");
        next[0] = _rhs[0] / _diagonal[0];
        for (int i = 1; i < Count; ++i)
            next[i] = (_rhs[i] + _nodes[i].AxialConductanceNs * next[_nodes[i].Parent]) / _diagonal[i];
        foreach (var voltage in next)
            if (!double.IsFinite(voltage)) throw new InvalidOperationException("Non-finite cable voltage; no reset.");
    }
}

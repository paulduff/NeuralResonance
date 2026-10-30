using NeuralResonanceEngine.Protocol;

// Distinct recovery mechanisms, not a firing command. Equations: De Schutter/
// Bower NaP (ModelDB 149000); de Gruijl et al. 2012 olivary CaT.
// Hybrid densities are provisional; this is not either complete published model.
internal readonly record struct SlowInwardChannelProfile(double Conductance, bool Calcium)
{
    static readonly double SodiumTemperatureScale = Math.Pow(3, (6.3 - 37) / 10);
    public static SlowInwardChannelProfile For(StructureId id) => id switch
    {
        StructureId.InferiorOlive => new(.75, true),
        StructureId.PurkinjeCellLayer or StructureId.CerebellarVermis or StructureId.CerebellarLobules => new(4, false),
        _ => default
    };
    public double Reversal => Calcium ? 120 : 50;
    public (double Equilibrium, double Tau) Activation(double v)
    {
        if (Calcium) return (1 / (1 + Math.Exp(-(v + 61) / 4.2)), 1);
        double alpha = 200 / (1 + Math.Exp(-(v - 18) / 16));
        double beta = 25 / (1 + Math.Exp((v + 58) / 8));
        // Published NaP rates reference 37 C, Q10=3; match the 6.3 C HH host.
        return (alpha / (alpha + beta), 1 / ((alpha + beta) * SodiumTemperatureScale));
    }
    public (double Equilibrium, double Tau) Inactivation(double v) => Calcium
        ? (1 / (1 + Math.Exp((v + 85.5) / 8.5)),
            20 * Math.Exp((v + 160) / 30) / (1 + Math.Exp((v + 84) / 7.3)) + 35)
        : (1, 1);
}


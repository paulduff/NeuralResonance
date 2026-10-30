using NeuralResonanceEngine.Protocol;

// Published Ih gate equations, combined with a reference HH membrane: a hybrid,
// NOT the complete Purkinje/olivary models or a temperature-matched cell fit.
// KGR/ModelDB 48332 ihpkj.mod; de Gruijl et al. 2012, doi:10.1371/journal.pcbi.1002814.
internal readonly record struct HcnChannelProfile(double Conductance, double Reversal, bool Olivary)
{
    public static HcnChannelProfile For(StructureId id) => id switch
    {
        StructureId.InferiorOlive => new(.12, -43, true),
        StructureId.PurkinjeCellLayer or StructureId.CerebellarVermis or StructureId.CerebellarLobules => new(.1, -30, false),
        _ => default
    };
    public double Equilibrium(double v) => Olivary
        ? 1 / (1 + Math.Exp((v + 80) / 4)) : 1 / (1 + Math.Exp((v + 90.1) / 9.9));
    public double TimeConstant(double v) => Olivary
        ? 1 / (Math.Exp(-.086 * v - 14.6) + Math.Exp(.070 * v - 1.87))
        : 1000 * (.19 + .72 * Math.Exp(-Math.Pow((v + 81.5) / 11.9, 2)));
}

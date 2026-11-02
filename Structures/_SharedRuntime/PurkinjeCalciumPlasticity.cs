// Reduced, contact-local biochemical candidate, NOT a reproduction of a full
// Purkinje neuron or a fitted human spine. See the parameter/source table in
// docs/DNNE_Purkinje_Calcium_Candidate_2026-11-02.md.
internal sealed class PurkinjeCalciumPlasticity
{
    internal const string Rule = "PurkinjeCalciumLTD";
    internal const string Version = "purkinje-spine-calcium-v1";
    internal const double RestCalciumUm = .05;
    internal double Receptor { get; private set; }
    internal double Ip3Um { get; private set; } = .1;
    internal double CalciumUm { get; private set; } = RestCalciumUm;
    internal double StoreUm { get; private set; } = 5;
    internal double Availability { get; private set; } = 1;
    internal double IntegratedCalciumUm { get; private set; }
    internal double InducedDepression { get; private set; }
    internal double PeakCalciumUm { get; private set; } = RestCalciumUm;
    internal double Weight { get; private set; }
    internal double EntryUm { get; private set; }
    internal double ExtrudedUm { get; private set; }
    internal double MaximumMassError { get; private set; }
    internal double Activation { get; private set; }
    internal double Inactivation { get; private set; }
    readonly double initialWeight;
    internal bool BlockEntry { get; init; }
    internal bool BlockRelease { get; init; }

    internal PurkinjeCalciumPlasticity(double weight, double restingVoltageMv)
    {
        initialWeight = Weight = weight;
        var (a,b,c,d)=Rates(restingVoltageMv);
        Activation=a/(a+b);Inactivation=c/(c+d);
    }

    internal void ParallelRelease(double quanta)
    {
        if (!double.IsFinite(quanta) || quanta <= 0) throw new ArgumentOutOfRangeException(nameof(quanta));
        // Finite receptor occupancy, not a capped teaching-event count.
        Receptor=1-(1-Receptor)*Math.Exp(-.35*quanta);
    }

    // P-channel alpha/beta equations (De Schutter/Bower reference, mV/ms).
    // Channel density/buffering conversion below is a separate provisional fit.
    static (double,double,double,double) Rates(double v) => (
        8.5/(1+Math.Exp((v-8)/-12.5)),35/(1+Math.Exp((v+74)/14.5)),
        .0015/(1+Math.Exp((v+29)/8)),.0055/(1+Math.Exp((v+23)/-8)));

    internal double Advance(double dtMs, double dendriteVoltageMv)
    {
        if (!double.IsFinite(dtMs) || dtMs<=0 || !double.IsFinite(dendriteVoltageMv))
            throw new InvalidOperationException("Invalid spine integration input.");
        double previousWeight=Weight;
        int n=(int)Math.Ceiling(dtMs/.125);double h=dtMs/n;
        for(int i=0;i<n;i++)
        {
            var (a,b,c,d)=Rates(dendriteVoltageMv);
            Activation=a/(a+b)+(Activation-a/(a+b))*Math.Exp(-(a+b)*h);
            Inactivation=c/(c+d)+(Inactivation-c/(c+d))*Math.Exp(-(c+d)*h);
            double receptorMid=Receptor*Math.Exp(-h/160);
            double targetIp3=.1+3.75*receptorMid;
            Ip3Um=targetIp3+(Ip3Um-targetIp3)*Math.Exp(-h/150);
            Receptor*=Math.Exp(-h/80);
            double ip3=Ip3Um-.1, ip3Cube=ip3*ip3*ip3;
            double ca2=CalciumUm*CalciumUm;
            double releaseRate=BlockRelease?0:.02*ip3Cube/(1+ip3Cube)*ca2/(1+ca2)*Availability;
            double release=StoreUm*(1-Math.Exp(-releaseRate*h));
            StoreUm-=release;CalciumUm+=release;
            // Free-calcium conversion includes effective channel density, spine
            // geometry and buffering. No input-source label directly raises Ca.
            double entry=BlockEntry?0:.001*Activation*Inactivation*Math.Max(0,120-dendriteVoltageMv)*h;
            CalciumUm+=entry;EntryUm+=entry;
            double excess=CalciumUm-RestCalciumUm;
            double removed=excess*(1-Math.Exp(-h/90));
            CalciumUm-=removed;ExtrudedUm+=removed;
            // A finite store refills from cytosolic excess; record conserved mass.
            double uptake=(CalciumUm-RestCalciumUm)*(1-Math.Exp(-.002*h))*(1-StoreUm/5);
            CalciumUm-=uptake;StoreUm+=uptake;
            double available=.3/(.3+CalciumUm);
            Availability=available+(Availability-available)*Math.Exp(-h/100);
            double excessCa=CalciumUm-RestCalciumUm;
            IntegratedCalciumUm=excessCa+(IntegratedCalciumUm-excessCa)*Math.Exp(-h/560);
            double hill=Math.Pow(IntegratedCalciumUm/1.8,3.8);
            // Reduced persistent induction/AMPAR-expression model. The retained
            // peak is an explicit approximation to molecular consolidation.
            InducedDepression=Math.Max(InducedDepression,.372*hill/(1+hill));
            // Preserve the existing persisted efficacy domain. This numerical
            // floor is a compatibility limit, not an AMPAR physiological bound.
            double targetWeight=Math.Max(.05,initialWeight*(1-InducedDepression));
            Weight=targetWeight+(Weight-targetWeight)*Math.Exp(-h/390000);
            PeakCalciumUm=Math.Max(PeakCalciumUm,CalciumUm);
            MaximumMassError=Math.Max(MaximumMassError,Math.Abs(CalciumUm+StoreUm-(5+RestCalciumUm+EntryUm-ExtrudedUm)));
            if(!double.IsFinite(Weight+CalciumUm+StoreUm+IntegratedCalciumUm) || CalciumUm<RestCalciumUm-1e-10 || StoreUm<0 || StoreUm>5+1e-10)
                throw new InvalidOperationException("Spine chemistry left its physical domain.");
        }
        return Weight-previousWeight;
    }
}

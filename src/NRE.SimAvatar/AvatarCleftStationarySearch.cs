namespace NRE.SimAvatar;

public sealed record AvatarCleftStationarySettings(double VoltageRateToleranceMvPerMs=1e-9,
    double IonRateToleranceMmPerMs=1e-11, double GateRateTolerancePerMs=1e-12,
    int MaximumNewtonIterations=40, int MaximumLineSearchHalvings=24)
{
    public void Validate()
    {
        CleftNumbers.Positive(VoltageRateToleranceMvPerMs); CleftNumbers.Positive(IonRateToleranceMmPerMs);
        CleftNumbers.Positive(GateRateTolerancePerMs);
        if (MaximumNewtonIterations is < 1 or > 100 || MaximumLineSearchHalvings is < 1 or > 40)
            throw new ArgumentException("Declare bounded stationary-search work.");
    }
}

public sealed record AvatarCleftStationaryPoint(double HairMv, AvatarCleftCompartmentState[] Compartments,
    AvatarCleftGateState[] Gates, double[] VoltageIonRates, double MaximumGateRatePerMs,
    double ScaledResidual, AvatarVestibularCleftEvaluation Evaluation);
public sealed record AvatarCleftStationaryIteration(int Iteration, int AcceptedHalvings,
    double[] TransformedState, AvatarCleftStationaryPoint Point);
public sealed record AvatarCleftStationaryResult(string Version,string Fingerprint,string KernelFingerprint,
    AvatarCleftStationarySettings Settings,double PerilymphMv,double BundleDisplacementNanometers,
    AvatarCleftAxonCurrent[] AxonCurrents,string[] UnknownLabels,string[] RateLabels,
    double[] InitialTransformedState,AvatarCleftStationaryIteration[] AcceptedIterations,
    bool StationaryCriteriaPassed,string Outcome,long Evaluations,string Phase,
    double[] LastTrialTransformedState,string? LastTrialError,
    bool LocalStabilityAssessed,bool PhysiologicalRestQualified,bool LiveCellModified);

// Algebraic initialization search only. Gate equilibrium is an exact elimination
// of dg/dt=(g_inf(V)-g)/tau(V)=0 for finite positive tau; it is NOT a gate reset
// during time integration. A root need not be stable, unique or physiological.
public sealed class AvatarCleftStationarySearch
{
    public const string Version="joint-cleft-algebraic-stationary-search-v1";
    private const int Width=4;
    private readonly AvatarVestibularCleftMembranes _kernel;
    private readonly AvatarCleftStationarySettings _settings;
    private readonly AvatarCleftCompartment[] _nodes;
    public AvatarCleftStationarySearch(AvatarVestibularCleftMembranes kernel,AvatarCleftStationarySettings settings)
    {
        ArgumentNullException.ThrowIfNull(kernel); ArgumentNullException.ThrowIfNull(settings); settings.Validate();
        _kernel=kernel;_settings=settings;_nodes=kernel.Coupling.Spatial.Compartments.ToArray();
    }

    public AvatarCleftStationaryResult Find(double hairMv,double perilymphMv,
        IReadOnlyList<AvatarCleftCompartmentState> initial,double bundleDisplacementNanometers,
        IReadOnlyList<AvatarCleftAxonCurrent> axonCurrents)
    {
        CleftNumbers.Finite(hairMv);CleftNumbers.Finite(perilymphMv);CleftNumbers.Finite(bundleDisplacementNanometers);
        CleftNumbers.Finite(perilymphMv+_kernel.HairParameters.EndolymphPotentialMv);
        ArgumentNullException.ThrowIfNull(initial);ArgumentNullException.ThrowIfNull(axonCurrents);
        if (initial.Count!=_nodes.Length || axonCurrents.Count!=_nodes.Length)
            throw new ArgumentException("Complete initial compartments and explicit terminal currents required.");
        var named=initial.ToDictionary(s=>s.Id,StringComparer.Ordinal);var terminals=axonCurrents.ToDictionary(p=>p.Id,StringComparer.Ordinal);
        var currents=new AvatarCleftAxonCurrent[_nodes.Length];var first=new double[1+Width*_nodes.Length];first[0]=hairMv-perilymphMv;
        var labels=new List<string> { "hairRelativeToBathMv" };var rateLabels=new List<string> { "hairMvPerMs" };
        for (int i=0;i<_nodes.Length;i++)
        {
            var id=_nodes[i].Id;
            if (!named.TryGetValue(id,out var s) || !terminals.TryGetValue(id,out var terminal)) throw new ArgumentException("Unknown/missing stationary compartment identity.");
            new AvatarCleftSite(s.CleftMv,s.PotassiumMm,s.SodiumMm).Validate();CleftNumbers.Finite(s.CalyxMv);CleftNumbers.Finite(terminal.OutgoingPa);
            currents[i]=terminal;int o=1+Width*i;first[o]=s.CleftMv-perilymphMv;first[o+1]=s.CalyxMv-perilymphMv;
            first[o+2]=Math.Log(s.PotassiumMm);first[o+3]=Math.Log(s.SodiumMm);
            labels.AddRange(new[] { "cleftRelativeToBathMv","calyxRelativeToBathMv","logPotassiumMm","logSodiumMm" }.Select(n=>id+"/"+n));
            rateLabels.AddRange(new[] { "cleftMvPerMs","calyxMvPerMs","potassiumMmPerMs","sodiumMmPerMs" }.Select(n=>id+"/"+n));
        }
        foreach (double x in first) CleftNumbers.Finite(x);
        var history=new List<AvatarCleftStationaryIteration>();var lastTrial=(double[])first.Clone();string? lastError=null;
        long evaluations=0;string phase="initial";bool passed=false;string outcome="Unattempted";
        string fingerprint=CleftNumbers.Fingerprint(new { Version,_kernel.Fingerprint,_settings,perilymphMv,bundleDisplacementNanometers,
            AxonCurrents=currents,InitialUnknowns=first,Coordinates="bath-relative voltages; natural logs of positive mM concentrations",
            GateElimination="actual local g_inf at a stationary point; no dynamic reset",DifferenceFraction=Math.ScaleB(1,-20),
            Interpretation="stationary residual only; stability/physiology remain separate" });
        try
        {
            var x=(double[])first.Clone();var point=Evaluate(x);history.Add(new(0,0,(double[])x.Clone(),point));
            for (int iteration=0;iteration<=_settings.MaximumNewtonIterations;iteration++)
            {
                if (point.ScaledResidual<=1) { passed=true;outcome="StationaryResidualCriteriaMet";break; }
                if (iteration==_settings.MaximumNewtonIterations) { outcome="IterationLimit";break; }
                var matrix=new double[x.Length,x.Length];
                for (int column=0;column<x.Length;column++)
                {
                    double step=Math.ScaleB(Math.Max(1,Math.Abs(x[column])),-20);
                    var a=(double[])x.Clone();var b=(double[])x.Clone();a[column]+=step;b[column]-=step;
                    phase="jacobian-positive";var plus=TryEvaluate(a);
                    phase="jacobian-negative";var minus=TryEvaluate(b);
                    if (plus is null && minus is null) throw new ArithmeticException("No in-domain stationary Jacobian perturbation.");
                    double distance=(plus is null ? x[column] : a[column])-(minus is null ? x[column] : b[column]);CleftNumbers.Positive(distance);
                    for (int row=0;row<x.Length;row++) matrix[row,column]=((plus??point).VoltageIonRates[row]-(minus??point).VoltageIonRates[row])/distance;
                }
                phase="linear-solve";lastTrial=(double[])x.Clone();
                var correction=Solve(matrix,point.VoltageIonRates.Select(v=>-v).ToArray());bool accepted=false;
                for (int half=0;half<=_settings.MaximumLineSearchHalvings;half++)
                {
                    double alpha=Math.ScaleB(1,-half);var trial=new double[x.Length];
                    for (int i=0;i<trial.Length;i++) trial[i]=x[i]+alpha*correction[i];
                    phase="line-search";var candidate=TryEvaluate(trial);
                    if (candidate is null || candidate.ScaledResidual>=point.ScaledResidual) continue;
                    x=trial;point=candidate;history.Add(new(iteration+1,half,(double[])x.Clone(),point));accepted=true;break;
                }
                if (!accepted) { outcome="LineSearchFailed";break; }
            }
        }
        catch (Exception error) when (error is ArgumentException or ArithmeticException or InvalidOperationException)
        {
            outcome="NumericalFailure";lastError=error.ToString();
        }
        return new(Version,fingerprint,_kernel.Fingerprint,_settings,perilymphMv,bundleDisplacementNanometers,currents,
            labels.ToArray(),rateLabels.ToArray(),first,history.ToArray(),passed,outcome,evaluations,phase,lastTrial,lastError,
            LocalStabilityAssessed:false,PhysiologicalRestQualified:false,LiveCellModified:false);

        AvatarCleftStationaryPoint? TryEvaluate(double[] trial)
        {
            lastTrial=(double[])trial.Clone();lastError=null;
            try { return Evaluate(trial); }
            catch (Exception error) when (error is ArgumentException or ArithmeticException)
            { lastError=error.ToString();return null; }
        }
        AvatarCleftStationaryPoint Evaluate(double[] values)
        {
            lastTrial=(double[])values.Clone();evaluations=checked(evaluations+1);
            foreach (double value in values) CleftNumbers.Finite(value);
            double h=perilymphMv+values[0];CleftNumbers.Finite(h);
            var states=new AvatarCleftCompartmentState[_nodes.Length];var gates=new AvatarCleftGateState[_nodes.Length];
            var inputs=new AvatarCleftLocalMembraneInput[_nodes.Length];
            for (int i=0;i<_nodes.Length;i++)
            {
                int o=1+Width*i;string id=_nodes[i].Id;
                // Log coordinates impose positivity by a variable transform,
                // not a clipped concentration or substituted membrane flux.
                double s=perilymphMv+values[o],c=perilymphMv+values[o+1],k=Math.Exp(values[o+2]),na=Math.Exp(values[o+3]);
                new AvatarCleftSite(s,k,na).Validate();CleftNumbers.Finite(c);states[i]=new(id,s,c,k,na);
                var hr=AvatarHairCellKernel.Rates(h-s);var cr=AvatarCalyxKernel.Rates(c-perilymphMv,s-perilymphMv);
                var hg=new AvatarHairMembraneGates(hr.PotassiumLow.Equilibrium,hr.Calcium.Equilibrium,hr.Hcn1.Equilibrium);
                var cg=new AvatarCalyxGates(cr.Inner.Kv74.Equilibrium,cr.Inner.Hcn2.Equilibrium,cr.Outer.Kv74.Equilibrium,cr.Outer.Hcn2.Equilibrium);
                gates[i]=new(id,hg,cg);inputs[i]=new(id,hg,cg,currents[i].OutgoingPa);
            }
            var result=_kernel.Evaluate(h,perilymphMv,bundleDisplacementNanometers,states,inputs);
            var rates=new double[values.Length];rates[0]=result.Coupling.Spatial.HairMvPerMs;double maximumGate=0;
            for (int i=0;i<_nodes.Length;i++)
            {
                int o=1+Width*i;var s=result.Coupling.Spatial.Compartments[i];var m=result.Membranes[i];
                if (s.Id!=_nodes[i].Id || m.Id!=s.Id) throw new InvalidOperationException("Stationary derivative identity changed.");
                rates[o]=s.CleftMvPerMs;rates[o+1]=s.CalyxMvPerMs;rates[o+2]=s.PotassiumMmPerMs;rates[o+3]=s.SodiumMmPerMs;
                var gh=m.HairGateDerivatives;var gc=m.CalyxGateDerivatives;
                foreach (double v in new[] { gh.PotassiumLowPerMs,gh.CalciumPerMs,gh.Hcn1PerMs,gc.InnerKv74PerMs,gc.InnerHcn2PerMs,gc.OuterKv74PerMs,gc.OuterHcn2PerMs })
                { CleftNumbers.Finite(v);maximumGate=Math.Max(maximumGate,Math.Abs(v)); }
            }
            double norm=maximumGate/_settings.GateRateTolerancePerMs;
            for (int i=0;i<rates.Length;i++)
            {
                CleftNumbers.Finite(rates[i]);bool ion=i>0 && (i-1)%Width>=2;
                double scaled=Math.Abs(rates[i])/(ion ? _settings.IonRateToleranceMmPerMs : _settings.VoltageRateToleranceMvPerMs);
                CleftNumbers.Finite(scaled);norm=Math.Max(norm,scaled);
            }
            CleftNumbers.Finite(norm);return new(h,states,gates,rates,maximumGate,norm,result);
        }
    }

    private static double[] Solve(double[,] a,double[] rhs)
    {
        // Row scaling changes neither the root nor the equations. No added
        // diagonal, pseudo-inverse or fabricated constraint hides a singularity.
        int n=rhs.Length;
        for (int i=0;i<n;i++)
        {
            double scale=0;for (int j=0;j<n;j++) { CleftNumbers.Finite(a[i,j]);scale=Math.Max(scale,Math.Abs(a[i,j])); }
            if (scale==0) throw new ArithmeticException("Underconstrained stationary Jacobian (zero row).");
            for (int j=0;j<n;j++) a[i,j]/=scale;rhs[i]/=scale;CleftNumbers.Finite(rhs[i]);
        }
        for (int column=0;column<n;column++)
        {
            int pivot=column;for (int i=column+1;i<n;i++) if (Math.Abs(a[i,column])>Math.Abs(a[pivot,column])) pivot=i;
            if (!double.IsFinite(a[pivot,column]) || a[pivot,column]==0) throw new ArithmeticException("Singular stationary Jacobian; uniqueness not established.");
            if (pivot!=column)
            {
                for (int j=column;j<n;j++) (a[column,j],a[pivot,j])=(a[pivot,j],a[column,j]);
                (rhs[column],rhs[pivot])=(rhs[pivot],rhs[column]);
            }
            for (int i=column+1;i<n;i++)
            {
                double factor=a[i,column]/a[column,column];CleftNumbers.Finite(factor);
                for (int j=column+1;j<n;j++) a[i,j]-=factor*a[column,j];rhs[i]-=factor*rhs[column];a[i,column]=0;
            }
        }
        var x=new double[n];
        for (int i=n-1;i>=0;i--)
        {
            double value=rhs[i];for (int j=i+1;j<n;j++) value-=a[i,j]*x[j];x[i]=value/a[i,i];CleftNumbers.Finite(x[i]);
        }
        return x;
    }
}

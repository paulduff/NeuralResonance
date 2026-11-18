namespace NRE.SimAvatar;

public readonly record struct AvatarAfferentStationaryGuess(string Id,double PotentialMv);

public sealed record AvatarAfferentStationaryPoint(double HairMv, AvatarCleftCompartmentState[] Compartments,
    AvatarCleftGateState[] Gates, AvatarAfferentCableState[] CableStates, double[] VoltageIonRates, double MaximumGateRatePerMs,
    double ScaledResidual, AvatarVestibularCleftAfferentEvaluation Evaluation);
public sealed record AvatarAfferentStationaryIteration(int Iteration, int AcceptedHalvings,
    double[] TransformedState, AvatarAfferentStationaryPoint Point);
public sealed record AvatarAfferentStationaryResult(string Version,string Fingerprint,string KernelFingerprint,
    AvatarCleftStationarySettings Settings,double PerilymphMv,double BundleDisplacementNanometers,
    string[] UnknownLabels,string[] RateLabels,
    double[] InitialTransformedState,AvatarAfferentStationaryIteration[] AcceptedIterations,
    bool StationaryCriteriaPassed,string Outcome,long Evaluations,string Phase,
    double[] LastTrialTransformedState,string? LastTrialError,
    bool LocalStabilityAssessed,bool PhysiologicalRestQualified,bool LiveCellModified);

// Isolated complete sensory/nerve algebraic search; no live caller or reset.
// Both sides of each attachment use the current root trial.
// Algebraic initialization search only. Gate equilibrium is an exact elimination
// of dg/dt=(g_inf(V)-g)/tau(V)=0 for finite positive tau; it is NOT a gate reset
// during time integration. A root need not be stable, unique or physiological.
public sealed class AvatarAfferentStationarySearch
{
    public const string Version="joint-cleft-afferent-algebraic-stationary-search-v1";
    private const int Width=4;
    private readonly AvatarVestibularCleftAfferent _kernel;
    private readonly AvatarCleftStationarySettings _settings;
    private readonly AvatarCleftCompartment[] _nodes;
    private readonly AvatarAfferentCableNode[] _cableNodes;
    public AvatarAfferentStationarySearch(AvatarVestibularCleftAfferent kernel,AvatarCleftStationarySettings settings)
    {
        ArgumentNullException.ThrowIfNull(kernel); ArgumentNullException.ThrowIfNull(settings); settings.Validate();
        _kernel=kernel;_settings=settings;_nodes=kernel.Sensory.Coupling.Spatial.Compartments.ToArray();_cableNodes=kernel.Cable.Nodes.ToArray();
    }

    public AvatarAfferentStationaryResult Find(double hairMv,double perilymphMv,
        IReadOnlyList<AvatarCleftCompartmentState> initial,
        IReadOnlyList<AvatarAfferentStationaryGuess> cableInitial,double bundleDisplacementNanometers)
    {
        CleftNumbers.Finite(hairMv);CleftNumbers.Finite(perilymphMv);CleftNumbers.Finite(bundleDisplacementNanometers);
        CleftNumbers.Finite(perilymphMv+_kernel.Sensory.HairParameters.EndolymphPotentialMv);
        ArgumentNullException.ThrowIfNull(initial);ArgumentNullException.ThrowIfNull(cableInitial);
        if (initial.Count!=_nodes.Length || cableInitial.Count!=_cableNodes.Length)
            throw new ArgumentException("Complete identified sensory and cable voltage guesses required.");
        var named=initial.ToDictionary(s=>s.Id,StringComparer.Ordinal);var cableNamed=cableInitial.ToDictionary(p=>p.Id,StringComparer.Ordinal);
        int cableStart=1+Width*_nodes.Length;var first=new double[cableStart+_cableNodes.Length];first[0]=hairMv-perilymphMv;
        var labels=new List<string> { "hairRelativeToBathMv" };var rateLabels=new List<string> { "hairMvPerMs" };
        for (int i=0;i<_nodes.Length;i++)
        {
            var id=_nodes[i].Id;
            if (!named.TryGetValue(id,out var s)) throw new ArgumentException("Unknown/missing stationary compartment identity.");
            new AvatarCleftSite(s.CleftMv,s.PotassiumMm,s.SodiumMm).Validate();CleftNumbers.Finite(s.CalyxMv);
            int o=1+Width*i;first[o]=s.CleftMv-perilymphMv;first[o+1]=s.CalyxMv-perilymphMv;
            first[o+2]=Math.Log(s.PotassiumMm);first[o+3]=Math.Log(s.SodiumMm);
            labels.AddRange(new[] { "cleftRelativeToBathMv","calyxRelativeToBathMv","logPotassiumMm","logSodiumMm" }.Select(n=>id+"/"+n));
            rateLabels.AddRange(new[] { "cleftMvPerMs","calyxMvPerMs","potassiumMmPerMs","sodiumMmPerMs" }.Select(n=>id+"/"+n));
        }
        for (int i=0;i<_cableNodes.Length;i++)
        {
            string id=_cableNodes[i].Id;
            if(!cableNamed.TryGetValue(id,out var guess))throw new ArgumentException("Unknown/missing stationary cable identity.");
            CleftNumbers.Finite(guess.PotentialMv);first[cableStart+i]=guess.PotentialMv-perilymphMv;
            labels.Add("axon/"+id+"/relativeToBathMv");rateLabels.Add("axon/"+id+"/potentialMvPerMs");
        }
        foreach (double x in first) CleftNumbers.Finite(x);
        var history=new List<AvatarAfferentStationaryIteration>();var lastTrial=(double[])first.Clone();string? lastError=null;
        long evaluations=0;string phase="initial";bool passed=false;string outcome="Unattempted";
        string fingerprint=CleftNumbers.Fingerprint(new { Version,_kernel.Fingerprint,_settings,perilymphMv,bundleDisplacementNanometers,
            InitialUnknowns=first,Connection="same-state reciprocal calyx/cable current; no prescribed axon load",Coordinates="bath-relative voltages; natural logs of positive mM concentrations",
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
        return new(Version,fingerprint,_kernel.Fingerprint,_settings,perilymphMv,bundleDisplacementNanometers,
            labels.ToArray(),rateLabels.ToArray(),first,history.ToArray(),passed,outcome,evaluations,phase,lastTrial,lastError,
            LocalStabilityAssessed:false,PhysiologicalRestQualified:false,LiveCellModified:false);

        AvatarAfferentStationaryPoint? TryEvaluate(double[] trial)
        {
            lastTrial=(double[])trial.Clone();lastError=null;
            try { return Evaluate(trial); }
            catch (Exception error) when (error is ArgumentException or ArithmeticException)
            { lastError=error.ToString();return null; }
        }
        AvatarAfferentStationaryPoint Evaluate(double[] values)
        {
            lastTrial=(double[])values.Clone();evaluations=checked(evaluations+1);
            foreach (double value in values) CleftNumbers.Finite(value);
            double h=perilymphMv+values[0];CleftNumbers.Finite(h);
            var states=new AvatarCleftCompartmentState[_nodes.Length];var gates=new AvatarCleftGateState[_nodes.Length];
            var cable=new AvatarAfferentCableState[_cableNodes.Length];
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
                gates[i]=new(id,hg,cg);
            }
            for(int i=0;i<_cableNodes.Length;i++)
            {
                double v=perilymphMv+values[cableStart+i];CleftNumbers.Finite(v);
                cable[i]=new(_cableNodes[i].Id,v,AvatarAfferentMembraneKernel.VoltageConditionedGates(v-perilymphMv));
            }
            var result=_kernel.Evaluate(h,perilymphMv,bundleDisplacementNanometers,states,gates,cable);
            var rates=new double[values.Length];rates[0]=result.Sensory.Coupling.Spatial.HairMvPerMs;double maximumGate=0;
            for (int i=0;i<_nodes.Length;i++)
            {
                int o=1+Width*i;var s=result.Sensory.Coupling.Spatial.Compartments[i];var m=result.Sensory.Membranes[i];
                if (s.Id!=_nodes[i].Id || m.Id!=s.Id) throw new InvalidOperationException("Stationary derivative identity changed.");
                rates[o]=s.CleftMvPerMs;rates[o+1]=s.CalyxMvPerMs;rates[o+2]=s.PotassiumMmPerMs;rates[o+3]=s.SodiumMmPerMs;
                var gh=m.HairGateDerivatives;var gc=m.CalyxGateDerivatives;
                foreach (double v in new[] { gh.PotassiumLowPerMs,gh.CalciumPerMs,gh.Hcn1PerMs,gc.InnerKv74PerMs,gc.InnerHcn2PerMs,gc.OuterKv74PerMs,gc.OuterHcn2PerMs })
                { CleftNumbers.Finite(v);maximumGate=Math.Max(maximumGate,Math.Abs(v)); }
            }
            for(int i=0;i<_cableNodes.Length;i++)
            {
                var node=result.Cable.Nodes[i];if(node.Id!=_cableNodes[i].Id)throw new InvalidOperationException("Stationary cable derivative identity changed.");
                rates[cableStart+i]=node.PotentialMvPerMs;var g=node.Membrane.GateDerivatives;
                foreach(double v in new[] { g.SodiumActivationPerMs,g.SodiumInactivationPerMs,g.Kv1ActivationPerMs,
                    g.Kv7ActivationPerMs,g.Kv34ActivationPerMs,g.Kv34InactivationPerMs })
                { CleftNumbers.Finite(v);maximumGate=Math.Max(maximumGate,Math.Abs(v)); }
            }
            double norm=maximumGate/_settings.GateRateTolerancePerMs;
            for (int i=0;i<rates.Length;i++)
            {
                CleftNumbers.Finite(rates[i]);bool ion=i>0 && i<cableStart && (i-1)%Width>=2;
                double scaled=Math.Abs(rates[i])/(ion ? _settings.IonRateToleranceMmPerMs : _settings.VoltageRateToleranceMvPerMs);
                CleftNumbers.Finite(scaled);norm=Math.Max(norm,scaled);
            }
            CleftNumbers.Finite(norm);return new(h,states,gates,cable,rates,maximumGate,norm,result);
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

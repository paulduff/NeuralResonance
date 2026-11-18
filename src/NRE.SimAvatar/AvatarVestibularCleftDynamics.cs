namespace NRE.SimAvatar;

public readonly record struct AvatarCleftGateState(string Id, AvatarHairMembraneGates Hair, AvatarCalyxGates Calyx);
public readonly record struct AvatarCleftAxonCurrent(string Id, double OutgoingPa);
public sealed record AvatarCleftIntegrationSettings(double StepMs, double VoltageAbsoluteToleranceMv = 1e-9,
    double IonAbsoluteToleranceMm = 1e-11, double GateAbsoluteTolerance = 1e-12,
    double RelativeTolerance = 1e-11, int MaximumNewtonIterations = 12, int MaximumLineSearchHalvings = 16)
{
    public void Validate()
    {
        foreach (double x in new[] { StepMs, VoltageAbsoluteToleranceMv, IonAbsoluteToleranceMm, GateAbsoluteTolerance, RelativeTolerance })
            CleftNumbers.Positive(x);
        if (StepMs > .05 || MaximumNewtonIterations is < 1 or > 64 || MaximumLineSearchHalvings is < 1 or > 32)
            throw new ArgumentException("Declare a step no larger than .05 ms and bounded nonlinear work; accuracy still requires refinement.");
    }
}

public sealed record AvatarCleftHeldInput(long StartingStep, long RequestedEndStep,
    double BundleDisplacementNanometers, AvatarCleftAxonCurrent[] AxonCurrents);
public sealed record AvatarCleftDynamicsFailure(string Reason, string Phase, int ImplicitStage, int NewtonIteration,
    double[] StageBase, double[] Candidate, double[]? Residual);
public sealed record AvatarCleftDynamicsState(Guid SessionId, Guid CellId, string Version, string Fingerprint,
    string InitialStateFingerprint, long CompletedSteps, double EpochMs, double TimeMs,
    double PerilymphMv, AvatarCleftIntegrationSettings Settings, string[] StateLabels, double[] InitialValues, double[] Values,
    double[] AdditionResiduals, string[] LedgerLabels, double[] Ledgers, double[] LedgerAdditionResiduals,
    double MaximumChargeResidualFc, double MaximumIonResidualAttomoles,
    long NonlinearEvaluations, int MaximumNewtonIterations, double MaximumAcceptedScaledResidual,
    AvatarCleftHeldInput? MostRecentHeldInput, AvatarCleftDynamicsFailure? Failure);

// Two-stage, second-order L-stable SDIRK: gamma=1-1/sqrt(2), a21=1-gamma,
// b=(1-gamma,gamma). Every Newton evaluation uses the complete same-stage
// voltage/ion/gate state. No operator split, gate reset or membrane clamp.
// Fixed timestep; no hidden retry, adaptive substitution or live selection.
public sealed class AvatarVestibularCleftDynamics
{
    public const string Version = "joint-hair-cleft-calyx-sdirk2-compensated-v1";
    public static readonly double Gamma = 1-1/Math.Sqrt(2);
    private const int Width = 11, NodeLedgerWidth = 8, EdgeLedgerWidth = 4, FaceLedgerWidth = 3;
    private readonly int _owner = Environment.CurrentManagedThreadId;
    private readonly Guid _session, _cell;
    private readonly double _epoch, _bath;
    private readonly AvatarCleftCompartment[] _nodes;
    private readonly AvatarCleftConnection[] _edges;
    private readonly AvatarCleftApexFace[] _faces;
    private readonly Dictionary<string,int> _indices;
    private readonly double[] _state, _stateError, _ledger, _ledgerError, _initial;
    private readonly string[] _labels, _ledgerLabels;
    private readonly string _initialFingerprint;
    private long _steps, _evaluations;
    private int _maximumIterations, _diagnosticStage, _diagnosticIteration;
    private double _maximumScaledResidual, _maximumChargeResidual, _maximumIonResidual;
    private double[] _diagnosticBase = [], _diagnosticCandidate = [];
    private double[]? _diagnosticResidual;
    private string _diagnosticPhase = "initial";
    private AvatarCleftHeldInput? _lastInput;
    private AvatarCleftDynamicsFailure? _failure;
    public AvatarVestibularCleftMembranes Kernel { get; }
    public AvatarCleftIntegrationSettings Settings { get; }
    public string Fingerprint { get; }

    public AvatarVestibularCleftDynamics(Guid sessionId, Guid cellId, AvatarVestibularCleftMembranes kernel,
        AvatarCleftIntegrationSettings settings, double hairMv, double perilymphMv,
        IReadOnlyList<AvatarCleftCompartmentState> states, IReadOnlyList<AvatarCleftGateState> gates, double epochMs = 0)
    {
        ArgumentNullException.ThrowIfNull(kernel); ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(states); ArgumentNullException.ThrowIfNull(gates); settings.Validate();
        CleftNumbers.Finite(hairMv); CleftNumbers.Finite(perilymphMv); CleftNumbers.Nonnegative(epochMs);
        if (sessionId == Guid.Empty || cellId == Guid.Empty || epochMs+settings.StepMs <= epochMs)
            throw new ArgumentException("Stable identity and a representable local clock are required.");
        _session=sessionId; _cell=cellId; _epoch=epochMs; _bath=perilymphMv; Kernel=kernel; Settings=settings;
        CleftNumbers.Finite(_bath+kernel.HairParameters.EndolymphPotentialMv);
        _nodes=kernel.Coupling.Spatial.Compartments.ToArray(); _edges=kernel.Coupling.Spatial.Connections.ToArray();
        _faces=kernel.Coupling.Faces.ToArray(); _indices=_nodes.Select((n,i)=>(n.Id,i)).ToDictionary(p=>p.Id,p=>p.i,StringComparer.Ordinal);
        if (states.Count != _nodes.Length || gates.Count != _nodes.Length) throw new ArgumentException("Complete identified initial state and actual gates required.");
        var named=states.ToDictionary(s=>s.Id,StringComparer.Ordinal); var namedGates=gates.ToDictionary(g=>g.Id,StringComparer.Ordinal);
        _state=new double[1+Width*_nodes.Length]; _state[0]=hairMv;
        var labels=new List<string> { "hairMv" };
        for (int i=0;i<_nodes.Length;i++)
        {
            string id=_nodes[i].Id;
            if (!named.TryGetValue(id,out var s) || !namedGates.TryGetValue(id,out var g)) throw new ArgumentException("Unknown initial compartment identity.");
            int o=Offset(i); _state[o]=s.CleftMv; _state[o+1]=s.CalyxMv; _state[o+2]=s.PotassiumMm; _state[o+3]=s.SodiumMm;
            _state[o+4]=g.Hair.PotassiumLow; _state[o+5]=g.Hair.Calcium; _state[o+6]=g.Hair.Hcn1;
            _state[o+7]=g.Calyx.InnerKv74; _state[o+8]=g.Calyx.InnerHcn2; _state[o+9]=g.Calyx.OuterKv74; _state[o+10]=g.Calyx.OuterHcn2;
            labels.AddRange(new[] { "cleftMv","calyxMv","potassiumMm","sodiumMm","hairPotassiumLow","hairCalcium","hairHcn1",
                "calyxInnerKv74","calyxInnerHcn2","calyxOuterKv74","calyxOuterHcn2" }.Select(n=>id+"/"+n));
        }
        ValidateState(_state); _initial=(double[])_state.Clone(); _stateError=new double[_state.Length]; _labels=labels.ToArray();
        var ledgerLabels=new List<string> { "metOutwardChargeFc","endolymphHoldingChargeFc","perilymphHoldingChargeFc" };
        foreach (var n in _nodes) ledgerLabels.AddRange(new[] { "hairToCleftChargeFc","calyxToCleftChargeFc","calyxToPerilymphChargeFc","calyxToAxonChargeFc",
            "hairPotassiumAttomoles","hairSodiumAttomoles","calyxPotassiumAttomoles","calyxSodiumAttomoles" }.Select(k=>"membrane/"+n.Id+"/"+k));
        foreach (var e in _edges) ledgerLabels.AddRange(new[] { "potassiumLeftToRightAttomoles","sodiumLeftToRightAttomoles","cleftLeftToRightChargeFc","calyxLeftToRightChargeFc" }.Select(k=>"edge/"+e.Id+"/"+k));
        foreach (var f in _faces) ledgerLabels.AddRange(new[] { "potassiumToBathAttomoles","sodiumToBathAttomoles","chargeToBathFc" }.Select(k=>"apex/"+f.Id+"/"+k));
        _ledgerLabels=ledgerLabels.ToArray(); _ledger=new double[_ledgerLabels.Length]; _ledgerError=new double[_ledger.Length];
        _initialFingerprint=CleftNumbers.Fingerprint(new { StateLabels=_labels, Values=_state });
        Fingerprint=CleftNumbers.Fingerprint(new { Version, kernel.Fingerprint, settings, epochMs, perilymphMv,
            InitialState=_initialFingerprint, Gamma, FiniteDifference="central 2^-20 relative, inward at state-domain boundaries",
            Forcing="explicit held bundle displacement and identified axon currents; fixed bath",
            Ledgers="SDIRK stage quadrature; compensated state and flux sums; no fictitious calcium/outer species" });
        // Construction establishes explicit state only; it neither settles nor
        // evaluates the biological model. A quiet interval is not equilibrium.
    }

    public AvatarCleftDynamicsState Capture()
    {
        RequireOwner(); var f=_failure;
        return new(_session,_cell,Version,Fingerprint,_initialFingerprint,_steps,_epoch,TimeAt(_steps),_bath,Settings,
            (string[])_labels.Clone(),(double[])_initial.Clone(),(double[])_state.Clone(),(double[])_stateError.Clone(),(string[])_ledgerLabels.Clone(),
            (double[])_ledger.Clone(),(double[])_ledgerError.Clone(),_maximumChargeResidual,_maximumIonResidual,
            _evaluations,_maximumIterations,_maximumScaledResidual,_lastInput is null ? null : _lastInput with {
                AxonCurrents=(AvatarCleftAxonCurrent[])_lastInput.AxonCurrents.Clone() },f is null ? null : f with {
                StageBase=(double[])f.StageBase.Clone(),Candidate=(double[])f.Candidate.Clone(),Residual=f.Residual is null ? null : (double[])f.Residual.Clone() });
    }

    public void AdvanceHeld(long stepCount, double bundleDisplacementNanometers, IReadOnlyList<AvatarCleftAxonCurrent> axonCurrents)
    {
        RequireOwner(); if (_failure is not null) throw new InvalidOperationException("Faulted coupled cell cannot silently restart.");
        CleftNumbers.Finite(bundleDisplacementNanometers); ArgumentNullException.ThrowIfNull(axonCurrents);
        if (stepCount < 0 || axonCurrents.Count != _nodes.Length) throw new ArgumentException("Nonnegative step count and every explicit axon port required.");
        var currents=new double[_nodes.Length]; var seen=new bool[_nodes.Length];
        foreach (var p in axonCurrents)
        {
            if (p.Id is null || !_indices.TryGetValue(p.Id,out int i) || seen[i]) throw new ArgumentException("Duplicate or unknown axon current identity.");
            CleftNumbers.Finite(p.OutgoingPa); currents[i]=p.OutgoingPa; seen[i]=true;
        }
        long end=checked(_steps+stepCount);
        if (stepCount>0 && (!double.IsFinite(TimeAt(end)) || TimeAt(end)<=TimeAt(end-1))) throw new ArgumentException("Unrepresentable clock endpoint.");
        if (stepCount==0) return;
        _lastInput=new(_steps,end,bundleDisplacementNanometers,_nodes.Select((n,i)=>new AvatarCleftAxonCurrent(n.Id,currents[i])).ToArray());
        while (_steps<end)
        {
            _diagnosticStage=0; _diagnosticIteration=0; _diagnosticBase=(double[])_state.Clone();
            _diagnosticCandidate=(double[])_state.Clone(); _diagnosticResidual=null; _diagnosticPhase="clock";
            try
            {
                if (TimeAt(_steps+1)<=TimeAt(_steps)) throw new ArithmeticException("Local clock lost resolution.");
                AdvanceOne(bundleDisplacementNanometers,currents); _steps++;
            }
            catch (Exception error)
            {
                _failure=new(error.ToString(),_diagnosticPhase,_diagnosticStage,_diagnosticIteration,(double[])_diagnosticBase.Clone(),
                    (double[])_diagnosticCandidate.Clone(),_diagnosticResidual is null ? null : (double[])_diagnosticResidual.Clone());
                throw;
            }
        }
    }

    private void AdvanceOne(double displacement, double[] axon)
    {
        double h=Settings.StepMs;
        _diagnosticStage=1;
        var first=SolveStage(_state,_state,h*Gamma,displacement,axon);
        var secondBase=new double[_state.Length];
        for (int i=0;i<secondBase.Length;i++) secondBase[i]=_state[i]+h*(1-Gamma)*first.Rates[i];
        _diagnosticStage=2;
        var second=SolveStage(secondBase,first.State,h*Gamma,displacement,axon);
        var next=new double[_state.Length]; var error=new double[next.Length];
        for (int i=0;i<next.Length;i++) Add(_state[i],_stateError[i],h*((1-Gamma)*first.Rates[i]+Gamma*second.Rates[i]),out next[i],out error[i]);
        _diagnosticStage=3; _diagnosticCandidate=next; _diagnosticResidual=null; _diagnosticPhase="endpoint";
        ValidateState(next);
        _diagnosticPhase="ledger";
        var a=LedgerRates(first.Evaluation); var b=LedgerRates(second.Evaluation);
        var ledger=new double[_ledger.Length]; var ledgerError=new double[ledger.Length];
        for (int i=0;i<ledger.Length;i++) Add(_ledger[i],_ledgerError[i],h*((1-Gamma)*a[i]+Gamma*b[i]),out ledger[i],out ledgerError[i]);
        var residual=Conservation(next,ledger);
        // Commit a complete step atomically. Failed stages/ledgers never replace
        // the last accepted physical state, flux sums, compensation or clock.
        next.CopyTo(_state,0); error.CopyTo(_stateError,0); ledger.CopyTo(_ledger,0); ledgerError.CopyTo(_ledgerError,0);
        _maximumChargeResidual=Math.Max(_maximumChargeResidual,residual.Charge);
        _maximumIonResidual=Math.Max(_maximumIonResidual,residual.Ion);
    }

    private sealed record Stage(double[] State, double[] Rates, AvatarVestibularCleftEvaluation Evaluation);
    private Stage SolveStage(double[] baseline, double[] guess, double scale, double displacement, double[] axon)
    {
        _diagnosticBase=(double[])baseline.Clone(); var state=(double[])guess.Clone();
        for (int iteration=0;iteration<=Settings.MaximumNewtonIterations;iteration++)
        {
            _diagnosticIteration=iteration; _diagnosticCandidate=state; _diagnosticResidual=null; _diagnosticPhase="residual";
            var evaluated=Derivative(state,displacement,axon); var residual=Residual(state,baseline,evaluated.Rates,scale);
            _diagnosticResidual=residual; double norm=Norm(residual,state,baseline);
            if (norm<=1)
            {
                _maximumIterations=Math.Max(_maximumIterations,iteration); _maximumScaledResidual=Math.Max(_maximumScaledResidual,norm);
                return new(state,evaluated.Rates,evaluated.Evaluation);
            }
            if (iteration==Settings.MaximumNewtonIterations) break;
            var jacobian=new double[state.Length,state.Length];
            for (int column=0;column<state.Length;column++)
            {
                double delta=Math.ScaleB(Math.Max(1,Math.Abs(state[column])),-20);
                var plus=(double[])state.Clone(); var minus=(double[])state.Clone();
                plus[column]+=delta; minus[column]-=delta;
                bool forward=Admissible(plus), backward=Admissible(minus);
                if (!forward && !backward) throw new ArithmeticException("No representable in-domain Jacobian perturbation.");
                _diagnosticPhase="jacobian-positive"; _diagnosticCandidate=plus; _diagnosticResidual=null;
                var fp=forward ? Derivative(plus,displacement,axon).Rates : evaluated.Rates;
                _diagnosticPhase="jacobian-negative"; _diagnosticCandidate=minus;
                var fm=backward ? Derivative(minus,displacement,axon).Rates : evaluated.Rates;
                double width=(forward ? plus[column] : state[column])-(backward ? minus[column] : state[column]);
                CleftNumbers.Positive(width);
                for (int row=0;row<state.Length;row++) jacobian[row,column]=(row==column ? 1 : 0)-scale*(fp[row]-fm[row])/width;
            }
            _diagnosticPhase="linear-solve"; _diagnosticCandidate=state; _diagnosticResidual=residual;
            var correction=LinearSolve(jacobian,residual.Select(x=>-x).ToArray()); bool accepted=false;
            for (int half=0;half<=Settings.MaximumLineSearchHalvings;half++)
            {
                double alpha=Math.ScaleB(1,-half); var trial=new double[state.Length];
                for (int i=0;i<trial.Length;i++) trial[i]=state[i]+alpha*correction[i];
                _diagnosticPhase="line-search"; _diagnosticCandidate=trial; _diagnosticResidual=null;
                if (!Admissible(trial)) continue;
                var candidate=Derivative(trial,displacement,axon); var r=Residual(trial,baseline,candidate.Rates,scale);
                _diagnosticResidual=r;
                if (Norm(r,trial,baseline)<norm) { state=trial; accepted=true; break; }
            }
            if (!accepted) throw new ArithmeticException("Coupled Newton line search failed; no state clipping or step substitution.");
        }
        throw new ArithmeticException("Coupled implicit stage did not converge within the declared nonlinear work bound.");
    }

    private (double[] Rates, AvatarVestibularCleftEvaluation Evaluation) Derivative(double[] state, double displacement, double[] axon)
    {
        ValidateState(state); _evaluations=checked(_evaluations+1);
        var states=new AvatarCleftCompartmentState[_nodes.Length]; var inputs=new AvatarCleftLocalMembraneInput[_nodes.Length];
        for (int i=0;i<_nodes.Length;i++)
        {
            int o=Offset(i); string id=_nodes[i].Id;
            states[i]=new(id,state[o],state[o+1],state[o+2],state[o+3]);
            inputs[i]=new(id,new(state[o+4],state[o+5],state[o+6]),new(state[o+7],state[o+8],state[o+9],state[o+10]),axon[i]);
        }
        var r=Kernel.Evaluate(state[0],_bath,displacement,states,inputs); var rates=new double[state.Length]; rates[0]=r.Coupling.Spatial.HairMvPerMs;
        for (int i=0;i<_nodes.Length;i++)
        {
            int o=Offset(i); var s=r.Coupling.Spatial.Compartments[i]; var m=r.Membranes[i];
            if (s.Id!=_nodes[i].Id || m.Id!=s.Id) throw new InvalidOperationException("Coupled derivative identity/order changed.");
            rates[o]=s.CleftMvPerMs; rates[o+1]=s.CalyxMvPerMs; rates[o+2]=s.PotassiumMmPerMs; rates[o+3]=s.SodiumMmPerMs;
            rates[o+4]=m.HairGateDerivatives.PotassiumLowPerMs; rates[o+5]=m.HairGateDerivatives.CalciumPerMs; rates[o+6]=m.HairGateDerivatives.Hcn1PerMs;
            rates[o+7]=m.CalyxGateDerivatives.InnerKv74PerMs; rates[o+8]=m.CalyxGateDerivatives.InnerHcn2PerMs;
            rates[o+9]=m.CalyxGateDerivatives.OuterKv74PerMs; rates[o+10]=m.CalyxGateDerivatives.OuterHcn2PerMs;
        }
        foreach (double value in rates) CleftNumbers.Finite(value);
        return (rates,r);
    }

    private double[] LedgerRates(AvatarVestibularCleftEvaluation r)
    {
        var values=new double[_ledger.Length]; values[0]=r.MetCurrentPa;
        values[1]=r.Coupling.EndolymphHoldingCurrentPa; values[2]=r.Coupling.Perilymph.HoldingCurrentPa;
        double f=AvatarCleftEdgeKernel.ChargePaPerAttomolePerMs;
        for (int i=0;i<_nodes.Length;i++)
        {
            int o=NodeLedger(i); var p=r.Membranes[i].Port;
            values[o]=p.HairToCleftPa; values[o+1]=p.CalyxToCleftPa; values[o+2]=p.CalyxToPerilymphPa; values[o+3]=p.CalyxToAxonPa;
            values[o+4]=p.HairPotassiumPa/f; values[o+5]=p.HairSodiumPa/f; values[o+6]=p.CalyxPotassiumPa/f; values[o+7]=p.CalyxSodiumPa/f;
        }
        for (int i=0;i<_edges.Length;i++)
        {
            int o=EdgeLedger(i); var e=r.Coupling.Spatial.Transfers[i]; if (e.Id!=_edges[i].Id) throw new InvalidOperationException("Edge identity changed.");
            values[o]=e.Cleft.PotassiumAttomolesPerMs; values[o+1]=e.Cleft.SodiumAttomolesPerMs;
            values[o+2]=e.Cleft.TotalCurrentPa; values[o+3]=e.CalyxCurrentLeftToRightPa;
        }
        for (int i=0;i<_faces.Length;i++)
        {
            int o=FaceLedger(i); var fce=r.Coupling.ApexTransfers[i]; if (fce.Id!=_faces[i].Id) throw new InvalidOperationException("Apex identity changed.");
            values[o]=fce.Outgoing.PotassiumAttomolesPerMs; values[o+1]=fce.Outgoing.SodiumAttomolesPerMs; values[o+2]=fce.Outgoing.TotalCurrentPa;
        }
        foreach (double value in values) CleftNumbers.Finite(value); return values;
    }

    private (double Charge, double Ion) Conservation(double[] next, double[] ledger)
    {
        var spatial=Kernel.Coupling.Spatial;
        // Work in voltage differences from the initial state, avoiding subtraction
        // of two large absolute capacitor charges (including the bath gauge).
        double dh=next[0]-_initial[0], qh=(spatial.HairApicalPf+spatial.HairPerilymphPf)*dh;
        double qe=-spatial.HairApicalPf*dh, qp=-spatial.HairPerilymphPf*dh;
        double expectedHair=-ledger[0], expectedBath=ledger[2];
        var cleft=new double[_nodes.Length]; var calyx=new double[_nodes.Length];
        var k=new double[_nodes.Length]; var na=new double[_nodes.Length];
        for (int i=0;i<_nodes.Length;i++)
        {
            int o=NodeLedger(i); expectedHair-=ledger[o]; expectedBath+=ledger[o+2];
            cleft[i]=ledger[o]+ledger[o+1]; calyx[i]=-ledger[o+1]-ledger[o+2]-ledger[o+3];
            k[i]=ledger[o+4]+ledger[o+6]; na[i]=ledger[o+5]+ledger[o+7];
        }
        for (int i=0;i<_edges.Length;i++)
        {
            int o=EdgeLedger(i), left=_indices[_edges[i].LeftId], right=_indices[_edges[i].RightId];
            cleft[left]-=ledger[o+2]; cleft[right]+=ledger[o+2]; calyx[left]-=ledger[o+3]; calyx[right]+=ledger[o+3];
            k[left]-=ledger[o]; k[right]+=ledger[o]; na[left]-=ledger[o+1]; na[right]+=ledger[o+1];
        }
        for (int i=0;i<_faces.Length;i++)
        {
            int o=FaceLedger(i), node=_indices[_faces[i].CompartmentId]; cleft[node]-=ledger[o+2]; expectedBath+=ledger[o+2];
            k[node]-=ledger[o]; na[node]-=ledger[o+1];
        }
        double charge=0, ion=0;
        for (int i=0;i<_nodes.Length;i++)
        {
            int o=Offset(i); var n=_nodes[i]; double ds=next[o]-_initial[o], dc=next[o+1]-_initial[o+1];
            double hair=n.HairCleftPf*(dh-ds), inner=n.CalyxInnerPf*(dc-ds), outer=n.CalyxOuterPf*dc;
            qh+=hair; qp-=outer; charge=Math.Max(charge,Math.Max(Math.Abs(-hair-inner-cleft[i]),Math.Abs(inner+outer-calyx[i])));
            ion=Math.Max(ion,Math.Max(Math.Abs(n.VolumeCubicMicrometers*(next[o+2]-_initial[o+2])-k[i]),
                Math.Abs(n.VolumeCubicMicrometers*(next[o+3]-_initial[o+3])-na[i])));
        }
        charge=Math.Max(charge,Math.Max(Math.Abs(qh-expectedHair),Math.Max(Math.Abs(qe-ledger[0]-ledger[1]),Math.Abs(qp-expectedBath))));
        CleftNumbers.Finite(charge); CleftNumbers.Finite(ion); return (charge,ion);
    }

    private double Norm(double[] residual, double[] state, double[] baseline)
    {
        double norm=0;
        for (int i=0;i<residual.Length;i++)
        {
            int kind=i==0 ? 0 : (i-1)%Width;
            double tolerance=i==0 || kind<2 ? Settings.VoltageAbsoluteToleranceMv : kind<4 ? Settings.IonAbsoluteToleranceMm : Settings.GateAbsoluteTolerance;
            double denominator=tolerance+Settings.RelativeTolerance*Math.Max(Math.Abs(state[i]),Math.Abs(baseline[i]));
            CleftNumbers.Positive(denominator); double value=Math.Abs(residual[i])/denominator; CleftNumbers.Finite(value); norm=Math.Max(norm,value);
        }
        return norm;
    }
    private static double[] Residual(double[] state, double[] baseline, double[] derivative, double scale)
    {
        var residual=new double[state.Length];
        for (int i=0;i<residual.Length;i++) { residual[i]=(state[i]-baseline[i])-scale*derivative[i]; CleftNumbers.Finite(residual[i]); }
        return residual;
    }
    private static double[] LinearSolve(double[,] matrix, double[] rhs)
    {
        int n=rhs.Length;
        for (int column=0;column<n;column++)
        {
            int pivot=column;
            for (int row=column+1;row<n;row++) if (Math.Abs(matrix[row,column])>Math.Abs(matrix[pivot,column])) pivot=row;
            if (!double.IsFinite(matrix[pivot,column]) || matrix[pivot,column]==0) throw new ArithmeticException("Singular/nonfinite coupled Newton matrix.");
            if (pivot!=column)
            {
                for (int j=column;j<n;j++) (matrix[column,j],matrix[pivot,j])=(matrix[pivot,j],matrix[column,j]);
                (rhs[column],rhs[pivot])=(rhs[pivot],rhs[column]);
            }
            for (int row=column+1;row<n;row++)
            {
                double factor=matrix[row,column]/matrix[column,column]; CleftNumbers.Finite(factor);
                for (int j=column+1;j<n;j++) matrix[row,j]-=factor*matrix[column,j];
                rhs[row]-=factor*rhs[column]; matrix[row,column]=0;
            }
        }
        var result=new double[n];
        for (int i=n-1;i>=0;i--)
        {
            double value=rhs[i]; for (int j=i+1;j<n;j++) value-=matrix[i,j]*result[j];
            result[i]=value/matrix[i,i]; CleftNumbers.Finite(result[i]);
        }
        return result;
    }
    private bool Admissible(double[] state)
    {
        if (state.Length!=_state.Length || state.Any(x=>!double.IsFinite(x))) return false;
        for (int i=0;i<_nodes.Length;i++)
        {
            int o=Offset(i); if (state[o+2]<=0 || state[o+3]<=0) return false;
            for (int j=4;j<Width;j++) if (state[o+j]<0 || state[o+j]>1) return false;
        }
        return true;
    }
    private void ValidateState(double[] state)
    {
        if (!Admissible(state)) throw new ArgumentException("Finite coupled state, positive ions and actual gates in [0,1] required; no clipping.");
    }
    private static void Add(double previous, double residual, double increment, out double next, out double error)
    {
        double delta=increment-residual; next=previous+delta; error=(next-previous)-delta;
        CleftNumbers.Finite(next); CleftNumbers.Finite(error);
    }
    private static int Offset(int i)=>1+Width*i;
    private static int NodeLedger(int i)=>3+NodeLedgerWidth*i;
    private int EdgeLedger(int i)=>3+NodeLedgerWidth*_nodes.Length+EdgeLedgerWidth*i;
    private int FaceLedger(int i)=>3+NodeLedgerWidth*_nodes.Length+EdgeLedgerWidth*_edges.Length+FaceLedgerWidth*i;
    private double TimeAt(long step)=>_epoch+Settings.StepMs*step;
    private void RequireOwner()
    {
        if (Environment.CurrentManagedThreadId!=_owner) throw new InvalidOperationException("One owner advances and captures the coupled cell and clock.");
    }
}

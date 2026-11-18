using NeuralResonanceEngine.Protocol;

namespace NRE.SimAvatar;

public sealed record AvatarAfferentObservationSite(string SourceInstanceKey,string SourceNeuronId,string CableNodeId,double ThresholdMv);
public sealed record AvatarAfferentObservedEvent(Guid MessageId,Guid SessionId,Guid CellId,string SourceInstanceKey,string SourceNeuronId,
    string CableNodeId,long Ordinal,long CompletedStep,string ObservationFingerprint,AvatarAfferentCrossingEstimate Crossing);
public sealed record AvatarAfferentObservationReceipt(long StartingStep,long CompletedStep,double BundleDisplacementNanometers,
    string PhysicalStateFingerprint,AvatarAfferentVoltageInterval Interval,AvatarAfferentObservedEvent? Event);
public sealed record AvatarAfferentObservationFailure(long StartingStep,long PhysicalCompletedStep,string Reason,
    string PhysicalStateFingerprint,AvatarAfferentVoltageInterval? AttemptedInterval);
public sealed record AvatarAfferentObserverState(string Version,string Fingerprint,Guid SessionId,Guid CellId,
    AvatarAfferentObservationSite Site,long ObservedThroughStep,long EventCount,long EndpointEvaluations,
    AvatarAfferentObservationReceipt? LastReceipt,AvatarAfferentObservationFailure? Failure);

// Isolated observation owner, not a DNNE transport publisher or live source.
// One output site belongs to one identified afferent, not one neuron per cable
// compartment. Numerical accuracy and physiological spike identity are separate.
public sealed class AvatarAfferentEventObserver
{
    public const string Version="isolated-afferent-completed-step-observer-v1";
    private readonly int _owner=Environment.CurrentManagedThreadId;
    private readonly AvatarVestibularAfferentDynamics _model;
    private readonly AvatarAfferentObservationSite _site;
    private readonly Guid _session,_cell;
    private readonly string _modelFingerprint;
    private readonly string[] _labels;
    private readonly Dictionary<string,int> _indices;
    private long _step,_ordinal,_evaluations;
    private AvatarAfferentObservationReceipt? _receipt;
    private AvatarAfferentObservationFailure? _failure;
    public string Fingerprint { get; }

    public AvatarAfferentEventObserver(AvatarVestibularAfferentDynamics model,AvatarAfferentObservationSite site)
    {
        ArgumentNullException.ThrowIfNull(model);ArgumentNullException.ThrowIfNull(site);
        if(string.IsNullOrWhiteSpace(site.SourceInstanceKey)||string.IsNullOrWhiteSpace(site.SourceNeuronId)||
            string.IsNullOrWhiteSpace(site.CableNodeId)||!double.IsFinite(site.ThresholdMv))
            throw new ArgumentException("Declare the actual afferent identity, observation node and voltage criterion.");
        var state=model.Capture();
        if(state.CompletedSteps!=0||state.Failure is not null)throw new ArgumentException("Attach at an explicit fresh epoch; omitted history cannot be invented.");
        if(!model.Kernel.Cable.Nodes.Any(n=>n.Id==site.CableNodeId))throw new ArgumentException("Observation node is absent from the actual cable.");
        _model=model;_site=site;_session=state.SessionId;_cell=state.CellId;_modelFingerprint=state.Fingerprint;
        _labels=(string[])state.StateLabels.Clone();_indices=_labels.Select((n,i)=>(n,i)).ToDictionary(x=>x.n,x=>x.i,StringComparer.Ordinal);
        Fingerprint=CleftNumbers.Fingerprint(new { Version,Crossing=AvatarAfferentCrossing.Version,state.Fingerprint,site,
            Ownership="one complete source step; actual endpoint derivatives; no reset, current injection, firing-rate encoding or refractory suppression" });
        // No kernel evaluation or advance during attachment.
    }

    public AvatarAfferentObserverState Capture()
    {
        RequireOwner();return new(Version,Fingerprint,_session,_cell,_site,_step,_ordinal,_evaluations,_receipt,_failure);
    }

    public AvatarAfferentObservationReceipt AdvanceNext(long expectedStartingStep,double bundleDisplacementNanometers)
    {
        RequireOwner();
        if(_failure is not null)throw new InvalidOperationException("Faulted observation cannot silently resume or reset.");
        CleftNumbers.Finite(bundleDisplacementNanometers);
        var before=_model.Capture();
        if(before.CompletedSteps!=_step||before.SessionId!=_session||before.CellId!=_cell||before.Fingerprint!=_modelFingerprint||
            before.Failure is not null||!before.StateLabels.SequenceEqual(_labels))
            throw new InvalidOperationException("Source advanced or changed outside its observation owner.");
        if(_receipt is not null && expectedStartingStep==_receipt.StartingStep)
        {
            if(bundleDisplacementNanometers!=_receipt.BundleDisplacementNanometers)throw new ArgumentException("Retry changed the held physical input.");
            return _receipt;
        }
        if(expectedStartingStep!=_step)throw new ArgumentException("Contiguous source steps required; no skipped or stale interval.");
        if(_ordinal==long.MaxValue)throw new InvalidOperationException("Event ordinal capacity exhausted before source advancement.");
        AvatarAfferentVoltageInterval? interval=null;
        try
        {
            // At a forcing boundary use the new held input at both sides of
            // THIS interval, not the previous interval's left-hand derivative.
            double startSlope=Slope(before,bundleDisplacementNanometers);
            _model.AdvanceHeld(1,bundleDisplacementNanometers);var after=_model.Capture();
            if(after.CompletedSteps!=checked(_step+1)||after.Failure is not null)throw new InvalidOperationException("One accepted source step required.");
            double endSlope=Slope(after,bundleDisplacementNanometers);
            interval=new(before.TimeMs,after.TimeMs,Voltage(before),Voltage(after),startSlope,endSlope);
            var crossing=AvatarAfferentCrossing.Observe(interval.Value,_site.ThresholdMv);
            AvatarAfferentObservedEvent? emitted=null;
            if(crossing is { } value)
            {
                long ordinal=checked(_ordinal+1);
                var id=StableNeuralIdentity.Create(Version,_session,_cell,_site.SourceInstanceKey,_site.SourceNeuronId,_site.CableNodeId,Fingerprint,ordinal);
                emitted=new(id,_session,_cell,_site.SourceInstanceKey,_site.SourceNeuronId,_site.CableNodeId,ordinal,after.CompletedSteps,Fingerprint,value);
            }
            var receipt=new AvatarAfferentObservationReceipt(_step,after.CompletedSteps,bundleDisplacementNanometers,
                AcceptedFingerprint(after),interval.Value,emitted);
            // The physical step is already accepted. Commit observation only
            // after its complete bracket/identity proof succeeds.
            _step=after.CompletedSteps;if(emitted is not null)_ordinal=emitted.Ordinal;_receipt=receipt;return receipt;
        }
        catch(Exception error)
        {
            var physical=_model.Capture();
            _failure=new(expectedStartingStep,physical.CompletedSteps,error.ToString(),AcceptedFingerprint(physical),interval);
            // Never roll back or erase the accepted physical prefix because
            // an observation was ambiguous or failed.
            throw;
        }
    }

    private double Voltage(AvatarAfferentDynamicsState state)=>state.Values[_indices["axon/"+_site.CableNodeId+"/potentialMv"]]-state.PerilymphMv;
    // A failed trial can contain nonfinite diagnostic candidates. Hash the
    // accepted prefix; the source retains its original failure separately.
    private static string AcceptedFingerprint(AvatarAfferentDynamicsState state)=>CleftNumbers.Fingerprint(state with { Failure=null });
    private double Slope(AvatarAfferentDynamicsState state,double displacement)
    {
        _evaluations=checked(_evaluations+1);
        double V(string name)=>state.Values[_indices[name]];
        var nodes=_model.Kernel.Sensory.Coupling.Spatial.Compartments;
        var compartments=nodes.Select(n=>new AvatarCleftCompartmentState(n.Id,V(n.Id+"/cleftMv"),V(n.Id+"/calyxMv"),V(n.Id+"/potassiumMm"),V(n.Id+"/sodiumMm"))).ToArray();
        var gates=nodes.Select(n=>new AvatarCleftGateState(n.Id,new(V(n.Id+"/hairPotassiumLow"),V(n.Id+"/hairCalcium"),V(n.Id+"/hairHcn1")),
            new(V(n.Id+"/calyxInnerKv74"),V(n.Id+"/calyxInnerHcn2"),V(n.Id+"/calyxOuterKv74"),V(n.Id+"/calyxOuterHcn2")))).ToArray();
        var cable=_model.Kernel.Cable.Nodes.Select(n=>new AvatarAfferentCableState(n.Id,V("axon/"+n.Id+"/potentialMv"),
            new(V("axon/"+n.Id+"/sodiumActivation"),V("axon/"+n.Id+"/sodiumInactivation"),V("axon/"+n.Id+"/kv1Activation"),
                V("axon/"+n.Id+"/kv7Activation"),V("axon/"+n.Id+"/kv34Activation"),V("axon/"+n.Id+"/kv34Inactivation")))).ToArray();
        var evaluated=_model.Kernel.Evaluate(V("hairMv"),state.PerilymphMv,displacement,compartments,gates,cable);
        return evaluated.Cable.Nodes.Single(n=>n.Id==_site.CableNodeId).PotentialMvPerMs;
    }
    private void RequireOwner()
    {
        if(Environment.CurrentManagedThreadId!=_owner)throw new InvalidOperationException("One owner observes each identified afferent epoch.");
    }
}

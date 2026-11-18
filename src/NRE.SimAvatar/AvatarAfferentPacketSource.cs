using NeuralResonanceEngine.Protocol;

namespace NRE.SimAvatar;

// Bounded observation outbox: both quiet and firing intervals carry progress.
// A sealed packet blocks further source advancement until its exact receipt is
// acknowledged. No network sender, rate encoding, live selector or model reset.
public sealed class AvatarAfferentPacketSource
{
    private readonly AvatarAfferentEventObserver _observer;
    private readonly int _owner=Environment.CurrentManagedThreadId,_maximumSteps;
    private readonly List<ObservedAfferentEvent> _events=[];
    private long _start,_through;
    private ObservedAfferentPacket? _sealed;
    private string? _lastAcknowledged;
    public ObservedAfferentSource Source=>_observer.Source;

    public AvatarAfferentPacketSource(AvatarAfferentEventObserver observer,int maximumSteps=4096)
    {
        ArgumentNullException.ThrowIfNull(observer);
        if(maximumSteps<1||maximumSteps>ObservedAfferentPacket.MaximumSteps)throw new ArgumentOutOfRangeException(nameof(maximumSteps));
        var state=observer.Capture();
        if(state.ObservedThroughStep!=0||state.EventCount!=0||state.Failure is not null)
            throw new ArgumentException("Attach the packet owner at a fresh observed epoch.");
        _observer=observer;_maximumSteps=maximumSteps;
    }
    public AvatarAfferentObservationReceipt AdvanceNext(long startingStep,double heldDisplacementNanometers)
    {
        RequireOwner();
        if(_sealed is not null)throw new InvalidOperationException("Acknowledge the exact sealed packet before source advancement.");
        if(_observer.Capture().ObservedThroughStep!=_through)throw new InvalidOperationException("Observer advanced outside its packet owner.");
        bool retry=_through>_start&&startingStep==_through-1;
        if(!retry&&(_through-_start>=_maximumSteps||_events.Count>=ObservedAfferentPacket.MaximumEvents))
            throw new InvalidOperationException("Observation outbox full; source was not advanced.");
        var receipt=_observer.AdvanceNext(startingStep,heldDisplacementNanometers);
        if(retry)return receipt;
        if(receipt.StartingStep!=_through||receipt.CompletedStep!=checked(_through+1))throw new InvalidOperationException("Observation skipped source time.");
        if(receipt.Event is { } e)_events.Add(new(e.MessageId,e.Ordinal,e.CompletedStep,e.Crossing.TimeMs));
        _through=receipt.CompletedStep;return receipt;
    }
    public ObservedAfferentPacket Seal()
    {
        RequireOwner();if(_sealed is not null)return _sealed;
        if(_through==_start)throw new InvalidOperationException("No completed observed interval to seal.");
        // A later physical/observation failure does not erase this valid prefix.
        return _sealed=new(Source,_start,_through,_events);
    }
    public void Acknowledge(string packetFingerprint)
    {
        RequireOwner();
        if(_sealed is null)
        {
            if(_lastAcknowledged is not null&&packetFingerprint==_lastAcknowledged)return;
            throw new ArgumentException("No matching pending packet.");
        }
        if(packetFingerprint!=_sealed.Fingerprint)throw new ArgumentException("Acknowledgement belongs to another packet.");
        _lastAcknowledged=packetFingerprint;_start=_through;_events.Clear();_sealed=null;
    }
    private void RequireOwner()
    {
        if(Environment.CurrentManagedThreadId!=_owner)throw new InvalidOperationException("One owner publishes each observed afferent epoch.");
    }
}

using NeuralResonanceEngine.Shared.Contracts;

namespace NRE.SimAvatar;

// Combined capture must read the body AND all declared contacts at the same
// endpoint, on the exclusive physical owner thread; no separate recapture.
// The producer must exclusively own stepping and observation on one thread.
// Implementations advance exactly one fixed physical step synchronously; they
// must expose the actual counter/duration, not a second invented clock.
public interface IAvatarPhysicalObservationBody
{
    long CompletedPhysicsStep { get; }
    double PhysicsStepMs { get; }
    void AdvanceOneStep();
    PhysicalObservationFrame CaptureObservation();
}

public sealed record PhysicalObservationProducerState(bool Initialized, bool Running, bool Faulted,
    string? FaultReason, long? ObservedPhysicsStep, long? CapturedThroughStep,
    int RetainedSamples, int RetainedObservationBytes, int UnsentSamples, int PendingBatches,
    long AcknowledgedThroughSequence);

// Isolated producer candidate; not selected by a live World/Unity caller.
// Capacity is checked BEFORE invoking physics. After physics begins, any failure
// faults the epoch: no rollback, later-frame substitution or silent step skip.
// Retained valid samples can still be drained after a producer fault. There is
// no crash journal and no inference that samples constitute afferent firings.
public sealed class AvatarPhysicalObservationProducer
{
    private readonly PhysicalSampleClock _clock;
    private readonly IAvatarPhysicalObservationBody _body;
    private readonly AvatarPhysicalObservationOutbox _outbox;
    private readonly int _ownerThread = Environment.CurrentManagedThreadId;
    private bool _busy, _initialized, _running, _faulted;
    private string? _faultReason;
    private long? _observedStep;

    public AvatarPhysicalObservationProducer(PhysicalObservationLayout layout, IAvatarPhysicalObservationBody body,
        int maximumSamples = 1024, int maximumObservationBytes = 16 * 1024 * 1024,
        int maximumBatches = 32, int retainedReceipts = 64)
    {
        ArgumentNullException.ThrowIfNull(body);
        _outbox = new(layout, maximumSamples, maximumObservationBytes, maximumBatches, retainedReceipts);
        _clock = layout.Clock; _body = body;
    }

    public PhysicalObservationProducerState State => Exclusive(() => new PhysicalObservationProducerState(_initialized, _running && !Faulted,
        Faulted, _faultReason, _observedStep, _outbox.CapturedThroughStep,
        _outbox.RetainedSamples, _outbox.RetainedObservationBytes, _outbox.UnsentSamples,
        _outbox.PendingBatches, _outbox.AcknowledgedThroughSequence), allowDrain: true);

    private bool Faulted => _faulted || _outbox.Faulted;

    public PhysicalObservationSample CaptureInitial() => Exclusive(() =>
    {
        if (_initialized) throw new InvalidOperationException("Initial observation already retained; use its original sample.");
        try
        {
            var sample = CaptureAt(_clock.InitialPhysicsStep);
            _initialized = true;
            return sample;
        }
        catch (Exception e) { Fault("Initial capture", e); throw; }
    });

    public void Resume() => Exclusive(() =>
    {
        if (!_initialized) throw new InvalidOperationException("Capture the unevolved initial body before enabling steps.");
        try { CheckBody(_outbox.CapturedThroughStep!.Value); _running = true; }
        catch (Exception e) { Fault("Resume", e); throw; }
        return true;
    });

    public void Pause() => Exclusive(() => { _running = false; return true; }, allowDrain: true);

    public PhysicalSampleAdvance TryAdvance() => Exclusive(() =>
    {
        if (!_running) return PhysicalSampleAdvance.Paused;
        string phase = "Before physics";
        try
        {
            long previous = _outbox.CapturedThroughStep!.Value;
            CheckBody(previous);
            long next = checked(previous + 1);
            _ = _clock.AtStep(next); // Distinguish exhausted time from a drainable full buffer.
            if (!_outbox.CanCaptureMaximumObservation) return PhysicalSampleAdvance.Backpressured;
            phase = "Physics advance";
            _body.AdvanceOneStep();
            phase = "Completed-step capture";
            _ = CaptureAt(next);
            return PhysicalSampleAdvance.Advanced;
        }
        catch (Exception e) { Fault(phase, e); throw; }
    });

    // Publishing/acknowledging never steps or recaptures the physical body.
    public PhysicalObservationBatch Publish(int maximumSamples = PhysicalObservationProtocol.MaximumBatchSamples) =>
        Exclusive(() => _outbox.Publish(maximumSamples), allowDrain: true);

    public IReadOnlyList<PhysicalObservationBatch> Unacknowledged() =>
        Exclusive(() => _outbox.Unacknowledged(), allowDrain: true);

    public void Acknowledge(PhysicalObservationReceipt receipt) => Exclusive(() =>
    {
        _outbox.Acknowledge(receipt);
        return true;
    }, allowDrain: true);

    private PhysicalObservationSample CaptureAt(long step)
    {
        CheckBody(step);
        var observation = _body.CaptureObservation();
        CheckBody(step); // A capture callback may not advance or reconfigure physics.
        return _outbox.Capture(step, observation);
    }

    private void CheckBody(long expectedStep)
    {
        _observedStep = _body.CompletedPhysicsStep;
        if (_observedStep != expectedStep || _body.PhysicsStepMs != _clock.PhysicsStepMs)
            throw new InvalidOperationException("Physical step/count differs from the declared source clock; this epoch cannot continue.");
    }

    private void Fault(string phase, Exception exception)
    {
        _faulted = true; _running = false;
        _faultReason = phase + ": " + exception.GetType().Name + ": " + exception.Message;
    }

    private T Exclusive<T>(Func<T> operation, bool allowDrain = false)
    {
        if (Environment.CurrentManagedThreadId != _ownerThread)
            throw new InvalidOperationException("Physical producer operations belong to their original owner thread.");
        if (_busy) throw new InvalidOperationException("Physical producer callbacks cannot reenter its operations.");
        if (!allowDrain && Faulted) throw new InvalidOperationException("Faulted physical producer cannot continue this epoch.");
        _busy = true;
        try { return operation(); }
        finally { _busy = false; }
    }
}

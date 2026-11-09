using NeuralResonanceEngine.Shared.Contracts;

namespace NRE.SimAvatar;

// Single-owner isolated source. Capture immediately at each declared physical
// step, starting with the initial state. Before advancing physics, check enough
// capacity via CanCaptureMaximumObservation. Do not run physics then discard its
// measurement when a queue is full. This class does not drive or rewind physics.
public sealed class AvatarPhysicalSampleOutbox
{
    private sealed record Retained(PhysicalSampleBatch Batch, PhysicalSampleReceipt Receipt, int Bytes);
    private readonly PhysicalSampleClock _clock;
    private readonly string _fingerprint;
    private readonly int _maximumSamples, _maximumBytes, _maximumBatches, _retainedReceipts;
    private readonly Queue<PhysicalBodySample> _unsent = new();
    private readonly SortedDictionary<long, Retained> _pending = new();
    private readonly Dictionary<long, PhysicalSampleReceipt> _acknowledged = new();
    private long _nextStep, _sequence;
    public int RetainedSamples { get; private set; }
    public int RetainedObservationBytes { get; private set; }
    public int UnsentSamples => _unsent.Count;
    public int PendingBatches => _pending.Count;
    public long? CapturedThroughStep { get; private set; }
    public long AcknowledgedThroughSequence { get; private set; }
    public bool Faulted { get; private set; }
    public bool CanCaptureMaximumObservation
    {
        get
        {
            if (Faulted || _nextStep > PhysicalSampleClock.MaximumExactStep || RetainedSamples >= _maximumSamples ||
                PhysicalSampleProtocol.MaximumObservationBytes > _maximumBytes - RetainedObservationBytes) return false;
            try { _ = _clock.AtStep(_nextStep); return true; }
            catch (ArgumentOutOfRangeException) { return false; }
        }
    }

    public AvatarPhysicalSampleOutbox(PhysicalSampleClock clock, int maximumSamples = 1024,
        int maximumObservationBytes = 16 * 1024 * 1024, int maximumBatches = 32, int retainedReceipts = 64)
    {
        ArgumentNullException.ThrowIfNull(clock); clock.Validate();
        if (maximumSamples <= 0 || maximumObservationBytes < PhysicalSampleProtocol.MaximumObservationBytes ||
            maximumBatches <= 0 || retainedReceipts <= 0) throw new ArgumentException("Declare positive bounded source capacities.");
        _clock = clock; _fingerprint = clock.Fingerprint; _nextStep = clock.InitialPhysicsStep;
        _maximumSamples = maximumSamples; _maximumBytes = maximumObservationBytes;
        _maximumBatches = maximumBatches; _retainedReceipts = retainedReceipts;
    }

    public PhysicalBodySample Capture(long step, PhysicalBodyFrameRequest observation)
    {
        RequireOperational();
        if (step != _nextStep) throw new ArgumentException("Missing, repeated or reversed physical step; no state reset.");
        if (!CanCaptureMaximumObservation) throw new InvalidOperationException("Source buffer full; hold physics before its next step.");
        var sample = PhysicalSampleProtocol.Capture(_clock, step, observation);
        int bytes = PhysicalSampleProtocol.ObservationBytes(sample);
        try
        {
            _unsent.Enqueue(sample); ++RetainedSamples; RetainedObservationBytes += bytes;
            CapturedThroughStep = step; _nextStep = checked(step + 1);
            return sample;
        }
        catch { Faulted = true; throw; }
    }

    public PhysicalSampleBatch Publish(int maximumSamples = PhysicalSampleProtocol.MaximumBatchSamples)
    {
        RequireOperational();
        if (maximumSamples <= 0 || maximumSamples > PhysicalSampleProtocol.MaximumBatchSamples)
            throw new ArgumentOutOfRangeException(nameof(maximumSamples));
        if (_unsent.Count == 0) throw new InvalidOperationException("No captured samples; an empty packet cannot assert physical completeness.");
        if (_pending.Count >= _maximumBatches) throw new InvalidOperationException("Unacknowledged batch capacity exhausted; retry existing packets.");
        var samples = Array.AsReadOnly(_unsent.Take(maximumSamples).ToArray());
        var batch = new PhysicalSampleBatch(PhysicalSampleProtocol.Version, _clock.SessionId, _clock.SourceId,
            _fingerprint, checked(_sequence + 1), samples[^1].PhysicsStep, samples);
        var receipt = PhysicalSampleProtocol.Receipt(batch);
        int bytes = samples.Sum(PhysicalSampleProtocol.ObservationBytes);
        try
        {
            _pending.Add(batch.Sequence, new(batch, receipt, bytes));
            for (int i = 0; i < samples.Count; ++i) _unsent.Dequeue();
            _sequence = batch.Sequence;
            return batch;
        }
        catch { Faulted = true; throw; }
    }

    public IReadOnlyList<PhysicalSampleBatch> Unacknowledged() => Array.AsReadOnly(_pending.Values.Select(x => x.Batch).ToArray());

    public void Acknowledge(PhysicalSampleReceipt receipt)
    {
        RequireOperational(); ArgumentNullException.ThrowIfNull(receipt);
        if (receipt.Version != PhysicalSampleProtocol.Version || receipt.SessionId != _clock.SessionId ||
            receipt.SourceId != _clock.SourceId || receipt.ClockFingerprint != _fingerprint || receipt.Sequence <= 0)
            throw new ArgumentException("Receipt belongs to another declared stream.");
        if (receipt.Sequence <= AcknowledgedThroughSequence)
        {
            if (!_acknowledged.TryGetValue(receipt.Sequence, out var prior) || prior != receipt)
                throw new ArgumentException("Conflicting or expired receipt; no silent epoch reset.");
            return;
        }
        if (!_pending.TryGetValue(receipt.Sequence, out var retained) || receipt != retained.Receipt)
            throw new ArgumentException("Receipt does not match retained original bytes.");
        // Receiver requires contiguous acceptance. A matching later receipt can
        // acknowledge earlier packets, but never claims receptor consumption.
        var released = _pending.Where(x => x.Key <= receipt.Sequence).ToArray();
        try
        {
            foreach (var entry in released)
            {
                _acknowledged.Add(entry.Key, entry.Value.Receipt);
                _pending.Remove(entry.Key); RetainedSamples -= entry.Value.Batch.Samples.Count;
                RetainedObservationBytes -= entry.Value.Bytes;
            }
            AcknowledgedThroughSequence = receipt.Sequence;
            foreach (long key in _acknowledged.Keys.Where(k => k <= receipt.Sequence - _retainedReceipts).ToArray()) _acknowledged.Remove(key);
        }
        catch { Faulted = true; throw; }
    }

    private void RequireOperational()
    {
        if (Faulted) throw new InvalidOperationException("Faulted physical source cannot continue this epoch.");
    }
}

// In-memory sample receiver; no receptor integration or neuronal event invention.
// Consume original fixed-grid samples in order. Batching is not interpolation.
public sealed class AvatarPhysicalSampleReceiver
{
    private readonly PhysicalSampleClock _clock;
    private readonly string _fingerprint;
    private readonly int _maximumSamples, _maximumBytes, _retainedBatches;
    private readonly Queue<PhysicalBodySample> _pending = new();
    private readonly Dictionary<long, PhysicalSampleReceipt> _receipts = new();
    private long _sequence, _nextStep;
    public int PendingSamples => _pending.Count;
    public int PendingObservationBytes { get; private set; }
    public long AcceptedSamples { get; private set; }
    public long ConsumedSamples { get; private set; }
    public long? SampleCompleteThroughStep { get; private set; }
    public long? ConsumedThroughStep { get; private set; }
    public bool Faulted { get; private set; }

    public AvatarPhysicalSampleReceiver(PhysicalSampleClock clock, int maximumSamples = 1024,
        int maximumObservationBytes = 16 * 1024 * 1024, int retainedBatches = 64)
    {
        ArgumentNullException.ThrowIfNull(clock); clock.Validate();
        if (maximumSamples <= 0 || maximumObservationBytes < PhysicalSampleProtocol.MaximumObservationBytes || retainedBatches <= 0)
            throw new ArgumentException("Declare positive bounded receiver capacities.");
        _clock = clock; _fingerprint = clock.Fingerprint; _nextStep = clock.InitialPhysicsStep;
        _maximumSamples = maximumSamples; _maximumBytes = maximumObservationBytes; _retainedBatches = retainedBatches;
    }

    public PhysicalSampleReceipt Accept(PhysicalSampleBatch batch)
    {
        RequireOperational(); ArgumentNullException.ThrowIfNull(batch);
        if (batch.Version != PhysicalSampleProtocol.Version || batch.SessionId != _clock.SessionId || batch.SourceId != _clock.SourceId ||
            batch.ClockFingerprint != _fingerprint || batch.Sequence <= 0 || batch.Samples is null ||
            batch.Samples.Count is <= 0 or > PhysicalSampleProtocol.MaximumBatchSamples)
            throw new ArgumentException("Wrong stream/protocol or missing physical samples.");
        var captured = batch with { Samples = Array.AsReadOnly(batch.Samples.ToArray()) };
        int bytes = 0;
        foreach (var sample in captured.Samples)
        {
            if (sample is null || string.IsNullOrEmpty(sample.ObservationJson)) throw new ArgumentException("Missing physical payload.");
            int size = PhysicalSampleProtocol.ObservationBytes(sample);
            if (size > PhysicalSampleProtocol.MaximumObservationBytes) throw new ArgumentException("Physical payload exceeds its limit.");
            bytes = checked(bytes + size);
            if (bytes > _maximumBytes) throw new InvalidOperationException("Physical packet exceeds receiver byte capacity.");
        }
        var receipt = PhysicalSampleProtocol.Receipt(captured);
        if (batch.Sequence <= _sequence)
        {
            if (!_receipts.TryGetValue(batch.Sequence, out var previous) || previous != receipt)
                throw new ArgumentException("Conflicting or expired physical packet replay.");
            return previous;
        }
        if (batch.Sequence != checked(_sequence + 1)) throw new ArgumentException("Missing packet; recover it before asserting completeness.");
        if (captured.Samples.Count > _maximumSamples - PendingSamples)
            throw new InvalidOperationException("Physical receiver full; no truncation or false acknowledgement.");
        for (int i = 0; i < captured.Samples.Count; ++i)
        {
            var sample = captured.Samples[i];
            if (sample.PhysicsStep != checked(_nextStep + i)) throw new ArgumentException("Physical grid has a gap, repeated step or reversal.");
            _ = PhysicalSampleProtocol.Decode(_clock, sample);
        }
        if (batch.CompleteThroughStep != captured.Samples[^1].PhysicsStep)
            throw new ArgumentException("Completeness exceeds or disagrees with captured physical samples.");
        if (bytes > _maximumBytes - PendingObservationBytes)
            throw new InvalidOperationException("Physical receiver byte capacity exhausted; retain original packet.");
        try
        {
            foreach (var sample in captured.Samples) _pending.Enqueue(sample);
            PendingObservationBytes += bytes; AcceptedSamples += captured.Samples.Count;
            _sequence = batch.Sequence; SampleCompleteThroughStep = batch.CompleteThroughStep;
            _nextStep = checked(batch.CompleteThroughStep + 1);
            _receipts.Add(batch.Sequence, receipt);
            if (_receipts.Count > _retainedBatches) _receipts.Remove(batch.Sequence - _retainedBatches);
            return receipt;
        }
        catch { Faulted = true; throw; }
    }

    public IReadOnlyList<PhysicalBodySample> ConsumeThroughStep(long step)
    {
        RequireOperational(); _ = _clock.AtStep(step);
        if (SampleCompleteThroughStep is null || step > SampleCompleteThroughStep || (ConsumedThroughStep is { } last && step < last))
            throw new ArgumentOutOfRangeException(nameof(step), "Wait for captured samples; never infer silence or rewind.");
        var samples = _pending.TakeWhile(s => s.PhysicsStep <= step).ToArray();
        try
        {
            foreach (var sample in samples) { _pending.Dequeue(); PendingObservationBytes -= PhysicalSampleProtocol.ObservationBytes(sample); }
            ConsumedSamples += samples.Length; ConsumedThroughStep = step;
            return Array.AsReadOnly(samples);
        }
        catch { Faulted = true; throw; }
    }

    private void RequireOperational()
    {
        if (Faulted) throw new InvalidOperationException("Faulted physical receiver cannot continue this epoch.");
    }
}

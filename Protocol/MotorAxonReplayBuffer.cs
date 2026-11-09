using System.Security.Cryptography;
using System.Text.Json;

namespace NeuralResonanceEngine.Protocol;

// Receipt of an intact accepted batch, not muscle delivery, durable storage,
// physiological validity or authentication of its source.
public sealed record MotorAxonAcknowledgement(int Version,Guid SessionId,string SourceInstanceKey,
    string AnatomyFingerprint,long Sequence,string BatchSha256);

public static class MotorAxonBatchIntegrity
{
    public static string Digest(MotorAxonBatch batch)=>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(batch)));

    public static MotorAxonAcknowledgement Receipt(MotorAxonBatch batch,string digest)=>
        new(batch.Version,batch.SessionId,batch.SourceInstanceKey,batch.AnatomyFingerprint,batch.Sequence,digest);
}

// Isolated, single-owner, IN-MEMORY source buffer. The caller must serialize
// access and feed original motor emissions, before any lossy dispatch cap.
// Restart/durable replay requires a separate joint checkpoint or journal; this
// type deliberately does not recreate a session or neural state after a crash.
public sealed class MotorAxonReplayBuffer
{
    private sealed record Retained(MotorAxonBatch Batch,MotorAxonAcknowledgement Receipt);
    private readonly MotorAxonPublisher _publisher;
    private readonly MotorAxonAnatomy _anatomy;
    private readonly MotorAxonClock _clock;
    private readonly string _instance;
    private readonly int _maximumBatches,_maximumEvents,_retainedAcknowledgements;
    private readonly SortedDictionary<long,Retained> _pending=new();
    private readonly Dictionary<long,MotorAxonAcknowledgement> _acknowledged=new();
    public long AcknowledgedThroughSequence { get; private set; }
    public bool Faulted { get; private set; }
    public int PendingBatches=>_pending.Count;
    public int PendingEvents { get; private set; }

    public MotorAxonReplayBuffer(MotorAxonAnatomy anatomy,MotorAxonClock clock,string instance,
        int maximumBatches=32,int maximumEvents=65_536,int retainedAcknowledgements=64,int maximumEpochEvents=1_000_000)
    {
        if(maximumBatches<=0||maximumEvents<=0||retainedAcknowledgements<=0)
            throw new ArgumentOutOfRangeException(nameof(maximumBatches));
        _publisher=new(anatomy,clock,instance,maximumEpochEvents);_anatomy=anatomy;_clock=clock;_instance=instance;
        _maximumBatches=maximumBatches;_maximumEvents=maximumEvents;_retainedAcknowledgements=retainedAcknowledgements;
    }

    public MotorAxonBatch Publish(IReadOnlyList<SpikeMessage> emitted,double completeThroughNeuralMs)
    {
        RequireOperational();
        ArgumentNullException.ThrowIfNull(emitted);
        // Check capacity before the publisher commits sequence, ordinal, IDs or
        // completeness. The caller must retain these emissions and halt source
        // advancement until capacity returns; never generate replacements.
        if(_pending.Count>=_maximumBatches||emitted.Count>_maximumEvents-PendingEvents)
            throw new InvalidOperationException("Unacknowledged motor buffer full; publication rejected before source progress.");
        var batch=_publisher.Publish(emitted,completeThroughNeuralMs);
        try
        {
            _pending.Add(batch.Sequence,new(batch,MotorAxonBatchIntegrity.Receipt(batch,MotorAxonBatchIntegrity.Digest(batch))));
            PendingEvents+=batch.Spikes.Count;
            return batch;
        }
        catch {Faulted=true;throw;} // A retention failure after publication cannot be resumed as though nothing happened.
    }

    // Retry these exact snapshots in sequence order. Do not republish their
    // events through Publish, which would assign a different batch/ordinal.
    public IReadOnlyList<MotorAxonBatch> Unacknowledged()=>
        Array.AsReadOnly(_pending.Values.Select(x=>x.Batch).ToArray());

    public void Acknowledge(MotorAxonAcknowledgement receipt)
    {
        RequireOperational();
        ArgumentNullException.ThrowIfNull(receipt);
        if(receipt.Version!=MotorAxonAnatomy.ProtocolVersion||receipt.SessionId!=_clock.SessionId||
            receipt.SourceInstanceKey!=_instance||receipt.AnatomyFingerprint!=_anatomy.Fingerprint||receipt.Sequence<=0)
            throw new ArgumentException("Receipt belongs to another protocol, session, anatomy or source.");
        if(receipt.Sequence<=AcknowledgedThroughSequence)
        {
            if(!_acknowledged.TryGetValue(receipt.Sequence,out var prior)||receipt!=prior)
                throw new ArgumentException("Conflicting or expired motor acknowledgement.");
            return;
        }
        if(!_pending.TryGetValue(receipt.Sequence,out var retained)||receipt!=retained.Receipt)
            throw new ArgumentException("Receipt does not match a retained batch; no source progress inferred.");
        // Receiver acceptance is contiguous, so an authentic receipt for this
        // sequence also acknowledges its preceding batches. It does not assert
        // that their delayed arrivals have been consumed by a muscle.
        var released=_pending.Where(x=>x.Key<=receipt.Sequence).ToArray();
        foreach(var entry in released)
        {
            _pending.Remove(entry.Key);PendingEvents-=entry.Value.Batch.Spikes.Count;
            _acknowledged.Add(entry.Key,entry.Value.Receipt);
        }
        AcknowledgedThroughSequence=receipt.Sequence;
        foreach(long sequence in _acknowledged.Keys.Where(k=>k<=AcknowledgedThroughSequence-_retainedAcknowledgements).ToArray())
            _acknowledged.Remove(sequence);
    }

    private void RequireOperational()
    {
        if(Faulted)throw new InvalidOperationException("Faulted motor replay buffer cannot continue this session.");
    }
}

#nullable enable
using System.Text.Json;
using NeuralResonanceEngine.Shared.Contracts;

internal sealed record RecipientNucleusProjectionStatus(VestibularProjectionStatus Outgoing,
    VestibularRecipientStatus Source,string? Failure,VestibularRecipientFrame? Original);

// Binds an outgoing owner to the actual retained RF/spinal integration results.
// Fabricated step records cannot enter through this adapter.
internal sealed class RecipientNucleusProjection
{
    private readonly VestibularProjectionReceiver _source;
    private readonly VestibularProjectionOutbox _outbox;
    private readonly int _owner=Environment.CurrentManagedThreadId;
    private VestibularRecipientFrame? _original;
    private string? _failure;
    internal string Fingerprint=>_outbox.Fingerprint;

    internal RecipientNucleusProjection(VestibularProjectionReceiver source,string hemisphere,
        IReadOnlyList<SynapticConnection> routes,IReadOnlyList<ServiceInstance> recipients)
    {
        ArgumentNullException.ThrowIfNull(source);var state=source.Capture();
        if(state.CompletedTicks!=0||state.Pending is not null||state.Failure is not null||state.Sources.Any(s=>s.Sequence!=0))
            throw new ArgumentException("Attach before original neuronal integration or input admission.");
        _source=source;_outbox=new(source.Instance,hemisphere,source.EpochMs,source.StepMs,routes,recipients,source.Structure);
    }
    internal VestibularProjectionFrame PublishCompleted()
    {
        Healthy();
        if(_original is not null){CheckOriginalHeld();return _outbox.Capture().Pending??throw new InvalidOperationException("Retain failed staging evidence.");}
        _original=_source.PublishCompleted();
        try
        {
            CheckOriginalHeld();using var doc=JsonDocument.Parse(_original.CanonicalJson);var root=doc.RootElement;
            if(VestibularProjectionOutbox.Hash(_original.CanonicalJson)!=_original.Sha256||
                root.GetProperty("Version").GetString()!=VestibularProjectionReceiver.Version||
                root.GetProperty("Fingerprint").GetString()!=_source.Fingerprint||
                root.GetProperty("SourceStructure").GetInt32()!=(int)_source.Structure||
                root.GetProperty("SourceInstance").GetString()!=_source.Instance||root.GetProperty("Failure").ValueKind!=JsonValueKind.Null)
                throw new InvalidOperationException("Original neuronal ownership or identity changed.");
            return _outbox.Stage(_original.Sequence,_original.CanonicalJson,_original.Sha256,_original.CompletedTicks,
                _original.CompletedThroughMs,root.GetProperty("ReceiverStepJson").EnumerateArray().Select(e=>e.GetString()!).ToArray());
        }
        catch(Exception e){_failure=e.ToString();throw;}
    }
    internal void Accept(VestibularProjectionReceipt receipt){Healthy();CheckOriginalHeld();_outbox.Accept(receipt);}
    internal void ReleaseSource(string frameSha256)
    {
        Healthy();if(_original is null&&_outbox.Capture().LastReleasedSha256 is {} released&&released==frameSha256)return;
        CheckOriginalHeld();_outbox.RequireAllReceipts(frameSha256);
        try{_source.Acknowledge(_original!.Sha256);_outbox.Release(frameSha256);_original=null;}
        catch(Exception e){_failure=e.ToString();throw;}
    }
    internal RecipientNucleusProjectionStatus Capture(){Owner();return new(_outbox.Capture(),_source.Capture(),_failure,_original);}
    private void CheckOriginalHeld()
    {
        var state=_source.Capture();
        if(_original is null||!ReferenceEquals(state.Pending,_original)||state.Failure is not null)
            throw new InvalidOperationException("The original neuronal output was released or changed outside its outgoing owner.");
    }
    private void Owner(){if(Environment.CurrentManagedThreadId!=_owner)throw new InvalidOperationException("Use the neuronal owner's thread.");}
    private void Healthy(){Owner();if(_failure is not null)throw new InvalidOperationException("Retain failed source and projection; no replay or reset.");}
}

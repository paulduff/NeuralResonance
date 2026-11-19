#nullable enable
using System.Text.Json;
using NeuralResonanceEngine.Shared.Contracts;

internal sealed record CanalNucleusProjectionStatus(VestibularProjectionStatus Outgoing,
    CanalConvergenceStatus Source,string? Failure,string? AttemptedOriginalFrameJson);

// Attaches before the existing shared source starts. It releases that source
// only after all declared recipients acknowledge their exact original packets.
// Caller retains the immutable output/receipts; no network or target is invented.
internal sealed class CanalNucleusProjection
{
    private readonly CanalConvergenceConnection _source;
    private readonly VestibularProjectionOutbox _outbox;
    private readonly int _owner=Environment.CurrentManagedThreadId;
    private CanalConvergenceFrame? _original;
    private string? _failure;
    internal CanalNucleusProjection(CanalConvergenceConnection source,string instance,string hemisphere,double receiverStepMs,
        IReadOnlyList<SynapticConnection> routes,IReadOnlyList<ServiceInstance> recipients)
    {
        ArgumentNullException.ThrowIfNull(source);var state=source.Capture();
        if(state.ReceiverTicks!=0||state.Pending is not null||state.Failure is not null||
            state.Sources.Any(s=>s.CompletedSteps!=0||s.AcknowledgedSteps!=0))
            throw new ArgumentException("Attach the outgoing owner before any source advances; no lost preceding output.");
        _source=source;_outbox=new(instance,hemisphere,state.ReceiverThroughMs,receiverStepMs,routes,recipients);
    }
    internal VestibularProjectionFrame PublishCompleted()
    {
        Healthy();
        if(_original is not null){CheckOriginalHeld();return _outbox.Capture().Pending??throw new InvalidOperationException("Preserve failed staging evidence.");}
        // A normal 'no completed work' rejection has no partial output to fault.
        var original=_source.PublishCompleted();_original=original;
        try
        {
            CheckOriginalHeld();using var doc=JsonDocument.Parse(original.CanonicalJson);var root=doc.RootElement;
            if(root.GetProperty("Version").GetString()!=CanalConvergenceConnection.Version||
                root.GetProperty("Fingerprint").GetString()!=_source.Fingerprint||root.GetProperty("Failure").ValueKind!=JsonValueKind.Null)
                throw new InvalidOperationException("The original receiving owner or failure state changed.");
            return _outbox.Stage(original.Sequence,original.CanonicalJson,original.Sha256,
                root.GetProperty("ReceiverTicks").GetInt64(),root.GetProperty("ReceiverThroughMs").GetDouble(),
                root.GetProperty("ReceiverStepJson").EnumerateArray().Select(e=>e.GetString()!).ToArray());
        }
        catch(Exception e){_failure=e.ToString();throw;}
    }
    internal void Accept(VestibularProjectionReceipt receipt){Healthy();CheckOriginalHeld();_outbox.Accept(receipt);}
    internal void ReleaseSource(string frameSha256)
    {
        Healthy();
        if(_original is null&&_outbox.Capture().LastReleasedSha256 is { } released&&released==frameSha256)return;
        CheckOriginalHeld();_outbox.RequireAllReceipts(frameSha256);
        try{_source.Acknowledge(_original!.Sha256);_outbox.Release(frameSha256);_original=null;}
        catch(Exception e){_failure=e.ToString();throw;}
    }
    internal CanalNucleusProjectionStatus Capture()
    {Owner();return new(_outbox.Capture(),_source.Capture(),_failure,_original?.CanonicalJson);}
    private void CheckOriginalHeld()
    {
        var current=_source.Capture();
        if(_original is null||!ReferenceEquals(current.Pending,_original)||current.Failure is not null)
            throw new InvalidOperationException("The original source output must remain owned and unacknowledged through downstream admission.");
    }
    private void Owner(){if(Environment.CurrentManagedThreadId!=_owner)throw new InvalidOperationException("Use the source owner's thread.");}
    private void Healthy(){Owner();if(_failure is not null)throw new InvalidOperationException("Preserve the failed hand-off; do not repeat or discard its original source frame.");}
}

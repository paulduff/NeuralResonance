#nullable enable
using System.Text.Json;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

internal sealed record ReticulospinalTransfer(StructureId Source,VestibularRecipientFrame Original,
    VestibularProjectionFrame Projection,IReadOnlyList<VestibularProjectionReceipt> Receipts);
internal sealed record ReticulospinalFrame(long Sequence,string CanonicalJson,string Sha256,bool MoreKnownWork);
internal sealed record ReticulospinalStatus(VestibularRecipientStatus Reticular,VestibularRecipientStatus Spinal,
    ReticulospinalFrame? Pending,string? Failure,string RetainedEvidenceJson);

// Isolated four-edge subcircuit of the existing connectome: VN -> RF, VN ->
// spinal, RF -> spinal and spinal -> RF. Other outputs are explicitly outside
// this experiment, not silently acknowledged as though their targets ran.
internal sealed class ReticulospinalCircuit : IDisposable
{
    internal const string Version="owned-reticulospinal-circuit-v1";
    private const int MaximumTransfersPerFrame=4096;
    private readonly VestibularProjectionReceiver _reticular,_spinal;
    private readonly RecipientNucleusProjection _reticularOut,_spinalOut;
    private readonly int _owner=Environment.CurrentManagedThreadId;
    private readonly List<ReticulospinalTransfer> _transfers=[];
    private readonly List<VestibularProjectionReceipt> _inputReceipts=[];
    private VestibularProjectionFrame? _input,_lastInput;
    private IReadOnlyList<VestibularProjectionReceipt>? _lastReceipts;
    private ReticulospinalFrame? _pending;
    private string? _failure,_lastReleased;
    private long _sequence;
    private bool _disposed;
    internal string Fingerprint { get; }
    internal string InputFingerprint { get; }
    internal IReadOnlyList<SynapticConnection> InputRoutes { get; }
    internal IReadOnlyList<ServiceInstance> InputRecipients { get; }

    internal ReticulospinalCircuit(string sourceInstance,string hemisphere,double epochMs,double sourceStepMs,
        double reticularStepMs,double spinalStepMs,string stateDirectory,
        IReadOnlyList<SynapticConnection> vestibularCatalog,IReadOnlyList<SynapticConnection> reticularCatalog,
        IReadOnlyList<SynapticConnection> spinalCatalog,NeuralSubstrateSupply supply,int capacity=4096)
    {
        ArgumentNullException.ThrowIfNull(vestibularCatalog);ArgumentNullException.ThrowIfNull(reticularCatalog);ArgumentNullException.ThrowIfNull(spinalCatalog);
        if(!Path.IsPathFullyQualified(stateDirectory)||File.Exists(stateDirectory)||
            Directory.Exists(stateDirectory)&&Directory.EnumerateFileSystemEntries(stateDirectory).Any())
            throw new ArgumentException("Preserve old state; use a fresh absolute circuit directory.");
        SynapticConnection Select(IReadOnlyList<SynapticConnection> catalog,StructureId target)
        {
            if(catalog.Count is <1 or >64||catalog.Any(r=>r is null)||catalog.Select(r=>r.SynapseId).Distinct().Count()!=catalog.Count)
                throw new ArgumentException("Declare complete distinct source catalogs.");
            var matches=catalog.Where(r=>r.Target==target).ToArray();
            return matches.Length==1?matches[0]:throw new ArgumentException("Each selected neuronal pathway needs one explicit original projection.");
        }
        var vestibularReticular=Select(vestibularCatalog,StructureId.ReticularFormation);
        var vestibularSpinal=Select(vestibularCatalog,StructureId.SpinalCordMotor);
        var reticularSpinal=Select(reticularCatalog,StructureId.SpinalCordMotor);
        var spinalReticular=Select(spinalCatalog,StructureId.ReticularFormation);
        var reticular=new ServiceInstance(StructureId.ReticularFormation,sourceInstance+"-reticular",hemisphere,new Uri("http://127.0.0.1:1"));
        var spinal=new ServiceInstance(StructureId.SpinalCordMotor,sourceInstance+"-spinal",hemisphere,new Uri("http://127.0.0.1:1"));
        InputRoutes=Array.AsReadOnly(new[]{vestibularReticular,vestibularSpinal});InputRecipients=Array.AsReadOnly(new[]{reticular,spinal});
        var input=new VestibularProjectionOutbox(sourceInstance,hemisphere,epochMs,sourceStepMs,InputRoutes,InputRecipients);
        var rfOut=new VestibularProjectionOutbox(reticular.InstanceKey,hemisphere,epochMs,reticularStepMs,[reticularSpinal],[spinal],reticular.StructureId);
        var spinalOut=new VestibularProjectionOutbox(spinal.InstanceKey,hemisphere,epochMs,spinalStepMs,[spinalReticular],[reticular],spinal.StructureId);
        InputFingerprint=input.Fingerprint;
        _reticular=new(reticular.StructureId,reticular.InstanceKey,Path.Combine(stateDirectory,"reticular"),reticularStepMs,
            [new(sourceInstance,input.Fingerprint,epochMs,sourceStepMs,[vestibularReticular]),
             new(spinal.InstanceKey,spinalOut.Fingerprint,epochMs,spinalStepMs,[spinalReticular],spinal.StructureId)],supply,capacity,true);
        try
        {
            _spinal=new(spinal.StructureId,spinal.InstanceKey,Path.Combine(stateDirectory,"spinal"),spinalStepMs,
                [new(sourceInstance,input.Fingerprint,epochMs,sourceStepMs,[vestibularSpinal]),
                 new(reticular.InstanceKey,rfOut.Fingerprint,epochMs,reticularStepMs,[reticularSpinal],reticular.StructureId)],supply,capacity,true);
            if(!_reticular.CanAdvance||!_spinal.CanAdvance)
                throw new ArgumentException("Each neuronal grid must fit within its guaranteed initial conduction interval; no invented progress.");
            _reticularOut=new(_reticular,hemisphere,[reticularSpinal],[spinal]);_spinalOut=new(_spinal,hemisphere,[spinalReticular],[reticular]);
            if(_reticularOut.Fingerprint!=rfOut.Fingerprint||_spinalOut.Fingerprint!=spinalOut.Fingerprint)
                throw new InvalidOperationException("Declared feedback ownership differs from the actual source adapters.");
            Fingerprint=Hash(JsonSerializer.Serialize(new{Version,input.Fingerprint,Reticular=_reticular.Fingerprint,Spinal=_spinal.Fingerprint,
                Selected=new[]{vestibularReticular,vestibularSpinal,reticularSpinal,spinalReticular},
                OmittedVestibular=vestibularCatalog.Except(InputRoutes),OmittedReticular=reticularCatalog.Except([reticularSpinal]),
                OmittedSpinal=spinalCatalog.Except([spinalReticular]),MaximumTransfersPerFrame}));
        }
        catch{_spinal?.Dispose();_reticular.Dispose();throw;}
    }

    internal IReadOnlyList<VestibularProjectionReceipt> AdmitSource(VestibularProjectionFrame frame)
    {
        Healthy();ArgumentNullException.ThrowIfNull(frame);
        // Copy the caller's packet collection before retaining it.
        var copy=frame with{Packets=Array.AsReadOnly(frame.Packets.ToArray())};
        if(_lastInput is not null&&JsonSerializer.Serialize(copy)==JsonSerializer.Serialize(_lastInput))return _lastReceipts!;
        if(_pending is not null||_input is not null)throw new InvalidOperationException("Retain and acknowledge circuit output before admitting new source work.");
        if(copy.Packets.Count!=2||copy.Packets.Select(p=>p.TargetInstance).Distinct().Count()!=2||
            !copy.Packets.Select(p=>p.TargetInstance).ToHashSet().SetEquals(InputRecipients.Select(r=>r.InstanceKey)))
            throw new ArgumentException("Both actual recipients need their exact original source packets.");
        string expected=Hash(JsonSerializer.Serialize(new{Version=VestibularProjectionOutbox.Version,Fingerprint=InputFingerprint,
            sequence=copy.Sequence,originalFrameSha256=copy.SourceFrameSha256,completedTick=copy.CompletedTick,
            completedThroughMs=copy.CompletedThroughMs,Packets=copy.Packets}));
        if(expected!=copy.Sha256)throw new ArgumentException("Original source frame identity changed.");
        foreach(var packet in copy.Packets)
        {
            using var doc=JsonDocument.Parse(packet.CanonicalJson);var root=doc.RootElement;
            if(packet.SourceSequence!=copy.Sequence||packet.SourceFrameSha256!=copy.SourceFrameSha256||
                root.GetProperty("CompletedTick").GetInt64()!=copy.CompletedTick||root.GetProperty("CompletedThroughMs").GetDouble()!=copy.CompletedThroughMs)
                throw new ArgumentException("Source frame and original packet clocks differ.");
        }
        _input=copy;
        try
        {
            foreach(var recipient in new[]{_reticular,_spinal})
                _inputReceipts.Add(recipient.Admit(copy.Packets.Single(p=>p.TargetInstance==recipient.Instance)));
            _lastInput=copy;return _lastReceipts=Array.AsReadOnly(_inputReceipts.ToArray());
        }
        catch(Exception e){_failure=e.ToString();throw;} // A real accepted prefix remains owned.
    }

    internal ReticulospinalFrame PublishCompleted()
    {
        Healthy();if(_pending is not null)return _pending;
        if(_lastInput is null||_input is null&&!_reticular.CanAdvance&&!_spinal.CanAdvance)
            throw new InvalidOperationException("No newly owned source interval or known circuit work.");
        try
        {
            int count=0;
            while(count<MaximumTransfersPerFrame)
            {
                bool advanced=false;
                if(_reticular.CanAdvance){Transfer(_reticularOut,_spinal,StructureId.ReticularFormation);count++;advanced=true;}
                if(count<MaximumTransfersPerFrame&&_spinal.CanAdvance){Transfer(_spinalOut,_reticular,StructureId.SpinalCordMotor);count++;advanced=true;}
                if(!advanced)break;
            }
            bool more=_reticular.CanAdvance||_spinal.CanAdvance;string json=EvidenceJson();
            return _pending=new(checked(++_sequence),json,Hash(json),more);
        }
        catch(Exception e){_failure=e.ToString();throw;}
    }
    private void Transfer(RecipientNucleusProjection source,VestibularProjectionReceiver target,StructureId structure)
    {
        var frame=source.PublishCompleted();var actual=source.Capture().Original!;int index=_transfers.Count;
        var receipts=new List<VestibularProjectionReceipt>();
        _transfers.Add(new(structure,actual,frame,Array.AsReadOnly(receipts.ToArray())));
        foreach(var packet in frame.Packets)
        {
            var receipt=target.Admit(packet);receipts.Add(receipt);
            _transfers[index]=_transfers[index] with{Receipts=Array.AsReadOnly(receipts.ToArray())};source.Accept(receipt);
        }
        source.ReleaseSource(frame.Sha256);
    }
    internal void Acknowledge(string frameSha256)
    {
        Healthy();if(_pending is null&&_lastReleased is not null&&frameSha256==_lastReleased)return;
        if(_pending is null||frameSha256!=_pending.Sha256)throw new ArgumentException("Acknowledge the exact retained circuit evidence.");
        _lastReleased=frameSha256;_pending=null;_input=null;_inputReceipts.Clear();_transfers.Clear();
    }
    internal ReticulospinalStatus Capture(){Owner();return new(_reticular.Capture(),_spinal.Capture(),_pending,_failure,EvidenceJson());}
    private string EvidenceJson()=>JsonSerializer.Serialize(new{Version,Fingerprint,Input=_input,InputReceipts=_inputReceipts,
        Transfers=_transfers,Reticular=_reticularOut.Capture(),Spinal=_spinalOut.Capture(),Failure=_failure});
    private static string Hash(string value)=>VestibularProjectionOutbox.Hash(value);
    private void Owner(){if(Environment.CurrentManagedThreadId!=_owner)throw new InvalidOperationException("One thread owns the recurrent neuronal circuit.");if(_disposed)throw new ObjectDisposedException(nameof(ReticulospinalCircuit));}
    private void Healthy(){Owner();if(_failure is not null)throw new InvalidOperationException("Preserve the actual failed circuit prefix; no replay or reset.");}
    public void Dispose(){if(_disposed)return;Owner();_spinal.Dispose();_reticular.Dispose();_disposed=true;}
}

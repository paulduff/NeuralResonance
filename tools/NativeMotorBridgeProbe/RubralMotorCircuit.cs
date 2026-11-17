#nullable enable
using System.Text.Json;
using NeuralResonanceEngine.Protocol;

internal sealed record RubralCircuitAdmission(MotorReceptorBatchReceipt Receipt,
    IReadOnlyList<string> SelectedOriginalJson, IReadOnlyList<string> DeliveredOriginalJson, string CanonicalInputJson);
internal sealed record RubralCircuitFrame(RubralSourceFrame OriginalSource,
    IReadOnlyList<RubralCircuitAdmission> Admissions, IReadOnlyList<ReceptorMotorSourceState> MotorSamples,
    RubralSourceReceipt SourceAcknowledgement);

// Isolated ownership for the existing ten-input circuit. No live host selects
// this class. Body-side publication binds only after retained quiet priming;
// the original source/membrane grids and neuronal messages are unchanged.
internal sealed class RubralMotorCircuit : IDisposable
{
    internal const string Version = "owned-rubral-motor-circuit-v1";
    private const string FixtureVersion = "isolated-rubral-receptor-motor-study-v1";
    private readonly Guid _session;
    private readonly bool _disabled;
    private readonly IsolatedRubralSource _source;
    private readonly ReceptorMotorSource _motor;
    private readonly int _owner = Environment.CurrentManagedThreadId;
    private ReceptorMotorLane? _lane;
    private bool _faulted, _disposed;
    internal ReceptorMotorIdentity Identity { get; }
    internal IReadOnlyList<MotorReceptorDefinition> Contacts { get; }
    internal string OriginalInputJson { get; }
    internal string InputAnatomyJson { get; }
    internal string TopologyJson { get; }
    internal string ConfigurationFingerprint { get; }

    internal RubralMotorCircuit(Guid session, string newStateDirectory, float quanta, bool contactsDisabled = false)
    {
        if (session == Guid.Empty || quanta is not (0 or .25f or 1 or 4))
            throw new ArgumentException("Declare the original isolated drive condition and session.");
        _session = session; _disabled = contactsDisabled;
        var input = Input(quanta);
        var targets = new IsolatedSynapticTargets(Input(1).Take(256)
            .Select((message,index) => IsolatedSynapticTarget.From(message,index)),256);
        var projection = new IsolatedRubralProjection(Enumerable.Range(81,10),2);
        _source = new(session,"isolated-rubral-study",newStateDirectory,1500,input,targets,projection);
        try
        {
            var selected = _source.Routes.Where(r => r.TargetNeuron == "auto-SpinalCordMotor-002").OrderBy(r => r.SourceIndex).ToArray();
            if (!selected.Select(r => r.SourceIndex).SequenceEqual(Enumerable.Range(81,10)))
                throw new InvalidOperationException("Declared convergent anatomy changed.");
            Identity = new("isolated-rubral-motor","auto-SpinalCordMotor-002",
                StableNeuralIdentity.Create(FixtureVersion,session,"motor-projection"),
                StableNeuralIdentity.Create(FixtureVersion,session,"motor-synapse"));
            Contacts = Array.AsReadOnly(selected.Select(r => new MotorReceptorDefinition(r.SourceInstance,StructureId.RedNucleus,
                r.SourceNeuron,r.Synapse,r.Projection,Identity.Instance,Identity.Neuron,r.TargetIndex,"rubral-"+r.SourceNeuron,
                1+r.SourceIndex%4,MotorReceptorKind.AmpaReference,50,.125)).ToArray());
            _motor = new(MotoneuronReferenceParameters.CatLowThreshold,Contacts,session,Identity,
                crossingInterpolation:MotoneuronCrossingInterpolation.CubicHermite);
            OriginalInputJson = JsonSerializer.Serialize(new { session, instance="isolated-rubral-study", quanta, originalEvents=input });
            InputAnatomyJson = JsonSerializer.Serialize(new { version=IsolatedSynapticTargets.Version,
                fingerprint=_source.InputTargetFingerprint, entries=targets.Entries, actualDeliveryVerificationRequired=true });
            TopologyJson = JsonSerializer.Serialize(new { allRoutes=_source.Routes, selected, identity=Identity,
                contacts=Contacts, contactIds=Contacts.Select(c => c.ContactId).ToArray() });
            ConfigurationFingerprint = IsolatedRubralSource.Hash(JsonSerializer.Serialize(new { Version,
                SourceVersion=IsolatedRubralSource.Version, Projection=projection.Fingerprint,
                InputAnatomy=_source.InputTargetFingerprint, Motor=_motor.ConfigurationFingerprint,
                ContactsDisabled=_disabled, quanta, DurationMs=1500 }));
        }
        catch { _source.Dispose(); throw; }
    }

    internal ReceptorMotorSourceState CaptureMotor() { RequireOwner(); return _motor.Capture(); }
    internal RubralSourceStatus CaptureSource() { RequireOwner(); return _source.Capture(); }
    internal RubralSourceFrame? PendingSource() { RequireOwner(); return _source.Pending(); }
    internal bool Faulted { get { RequireOwner(); return _faulted || _motor.Capture().Faulted || _source.Capture().Faulted; } }

    internal void AttachMotorOutput(MotorAxonAnatomy anatomy, MotorAxonClock clock)
    {
        RequireReady();
        if (_lane is not null) throw new InvalidOperationException("Motor ownership is already bound.");
        // Existing lane validates quiet prehistory, source identity and exact
        // session/time before claiming the cell. No reset/replay of the cell.
        _lane = new ReceptorMotorLane(_motor,anatomy,clock,maximumBatches:1);
    }

    internal RubralCircuitFrame AdvanceOneSourceSample()
    {
        RequireReady();
        if (_lane is not null && _lane.Unacknowledged().Count != 0)
            throw new InvalidOperationException("Retain the whole neuronal circuit while motor output is unacknowledged.");
        try
        {
            var frame = _source.AdvanceOneSample(); var original = IsolatedRubralSource.Decode(frame);
            var receipts = new List<RubralCircuitAdmission>();
            foreach (var contact in Contacts)
            {
                var selected = original.OriginalEvents.Where(e => e.SourceNeuronId == contact.SourceNeuron && e.TargetNeuronId == contact.TargetNeuron).ToArray();
                var delivered = _disabled ? Array.Empty<SpikeMessage>() : selected;
                var receipt = _lane is null ? _motor.Admit(_session,contact.ContactId,frame.Sequence,frame.CompleteThroughMs,delivered) :
                    _lane.Admit(_session,contact.ContactId,frame.Sequence,frame.CompleteThroughMs,delivered);
                string[] selectedJson=selected.Select(e=>JsonSerializer.Serialize(e)).ToArray(), deliveredJson=delivered.Select(e=>JsonSerializer.Serialize(e)).ToArray();
                string canonical=JsonSerializer.Serialize(new{SessionId=_session,ContactId=contact.ContactId,Sequence=frame.Sequence,
                    CompleteThroughSourceMs=frame.CompleteThroughMs,OriginalEventHashes=deliveredJson.Select(IsolatedRubralSource.Hash).ToArray()});
                if(IsolatedRubralSource.Hash(canonical)!=receipt.Sha256)throw new InvalidOperationException("Original receptor payload fingerprint changed.");
                receipts.Add(new(receipt,Array.AsReadOnly(selectedJson),Array.AsReadOnly(deliveredJson),canonical));
            }
            var samples = new List<ReceptorMotorSourceState>();
            while ((_motor.Capture().CompletedSamples+1)*ReceptorMotorSource.DefaultSampleMs <= frame.CompleteThroughMs)
                samples.Add(_lane is null ? _motor.AdvanceOneSample() : _lane.AdvanceOneSample());
            var acknowledgement = _source.Acknowledge(_session,frame.Sequence,frame.Sha256);
            return new(frame,receipts.AsReadOnly(),samples.AsReadOnly(),acknowledgement);
        }
        catch { _faulted = true; throw; } // Partial admission/advance cannot resume.
    }

    internal MotorAxonBatch PublishCompleted() { RequireReady(); return Lane().PublishCompleted(); }
    internal IReadOnlyList<MotorAxonBatch> Unacknowledged() { RequireOwner(); return Lane().Unacknowledged(); }
    internal void Acknowledge(MotorAxonAcknowledgement receipt) { RequireReady(); Lane().Acknowledge(receipt); }
    private ReceptorMotorLane Lane() => _lane ?? throw new InvalidOperationException("Declare the motor/body epoch before publication.");
    private void RequireOwner()
    {
        if (Environment.CurrentManagedThreadId != _owner) throw new InvalidOperationException("The neuronal circuit has one simulation owner.");
        if (_disposed) throw new ObjectDisposedException(nameof(RubralMotorCircuit));
    }
    private void RequireReady()
    {
        RequireOwner(); if (Faulted) throw new InvalidOperationException("Faulted circuit cannot resume or discard partial evidence.");
    }
    public void Dispose()
    {
        if (Environment.CurrentManagedThreadId != _owner) throw new InvalidOperationException("Dispose on the circuit owner.");
        if (_disposed) return;
        _source.Dispose(); _disposed = true;
    }

    private static SpikeMessage[] Input(float quanta) => quanta == 0 ? [] : Enumerable.Range(0,20)
        .SelectMany(pulse => Enumerable.Range(0,256).Select(cell => new SpikeMessage
        {
            MessageId=StableNeuralIdentity.Create(FixtureVersion,"input",pulse,cell),TimestampMs=1000.125+10*pulse,
            SourceStructure=StructureId.M1,TargetStructure=StructureId.RedNucleus,SourceInstanceKey="isolated-rubral-drive-fixture",
            SourceNeuronId=$"fixture-{cell:000}",TargetNeuronId=$"n-{cell:000}",
            SynapseId=StableNeuralIdentity.Create(FixtureVersion,"input-contact",cell),
            ProjectionId=StableNeuralIdentity.Create(FixtureVersion,"input-projection",cell),
            Neurotransmitter=NTEnum.GLUTAMATE,VesicleQuanta=quanta,ReuptakeRate=8,SpikeType=SpikeTypeEnum.ACTION_POTENTIAL,
            ModulationContext=new NeuromodState(),IsSyntheticBackground=false
        })).ToArray();
}

using System.Reflection;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

namespace Nre.PosturalCircuitProbe;

internal static partial class Probe
{
    static readonly FieldInfo NeuronsField = typeof(StructureEngine).GetField("_neurons", BindingFlags.Instance | BindingFlags.NonPublic)!;
    static readonly FieldInfo InboundField = typeof(StructureEngine).GetField("_inboundSynapses", BindingFlags.Instance | BindingFlags.NonPublic)!;
    static readonly FieldInfo OutboundField = typeof(StructureEngine).GetField("_outboundSynapses", BindingFlags.Instance | BindingFlags.NonPublic)!;
    static readonly string[] ElectricalFields = ["_aisVoltage", "_ampaCurrent", "_nmdaCurrent", "_gabaACurrent", "_gabaBCurrent", "_adaptationCurrent", "_refractoryMs"];
    static readonly FieldInfo[] ElectricalAccessors = ElectricalFields.Select(name => typeof(ModelNeuron).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!).ToArray();

    static void CanonicalizeProbeMessage(SpikeMessage message, string sourceInstance)
    {
        var identity = FormattableString.Invariant($"{sourceInstance}|{message.SourceInstanceKey}|{message.TimestampMs:R}|{message.SourceStructure}|{message.TargetStructure}|{message.SourceNeuronId}|{message.TargetNeuronId}|{message.SynapseId}|{message.ProjectionId}|{message.Neurotransmitter}|{message.IsFeedback}|{message.IsSyntheticBackground}");
        message.MessageId = new Guid(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(identity)).AsSpan(0, 16));
    }

    static string[] TransferPlans(bool smoke) => smoke
        ? ["quiet-hold", "tilt-return", "muted-tilt", "left-only-tilt"]
        : ["quiet-hold", "quiet-normal-hold", "tilt-return", "support-return", "head-motion-return", "captured-trajectory", "muted-tilt", "left-only-tilt"];

    static object RestoreTransferBaseline(StructureEngine engine, StructureId id, string weights)
    {
        var inbound = (Dictionary<Guid, SynapseState>)InboundField.GetValue(engine)!;
        var outbound = (Dictionary<string, SynapseState>)OutboundField.GetValue(engine)!;
        object Summary(IEnumerable<SynapseState> entries)
        {
            var values = entries.ToArray();
            return new { Count = values.Length, AtFloor = values.Count(item => item.VesicleQuanta <= .050001f),
                Mean = values.Length == 0 ? 0 : values.Average(item => item.VesicleQuanta),
                Minimum = values.Length == 0 ? 0 : values.Min(item => item.VesicleQuanta),
                Maximum = values.Length == 0 ? 0 : values.Max(item => item.VesicleQuanta) };
        }
        var beforeInbound = Summary(inbound.Values); var beforeOutbound = Summary(outbound.Values);
        var targets = new List<SynapseState>();
        if (id == StructureId.VestibularAfferents && weights is "saved-afferent-baseline" or "saved-both-baseline") targets.AddRange(outbound.Values);
        if (id == StructureId.VestibularNuclei && weights is "saved-nucleus-baseline" or "saved-both-baseline") targets.AddRange(inbound.Values);
        var changed = targets.Count(item => item.VesicleQuanta != item.BaselineVesicleQuanta);
        foreach (var synapse in targets) synapse.VesicleQuanta = PlasticityRules.ClampQuanta(synapse.BaselineVesicleQuanta);
        return new { BeforeInbound = beforeInbound, BeforeOutbound = beforeOutbound, SelectedEntries = targets.Count,
            ChangedEntries = changed, AfterInbound = Summary(inbound.Values), AfterOutbound = Summary(outbound.Values), TracesReset = false };
    }

    static string TransferPhase(string plan, int tick, int duration) => plan.StartsWith("quiet-") ? plan :
        plan == "captured-trajectory" ? "captured-trajectory" : tick <= duration / 4 ? "initial-quiet" : tick <= duration * 3 / 4 ? "stimulus" : "returned-quiet";

    static PhysicalBodyFrameRequest TransferInput(string plan, int tick, int duration, PhysicalBodyFrameRequest stable, PhysicalBodyFrameRequest[] frames)
    {
        var seconds = tick * 8.0 / duration;
        var clockScale = 8000.0 / duration;
        if (plan == "captured-trajectory")
            return frames.Last(frame => frame.TimestampMs <= frames[0].TimestampMs + seconds * 1000);
        var envelope = 0.0; var derivative = 0.0;
        if (seconds is > 2 and < 2.5)
        {
            var phase = (seconds - 2) * Math.PI / .5;
            envelope = (1 - Math.Cos(phase)) / 2; derivative = Math.Sin(phase) * Math.PI * clockScale;
        }
        else if (seconds is >= 2.5 and <= 5.5) envelope = 1;
        else if (seconds is > 5.5 and < 6)
        {
            var phase = (seconds - 5.5) * Math.PI / .5;
            envelope = (1 + Math.Cos(phase)) / 2; derivative = -Math.Sin(phase) * Math.PI * clockScale;
        }
        var tilt = plan is "tilt-return" or "muted-tilt" or "left-only-tilt" ? (float)(.55 * envelope) : 0f;
        var tiltVelocity = plan is "tilt-return" or "muted-tilt" or "left-only-tilt" ? (float)(.55 * derivative) : 0f;
        var displacement = plan == "support-return" ? (float)(.28 * envelope) : 0f;
        var velocityX = plan == "support-return" ? (float)(.28 * derivative) : 0f;
        var comZ = .88f * MathF.Sin(tilt);
        var velocityZ = .88f * MathF.Cos(tilt) * tiltVelocity;
        var yaw = plan == "head-motion-return" && seconds is > 2 and < 6 ? (float)(.35 * Math.Sin(Math.PI * (seconds - 2))) : 0f;
        var balance = PhysicalBalanceStateFrame.Neutral with
        {
            CenterOfMassXMeters = displacement, CenterOfMassYMeters = .88f * MathF.Cos(tilt), CenterOfMassZMeters = comZ,
            CenterOfMassVelocityXMetersPerSecond = velocityX, CenterOfMassVelocityZMetersPerSecond = velocityZ,
            ExtrapolatedCenterOfMassXMeters = displacement + velocityX / 3.34f, ExtrapolatedCenterOfMassZMeters = comZ + velocityZ / 3.34f,
            SupportAreaSquareMeters = plan == "quiet-normal-hold" ? .08f : .16f,
            SupportMarginMeters = .12f - Math.Max(Math.Abs(displacement), Math.Abs(comZ)),
            FallPitchRadians = tilt, FallPitchVelocityRadiansPerSecond = tiltVelocity,
            Phase = tilt != 0 || displacement != 0 ? "fixture-unstable" : "fixture-stable"
        };
        var muscles = stable.Articulation!.Musculoskeletal!.Muscles.Select(muscle => muscle with
            { Activation = 0, ForceNewtons = 0, LengthFraction = 1, VelocityPerSecond = 0, FatigueFraction = 0 }).ToArray();
        var articulation = PhysicalArticulationFrame.Neutral with
        {
            LeftFootLoadNewtons = 359.904f, RightFootLoadNewtons = 359.904f,
            LeftFootPressure = new(89.976f, 89.976f, 89.976f, 89.976f), RightFootPressure = new(89.976f, 89.976f, 89.976f, 89.976f),
            NeckYawRadians = yaw,
            Musculoskeletal = MusculoskeletalStateFrame.Neutral with { BodyHeightMeters = 1.74f * MathF.Cos(tilt),
                UprightFraction = MathF.Cos(tilt), SupportFraction = 1, BalanceError = Math.Clamp(Math.Abs(tilt) / .55f + Math.Abs(displacement) / .28f, 0, 1),
                Muscles = muscles, Balance = balance }
        };
        return stable with { Sequence = 0, TimestampMs = 0, LinearVelocityXMetersPerSecond = velocityX, LinearVelocityYMetersPerSecond = 0,
            LinearVelocityZMetersPerSecond = velocityZ, AngularVelocityXRadiansPerSecond = tiltVelocity,
            AngularVelocityYRadiansPerSecond = 0, AngularVelocityZRadiansPerSecond = 0, Articulation = articulation, MotorTrainingMode = false };
    }

    static void ValidateTransferInputs(PhysicalBodyFrameRequest stable, PhysicalBodyFrameRequest[] frames)
    {
        if (frames[^1].TimestampMs - frames[0].TimestampMs < 8000 || frames.Zip(frames.Skip(1)).Any(pair => pair.First.TimestampMs >= pair.Second.TimestampMs))
            throw new InvalidOperationException("The captured trajectory must have ordered timestamps and at least eight seconds.");
        foreach (var plan in TransferPlans(false))
        {
            var transducer = new PhysicalBodyTransducerRuntime();
            var tagged = 0;
            for (var tick = 1; tick <= 8000; tick += 140)
            {
                var fixture = TransferInput(plan, tick, 8000, stable, frames) with { Sequence = tick, TimestampMs = tick };
                if (!PhysicalBodyFrameDescriptor.TryCreate(fixture, out var descriptor, out var error)) throw new InvalidOperationException(plan + ": " + error);
                var input = transducer.Transduce(descriptor!, tick, tick);
                tagged += input.For(StructureId.VestibularAfferents, "L").Count(RightingReflexTopology.IsPrimaryRightingAfferent);
                tagged += input.For(StructureId.ProprioceptiveAfferents, "L").Count(RightingReflexTopology.IsPrimaryRightingAfferent);
                if (plan == "quiet-hold" && (fixture.AngularVelocityXRadiansPerSecond != 0 || fixture.LinearVelocityXMetersPerSecond != 0 || fixture.Articulation!.Musculoskeletal!.Muscles.Any(muscle => muscle.VelocityPerSecond != 0)))
                    throw new InvalidOperationException("Stationary fixture contains movement.");
            }
            if (plan is "quiet-hold" or "head-motion-return" && tagged != 0) throw new InvalidOperationException("Negative control contains primary fall-sensitive events: " + plan);
            if (plan is "tilt-return" or "support-return" or "quiet-normal-hold" && tagged == 0) throw new InvalidOperationException("Positive/tonic control lacks primary fall-sensitive events: " + plan);
        }
    }

    sealed class TransferRecorder : IDisposable
    {
        readonly string directory;
        readonly (ServiceInstance Instance, StructureEngine Engine, ModelNeuron[] Cells)[] nuclei;
        readonly StreamWriter inboundTrace, electricalTrace;
        readonly Dictionary<string, TransmissionStats> paths = [];
        readonly Dictionary<string, ElectricalStats> electrical = [];
        readonly Dictionary<string, int> eventSamples = [];
        int tick;
        string phase = "initial";
        public TransferRecorder(string directory, List<(ServiceInstance Instance, StructureEngine Engine)> nodes)
        {
            this.directory = directory;
            nuclei = nodes.Where(node => node.Instance.StructureId == StructureId.VestibularNuclei)
                .Select(node => (node.Instance, node.Engine, (ModelNeuron[])NeuronsField.GetValue(node.Engine)!)).ToArray();
            inboundTrace = new(Path.Combine(directory, "inbound-integration.jsonl"));
            electricalTrace = new(Path.Combine(directory, "electrical-trace.jsonl"));
            foreach (var node in nuclei) node.Engine.InboundIntegrationObserver = observation =>
            {
                var key = $"{phase}|{node.Instance.InstanceKey}|{observation.Source}|{observation.Neurotransmitter}|{observation.IsRightingCircuitSpike}";
                if (!paths.TryGetValue(key, out var stats)) paths[key] = stats = new();
                stats.Add(observation);
                var sampleKey = phase + "|" + node.Instance.InstanceKey;
                var count = eventSamples.GetValueOrDefault(sampleKey);
                if (count < 100)
                {
                    inboundTrace.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { Tick = tick, Phase = phase,
                        Instance = node.Instance.InstanceKey, Observation = observation, ImmediatelyAfterIntegration = Cell(node.Cells[observation.TargetNeuronIndex]) }, Compact));
                    eventSamples[sampleKey] = count + 1;
                }
            };
        }
        public void BeginTick(int tick, string phase) { this.tick = tick; this.phase = phase; }
        public void AfterStep()
        {
            foreach (var node in nuclei)
            {
                var key = phase + "|" + node.Instance.InstanceKey;
                if (!electrical.TryGetValue(key, out var stats)) electrical[key] = stats = new();
                var highest = node.Cells.MaxBy(cell => cell.MembranePotentialMillivolts)!;
                var strongest = node.Cells.MaxBy(cell => cell.NetSynapticCurrent)!;
                stats.Add(node.Cells, highest, strongest);
                if (tick % 10 == 0)
                {
                    var righting = node.Cells.Where(cell => ActionChannelTopology.ChannelForNeuron(cell.Index, StructureId.VestibularNuclei) == RightingReflexTopology.StandChannel)
                        .MaxBy(cell => cell.MembranePotentialMillivolts)!;
                    electricalTrace.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { Tick = tick, Phase = phase, Instance = node.Instance.InstanceKey,
                        MostDepolarized = Cell(highest), HighestNetCurrent = Cell(strongest), MostDepolarizedStandLane = Cell(righting),
                        FiredCellCount = node.Cells.Count(cell => cell.IsActive) }, Compact));
                }
            }
        }
        static object Cell(ModelNeuron cell)
        {
            var fields = ElectricalAccessors.Select((accessor, index) => (ElectricalFields[index], Value: Convert.ToDouble(accessor.GetValue(cell)))).ToDictionary(item => item.Item1, item => item.Value);
            if (fields.Values.Any(value => !double.IsFinite(value)) || !float.IsFinite(cell.MembranePotentialMillivolts) || !float.IsFinite(cell.NetSynapticCurrent))
                throw new InvalidOperationException("Non-finite electrical observation.");
            return new { cell.Index, SomaMillivolts = cell.MembranePotentialMillivolts, cell.NetSynapticCurrent, cell.IsActive, Fields = fields };
        }
        public void Complete()
        {
            var accounting = nuclei.Select(node =>
            {
                var admitted = (int)typeof(StructureEngine).GetField("_spikeInCount", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(node.Engine)!;
                int QueueCount(string field) => ((PriorityQueue<SpikeEnvelope, double>)typeof(StructureEngine).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(node.Engine)!).Count;
                var pending = QueueCount("_feedForward") + QueueCount("_feedback");
                var integrated = paths.Where(pair => pair.Key.Split('|')[1] == node.Instance.InstanceKey).Sum(pair => pair.Value.Count);
                if (integrated + pending != admitted) throw new InvalidOperationException("Observed integration count differs from actual ingress minus pending queues.");
                return new { Instance = node.Instance.InstanceKey, Admitted = admitted, Pending = pending, Integrated = integrated };
            }).ToArray();
            Save(Path.Combine(directory, "transfer-summary.json"), new { Paths = paths, Electrical = electrical, Accounting = accounting,
                ElectricalAggregateSampleMilliseconds = 1, IndividualEventLimitPerNucleusPerPhase = 100, IndividualElectricalIntervalMilliseconds = 10,
                CompleteIntegrationCounts = paths.Values.Sum(stats => stats.Count), LiveBrainAccessed = false });
        }
        public void Dispose()
        {
            foreach (var node in nuclei) node.Engine.InboundIntegrationObserver = null;
            inboundTrace.Dispose(); electricalTrace.Dispose();
        }
    }

    sealed class TransmissionStats
    {
        public long Count { get; private set; }
        public double MeanPresynapticQuanta => presynaptic / Count;
        public double MeanPostsynapticQuanta => postsynaptic / Count;
        public double MeanEffectiveQuanta => effective / Count;
        public float MinimumEffectiveQuanta { get; private set; } = float.MaxValue;
        public float MaximumEffectiveQuanta { get; private set; }
        public long PresynapticAtFloor { get; private set; }
        public double MaximumDeliveryLatenessMilliseconds { get; private set; }
        double presynaptic, postsynaptic, effective;
        public void Add(InboundSynapticObservation input)
        {
            Count++; presynaptic += input.PresynapticQuanta; postsynaptic += input.PostsynapticQuanta; effective += input.EffectiveQuanta;
            MinimumEffectiveQuanta = Math.Min(MinimumEffectiveQuanta, input.EffectiveQuanta); MaximumEffectiveQuanta = Math.Max(MaximumEffectiveQuanta, input.EffectiveQuanta);
            if (input.PresynapticQuanta <= .050001f) PresynapticAtFloor++;
            MaximumDeliveryLatenessMilliseconds = Math.Max(MaximumDeliveryLatenessMilliseconds, input.ProcessingTimestampMs - input.ScheduledDeliveryTimestampMs);
        }
    }

    sealed class ElectricalStats
    {
        public long MillisecondSamples { get; private set; }
        public long Spikes { get; private set; }
        public float PeakSomaMillivolts { get; private set; } = -1000;
        public float PeakNetSynapticCurrent { get; private set; } = float.MinValue;
        public void Add(ModelNeuron[] cells, ModelNeuron highest, ModelNeuron strongest)
        {
            MillisecondSamples++; Spikes += cells.Count(cell => cell.IsActive);
            PeakSomaMillivolts = Math.Max(PeakSomaMillivolts, highest.MembranePotentialMillivolts);
            PeakNetSynapticCurrent = Math.Max(PeakNetSynapticCurrent, strongest.NetSynapticCurrent);
        }
    }
}

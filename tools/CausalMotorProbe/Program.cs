using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;
using NRE.SimAvatar;

if (args.Length != 3 || args.Any(x => !Path.IsPathFullyQualified(x)) || Directory.Exists(args[2]))
    throw new ArgumentException("Supply absolute protocol, recorded arrivals, and NEW output paths.");
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
json.Converters.Add(new JsonStringEnumConverter());
var compact = new JsonSerializerOptions(JsonSerializerDefaults.Web);
var spec = JsonSerializer.Deserialize<Protocol>(File.ReadAllText(args[0]), json) ?? throw new ArgumentException("Missing protocol.");
string Digest(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant(); }
if (spec.Version != "causal-motor-comparison-v1" || spec.CandidateVersion != AvatarCausalMotorPlant.Version ||
    spec.MuscleVersion != AvatarDynamicMuscle.Version || spec.Cases.Length != 8 || spec.Resolutions.Length != 5 ||
    !spec.Resolutions.Select(r => r.PhysicalStepMs).SequenceEqual(new[] { 1.0, .5, .25, .125, .125 }) ||
    !spec.Resolutions.Select(r => r.MuscleStepMs).SequenceEqual(new[] { .0625, .0625, .0625, .0625, .03125 }) ||
    !spec.MomentArmsMeters.SequenceEqual(new[] { .02, -.02 }) || spec.InertiaKgMetersSquared != 1 ||
    !spec.UnitRows.SequenceEqual(new[] { 0, 0 }) || spec.RecordedArrivalsSha256 != Digest(args[1]) ||
    spec.ParameterRows[0].Calcium != MotorUnitCalciumParameters.NagamoriSlowUnit1)
    throw new ArgumentException("Unexpected declared causal comparison inputs.");
var recorded = File.ReadLines(args[1]).Select(line => JsonSerializer.Deserialize<AvatarMotorAxonArrival>(line, compact)!).ToArray();
if (recorded.Length != 28 || recorded.Any(a => a.SessionId != recorded[0].SessionId || a.MotorUnitId != recorded[0].MotorUnitId))
    throw new ArgumentException("Require the complete original somatic motor-unit arrival record.");
Directory.CreateDirectory(args[2]);
void Save(string name, object value)
{
    using var stream = new FileStream(Path.Combine(args[2], name), FileMode.CreateNew);
    JsonSerializer.Serialize(stream, value, json);
}
Save("protocol.json", JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(args[0])));
var results = new List<object>(); int successful = 0;
foreach (var scenario in spec.Cases)
foreach (var resolution in spec.Resolutions)
{
    string key = scenario.Key + "-" + resolution.Key; Console.WriteLine("CAUSAL BODY " + key);
    AvatarCausalMotorPlant? plant = null; AnalyticJoint? body = null;
    var delivered = new List<AvatarMotorAxonArrival>();
    try
    {
        Guid session = scenario.ReplayOriginalArrivals ? recorded[0].SessionId : StableNeuralIdentity.Create("causal-joint-session-v1", scenario.Key);
        var physicalClock = new PhysicalSampleClock(session, "analytic-joint", "muscle-paths", 0, resolution.PhysicalStepMs, 0, 0);
        var motorClock = new MotorAxonClock(session, 0, 0);
        MotorAxonContact Contact(int index) => scenario.ReplayOriginalArrivals && index == 0
            ? MotorAxonContact.Create(recorded[0].SourceInstanceKey, recorded[0].SourceNeuronId,
                recorded[0].ProjectionId, recorded[0].MotorUnitId, 2.0625)
            : MotorAxonContact.Create("causal-joint-source-" + index, "fixture-cell-" + index,
                StableNeuralIdentity.Create("causal-joint-projection", index), "fixture-unit-" + index, .015625);
        var contacts = new[] { Contact(0), Contact(1) };
        if (scenario.ReplayOriginalArrivals && contacts[0].ContactId != recorded[0].ContactId)
            throw new InvalidOperationException("Recorded motor contact identity changed.");
        var anatomy = new MotorAxonAnatomy(contacts);
        var definitions = contacts.Select((contact, i) =>
        {
            var row = spec.ParameterRows[spec.UnitRows[i]];
            return new CausalMuscleDefinition("m" + i, spec.Geometry,
                [new SlowMotorUnitDefinition(contact, row.Calcium, row.LengthGainAboveOptimal, row.LengthGainBelowOptimal)],
                spec.InitialFiberLengthMeters);
        }).ToArray();
        body = new(physicalClock, spec, scenario);
        int substeps = checked((int)(resolution.PhysicalStepMs / resolution.MuscleStepMs));
        if (substeps * resolution.MuscleStepMs != resolution.PhysicalStepMs) throw new ArgumentException("Nonintegral muscle grid.");
        plant = new(physicalClock, body, definitions, substeps);
        var initial = plant.CaptureInitial();
        if (plant.State.Running || plant.State.CompletedPhysicsStep != 0 || initial.PhysicalTimeMs != 0)
            throw new InvalidOperationException("Body did not begin paused and unevolved.");
        var expected = new List<AvatarMotorAxonArrival>();
        for (int unit = 0; unit < 2; ++unit)
        {
            var contact = contacts[unit]; var inputs = scenario.Events.Where(e => e.MuscleIndex == unit).ToArray();
            var spikes = new List<SpikeMessage>();
            for (int i = 0; i < inputs.Length; ++i)
            {
                var input = inputs[i];
                var arrival = scenario.ReplayOriginalArrivals && unit == 0 ? recorded[i] :
                    new AvatarMotorAxonArrival(session, contact.SourceInstanceKey,
                        StableNeuralIdentity.Create("causal-joint-event-v1", scenario.Key, unit, i), i + 1,
                        contact.SourceNeuronId, contact.ProjectionId, StableNeuralIdentity.Create("causal-joint-synapse", unit),
                        contact.ContactId, contact.MotorUnitId, input.ArrivalAtMs - contact.ConductionDelayMs, input.ArrivalAtMs);
                if (arrival.ArrivalAtBodyMs != input.ArrivalAtMs || arrival.FiredAtNeuralMs + contact.ConductionDelayMs != input.ArrivalAtMs)
                    throw new InvalidOperationException("Original firing or arrival changed.");
                expected.Add(arrival);
                spikes.Add(new SpikeMessage { MessageId = arrival.MessageId, TimestampMs = arrival.FiredAtNeuralMs,
                    SourceInstanceKey = arrival.SourceInstanceKey, SourceNeuronId = arrival.SourceNeuronId,
                    ProjectionId = arrival.ProjectionId, SynapseId = arrival.SourceSynapseId,
                    SourceStructure = StructureId.SpinalCordMotor, Neurotransmitter = NTEnum.ACETYLCHOLINE });
            }
            plant.Accept(new MotorAxonPublisher(anatomy, motorClock, contact.SourceInstanceKey).Publish(spikes, scenario.DurationMs));
        }
        if (scenario.ReplayOriginalArrivals && !expected.SequenceEqual(recorded))
            throw new InvalidOperationException("Incomplete original arrival input.");
        Save(key + "-inputs.json", new { physicalClock, motorClock, definitions, expectedArrivals = expected,
            scenario, resolution, bodyFingerprint = body.Fingerprint, plantFingerprint = plant.Fingerprint,
            sourceCompleteThroughMs = scenario.DurationMs, allEventsSuppliedUpFront = true,
            neuronalModelExecuted = false, fixedAnatomicalFixture = true });
        using var trace = new StreamWriter(Path.Combine(args[2], key + ".csv"));
        using var eventLog = new StreamWriter(Path.Combine(args[2], key + "-arrivals.jsonl"));
        using var boundaries = new StreamWriter(Path.Combine(args[2], key + "-boundaries.csv"));
        string[] unitNames = ["totalLengthMeters", "fiberLengthMeters", "fiberVelocityMetersPerSecond", "yieldFraction",
            "activeFiberForceNewtons", "passiveFiberForceNewtons", "tendonAxialForceNewtons", "projectedTendonForceNewtons",
            "projectedFiberForceNewtons", "geometricForceNewtons", "accelerationMetersPerSecondSquared",
            "impulseNewtonSeconds", "workJoules", "momentumLedger", "kineticLedger", "releaseDrive", "freeCalcium",
            "boundCalcium", "activation", "released", "recovered", "netBound", "calciumLedger", "arrivals"];
        string[] bodyNames = ["timeMs", "jointAngleRadians", "jointSpeedRadiansPerSecond", "jointImpulseNewtonMeterSeconds",
            "jointWorkJoules", "boundaryWorkJoules", "jointMomentumLedger", "jointKineticLedger", "virtualWorkLedger"];
        trace.WriteLine(string.Join(',', bodyNames.Concat(Enumerable.Range(0, 2).SelectMany(i => unitNames.Select(n => "m" + i + "_" + n)))));
        boundaries.WriteLine("timeMs,bodyWorkJoules,m0_startLength,m0_endLength,m0_endFiber,m0_startForce,m1_startLength,m1_endLength,m1_endFiber,m1_startForce");
        double boundaryWork = 0, maximumVirtualWorkError = 0;
        void Sample(IReadOnlyDictionary<string, DynamicMuscleSnapshot> muscles)
        {
            var values = new List<double> { physicalClock.AtStep(body.CompletedPhysicsStep).PhysicalMs, body.Angle, body.Speed,
                body.Impulse, body.Work, boundaryWork, body.MaximumMomentumError, body.MaximumKineticError, maximumVirtualWorkError };
            for (int i = 0; i < 2; ++i)
            {
                var s = muscles["m" + i]; var f = s.Forces; var u = s.Units.Single();
                values.AddRange([s.MusculotendonLengthMeters, s.FiberLengthMeters, s.FiberVelocityMetersPerSecond, s.Yield,
                    f.ActiveFiberForceNewtons, f.PassiveFiberForceNewtons, f.TendonAxialForceNewtons, f.ProjectedTendonForceNewtons,
                    f.ProjectedFiberForceNewtons, f.GeometricForceNewtons, f.AccelerationMetersPerSecondSquared,
                    s.IntegratedNetImpulseNewtonSeconds, s.IntegratedNetFiberWorkJoules, s.MaximumMomentumLedgerError, s.MaximumKineticLedgerError,
                    u.ReleaseDrive, u.FreeCalcium, u.BoundCalcium, u.Activation, u.ReleasedCalcium, u.RecoveredCalcium,
                    u.NetBoundCalcium, u.MaximumCalciumLedgerError, u.Arrivals]);
            }
            if (values.Any(v => !double.IsFinite(v))) throw new InvalidOperationException("Nonfinite saved mechanical/muscle state.");
            trace.WriteLine(string.Join(',', values.Select(v => v.ToString("R"))));
        }
        Sample(plant.CaptureMuscles()); plant.Resume();
        long steps = checked((long)(scenario.DurationMs / resolution.PhysicalStepMs));
        if (steps * resolution.PhysicalStepMs != scenario.DurationMs) throw new ArgumentException("Nonintegral physical endpoint grid.");
        for (long tick = 1; tick <= steps; ++tick)
        {
            double oldWork = body.Work;
            var outcome = plant.TryAdvance(); var step = outcome.Step;
            if (outcome.Outcome != CausalMotorAdvance.Advanced || step is null || step.After.PhysicsStep != tick)
                throw new InvalidOperationException("The complete causal input did not advance exactly one body step.");
            delivered.AddRange(step.Arrivals);
            foreach (var arrival in step.Arrivals) eventLog.WriteLine(JsonSerializer.Serialize(arrival, compact));
            boundaryWork += step.HeldTensionWorkOnBodyJoules;
            maximumVirtualWorkError = Math.Max(maximumVirtualWorkError, Math.Abs(step.HeldTensionWorkOnBodyJoules - (body.Work - oldWork)));
            var row = new List<double> { step.After.PhysicalTimeMs, step.HeldTensionWorkOnBodyJoules };
            for (int i = 0; i < 2; ++i)
            {
                string muscle = "m" + i;
                row.AddRange([step.Before.Paths.Single(p => p.MuscleId == muscle).LengthMeters,
                    step.After.Paths.Single(p => p.MuscleId == muscle).LengthMeters,
                    step.Muscles[muscle].FiberLengthMeters, step.Effort.Tensions.Single(t => t.MuscleId == muscle).AxialNewtons]);
            }
            boundaries.WriteLine(string.Join(',', row.Select(v => v.ToString("R"))));
            Sample(step.Muscles);
        }
        var expectedById = expected.ToDictionary(a => a.MessageId);
        if (delivered.Count != expected.Count || delivered.Select(a => a.MessageId).Distinct().Count() != expected.Count ||
            delivered.Any(a => !expectedById.TryGetValue(a.MessageId, out var original) || a != original) ||
            delivered.Zip(delivered.Skip(1)).Any(p => p.First.ArrivalAtBodyMs > p.Second.ArrivalAtBodyMs) ||
            (scenario.ReplayOriginalArrivals && !delivered.SequenceEqual(recorded)))
            throw new InvalidOperationException("Original event identity/time/delivery changed.");
        Save(key + "-final.json", new { plant = plant.State, muscles = plant.CaptureMuscles(), body.Angle, body.Speed,
            body.Impulse, body.Work, boundaryWork, maximumVirtualWorkError });
        results.Add(new { key, completed = true, scenario = scenario.Key, resolution = resolution.Key,
            delivered = delivered.Count, physicalSteps = steps, sampleCount = steps + 1 }); successful++;
    }
    catch (Exception error)
    {
        Save(key + "-failure.json", new { error = error.ToString(), state = plant?.State, bodyStep = body?.CompletedPhysicsStep,
            angle = body?.Angle, speed = body?.Speed, delivered = delivered.Count });
        results.Add(new { key, completed = false, error = error.ToString(), delivered = delivered.Count }); Console.WriteLine(error);
    }
    Save("progress-" + results.Count.ToString("D2") + ".json", new { attempted = results.Count, successful, cases = results });
}
Save("results.json", new { completed = results.Count == 40, successfulCases = successful, cases = results,
    neuronalModelExecuted = false, nativeUnityExecuted = false, physiologyQualified = false, avatarRecoveryAssessed = false });
if (successful != 40) throw new InvalidOperationException("Causal motor/body trajectories failed; preserve partial evidence.");

internal sealed class AnalyticJoint : IAvatarMotorMechanicalBody
{
    private readonly PhysicalSampleClock _clock;
    private readonly Protocol _protocol;
    private readonly double _initialSpeed;
    private Guid _lastEffort;
    public string Fingerprint { get; }
    public long CompletedPhysicsStep { get; private set; }
    public double PhysicsStepMs => _clock.PhysicsStepMs;
    public double Angle { get; private set; }
    public double Speed { get; private set; }
    public double Impulse { get; private set; }
    public double Work { get; private set; }
    public double MaximumMomentumError { get; private set; }
    public double MaximumKineticError { get; private set; }
    public AnalyticJoint(PhysicalSampleClock clock, Protocol protocol, Scenario scenario)
    {
        _clock = clock; _protocol = protocol; Angle = scenario.InitialAngleRadians;
        Speed = _initialSpeed = scenario.InitialSpeedRadiansPerSecond;
        Fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        { Version = "analytic-constant-arm-joint-v1", protocol.InertiaKgMetersSquared, protocol.MomentArmsMeters, protocol.RestPathLengthMeters })));
    }
    public MotorMechanicalFrame CaptureGeometry() => new(_clock.SessionId, Fingerprint, CompletedPhysicsStep,
        _clock.AtStep(CompletedPhysicsStep).PhysicalMs, _lastEffort,
        _protocol.MomentArmsMeters.Select((arm, i) => new MusculotendonPathMeasurement("m" + i,
            _protocol.RestPathLengthMeters - arm * Angle, -arm * Speed)).ToArray());
    public void AdvanceOneStep(MotorMechanicalEffort effort)
    {
        if (effort.SessionId != _clock.SessionId || effort.BodyFingerprint != Fingerprint || effort.StartStep != CompletedPhysicsStep ||
            effort.StartTimeMs != _clock.AtStep(CompletedPhysicsStep).PhysicalMs ||
            effort.EndTimeMs != _clock.AtStep(CompletedPhysicsStep + 1).PhysicalMs || effort.EffortId == Guid.Empty ||
            effort.Tensions.Count != 2 || !effort.Tensions.Select(t => t.MuscleId).Order().SequenceEqual(new[] { "m0", "m1" }) ||
            effort.Tensions.Any(t => !double.IsFinite(t.AxialNewtons) || t.AxialNewtons < 0))
            throw new InvalidOperationException("Effort does not belong to this analytic joint interval.");
        double h = (effort.EndTimeMs - effort.StartTimeMs) / 1000;
        double torque = Enumerable.Range(0, 2).Sum(i => _protocol.MomentArmsMeters[i] * effort.Tensions.Single(t => t.MuscleId == "m" + i).AxialNewtons);
        double delta = Speed * h + .5 * torque / _protocol.InertiaKgMetersSquared * h * h;
        Angle += delta; Speed += torque / _protocol.InertiaKgMetersSquared * h;
        Impulse += torque * h; Work += torque * delta;
        if (!double.IsFinite(Angle) || !double.IsFinite(Speed) || !double.IsFinite(Impulse) || !double.IsFinite(Work))
            throw new InvalidOperationException("Nonfinite physical joint state.");
        MaximumMomentumError = Math.Max(MaximumMomentumError, Math.Abs(_protocol.InertiaKgMetersSquared * (Speed - _initialSpeed) - Impulse));
        MaximumKineticError = Math.Max(MaximumKineticError, Math.Abs(.5 * _protocol.InertiaKgMetersSquared * (Speed * Speed - _initialSpeed * _initialSpeed) - Work));
        CompletedPhysicsStep++; _lastEffort = effort.EffortId;
    }
}

internal sealed record ParameterRow(int Row, MotorUnitCalciumParameters Calcium, double LengthGainAboveOptimal, double LengthGainBelowOptimal);
internal sealed record Resolution(string Key, double PhysicalStepMs, double MuscleStepMs);
internal sealed record EventInput(int MuscleIndex, double ArrivalAtMs);
internal sealed record Scenario(string Key, double DurationMs, double InitialAngleRadians, double InitialSpeedRadiansPerSecond,
    EventInput[] Events, bool ReplayOriginalArrivals);
internal sealed record Protocol(string Version, string CandidateVersion, string MuscleVersion, DynamicMuscleGeometry Geometry,
    ParameterRow[] ParameterRows, double InertiaKgMetersSquared, double[] MomentArmsMeters, int[] UnitRows,
    double InitialFiberLengthMeters, double RestPathLengthMeters, Resolution[] Resolutions, Scenario[] Cases, string RecordedArrivalsSha256);

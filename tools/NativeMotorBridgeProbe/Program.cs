using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;
using NRE.SimAvatar;
using static Nre.UnityWorld.MuscleHingeWire;
using Path=System.IO.Path;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
try
{
if (args.Length == 3 && args[0] == "--afferent-rest")
{
    AfferentRestStudy.Run(args[1], args[2]);
    return;
}
if (args.Length == 3 && args[0] == "--afferent-trajectories")
{
    AfferentTrajectoryStudy.Run(args[1], args[2]);
    return;
}
if (args.Length == 3 && args[0] == "--afferent-endpoint-hold-trajectories")
{
    AfferentTrajectoryStudy.Run(args[1], args[2], AvatarSodiumActivationContinuation.EndpointHold);
    return;
}
if (args.Length == 3 && args[0] == "--afferent-numerics")
{
    AfferentNumericalStudy.Run(args[1], args[2]);
    return;
}
if (args.Length == 3 && args[0] == "--cleft-rest")
{
    CleftRestStudy.Run(args[1], args[2]);
    return;
}
if (args.Length == 3 && args[0] == "--cleft-trajectories")
{
    CleftTrajectoryStudy.Run(args[1], args[2]);
    return;
}
if (args.Length == 5 && args[0] == "--cleft-membranes")
{
    CleftMembraneStudy.Run(args[1], args[2], args[3], args[4]);
    return;
}
if (args.Length == 4 && args[0] == "--cleft-open")
{
    CleftOpenStudy.Run(args[1], args[2], args[3]);
    return;
}
if (args.Length == 3 && args[0] == "--cleft-coupling")
{
    CleftCouplingStudy.Run(args[1], args[2]);
    return;
}
if (args.Length == 3 && args[0] == "--calyx-membranes")
{
    CalyxMembraneStudy.Run(args[1], args[2]);
    return;
}
if (args.Length == 3 && args[0] == "--vestibular-hair-cell")
{
    VestibularHairCellStudy.Run(args[1], args[2]);
    return;
}
if (args.Length == 5 && args[0] == "--rubral-native-body")
{
    RubralNativeBodyStudy.Run(args[1], args[2], args[3], args[4]);
    return;
}
if (args.Length == 5 && args[0] == "--rubral-native-refinement")
{
    RubralNativeRefinementStudy.Run(args[1], args[2], args[3], args[4]);
    return;
}
if (args.Length == 3 && args[0] == "--rubral-body-source")
{
    RubralMotorCircuitStudy.Run(args[1], args[2]);
    return;
}
if (args.Length == 3 && args[0] == "--vestibular-mechanics")
{
    VestibularMechanicsStudy.Run(args[1], args[2]);
    return;
}
if (args.Length == 2 && args[0] == "--rubral-motor-input")
{
    RubralMotorStudy.Run(args[1]);
    return;
}
if (args.Length == 2 && args[0] == "--rubral-declared-input")
{
    RubralMotorStudy.Run(args[1], declaredInputs: true);
    return;
}
if (args.Length == 2 && args[0] == "--rubral-ten-inputs")
{
    RubralMotorStudy.Run(args[1], declaredInputs: true, tenInputs: true);
    return;
}
if (args.Length == 5 && args[0] == "--receptor-motor-output")
{
    ReceptorMotorOutputStudy.Run(args[1], args[2], args[3], args[4]);
    return;
}
if (args.Length == 2 && args[0] == "--runtime-fingerprint")
{
    RuntimeFingerprint.Write(args[1]);
    return;
}
if (args.Length == 3 && args[0] == "--receptor-membrane")
{
    ReceptorMembraneStudy.Run(args[1], args[2]);
    return;
}
if (args.Length == 5 && args[0] == "--neuronal-body")
{
    ObservedMotorBodyStudy.Run(args[1], args[2], args[3], args[4]);
    return;
}
if (args.Length == 3 && args[0] == "--source-check")
{
    ObservedMotorSourceStudy.Run(args[1], args[2]);
    return;
}
if ((args.Length != 3 && args.Length != 4) || args.Any(a => !Path.IsPathFullyQualified(a)) || Directory.Exists(args[2]))
    throw new ArgumentException("Supply absolute native player, frozen muscle parameter input and NEW output paths.");
var parameterJson = new JsonSerializerOptions(JsonSerializerDefaults.Web);
var options = args.Length == 4 ? JsonSerializer.Deserialize<RunOptions>(File.ReadAllText(args[3]), NativeHingeBody.Json) : new RunOptions(0, 2);
Require(options != null && (options.PhysicsResolution, options.MuscleSubsteps) is (-1, 4) or (0, 2) or (0, 4) or (1, 1) or (1, 2),
    "Require a declared isolated physical/muscle refinement condition.");
double physicalStepMs = StepMsAt(options.PhysicsResolution);
int steps = options.PhysicsResolution == -1 ? 512 : options.PhysicsResolution == 1 ? 2048 : 1024;
Require(steps * physicalStepMs == 1024 * StepMs, "Refinement must preserve the exact duration.");
var spec = JsonSerializer.Deserialize<Parameters>(Nre.UnityWorld.DiagnosticBridgeFiles.ReadPublished(args[1], 30000), parameterJson);
Require(spec != null && spec.CandidateVersion == AvatarCausalMotorPlant.Version && spec.MuscleVersion == AvatarDynamicMuscle.Version &&
    spec.ParameterRows[0].Calcium == MotorUnitCalciumParameters.NagamoriSlowUnit1, "Qualified muscle parameters required.");
Directory.CreateDirectory(args[2]);
WirePreflight.Run(args[0], Path.GetDirectoryName(args[1]), args[2]);
var results = new List<object>(); var angles = new Dictionary<string, double>();
string[] positive = ["quiet", "left", "right", "balanced"];
string[] negative = ["session", "sequence", "clock", "state", "plant", "duplicate", "negative", "inspect-force"];
foreach (string scenario in positive.Concat(negative.Select(n => "reject-" + n)))
{
    Console.WriteLine("NATIVE MOTOR BRIDGE " + scenario);
    string output = Path.Combine(args[2], scenario); Directory.CreateDirectory(output);
    string bridge = Path.Combine(output, "bridge"); Guid session = Guid.NewGuid();
    var start = new ProcessStartInfo(args[0]) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Path.GetDirectoryName(args[0]) };
    foreach (string arg in new[] { "-batchmode", "-nographics", "-logFile", Path.Combine(output, "player.log"),
        "--motor-bridge", bridge, "--motor-session", session.ToString("N"), "--motor-rest-length-bits", Encode(spec.RestPathLengthMeters),
        "--motor-resolution", options.PhysicsResolution.ToString(CultureInfo.InvariantCulture) }) start.ArgumentList.Add(arg);
    using var process = Process.Start(start) ?? throw new InvalidOperationException("Player did not start.");
    try
    {
        var body = new NativeHingeBody(bridge, session, process, exerciseReadLock: scenario == "quiet", physicsResolution: options.PhysicsResolution);
        var clock = new PhysicalSampleClock(session, "native-opposing-hinge", "muscle-paths", 0, physicalStepMs, 0, 0);
        var motorClock = new MotorAxonClock(session, 0, 0);
        var contacts = Enumerable.Range(0, 2).Select(i => MotorAxonContact.Create("native-bridge-fixture-" + i, "fixture-cell-" + i,
            StableNeuralIdentity.Create("native-bridge-projection", i), "fixture-unit-" + i, .015625)).ToArray();
        var anatomy = new MotorAxonAnatomy(contacts); var row = spec.ParameterRows[0];
        var definitions = contacts.Select((c, i) => new CausalMuscleDefinition("m" + i, spec.Geometry,
            [new SlowMotorUnitDefinition(c, row.Calcium, row.LengthGainAboveOptimal, row.LengthGainBelowOptimal)], spec.InitialFiberLengthMeters)).ToArray();
        var plant = new AvatarCausalMotorPlant(clock, body, definitions, options.MuscleSubsteps);
        body.Bind(plant.Fingerprint); var initial = plant.CaptureInitial(); var initialMuscles = plant.CaptureMuscles();
        NativeHingeBody.Write(Path.Combine(output, "initial.json"), new { scenario, clock, definitions, plantFingerprint = plant.Fingerprint,
            bodyFingerprint = body.Fingerprint, initial, muscles = initialMuscles, native = body.Last, muscleSubsteps = options.MuscleSubsteps,
            originalParameterInput = args[1], syntheticMotorEvents = true, neuronalModelExecuted = false, actualSteps = steps });
        Require(!plant.State.Running && plant.TryAdvance().Outcome == CausalMotorAdvance.Paused && body.CompletedPhysicsStep == 0,
            "The native plant did not begin paused.");
        if (scenario.StartsWith("reject-", StringComparison.Ordinal))
        {
            var failure = body.RejectNative(scenario[7..]);
            results.Add(new { scenario, passed = true, rejectedBeforePhysics = true, physicsSteps = 0, error = failure.error });
            continue;
        }
        plant.Resume();
        Require(plant.TryAdvance().Outcome == CausalMotorAdvance.WaitingForNeuronalCompleteness && body.CompletedPhysicsStep == 0,
            "Body advanced before source completeness.");
        double end = clock.AtStep(steps).PhysicalMs; int expected = 0;
        for (int i = 0; i < 2; ++i)
        {
            bool stimulated = scenario == "balanced" || (scenario == "left" && i == 0) || (scenario == "right" && i == 1);
            var contact = contacts[i]; var spikes = new List<SpikeMessage>();
            if (stimulated) foreach (double at in new[] { 10.03125, 60.03125 })
                spikes.Add(new SpikeMessage { MessageId = StableNeuralIdentity.Create("native-bridge-event", scenario, i, at),
                    TimestampMs = at - contact.ConductionDelayMs, SourceInstanceKey = contact.SourceInstanceKey,
                    SourceNeuronId = contact.SourceNeuronId, ProjectionId = contact.ProjectionId,
                    SynapseId = StableNeuralIdentity.Create("native-bridge-synapse", i),
                    SourceStructure = StructureId.SpinalCordMotor, Neurotransmitter = NTEnum.ACETYLCHOLINE });
            var batch = new MotorAxonPublisher(anatomy, motorClock, contact.SourceInstanceKey).Publish(spikes, end);
            NativeHingeBody.Write(Path.Combine(output, "source-" + i + ".json"), batch); plant.Accept(batch); expected += spikes.Count;
        }
        using var trace = new StreamWriter(Path.Combine(output, "steps.jsonl")); int arrivals = 0;
        for (int tick = 1; tick <= steps; ++tick)
        {
            var advance = plant.TryAdvance(); var step = advance.Step;
            Require(advance.Outcome == CausalMotorAdvance.Advanced && step != null && step.After.PhysicsStep == tick, "Missing native physical step.");
            arrivals += step.Arrivals.Count;
            trace.WriteLine(JsonSerializer.Serialize(new { step, native = body.Last }, NativeHingeBody.Json));
        }
        plant.Pause(); var final = body.Last;
        Require(plant.TryAdvance().Outcome == CausalMotorAdvance.Paused && plant.State.CompletedPhysicsStep == steps &&
            plant.State.DeliveredEvents == expected && arrivals == expected && plant.State.PendingEvents == 0 && !plant.State.Faulted,
            "Incomplete causal event delivery or pause failure.");
        var observed = body.CaptureGeometry(); Require(observed.PhysicsStep == steps && body.Last.stateHash == final.stateHash, "Paused native endpoint moved.");
        using var state = JsonDocument.Parse(final.stateJson); double angle = state.RootElement.GetProperty("angle").GetDouble(); angles.Add(scenario, angle);
        NativeHingeBody.Write(Path.Combine(output, "final.json"), new { plant = plant.State, muscles = plant.CaptureMuscles(), native = final, arrivals, angle });
        body.Stop(); if (!process.WaitForExit(30000)) throw new TimeoutException("Native player did not stop.");
        Require(process.ExitCode == 0, "Native valid scenario failed.");
        results.Add(new { scenario, passed = true, physicsSteps = steps, arrivals, angle });
    }
    catch (Exception e) { NativeHingeBody.Write(Path.Combine(output, "driver-failure.json"), new { error = e.ToString() }); throw; }
    finally { if (!process.HasExited) { process.Kill(true); process.WaitForExit(); } }
}
// Prospective qualitative response criteria, not a trajectory accuracy bound.
bool response = Math.Abs(angles["quiet"]) <= 1e-5 && Math.Abs(angles["balanced"]) <= 1e-5 &&
    angles["left"] > 1e-7 && angles["right"] < -1e-7 && Math.Abs(angles["left"] + angles["right"]) <= 1e-5;
NativeHingeBody.Write(Path.Combine(args[2], "results.json"), new { completed = true, passed = response, cases = results,
    responseBoundRadians = 1e-5, minimumResponseRadians = 1e-7, actualPhysicsSteps = 4 * steps,
    liveBodySelected = false, neuronalModelExecuted = false, trajectoryAccuracyQualified = false,
    physiologicalCalibrationQualified = false, avatarRecoveryAssessed = false });
Require(response, "Native opposing-muscle response criteria failed; preserve the original bounds.");
Console.WriteLine("4 causal native muscle cases and 8 native rejection controls complete.");
}
catch (Exception error)
{
    Console.Error.WriteLine("NATIVE MOTOR BRIDGE FAILED: " + error);
    Environment.ExitCode = 1;
}

internal sealed record ParameterRow(int Row, MotorUnitCalciumParameters Calcium, double LengthGainAboveOptimal, double LengthGainBelowOptimal);
internal sealed record RunOptions(int PhysicsResolution, int MuscleSubsteps);
internal sealed record Parameters(string CandidateVersion, string MuscleVersion, DynamicMuscleGeometry Geometry,
    ParameterRow[] ParameterRows, double InitialFiberLengthMeters, double RestPathLengthMeters);

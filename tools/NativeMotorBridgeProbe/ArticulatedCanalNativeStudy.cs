#nullable enable
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;
using NRE.SimAvatar;
using static Nre.UnityWorld.MuscleHingeWire;
using Wire = Nre.UnityWorld.ArticulatedTendonWire;
using Path = System.IO.Path;

// Isolated forward sensory integration. Native movement is prescribed; neither
// the receiving nucleus nor an invented firing rate supplies the tendon pulls.
internal static class ArticulatedCanalNativeStudy
{
    internal const string Version = "native-articulated-canal-nucleus-v1";
    private const int FinalStep = 8, PacketSteps = 512;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly JsonSerializerOptions FailureJson = new(JsonSerializerDefaults.Web)
        { NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals };
    private sealed record Condition(string Name, string Muscle);
    private static readonly Condition[] Cases = [new("quiet", ""), new("hip", "leftHip/flexor"), new("shoulder", "leftShoulderAbduction/flexor")];
    private static readonly int[] Counts = [8192, 16384];

    internal static void Run(string player, string originalDirectory, string input, string output, bool visible)
    {
        Require(new[] { player, originalDirectory, input, output }.All(Path.IsPathFullyQualified) &&
            File.Exists(player) && File.Exists(input) && Directory.Exists(originalDirectory) && !Directory.Exists(output),
            "Existing frozen player/input and a new output directory required.");
        using var document = JsonDocument.Parse(File.ReadAllText(input));
        var protocol = document.RootElement;
        Require(protocol.GetProperty("version").GetString() == "prescribed-head-midpoint-afferent-v1" &&
            protocol.GetProperty("forcingPolicy").GetString() == "MidpointHeld" &&
            protocol.GetProperty("activationPolicy").GetString() == "EndpointHold", "Preserve the reviewed sensory fixture.");
        Directory.CreateDirectory(output);
        Write(Path.Combine(output, "protocol.json"), new { Version, Cases, Counts, FinalStep, PacketSteps, Visible = visible,
            MechanicsMode = "no-joint-friction", Sensor = "right", MechanicalTicksPerInterval = 262144,
            ReceiverStepMs = .125, RemainingConductionDelayMs = .25, ReceivingNeuronIndex = 17,
            PrescribedTensionNewtons = 40, KeepaliveInspectionSeconds = 30,
            DiagnosticContactOnly = true, NeuronalMotorAuthority = false, IndependentConnectedAccuracyQualified = false,
            PhysiologicalFiringQualified = false, AvatarStandingQualified = false });
        foreach (var condition in Cases) foreach (int count in Counts)
            RunCase(player, originalDirectory, protocol, output, condition, count, visible);
        Write(Path.Combine(output, "result.json"), new { Version, Completed = true, Cases = 6, PhysicsSteps = 48,
            PhysicalEndpoints = 54, HeadReceipts = 162, SensorySteps = 516096, Packets = 1008,
            OriginalSensoryAndReceivingCellsExecuted = true, PrescribedForcesOnly = true,
            IndependentConnectedAccuracyQualified = false, PhysiologicalFiringQualified = false,
            NeuronalMotorAuthority = false, AvatarStandingQualified = false });
    }

    private static void RunCase(string player, string originalDirectory, JsonElement protocol, string output,
        Condition condition, int count, bool visible)
    {
        string name = condition.Name+"-n"+count;
        Console.WriteLine("NATIVE HEAD TO EXISTING SENSORY CELL / NUCLEUS " + name);
        string folder = Path.Combine(output, name); Directory.CreateDirectory(folder);
        // Reuse the exact v3 geometry/setting declaration, changing only the
        // session identity. The original source and both backups stay intact.
        var config = JsonSerializer.Deserialize<Wire.Configuration>(File.ReadAllText(Path.Combine(originalDirectory, condition.Name, "input.json")), NativeArticulatedBody.Json)!;
        Require(config.mechanicsMode == "no-joint-friction" && config.passivePreludeSteps == 1 &&
            (double)config.stepSeconds == (double).005f, "Keep the qualified native setting and physical rate.");
        Guid session = Guid.NewGuid(); config.sessionId = session.ToString("N"); Wire.Validate(config);
        string configPath = Path.Combine(folder, "input.json"); NativeHingeBody.Write(configPath, config);
        var start = new ProcessStartInfo(player) { UseShellExecute = false, CreateNoWindow = !visible,
            WindowStyle = visible ? ProcessWindowStyle.Normal : ProcessWindowStyle.Hidden, WorkingDirectory = Path.GetDirectoryName(player) };
        if (visible)
            foreach (string argument in new[] { "-screen-fullscreen", "0", "-screen-width", "1280", "-screen-height", "900" }) start.ArgumentList.Add(argument);
        else foreach (string argument in new[] { "-batchmode", "-nographics" }) start.ArgumentList.Add(argument);
        foreach (string argument in new[] { "-logFile", Path.Combine(folder, "player.log"), "--neuronal-muscle-qualify", "1",
            "--tendon-bridge", session.ToString("N"), "--tendon-config", configPath, "--output", folder }) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Native player did not start.");
        NativeArticulatedBody? body = null; NativeArticulatedHeadInput? head = null;
        AvatarCanalAfferentInput? source = null; AvatarVestibularAfferentDynamics? cell = null;
        CanalAfferentNucleusConnection? connection = null;
        long steps = 0; int endpoints = 0, packets = 0, events = 0, inspections = 0;
        using var acknowledged = Lines(Path.Combine(folder, "acknowledged.jsonl"));
        using var heads = Lines(Path.Combine(folder, "heads.jsonl"));
        using var retained = Lines(Path.Combine(folder, "nucleus-frames.jsonl"));
        using var eventWriter = Lines(Path.Combine(folder, "events.jsonl"));
        using var inspected = Lines(Path.Combine(folder, "inspections.jsonl"));
        try
        {
            body = new(File.ReadAllText(configPath), new FileArticulatedTendonExchange(Path.Combine(folder, "tendon-bridge"), process));
            var clock = new PhysicalSampleClock(session, "full-avatar-native", "avatar_unity_body", 0, body.PhysicsStepMs, 0, 1000);
            head = new(body, clock); var first = Record();
            if (visible) Thread.Sleep(2000); // Wall viewing time only; no physical/neuronal step.
            var attachment = body.InitializePassiveInterval(clock); var second = Record();
            Write(Path.Combine(folder, "attachment.json"), new { Attachment = attachment, OriginalEvidenceJson = body.InitializationEvidenceJson });
            var binding = new AvatarCanalAfferentClock(clock, 262144, count);
            AvatarCupulaShearField field; AvatarCupulaBundleAttachment sensoryAttachment;
            (source, cell, field, sensoryAttachment) = CreateSource(protocol, binding, Right(first).Observation, Right(second).Observation);
            var actual = source.Capture().Mechanical.LastReceipt!.Mechanics.Input.Skull;
            Require(actual == Right(second).Kinematics && actual.PreviousObservationSha256 == Right(first).Kinematics.RawObservationSha256,
                "The sensory source must start with both original native head measurements.");
            const string instance = "isolated-native-canal-nucleus";
            var route = new ObservedAfferentRoute(StableNeuralIdentity.Create("native-canal-nucleus-contact", 17), instance, 17, .25, 1, 8);
            connection = new(source, instance, Path.Combine(folder, "fresh-nucleus"), [route], .125, NeuralSubstrateSupply.ReferenceBath);
            Write(Path.Combine(folder, "initial.json"), new { Binding = binding, Source = source.Source, source.Fingerprint,
                Neuronal = cell.Capture(), Mechanical = source.Capture().Mechanical, Route = route,
                Field = field, Attachment = sensoryAttachment,
                NucleusFingerprint = connection.Fingerprint, ReceivingContactPhysiologyQualified = false });
            string plant = Wire.Digest(JsonSerializer.Serialize(new { Version, name, Clock = clock.Fingerprint,
                attachment.InitializationEvidenceSha256, Kind = "prescribed tendon pulls; no neuronal motor source" }));
            body.Bind(plant);
            var initial = cell.Capture();
            using var file = new FileStream(Path.Combine(folder, "trajectory.csv.gz"), FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            using var gzip = new GZipStream(file, CompressionLevel.Fastest); using var rows = new StreamWriter(gzip, new UTF8Encoding(false));
            rows.WriteLine(string.Join(',', new[] { "step", "timeMs", "heldBundleNm", "mechanicalStartTick", "mechanicalEndTick",
                "mechanicalEndNeuralMs", "representationDifferenceMs", "startMv", "endMv", "startSlopeMvPerMs", "endSlopeMvPerMs" }
                .Concat(initial.StateLabels).Concat(initial.StateLabels.Select(x => "stateError/"+x))
                .Concat(initial.LedgerLabels).Concat(initial.LedgerLabels.Select(x => "ledgerError/"+x))
                .Concat(new[] { "maximumChargeResidualFc", "maximumIonResidualAttomoles", "maximumCableChargeResidualFc",
                    "maximumInterfaceChargeResidualFc", "nonlinearEvaluations", "maximumNewtonIterations", "maximumAcceptedScaledResidual" })));
            var keepalive = Stopwatch.StartNew();
            for (long step = 1; step < FinalStep; ++step)
            {
                var before = source.Capture(); var recipient = connection.Capture();
                Require(before.CompletedSteps == before.AcknowledgedSteps && before.PendingPacket is null && before.Mechanical.PendingTrace is null &&
                    recipient.Pending is null && recipient.SourceSteps == recipient.AcknowledgedSourceSteps && steps == (step-1)*count,
                    "Finish and acknowledge the original sensory interval before requesting another physical step.");
                var effort = new MotorMechanicalEffort(Guid.NewGuid(), session, body.Fingerprint, plant, step,
                    clock.AtStep(step).PhysicalMs, clock.AtStep(step+1).PhysicalMs,
                    Array.AsReadOnly(config.declarations.Select(d => new TendonTension(d.muscleId, d.muscleId == condition.Muscle ? 40 : 0)).ToArray()));
                body.AdvanceOneStep(effort); var frame = Record(); keepalive.Restart();
                var released = connection.ConsumeHead(Right(frame).Observation);
                Require(released.Mechanics.Input.Skull == Right(frame).Kinematics,
                    "Canal input differs from the original head adapter receipt.");
                Write(Path.Combine(folder, "mechanical-"+frame.PhysicsStep.ToString("D2")+".json"), released);
                for (int local = 0; local < count; ++local)
                {
                    var receipt = connection.AdvanceNext(steps); steps++; var state = cell.Capture(); var voltage = receipt.Observation.Interval;
                    Require(state.CompletedSteps == steps && state.TimeMs == receipt.End.SourceNeuralMs && state.Failure is null,
                        "Original sensory state or time changed.");
                    if (receipt.Observation.Event is not null) { events++; eventWriter.WriteLine(JsonSerializer.Serialize(receipt.Observation, Json)); }
                    var values = new List<double> { steps, state.TimeMs, receipt.HeldBundle.Bundle.BundleDisplacementNanometers,
                        receipt.Start.MechanicalTick, receipt.End.MechanicalTick, receipt.End.MechanicalNeuralMs, receipt.End.RepresentationDifferenceMs,
                        voltage.StartMv, voltage.EndMv, voltage.StartSlopeMvPerMs, voltage.EndSlopeMvPerMs };
                    values.AddRange(state.Values); values.AddRange(state.AdditionResiduals); values.AddRange(state.Ledgers); values.AddRange(state.LedgerAdditionResiduals);
                    values.AddRange(new[] { state.MaximumChargeResidualFc, state.MaximumIonResidualAttomoles, state.MaximumCableChargeResidualFc,
                        state.MaximumInterfaceChargeResidualFc, state.NonlinearEvaluations, (double)state.MaximumNewtonIterations, state.MaximumAcceptedScaledResidual });
                    Require(values.All(double.IsFinite), "Nonfinite retained sensory state.");
                    rows.WriteLine(string.Join(',', values.Select(x => x.ToString("R", CultureInfo.InvariantCulture))));
                    if ((local+1)%PacketSteps == 0)
                    {
                        var published = connection.PublishCompleted();
                        retained.WriteLine(JsonSerializer.Serialize(published, Json)); // Save before source release.
                        connection.Acknowledge(published.Sha256); packets++;
                    }
                    // Original bridge has a five-minute missing-command guard.
                    // A read-only inspection retains the endpoint and keeps its
                    // file protocol alive during expensive sensory calculation.
                    if (keepalive.Elapsed.TotalSeconds >= 30)
                    {
                        body.CaptureGeometry(); inspected.WriteLine(JsonSerializer.Serialize(body.Last, NativeArticulatedBody.Json)); inspections++;
                        Require(ReferenceEquals(frame, head.ReadAcknowledgedEndpoint()), "Inspection advanced the physical observation.");
                        keepalive.Restart();
                    }
                }
                rows.Flush();
                Console.WriteLine(name+" physical interval "+step+" / 7; source steps "+steps+"; observed afferent events "+events);
            }
            var final = source.Capture(); var nucleus = connection.Capture();
            Require(final.CompletedSteps == 7L*count && final.AcknowledgedSteps == steps && final.Mechanical.PendingTrace is null &&
                nucleus.SourceSteps == steps && nucleus.AcknowledgedSourceSteps == steps && nucleus.Pending is null && nucleus.Failure is null &&
                endpoints == 9 && packets == 7*count/PacketSteps, "Incomplete original source-to-nucleus accounting.");
            if (visible) Thread.Sleep(2000);
            body.Stop(); Require(process.WaitForExit(30000) && process.ExitCode == 0, "Native player did not stop cleanly.");
            Write(Path.Combine(folder, "result.json"), new { Completed = true, Name = name, SourceStepsPerInterval = count,
                PhysicalEndpoints = endpoints, PhysicsSteps = body.CompletedPhysicsStep, HeadReceipts = endpoints*3,
                SensorySteps = steps, Packets = packets, Events = events, KeepaliveInspections = inspections,
                Source = final, Nucleus = nucleus, OriginalNativeHeadConsumed = true,
                IndependentConnectedAccuracyQualified = false, PhysiologicalFiringQualified = false,
                NeuronalMotorAuthority = false, AvatarStandingQualified = false });

            ArticulatedHeadFrame Record()
            {
                acknowledged.WriteLine(JsonSerializer.Serialize(body.Last, NativeArticulatedBody.Json));
                var frame = head.ReadAcknowledgedEndpoint(); heads.WriteLine(JsonSerializer.Serialize(frame, Json)); endpoints++;
                Require(frame.PhysicsStep == endpoints-1, "Native head endpoint missing."); return frame;
            }
        }
        catch (Exception error)
        {
            string? prefixError = null;
            try
            {
                var state = source?.Capture();
                if (state is not null && state.CompletedSteps > state.AcknowledgedSteps)
                    Write(Path.Combine(folder, "unacknowledged-prefix.json"), source!.Seal());
            }
            catch (Exception preservation) { prefixError = preservation.ToString(); }
            Write(Path.Combine(folder, "failure.json"), new { Error = error.ToString(), PrefixError = prefixError,
                LastAcknowledged = body?.Last, Head = head?.Last, head?.AttemptedReplyJson, head?.PartialSamples,
                HeadFailure = head?.Failure, Source = source?.Capture(), Neuronal = cell?.Capture(), Nucleus = connection?.Capture(),
                Steps = steps, Endpoints = endpoints, Packets = packets, Events = events, CommandResent = false }, FailureJson);
            throw;
        }
        finally
        {
            connection?.Dispose();
            if (!process.HasExited) { process.Kill(entireProcessTree: true); process.WaitForExit(); }
        }
    }

    private static (AvatarCanalAfferentInput, AvatarVestibularAfferentDynamics, AvatarCupulaShearField, AvatarCupulaBundleAttachment) CreateSource(JsonElement protocol,
        AvatarCanalAfferentClock binding, AvatarSkullObservation first, AvatarSkullObservation second)
    {
        Require(first.SensorId == "right" && second.SensorId == "right" && first.PhysicsStep == 0 && second.PhysicsStep == 1,
            "Use the original right material-point baseline and first derivative.");
        var fixture = protocol.GetProperty("fixture").Deserialize<AfferentNumericalStudy.JointCase>(Json)!;
        var settings = protocol.GetProperty("nonlinearSettings").Deserialize<AfferentLoadedRestStudy.Settings>(Json)!;
        var bundle = protocol.GetProperty("bundle"); var g = bundle.GetProperty("geometry"); var a = bundle.GetProperty("attachment");
        var parameters = AvatarCoupledCanalParameters.PublishedRightLabyrinth1();
        var field = AvatarCupulaMechanicalBinding.EquivalentCircularField(parameters, g.GetProperty("organId").GetString()!,
            (AvatarCupulaCanal)g.GetProperty("canal").GetInt32(), g.GetProperty("source").GetString()!, g.GetProperty("sourceSha256").GetString()!,
            g.GetProperty("shearModulusPascal").GetDouble(), g.GetProperty("thicknessOverRadius").GetDouble());
        var attachment = new AvatarCupulaBundleAttachment(a.GetProperty("hairCellId").GetGuid(), field.OrganId, field.Canal,
            a.GetProperty("source").GetString()!, a.GetProperty("rootXOverRadius").GetDouble(), a.GetProperty("bundleLengthMeters").GetDouble(),
            a.GetProperty("excitatoryPolarity").GetInt32());
        var mechanics = new AvatarCanalBundleInput(binding.PhysicalClock, first.HeadBodyId, first.SensorId, first.LocalPointMeters,
            AvatarCanalPrimeBasis.PublishedPrintedRightLabyrinth1(), parameters, new(default, default), AvatarCanalStepSchedule.BoundaryRefined(4), field, attachment);
        mechanics.Consume(first); mechanics.Consume(second);
        var f = fixture.Sensory; var t = f.Topology;
        Require(f.Inputs.All(x => x.AxonOutgoingPa == 0), "The actual cable owns all calyx loading.");
        var spatial = new AvatarCleftSpatialCoupling(t.HairCellId, t.HairApicalPf, t.HairPerilymphPf, t.Material, t.Compartments, t.Connections);
        var sensory = new AvatarVestibularCleftMembranes(new(spatial, t.Reservoir, t.Faces), f.HairParameters, f.CalyxParameters);
        var cable = new AvatarAfferentCable(fixture.CableTopology.Nodes, fixture.CableTopology.Edges, fixture.CableTopology.Terminals, AvatarSodiumActivationContinuation.EndpointHold);
        var kernel = new AvatarVestibularCleftAfferent(sensory, cable, fixture.Attachments);
        var cell = new AvatarVestibularAfferentDynamics(binding.PhysicalClock.SessionId, attachment.HairCellId, kernel,
            new(binding.SourceStepMs, settings.VoltageAbsoluteToleranceMv, settings.IonAbsoluteToleranceMm, settings.GateAbsoluteTolerance,
                settings.RelativeTolerance, settings.MaximumNewtonIterations, settings.MaximumLineSearchHalvings), f.HairMv, f.PerilymphMv,
            f.States, f.Inputs.Select(x => new AvatarCleftGateState(x.Id, x.HairGates, x.CalyxGates)).ToArray(), fixture.CableStates, binding.SourceEpochMs);
        var source = new AvatarCanalAfferentInput(mechanics, cell,
            new("isolated-native-canal-afferent", attachment.HairCellId.ToString("D"), protocol.GetProperty("observationNode").GetString()!,
                protocol.GetProperty("thresholdMv").GetDouble()), binding, PacketSteps, AvatarCanalForcingPolicy.MidpointHeld);
        return (source, cell, field, attachment);
    }
    private static ArticulatedHeadSample Right(ArticulatedHeadFrame frame) => frame.Samples.Single(s => s.SensorId == "right");
    private static StreamWriter Lines(string path) => new(new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read),
        new UTF8Encoding(false, true)) { AutoFlush = true };
    private static void Write(string path, object value, JsonSerializerOptions? options = null)
    { using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read); JsonSerializer.Serialize(stream, value, options ?? Json); }
}

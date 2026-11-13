using System.Diagnostics;
using System.Text.Json;
using NeuralResonanceEngine.Shared.Contracts;
using NRE.SimAvatar;
using UnityPhysicalSampleProbe;

// Run only inside the independent PowerShell study. This program owns and
// observes its own isolated Unity child; the agent does not monitor the study.
static class Program
{
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }

    public static int Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--audit")
        {
            Console.WriteLine(JsonSerializer.Serialize(SavedEvidence.Audit(Path.GetFullPath(args[1])), UnityPhysicalSampleBody.Json));
            return 0;
        }
        if (args.Length != 3) throw new ArgumentException("PLAYER OUTPUT SESSION_GUID_N required.");
        string player = Path.GetFullPath(args[0]), output = Path.GetFullPath(args[1]);
        Guid session = Guid.ParseExact(args[2], "N");
        if (Directory.Exists(output)) throw new InvalidOperationException("Fresh study output required.");
        Directory.CreateDirectory(output); Directory.CreateDirectory(Path.Combine(output, "batches"));
        Process? process = null; UnityPhysicalSampleBody? body = null;
        var report = new Dictionary<string, object?> { ["completed"] = false, ["modelKind"] = "actual Unity mechanics",
            ["sessionId"] = session, ["liveBrainAccessed"] = false, ["neuronalSourceQualified"] = false,
            ["afferentPhysiologyQualified"] = false, ["avatarRecoveryAssessed"] = false, ["speedMeasured"] = false };
        int exit = 1;
        try
        {
            var start = new ProcessStartInfo(player) { UseShellExecute = false, CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden, WorkingDirectory = Path.GetDirectoryName(player)! };
            foreach (string arg in new[] { "-batchmode", "-nographics", "--physical-sample-qualify", session.ToString("N"),
                "--muscles", "--output", output, "-logFile", Path.Combine(output, "unity.log") }) start.ArgumentList.Add(arg);
            process = Process.Start(start) ?? throw new InvalidOperationException("Unity did not start.");
            body = new UnityPhysicalSampleBody(output, session, process);
            report["unity"] = body.LastStep.Unity;
            var clock = new PhysicalSampleClock(session, "isolated-unity-avatar", "avatar_unity_body", 0, body.PhysicsStepMs, 0, 1000);
            UnityPhysicalSampleBody.WriteNew(Path.Combine(output, "clock.json"), clock);
            var producer = new AvatarPhysicalSampleProducer(clock, body, maximumSamples: 4);
            var receiver = new AvatarPhysicalSampleReceiver(clock, maximumSamples: 4);
            var expected = new List<PhysicalBodySample>(); var consumed = new List<PhysicalBodySample>();
            var checkpoints = new List<object>(); int backpressureChecks = 0, retries = 0, pauseChecks = 0, packetIndex = 0;
            float initialHeight = body.LastStep.PelvisHeightMeters, maxDisplacement = 0;
            double maximumContactForce = 0;
            void RecordSource()
            {
                expected.Add(PhysicalSampleProtocol.Capture(clock, body.CompletedPhysicsStep, body.CaptureObservation()));
                maxDisplacement = Math.Max(maxDisplacement, Math.Abs(body.LastStep.PelvisHeightMeters - initialHeight));
                foreach (string text in body.LastStep.ContactsJson)
                    maximumContactForce = Math.Max(maximumContactForce,
                        JsonSerializer.Deserialize<SomaticContactFrameRequest>(text, UnityPhysicalSampleBody.Json)!.ForceNewtons);
            }
            void VerifyHeldBody(string reason)
            {
                long prior = body.CompletedPhysicsStep; string observation = UnityPhysicalSampleBody.BodyWithoutWallStamp(body.LastStep);
                Thread.Sleep(250); // Deliberate transport delay inside the standalone experiment.
                var inspection = body.Inspect();
                Require(inspection.PhysicsStep == prior && UnityPhysicalSampleBody.BodyWithoutWallStamp(inspection) == observation,
                    "Actual Unity body moved while held: " + reason);
                checkpoints.Add(new { reason, step = prior, inspectedSequence = inspection.Sequence, unchangedObservation = true });
            }
            void Drain(int count)
            {
                var batch = producer.Publish(count);
                UnityPhysicalSampleBody.WriteNew(Path.Combine(output, "batches", "batch-" + batch.Sequence.ToString("D6") + ".json"), batch);
                // Round-trip the real wire representation before acceptance.
                var wire = JsonSerializer.Deserialize<PhysicalSampleBatch>(JsonSerializer.Serialize(batch, UnityPhysicalSampleBody.Json), UnityPhysicalSampleBody.Json)!;
                var receipt = receiver.Accept(wire);
                Require(receiver.Accept(wire) == receipt, "Receiver retry differs."); ++retries;
                consumed.AddRange(receiver.ConsumeThroughStep(batch.CompleteThroughStep));
                Require(receiver.ConsumeThroughStep(batch.CompleteThroughStep).Count == 0, "Repeated consumption duplicated samples.");
                producer.Acknowledge(receipt); producer.Acknowledge(receipt);
                UnityPhysicalSampleBody.WriteNew(Path.Combine(output, "batches", "receipt-" + batch.Sequence.ToString("D6") + ".json"), receipt);
            }
            Require(producer.TryAdvance() == PhysicalSampleAdvance.Paused, "Startup must remain paused.");
            producer.CaptureInitial(); RecordSource();
            VerifyHeldBody("initial paused state"); ++pauseChecks; producer.Resume();
            for (int step = 1; step <= 200; ++step)
            {
                if (producer.State.RetainedSamples == 4)
                {
                    int requests = body.StepRequests;
                    for (int i = 0; i < 3; ++i)
                    { Require(producer.TryAdvance() == PhysicalSampleAdvance.Backpressured, "A full buffer must hold before physics."); ++backpressureChecks; }
                    Require(body.StepRequests == requests, "Backpressure sent a Unity step request.");
                    if (step == 4) VerifyHeldBody("full unacknowledged buffer");
                    int[] sizes = { 1, 3, 4 }; Drain(sizes[packetIndex++ % sizes.Length]);
                }
                Require(producer.TryAdvance() == PhysicalSampleAdvance.Advanced && body.CompletedPhysicsStep == step,
                    "Missing actual Unity endpoint."); RecordSource();
                if (step == 80)
                {
                    producer.Pause(); int requests = body.StepRequests;
                    for (int i = 0; i < 3; ++i) Require(producer.TryAdvance() == PhysicalSampleAdvance.Paused, "Paused producer stepped.");
                    Require(requests == body.StepRequests, "Pause sent a Unity step request.");
                    VerifyHeldBody("operator-style pause"); ++pauseChecks; producer.Resume();
                }
                if (step % 50 == 0) Console.WriteLine("Captured actual Unity physics step " + step);
            }
            while (producer.State.UnsentSamples > 0) Drain(4);
            Require(expected.SequenceEqual(consumed) && consumed.Count == 201 && receiver.AcceptedSamples == 201 &&
                receiver.ConsumedSamples == 201 && producer.State.RetainedSamples == 0, "Body delivery differs from original fixed-grid observations.");
            Require(maxDisplacement > .05f && maximumContactForce > 0, "No qualifying actual motion/contact evidence.");
            // Explicit failure intervention after one real additional movement.
            body.InjectNextCaptureFailure = true; bool injectionObserved = false;
            try { producer.TryAdvance(); }
            catch (InvalidOperationException e) when (e.Message == "Injected post-movement capture failure") { injectionObserved = true; }
            Require(injectionObserved && producer.State.Faulted && !producer.State.Running &&
                producer.State.CapturedThroughStep == 200 && body.CompletedPhysicsStep == 201,
                "Post-movement capture failure did not stop at the last valid sample.");
            int afterFailureRequests = body.StepRequests;
            bool continuationRejected = false;
            try { producer.TryAdvance(); } catch (InvalidOperationException) { continuationRejected = true; }
            Require(continuationRejected && body.StepRequests == afterFailureRequests, "Faulted epoch issued another step.");
            VerifyHeldBody("faulted epoch after failed capture");
            UnityPhysicalSampleBody.WriteNew(Path.Combine(output, "source-grid.json"), body.StepSources.Select(s => new { step = s.Step, replyFile = s.ReplyFile }).ToArray());
            UnityPhysicalSampleBody.WriteNew(Path.Combine(output, "consumed.json"), consumed);
            UnityPhysicalSampleBody.WriteNew(Path.Combine(output, "checkpoints.json"), checkpoints);
            body.Stop();
            Require(process.WaitForExit(30_000) && process.ExitCode == 0, "Unity did not shut down cleanly.");
            UnityPhysicalSampleBody.WriteNew(Path.Combine(output, "saved-analysis.json"), SavedEvidence.Audit(output));
            report["completed"] = true; report["actualPhysicsSteps"] = body.CompletedPhysicsStep;
            report["capturedAndConsumedSamples"] = consumed.Count; report["completeThroughStep"] = receiver.SampleCompleteThroughStep;
            report["backpressureChecks"] = backpressureChecks; report["receiptRetries"] = retries; report["pauseChecks"] = pauseChecks;
            report["heldBodyChecks"] = checkpoints.Count; report["maximumPelvisDisplacementMeters"] = maxDisplacement;
            report["maximumContactForceNewtons"] = maximumContactForce; report["postMovementCaptureFailureStopped"] = true;
            report["directAndWrongOwnerStepsRejected"] = true; report["stepAndBodyDeliveryChecksPassed"] = true;
            report["contactsTransportedThroughBodyLane"] = false;
            report["contactScope"] = "All 15 per-link encoded contacts retained in raw source replies; not delivered by the body-only v1 lane.";
            report["clockScope"] = "Declared isolated epoch; no live DNNE clock handshake.";
            exit = 0;
        }
        catch (Exception e) { report["error"] = e.ToString(); Console.Error.WriteLine(e); }
        finally
        {
            // Only this program's child is stopped; uncertain commands are never replayed.
            if (process is not null && !process.HasExited)
            { process.Kill(entireProcessTree: true); process.WaitForExit(); report["dedicatedPlayerStoppedOnFailure"] = true; }
            report["completedUtc"] = DateTime.UtcNow; report["exitCode"] = exit;
            UnityPhysicalSampleBody.WriteNew(Path.Combine(output, "study-result.json"), report);
            process?.Dispose();
        }
        return exit;
    }
}

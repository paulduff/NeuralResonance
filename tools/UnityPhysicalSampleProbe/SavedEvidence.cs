using System.Text.Json;
using NeuralResonanceEngine.Shared.Contracts;
using NRE.SimAvatar;

namespace UnityPhysicalSampleProbe;

// Reconstruct delivery from immutable Unity replies and saved packets only.
// This entry point does not start Unity, step any model or invent observations.
public static class SavedEvidence
{
    private sealed record SourceRow(long Step, string ReplyFile);
    private sealed record Request(int Version, string SessionId, string Operation, long Sequence, long ExpectedStep);
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
    private static T Read<T>(string path) => JsonSerializer.Deserialize<T>(File.ReadAllText(path), UnityPhysicalSampleBody.Json)
        ?? throw new InvalidOperationException("Missing evidence: " + path);

    public static object Audit(string directory)
    {
        var clock = Read<PhysicalSampleClock>(Path.Combine(directory, "clock.json")); clock.Validate();
        Require(clock.InitialPhysicsStep == 0 && clock.PhysicsStepMs == (double).01f * 1000 &&
            clock.PhysicalEpochMs == 0 && clock.NeuralEpochMs == 1000 && clock.BodyId == "isolated-unity-avatar" &&
            clock.SourceId == "avatar_unity_body", "Declared isolated clock differs.");
        var rows = Read<SourceRow[]>(Path.Combine(directory, "source-grid.json"));
        Require(rows.Length == 202 && rows.Select(s => s.Step).SequenceEqual(Enumerable.Range(0, 202).Select(i => (long)i)),
            "Original Unity grid is incomplete or repeated.");
        var originals = new Dictionary<long, BridgeReply>();
        foreach (var row in rows)
        {
            Require(row.ReplyFile == "bridge/hello.json" && row.Step == 0 ||
                System.Text.RegularExpressions.Regex.IsMatch(row.ReplyFile, "^bridge/reply-[0-9]{6}\\.json$") && row.Step > 0,
                "Unexpected source evidence path.");
            var reply = Read<BridgeReply>(Path.Combine(directory, row.ReplyFile));
            Validate(reply, clock);
            Require(reply.PhysicsStep == row.Step && reply.Operation == (row.Step == 0 ? "hello" : "step"), "Source grid identity differs.");
            originals.Add(row.Step, reply);
        }
        string bridge = Path.Combine(directory, "bridge");
        Require(!File.Exists(Path.Combine(bridge, "failure.json")) && Directory.GetFiles(bridge, "*.tmp").Length == 0,
            "Failed or incomplete source transaction exists.");
        var requests = Directory.GetFiles(bridge, "request-*.json").OrderBy(p => p, StringComparer.Ordinal).ToArray();
        Require(requests.Length == 206 && Directory.GetFiles(bridge, "reply-*.json").Length == 206, "Expected 201 step, four inspect and one stop transactions.");
        long step = 0; int inspections = 0; var inspectedSteps = new List<long>();
        for (int i = 0; i < requests.Length; ++i)
        {
            var request = Read<Request>(requests[i]); long sequence = i + 1;
            Require(Path.GetFileName(requests[i]) == "request-" + sequence.ToString("D6") + ".json" &&
                request.Version == 1 && request.SessionId == clock.SessionId.ToString("N") && request.Sequence == sequence && request.ExpectedStep == step,
                "Invalid physical command history.");
            if (request.Operation == "step") ++step;
            else Require(request.Operation == "inspect" || request.Operation == "stop" && sequence == requests.Length, "Unknown or premature stop command.");
            var reply = Read<BridgeReply>(Path.Combine(bridge, "reply-" + sequence.ToString("D6") + ".json")); Validate(reply, clock);
            Require(reply.Sequence == sequence && reply.Operation == request.Operation && reply.PhysicsStep == step, "Reply changed the actual command/step mapping.");
            if (request.Operation == "inspect")
            {
                ++inspections;
                inspectedSteps.Add(step);
                Require(UnityPhysicalSampleBody.BodyWithoutWallStamp(reply) == UnityPhysicalSampleBody.BodyWithoutWallStamp(originals[step]),
                    "Held body changed at an independently inspected physical endpoint.");
            }
            if (request.Operation == "step") Require(reply == originals[step] ||
                JsonSerializer.Serialize(reply, UnityPhysicalSampleBody.Json) == JsonSerializer.Serialize(originals[step], UnityPhysicalSampleBody.Json),
                "Grid does not retain the original step reply.");
        }
        Require(step == 201 && inspections == 4 && inspectedSteps.SequenceEqual(new long[] { 0, 3, 80, 201 }), "Actual physical/check history differs.");
        var receiver = new AvatarPhysicalSampleReceiver(clock, maximumSamples: 4);
        var consumed = new List<PhysicalBodySample>(); var packetSizes = new HashSet<int>(); long batchSequence = 0;
        string batches = Path.Combine(directory, "batches");
        foreach (string path in Directory.GetFiles(batches, "batch-*.json").OrderBy(p => p, StringComparer.Ordinal))
        {
            var batch = Read<PhysicalSampleBatch>(path); ++batchSequence;
            Require(batch.Sequence == batchSequence && Path.GetFileName(path) == "batch-" + batchSequence.ToString("D6") + ".json", "Batch sequence differs.");
            packetSizes.Add(batch.Samples.Count);
            var receipt = receiver.Accept(batch);
            Require(receipt == Read<PhysicalSampleReceipt>(Path.Combine(batches, "receipt-" + batchSequence.ToString("D6") + ".json")), "Saved receipt differs from accepted bytes.");
            Require(receiver.Accept(batch) == receipt, "Saved replay differs.");
            consumed.AddRange(receiver.ConsumeThroughStep(batch.CompleteThroughStep));
        }
        Require(Directory.GetFiles(batches, "receipt-*.json").LongLength == batchSequence && packetSizes.IsSupersetOf(new[] { 1, 3, 4 }), "Packet-size/receipt coverage incomplete.");
        var expected = Enumerable.Range(0, 201).Select(i => PhysicalSampleProtocol.Capture(clock, i,
            UnityPhysicalSampleBody.DecodeBody(originals[i]))).ToArray();
        Require(expected.SequenceEqual(consumed) && expected.SequenceEqual(Read<PhysicalBodySample[]>(Path.Combine(directory, "consumed.json"))),
            "Saved body samples differ from original Unity observations.");
        Require(receiver.SampleCompleteThroughStep == 200 && receiver.AcceptedSamples == 201 && receiver.ConsumedSamples == 201,
            "Delivery asserted the deliberately failed endpoint or lost earlier observations.");
        float displacement = originals.Where(p => p.Key <= 200).Max(p => Math.Abs(p.Value.PelvisHeightMeters - originals[0].PelvisHeightMeters));
        float contactForce = originals.Where(p => p.Key <= 200).SelectMany(p => p.Value.ContactsJson)
            .Max(s => JsonSerializer.Deserialize<SomaticContactFrameRequest>(s, UnityPhysicalSampleBody.Json)!.ForceNewtons);
        Require(displacement > .05f && contactForce > 0, "Saved physical motion/contact evidence is insufficient.");
        return new { completed = true, originalGridSamples = 202, deliveredBodySamples = 201, completeThroughStep = 200,
            actualPhysicsSteps = step, heldBodyInspections = inspections, packets = batchSequence,
            maximumPelvisDisplacementMeters = displacement, maximumContactForceNewtons = contactForce,
            originalTypedObservationsExactlyPreserved = true, rawUnityRepliesRetained = true,
            contactsTransportedThroughBodyLane = false, modelExecutedDuringAudit = false,
            liveNeuralClockHandshake = false, afferentPhysiologyQualified = false, avatarRecoveryAssessed = false };
    }

    private static void Validate(BridgeReply reply, PhysicalSampleClock clock)
    {
        Require(reply.Version == 1 && reply.SessionId == clock.SessionId.ToString("N") && reply.PhysicsStepMs == clock.PhysicsStepMs &&
            reply.Paused && reply.Isolated && !reply.Connected && reply.Finite && reply.MusclesEnabled &&
            reply.DirectStepRejected && reply.WrongOwnerRejected && reply.Links == 15 && reply.BodyFramesAccepted == 0 &&
            reply.RetinalFramesAccepted == 0 && reply.ContactFramesAccepted == 0, "Source state/clock/isolation differs.");
        var body = UnityPhysicalSampleBody.DecodeBody(reply);
        Require(body.Sequence == reply.PhysicsStep && body.InputSource == clock.SourceId && body.Articulation?.Musculoskeletal?.Muscles.Count == 38,
            "Body source fields differ.");
        _ = PhysicalSampleProtocol.Capture(clock, reply.PhysicsStep, body);
        Require(reply.ContactsJson is { Length: 15 }, "Raw per-link contacts missing.");
        var contacts = reply.ContactsJson.Select(s => JsonSerializer.Deserialize<SomaticContactFrameRequest>(s, UnityPhysicalSampleBody.Json)!).ToArray();
        Require(contacts.All(c => c is not null && c.Sequence == reply.PhysicsStep) && contacts.Select(c => c.InputSource).Distinct().Count() == 15,
            "Raw contact source identities differ.");
        _ = JsonSerializer.Serialize(contacts, UnityPhysicalSampleBody.Json);
    }
}

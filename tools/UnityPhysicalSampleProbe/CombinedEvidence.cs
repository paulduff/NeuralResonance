using System.Text.Json;
using NeuralResonanceEngine.Shared.Contracts;
using NRE.SimAvatar;

namespace UnityPhysicalSampleProbe;

// No physics/model execution: rederive complete observations from the original
// native replies, then replay retained packets through the actual receiver.
public static class CombinedEvidence
{
    public sealed record LayoutEvidence(int Version, string Fingerprint, string ClockFingerprint,
        string ContactEncoding, PhysicalBodyObservationProfile BodyProfile, IReadOnlyList<PhysicalContactSite> ContactSites);
    public static LayoutEvidence Declare(PhysicalObservationLayout layout) => new(PhysicalObservationProtocol.Version,
        layout.Fingerprint, layout.Clock.Fingerprint, layout.ContactEncoding, layout.BodyProfile, layout.ContactSites);
    private static T Read<T>(string path) => JsonSerializer.Deserialize<T>(File.ReadAllText(path), UnityPhysicalSampleBody.Json)
        ?? throw new InvalidOperationException("Missing combined evidence: " + path);
    private static string Json<T>(T value) => JsonSerializer.Serialize(value, UnityPhysicalSampleBody.Json);
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
    public static PhysicalObservationFrame Frame(BridgeReply reply) => UnityObservationEncoding.Capture(reply.BodyJson, reply.ContactsJson);

    public static string WithoutWallStamps(BridgeReply reply)
    {
        var frame = Frame(reply);
        return Json(frame with { Body = frame.Body with { TimestampMs = 0 },
            Contacts = frame.Contacts.OrderBy(c => c.SiteId, StringComparer.Ordinal)
                .Select(c => c with { Observation = c.Observation with { TimestampMs = 0 } }).ToArray() });
    }

    public static object Audit(string directory)
    {
        var history = SavedEvidence.ReadSourceHistory(directory);
        var layout = UnityObservationEncoding.CreateLayout(history.Clock);
        Require(Json(Read<LayoutEvidence>(Path.Combine(directory, "layout.json"))) == Json(Declare(layout)),
            "Body availability, contact encoding, sites, clock or layout identity differs.");
        // Includes step 201's untouched full native reply, though its deliberately
        // truncated adapter copy must never become an accepted physical sample.
        var originals = history.Originals.ToDictionary(p => p.Key,
            p => PhysicalObservationProtocol.Capture(layout, p.Key, Frame(p.Value)));
        foreach (var held in history.Held)
            Require(WithoutWallStamps(held) == WithoutWallStamps(history.Originals[held.PhysicsStep]),
                "Body or contacts changed while physics was held.");
        var receiver = new AvatarPhysicalObservationReceiver(layout, maximumSamples: 4);
        var consumed = new List<PhysicalObservationSample>(); var packetSizes = new HashSet<int>(); long sequence = 0;
        string batches = Path.Combine(directory, "batches");
        foreach (string path in Directory.GetFiles(batches, "batch-*.json").OrderBy(p => p, StringComparer.Ordinal))
        {
            var batch = Read<PhysicalObservationBatch>(path); ++sequence;
            Require(batch.Sequence == sequence && Path.GetFileName(path) == "batch-" + sequence.ToString("D6") + ".json",
                "Combined batch sequence differs.");
            packetSizes.Add(batch.Samples.Count);
            var receipt = receiver.Accept(batch);
            Require(receipt == Read<PhysicalObservationReceipt>(Path.Combine(batches, "receipt-" + sequence.ToString("D6") + ".json")),
                "Receipt does not acknowledge the original complete payload.");
            Require(receiver.Accept(batch) == receipt, "Packet replay differs.");
            consumed.AddRange(receiver.ConsumeThroughStep(batch.CompleteThroughStep));
            Require(receiver.ConsumeThroughStep(batch.CompleteThroughStep).Count == 0, "Repeated consumption duplicated observations.");
        }
        Require(Directory.GetFiles(batches, "receipt-*.json").LongLength == sequence && packetSizes.SetEquals(new[] { 1, 3, 4 }),
            "Packet-size/receipt coverage differs.");
        var expected = Enumerable.Range(0, 201).Select(i => originals[i]).ToArray();
        Require(expected.SequenceEqual(consumed) && expected.SequenceEqual(Read<PhysicalObservationSample[]>(Path.Combine(directory, "consumed.json"))),
            "Delivered samples differ from original Unity body/contact endpoints.");
        Require(receiver.AcceptedSamples == 201 && receiver.ConsumedSamples == 201 && receiver.SampleCompleteThroughStep == 200 &&
            receiver.PendingSamples == 0 && receiver.PendingObservationBytes == 0, "Partial endpoint was accepted or an earlier observation was lost.");
        var injection = Read<MissingContactEvidence>(Path.Combine(directory, "missing-contact-intervention.json"));
        var original = Frame(history.Originals[201]);
        Require(injection.PhysicsStep == 201 && injection.DroppedSiteId == "head" &&
            injection.OriginalReplyFile == "bridge/reply-" + history.Originals[201].Sequence.ToString("D6") + ".json" &&
            Json(injection.DeliveredToProducer) == Json(original with { Contacts = original.Contacts.Where(c => c.SiteId != "head").ToArray() }),
            "Missing-contact intervention differs from one explicitly omitted native site.");
        bool rejected = false;
        try { _ = PhysicalObservationProtocol.Capture(layout, 201, injection.DeliveredToProducer); }
        catch (ArgumentException) { rejected = true; }
        var fault = Read<PhysicalObservationProducerState>(Path.Combine(directory, "fault-state.json"));
        Require(rejected && fault.Initialized && fault.Faulted && !fault.Running && fault.ObservedPhysicsStep == 201 &&
            fault.CapturedThroughStep == 200 && fault.RetainedSamples == 0 && fault.RetainedObservationBytes == 0 &&
            fault.UnsentSamples == 0 && fault.PendingBatches == 0 && fault.AcknowledgedThroughSequence == sequence &&
            fault.FaultReason == "Completed-step capture: ArgumentException: Body and every declared contact must be captured at this physical step.",
            "Producer did not stop completeness at the last whole endpoint.");
        var frames = consumed.Select(s => PhysicalObservationProtocol.Decode(layout, s)).ToArray();
        int count = frames.Sum(f => f.Contacts.Count);
        float displacement = history.Originals.Where(p => p.Key <= 200)
            .Max(p => Math.Abs(p.Value.PelvisHeightMeters - history.Originals[0].PelvisHeightMeters));
        float force = frames.SelectMany(f => f.Contacts).Max(c => c.Observation.ForceNewtons);
        int unloaded = frames.SelectMany(f => f.Contacts).Count(c => c.Observation.ForceNewtons == 0);
        Require(count == 3015 && unloaded > 0 && displacement > .05f && force > 0,
            "Native motion or complete loaded/unloaded contact evidence is insufficient.");
        return new { completed = true, originalGridSamples = originals.Count, actualPhysicsSteps = history.FinalStep,
            deliveredBodySamples = consumed.Count, deliveredContactObservations = count, completeThroughStep = receiver.SampleCompleteThroughStep,
            heldBodyAndContactInspections = history.Held.Count, packets = sequence, unloadedContactObservations = unloaded,
            maximumPelvisDisplacementMeters = displacement, maximumContactForceNewtons = force,
            originalTypedObservationsExactlyPreserved = true, rawUnityRepliesRetained = true, bodyProfilePreserved = true,
            missingContactInjectionRejected = true, nativeSourceActuallyLostContact = false, completeBodyAndContactsTransported = true,
            modelExecutedDuringAudit = false, liveNeuralClockHandshake = false, afferentPhysiologyQualified = false, avatarRecoveryAssessed = false };
    }
}

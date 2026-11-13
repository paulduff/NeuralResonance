using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using NeuralResonanceEngine.Protocol;

namespace NeuralResonanceEngine.Shared.Contracts;

public sealed record PhysicalContactSite(string SiteId, string SourceId);

// Immutable session declaration. Changing a site, its source, its encoding or
// the clock creates a different stream; a missing site never means zero load.
public sealed class PhysicalObservationLayout
{
    public PhysicalSampleClock Clock { get; }
    public string ContactEncoding { get; }
    public IReadOnlyList<PhysicalContactSite> ContactSites { get; }
    public string Fingerprint { get; }

    public PhysicalObservationLayout(PhysicalSampleClock clock, string contactEncoding,
        IReadOnlyList<PhysicalContactSite> contactSites)
    {
        ArgumentNullException.ThrowIfNull(clock); clock.Validate();
        ArgumentNullException.ThrowIfNull(contactSites);
        if (!ValidKey(contactEncoding) || contactSites.Count is < 1 or > 128)
            throw new ArgumentException("Declare an encoding and one to 128 contact sites.");
        var sites = contactSites.ToArray();
        if (sites.Any(s => s is null || !ValidKey(s.SiteId) || !ValidKey(s.SourceId) || s.SourceId == clock.SourceId) ||
            sites.Select(s => s.SiteId).Distinct(StringComparer.Ordinal).Count() != sites.Length ||
            sites.Select(s => s.SourceId).Distinct(StringComparer.Ordinal).Count() != sites.Length)
            throw new ArgumentException("Each contact site and source must be distinct, explicit and separate from the body source.");
        Array.Sort(sites, (a, b) => StringComparer.Ordinal.Compare(a.SiteId, b.SiteId));
        Clock = clock; ContactEncoding = contactEncoding; ContactSites = Array.AsReadOnly(sites);
        Fingerprint = PhysicalSampleProtocol.Digest(JsonSerializer.Serialize(new
        {
            Domain = "body-and-contact-observations", PhysicalObservationProtocol.Version,
            Clock = clock.Fingerprint, ContactEncoding, ContactSites
        }));
    }

    private static bool ValidKey(string? value) => value is { Length: > 0 and <= 128 } &&
        !string.IsNullOrWhiteSpace(value) && value == value.Trim() && !value.Any(char.IsControl);
}

public sealed record PhysicalContactObservation(string SiteId, SomaticContactFrameRequest Observation);
public sealed record PhysicalObservationFrame(PhysicalBodyFrameRequest Body, IReadOnlyList<PhysicalContactObservation> Contacts);
public sealed record PhysicalObservationSample(Guid SampleId, long PhysicsStep, double PhysicalTimeMs,
    double NeuralTimeMs, string ObservationJson, string ObservationSha256);
public sealed record PhysicalObservationBatch(int Version, Guid SessionId, string SourceId,
    string LayoutFingerprint, long Sequence, long CompleteThroughStep, IReadOnlyList<PhysicalObservationSample> Samples);
public sealed record PhysicalObservationReceipt(int Version, Guid SessionId, string SourceId,
    string LayoutFingerprint, long Sequence, long CompleteThroughStep, string BatchSha256);

// Separate protocol from body-only PhysicalSampleProtocol v1: no implicit
// upgrade of captures, no claim about collisions between endpoints or spikes.
public static class PhysicalObservationProtocol
{
    public const int Version = 1;
    public const int MaximumObservationBytes = 1_048_576;
    public const int MaximumBatchSamples = 128;
    private static readonly JsonSerializerOptions Json = CreateJson();

    private static JsonSerializerOptions CreateJson()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(info =>
        {
            if (info.Kind == JsonTypeInfoKind.Object)
                foreach (var property in info.Properties) property.IsRequired = true;
        });
        // This protocol serializes every member, including explicit nulls. A
        // missing force/normal/time member cannot silently become numeric zero.
        return new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, TypeInfoResolver = resolver };
    }

    public static int ObservationBytes(PhysicalObservationSample sample) => Encoding.UTF8.GetByteCount(sample.ObservationJson);
    public static Guid Identity(PhysicalObservationLayout layout, long step) =>
        StableNeuralIdentity.Create("physical-body-and-contacts-v1", layout.Fingerprint, step);

    public static PhysicalObservationSample Capture(PhysicalObservationLayout layout, long step, PhysicalObservationFrame observation)
    {
        ArgumentNullException.ThrowIfNull(layout); ArgumentNullException.ThrowIfNull(observation);
        var time = layout.Clock.AtStep(step);
        // Freeze the caller's site list before validating/serializing it.
        var contacts = observation.Contacts?.ToArray() ?? throw new ArgumentException("Contact observations are required.");
        var frame = observation with { Contacts = contacts };
        ValidateOrigin(layout, step, frame);
        frame = frame with { Contacts = contacts.OrderBy(c => c.SiteId, StringComparer.Ordinal).ToArray() };
        string payload = JsonSerializer.Serialize(frame, Json); // Rejects all nonfinite nested measurements.
        if (Encoding.UTF8.GetByteCount(payload) > MaximumObservationBytes)
            throw new ArgumentException("Combined observation exceeds its declared byte limit.");
        return new(Identity(layout, step), step, time.PhysicalMs, time.NeuralMs, payload, PhysicalSampleProtocol.Digest(payload));
    }

    public static PhysicalObservationFrame Decode(PhysicalObservationLayout layout, PhysicalObservationSample sample)
    {
        ArgumentNullException.ThrowIfNull(layout); ArgumentNullException.ThrowIfNull(sample);
        var time = layout.Clock.AtStep(sample.PhysicsStep);
        if (sample.SampleId != Identity(layout, sample.PhysicsStep) || sample.PhysicalTimeMs != time.PhysicalMs ||
            sample.NeuralTimeMs != time.NeuralMs || string.IsNullOrEmpty(sample.ObservationJson) ||
            ObservationBytes(sample) > MaximumObservationBytes || sample.ObservationSha256 != PhysicalSampleProtocol.Digest(sample.ObservationJson))
            throw new ArgumentException("Combined observation identity, clock or original payload differs.");
        using (var document = JsonDocument.Parse(sample.ObservationJson)) RejectDuplicateKeys(document.RootElement);
        var frame = JsonSerializer.Deserialize<PhysicalObservationFrame>(sample.ObservationJson, Json)
            ?? throw new ArgumentException("Missing combined physical observation.");
        ValidateOrigin(layout, sample.PhysicsStep, frame);
        _ = JsonSerializer.Serialize(frame, Json);
        return frame; // Fresh decoded graph; callers cannot change retained bytes.
    }

    private static void ValidateOrigin(PhysicalObservationLayout layout, long step, PhysicalObservationFrame frame)
    {
        if (frame.Body is null || frame.Body.InputSource != layout.Clock.SourceId ||
            frame.Body.Sequence != step || frame.Body.TimestampMs < 0 ||
            frame.Contacts is null || frame.Contacts.Count != layout.ContactSites.Count)
            throw new ArgumentException("Body and every declared contact must be captured at this physical step.");
        var bySite = layout.ContactSites.ToDictionary(s => s.SiteId, StringComparer.Ordinal);
        foreach (var contact in frame.Contacts)
        {
            if (contact is null || contact.SiteId is null || !bySite.Remove(contact.SiteId, out var site) ||
                contact.Observation is not { } value || value.InputSource != site.SourceId ||
                value.Sequence != step || value.TimestampMs < 0)
                throw new ArgumentException("Missing, duplicated, stale or substituted contact site/source.");
        }
        // No normalization, clipping, rectification or physiological claim.
        // Existing encoding and its units/coordinate convention are explicit in
        // the layout. Different wall stamps at one endpoint remain metadata.
    }

    private static void RejectDuplicateKeys(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new ArgumentException("Repeated observation member.");
                RejectDuplicateKeys(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) RejectDuplicateKeys(item);
    }

    public static PhysicalObservationReceipt Receipt(PhysicalObservationBatch batch) => new(batch.Version, batch.SessionId,
        batch.SourceId, batch.LayoutFingerprint, batch.Sequence, batch.CompleteThroughStep,
        PhysicalSampleProtocol.Digest(JsonSerializer.Serialize(batch)));
}

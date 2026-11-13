using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using NeuralResonanceEngine.Shared.Contracts;

namespace UnityPhysicalSampleProbe;

// Declared from ArticulatedAvatar.Create, not inferred from whichever contacts
// happen to arrive. This bridge represents the existing encoded measurements;
// it does not invent contacts for a missing link or expose raw collision events.
public static class UnityObservationEncoding
{
    private static readonly string[] Links = { "pelvis", "chest", "head", "left_thigh", "left_shin", "left_foot",
        "left_arm", "left_forearm", "left_hand", "right_thigh", "right_shin", "right_foot", "right_arm", "right_forearm", "right_hand" };
    private static readonly PhysicalContactSite[] Sites = Links.Select(link => new PhysicalContactSite(link, "avatar_unity_" + link + "_contact")).ToArray();
    private static readonly Dictionary<string, string> SiteBySource = Sites.ToDictionary(s => s.SourceId, s => s.SiteId, StringComparer.Ordinal);
    private static readonly JsonSerializerOptions Json = CreateJson();
    public const string Encoding = "unity-contact-v1-pelvis-rotated-position-with-initial-height-offset";
    // Unity has rigid hands, not the legacy articulated finger/grip plant. Two
    // recovery diagnostics and the manipulator extension are also placeholders.
    // Preserve their encoded/default values, but never label them measurements.
    public static PhysicalBodyObservationProfile BodyProfile { get; } = new("unity-muscular-body-v1",
        new[] { "/articulation/leftHandApertureFraction", "/articulation/rightHandApertureFraction",
            "/articulation/leftGripForceNewtons", "/articulation/rightGripForceNewtons",
            "/articulation/leftHandFatigue", "/articulation/rightHandFatigue",
            "/articulation/leftHandSlip", "/articulation/rightHandSlip", "/articulation/manipulatorExtensionFraction",
            "/articulation/musculoskeletal/balance/rightingForceFraction",
            "/articulation/musculoskeletal/balance/dynamicStabilityAllowanceMeters" },
        new[] { "/coreTemperatureCelsius", "/bloodOxygenSaturationFraction", "/articulation/supportPlaneOffsetMeters" });

    public static PhysicalObservationLayout CreateLayout(PhysicalSampleClock clock)
    {
        if (clock.SourceId != "avatar_unity_body") throw new ArgumentException("Unity body source differs.");
        return new(clock, Encoding, Sites, BodyProfile);
    }

    public static PhysicalObservationFrame Capture(PhysicalBodyFrameRequest body, IReadOnlyList<string> contactsJson)
    {
        if (contactsJson is null || contactsJson.Count != Sites.Length)
            throw new ArgumentException("All fifteen encoded Unity contacts are required.");
        var contacts = contactsJson.Select(json =>
        {
            using var document = JsonDocument.Parse(json);
            var fields = document.RootElement.EnumerateObject().Select(p => p.Name).ToArray();
            if (fields.Distinct(StringComparer.Ordinal).Count() != fields.Length)
                throw new ArgumentException("Repeated Unity contact member.");
            var observation = JsonSerializer.Deserialize<SomaticContactFrameRequest>(json, Json)
                ?? throw new ArgumentException("Missing Unity contact.");
            if (observation.InputSource is null || !SiteBySource.TryGetValue(observation.InputSource, out var site))
                throw new ArgumentException("Unexpected Unity contact source.");
            return new PhysicalContactObservation(site, observation);
        }).ToArray();
        return new(body, contacts);
        // The combined producer validates the declared site set and all step
        // counters atomically before retention. No fallback to body-only capture.
    }

    private static JsonSerializerOptions CreateJson()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(info => { foreach (var property in info.Properties) property.IsRequired = true; });
        return new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = false,
            TypeInfoResolver = resolver, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    }
}

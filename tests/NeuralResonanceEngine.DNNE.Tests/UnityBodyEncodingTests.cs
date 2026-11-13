using System.Text.Json;
using System.Text.Json.Nodes;
using NeuralResonanceEngine.Shared.Contracts;
using UnityPhysicalSampleProbe;

namespace NeuralResonanceEngine.DNNE.Tests;

// Synthetic source-schema checks; the native Unity study supplies real frames.
public sealed class UnityBodyEncodingTests
{
    private static readonly string[] Absent = { "leftHandApertureFraction", "rightHandApertureFraction",
        "leftGripForceNewtons", "rightGripForceNewtons", "leftHandFatigue", "rightHandFatigue", "leftHandSlip", "rightHandSlip" };
    private static JsonObject Native()
    {
        var body = new PhysicalBodyFrameRequest(0, 1234, -.2f, .1f, 0, 0, 0, 0, 6_000_000, .99f, 37, .98f, .75f,
            "avatar_unity_body", PhysicalArticulationFrame.Neutral with { Musculoskeletal = MusculoskeletalStateFrame.Neutral with
                { Muscles = new[] { new PhysicalMuscleMeasurement("soleus", "L", .1f, 20, .95f, -.25f, .2f) } } },
            false, new(.8, .7, .9, "avatar-reduced-availability-v1"));
        var node = JsonSerializer.SerializeToNode(body, new JsonSerializerOptions(JsonSerializerDefaults.Web))!.AsObject();
        foreach (string name in Absent) node["articulation"]!.AsObject().Remove(name);
        node["neuralSubstrates"]!.AsObject().Remove("isValid");
        return node;
    }

    [Fact]
    public void NativeSchemaPreservesSignedMeasurementsAndDeclaresHandDefaultsUnavailable()
    {
        var body = UnityObservationEncoding.DecodeNativeBody(Native().ToJsonString());
        Assert.Equal(-.2f, body.LinearVelocityXMetersPerSecond);
        Assert.Equal(-.25f, Assert.Single(body.Articulation!.Musculoskeletal!.Muscles).VelocityPerSecond);
        Assert.Equal(1f, body.Articulation.LeftHandApertureFraction);
        foreach (string name in Absent)
            Assert.Contains("/articulation/" + name, UnityObservationEncoding.BodyProfile.UnavailableMembers);
        Assert.True(body.NeuralSubstrates!.IsValid);
    }

    [Fact]
    public void EveryNativeMemberMustBePresentBeforeSharedDefaultsCanBeApplied()
    {
        int removed = 0;
        void Check(JsonNode node, Action<JsonNode> replaceRoot)
        {
            if (node is JsonObject obj)
                foreach (string key in obj.Select(p => p.Key).ToArray())
                {
                    var value = obj[key]; obj.Remove(key); ++removed;
                    replaceRoot(node);
                    obj.Add(key, value);
                    if (value is not null) Check(value, _ => replaceRoot(node));
                }
            else if (node is JsonArray array)
                foreach (var child in array) if (child is not null) Check(child, _ => replaceRoot(node));
        }
        var root = Native();
        Check(root, _ => Assert.Throws<JsonException>(() => UnityObservationEncoding.DecodeNativeBody(root.ToJsonString())));
        Assert.True(removed > 80);
    }

    [Fact]
    public void DuplicateUnknownAndNewHandMembersCannotMasqueradeAsTheDeclaredEncoding()
    {
        var node = Native();
        foreach (string name in Absent)
        {
            node["articulation"]![name] = 0;
            Assert.Throws<JsonException>(() => UnityObservationEncoding.DecodeNativeBody(node.ToJsonString()));
            node["articulation"]!.AsObject().Remove(name);
        }
        node["neuralSubstrates"]!["isValid"] = true;
        Assert.Throws<JsonException>(() => UnityObservationEncoding.DecodeNativeBody(node.ToJsonString()));
        node["neuralSubstrates"]!.AsObject().Remove("isValid");
        string json = node.ToJsonString();
        Assert.Throws<ArgumentException>(() => UnityObservationEncoding.DecodeNativeBody(json.Replace("\"sequence\":0", "\"sequence\":0,\"sequence\":0")));
        Assert.Throws<ArgumentException>(() => UnityObservationEncoding.DecodeNativeBody(json.Replace("\"velocityPerSecond\":-0.25", "\"velocityPerSecond\":-0.25,\"velocityPerSecond\":0")));
    }

    [Fact]
    public void NullNativePlantsAreNotMeasuredZeroButFiniteOutOfRangeValuesStayVisible()
    {
        foreach (string name in new[] { "articulation", "neuralSubstrates" })
        {
            var node = Native(); node[name] = null;
            Assert.Throws<ArgumentException>(() => UnityObservationEncoding.DecodeNativeBody(node.ToJsonString()));
        }
        foreach (string name in new[] { "leftFootPressure", "rightFootPressure", "musculoskeletal" })
        {
            var node = Native(); node["articulation"]![name] = null;
            Assert.Throws<ArgumentException>(() => UnityObservationEncoding.DecodeNativeBody(node.ToJsonString()));
        }
        foreach (string name in new[] { "muscles", "balance" })
        {
            var node = Native(); node["articulation"]!["musculoskeletal"]![name] = null;
            Assert.Throws<ArgumentException>(() => UnityObservationEncoding.DecodeNativeBody(node.ToJsonString()));
        }
        var unusual = Native(); unusual["neuralSubstrates"]!["glucoseAvailability"] = -2;
        Assert.Equal(-2, UnityObservationEncoding.DecodeNativeBody(unusual.ToJsonString()).NeuralSubstrates!.GlucoseAvailability);
    }
}

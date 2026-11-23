using System.Text.Json;
using System.Text.Json.Nodes;
using static NeuralResonanceEngine.DNNE.Tests.ArticulatedTendonBridgeTests;
using Wire = Nre.UnityWorld.ArticulatedTendonWire;

namespace NeuralResonanceEngine.DNNE.Tests;

// Mechanical declaration/admission controls, not native friction calibration.
public sealed class ArticulatedMechanicsSelectionTests
{
    [Theory]
    [InlineData("original", .05f)][InlineData("no-joint-friction", 0f)]
    public void ExplicitMechanicalChoiceMatchesAcknowledgedGeometry(string mode, float expected)
    {
        var config = Config(); config.mechanicsMode = mode; var peer = new Peer(config); var body = peer.Host();
        body.CaptureGeometry();
        using var geometry = JsonDocument.Parse(body.Last.geometryJson);
        Assert.All(geometry.RootElement.GetProperty("links").EnumerateArray(), l => Assert.Equal(expected, l.GetProperty("jointFriction").GetSingle()));
        Assert.Equal(mode, JsonDocument.Parse(body.Last.mechanicsDiagnosticJson).RootElement.GetProperty("mode").GetString());
        Assert.Equal(0, peer.PhysicalSteps);
    }
    [Theory]
    [InlineData(null)][InlineData("")][InlineData("free-active-axes")][InlineData("no-gravity")]
    public void MissingOrUnreviewedMechanicalModeCannotBeDefaulted(string? mode)
    {
        var config = Config(); config.mechanicsMode = mode!;
        Assert.Throws<InvalidOperationException>(() => Wire.Validate(config));
    }
    [Theory]
    [InlineData("original", 0f)][InlineData("no-joint-friction", .05f)]
    public void AnotherNativeFrictionCannotMasqueradeAsDeclaredSetting(string mode, float other)
    {
        var config = Config(); config.mechanicsMode = mode; var peer = new Peer(config);
        var geometry = JsonNode.Parse(peer.Current.geometryJson)!;
        foreach (var link in geometry["links"]!.AsArray()) link!["jointFriction"] = other;
        peer.Current.geometryJson = geometry.ToJsonString(); peer.Current.bodyFingerprint = Wire.Digest(peer.Current.geometryJson);
        Assert.Throws<InvalidOperationException>(() => peer.Host()); Assert.Empty(peer.Commands);
    }
    [Fact]
    public void MechanicalProvenanceCannotChangeDuringAnInspection()
    {
        var peer = new Peer(); var body = peer.Host();
        peer.Corrupt = reply => reply.mechanicsDiagnosticJson += " ";
        Assert.Throws<InvalidOperationException>(() => body.CaptureGeometry());
        Assert.True(body.Faulted); Assert.Equal(0, peer.PhysicalSteps);
    }
    [Fact]
    public void LegacyConfigurationCannotSilentlySelectNewMechanics()
    {
        var config = Config(); config.protocol = "full-avatar-tendon-wire-v2";
        Assert.Throws<InvalidOperationException>(() => Wire.Validate(config));
    }
}

using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class InputGatesAndVisualDispatchTests
{
    [Fact]
    public void Input_Gates_Update_Runtime_And_State()
    {
        var state = CreateState();

        var applied = state.TrySetInputGates(
            new InputGateControlRequest(AvatarVisionEnabled: false, SpontaneousSpikingEnabled: false),
            out var runtime,
            out var error);

        Assert.True(applied);
        Assert.Null(error);
        Assert.False(runtime.AvatarVisionEnabled);
        Assert.False(runtime.SpontaneousSpikingEnabled);
        Assert.False(state.IsAvatarVisionEnabled());
        Assert.False(state.IsSpontaneousSpikingEnabled());
    }

    [Fact]
    public void Input_Gates_Reject_NoOp_Request()
    {
        var state = CreateState();

        var applied = state.TrySetInputGates(
            new InputGateControlRequest(AvatarVisionEnabled: null, SpontaneousSpikingEnabled: null),
            out _,
            out var error);

        Assert.False(applied);
        Assert.Contains("At least one setting is required", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Avatar_Input_Source_Classification_Is_Stable()
    {
        Assert.True(AdminInputSource.IsAvatarSource(AdminInputSource.Normalize("avatar_vision")));
        Assert.True(AdminInputSource.IsAvatarSource(AdminInputSource.Normalize("avatar-object")));
        Assert.True(AdminInputSource.IsAvatarSource(AdminInputSource.Normalize("editor_webcam")));
        Assert.False(AdminInputSource.IsAvatarSource(AdminInputSource.Normalize("world_map_editor")));
    }

    private static SimulationState CreateState()
    {
        var state = new SimulationState();
        state.Configure(
            tickDurationMs: 1.0,
            registry: new Dictionary<StructureId, string>(),
            connectivity: new Dictionary<StructureId, List<SynapticConnection>>());
        return state;
    }

}

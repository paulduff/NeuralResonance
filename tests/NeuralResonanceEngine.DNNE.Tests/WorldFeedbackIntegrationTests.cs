using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;
using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class WorldFeedbackIntegrationTests
{
    [Fact]
    public void Avatar_Body_State_Request_Clamps_Raw_Receptor_Feedback()
    {
        var request = AvatarBodyStateInputFactory.CreateRequest(
            new AvatarBodyTelemetry(
                ForwardVelocity: 2.0,
                TurnRateDeg: -90.0,
                ContactLevel: 1.4,
                LeftMotorDrive: 0.7,
                RightMotorDrive: 0.1,
                Hunger: 1.8,
                Health: -0.2,
                TactileFront: 1.7,
                TactileLeft: -0.4,
                PainLevel: 1.3),
            new AvatarBodyStateProfile(
                MaxForwardSpeed: 2.7,
                MaxTurnRateDeg: 240.0,
                BaseIntensity: 0.25,
                MotionIntensityWeight: 0.35,
                TurnIntensityWeight: 0.18,
                ContactIntensityWeight: 0.38,
                BaseBurstCount: 6.0,
                MotionBurstWeight: 9.0,
                TurnBurstWeight: 5.0,
                ContactBurstWeight: 8.0));

        Assert.Equal(1.0f, request.Hunger);
        Assert.Equal(0.0f, request.Health);
        Assert.Equal(1.0f, request.TactileFront);
        Assert.Equal(0.0f, request.TactileLeft);
        Assert.Equal(1.0f, request.PainLevel);
    }

    [Fact]
    public void Body_State_Retains_Only_Raw_Receptor_Feedback()
    {
        var state = CreateState();
        state.AdvanceClockAndCreateTickSignal();

        var body = state.UpdateBodyState(
            forwardVelocity: 0.72f,
            turnRateDeg: -12f,
            contactLevel: 0.64f,
            tactileFront: 0.51f,
            tactileLeft: 0.18f,
            tactileRight: 0.27f,
            tactileGround: 0.83f,
            painLevel: 0.42f,
            hunger: 0.83f,
            health: 0.42f,
            leftMotorDrive: 0.3f,
            rightMotorDrive: 0.6f);

        Assert.Equal(0.72f, body.ForwardVelocity);
        Assert.Equal(0.64f, body.ContactLevel);
        Assert.Equal(0.51f, body.TactileFront);
        Assert.Equal(0.83f, body.Hunger);
        Assert.Equal(0.42f, body.Health);
        Assert.Equal(0.42f, body.PainLevel);
        Assert.Equal(state.Tick, body.LastInputTick);
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

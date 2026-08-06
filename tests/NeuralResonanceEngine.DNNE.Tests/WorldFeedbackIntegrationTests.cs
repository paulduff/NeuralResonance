using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;
using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class WorldFeedbackIntegrationTests
{
    [Fact]
    public void Avatar_Body_State_Request_Clamps_World_Feedback()
    {
        var request = AvatarBodyStateInputFactory.CreateRequest(
            new AvatarBodyTelemetry(
                ForwardVelocity: 2.0,
                TurnRateDeg: -90.0,
                ContactLevel: 1.4,
                LeftMotorDrive: 0.7,
                RightMotorDrive: 0.1,
                EnvironmentalDarkness: 1.5,
                ShelterNeed: -0.3,
                Anxiety: 0.8,
                Hunger: 1.8,
                PredatorThreat: 1.4,
                InShelter: -1.0,
                Health: 1.2,
                ShelterSafety: 0.65),
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

        Assert.Equal(1.0f, request.EnvironmentalDarkness);
        Assert.Equal(0.0f, request.ShelterNeed);
        Assert.Equal(0.8f, request.Anxiety);
        Assert.Equal(1.0f, request.Hunger);
        Assert.Equal(1.0f, request.PredatorThreat);
        Assert.Equal(0.0f, request.InShelter);
        Assert.Equal(1.0f, request.Health);
        Assert.Equal(0.65f, request.ShelterSafety);
    }

    [Fact]
    public void Environmental_State_Retains_World_Feedback_For_Limbic_Runtime()
    {
        var state = CreateState();
        state.AdvanceClockAndCreateTickSignal();

        var environment = state.UpdateEnvironmentalState(
            darkness: 0.72f,
            shelterNeed: 0.64f,
            anxiety: 0.51f,
            hunger: 0.83f,
            predatorThreat: 0.91f,
            inShelter: 0.0f,
            health: 0.42f,
            shelterSafety: 0.18f);

        Assert.Equal(0.72f, environment.Darkness);
        Assert.Equal(0.64f, environment.ShelterNeed);
        Assert.Equal(0.51f, environment.Anxiety);
        Assert.Equal(0.83f, environment.Hunger);
        Assert.Equal(0.91f, environment.PredatorThreat);
        Assert.Equal(0.0f, environment.InShelter);
        Assert.Equal(0.42f, environment.Health);
        Assert.Equal(0.18f, environment.ShelterSafety);
        Assert.Equal(state.Tick, environment.LastInputTick);
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

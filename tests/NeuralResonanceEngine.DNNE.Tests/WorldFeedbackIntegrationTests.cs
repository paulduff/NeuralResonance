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

    [Fact]
    public void Predator_Threat_Delays_Sleep_When_Avatar_Is_Exposed()
    {
        var exposed = CreateState();
        var sheltered = CreateState();

        exposed.UpdateEnvironmentalState(
            darkness: 1.0f,
            shelterNeed: 1.0f,
            anxiety: 1.0f,
            hunger: 0.0f,
            predatorThreat: 1.0f,
            inShelter: 0.0f,
            health: 1.0f,
            shelterSafety: 0.0f);
        sheltered.UpdateEnvironmentalState(
            darkness: 1.0f,
            shelterNeed: 1.0f,
            anxiety: 0.25f,
            hunger: 0.0f,
            predatorThreat: 0.0f,
            inShelter: 1.0f,
            health: 1.0f,
            shelterSafety: 1.0f);

        var exposedSleepTick = -1;
        var shelteredSleepTick = -1;
        for (var i = 0; i < 512; i++)
        {
            exposed.AdvanceClockAndCreateTickSignal();
            sheltered.AdvanceClockAndCreateTickSignal();
            exposed.UpdateEnvironmentalState(
                darkness: 1.0f,
                shelterNeed: 1.0f,
                anxiety: 1.0f,
                hunger: 0.0f,
                predatorThreat: 1.0f,
                inShelter: 0.0f,
                health: 1.0f,
                shelterSafety: 0.0f);
            sheltered.UpdateEnvironmentalState(
                darkness: 1.0f,
                shelterNeed: 1.0f,
                anxiety: 0.25f,
                hunger: 0.0f,
                predatorThreat: 0.0f,
                inShelter: 1.0f,
                health: 1.0f,
                shelterSafety: 1.0f);
            if (exposedSleepTick < 0 && Step(exposed).IsSleeping)
            {
                exposedSleepTick = i;
            }

            if (shelteredSleepTick < 0 && Step(sheltered).IsSleeping)
            {
                shelteredSleepTick = i;
            }

            if (shelteredSleepTick >= 0 && exposedSleepTick >= 0)
            {
                break;
            }
        }

        Assert.True(shelteredSleepTick >= 0);
        Assert.True(exposedSleepTick < 0 || exposedSleepTick > shelteredSleepTick);
    }

    private static SimulationState CreateState()
    {
        var state = new SimulationState();
        state.Configure(
            tickDurationMs: 1.0,
            registry: new Dictionary<StructureId, string>(),
            connectivity: new Dictionary<StructureId, List<SynapticConnection>>());
        state.UpdateNeuromod(
            new NeuromodState
            {
                DopamineLevel = 0.5f,
                SerotoninLevel = 0.5f,
                AcetylcholineLevel = 0.5f,
                NorepinephrineLevel = 0.5f
            },
            rewardPredictionError: 0.0f);
        return state;
    }

    private static SleepTransitionResult Step(SimulationState state)
        => state.AdvanceSleepHomeostasis(new SleepTickInput(
            DrainedSpikes: 34,
            DispatchedSpikes: 34,
            ActivePathways: 10,
            SpontaneousGenerated: 1,
            EngramsCaptured: 0,
            ReplayedEngrams: 0,
            ReplayDispatchedSpikes: 0));
}

using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class AvatarWorldDynamicsTests
{
    private static readonly AvatarPhysiologyOptions Options = new(
        NominalStoredEnergyJoules: 8_000_000.0,
        MetabolicBurnJoulesPerSecond: 33_600.0,
        HydrationLossPerSecond: 0.00022,
        EnergyDepletionStressEnter: 0.62,
        EnergyDepletionStressFull: 0.92,
        EnergyDamageRateMinimum: 0.0028,
        EnergyDamageRateScale: 0.0062,
        DehydrationDamageThreshold: 0.20,
        DehydrationDamageRateMinimum: 0.002,
        DehydrationDamageRateScale: 0.008,
        ShelteredSleepRecoveryRate: 0.010);

    [Fact]
    public void Repeated_Headless_Run_Is_Deterministic()
    {
        var first = RunScenario();
        var second = RunScenario();

        Assert.Equal(first, second);
        Assert.InRange(first.StoredEnergyJoules, 0.0, Options.NominalStoredEnergyJoules);
        Assert.InRange(first.HydrationFraction, 0.0, 1.0);
        Assert.InRange(first.TissueIntegrityFraction, 0.0, 1.0);
    }

    [Fact]
    public void Food_And_Water_Produce_Bounded_Physical_Consequences()
    {
        var initial = new AvatarPhysiologyState(7_500_000.0, 0.80, 0.90);

        var fed = AvatarWorldDynamics.ConsumeFood(initial, Options, nominalEnergyFraction: 0.35);
        var hydrated = AvatarWorldDynamics.Drink(fed, hydrationFraction: 0.38);

        Assert.Equal(Options.NominalStoredEnergyJoules, fed.StoredEnergyJoules);
        Assert.Equal(1.0, hydrated.HydrationFraction);
        Assert.Equal(initial.TissueIntegrityFraction, hydrated.TissueIntegrityFraction);
    }

    [Fact]
    public void Shelter_Recovery_Requires_Both_Sleep_And_Shelter()
    {
        var initial = new AvatarPhysiologyState(8_000_000.0, 1.0, 0.50);

        var awake = AvatarWorldDynamics.AdvancePhysiology(initial, Options, 10.0, 0.0, sleeping: false, inShelter: true);
        var exposed = AvatarWorldDynamics.AdvancePhysiology(initial, Options, 10.0, 0.0, sleeping: true, inShelter: false);
        var sheltered = AvatarWorldDynamics.AdvancePhysiology(initial, Options, 10.0, 0.0, sleeping: true, inShelter: true);

        Assert.Equal(0.50, awake.TissueIntegrityFraction);
        Assert.Equal(0.50, exposed.TissueIntegrityFraction);
        Assert.Equal(0.60, sheltered.TissueIntegrityFraction, precision: 10);
    }

    [Fact]
    public void Predator_Contact_Damages_Tissue_Without_Changing_Needs()
    {
        var initial = new AvatarPhysiologyState(4_000_000.0, 0.50, 1.0);

        var struck = AvatarWorldDynamics.ApplyPredatorContact(initial, 0.5, 0.08, 1.25);

        Assert.Equal(0.95, struck.TissueIntegrityFraction, precision: 10);
        Assert.Equal(initial.StoredEnergyJoules, struck.StoredEnergyJoules);
        Assert.Equal(initial.HydrationFraction, struck.HydrationFraction);
    }

    [Theory]
    [InlineData("left_foot")]
    [InlineData("left_foot_heel_medial")]
    [InlineData("left_foot_heel_lateral")]
    [InlineData("left_foot_forefoot_medial")]
    [InlineData("left_foot_forefoot_lateral")]
    [InlineData("right_foot_heel_medial")]
    [InlineData("right_foot_forefoot_lateral")]
    public void Ordinary_Plantar_Support_Does_Not_Damage_Tissue(string region)
    {
        var initial = new AvatarPhysiologyState(8_000_000.0, 1.0, 1.0);

        var supported = AvatarWorldDynamics.ApplyPhysicalContact(
            initial,
            new AvatarPhysicalContactExposure(
                region, 420.0, 18.0, 7_500.0, 600.0, 0.02));

        Assert.Equal(0.0, supported.DamageFraction);
        Assert.Equal(initial, supported.State);
        Assert.False(supported.ImpactEvent);
    }

    [Fact]
    public void Severe_Fine_Grained_Heel_Impact_Still_Damages_Tissue()
    {
        var initial = new AvatarPhysiologyState(8_000_000.0, 1.0, 1.0);

        var struck = AvatarWorldDynamics.ApplyPhysicalContact(
            initial,
            new AvatarPhysicalContactExposure(
                "right_foot_heel_lateral", 2_400.0, 80.0, 1_500.0, 0.02, 0.02));

        Assert.True(struck.ImpactEvent);
        Assert.True(struck.ImpactDamageFraction > 0.0);
        Assert.True(struck.State.TissueIntegrityFraction < initial.TissueIntegrityFraction);
    }

    [Fact]
    public void Severe_New_Impact_Produces_Immediate_Graded_Tissue_Damage()
    {
        var initial = new AvatarPhysiologyState(8_000_000.0, 1.0, 1.0);

        var struck = AvatarWorldDynamics.ApplyPhysicalContact(
            initial,
            new AvatarPhysicalContactExposure(
                "left_forearm", 2_400.0, 70.0, 1_100.0, 0.02, 0.02));

        Assert.InRange(struck.ImpactDamageFraction, 0.005, 0.02);
        Assert.Equal(struck.ImpactDamageFraction, struck.DamageFraction, precision: 10);
        Assert.True(struck.ImpactEvent);
        Assert.True(struck.State.TissueIntegrityFraction < initial.TissueIntegrityFraction);
    }

    [Fact]
    public void Sustained_NonFoot_Pressure_Accumulates_Slow_Local_Consequence()
    {
        var initial = new AvatarPhysiologyState(8_000_000.0, 1.0, 1.0);
        var early = AvatarWorldDynamics.ApplyPhysicalContact(
            initial,
            new AvatarPhysicalContactExposure(
                "right_hand", 300.0, 2.0, 1_200.0, 7.0, 0.02));
        var sustained = AvatarWorldDynamics.ApplyPhysicalContact(
            initial,
            new AvatarPhysicalContactExposure(
                "right_hand", 300.0, 2.0, 1_200.0, 35.0, 0.02));

        Assert.Equal(0.0, early.DamageFraction);
        Assert.InRange(sustained.SustainedPressureDamageFraction, 0.0, 0.00001);
        Assert.True(sustained.State.TissueIntegrityFraction < initial.TissueIntegrityFraction);
        Assert.False(sustained.ImpactEvent);
    }

    [Fact]
    public void Invalid_Physical_Contact_Is_Rejected()
    {
        var initial = new AvatarPhysiologyState(8_000_000.0, 1.0, 1.0);

        Assert.Throws<ArgumentOutOfRangeException>(() => AvatarWorldDynamics.ApplyPhysicalContact(
            initial,
            new AvatarPhysicalContactExposure(
                "left_hand", double.NaN, 0.0, 1_000.0, 1.0, 0.02)));
    }

    [Fact]
    public void Vital_Assessment_Uses_Only_Physical_State()
    {
        var viable = AvatarWorldDynamics.AssessVitalState(
            new AvatarPhysiologyState(Options.NominalStoredEnergyJoules, 1.0, 1.0),
            Options);
        var incapacitated = AvatarWorldDynamics.AssessVitalState(
            new AvatarPhysiologyState(0.0, 1.0, 1.0),
            Options);
        var dead = AvatarWorldDynamics.AssessVitalState(
            new AvatarPhysiologyState(Options.NominalStoredEnergyJoules, 1.0, 0.0),
            Options);

        Assert.Equal(AvatarVitalState.Viable, viable.State);
        Assert.Equal(1.0, viable.MotorCapacity);
        Assert.True(viable.CanInteract);
        Assert.Equal(AvatarVitalState.Incapacitated, incapacitated.State);
        Assert.Equal(0.0, incapacitated.MotorCapacity);
        Assert.False(incapacitated.CanInteract);
        Assert.Equal(AvatarVitalState.Dead, dead.State);
        Assert.Equal(0.0, dead.MotorCapacity);
        Assert.False(dead.CanInteract);
    }

    [Fact]
    public void Respawn_State_Restores_A_Bounded_Physical_Body()
    {
        var respawned = AvatarWorldDynamics.CreateRespawnState(Options);

        Assert.Equal(Options.NominalStoredEnergyJoules * 0.75, respawned.StoredEnergyJoules);
        Assert.Equal(0.75, respawned.HydrationFraction);
        Assert.Equal(1.0, respawned.TissueIntegrityFraction);
        Assert.Equal(AvatarVitalState.Viable, AvatarWorldDynamics.AssessVitalState(respawned, Options).State);
    }

    [Fact]
    public void Device_Inventory_Has_One_Physical_Capacity_And_Long_Range_Priority()
    {
        var inventory = default(AvatarDeviceInventory);

        Assert.True(inventory.TryCollect(AvatarDeviceRangeProfile.Short, 3, out inventory));
        Assert.True(inventory.TryCollect(AvatarDeviceRangeProfile.Long, 3, out inventory));
        Assert.True(inventory.TryCollect(AvatarDeviceRangeProfile.Short, 3, out inventory));
        Assert.False(inventory.TryCollect(AvatarDeviceRangeProfile.Long, 3, out var full));
        Assert.Equal(3, full.TotalCharges);
        Assert.Equal(AvatarDeviceRangeProfile.Long, full.ActiveProfile);

        Assert.True(full.TryDischarge(full.ActiveProfile, out var discharged));
        Assert.Equal(2, discharged.TotalCharges);
        Assert.Equal(AvatarDeviceRangeProfile.Short, discharged.ActiveProfile);
    }

    private static AvatarPhysiologyState RunScenario()
    {
        var state = new AvatarPhysiologyState(6_000_000.0, 0.75, 1.0);
        for (var step = 0; step < 10_000; step++)
        {
            state = AvatarWorldDynamics.AdvancePhysiology(
                state,
                Options,
                elapsedSeconds: 0.02,
                metabolicRateScale: 1.0,
                sleeping: step % 400 < 80,
                inShelter: step % 400 < 80);
            if (step == 3_000)
            {
                state = AvatarWorldDynamics.ConsumeFood(state, Options, nominalEnergyFraction: 0.35);
            }
            if (step == 6_000)
            {
                state = AvatarWorldDynamics.Drink(state, hydrationFraction: 0.38);
            }
            if (step % 1_250 == 0)
            {
                state = AvatarWorldDynamics.ApplyPredatorContact(state, 0.08, 0.08, 1.0);
            }
        }

        return state;
    }
}

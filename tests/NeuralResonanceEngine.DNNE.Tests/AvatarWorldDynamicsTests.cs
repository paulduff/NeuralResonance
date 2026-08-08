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

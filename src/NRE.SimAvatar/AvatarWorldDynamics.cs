namespace NRE.SimAvatar;

public readonly record struct AvatarPhysiologyState(
    double StoredEnergyJoules,
    double HydrationFraction,
    double TissueIntegrityFraction);

public enum AvatarVitalState
{
    Viable = 0,
    Incapacitated = 1,
    Dead = 2
}

public readonly record struct AvatarVitalAssessment(
    AvatarVitalState State,
    double MotorCapacity,
    bool CanInteract);

public readonly record struct AvatarPhysiologyOptions(
    double NominalStoredEnergyJoules,
    double MetabolicBurnJoulesPerSecond,
    double HydrationLossPerSecond,
    double EnergyDepletionStressEnter,
    double EnergyDepletionStressFull,
    double EnergyDamageRateMinimum,
    double EnergyDamageRateScale,
    double DehydrationDamageThreshold,
    double DehydrationDamageRateMinimum,
    double DehydrationDamageRateScale,
    double ShelteredSleepRecoveryRate)
{
    public void Validate()
    {
        if (!double.IsFinite(NominalStoredEnergyJoules) || NominalStoredEnergyJoules <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(NominalStoredEnergyJoules));
        }
        if (!double.IsFinite(MetabolicBurnJoulesPerSecond) || MetabolicBurnJoulesPerSecond < 0.0 ||
            !double.IsFinite(HydrationLossPerSecond) || HydrationLossPerSecond < 0.0 ||
            !double.IsFinite(EnergyDepletionStressEnter) || !double.IsFinite(EnergyDepletionStressFull) ||
            EnergyDepletionStressEnter < 0.0 || EnergyDepletionStressFull <= EnergyDepletionStressEnter ||
            EnergyDepletionStressFull > 1.0 ||
            !double.IsFinite(EnergyDamageRateMinimum) || EnergyDamageRateMinimum < 0.0 ||
            !double.IsFinite(EnergyDamageRateScale) || EnergyDamageRateScale < 0.0 ||
            !double.IsFinite(DehydrationDamageThreshold) || DehydrationDamageThreshold <= 0.0 ||
            DehydrationDamageThreshold > 1.0 ||
            !double.IsFinite(DehydrationDamageRateMinimum) || DehydrationDamageRateMinimum < 0.0 ||
            !double.IsFinite(DehydrationDamageRateScale) || DehydrationDamageRateScale < 0.0 ||
            !double.IsFinite(ShelteredSleepRecoveryRate) || ShelteredSleepRecoveryRate < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(AvatarPhysiologyOptions));
        }
    }
}

public static class AvatarWorldDynamics
{
    private const double TissueDeathThreshold = 0.000001;
    private const double TissueIncapacitationThreshold = 0.08;
    private const double EnergyIncapacitationThresholdJoules = 0.5;

    public static AvatarPhysiologyState AdvancePhysiology(
        AvatarPhysiologyState state,
        AvatarPhysiologyOptions options,
        double elapsedSeconds,
        double metabolicRateScale,
        bool sleeping,
        bool inShelter)
    {
        options.Validate();
        ValidateState(state);
        if (!double.IsFinite(elapsedSeconds) || elapsedSeconds < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        }
        if (!double.IsFinite(metabolicRateScale) || metabolicRateScale < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(metabolicRateScale));
        }

        var sleepScale = sleeping ? 0.72 : 1.0;
        var energy = Math.Clamp(
            state.StoredEnergyJoules - (elapsedSeconds * options.MetabolicBurnJoulesPerSecond * metabolicRateScale * sleepScale),
            0.0,
            options.NominalStoredEnergyJoules);
        var hydration = Math.Clamp(
            state.HydrationFraction - (elapsedSeconds * options.HydrationLossPerSecond * metabolicRateScale * sleepScale),
            0.0,
            1.0);
        var tissue = Math.Clamp(state.TissueIntegrityFraction, 0.0, 1.0);

        var energyDepletion = 1.0 - (energy / options.NominalStoredEnergyJoules);
        var energyStress = ComputeNeedDrive(
            energyDepletion,
            options.EnergyDepletionStressEnter,
            options.EnergyDepletionStressFull);
        if (energyStress > 0.0)
        {
            var rate = options.EnergyDamageRateMinimum + (energyStress * options.EnergyDamageRateScale);
            tissue = Math.Clamp(tissue - (elapsedSeconds * rate), 0.0, 1.0);
        }

        if (hydration < options.DehydrationDamageThreshold)
        {
            var dehydrationStress = Math.Clamp(
                (options.DehydrationDamageThreshold - hydration) / options.DehydrationDamageThreshold,
                0.0,
                1.0);
            var rate = options.DehydrationDamageRateMinimum +
                       (dehydrationStress * options.DehydrationDamageRateScale);
            tissue = Math.Clamp(tissue - (elapsedSeconds * rate), 0.0, 1.0);
        }

        if (sleeping && inShelter)
        {
            tissue = Math.Clamp(tissue + (elapsedSeconds * options.ShelteredSleepRecoveryRate), 0.0, 1.0);
        }

        return new AvatarPhysiologyState(energy, hydration, tissue);
    }

    public static AvatarPhysiologyState ConsumeFood(
        AvatarPhysiologyState state,
        AvatarPhysiologyOptions options,
        double nominalEnergyFraction)
    {
        options.Validate();
        ValidateState(state);
        if (!double.IsFinite(nominalEnergyFraction) || nominalEnergyFraction < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(nominalEnergyFraction));
        }

        return state with
        {
            StoredEnergyJoules = Math.Clamp(
                state.StoredEnergyJoules + (options.NominalStoredEnergyJoules * nominalEnergyFraction),
                0.0,
                options.NominalStoredEnergyJoules)
        };
    }

    public static AvatarPhysiologyState Drink(AvatarPhysiologyState state, double hydrationFraction)
    {
        ValidateState(state);
        if (!double.IsFinite(hydrationFraction) || hydrationFraction < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(hydrationFraction));
        }

        return state with
        {
            HydrationFraction = Math.Clamp(state.HydrationFraction + hydrationFraction, 0.0, 1.0)
        };
    }

    public static AvatarPhysiologyState ApplyPredatorContact(
        AvatarPhysiologyState state,
        double elapsedSeconds,
        double damageRatePerSecond,
        double speedScale)
    {
        ValidateState(state);
        if (!double.IsFinite(elapsedSeconds) || elapsedSeconds < 0.0 ||
            !double.IsFinite(damageRatePerSecond) || damageRatePerSecond < 0.0 ||
            !double.IsFinite(speedScale) || speedScale < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        }

        return state with
        {
            TissueIntegrityFraction = Math.Clamp(
                state.TissueIntegrityFraction - (elapsedSeconds * damageRatePerSecond * speedScale),
                0.0,
                1.0)
        };
    }

    public static AvatarVitalAssessment AssessVitalState(
        AvatarPhysiologyState state,
        AvatarPhysiologyOptions options)
    {
        options.Validate();
        ValidateState(state);

        var tissue = Math.Clamp(state.TissueIntegrityFraction, 0.0, 1.0);
        var energy = Math.Clamp(state.StoredEnergyJoules, 0.0, options.NominalStoredEnergyJoules);
        if (tissue <= TissueDeathThreshold)
        {
            return new AvatarVitalAssessment(AvatarVitalState.Dead, 0.0, CanInteract: false);
        }

        if (energy <= EnergyIncapacitationThresholdJoules || tissue <= TissueIncapacitationThreshold)
        {
            return new AvatarVitalAssessment(AvatarVitalState.Incapacitated, 0.0, CanInteract: false);
        }

        // This is physical capacity, not an action policy. Neural output remains the
        // sole source of direction and intent while failing tissue/energy can only
        // reduce the body's ability to express that output.
        var energyReserve = energy / options.NominalStoredEnergyJoules;
        var energyCapacity = Math.Clamp(energyReserve / 0.18, 0.0, 1.0);
        var tissueCapacity = Math.Clamp(tissue / 0.30, 0.0, 1.0);
        var motorCapacity = Math.Min(energyCapacity, tissueCapacity);
        return new AvatarVitalAssessment(AvatarVitalState.Viable, motorCapacity, CanInteract: true);
    }

    public static AvatarPhysiologyState CreateRespawnState(AvatarPhysiologyOptions options)
    {
        options.Validate();
        return new AvatarPhysiologyState(
            StoredEnergyJoules: options.NominalStoredEnergyJoules * 0.75,
            HydrationFraction: 0.75,
            TissueIntegrityFraction: 1.0);
    }

    private static double ComputeNeedDrive(double value, double enter, double full)
        => Math.Clamp((value - enter) / Math.Max(0.000001, full - enter), 0.0, 1.0);

    private static void ValidateState(AvatarPhysiologyState state)
    {
        if (!double.IsFinite(state.StoredEnergyJoules) ||
            !double.IsFinite(state.HydrationFraction) ||
            !double.IsFinite(state.TissueIntegrityFraction))
        {
            throw new ArgumentOutOfRangeException(nameof(state));
        }
    }
}

public enum AvatarDeviceRangeProfile
{
    None = 0,
    Short = 1,
    Long = 2
}

public readonly record struct AvatarDeviceInventory(int ShortCharges, int LongCharges)
{
    public int TotalCharges => ShortCharges + LongCharges;

    public AvatarDeviceRangeProfile ActiveProfile => LongCharges > 0
        ? AvatarDeviceRangeProfile.Long
        : ShortCharges > 0
            ? AvatarDeviceRangeProfile.Short
            : AvatarDeviceRangeProfile.None;

    public bool TryCollect(AvatarDeviceRangeProfile profile, int capacity, out AvatarDeviceInventory next)
    {
        Validate();
        if (capacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }
        if (profile == AvatarDeviceRangeProfile.None || TotalCharges >= capacity)
        {
            next = this;
            return false;
        }

        next = profile == AvatarDeviceRangeProfile.Long
            ? this with { LongCharges = LongCharges + 1 }
            : this with { ShortCharges = ShortCharges + 1 };
        return true;
    }

    public bool TryDischarge(AvatarDeviceRangeProfile profile, out AvatarDeviceInventory next)
    {
        Validate();
        if (profile == AvatarDeviceRangeProfile.Long && LongCharges > 0)
        {
            next = this with { LongCharges = LongCharges - 1 };
            return true;
        }
        if (profile == AvatarDeviceRangeProfile.Short && ShortCharges > 0)
        {
            next = this with { ShortCharges = ShortCharges - 1 };
            return true;
        }

        next = this;
        return false;
    }

    private void Validate()
    {
        if (ShortCharges < 0 || LongCharges < 0)
        {
            throw new InvalidOperationException("Device inventory cannot contain negative charges.");
        }
    }
}

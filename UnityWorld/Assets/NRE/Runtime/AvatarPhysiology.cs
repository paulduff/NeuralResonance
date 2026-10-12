// Generated from AvatarWorldDynamics.cs and HeadlessWorldRuntime.cs; do not edit.
// Equations SHA256 (UTF-8, LF): 9a6c5cecba668b69e04c2d22f9efc9d106ffc94b52d5e3028052f0e9bfaa26c0
using System;
namespace Nre.UnityWorld {

public readonly struct AvatarPhysiologyState
{
    public double StoredEnergyJoules { get; }
    public double HydrationFraction { get; }
    public double TissueIntegrityFraction { get; }
    public AvatarPhysiologyState(double StoredEnergyJoules, double HydrationFraction, double TissueIntegrityFraction) { this.StoredEnergyJoules = StoredEnergyJoules; this.HydrationFraction = HydrationFraction; this.TissueIntegrityFraction = TissueIntegrityFraction; }
}

public enum AvatarVitalState
{
    Viable = 0,
    Incapacitated = 1,
    Dead = 2
}

public readonly struct AvatarVitalAssessment
{
    public AvatarVitalState State { get; }
    public double MotorCapacity { get; }
    public bool CanInteract { get; }
    public AvatarVitalAssessment(AvatarVitalState State, double MotorCapacity, bool CanInteract) { this.State = State; this.MotorCapacity = MotorCapacity; this.CanInteract = CanInteract; }
}

public readonly struct AvatarPhysicalContactExposure
{
    public string Region { get; }
    public double ForceNewtons { get; }
    public double ImpulseNewtonSeconds { get; }
    public double ContactAreaSquareMillimeters { get; }
    public double ContinuousSeconds { get; }
    public double SampleSeconds { get; }
    public double ImpactImpulseNewtonSeconds { get; }
    public AvatarPhysicalContactExposure(string Region, double ForceNewtons, double ImpulseNewtonSeconds, double ContactAreaSquareMillimeters, double ContinuousSeconds, double SampleSeconds, double ImpactImpulseNewtonSeconds) { this.Region = Region; this.ForceNewtons = ForceNewtons; this.ImpulseNewtonSeconds = ImpulseNewtonSeconds; this.ContactAreaSquareMillimeters = ContactAreaSquareMillimeters; this.ContinuousSeconds = ContinuousSeconds; this.SampleSeconds = SampleSeconds; this.ImpactImpulseNewtonSeconds = ImpactImpulseNewtonSeconds; }
}

public readonly struct AvatarPhysicalContactDamageAssessment
{
    public AvatarPhysiologyState State { get; }
    public double DamageFraction { get; }
    public double ImpactDamageFraction { get; }
    public double SustainedPressureDamageFraction { get; }
    public bool ImpactEvent { get; }
    public bool SustainedPressureEpisodeBegan { get; }
    public AvatarPhysicalContactDamageAssessment(AvatarPhysiologyState State, double DamageFraction, double ImpactDamageFraction, double SustainedPressureDamageFraction, bool ImpactEvent, bool SustainedPressureEpisodeBegan) { this.State = State; this.DamageFraction = DamageFraction; this.ImpactDamageFraction = ImpactDamageFraction; this.SustainedPressureDamageFraction = SustainedPressureDamageFraction; this.ImpactEvent = ImpactEvent; this.SustainedPressureEpisodeBegan = SustainedPressureEpisodeBegan; }
}

public readonly struct AvatarPhysiologyOptions
{
    public double NominalStoredEnergyJoules { get; }
    public double MetabolicBurnJoulesPerSecond { get; }
    public double HydrationLossPerSecond { get; }
    public double EnergyDepletionStressEnter { get; }
    public double EnergyDepletionStressFull { get; }
    public double EnergyDamageRateMinimum { get; }
    public double EnergyDamageRateScale { get; }
    public double DehydrationDamageThreshold { get; }
    public double DehydrationDamageRateMinimum { get; }
    public double DehydrationDamageRateScale { get; }
    public double ShelteredSleepRecoveryRate { get; }
    public AvatarPhysiologyOptions(double NominalStoredEnergyJoules, double MetabolicBurnJoulesPerSecond, double HydrationLossPerSecond, double EnergyDepletionStressEnter, double EnergyDepletionStressFull, double EnergyDamageRateMinimum, double EnergyDamageRateScale, double DehydrationDamageThreshold, double DehydrationDamageRateMinimum, double DehydrationDamageRateScale, double ShelteredSleepRecoveryRate) { this.NominalStoredEnergyJoules = NominalStoredEnergyJoules; this.MetabolicBurnJoulesPerSecond = MetabolicBurnJoulesPerSecond; this.HydrationLossPerSecond = HydrationLossPerSecond; this.EnergyDepletionStressEnter = EnergyDepletionStressEnter; this.EnergyDepletionStressFull = EnergyDepletionStressFull; this.EnergyDamageRateMinimum = EnergyDamageRateMinimum; this.EnergyDamageRateScale = EnergyDamageRateScale; this.DehydrationDamageThreshold = DehydrationDamageThreshold; this.DehydrationDamageRateMinimum = DehydrationDamageRateMinimum; this.DehydrationDamageRateScale = DehydrationDamageRateScale; this.ShelteredSleepRecoveryRate = ShelteredSleepRecoveryRate; }

    public void Validate()
    {
        if (!(!double.IsNaN(NominalStoredEnergyJoules) && !double.IsInfinity(NominalStoredEnergyJoules)) || NominalStoredEnergyJoules <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(NominalStoredEnergyJoules));
        }
        if (!(!double.IsNaN(MetabolicBurnJoulesPerSecond) && !double.IsInfinity(MetabolicBurnJoulesPerSecond)) || MetabolicBurnJoulesPerSecond < 0.0 ||
            !(!double.IsNaN(HydrationLossPerSecond) && !double.IsInfinity(HydrationLossPerSecond)) || HydrationLossPerSecond < 0.0 ||
            !(!double.IsNaN(EnergyDepletionStressEnter) && !double.IsInfinity(EnergyDepletionStressEnter)) || !(!double.IsNaN(EnergyDepletionStressFull) && !double.IsInfinity(EnergyDepletionStressFull)) ||
            EnergyDepletionStressEnter < 0.0 || EnergyDepletionStressFull <= EnergyDepletionStressEnter ||
            EnergyDepletionStressFull > 1.0 ||
            !(!double.IsNaN(EnergyDamageRateMinimum) && !double.IsInfinity(EnergyDamageRateMinimum)) || EnergyDamageRateMinimum < 0.0 ||
            !(!double.IsNaN(EnergyDamageRateScale) && !double.IsInfinity(EnergyDamageRateScale)) || EnergyDamageRateScale < 0.0 ||
            !(!double.IsNaN(DehydrationDamageThreshold) && !double.IsInfinity(DehydrationDamageThreshold)) || DehydrationDamageThreshold <= 0.0 ||
            DehydrationDamageThreshold > 1.0 ||
            !(!double.IsNaN(DehydrationDamageRateMinimum) && !double.IsInfinity(DehydrationDamageRateMinimum)) || DehydrationDamageRateMinimum < 0.0 ||
            !(!double.IsNaN(DehydrationDamageRateScale) && !double.IsInfinity(DehydrationDamageRateScale)) || DehydrationDamageRateScale < 0.0 ||
            !(!double.IsNaN(ShelteredSleepRecoveryRate) && !double.IsInfinity(ShelteredSleepRecoveryRate)) || ShelteredSleepRecoveryRate < 0.0)
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
        if (!(!double.IsNaN(elapsedSeconds) && !double.IsInfinity(elapsedSeconds)) || elapsedSeconds < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        }
        if (!(!double.IsNaN(metabolicRateScale) && !double.IsInfinity(metabolicRateScale)) || metabolicRateScale < 0.0)
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
        if (!(!double.IsNaN(nominalEnergyFraction) && !double.IsInfinity(nominalEnergyFraction)) || nominalEnergyFraction < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(nominalEnergyFraction));
        }

        return new AvatarPhysiologyState(Math.Clamp(
                state.StoredEnergyJoules + (options.NominalStoredEnergyJoules * nominalEnergyFraction),
                0.0,
                options.NominalStoredEnergyJoules), state.HydrationFraction, state.TissueIntegrityFraction);
    }

    public static AvatarPhysiologyState Drink(AvatarPhysiologyState state, double hydrationFraction)
    {
        ValidateState(state);
        if (!(!double.IsNaN(hydrationFraction) && !double.IsInfinity(hydrationFraction)) || hydrationFraction < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(hydrationFraction));
        }

        return new AvatarPhysiologyState(state.StoredEnergyJoules, Math.Clamp(state.HydrationFraction + hydrationFraction, 0.0, 1.0), state.TissueIntegrityFraction);
    }

    public static AvatarPhysiologyState ApplyPredatorContact(
        AvatarPhysiologyState state,
        double elapsedSeconds,
        double damageRatePerSecond,
        double speedScale)
    {
        ValidateState(state);
        if (!(!double.IsNaN(elapsedSeconds) && !double.IsInfinity(elapsedSeconds)) || elapsedSeconds < 0.0 ||
            !(!double.IsNaN(damageRatePerSecond) && !double.IsInfinity(damageRatePerSecond)) || damageRatePerSecond < 0.0 ||
            !(!double.IsNaN(speedScale) && !double.IsInfinity(speedScale)) || speedScale < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        }

        return new AvatarPhysiologyState(state.StoredEnergyJoules, state.HydrationFraction, Math.Clamp(
                state.TissueIntegrityFraction - (elapsedSeconds * damageRatePerSecond * speedScale),
                0.0,
                1.0));
    }

    public static AvatarPhysicalContactDamageAssessment ApplyPhysicalContact(
        AvatarPhysiologyState state,
        AvatarPhysicalContactExposure exposure)
    {
        ValidateState(state);
        if (string.IsNullOrWhiteSpace(exposure.Region) ||
            !(!double.IsNaN(exposure.ForceNewtons) && !double.IsInfinity(exposure.ForceNewtons)) || exposure.ForceNewtons < 0.0 ||
            !(!double.IsNaN(exposure.ImpulseNewtonSeconds) && !double.IsInfinity(exposure.ImpulseNewtonSeconds)) || exposure.ImpulseNewtonSeconds < 0.0 ||
            !(!double.IsNaN(exposure.ContactAreaSquareMillimeters) && !double.IsInfinity(exposure.ContactAreaSquareMillimeters)) || exposure.ContactAreaSquareMillimeters < 0.0 ||
            !(!double.IsNaN(exposure.ContinuousSeconds) && !double.IsInfinity(exposure.ContinuousSeconds)) || exposure.ContinuousSeconds < 0.0 ||
            !(!double.IsNaN(exposure.SampleSeconds) && !double.IsInfinity(exposure.SampleSeconds)) || exposure.SampleSeconds < 0.0 ||
            !(!double.IsNaN(exposure.ImpactImpulseNewtonSeconds) && !double.IsInfinity(exposure.ImpactImpulseNewtonSeconds)) || exposure.ImpactImpulseNewtonSeconds < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(exposure));
        }

        var region = exposure.Region.Trim().ToLowerInvariant();
        var plantar =
            region.StartsWith("left_foot", StringComparison.Ordinal) ||
            region.StartsWith("right_foot", StringComparison.Ordinal);
        // Support force integrated over a sensory frame is not impact momentum.
        // Count an initial collision once, rather than on every sample of its
        // first 150 ms. Sustained contact still has its pressure consequences.
        var firstContactSample = exposure.SampleSeconds > 0.0 &&
            exposure.ContinuousSeconds <= exposure.SampleSeconds + 0.000001;
        var impactThreshold = region.Contains("head", StringComparison.Ordinal)
            ? 8.0
            : plantar
                ? 24.0
                : region.Contains("torso", StringComparison.Ordinal) ||
                  region.Contains("pelvis", StringComparison.Ordinal)
                    ? 14.0
                    : 11.0;
        var impactExcess = firstContactSample
            ? Math.Max(0.0, exposure.ImpactImpulseNewtonSeconds - impactThreshold)
            : 0.0;
        var impactDamage = Math.Min(
            0.025,
            Math.Pow(impactExcess / 90.0, 1.20) * 0.012);

        const double pressureGraceSeconds = 8.0;
        var pressure = exposure.ForceNewtons / Math.Max(1.0, exposure.ContactAreaSquareMillimeters);
        var pressureThreat = plantar
            ? 0.0
            : Math.Clamp((pressure - 0.045) / 0.20, 0.0, 1.0);
        var pressureDuration = Math.Clamp(
            (exposure.ContinuousSeconds - pressureGraceSeconds) / 52.0,
            0.0,
            1.0);
        var sustainedDamage = pressureThreat * pressureDuration * 0.00030 * exposure.SampleSeconds;
        var totalDamage = Math.Clamp(impactDamage + sustainedDamage, 0.0, 0.03);
        var next = new AvatarPhysiologyState(state.StoredEnergyJoules, state.HydrationFraction, Math.Clamp(
                state.TissueIntegrityFraction - totalDamage,
                0.0,
                1.0));
        var previousContinuousSeconds = Math.Max(0.0, exposure.ContinuousSeconds - exposure.SampleSeconds);
        return new AvatarPhysicalContactDamageAssessment(
            next,
            totalDamage,
            impactDamage,
            sustainedDamage,
            ImpactEvent: impactDamage > 0.0,
            SustainedPressureEpisodeBegan: sustainedDamage > 0.0 &&
                previousContinuousSeconds <= pressureGraceSeconds);
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
        if (!(!double.IsNaN(state.StoredEnergyJoules) && !double.IsInfinity(state.StoredEnergyJoules)) ||
            !(!double.IsNaN(state.HydrationFraction) && !double.IsInfinity(state.HydrationFraction)) ||
            !(!double.IsNaN(state.TissueIntegrityFraction) && !double.IsInfinity(state.TissueIntegrityFraction)))
        {
            throw new ArgumentOutOfRangeException(nameof(state));
        }
    }
}

public static class AvatarWorldDefaults {
    public static readonly AvatarPhysiologyOptions Options = new AvatarPhysiologyOptions(
        8_000_000.0,
        MetabolicBurnJoulesPerSecond: 3_360.0,
        HydrationLossPerSecond: 0.00022,
        EnergyDepletionStressEnter: 0.62,
        EnergyDepletionStressFull: 0.92,
        EnergyDamageRateMinimum: 0.0028,
        EnergyDamageRateScale: 0.0062,
        DehydrationDamageThreshold: 0.20,
        DehydrationDamageRateMinimum: 0.002,
        DehydrationDamageRateScale: 0.008,
        ShelteredSleepRecoveryRate: 0.010);
}
}

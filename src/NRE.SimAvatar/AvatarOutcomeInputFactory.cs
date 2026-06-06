using NeuralResonanceEngine.Shared.Contracts;

namespace NRE.SimAvatar;

public static class AvatarOutcomeInputFactory
{
    public static OutcomeInputRequest CreateRequest(AvatarOutcomeTelemetry telemetry)
    {
        var satietyRelief = Math.Clamp(telemetry.SatietyRelief, 0.0, 1.0);
        var safetyRelief = Math.Clamp(telemetry.SafetyRelief, 0.0, 1.0);
        var painLevel = Math.Clamp(telemetry.PainLevel, 0.0, 1.0);
        var damageLevel = Math.Clamp(telemetry.DamageLevel, 0.0, 1.0);
        var shelterComfort = Math.Clamp(telemetry.ShelterComfort, 0.0, 1.0);
        var progress = Math.Clamp(telemetry.Progress, 0.0, 1.0);
        var effortCost = Math.Clamp(telemetry.EffortCost, 0.0, 1.0);
        var novelty = Math.Clamp(telemetry.Novelty, 0.0, 1.0);
        var socialApproval = Math.Clamp(telemetry.SocialApproval, 0.0, 1.0);
        var appetitive = Math.Max(satietyRelief, Math.Max(safetyRelief, Math.Max(shelterComfort, Math.Max(progress, Math.Max(novelty * 0.65, socialApproval)))));
        var aversive = Math.Max(painLevel, Math.Max(damageLevel, effortCost * 0.75));
        var intensity = Math.Clamp(0.22 + (appetitive * 1.25) + (aversive * 1.45), 0.20, 3.0);
        var burstCount = Math.Clamp((int)Math.Round(8 + (appetitive * 24) + (aversive * 30)), 6, 64);

        return new OutcomeInputRequest(
            Pattern: string.IsNullOrWhiteSpace(telemetry.Pattern) ? AvatarRuntimeDefaults.OutcomePattern : telemetry.Pattern.Trim(),
            InputSource: string.IsNullOrWhiteSpace(telemetry.InputSource) ? AvatarRuntimeDefaults.OutcomeInputSource : telemetry.InputSource.Trim(),
            SatietyRelief: (float)satietyRelief,
            SafetyRelief: (float)safetyRelief,
            PainLevel: (float)painLevel,
            DamageLevel: (float)damageLevel,
            ShelterComfort: (float)shelterComfort,
            Progress: (float)progress,
            EffortCost: (float)effortCost,
            Novelty: (float)novelty,
            SocialApproval: (float)socialApproval,
            Intensity: (float)intensity,
            BurstCount: burstCount,
            Hemisphere: telemetry.Hemisphere);
    }
}

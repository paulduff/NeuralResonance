using NeuralResonanceEngine.Shared.Contracts;

namespace NRE.SimAvatar;

public static class AvatarBodyStateInputFactory
{
    public static BodyStateInputRequest CreateRequest(AvatarBodyTelemetry telemetry, AvatarBodyStateProfile profile)
    {
        var motion = Math.Clamp(Math.Abs(telemetry.ForwardVelocity) / profile.MaxForwardSpeed, 0.0, 1.0);
        var turn = Math.Clamp(Math.Abs(telemetry.TurnRateDeg) / profile.MaxTurnRateDeg, 0.0, 1.0);
        var contact = Math.Clamp(telemetry.ContactLevel, 0.0, 1.0);
        var tactileFront = Math.Clamp(telemetry.TactileFront, 0.0, 1.0);
        var tactileLeft = Math.Clamp(telemetry.TactileLeft, 0.0, 1.0);
        var tactileRight = Math.Clamp(telemetry.TactileRight, 0.0, 1.0);
        var tactileGround = Math.Clamp(telemetry.TactileGround, 0.0, 1.0);
        var tactileLoad = Math.Max(contact, Math.Max(tactileFront, Math.Max(tactileLeft, Math.Max(tactileRight, tactileGround * 0.35))));
        var intensity = Math.Clamp(
            profile.BaseIntensity +
            (profile.MotionIntensityWeight * motion) +
            (profile.TurnIntensityWeight * turn) +
            (profile.ContactIntensityWeight * tactileLoad),
            profile.MinIntensity,
            profile.MaxIntensity);

        var burstCount = Math.Clamp(
            (int)Math.Round(
                profile.BaseBurstCount +
                (profile.MotionBurstWeight * motion) +
                (profile.TurnBurstWeight * turn) +
                (profile.ContactBurstWeight * tactileLoad)),
            profile.MinBurstCount,
            profile.MaxBurstCount);

        return new BodyStateInputRequest(
            ForwardVelocity: (float)telemetry.ForwardVelocity,
            TurnRateDeg: (float)telemetry.TurnRateDeg,
            ContactLevel: (float)contact,
            LeftMotorDrive: (float)telemetry.LeftMotorDrive,
            RightMotorDrive: (float)telemetry.RightMotorDrive,
            Intensity: (float)intensity,
            BurstCount: burstCount,
            TargetStructure: profile.TargetStructure,
            SourceStructure: profile.SourceStructure,
            Hemisphere: null,
            IncludeVestibular: true,
            IncludeCerebellar: true,
            IsFeedback: true,
            Pattern: profile.Pattern,
            InputSource: profile.InputSource,
            EnvironmentalDarkness: (float)Math.Clamp(telemetry.EnvironmentalDarkness, 0.0, 1.0),
            ShelterNeed: (float)Math.Clamp(telemetry.ShelterNeed, 0.0, 1.0),
            Anxiety: (float)Math.Clamp(telemetry.Anxiety, 0.0, 1.0),
            Hunger: (float)Math.Clamp(telemetry.Hunger, 0.0, 1.0),
            PredatorThreat: (float)Math.Clamp(telemetry.PredatorThreat, 0.0, 1.0),
            InShelter: (float)Math.Clamp(telemetry.InShelter, 0.0, 1.0),
            Health: (float)Math.Clamp(telemetry.Health, 0.0, 1.0),
            ShelterSafety: (float)Math.Clamp(telemetry.ShelterSafety, 0.0, 1.0),
            TactileFront: (float)tactileFront,
            TactileLeft: (float)tactileLeft,
            TactileRight: (float)tactileRight,
            TactileGround: (float)tactileGround,
            PainLevel: (float)Math.Clamp(telemetry.PainLevel, 0.0, 1.0),
            Urgency: (float)Math.Clamp(telemetry.Urgency, 0.0, 1.0));
    }
}

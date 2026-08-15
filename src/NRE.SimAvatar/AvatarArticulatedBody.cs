using NeuralResonanceEngine.Shared.Contracts;

namespace NRE.SimAvatar;

/// <summary>
/// Deterministic musculoskeletal plant. Descending neuronal populations provide
/// excitation; antagonistic muscles, spinal coordination, joint mechanics, and
/// receptor measurements turn that excitation into movement.
/// </summary>
public sealed class AvatarArticulatedBody
{
    public const double MinimumShoulderAngleRadians = -0.70;
    public const double MaximumShoulderAngleRadians = 2.62;
    public const double MinimumShoulderAbductionRadians = -0.18;
    public const double MaximumShoulderAbductionRadians = 2.45;
    public const double MinimumElbowAngleRadians = 0.0;
    public const double MaximumElbowAngleRadians = 2.62;
    public const double MinimumNeckYawRadians = -1.35;
    public const double MaximumNeckYawRadians = 1.35;
    public const double MinimumNeckPitchRadians = -0.78;
    public const double MaximumNeckPitchRadians = 0.95;
    // ISB sagittal convention: flexion is positive and extension is negative.
    // Normal gait uses about 20 degrees of terminal-stance hip extension; the
    // wider passive range must not become the locomotor working envelope.
    public const double MinimumHipAngleRadians = -0.35;
    public const double MaximumHipAngleRadians = 2.09;
    public const double MinimumKneeAngleRadians = 0.0;
    public const double MaximumKneeAngleRadians = 2.45;
    public const double MinimumAnkleAngleRadians = -0.78;
    public const double MaximumAnkleAngleRadians = 0.52;

    private const double BodyWeightNewtons = 720.0;
    private const double MaximumManipulatorSpeedPerSecond = 1.65;
    private const double MaximumHandLoadNewtons = 450.0;
    private const double StandingHeightMeters = 1.74;
    private const double WalkingHipCenterRadians = 0.085;
    private const double WalkingHipExcursionRadians = 0.435;
    private const double SustainedRightingDriveThreshold = 0.08;

    private readonly AvatarMuscleJoint leftHip;
    private readonly AvatarMuscleJoint rightHip;
    private readonly AvatarMuscleJoint leftKnee;
    private readonly AvatarMuscleJoint rightKnee;
    private readonly AvatarMuscleJoint leftAnkle;
    private readonly AvatarMuscleJoint rightAnkle;
    private readonly AvatarMuscleJoint leftShoulder;
    private readonly AvatarMuscleJoint rightShoulder;
    private readonly AvatarMuscleJoint leftShoulderAbduction;
    private readonly AvatarMuscleJoint rightShoulderAbduction;
    private readonly AvatarMuscleJoint leftElbow;
    private readonly AvatarMuscleJoint rightElbow;
    private readonly AvatarMuscleJoint neckYaw;
    private readonly AvatarMuscleJoint neckPitch;
    private readonly AvatarMuscle rectusAbdominis = new("RectusAbdominis", "M", 1_100.0);
    private readonly AvatarMuscle erectorSpinae = new("ErectorSpinae", "M", 1_900.0);
    private readonly AvatarMuscle leftObliques = new("Obliques", "L", 760.0);
    private readonly AvatarMuscle rightObliques = new("Obliques", "R", 760.0);
    private readonly AvatarMuscle[] muscles;

    private double gaitPhase;
    private double manipulatorExtension;
    private double leftHandLoad;
    private double rightHandLoad;
    private double leftFootLoad;
    private double rightFootLoad;
    private double trunkPitch;
    private double trunkPitchVelocity;
    private double trunkRoll;
    private double trunkRollVelocity;
    private double postureBlend;
    private string posture = "standing";
    private double bodyHeight = StandingHeightMeters;
    private double uprightFraction = 1.0;
    private double supportFraction;
    private double balanceError;
    private AvatarBalanceState balanceState = AvatarBalanceState.Neutral;
    private PhysicalBalanceStateFrame balanceFrame = PhysicalBalanceStateFrame.Neutral;
    private readonly List<AvatarExternalBodyContact> pendingExternalContacts = [];
    private bool groundedState;
    private double pelvisGroundLoad;
    private double chestGroundLoad;
    private double headGroundLoad;
    private double leftKneeGroundLoad;
    private double rightKneeGroundLoad;
    private AvatarMechanicalOutput mechanicalOutput = new(0.0, 0.0, 0.0, 1.0, StandingHeightMeters);

    public AvatarArticulatedBody()
    {
        leftHip = CreateJoint("Iliopsoas", "GluteusMaximus", "L", 1_550, 2_350,
            MinimumHipAngleRadians, MaximumHipAngleRadians, 0.0, 2.6, 0.050, 42.0, 18.0);
        rightHip = CreateJoint("Iliopsoas", "GluteusMaximus", "R", 1_550, 2_350,
            MinimumHipAngleRadians, MaximumHipAngleRadians, 0.0, 2.6, 0.050, 42.0, 18.0);
        leftKnee = CreateJoint("Hamstrings", "Quadriceps", "L", 1_750, 3_200,
            MinimumKneeAngleRadians, MaximumKneeAngleRadians, 0.06, 1.7, 0.042, 34.0, 6.8);
        rightKnee = CreateJoint("Hamstrings", "Quadriceps", "R", 1_750, 3_200,
            MinimumKneeAngleRadians, MaximumKneeAngleRadians, 0.06, 1.7, 0.042, 34.0, 6.8);
        leftAnkle = CreateJoint("TibialisAnterior", "GastrocnemiusSoleus", "L", 720, 2_900,
            MinimumAnkleAngleRadians, MaximumAnkleAngleRadians, 0.0, 0.55, 0.038, 22.0, 3.8);
        rightAnkle = CreateJoint("TibialisAnterior", "GastrocnemiusSoleus", "R", 720, 2_900,
            MinimumAnkleAngleRadians, MaximumAnkleAngleRadians, 0.0, 0.55, 0.038, 22.0, 3.8);
        leftShoulder = CreateJoint("AnteriorDeltoid", "LatissimusDorsi", "L", 1_050, 1_250,
            MinimumShoulderAngleRadians, MaximumShoulderAngleRadians, 0.0, 0.52, 0.032, 9.0, 2.8);
        rightShoulder = CreateJoint("AnteriorDeltoid", "LatissimusDorsi", "R", 1_050, 1_250,
            MinimumShoulderAngleRadians, MaximumShoulderAngleRadians, 0.0, 0.52, 0.032, 9.0, 2.8);
        leftShoulderAbduction = CreateJoint("MiddleDeltoid", "PectoralisMajor", "L", 1_150, 1_420,
            MinimumShoulderAbductionRadians, MaximumShoulderAbductionRadians, 0.0, 0.46, 0.035, 11.0, 3.2);
        rightShoulderAbduction = CreateJoint("MiddleDeltoid", "PectoralisMajor", "R", 1_150, 1_420,
            MinimumShoulderAbductionRadians, MaximumShoulderAbductionRadians, 0.0, 0.46, 0.035, 11.0, 3.2);
        leftElbow = CreateJoint("BicepsBrachii", "TricepsBrachii", "L", 780, 1_050,
            MinimumElbowAngleRadians, MaximumElbowAngleRadians, 0.04, 0.22, 0.030, 7.0, 1.8);
        rightElbow = CreateJoint("BicepsBrachii", "TricepsBrachii", "R", 780, 1_050,
            MinimumElbowAngleRadians, MaximumElbowAngleRadians, 0.04, 0.22, 0.030, 7.0, 1.8);
        neckYaw = CreateJoint(
            "LeftSpleniusCapitisRightSternocleidomastoid",
            "RightSpleniusCapitisLeftSternocleidomastoid",
            "M", 540, 540,
            MinimumNeckYawRadians, MaximumNeckYawRadians, 0.0, 0.18, 0.021, 8.0, 2.4);
        neckPitch = CreateJoint(
            "SpleniusCapitisUpperTrapezius",
            "LongusColliSternocleidomastoid",
            "M", 680, 620,
            MinimumNeckPitchRadians, MaximumNeckPitchRadians, 0.0, 0.20, 0.020, 9.0, 2.6);
        muscles =
        [
            leftHip.Flexor, leftHip.Extensor, rightHip.Flexor, rightHip.Extensor,
            leftKnee.Flexor, leftKnee.Extensor, rightKnee.Flexor, rightKnee.Extensor,
            leftAnkle.Flexor, leftAnkle.Extensor, rightAnkle.Flexor, rightAnkle.Extensor,
            leftShoulder.Flexor, leftShoulder.Extensor, rightShoulder.Flexor, rightShoulder.Extensor,
            leftShoulderAbduction.Flexor, leftShoulderAbduction.Extensor,
            rightShoulderAbduction.Flexor, rightShoulderAbduction.Extensor,
            leftElbow.Flexor, leftElbow.Extensor, rightElbow.Flexor, rightElbow.Extensor,
            neckYaw.Flexor, neckYaw.Extensor, neckPitch.Flexor, neckPitch.Extensor,
            rectusAbdominis, erectorSpinae, leftObliques, rightObliques
        ];
    }

    public AvatarMechanicalOutput CurrentMechanicalOutput => mechanicalOutput;
    public string CurrentPosture => posture;

    public AvatarMechanicalOutput Advance(
        double deltaSeconds,
        double leftMotorDrive,
        double rightMotorDrive,
        double achievedForwardSpeed,
        double turnRateDegrees,
        double manipulatorDrive,
        bool grounded,
        bool movementBlocked,
        double standDrive = 0.0,
        double crouchDrive = 0.0,
        double sitDrive = 0.0,
        double lieDrive = 0.0,
        double headYawDrive = 0.0,
        double headPitchDrive = 0.0)
    {
        if (!double.IsFinite(deltaSeconds) || deltaSeconds <= 0.0)
        {
            return mechanicalOutput;
        }

        var dt = Math.Clamp(deltaSeconds, 0.001, 0.25);
        var descendingStandDrive = Math.Clamp(standDrive, 0.0, 1.0);
        var rightingRecoveryActive = grounded &&
            balanceState.Phase is (AvatarBalancePhase.Falling or
                AvatarBalancePhase.Fallen or
                AvatarBalancePhase.Righting) &&
            descendingStandDrive >= SustainedRightingDriveThreshold;
        var rightingRecruitment = rightingRecoveryActive
            ? descendingStandDrive
            : 0.0;
        var leftDrive = rightingRecoveryActive
            ? 0.0
            : Math.Clamp(leftMotorDrive, -1.0, 1.0);
        var rightDrive = rightingRecoveryActive
            ? 0.0
            : Math.Clamp(rightMotorDrive, -1.0, 1.0);
        var locomotorEffort = Math.Clamp((Math.Abs(leftDrive) + Math.Abs(rightDrive)) * 0.5, 0.0, 1.0);
        var postureCommand = rightingRecoveryActive
            ? ResolvePosture(descendingStandDrive, 0.0, 0.0, 0.0)
            : ResolvePosture(descendingStandDrive, crouchDrive, sitDrive, lieDrive);
        posture = postureCommand.Name;
        postureBlend = Approach(postureBlend, postureCommand.Blend, dt * 2.8);
        groundedState = grounded;

        if (locomotorEffort > 0.015 && postureCommand.Name is ("standing" or "crouching"))
        {
            gaitPhase = WrapRadians(gaitPhase + (dt * (1.4 + (locomotorEffort * 5.4))));
        }

        var leftCycle = Math.Sin(gaitPhase);
        var rightCycle = -leftCycle;
        var strideAmplitude = locomotorEffort *
            (postureCommand.Name == "crouching" ? 0.22 : WalkingHipExcursionRadians);
        var hipBase = postureCommand.Hip +
            (postureCommand.Name == "standing" ? locomotorEffort * WalkingHipCenterRadians : 0.0);
        var kneeBase = postureCommand.Knee;
        var ankleBase = postureCommand.Ankle;
        var legGain = Math.Clamp(0.36 + (locomotorEffort * 0.64) + (postureCommand.Blend * 0.28), 0.0, 1.0);
        var posturalTone = grounded
            ? 0.055 + (postureCommand.Blend * 0.045) + (rightingRecruitment * 0.55)
            : 0.025;

        leftHip.Advance(hipBase + (leftCycle * strideAmplitude), legGain, posturalTone, dt);
        rightHip.Advance(hipBase + (rightCycle * strideAmplitude), legGain, posturalTone, dt);
        leftKnee.Advance(kneeBase + (Math.Max(0.0, -leftCycle) * locomotorEffort * 0.82), legGain, posturalTone, dt);
        rightKnee.Advance(kneeBase + (Math.Max(0.0, -rightCycle) * locomotorEffort * 0.82), legGain, posturalTone, dt);
        leftAnkle.Advance(ankleBase - (leftHip.Angle * 0.34) + (leftKnee.Angle * 0.14), legGain, 0.04, dt);
        rightAnkle.Advance(ankleBase - (rightHip.Angle * 0.34) + (rightKnee.Angle * 0.14), legGain, 0.04, dt);

        var requestedExtension = Math.Clamp(manipulatorDrive, 0.0, 1.0);
        manipulatorExtension = Approach(
            manipulatorExtension,
            requestedExtension,
            MaximumManipulatorSpeedPerSecond * dt);
        var locomotorArmShare = 1.0 - manipulatorExtension;
        var armGain = Math.Clamp(0.28 + (locomotorEffort * 0.42) + (manipulatorExtension * 0.55), 0.0, 1.0);
        var armPosturalTone = 0.018 + (rightingRecruitment * 0.24);
        leftShoulder.Advance(
            (manipulatorExtension * 1.08) + (leftCycle * strideAmplitude * locomotorArmShare * 0.82),
            armGain, armPosturalTone, dt);
        rightShoulder.Advance(
            (manipulatorExtension * 1.08) + (rightCycle * strideAmplitude * locomotorArmShare * 0.82),
            armGain, armPosturalTone, dt);
        var lateralReachTarget = manipulatorExtension * 1.05;
        leftShoulderAbduction.Advance(lateralReachTarget, armGain, armPosturalTone, dt);
        rightShoulderAbduction.Advance(lateralReachTarget, armGain, armPosturalTone, dt);
        leftElbow.Advance(
            (manipulatorExtension * 0.30) + (Math.Max(0.0, -leftCycle) * locomotorEffort * locomotorArmShare * 0.34),
            armGain, 0.016, dt);
        rightElbow.Advance(
            (manipulatorExtension * 0.30) + (Math.Max(0.0, -rightCycle) * locomotorEffort * locomotorArmShare * 0.34),
            armGain, 0.016, dt);

        var neckGain = Math.Clamp(0.30 + (Math.Max(Math.Abs(headYawDrive), Math.Abs(headPitchDrive)) * 0.70), 0.0, 1.0);
        neckYaw.Advance(
            Math.Clamp(headYawDrive, -1.0, 1.0) * MaximumNeckYawRadians,
            neckGain,
            0.035,
            dt);
        neckPitch.Advance(
            Math.Clamp(headPitchDrive, -1.0, 1.0) * MaximumNeckPitchRadians,
            neckGain,
            0.040,
            dt);

        var coordinatedForwardSpeed = rightingRecoveryActive ? 0.0 : achievedForwardSpeed;
        var coordinatedTurnRate = rightingRecoveryActive ? 0.0 : turnRateDegrees;
        AdvanceAxialMuscles(
            postureCommand.TrunkPitch - (coordinatedForwardSpeed * 0.035),
            coordinatedTurnRate,
            rightingRecruitment,
            dt);

        var targetBodyHeight = Math.Clamp(
            postureCommand.Height -
            (((leftKnee.Angle + rightKnee.Angle) * 0.5 - postureCommand.Knee) * 0.08),
            0.28,
            StandingHeightMeters);
        var verticalTransitionRate = targetBodyHeight < bodyHeight ? 0.92 : 0.68;
        bodyHeight = Approach(bodyHeight, targetBodyHeight, verticalTransitionRate * dt);
        uprightFraction = Math.Clamp(1.0 - (Math.Abs(trunkPitch) / 1.45), 0.0, 1.0);

        leftHandLoad = Approach(leftHandLoad, 0.0, dt * 180.0);
        rightHandLoad = Approach(rightHandLoad, 0.0, dt * 180.0);
        var supportLoad = grounded ? BodyWeightNewtons : 0.0;
        var blockedLoad = movementBlocked ? locomotorEffort * 260.0 : 0.0;
        var bodySupportShare = ResolveBodySupportShare(postureCommand.Name, bodyHeight, trunkPitch);
        var footSupportLoad = supportLoad * (1.0 - bodySupportShare);
        var leftSupportShare = grounded ? Math.Clamp(0.5 + (leftCycle * locomotorEffort * 0.30), 0.12, 0.88) : 0.0;
        leftFootLoad = Math.Max(0.0, (footSupportLoad * leftSupportShare) + (blockedLoad * Math.Abs(leftDrive)));
        rightFootLoad = Math.Max(0.0, (footSupportLoad * (grounded ? 1.0 - leftSupportShare : 0.0)) +
            (blockedLoad * Math.Abs(rightDrive)));
        DistributeBodySupportLoad(postureCommand.Name, supportLoad * bodySupportShare);
        supportFraction = grounded
            ? Math.Clamp((supportLoad + blockedLoad) / BodyWeightNewtons, 0.0, 1.5)
            : 0.0;
        var rightingForceFraction = ResolveRightingForceFraction();
        var balance = AvatarBalanceDynamics.Advance(
            balanceState,
            CaptureFrame(),
            CaptureGroundContacts(),
            pendingExternalContacts.ToArray(),
            grounded,
            postureCommand.Name,
            bodyHeight,
            dt,
            rightingDrive: descendingStandDrive,
            rightingForceFraction: rightingForceFraction);
        pendingExternalContacts.Clear();
        balanceState = balance.State;
        balanceFrame = balance.Frame;
        balanceError = balance.BalanceError;
        uprightFraction = Math.Min(uprightFraction, balance.UprightFraction);
        if (balance.State.Phase is AvatarBalancePhase.Righting)
        {
            var rightingHeight = Math.Clamp(
                (targetBodyHeight * balance.UprightFraction) + (0.28 * (1.0 - balance.UprightFraction)),
                0.28,
                targetBodyHeight);
            bodyHeight = Approach(bodyHeight, rightingHeight, verticalTransitionRate * dt);
        }
        else
        {
            bodyHeight = Math.Min(bodyHeight, balance.BodyHeightMeters);
        }
        posture = balance.PhysicalPosture;

        var plantarCapacity = (leftAnkle.Extensor.ForceNewtons + rightAnkle.Extensor.ForceNewtons) /
            (leftAnkle.Extensor.MaximumIsometricForceNewtons + rightAnkle.Extensor.MaximumIsometricForceNewtons);
        var extensorCapacity = (leftKnee.Extensor.ForceNewtons + rightKnee.Extensor.ForceNewtons) /
            (leftKnee.Extensor.MaximumIsometricForceNewtons + rightKnee.Extensor.MaximumIsometricForceNewtons);
        var forceCapacity = Math.Clamp((plantarCapacity * 0.42) + (extensorCapacity * 0.58), 0.0, 1.0);
        var postureMobility = posture switch
        {
            "standing" => 1.0,
            "crouching" => 0.38,
            _ => 0.0
        };
        var movementCapacity = grounded
            ? Math.Clamp((0.22 + (forceCapacity * 1.18)) * uprightFraction * postureMobility, 0.0, 1.0)
            : 0.0;
        var signedDrive = Math.Clamp((leftDrive + rightDrive) * 0.5, -1.0, 1.0);
        var forwardSpeed = Math.Sign(signedDrive) * Math.Min(
            Math.Abs(achievedForwardSpeed),
            Math.Abs(signedDrive) * 1.8 * movementCapacity);
        var turnRate = Math.Clamp(turnRateDegrees * movementCapacity, -120.0, 120.0);
        mechanicalOutput = new AvatarMechanicalOutput(
            forwardSpeed,
            turnRate,
            supportFraction,
            uprightFraction,
            bodyHeight);
        return mechanicalOutput;
    }

    public void ApplyManipulatorContact(double loadNewtons, double bodyLocalLateralDirection)
    {
        var load = Math.Clamp(loadNewtons, 0.0, MaximumHandLoadNewtons);
        var lateral = Math.Clamp(bodyLocalLateralDirection, -1.0, 1.0);
        var rightShare = 0.5 + (lateral * 0.38);
        leftHandLoad = Math.Max(leftHandLoad, load * (1.0 - rightShare));
        rightHandLoad = Math.Max(rightHandLoad, load * rightShare);
    }

    public void ApplyExternalContact(AvatarExternalBodyContact contact)
    {
        if (!double.IsFinite(contact.ForceNewtons) || contact.ForceNewtons <= 0.0 ||
            !float.IsFinite(contact.BodyPosition.X) ||
            !float.IsFinite(contact.BodyPosition.Y) ||
            !float.IsFinite(contact.BodyPosition.Z) ||
            !float.IsFinite(contact.BodyNormal.X) ||
            !float.IsFinite(contact.BodyNormal.Y) ||
            !float.IsFinite(contact.BodyNormal.Z))
        {
            return;
        }

        if (pendingExternalContacts.Count >= 64)
        {
            pendingExternalContacts.RemoveAt(0);
        }
        pendingExternalContacts.Add(contact);
    }

    public IReadOnlyList<AvatarCollisionProbe> CaptureCollisionProbes()
    {
        if (posture == "lying" && bodyHeight < 0.78 && trunkPitch > 0.62)
        {
            return
            [
                new("head", 0.0, -0.46, 0.72, 0.18, true),
                new("chest", 0.0, -0.55, 0.28, 0.27, true),
                new("pelvis", 0.0, -0.62, -0.12, 0.25, true),
                new("left_knee", -0.15, -0.70, -0.48, 0.13, true),
                new("right_knee", 0.15, -0.70, -0.48, 0.13, true),
                new("left_foot", -0.14, -0.78, -0.82, 0.12, true),
                new("right_foot", 0.14, -0.78, -0.82, 0.12, true),
                new("left_hand", -0.38, -0.56, 0.18, 0.10, false),
                new("right_hand", 0.38, -0.56, 0.18, 0.10, false)
            ];
        }

        if (posture == "sitting" && bodyHeight < 1.08)
        {
            return
            [
                new("head", 0.0, 0.55, 0.02, 0.19, true),
                new("chest", 0.0, 0.18, 0.02, 0.28, true),
                new("pelvis", 0.0, -0.24, -0.08, 0.25, true),
                new("left_knee", -0.15, -0.42, 0.36, 0.13, true),
                new("right_knee", 0.15, -0.42, 0.36, 0.13, true),
                new("left_foot", -0.14, -0.66, 0.52, 0.12, true),
                new("right_foot", 0.14, -0.66, 0.52, 0.12, true),
                new("left_hand", -0.38, 0.02, 0.20, 0.10, false),
                new("right_hand", 0.38, 0.02, 0.20, 0.10, false)
            ];
        }

        var compression = Math.Clamp((StandingHeightMeters - bodyHeight) / 1.2, 0.0, 1.0);
        var footSwing = Math.Sin(gaitPhase) * 0.20;
        return
        [
            new("left_foot", -0.14, -0.90, footSwing, 0.12, true),
            new("right_foot", 0.14, -0.90, -footSwing, 0.12, true),
            new("left_shin", -0.14, -0.62 + (compression * 0.12), footSwing * 0.55, 0.11, true),
            new("right_shin", 0.14, -0.62 + (compression * 0.12), -footSwing * 0.55, 0.11, true),
            new("left_knee", -0.15, -0.35 + (compression * 0.18), 0.08, 0.13, true),
            new("right_knee", 0.15, -0.35 + (compression * 0.18), 0.08, 0.13, true),
            new("pelvis", 0.0, -0.08 - (compression * 0.28), 0.0, 0.25, true),
            new("chest", 0.0, 0.35 - (compression * 0.38), 0.04, 0.28, true),
            new("head", 0.0, 0.82 - (compression * 0.62), 0.02, 0.19, true),
            new("left_hand", -0.38, 0.20 - (compression * 0.30), 0.12 + (manipulatorExtension * 0.48), 0.10, false),
            new("right_hand", 0.38, 0.20 - (compression * 0.30), 0.12 + (manipulatorExtension * 0.48), 0.10, false)
        ];
    }

    public IReadOnlyList<AvatarGroundContactProbe> CaptureGroundContacts()
    {
        if (!groundedState)
        {
            return [];
        }

        var contacts = new List<AvatarGroundContactProbe>(7);
        AddGroundContact(contacts, "left_foot", -0.14, -0.90, 0.0, leftFootLoad, 6_200.0);
        AddGroundContact(contacts, "right_foot", 0.14, -0.90, 0.0, rightFootLoad, 6_200.0);
        AddGroundContact(contacts, "left_knee", -0.15, -0.70, -0.30, leftKneeGroundLoad, 7_200.0);
        AddGroundContact(contacts, "right_knee", 0.15, -0.70, -0.30, rightKneeGroundLoad, 7_200.0);
        AddGroundContact(contacts, "pelvis", 0.0, -0.62, -0.12, pelvisGroundLoad, 18_000.0);
        AddGroundContact(contacts, "chest", 0.0, -0.55, 0.28, chestGroundLoad, 24_000.0);
        AddGroundContact(contacts, "head", 0.0, -0.46, 0.72, headGroundLoad, 8_000.0);
        return contacts;
    }

    public PhysicalArticulationFrame CaptureFrame()
        => AvatarColliderRig.WithComputedSupportPlaneOffset(new PhysicalArticulationFrame(
            (float)leftHip.Angle,
            (float)rightHip.Angle,
            (float)leftKnee.Angle,
            (float)rightKnee.Angle,
            (float)leftAnkle.Angle,
            (float)rightAnkle.Angle,
            (float)leftFootLoad,
            (float)rightFootLoad,
            (float)leftShoulder.Angle,
            (float)rightShoulder.Angle,
            (float)leftElbow.Angle,
            (float)rightElbow.Angle,
            (float)leftHandLoad,
            (float)rightHandLoad,
            (float)manipulatorExtension,
            (float)trunkPitch,
            (float)trunkRoll,
            new MusculoskeletalStateFrame(
                posture,
                (float)bodyHeight,
                (float)uprightFraction,
                (float)supportFraction,
                (float)balanceError,
                muscles.Select(static muscle => muscle.Capture()).ToArray(),
                balanceFrame),
            (float)leftShoulderAbduction.Angle,
            (float)rightShoulderAbduction.Angle,
            (float)neckYaw.Angle,
            (float)neckPitch.Angle));

    public void Reset()
    {
        gaitPhase = 0.0;
        manipulatorExtension = 0.0;
        leftHandLoad = 0.0;
        rightHandLoad = 0.0;
        leftFootLoad = 0.0;
        rightFootLoad = 0.0;
        trunkPitch = 0.0;
        trunkPitchVelocity = 0.0;
        trunkRoll = 0.0;
        trunkRollVelocity = 0.0;
        postureBlend = 0.0;
        posture = "standing";
        bodyHeight = StandingHeightMeters;
        uprightFraction = 1.0;
        supportFraction = 0.0;
        balanceError = 0.0;
        balanceState = AvatarBalanceState.Neutral;
        balanceFrame = PhysicalBalanceStateFrame.Neutral;
        pendingExternalContacts.Clear();
        groundedState = false;
        pelvisGroundLoad = 0.0;
        chestGroundLoad = 0.0;
        headGroundLoad = 0.0;
        leftKneeGroundLoad = 0.0;
        rightKneeGroundLoad = 0.0;
        leftHip.Reset();
        rightHip.Reset();
        leftKnee.Reset();
        rightKnee.Reset();
        leftAnkle.Reset();
        rightAnkle.Reset();
        leftShoulder.Reset();
        rightShoulder.Reset();
        leftShoulderAbduction.Reset();
        rightShoulderAbduction.Reset();
        leftElbow.Reset();
        rightElbow.Reset();
        neckYaw.Reset();
        neckPitch.Reset();
        rectusAbdominis.Reset();
        erectorSpinae.Reset();
        leftObliques.Reset();
        rightObliques.Reset();
        mechanicalOutput = new AvatarMechanicalOutput(0.0, 0.0, 0.0, 1.0, StandingHeightMeters);
    }

    private void AdvanceAxialMuscles(
        double targetPitch,
        double turnRateDegrees,
        double rightingRecruitment,
        double dt)
    {
        var pitchError = Math.Clamp(targetPitch, -1.35, 1.35) - trunkPitch;
        var rightingPitch = Math.Clamp(balanceState.FallPitchRadians / 1.50, -1.0, 1.0);
        var rightingRoll = Math.Clamp(balanceState.FallRollRadians / 1.50, -1.0, 1.0);
        const double axialToneNewtons = 82.0;
        rectusAbdominis.Advance(
            (axialToneNewtons / rectusAbdominis.MaximumIsometricForceNewtons) +
            Math.Max(0.0, pitchError * 1.5) +
            (rightingRecruitment * Math.Max(0.0, -rightingPitch) * 0.72),
            1.0 - (trunkPitch * 0.16), dt);
        erectorSpinae.Advance(
            (axialToneNewtons / erectorSpinae.MaximumIsometricForceNewtons) +
            Math.Max(0.0, -pitchError * 1.5) +
            (rightingRecruitment * Math.Max(0.0, rightingPitch) * 0.72),
            1.0 + (trunkPitch * 0.16), dt);
        var pitchTorque = ((rectusAbdominis.ForceNewtons - erectorSpinae.ForceNewtons) * 0.018) -
            (trunkPitch * 28.0) - (trunkPitchVelocity * 7.0);
        trunkPitchVelocity = Math.Clamp(trunkPitchVelocity + ((pitchTorque / 4.8) * dt), -2.8, 2.8);
        trunkPitch = Math.Clamp(trunkPitch + (trunkPitchVelocity * dt), -0.35, 1.38);

        var targetRoll = Math.Clamp(-turnRateDegrees * 0.0022, -0.20, 0.20);
        var rollError = targetRoll - trunkRoll;
        leftObliques.Advance(
            0.028 + Math.Max(0.0, rollError * 2.1) +
            (rightingRecruitment * Math.Max(0.0, -rightingRoll) * 0.62),
            1.0 - (trunkRoll * 0.18), dt);
        rightObliques.Advance(
            0.028 + Math.Max(0.0, -rollError * 2.1) +
            (rightingRecruitment * Math.Max(0.0, rightingRoll) * 0.62),
            1.0 + (trunkRoll * 0.18), dt);
        var rollTorque = ((leftObliques.ForceNewtons - rightObliques.ForceNewtons) * 0.015) -
            (trunkRoll * 24.0) - (trunkRollVelocity * 6.0);
        trunkRollVelocity = Math.Clamp(trunkRollVelocity + ((rollTorque / 3.6) * dt), -2.2, 2.2);
        trunkRoll = Math.Clamp(trunkRoll + (trunkRollVelocity * dt), -0.48, 0.48);
    }

    private static AvatarMuscleJoint CreateJoint(
        string flexor,
        string extensor,
        string side,
        double flexorForce,
        double extensorForce,
        double minimum,
        double maximum,
        double rest,
        double inertia,
        double momentArm,
        double stiffness,
        double damping)
        => new(
            new AvatarMuscle(flexor, side, flexorForce),
            new AvatarMuscle(extensor, side, extensorForce),
            minimum,
            maximum,
            rest,
            inertia,
            momentArm,
            stiffness,
            damping);

    private static PostureCommand ResolvePosture(double stand, double crouch, double sit, double lie)
    {
        var drives = new[]
        {
            (Name: "standing", Drive: Math.Clamp(stand, 0.0, 1.0)),
            (Name: "crouching", Drive: Math.Clamp(crouch, 0.0, 1.0)),
            (Name: "sitting", Drive: Math.Clamp(sit, 0.0, 1.0)),
            (Name: "lying", Drive: Math.Clamp(lie, 0.0, 1.0))
        };
        var selected = drives.OrderByDescending(static item => item.Drive).First();
        if (selected.Drive < 0.04)
        {
            selected = ("standing", 0.40);
        }

        return selected.Name switch
        {
            "crouching" => new(selected.Name, selected.Drive, 0.48, 1.08, -0.18, 0.20, 1.12),
            "sitting" => new(selected.Name, selected.Drive, 1.18, 1.52, -0.05, 0.10, 0.78),
            "lying" => new(selected.Name, selected.Drive, 0.30, 0.30, 0.0, 1.28, 0.34),
            _ => new("standing", selected.Drive, 0.0, 0.06, 0.0, 0.0, StandingHeightMeters)
        };
    }

    private static double ResolveBodySupportShare(string postureName, double height, double pitch)
        => postureName switch
        {
            "sitting" => Math.Clamp((1.28 - height) / 0.50, 0.0, 0.58),
            "lying" => Math.Clamp(((0.96 - height) / 0.62) * Math.Clamp(pitch / 0.90, 0.0, 1.0), 0.0, 0.92),
            _ => 0.0
        };

    private double ResolveRightingForceFraction()
    {
        var hipExtension = 0.5 * (
            (leftHip.Extensor.ForceNewtons / leftHip.Extensor.MaximumIsometricForceNewtons) +
            (rightHip.Extensor.ForceNewtons / rightHip.Extensor.MaximumIsometricForceNewtons));
        var kneeExtension = 0.5 * (
            (leftKnee.Extensor.ForceNewtons / leftKnee.Extensor.MaximumIsometricForceNewtons) +
            (rightKnee.Extensor.ForceNewtons / rightKnee.Extensor.MaximumIsometricForceNewtons));
        var axialCorrection = Math.Max(
            rectusAbdominis.ForceNewtons / rectusAbdominis.MaximumIsometricForceNewtons,
            erectorSpinae.ForceNewtons / erectorSpinae.MaximumIsometricForceNewtons);
        var armSupport = 0.5 * (
            Math.Max(
                leftShoulder.Flexor.ForceNewtons / leftShoulder.Flexor.MaximumIsometricForceNewtons,
                leftShoulder.Extensor.ForceNewtons / leftShoulder.Extensor.MaximumIsometricForceNewtons) +
            Math.Max(
                rightShoulder.Flexor.ForceNewtons / rightShoulder.Flexor.MaximumIsometricForceNewtons,
                rightShoulder.Extensor.ForceNewtons / rightShoulder.Extensor.MaximumIsometricForceNewtons));

        return Math.Clamp(
            (hipExtension * 0.30) +
            (kneeExtension * 0.34) +
            (axialCorrection * 0.24) +
            (armSupport * 0.12),
            0.0,
            1.0);
    }

    private void DistributeBodySupportLoad(string postureName, double bodyLoad)
    {
        pelvisGroundLoad = 0.0;
        chestGroundLoad = 0.0;
        headGroundLoad = 0.0;
        leftKneeGroundLoad = 0.0;
        rightKneeGroundLoad = 0.0;

        if (bodyLoad <= 0.0)
        {
            return;
        }

        if (postureName == "sitting")
        {
            pelvisGroundLoad = bodyLoad * 0.78;
            leftKneeGroundLoad = bodyLoad * 0.11;
            rightKneeGroundLoad = bodyLoad * 0.11;
            return;
        }

        if (postureName == "lying")
        {
            pelvisGroundLoad = bodyLoad * 0.34;
            chestGroundLoad = bodyLoad * 0.49;
            headGroundLoad = bodyLoad * 0.07;
            leftKneeGroundLoad = bodyLoad * 0.05;
            rightKneeGroundLoad = bodyLoad * 0.05;
        }
    }

    private static void AddGroundContact(
        List<AvatarGroundContactProbe> contacts,
        string region,
        double bodyX,
        double bodyY,
        double bodyZ,
        double loadNewtons,
        double areaSquareMillimeters)
    {
        if (loadNewtons >= 0.5)
        {
            contacts.Add(new AvatarGroundContactProbe(
                region,
                bodyX,
                bodyY,
                bodyZ,
                loadNewtons,
                areaSquareMillimeters));
        }
    }

    private static double Approach(double current, double target, double maximumDelta)
    {
        var delta = target - current;
        return Math.Abs(delta) <= maximumDelta
            ? target
            : current + (Math.Sign(delta) * maximumDelta);
    }

    private static double WrapRadians(double value)
    {
        var wrapped = value % (Math.PI * 2.0);
        return wrapped < 0.0 ? wrapped + (Math.PI * 2.0) : wrapped;
    }

    private readonly record struct PostureCommand(
        string Name,
        double Blend,
        double Hip,
        double Knee,
        double Ankle,
        double TrunkPitch,
        double Height);
}

using System.Numerics;
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
    public const double MinimumHipAbductionRadians = -0.45;
    public const double MaximumHipAbductionRadians = 0.78;
    public const double MinimumKneeAngleRadians = 0.0;
    public const double MaximumKneeAngleRadians = 2.45;
    public const double MinimumAnkleAngleRadians = -0.78;
    public const double MaximumAnkleAngleRadians = 0.52;
    public const double MinimumAnkleRollRadians = -0.26;
    public const double MaximumAnkleRollRadians = 0.52;
    public const double MinimumTrunkYawRadians = -0.61;
    public const double MaximumTrunkYawRadians = 0.61;

    private const double BodyWeightNewtons = 720.0;
    private const double MaximumManipulatorSpeedPerSecond = 1.65;
    private const double MaximumHandLoadNewtons = 450.0;
    private const double StandingHeightMeters = 1.74;
    private const double WalkingHipFlexionRadians = 0.36;
    private const double WalkingHipExtensionRadians = 0.21;
    private const double WalkingKneeFlexionRadians = 0.62;
    private const double WalkingAnkleExcursionRadians = 0.16;
    private const double SustainedRightingDriveThreshold = 0.08;
    private const double RightingLocomotorRecruitment = 0.34;
    private const double HandWithdrawalOnsetNewtons = 90.0;
    private const double HandWithdrawalFullNewtons = 260.0;
    private const double SeatedSupportHoldSeconds = 0.18;
    private const double SeatedSupportMinimumForceNewtons = 8.0;
    private const double MaximumHipCoronalStabilizationTone = 0.20;
    private const float SeatedSupportMinimumNormalY = 0.55f;
    private const float SoleContactToleranceMeters = 0.025f;

    private readonly AvatarMuscleJoint leftHip;
    private readonly AvatarMuscleJoint rightHip;
    private readonly AvatarMuscleJoint leftHipAbduction;
    private readonly AvatarMuscleJoint rightHipAbduction;
    private readonly AvatarMuscleJoint leftKnee;
    private readonly AvatarMuscleJoint rightKnee;
    private readonly AvatarMuscleJoint leftAnkle;
    private readonly AvatarMuscleJoint rightAnkle;
    private readonly AvatarMuscleJoint leftAnkleRoll;
    private readonly AvatarMuscleJoint rightAnkleRoll;
    private readonly AvatarMuscleJoint leftShoulder;
    private readonly AvatarMuscleJoint rightShoulder;
    private readonly AvatarMuscleJoint leftShoulderAbduction;
    private readonly AvatarMuscleJoint rightShoulderAbduction;
    private readonly AvatarMuscleJoint leftElbow;
    private readonly AvatarMuscleJoint rightElbow;
    private readonly AvatarMuscleJoint neckYaw;
    private readonly AvatarMuscleJoint neckPitch;
    private readonly AvatarMuscleJoint trunkYaw;
    private readonly AvatarMuscle rectusAbdominis = new("RectusAbdominis", "M", 1_100.0);
    private readonly AvatarMuscle erectorSpinae = new("ErectorSpinae", "M", 1_900.0);
    private readonly AvatarMuscle leftObliques = new("Obliques", "L", 760.0);
    private readonly AvatarMuscle rightObliques = new("Obliques", "R", 760.0);
    private readonly AvatarMuscle[] muscles;
    private readonly AvatarSpinalLocomotorCircuit spinalLocomotor = new();

    private double leftGaitCycle;
    private double rightGaitCycle;
    private double manipulatorExtension;
    private double leftHandLoad;
    private double rightHandLoad;
    private double leftHandWithdrawal;
    private double rightHandWithdrawal;
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
    private double seatedSupportRemainingSeconds;
    private AvatarMechanicalOutput mechanicalOutput = new(0.0, 0.0, 0.0, 1.0, StandingHeightMeters);

    public AvatarArticulatedBody()
    {
        leftHip = CreateJoint("Iliopsoas", "GluteusMaximus", "L", 1_550, 2_350,
            MinimumHipAngleRadians, MaximumHipAngleRadians, 0.0, 2.6, 0.050, 42.0, 18.0);
        rightHip = CreateJoint("Iliopsoas", "GluteusMaximus", "R", 1_550, 2_350,
            MinimumHipAngleRadians, MaximumHipAngleRadians, 0.0, 2.6, 0.050, 42.0, 18.0);
        leftHipAbduction = CreateJoint("GluteusMedius", "AdductorGroup", "L", 1_650, 2_100,
            MinimumHipAbductionRadians, MaximumHipAbductionRadians, 0.0, 1.8, 0.048, 48.0, 15.0);
        rightHipAbduction = CreateJoint("GluteusMedius", "AdductorGroup", "R", 1_650, 2_100,
            MinimumHipAbductionRadians, MaximumHipAbductionRadians, 0.0, 1.8, 0.048, 48.0, 15.0);
        leftKnee = CreateJoint("Hamstrings", "Quadriceps", "L", 1_750, 3_200,
            MinimumKneeAngleRadians, MaximumKneeAngleRadians, 0.06, 1.7, 0.042, 34.0, 6.8);
        rightKnee = CreateJoint("Hamstrings", "Quadriceps", "R", 1_750, 3_200,
            MinimumKneeAngleRadians, MaximumKneeAngleRadians, 0.06, 1.7, 0.042, 34.0, 6.8);
        leftAnkle = CreateJoint("TibialisAnterior", "GastrocnemiusSoleus", "L", 720, 2_900,
            MinimumAnkleAngleRadians, MaximumAnkleAngleRadians, 0.0, 0.55, 0.038, 22.0, 3.8);
        rightAnkle = CreateJoint("TibialisAnterior", "GastrocnemiusSoleus", "R", 720, 2_900,
            MinimumAnkleAngleRadians, MaximumAnkleAngleRadians, 0.0, 0.55, 0.038, 22.0, 3.8);
        leftAnkleRoll = CreateJoint("TibialisPosterior", "FibularisLongusBrevis", "L", 1_050, 1_350,
            MinimumAnkleRollRadians, MaximumAnkleRollRadians, 0.0, 0.42, 0.032, 18.0, 3.2);
        rightAnkleRoll = CreateJoint("TibialisPosterior", "FibularisLongusBrevis", "R", 1_050, 1_350,
            MinimumAnkleRollRadians, MaximumAnkleRollRadians, 0.0, 0.42, 0.032, 18.0, 3.2);
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
        trunkYaw = CreateJoint(
            "RightExternalObliqueLeftInternalOblique",
            "LeftExternalObliqueRightInternalOblique",
            "M", 1_180, 1_180,
            MinimumTrunkYawRadians, MaximumTrunkYawRadians, 0.0, 3.4, 0.045, 26.0, 6.2);
        muscles =
        [
            leftHip.Flexor, leftHip.Extensor, rightHip.Flexor, rightHip.Extensor,
            leftHipAbduction.Flexor, leftHipAbduction.Extensor,
            rightHipAbduction.Flexor, rightHipAbduction.Extensor,
            leftKnee.Flexor, leftKnee.Extensor, rightKnee.Flexor, rightKnee.Extensor,
            leftAnkle.Flexor, leftAnkle.Extensor, rightAnkle.Flexor, rightAnkle.Extensor,
            leftAnkleRoll.Flexor, leftAnkleRoll.Extensor,
            rightAnkleRoll.Flexor, rightAnkleRoll.Extensor,
            leftShoulder.Flexor, leftShoulder.Extensor, rightShoulder.Flexor, rightShoulder.Extensor,
            leftShoulderAbduction.Flexor, leftShoulderAbduction.Extensor,
            rightShoulderAbduction.Flexor, rightShoulderAbduction.Extensor,
            leftElbow.Flexor, leftElbow.Extensor, rightElbow.Flexor, rightElbow.Extensor,
            neckYaw.Flexor, neckYaw.Extensor, neckPitch.Flexor, neckPitch.Extensor,
            trunkYaw.Flexor, trunkYaw.Extensor,
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
        double headPitchDrive = 0.0,
        double leftShoulderSagittalDrive = 0.0,
        double rightShoulderSagittalDrive = 0.0,
        double leftShoulderCoronalDrive = 0.0,
        double rightShoulderCoronalDrive = 0.0,
        double leftElbowDrive = 0.0,
        double rightElbowDrive = 0.0,
        double leftHipCoronalDrive = 0.0,
        double rightHipCoronalDrive = 0.0,
        double leftAnkleSagittalDrive = 0.0,
        double rightAnkleSagittalDrive = 0.0,
        double leftAnkleCoronalDrive = 0.0,
        double rightAnkleCoronalDrive = 0.0,
        double trunkYawDrive = 0.0)
    {
        if (!double.IsFinite(deltaSeconds) || deltaSeconds <= 0.0)
        {
            return mechanicalOutput;
        }

        var dt = Math.Clamp(deltaSeconds, 0.001, 0.25);
        seatedSupportRemainingSeconds = Math.Max(0.0, seatedSupportRemainingSeconds - dt);
        var seatedSupport = seatedSupportRemainingSeconds > 0.0 ||
            pendingExternalContacts.Any(IsSeatedSupportContact);
        var descendingStandDrive = Math.Clamp(standDrive, 0.0, 1.0);
        var rightingRecoveryActive = grounded &&
            balanceState.Phase is (AvatarBalancePhase.Falling or
                AvatarBalancePhase.Fallen or
                AvatarBalancePhase.Righting) &&
            descendingStandDrive >= SustainedRightingDriveThreshold;
        var rightingRecruitment = rightingRecoveryActive
            ? descendingStandDrive
            : 0.0;
        // Recovery does not choose a direction. It merely leaves a reduced
        // mechanical pathway available so a neuronal locomotor lane can move a
        // foot, knee, or support point while the stand/righting population is on.
        var recoveryMotionScale = rightingRecoveryActive ? RightingLocomotorRecruitment : 1.0;
        var leftDrive = Math.Clamp(leftMotorDrive, -1.0, 1.0) * recoveryMotionScale;
        var rightDrive = Math.Clamp(rightMotorDrive, -1.0, 1.0) * recoveryMotionScale;
        var locomotorEffort = Math.Clamp((Math.Abs(leftDrive) + Math.Abs(rightDrive)) * 0.5, 0.0, 1.0);
        var postureCommand = rightingRecoveryActive
            ? ResolvePosture(descendingStandDrive, 0.0, 0.0, 0.0)
            : ResolvePosture(descendingStandDrive, crouchDrive, sitDrive, lieDrive);
        var unsupportedSitDescent = postureCommand.Name == "sitting" && !seatedSupport;
        var physicalPostureName = unsupportedSitDescent
            ? "crouching"
            : postureCommand.Name;
        posture = physicalPostureName;
        postureBlend = Approach(postureBlend, postureCommand.Blend, dt * 2.8);
        groundedState = grounded;

        var canStep = !unsupportedSitDescent &&
            locomotorEffort > 0.015 &&
            physicalPostureName is ("standing" or "crouching");
        var preStepSoleSupport = ResolveSoleSupport(CaptureFrame(), grounded);
        var spinalState = spinalLocomotor.Advance(
            dt,
            canStep ? locomotorEffort : 0.0,
            preStepSoleSupport.Left ? Math.Clamp(leftFootLoad / BodyWeightNewtons, 0.0, 1.0) : 0.0,
            preStepSoleSupport.Right ? Math.Clamp(rightFootLoad / BodyWeightNewtons, 0.0, 1.0) : 0.0);
        leftGaitCycle = spinalState.LeftCycle;
        rightGaitCycle = spinalState.RightCycle;
        var leftSwing = spinalState.LeftFlexorRecruitment * locomotorEffort;
        var rightSwing = spinalState.RightFlexorRecruitment * locomotorEffort;
        var crouchStrideScale = postureCommand.Name == "crouching" ? 0.62 : 1.0;
        var signedGaitDirection = (double)Math.Sign(leftDrive + rightDrive);
        if (signedGaitDirection == 0)
        {
            signedGaitDirection = 1.0;
        }
        var leftHipGait = ResolveGaitHipTarget(leftGaitCycle, locomotorEffort, crouchStrideScale) *
            signedGaitDirection;
        var rightHipGait = ResolveGaitHipTarget(rightGaitCycle, locomotorEffort, crouchStrideScale) *
            signedGaitDirection;
        var hipBase = postureCommand.Hip;
        var kneeBase = postureCommand.Knee;
        var ankleBase = postureCommand.Ankle;
        var legGain = Math.Clamp(0.36 + (locomotorEffort * 0.64) + (postureCommand.Blend * 0.28), 0.0, 1.0);
        var posturalTone = grounded
            ? 0.055 + (postureCommand.Blend * 0.045) + (rightingRecruitment * 0.55)
            : 0.025;

        leftHip.Advance(hipBase + leftHipGait, legGain, posturalTone, dt);
        rightHip.Advance(hipBase + rightHipGait, legGain, posturalTone, dt);
        var leftHipCoronalTone = ResolveHipCoronalStabilizationTone(
            posturalTone,
            leftHipAbduction.Angle,
            physicalPostureName);
        var rightHipCoronalTone = ResolveHipCoronalStabilizationTone(
            posturalTone,
            rightHipAbduction.Angle,
            physicalPostureName);
        leftHipAbduction.Advance(
            SignedJointTarget(
                leftHipCoronalDrive,
                MinimumHipAbductionRadians,
                MaximumHipAbductionRadians),
            legGain, leftHipCoronalTone, dt);
        rightHipAbduction.Advance(
            SignedJointTarget(
                rightHipCoronalDrive,
                MinimumHipAbductionRadians,
                MaximumHipAbductionRadians),
            legGain, rightHipCoronalTone, dt);
        leftKnee.Advance(
            kneeBase + (leftSwing * WalkingKneeFlexionRadians * crouchStrideScale),
            legGain,
            posturalTone,
            dt);
        rightKnee.Advance(
            kneeBase + (rightSwing * WalkingKneeFlexionRadians * crouchStrideScale),
            legGain,
            posturalTone,
            dt);
        var ankleGain = Math.Clamp(
            0.24 + (legGain * 0.42) +
            (new[]
            {
                Math.Abs(leftAnkleSagittalDrive), Math.Abs(rightAnkleSagittalDrive),
                Math.Abs(leftAnkleCoronalDrive), Math.Abs(rightAnkleCoronalDrive)
            }.Max() * 0.58),
            0.0,
            1.0);
        var leftAnkleCoordination = canStep
            ? leftHip.Angle - leftKnee.Angle -
                (leftSwing * WalkingAnkleExcursionRadians) +
                (rightSwing * WalkingAnkleExcursionRadians * 0.25)
            : -(leftHip.Angle * 0.34) + (leftKnee.Angle * 0.14);
        var rightAnkleCoordination = canStep
            ? rightHip.Angle - rightKnee.Angle -
                (rightSwing * WalkingAnkleExcursionRadians) +
                (leftSwing * WalkingAnkleExcursionRadians * 0.25)
            : -(rightHip.Angle * 0.34) + (rightKnee.Angle * 0.14);
        leftAnkle.Advance(
            ankleBase + leftAnkleCoordination +
            SignedJointTarget(leftAnkleSagittalDrive, MinimumAnkleAngleRadians, MaximumAnkleAngleRadians),
            ankleGain, 0.04, dt);
        rightAnkle.Advance(
            ankleBase + rightAnkleCoordination +
            SignedJointTarget(rightAnkleSagittalDrive, MinimumAnkleAngleRadians, MaximumAnkleAngleRadians),
            ankleGain, 0.04, dt);
        leftAnkleRoll.Advance(
            SignedJointTarget(leftAnkleCoronalDrive, MinimumAnkleRollRadians, MaximumAnkleRollRadians),
            ankleGain, 0.035, dt);
        rightAnkleRoll.Advance(
            SignedJointTarget(rightAnkleCoronalDrive, MinimumAnkleRollRadians, MaximumAnkleRollRadians),
            ankleGain, 0.035, dt);

        leftHandWithdrawal = AdvanceWithdrawalReflex(leftHandWithdrawal, leftHandLoad, dt);
        rightHandWithdrawal = AdvanceWithdrawalReflex(rightHandWithdrawal, rightHandLoad, dt);
        var effectiveLeftShoulderSagittalDrive = leftShoulderSagittalDrive * (1.0 - leftHandWithdrawal);
        var effectiveRightShoulderSagittalDrive = rightShoulderSagittalDrive * (1.0 - rightHandWithdrawal);
        var effectiveLeftShoulderCoronalDrive = leftShoulderCoronalDrive * (1.0 - leftHandWithdrawal);
        var effectiveRightShoulderCoronalDrive = rightShoulderCoronalDrive * (1.0 - rightHandWithdrawal);
        var effectiveLeftElbowDrive = leftElbowDrive * (1.0 - leftHandWithdrawal);
        var effectiveRightElbowDrive = rightElbowDrive * (1.0 - rightHandWithdrawal);
        var requestedExtension = new[]
        {
            Math.Abs(effectiveLeftShoulderSagittalDrive), Math.Abs(effectiveRightShoulderSagittalDrive),
            Math.Abs(effectiveLeftShoulderCoronalDrive), Math.Abs(effectiveRightShoulderCoronalDrive),
            Math.Abs(effectiveLeftElbowDrive), Math.Abs(effectiveRightElbowDrive)
        }.Max();
        manipulatorExtension = Approach(
            manipulatorExtension,
            requestedExtension,
            MaximumManipulatorSpeedPerSecond * dt);
        var locomotorArmShare = 1.0 - manipulatorExtension;
        var armGain = Math.Clamp(0.28 + (locomotorEffort * 0.42) + (manipulatorExtension * 0.55), 0.0, 1.0);
        // With no descending or righting excitation the upper limbs are
        // relaxed. Passive joint mechanics still return them toward neutral.
        var armPosturalTone = rightingRecruitment * 0.24;
        leftShoulder.Advance(
            SignedJointTarget(
                effectiveLeftShoulderSagittalDrive,
                MinimumShoulderAngleRadians,
                MaximumShoulderAngleRadians) +
            (-leftHipGait * locomotorArmShare * 0.82),
            armGain, armPosturalTone, dt);
        rightShoulder.Advance(
            SignedJointTarget(
                effectiveRightShoulderSagittalDrive,
                MinimumShoulderAngleRadians,
                MaximumShoulderAngleRadians) +
            (-rightHipGait * locomotorArmShare * 0.82),
            armGain, armPosturalTone, dt);
        leftShoulderAbduction.Advance(
            SignedJointTarget(
                effectiveLeftShoulderCoronalDrive,
                MinimumShoulderAbductionRadians,
                MaximumShoulderAbductionRadians),
            armGain, armPosturalTone, dt);
        rightShoulderAbduction.Advance(
            SignedJointTarget(
                effectiveRightShoulderCoronalDrive,
                MinimumShoulderAbductionRadians,
                MaximumShoulderAbductionRadians),
            armGain, armPosturalTone, dt);
        leftElbow.Advance(
            SignedJointTarget(effectiveLeftElbowDrive, MinimumElbowAngleRadians, MaximumElbowAngleRadians) +
            (rightSwing * locomotorArmShare * 0.34),
            armGain, rightingRecruitment * 0.16, dt);
        rightElbow.Advance(
            SignedJointTarget(effectiveRightElbowDrive, MinimumElbowAngleRadians, MaximumElbowAngleRadians) +
            (leftSwing * locomotorArmShare * 0.34),
            armGain, rightingRecruitment * 0.16, dt);

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

        var coordinatedForwardSpeed = achievedForwardSpeed * recoveryMotionScale;
        var coordinatedTurnRate = turnRateDegrees * recoveryMotionScale;
        AdvanceAxialMuscles(
            postureCommand.TrunkPitch - (coordinatedForwardSpeed * 0.035),
            coordinatedTurnRate,
            (rightSwing - leftSwing),
            rightingRecruitment,
            trunkYawDrive,
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
        var externalHandSupportLoad = ResolveExternalHandSupportLoad(pendingExternalContacts, supportLoad);
        var bodyGroundSupportLoad = Math.Max(0.0, supportLoad - externalHandSupportLoad);
        var blockedLoad = movementBlocked ? locomotorEffort * 260.0 : 0.0;
        var supportPosture = ResolveSupportPosture(physicalPostureName, balanceState.Phase);
        var bodySupportShare = ResolveBodySupportShare(
            physicalPostureName,
            bodyHeight,
            trunkPitch,
            balanceState);
        var footSupportLoad = bodyGroundSupportLoad * (1.0 - bodySupportShare);
        var soleSupport = ResolveSoleSupport(CaptureFrame(), grounded);
        var leftSupportShare = ResolveLeftSoleSupportShare(
            soleSupport,
            leftSwing,
            rightSwing,
            locomotorEffort);
        var supportedFootLoad = soleSupport.HasSupport
            ? footSupportLoad + blockedLoad
            : 0.0;
        leftFootLoad = Math.Max(0.0, supportedFootLoad * leftSupportShare);
        rightFootLoad = Math.Max(
            0.0,
            supportedFootLoad * (soleSupport.HasSupport ? 1.0 - leftSupportShare : 0.0));
        var bodySupportLoad = bodyGroundSupportLoad * bodySupportShare;
        DistributeBodySupportLoad(supportPosture, bodySupportLoad);
        var assignedGroundLoad = supportedFootLoad + bodySupportLoad;
        supportFraction = grounded
            ? Math.Clamp((assignedGroundLoad + externalHandSupportLoad) / BodyWeightNewtons, 0.0, 1.5)
            : 0.0;
        var rightingForceFraction = ResolveRightingForceFraction();
        var balance = AvatarBalanceDynamics.Advance(
            balanceState,
            CaptureFrame(),
            CaptureGroundContacts(),
            pendingExternalContacts.ToArray(),
            grounded,
            physicalPostureName,
            bodyHeight,
            dt,
            rightingDrive: descendingStandDrive,
            rightingForceFraction: rightingForceFraction,
            locomotorEffort: locomotorEffort,
            commandedForwardSpeedMetersPerSecond: coordinatedForwardSpeed);
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
        var fatigueCapacity = ResolveLocomotorFatigueCapacity();
        var postureMobility = unsupportedSitDescent ? 0.0 : posture switch
        {
            "standing" => 1.0,
            "crouching" => 0.38,
            "righting" => 0.28,
            "falling" or "fallen" => rightingRecoveryActive ? 0.12 : 0.0,
            _ => 0.0
        };
        var movementCapacity = grounded
            ? Math.Clamp(
                (0.22 + (forceCapacity * 1.18)) * fatigueCapacity * uprightFraction * postureMobility,
                0.0,
                1.0)
            : 0.0;
        var signedDrive = Math.Clamp((leftDrive + rightDrive) * 0.5, -1.0, 1.0);
        var gaitPropulsion = ResolveGaitPropulsion(
            canStep,
            soleSupport,
            leftSwing,
            rightSwing);
        var forwardSpeed = Math.Sign(signedDrive) * Math.Min(
            Math.Abs(achievedForwardSpeed),
            Math.Abs(signedDrive) * 1.8 * movementCapacity * gaitPropulsion);
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

    /// <summary>
    /// Applies a physical collision load to the hand that actually contacted
    /// the world. Unlike a target interaction, a collision must never infer a
    /// second hand from a lateral coordinate.
    /// </summary>
    public void ApplyHandContact(string region, double loadNewtons)
    {
        var load = Math.Clamp(loadNewtons, 0.0, MaximumHandLoadNewtons);
        if (string.Equals(region, "left_hand", StringComparison.Ordinal))
        {
            leftHandLoad = Math.Max(leftHandLoad, load);
        }
        else if (string.Equals(region, "right_hand", StringComparison.Ordinal))
        {
            rightHandLoad = Math.Max(rightHandLoad, load);
        }
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
        if (IsSeatedSupportContact(contact))
        {
            seatedSupportRemainingSeconds = SeatedSupportHoldSeconds;
        }
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
        var leftFootSwing = leftGaitCycle * 0.20;
        var rightFootSwing = rightGaitCycle * 0.20;
        return
        [
            new("left_foot", -0.14, -0.90, leftFootSwing, 0.12, true),
            new("right_foot", 0.14, -0.90, rightFootSwing, 0.12, true),
            new("left_shin", -0.14, -0.62 + (compression * 0.12), leftFootSwing * 0.55, 0.11, true),
            new("right_shin", 0.14, -0.62 + (compression * 0.12), rightFootSwing * 0.55, 0.11, true),
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

        var articulation = CaptureFrame();
        var colliders = AvatarColliderRig.CaptureResolved(articulation);
        var candidates = colliders
            .Where(static collider =>
                collider.Chain is AvatarKinematicChain.Axial or
                    AvatarKinematicChain.LeftLeg or
                    AvatarKinematicChain.RightLeg)
            .ToArray();
        if (candidates.Length == 0)
        {
            return [];
        }

        var intendedLoad = leftFootLoad + rightFootLoad + leftKneeGroundLoad +
            rightKneeGroundLoad + pelvisGroundLoad + chestGroundLoad + headGroundLoad;
        if (intendedLoad < 0.5)
        {
            return [];
        }

        var leftLegBodyContacts = candidates.Count(static collider =>
            collider.Chain == AvatarKinematicChain.LeftLeg && collider.Region != "left_foot");
        var rightLegBodyContacts = candidates.Count(static collider =>
            collider.Chain == AvatarKinematicChain.RightLeg && collider.Region != "right_foot");
        var weights = candidates
            .Select(collider => NominalGroundLoad(collider, leftLegBodyContacts, rightLegBodyContacts))
            .ToArray();
        var totalWeight = weights.Sum();
        if (totalWeight < 0.5)
        {
            weights = candidates
                .Select(static collider => (double)Math.Max(0.001f, collider.EffectiveMassKilograms))
                .ToArray();
            totalWeight = weights.Sum();
        }

        return candidates
            .Select((collider, index) => new
            {
                Collider = collider,
                LoadNewtons = intendedLoad * (weights[index] / totalWeight)
            })
            .Where(static contact => contact.LoadNewtons > 0.0)
            .SelectMany(contact => CreateGroundContactProbes(
                contact.Collider,
                contact.LoadNewtons,
                articulation))
            .ToArray();
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
            NeckPitchRadians: (float)neckPitch.Angle,
            LeftHipAbductionRadians: (float)leftHipAbduction.Angle,
            RightHipAbductionRadians: (float)rightHipAbduction.Angle,
            LeftAnkleRollRadians: (float)leftAnkleRoll.Angle,
            RightAnkleRollRadians: (float)rightAnkleRoll.Angle,
            LeftFootPressure: CaptureFootPressure(leftFootLoad, leftAnkle.Angle, leftAnkleRoll.Angle),
            RightFootPressure: CaptureFootPressure(rightFootLoad, rightAnkle.Angle, rightAnkleRoll.Angle),
            TrunkYawRadians: (float)trunkYaw.Angle));

    public void ReconcileResolvedFrame(
        PhysicalArticulationFrame previousAccepted,
        PhysicalArticulationFrame proposed,
        PhysicalArticulationFrame resolved,
        double deltaSeconds)
    {
        ArgumentNullException.ThrowIfNull(previousAccepted);
        ArgumentNullException.ThrowIfNull(proposed);
        ArgumentNullException.ThrowIfNull(resolved);

        var dt = double.IsFinite(deltaSeconds)
            ? Math.Clamp(deltaSeconds, 0.001, 0.25)
            : 0.001;
        ReconcileJoint(leftHip, previousAccepted.LeftHipAngleRadians,
            proposed.LeftHipAngleRadians, resolved.LeftHipAngleRadians, dt);
        ReconcileJoint(rightHip, previousAccepted.RightHipAngleRadians,
            proposed.RightHipAngleRadians, resolved.RightHipAngleRadians, dt);
        ReconcileJoint(leftHipAbduction, previousAccepted.LeftHipAbductionRadians,
            proposed.LeftHipAbductionRadians, resolved.LeftHipAbductionRadians, dt);
        ReconcileJoint(rightHipAbduction, previousAccepted.RightHipAbductionRadians,
            proposed.RightHipAbductionRadians, resolved.RightHipAbductionRadians, dt);
        ReconcileJoint(leftKnee, previousAccepted.LeftKneeAngleRadians,
            proposed.LeftKneeAngleRadians, resolved.LeftKneeAngleRadians, dt);
        ReconcileJoint(rightKnee, previousAccepted.RightKneeAngleRadians,
            proposed.RightKneeAngleRadians, resolved.RightKneeAngleRadians, dt);
        ReconcileJoint(leftAnkle, previousAccepted.LeftAnkleAngleRadians,
            proposed.LeftAnkleAngleRadians, resolved.LeftAnkleAngleRadians, dt);
        ReconcileJoint(rightAnkle, previousAccepted.RightAnkleAngleRadians,
            proposed.RightAnkleAngleRadians, resolved.RightAnkleAngleRadians, dt);
        ReconcileJoint(leftAnkleRoll, previousAccepted.LeftAnkleRollRadians,
            proposed.LeftAnkleRollRadians, resolved.LeftAnkleRollRadians, dt);
        ReconcileJoint(rightAnkleRoll, previousAccepted.RightAnkleRollRadians,
            proposed.RightAnkleRollRadians, resolved.RightAnkleRollRadians, dt);
        ReconcileJoint(leftShoulder, previousAccepted.LeftShoulderAngleRadians,
            proposed.LeftShoulderAngleRadians, resolved.LeftShoulderAngleRadians, dt);
        ReconcileJoint(rightShoulder, previousAccepted.RightShoulderAngleRadians,
            proposed.RightShoulderAngleRadians, resolved.RightShoulderAngleRadians, dt);
        ReconcileJoint(leftShoulderAbduction, previousAccepted.LeftShoulderAbductionRadians,
            proposed.LeftShoulderAbductionRadians, resolved.LeftShoulderAbductionRadians, dt);
        ReconcileJoint(rightShoulderAbduction, previousAccepted.RightShoulderAbductionRadians,
            proposed.RightShoulderAbductionRadians, resolved.RightShoulderAbductionRadians, dt);
        ReconcileJoint(leftElbow, previousAccepted.LeftElbowAngleRadians,
            proposed.LeftElbowAngleRadians, resolved.LeftElbowAngleRadians, dt);
        ReconcileJoint(rightElbow, previousAccepted.RightElbowAngleRadians,
            proposed.RightElbowAngleRadians, resolved.RightElbowAngleRadians, dt);
        ReconcileJoint(neckYaw, previousAccepted.NeckYawRadians,
            proposed.NeckYawRadians, resolved.NeckYawRadians, dt);
        ReconcileJoint(neckPitch, previousAccepted.NeckPitchRadians,
            proposed.NeckPitchRadians, resolved.NeckPitchRadians, dt);
        ReconcileJoint(trunkYaw, previousAccepted.TrunkYawRadians,
            proposed.TrunkYawRadians, resolved.TrunkYawRadians, dt);

        if (RequiresReconciliation(proposed.TrunkPitchRadians, resolved.TrunkPitchRadians))
        {
            trunkPitch = Math.Clamp(resolved.TrunkPitchRadians, -0.35, 1.38);
            trunkPitchVelocity = Math.Clamp(
                (trunkPitch - previousAccepted.TrunkPitchRadians) / dt,
                -2.8,
                2.8);
            rectusAbdominis.ReconcileLength(1.0 - (trunkPitch * 0.16));
            erectorSpinae.ReconcileLength(1.0 + (trunkPitch * 0.16));
        }
        if (RequiresReconciliation(proposed.TrunkRollRadians, resolved.TrunkRollRadians))
        {
            trunkRoll = Math.Clamp(resolved.TrunkRollRadians, -0.48, 0.48);
            trunkRollVelocity = Math.Clamp(
                (trunkRoll - previousAccepted.TrunkRollRadians) / dt,
                -2.2,
                2.2);
            leftObliques.ReconcileLength(1.0 - (trunkRoll * 0.18));
            rightObliques.ReconcileLength(1.0 + (trunkRoll * 0.18));
        }

        manipulatorExtension = Math.Clamp(resolved.ManipulatorExtensionFraction, 0.0, 1.0);
        var resolvedBody = resolved.Musculoskeletal;
        if (resolvedBody is not null)
        {
            posture = resolvedBody.Posture;
            bodyHeight = Math.Clamp(resolvedBody.BodyHeightMeters, 0.15, 2.5);
            uprightFraction = Math.Clamp(resolvedBody.UprightFraction, 0.0, 1.0);
            supportFraction = Math.Clamp(resolvedBody.SupportFraction, 0.0, 1.5);
            balanceError = Math.Clamp(resolvedBody.BalanceError, 0.0, 1.0);
            if (resolvedBody.Balance is { } resolvedBalance)
            {
                balanceFrame = resolvedBalance;
                balanceState = balanceState with
                {
                    FallPitchRadians = resolvedBalance.FallPitchRadians,
                    FallRollRadians = resolvedBalance.FallRollRadians,
                    FallPitchVelocityRadiansPerSecond = resolvedBalance.FallPitchVelocityRadiansPerSecond,
                    FallRollVelocityRadiansPerSecond = resolvedBalance.FallRollVelocityRadiansPerSecond,
                    Phase = TryParseBalancePhase(resolvedBalance.Phase, out var phase)
                        ? phase
                        : balanceState.Phase
                };
            }
        }
    }

    internal static bool TryParseBalancePhase(string? value, out AvatarBalancePhase phase)
    {
        var normalized = new string((value ?? string.Empty)
            .Where(static character => character is not '_' and not '-' && !char.IsWhiteSpace(character))
            .ToArray());
        return Enum.TryParse(normalized, ignoreCase: true, out phase);
    }

    public void Reset()
    {
        leftGaitCycle = 0.0;
        rightGaitCycle = 0.0;
        spinalLocomotor.Reset();
        manipulatorExtension = 0.0;
        leftHandLoad = 0.0;
        rightHandLoad = 0.0;
        leftHandWithdrawal = 0.0;
        rightHandWithdrawal = 0.0;
        leftFootLoad = 0.0;
        rightFootLoad = 0.0;
        trunkPitch = 0.0;
        trunkPitchVelocity = 0.0;
        trunkRoll = 0.0;
        trunkRollVelocity = 0.0;
        trunkYaw.Reset();
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
        seatedSupportRemainingSeconds = 0.0;
        leftHip.Reset();
        rightHip.Reset();
        leftHipAbduction.Reset();
        rightHipAbduction.Reset();
        leftKnee.Reset();
        rightKnee.Reset();
        leftAnkle.Reset();
        rightAnkle.Reset();
        leftAnkleRoll.Reset();
        rightAnkleRoll.Reset();
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
        double gaitLateralPhase,
        double rightingRecruitment,
        double trunkYawDrive,
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

        var targetRoll = Math.Clamp(
            (-turnRateDegrees * 0.0022) - (gaitLateralPhase * 0.20),
            -0.24,
            0.24);
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

        var yawTarget = SignedJointTarget(
            trunkYawDrive,
            MinimumTrunkYawRadians,
            MaximumTrunkYawRadians);
        var yawGain = Math.Clamp(0.24 + (Math.Abs(trunkYawDrive) * 0.76), 0.0, 1.0);
        trunkYaw.Advance(yawTarget, yawGain, 0.035, dt);
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

    private static void ReconcileJoint(
        AvatarMuscleJoint joint,
        float previousAccepted,
        float proposed,
        float resolved,
        double dt)
    {
        if (RequiresReconciliation(proposed, resolved))
        {
            joint.Reconcile(previousAccepted, resolved, dt);
        }
    }

    private static bool RequiresReconciliation(float proposed, float resolved)
        => MathF.Abs(proposed - resolved) > 0.00001f;

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

    private static string ResolveSupportPosture(string commandedPosture, AvatarBalancePhase phase)
        => phase is AvatarBalancePhase.Falling or AvatarBalancePhase.Fallen or AvatarBalancePhase.Righting
            ? "lying"
            : commandedPosture;

    private static double ResolveBodySupportShare(
        string postureName,
        double height,
        double pitch,
        AvatarBalanceState balance)
    {
        if (balance.Phase is AvatarBalancePhase.Fallen)
        {
            return 1.0;
        }
        if (balance.Phase is AvatarBalancePhase.Falling or AvatarBalancePhase.Righting)
        {
            var physicalTilt = Math.Max(
                Math.Abs(balance.FallPitchRadians),
                Math.Abs(balance.FallRollRadians));
            return Math.Clamp(physicalTilt / 1.30, 0.0, 1.0);
        }

        return postureName switch
        {
            "sitting" => Math.Clamp((1.34 - height) / 0.56, 0.0, 0.72),
            // Once the body has physically descended, the axial and leg
            // surfaces carry its weight. Trunk pitch is not a valid reason to
            // preserve a fictitious plantar load after the feet are unloaded.
            "lying" => Math.Clamp((1.20 - height) / 0.75, 0.0, 1.0),
            _ => 0.0
        };
    }

    private static double ResolveExternalHandSupportLoad(
        IReadOnlyList<AvatarExternalBodyContact> contacts,
        double availableLoadNewtons)
    {
        if (availableLoadNewtons <= 0.0)
        {
            return 0.0;
        }

        var verticalSupport = contacts
            .Where(static contact =>
                (contact.Region is "left_hand" or "right_hand") &&
                contact.BodyNormal.Y > 0.55f)
            .Sum(static contact =>
                Math.Max(0.0, contact.ForceNewtons) * Math.Clamp(contact.BodyNormal.Y, 0f, 1f));
        return Math.Clamp(verticalSupport, 0.0, availableLoadNewtons);
    }

    private static bool IsSeatedSupportContact(AvatarExternalBodyContact contact)
        => (contact.Region is "pelvis" or "left_thigh" or "right_thigh") &&
           contact.BodyNormal.Y >= SeatedSupportMinimumNormalY &&
           contact.ForceNewtons >= SeatedSupportMinimumForceNewtons;

    private static double ResolveHipCoronalStabilizationTone(
        double baseTone,
        double hipCoronalAngle,
        string postureName)
    {
        if (postureName is not ("standing" or "crouching"))
        {
            return baseTone;
        }

        var availableExcursion = hipCoronalAngle >= 0.0
            ? MaximumHipAbductionRadians
            : Math.Abs(MinimumHipAbductionRadians);
        var excursion = Math.Clamp(
            Math.Abs(hipCoronalAngle) / Math.Max(0.01, availableExcursion),
            0.0,
            1.0);
        return Math.Clamp(
            baseTone + (excursion * MaximumHipCoronalStabilizationTone),
            0.0,
            1.0);
    }

    private static SoleSupportState ResolveSoleSupport(
        PhysicalArticulationFrame articulation,
        bool grounded)
    {
        if (!grounded)
        {
            return SoleSupportState.None;
        }

        var colliders = AvatarColliderRig.CaptureResolved(articulation);
        var leftFoot = colliders.FirstOrDefault(static collider => collider.Region == "left_foot");
        var rightFoot = colliders.FirstOrDefault(static collider => collider.Region == "right_foot");
        var contactHeight = AvatarColliderRig.LocalGroundPlaneY + SoleContactToleranceMeters;
        var leftInContact = leftFoot.Region is not null &&
            AvatarColliderRig.LowestSurfaceY(leftFoot) <= contactHeight;
        var rightInContact = rightFoot.Region is not null &&
            AvatarColliderRig.LowestSurfaceY(rightFoot) <= contactHeight;
        return new SoleSupportState(leftInContact, rightInContact);
    }

    private static double ResolveLeftSoleSupportShare(
        SoleSupportState support,
        double leftSwing,
        double rightSwing,
        double locomotorEffort)
    {
        if (support.Left && !support.Right)
        {
            return 1.0;
        }
        if (!support.Left)
        {
            return 0.0;
        }

        var alternatingTransfer = (rightSwing - leftSwing) *
            Math.Clamp(locomotorEffort, 0.0, 1.0) * 0.40;
        return Math.Clamp(0.5 + alternatingTransfer, 0.08, 0.92);
    }

    private static double ResolveGaitHipTarget(
        double cycle,
        double locomotorEffort,
        double strideScale)
    {
        var effort = Math.Clamp(locomotorEffort, 0.0, 1.0);
        var scale = effort * Math.Clamp(strideScale, 0.0, 1.0);
        return cycle >= 0.0
            ? cycle * WalkingHipFlexionRadians * scale
            : cycle * WalkingHipExtensionRadians * scale;
    }

    private static double ResolveGaitPropulsion(
        bool canStep,
        SoleSupportState support,
        double leftSwing,
        double rightSwing)
    {
        if (!canStep || !support.HasSupport)
        {
            return 0.0;
        }

        // The contralateral foot is the stance foot. Sole contact therefore
        // gates the spinal phase that can transfer muscle force into root
        // translation; an unsupported swing cannot propel the body.
        var leftStanceEvidence = support.Left ? rightSwing : 0.0;
        var rightStanceEvidence = support.Right ? leftSwing : 0.0;
        var alternatingSupport = Math.Max(leftStanceEvidence, rightStanceEvidence);
        var doubleSupport = support.Left && support.Right
            ? 1.0 - Math.Max(leftSwing, rightSwing)
            : 0.0;
        var coupledSupport = Math.Max(alternatingSupport, doubleSupport * 0.35);
        return Math.Clamp(0.18 + (coupledSupport * 0.82), 0.0, 1.0);
    }

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
        var ankleSagittal = 0.5 * (
            Math.Max(
                leftAnkle.Flexor.ForceNewtons / leftAnkle.Flexor.MaximumIsometricForceNewtons,
                leftAnkle.Extensor.ForceNewtons / leftAnkle.Extensor.MaximumIsometricForceNewtons) +
            Math.Max(
                rightAnkle.Flexor.ForceNewtons / rightAnkle.Flexor.MaximumIsometricForceNewtons,
                rightAnkle.Extensor.ForceNewtons / rightAnkle.Extensor.MaximumIsometricForceNewtons));
        var ankleCoronal = 0.5 * (
            Math.Max(
                leftAnkleRoll.Flexor.ForceNewtons / leftAnkleRoll.Flexor.MaximumIsometricForceNewtons,
                leftAnkleRoll.Extensor.ForceNewtons / leftAnkleRoll.Extensor.MaximumIsometricForceNewtons) +
            Math.Max(
                rightAnkleRoll.Flexor.ForceNewtons / rightAnkleRoll.Flexor.MaximumIsometricForceNewtons,
                rightAnkleRoll.Extensor.ForceNewtons / rightAnkleRoll.Extensor.MaximumIsometricForceNewtons));

        return Math.Clamp(
            (hipExtension * 0.26) +
            (kneeExtension * 0.30) +
            (axialCorrection * 0.20) +
            (armSupport * 0.10) +
            (ankleSagittal * 0.09) +
            (ankleCoronal * 0.05),
            0.0,
            1.0);
    }

    private double ResolveLocomotorFatigueCapacity()
    {
        AvatarMuscleJoint[] loadBearingJoints =
        [
            leftHip, rightHip,
            leftHipAbduction, rightHipAbduction,
            leftKnee, rightKnee,
            leftAnkle, rightAnkle,
            leftAnkleRoll, rightAnkleRoll
        ];
        var logCapacity = 0.0;
        foreach (var joint in loadBearingJoints)
        {
            // Cyclic bipedal motion needs both directions of each joint. A fully
            // exhausted antagonist therefore removes that joint's locomotor
            // contribution instead of leaving root translation with a hidden
            // reserve that the muscle plant cannot supply.
            var pairCapacity = Math.Sqrt(
                joint.Flexor.FatigueCapacityFraction *
                joint.Extensor.FatigueCapacityFraction);
            if (pairCapacity <= 0.0001)
            {
                return 0.0;
            }
            logCapacity += Math.Log(pairCapacity);
        }

        return Math.Clamp(Math.Exp(logCapacity / loadBearingJoints.Length), 0.0, 1.0);
    }

    private static PhysicalFootPressureFrame CaptureFootPressure(
        double loadNewtons,
        double anklePitchRadians,
        double ankleRollRadians)
    {
        var load = Math.Max(0.0, loadNewtons);
        var pitchScale = anklePitchRadians >= 0.0
            ? Math.Max(0.001, MaximumAnkleAngleRadians)
            : Math.Max(0.001, -MinimumAnkleAngleRadians);
        var rollScale = ankleRollRadians >= 0.0
            ? Math.Max(0.001, MaximumAnkleRollRadians)
            : Math.Max(0.001, -MinimumAnkleRollRadians);
        var heelShare = Math.Clamp(0.5 + (anklePitchRadians / pitchScale * 0.5), 0.0, 1.0);
        var lateralShare = Math.Clamp(0.5 + (ankleRollRadians / rollScale * 0.5), 0.0, 1.0);
        var forefootShare = 1.0 - heelShare;
        var medialShare = 1.0 - lateralShare;
        return new PhysicalFootPressureFrame(
            HeelMedialLoadNewtons: (float)(load * heelShare * medialShare),
            HeelLateralLoadNewtons: (float)(load * heelShare * lateralShare),
            ForefootMedialLoadNewtons: (float)(load * forefootShare * medialShare),
            ForefootLateralLoadNewtons: (float)(load * forefootShare * lateralShare));
    }

    private static IEnumerable<AvatarGroundContactProbe> CreateGroundContactProbes(
        AvatarBodyCollider collider,
        double loadNewtons,
        PhysicalArticulationFrame articulation)
    {
        var isLeftFoot = collider.Region == "left_foot";
        var isRightFoot = collider.Region == "right_foot";
        if (!isLeftFoot && !isRightFoot)
        {
            var supportPoint = AvatarColliderRig.LowestSurfacePoint(collider);
            yield return new AvatarGroundContactProbe(
                collider.Region,
                supportPoint.X,
                supportPoint.Y,
                supportPoint.Z,
                loadNewtons,
                collider.ContactAreaSquareMillimeters);
            yield break;
        }

        var pressure = (isLeftFoot ? articulation.LeftFootPressure : articulation.RightFootPressure) ??
            PhysicalFootPressureFrame.Unloaded;
        var fields = new (string Name, float X, float Z, float Load)[]
        {
            ("heel_medial", isLeftFoot ? 0.055f : -0.055f, -0.105f, pressure.HeelMedialLoadNewtons),
            ("heel_lateral", isLeftFoot ? -0.055f : 0.055f, -0.105f, pressure.HeelLateralLoadNewtons),
            ("forefoot_medial", isLeftFoot ? 0.055f : -0.055f, 0.105f, pressure.ForefootMedialLoadNewtons),
            ("forefoot_lateral", isLeftFoot ? -0.055f : 0.055f, 0.105f, pressure.ForefootLateralLoadNewtons)
        };
        var measuredTotal = fields.Sum(static field => Math.Max(0f, field.Load));
        var scale = measuredTotal >= 0.001f ? loadNewtons / measuredTotal : 0.0;
        foreach (var field in fields)
        {
            var fieldLoad = Math.Max(0.0, field.Load * scale);
            if (fieldLoad <= 0.0)
            {
                continue;
            }

            var point = collider.Position + Vector3.Transform(
                new Vector3(field.X, -collider.Size.Y * 0.5f, field.Z),
                collider.Orientation);
            yield return new AvatarGroundContactProbe(
                $"{collider.Region}_{field.Name}",
                point.X,
                point.Y,
                point.Z,
                fieldLoad,
                collider.ContactAreaSquareMillimeters * 0.25);
        }
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

    private double NominalGroundLoad(
        AvatarBodyCollider collider,
        int leftLegBodyContacts,
        int rightLegBodyContacts) => collider.Region switch
        {
            "left_foot" => leftFootLoad,
            "right_foot" => rightFootLoad,
            "pelvis" => pelvisGroundLoad,
            "chest" => chestGroundLoad,
            "head" => headGroundLoad,
            _ when collider.Chain == AvatarKinematicChain.LeftLeg && leftLegBodyContacts > 0 =>
                leftKneeGroundLoad / leftLegBodyContacts,
            _ when collider.Chain == AvatarKinematicChain.RightLeg && rightLegBodyContacts > 0 =>
                rightKneeGroundLoad / rightLegBodyContacts,
            _ => 0.0
        };

    private static double Approach(double current, double target, double maximumDelta)
    {
        var delta = target - current;
        return Math.Abs(delta) <= maximumDelta
            ? target
            : current + (Math.Sign(delta) * maximumDelta);
    }

    private static double AdvanceWithdrawalReflex(double current, double handLoadNewtons, double dt)
    {
        var target = Math.Clamp(
            (handLoadNewtons - HandWithdrawalOnsetNewtons) /
            (HandWithdrawalFullNewtons - HandWithdrawalOnsetNewtons),
            0.0,
            1.0);
        var rate = target > current ? 5.0 : 1.4;
        return Approach(current, target, rate * dt);
    }

    private static double SignedJointTarget(double drive, double minimum, double maximum)
    {
        var bounded = Math.Clamp(drive, -1.0, 1.0);
        return bounded >= 0.0 ? bounded * maximum : -bounded * minimum;
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

    private readonly record struct SoleSupportState(bool Left, bool Right)
    {
        public static SoleSupportState None => new(false, false);

        public bool HasSupport => Left || Right;
    }
}

using NeuralResonanceEngine.Shared.Contracts;

internal sealed record VestibularLabyrinthActivations(
    IReadOnlyList<(string Receptor, float Activation)> Left,
    IReadOnlyList<(string Receptor, float Activation)> Right);

/// <summary>
/// Converts skull kinematics into bilateral vestibular hair-cell activity.
/// The mechanics expose receptor evidence only; downstream neuronal circuits
/// retain all authority over posture, righting, and movement.
/// </summary>
internal sealed class VestibularLabyrinthTransducer
{
    private const float GravityMetersPerSecondSquared = 9.80665f;
    private const float CanalRestingActivation = 0.20f;
    private const float CanalModulation = 0.78f;
    private const float CanalVelocityScaleRadiansPerSecond = 2.0f;
    private const float CanalCupulaTimeSeconds = 0.045f;
    private const float CanalAdaptationTimeSeconds = 5.0f;
    private const float OtolithTimeSeconds = 0.080f;
    private const float MaximumJointAngularVelocityRadiansPerSecond = 8.0f;
    private const float InverseSqrtTwo = 0.70710678f;

    private readonly object _gate = new();
    private readonly Dictionary<string, LabyrinthState> _stateBySource =
        new(StringComparer.OrdinalIgnoreCase);

    public VestibularLabyrinthActivations Transduce(
        PhysicalBodyFrameDescriptor current,
        PhysicalBodyFrameDescriptor? previous,
        float deltaSeconds)
    {
        ArgumentNullException.ThrowIfNull(current);

        lock (_gate)
        {
            var reset = !_stateBySource.TryGetValue(current.InputSource, out var state) ||
                current.Sequence <= state.Sequence ||
                current.TimestampMs <= state.TimestampMs;
            if (reset)
            {
                state = new LabyrinthState();
                _stateBySource[current.InputSource] = state;
                previous = null;
                deltaSeconds = 0f;
            }
            ArgumentNullException.ThrowIfNull(state);

            var dt = deltaSeconds > 0f ? Math.Clamp(deltaSeconds, 0.001f, 1.0f) : 1f / 30f;
            var neckPitchVelocity = AngularDerivative(
                current.Articulation.NeckPitchRadians,
                previous?.Articulation.NeckPitchRadians,
                deltaSeconds);
            var neckYawVelocity = AngularDerivative(
                current.Articulation.NeckYawRadians,
                previous?.Articulation.NeckYawRadians,
                deltaSeconds);
            var trunkPitchVelocity = AngularDerivative(
                current.Articulation.TrunkPitchRadians,
                previous?.Articulation.TrunkPitchRadians,
                deltaSeconds);
            var trunkRollVelocity = AngularDerivative(
                current.Articulation.TrunkRollRadians,
                previous?.Articulation.TrunkRollRadians,
                deltaSeconds);
            var trunkYawVelocity = AngularDerivative(
                current.Articulation.TrunkYawRadians,
                previous?.Articulation.TrunkYawRadians,
                deltaSeconds);

            var headPitchVelocity = current.AngularVelocityX + trunkPitchVelocity + neckPitchVelocity;
            var headYawVelocity = current.AngularVelocityY + trunkYawVelocity + neckYawVelocity;
            var headRollVelocity = current.AngularVelocityZ + trunkRollVelocity;

            var leftHorizontalInput = headYawVelocity;
            var rightHorizontalInput = -headYawVelocity;
            var leftAnteriorInput = (headPitchVelocity - headRollVelocity) * InverseSqrtTwo;
            var rightPosteriorInput = -leftAnteriorInput;
            var rightAnteriorInput = (headPitchVelocity + headRollVelocity) * InverseSqrtTwo;
            var leftPosteriorInput = -rightAnteriorInput;

            (state.LeftHorizontalCupula, state.LeftHorizontalAdaptation) = AdvanceCanal(
                state.LeftHorizontalCupula,
                state.LeftHorizontalAdaptation,
                leftHorizontalInput,
                dt);
            (state.RightHorizontalCupula, state.RightHorizontalAdaptation) = AdvanceCanal(
                state.RightHorizontalCupula,
                state.RightHorizontalAdaptation,
                rightHorizontalInput,
                dt);
            (state.LeftAnteriorCupula, state.LeftAnteriorAdaptation) = AdvanceCanal(
                state.LeftAnteriorCupula,
                state.LeftAnteriorAdaptation,
                leftAnteriorInput,
                dt);
            (state.RightAnteriorCupula, state.RightAnteriorAdaptation) = AdvanceCanal(
                state.RightAnteriorCupula,
                state.RightAnteriorAdaptation,
                rightAnteriorInput,
                dt);
            (state.LeftPosteriorCupula, state.LeftPosteriorAdaptation) = AdvanceCanal(
                state.LeftPosteriorCupula,
                state.LeftPosteriorAdaptation,
                leftPosteriorInput,
                dt);
            (state.RightPosteriorCupula, state.RightPosteriorAdaptation) = AdvanceCanal(
                state.RightPosteriorCupula,
                state.RightPosteriorAdaptation,
                rightPosteriorInput,
                dt);

            var balance = current.Articulation.Musculoskeletal?.Balance ?? PhysicalBalanceStateFrame.Neutral;
            var headPitch = Math.Clamp(
                balance.FallPitchRadians +
                current.Articulation.TrunkPitchRadians +
                current.Articulation.NeckPitchRadians,
                -MathF.PI,
                MathF.PI);
            var headRoll = Math.Clamp(
                balance.FallRollRadians + current.Articulation.TrunkRollRadians,
                -MathF.PI,
                MathF.PI);

            var ax = LinearDerivative(current.LinearVelocityX, previous?.LinearVelocityX, deltaSeconds);
            var ay = LinearDerivative(current.LinearVelocityY, previous?.LinearVelocityY, deltaSeconds);
            var az = LinearDerivative(current.LinearVelocityZ, previous?.LinearVelocityZ, deltaSeconds);

            // The otoconial membranes respond to the gravito-inertial vector in
            // skull coordinates. Positive axes are anatomical right, up, and
            // forward; the stored vertical component is positive downward.
            var gravityRight = GravityMetersPerSecondSquared * MathF.Sin(headRoll) * MathF.Cos(headPitch);
            var gravityForward = GravityMetersPerSecondSquared * MathF.Sin(headPitch) * MathF.Cos(headRoll);
            var gravityDown = GravityMetersPerSecondSquared * MathF.Cos(headPitch) * MathF.Cos(headRoll);
            var gravitoInertialRight = gravityRight - ax;
            var gravitoInertialForward = gravityForward - az;
            var gravitoInertialDown = gravityDown + ay;

            if (!state.OtolithInitialized)
            {
                state.OtolithRight = gravitoInertialRight;
                state.OtolithForward = gravitoInertialForward;
                state.OtolithDown = gravitoInertialDown;
                state.OtolithInitialized = true;
            }
            else
            {
                var alpha = ExponentialAlpha(dt, OtolithTimeSeconds);
                state.OtolithRight += (gravitoInertialRight - state.OtolithRight) * alpha;
                state.OtolithForward += (gravitoInertialForward - state.OtolithForward) * alpha;
                state.OtolithDown += (gravitoInertialDown - state.OtolithDown) * alpha;
            }

            state.Sequence = current.Sequence;
            state.TimestampMs = current.TimestampMs;

            var commonOtolith = new (string Receptor, float Activation)[]
            {
                ("utricle_left", Negative(state.OtolithRight, GravityMetersPerSecondSquared)),
                ("utricle_right", Positive(state.OtolithRight, GravityMetersPerSecondSquared)),
                ("saccule_down", Positive(state.OtolithDown, GravityMetersPerSecondSquared)),
                ("saccule_up", Negative(state.OtolithDown, GravityMetersPerSecondSquared)),
                ("utricle_backward", Negative(state.OtolithForward, GravityMetersPerSecondSquared)),
                ("utricle_forward", Positive(state.OtolithForward, GravityMetersPerSecondSquared)),
                ("otolith_pitch_forward", Positive(state.OtolithForward, GravityMetersPerSecondSquared * 0.85f)),
                ("otolith_pitch_backward", Negative(state.OtolithForward, GravityMetersPerSecondSquared * 0.85f)),
                ("otolith_roll_left", Negative(state.OtolithRight, GravityMetersPerSecondSquared * 0.85f)),
                ("otolith_roll_right", Positive(state.OtolithRight, GravityMetersPerSecondSquared * 0.85f))
            };

            return new VestibularLabyrinthActivations(
                BuildSide(
                    state.LeftHorizontalCupula,
                    state.LeftAnteriorCupula,
                    state.LeftPosteriorCupula,
                    commonOtolith),
                BuildSide(
                    state.RightHorizontalCupula,
                    state.RightAnteriorCupula,
                    state.RightPosteriorCupula,
                    commonOtolith));
        }
    }

    private static IReadOnlyList<(string Receptor, float Activation)> BuildSide(
        float horizontal,
        float anterior,
        float posterior,
        IReadOnlyList<(string Receptor, float Activation)> commonOtolith)
    {
        var populations = new List<(string Receptor, float Activation)>(commonOtolith.Count + 3)
        {
            ("horizontal_canal", CanalActivation(horizontal)),
            ("anterior_canal", CanalActivation(anterior)),
            ("posterior_canal", CanalActivation(posterior))
        };
        populations.AddRange(commonOtolith);
        return populations;
    }

    private static (float Cupula, float AdaptedVelocity) AdvanceCanal(
        float cupula,
        float adaptedVelocity,
        float angularVelocity,
        float deltaSeconds)
    {
        var adaptationAlpha = ExponentialAlpha(deltaSeconds, CanalAdaptationTimeSeconds);
        adaptedVelocity += (angularVelocity - adaptedVelocity) * adaptationAlpha;
        var cupulaTarget = angularVelocity - adaptedVelocity;
        var cupulaAlpha = ExponentialAlpha(deltaSeconds, CanalCupulaTimeSeconds);
        cupula += (cupulaTarget - cupula) * cupulaAlpha;
        return (cupula, adaptedVelocity);
    }

    private static float CanalActivation(float cupulaVelocity)
    {
        var modulation = MathF.Tanh(cupulaVelocity / CanalVelocityScaleRadiansPerSecond) *
            CanalModulation;
        return Math.Clamp(CanalRestingActivation + modulation, 0f, 1f);
    }

    private static float LinearDerivative(float current, float? previous, float deltaSeconds) =>
        previous.HasValue && deltaSeconds > 0f
            ? (current - previous.Value) / deltaSeconds
            : 0f;

    private static float AngularDerivative(float current, float? previous, float deltaSeconds) =>
        Math.Clamp(
            LinearDerivative(current, previous, deltaSeconds),
            -MaximumJointAngularVelocityRadiansPerSecond,
            MaximumJointAngularVelocityRadiansPerSecond);

    private static float ExponentialAlpha(float deltaSeconds, float timeConstantSeconds) =>
        1f - MathF.Exp(-Math.Max(0f, deltaSeconds) / Math.Max(0.001f, timeConstantSeconds));

    private static float Positive(float value, float scale) =>
        Math.Clamp(value / Math.Max(0.001f, scale), 0f, 1f);

    private static float Negative(float value, float scale) =>
        Math.Clamp(-value / Math.Max(0.001f, scale), 0f, 1f);

    private sealed class LabyrinthState
    {
        public long Sequence { get; set; } = -1;
        public long TimestampMs { get; set; } = -1;
        public float LeftHorizontalAdaptation { get; set; }
        public float RightHorizontalAdaptation { get; set; }
        public float LeftAnteriorAdaptation { get; set; }
        public float RightAnteriorAdaptation { get; set; }
        public float LeftPosteriorAdaptation { get; set; }
        public float RightPosteriorAdaptation { get; set; }
        public float LeftHorizontalCupula { get; set; }
        public float RightHorizontalCupula { get; set; }
        public float LeftAnteriorCupula { get; set; }
        public float RightAnteriorCupula { get; set; }
        public float LeftPosteriorCupula { get; set; }
        public float RightPosteriorCupula { get; set; }
        public float OtolithRight { get; set; }
        public float OtolithForward { get; set; }
        public float OtolithDown { get; set; }
        public bool OtolithInitialized { get; set; }
    }
}

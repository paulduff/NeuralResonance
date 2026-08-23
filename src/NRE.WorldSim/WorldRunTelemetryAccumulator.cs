using NeuralResonanceEngine.Shared.Contracts;
using NRE.SimAvatar;

namespace NRE.WorldSim;

internal sealed class WorldRunTelemetryAccumulator
{
    private const double ActiveMotorThreshold = 0.01;
    private const double FootStanceLoadThresholdNewtons = 5.0;
    private const double ClearedSwingThresholdMeters = 0.015;
    private readonly object gate = new();
    private readonly Dictionary<string, double> balancePhaseSeconds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, long> balancePhaseEntries = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, MutableContactStatistics> contacts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, MutableMotorStatistics> motors = new(StringComparer.Ordinal);
    private readonly Dictionary<string, MutableWithdrawalSourceStatistics> withdrawalSources = new(StringComparer.Ordinal);
    private readonly HashSet<string> activeContactSources = new(StringComparer.Ordinal);
    private readonly HashSet<string> activeWithdrawalSources = new(StringComparer.Ordinal);
    private readonly List<WorldDeathRunEvent> deaths = [];
    private readonly MutableGaitStatistics gait = new();
    private double observedSeconds;
    private string? previousBalancePhase;
    private double minimumSupportMarginMeters = double.PositiveInfinity;
    private double maximumDynamicStabilityAllowanceMeters;
    private double maximumAbsoluteFallPitchRadians;
    private double maximumAbsoluteFallRollRadians;
    private double peakCombinedHandLoadNewtons;
    private double peakCombinedFootLoadNewtons;
    private double peakObservedVerticalSupportLoadNewtons;
    private long spinalWithdrawalSamples;
    private double spinalWithdrawalActiveSeconds;
    private double peakSpinalWithdrawalDrive;
    private long postureConflictSamples;
    private double postureConflictSeconds;
    private int peakConcurrentPostureDrives;
    private double locomotorRecruitmentActiveSeconds;
    private double integralLocomotorRecruitmentSeconds;
    private double peakLocomotorRecruitment;
    private ActionAuthorityCumulativeTelemetry? brainActionAuthority;

    public void Reset()
    {
        lock (gate)
        {
            balancePhaseSeconds.Clear();
            balancePhaseEntries.Clear();
            contacts.Clear();
            motors.Clear();
            withdrawalSources.Clear();
            activeContactSources.Clear();
            activeWithdrawalSources.Clear();
            deaths.Clear();
            gait.Reset();
            observedSeconds = 0.0;
            previousBalancePhase = null;
            minimumSupportMarginMeters = double.PositiveInfinity;
            maximumDynamicStabilityAllowanceMeters = 0.0;
            maximumAbsoluteFallPitchRadians = 0.0;
            maximumAbsoluteFallRollRadians = 0.0;
            peakCombinedHandLoadNewtons = 0.0;
            peakCombinedFootLoadNewtons = 0.0;
            peakObservedVerticalSupportLoadNewtons = 0.0;
            spinalWithdrawalSamples = 0;
            spinalWithdrawalActiveSeconds = 0.0;
            peakSpinalWithdrawalDrive = 0.0;
            postureConflictSamples = 0;
            postureConflictSeconds = 0.0;
            peakConcurrentPostureDrives = 0;
            locomotorRecruitmentActiveSeconds = 0.0;
            integralLocomotorRecruitmentSeconds = 0.0;
            peakLocomotorRecruitment = 0.0;
            brainActionAuthority = null;
        }
    }

    public void ObserveActionAuthority(ActionAuthorityCumulativeTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);
        lock (gate)
        {
            // The frame stream is serial. A lower sample count therefore means
            // the brain was restarted, not that an older response arrived late.
            brainActionAuthority = CopyActionAuthority(telemetry);
        }
    }

    public void ObserveDeath(WorldDeathRunEvent death)
    {
        ArgumentNullException.ThrowIfNull(death);
        lock (gate)
        {
            deaths.Add(death with
            {
                TissueDamageByCause = new Dictionary<string, double>(
                    death.TissueDamageByCause,
                    StringComparer.Ordinal)
            });
        }
    }

    public void Observe(
        double elapsedSeconds,
        PhysicalBalanceStateFrame? balance,
        PhysicalArticulationFrame articulation,
        AvatarNervousSystemSignal signal,
        IReadOnlyList<WorldRunContactObservation> contactSamples,
        double spinalWithdrawalDrive = 0.0,
        IReadOnlyList<SpinalWithdrawalSourceActivity>? spinalWithdrawalSources = null)
    {
        var dt = double.IsFinite(elapsedSeconds) ? Math.Clamp(elapsedSeconds, 0.0, 0.5) : 0.0;
        lock (gate)
        {
            observedSeconds += dt;
            ObserveBalance(dt, balance ?? PhysicalBalanceStateFrame.Neutral);
            ObserveLoads(articulation);
            ObserveGait(dt, articulation);
            ObserveMotorChannels(dt, signal);
            ObserveContacts(dt, contactSamples);
            ObserveSpinalWithdrawal(dt, spinalWithdrawalDrive);
            ObserveWithdrawalSources(dt, spinalWithdrawalSources ?? []);
        }
    }

    public WorldRunStatistics Capture()
    {
        lock (gate)
        {
            return new WorldRunStatistics(
                ObservedSeconds: observedSeconds,
                BalancePhaseSeconds: new Dictionary<string, double>(balancePhaseSeconds, StringComparer.OrdinalIgnoreCase),
                BalancePhaseEntries: new Dictionary<string, long>(balancePhaseEntries, StringComparer.OrdinalIgnoreCase),
                MinimumSupportMarginMeters: double.IsPositiveInfinity(minimumSupportMarginMeters)
                    ? 0.0
                    : minimumSupportMarginMeters,
                MaximumDynamicStabilityAllowanceMeters: maximumDynamicStabilityAllowanceMeters,
                MaximumAbsoluteFallPitchRadians: maximumAbsoluteFallPitchRadians,
                MaximumAbsoluteFallRollRadians: maximumAbsoluteFallRollRadians,
                PeakCombinedHandLoadNewtons: peakCombinedHandLoadNewtons,
                PeakCombinedFootLoadNewtons: peakCombinedFootLoadNewtons,
                PeakObservedVerticalSupportLoadNewtons: peakObservedVerticalSupportLoadNewtons,
                SpinalWithdrawalSamples: spinalWithdrawalSamples,
                SpinalWithdrawalActiveSeconds: spinalWithdrawalActiveSeconds,
                PeakSpinalWithdrawalDrive: peakSpinalWithdrawalDrive,
                PostureConflictSamples: postureConflictSamples,
                PostureConflictSeconds: postureConflictSeconds,
                PeakConcurrentPostureDrives: peakConcurrentPostureDrives,
                LocomotorRecruitmentActiveSeconds: locomotorRecruitmentActiveSeconds,
                IntegralLocomotorRecruitmentSeconds: integralLocomotorRecruitmentSeconds,
                PeakLocomotorRecruitment: peakLocomotorRecruitment,
                Gait: gait.Capture(),
                WithdrawalSources: withdrawalSources.Values
                    .OrderByDescending(static value => value.ActiveSeconds)
                    .ThenBy(static value => value.SourceKey, StringComparer.Ordinal)
                    .Select(static value => value.Capture())
                    .ToArray(),
                Contacts: contacts.Values
                    .OrderByDescending(static value => value.MaximumContinuousSeconds)
                    .ThenBy(static value => value.Source, StringComparer.Ordinal)
                    .Select(static value => value.Capture())
                    .ToArray(),
                MotorChannels: motors.Values
                    .OrderBy(static value => value.Channel, StringComparer.Ordinal)
                    .Select(static value => value.Capture())
                    .ToArray(),
                Deaths: deaths
                    .Select(static death => death with
                    {
                        TissueDamageByCause = new Dictionary<string, double>(
                            death.TissueDamageByCause,
                            StringComparer.Ordinal)
                    })
                    .ToArray(),
                BrainActionAuthority: brainActionAuthority is null
                    ? null
                    : CopyActionAuthority(brainActionAuthority));
        }
    }

    private static ActionAuthorityCumulativeTelemetry CopyActionAuthority(
        ActionAuthorityCumulativeTelemetry telemetry)
        => telemetry with
        {
            Channels = telemetry.Channels
                .Select(static channel => channel with { })
                .ToArray()
        };

    private void ObserveBalance(double dt, PhysicalBalanceStateFrame balance)
    {
        var phase = string.IsNullOrWhiteSpace(balance.Phase) ? "unknown" : balance.Phase.Trim().ToLowerInvariant();
        balancePhaseSeconds[phase] = balancePhaseSeconds.GetValueOrDefault(phase) + dt;
        if (!string.Equals(previousBalancePhase, phase, StringComparison.OrdinalIgnoreCase))
        {
            balancePhaseEntries[phase] = balancePhaseEntries.GetValueOrDefault(phase) + 1;
            previousBalancePhase = phase;
        }

        minimumSupportMarginMeters = Math.Min(minimumSupportMarginMeters, balance.SupportMarginMeters);
        maximumDynamicStabilityAllowanceMeters = Math.Max(
            maximumDynamicStabilityAllowanceMeters,
            balance.DynamicStabilityAllowanceMeters);
        maximumAbsoluteFallPitchRadians = Math.Max(maximumAbsoluteFallPitchRadians, Math.Abs(balance.FallPitchRadians));
        maximumAbsoluteFallRollRadians = Math.Max(maximumAbsoluteFallRollRadians, Math.Abs(balance.FallRollRadians));
    }

    private void ObserveLoads(PhysicalArticulationFrame articulation)
    {
        peakCombinedHandLoadNewtons = Math.Max(
            peakCombinedHandLoadNewtons,
            articulation.LeftHandLoadNewtons + articulation.RightHandLoadNewtons);
        peakCombinedFootLoadNewtons = Math.Max(
            peakCombinedFootLoadNewtons,
            articulation.LeftFootLoadNewtons + articulation.RightFootLoadNewtons);
    }

    private void ObserveGait(double dt, PhysicalArticulationFrame articulation)
    {
        var body = articulation.Musculoskeletal ?? MusculoskeletalStateFrame.Neutral;
        var eligible = body.UprightFraction >= 0.45f &&
            (string.Equals(body.Posture, "standing", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(body.Posture, "crouching", StringComparison.OrdinalIgnoreCase));
        var leftClearance = 0.0;
        var rightClearance = 0.0;
        if (eligible)
        {
            foreach (var collider in AvatarColliderRig.CaptureResolved(articulation))
            {
                if (collider.Region == "left_foot")
                {
                    leftClearance = Math.Max(
                        0.0,
                        AvatarColliderRig.LowestSurfaceY(collider) - AvatarColliderRig.LocalGroundPlaneY);
                }
                else if (collider.Region == "right_foot")
                {
                    rightClearance = Math.Max(
                        0.0,
                        AvatarColliderRig.LowestSurfaceY(collider) - AvatarColliderRig.LocalGroundPlaneY);
                }
            }
        }

        gait.Observe(
            dt,
            eligible,
            articulation.LeftFootLoadNewtons >= FootStanceLoadThresholdNewtons,
            articulation.RightFootLoadNewtons >= FootStanceLoadThresholdNewtons,
            leftClearance,
            rightClearance,
            ClearedSwingThresholdMeters);
    }

    private void ObserveContacts(double dt, IReadOnlyList<WorldRunContactObservation> samples)
    {
        activeContactSources.Clear();
        var combinedVerticalSupportLoadNewtons = 0.0;
        for (var i = 0; i < samples.Count; i++)
        {
            var sample = samples[i];
            combinedVerticalSupportLoadNewtons += Math.Max(0.0, sample.VerticalSupportNewtons);
            activeContactSources.Add(sample.Source);
            if (!contacts.TryGetValue(sample.Source, out var statistics))
            {
                statistics = new MutableContactStatistics(sample.Source, sample.Region);
                contacts.Add(sample.Source, statistics);
            }

            statistics.Observe(
                dt,
                sample.ForceNewtons,
                sample.ImpulseNewtonSeconds,
                sample.VerticalSupportNewtons);
        }
        peakObservedVerticalSupportLoadNewtons = Math.Max(
            peakObservedVerticalSupportLoadNewtons,
            combinedVerticalSupportLoadNewtons);

        foreach (var statistics in contacts.Values)
        {
            if (!activeContactSources.Contains(statistics.Source))
            {
                statistics.EndContinuousContact();
            }
        }
    }

    private void ObserveSpinalWithdrawal(double dt, double drive)
    {
        var bounded = double.IsFinite(drive) ? Math.Clamp(drive, 0.0, 1.0) : 0.0;
        spinalWithdrawalSamples++;
        peakSpinalWithdrawalDrive = Math.Max(peakSpinalWithdrawalDrive, bounded);
        if (bounded >= ActiveMotorThreshold)
        {
            spinalWithdrawalActiveSeconds += dt;
        }
    }

    private void ObserveWithdrawalSources(
        double dt,
        IReadOnlyList<SpinalWithdrawalSourceActivity> sources)
    {
        activeWithdrawalSources.Clear();
        for (var i = 0; i < sources.Count; i++)
        {
            var source = sources[i];
            if (string.IsNullOrWhiteSpace(source.SourceKey))
            {
                continue;
            }

            activeWithdrawalSources.Add(source.SourceKey);
            if (!withdrawalSources.TryGetValue(source.SourceKey, out var statistics))
            {
                statistics = new MutableWithdrawalSourceStatistics(source);
                withdrawalSources.Add(source.SourceKey, statistics);
            }

            statistics.Observe(dt, source, ActiveMotorThreshold);
        }

        foreach (var statistics in withdrawalSources.Values)
        {
            if (!activeWithdrawalSources.Contains(statistics.SourceKey))
            {
                statistics.EndEpisode();
            }
        }
    }

    private void ObserveMotorChannels(double dt, AvatarNervousSystemSignal signal)
    {
        var concurrentPostures = new[]
        {
            signal.StandDrive,
            signal.CrouchDrive,
            signal.SitDrive,
            signal.LieDrive
        }.Count(static drive => Math.Abs(drive) >= ActiveMotorThreshold);
        peakConcurrentPostureDrives = Math.Max(peakConcurrentPostureDrives, concurrentPostures);
        if (concurrentPostures > 1)
        {
            postureConflictSamples++;
            postureConflictSeconds += dt;
        }

        var locomotorRecruitment = Math.Clamp(
            (Math.Abs(HeadlessWorldRuntime.NormalizeMotorRecruitment(signal.LeftMotorDrive)) +
             Math.Abs(HeadlessWorldRuntime.NormalizeMotorRecruitment(signal.RightMotorDrive))) * 0.5,
            0.0,
            1.0);
        integralLocomotorRecruitmentSeconds += locomotorRecruitment * dt;
        peakLocomotorRecruitment = Math.Max(peakLocomotorRecruitment, locomotorRecruitment);
        if (locomotorRecruitment >= ActiveMotorThreshold)
        {
            locomotorRecruitmentActiveSeconds += dt;
        }

        ObserveMotor("left_motor", signal.LeftMotorDrive, dt);
        ObserveMotor("right_motor", signal.RightMotorDrive, dt);
        ObserveMotor("manipulator", signal.ManipulatorDrive, dt);
        ObserveMotor("left_shoulder_sagittal", signal.LeftShoulderSagittalDrive, dt);
        ObserveMotor("right_shoulder_sagittal", signal.RightShoulderSagittalDrive, dt);
        ObserveMotor("left_shoulder_coronal", signal.LeftShoulderCoronalDrive, dt);
        ObserveMotor("right_shoulder_coronal", signal.RightShoulderCoronalDrive, dt);
        ObserveMotor("left_elbow", signal.LeftElbowDrive, dt);
        ObserveMotor("right_elbow", signal.RightElbowDrive, dt);
        ObserveMotor("head_yaw", signal.HeadYawDrive, dt);
        ObserveMotor("head_pitch", signal.HeadPitchDrive, dt);
        ObserveMotor("stand", signal.StandDrive, dt);
        ObserveMotor("crouch", signal.CrouchDrive, dt);
        ObserveMotor("sit", signal.SitDrive, dt);
        ObserveMotor("lie", signal.LieDrive, dt);
        ObserveMotor("left_hip_coronal", signal.LeftHipCoronalDrive, dt);
        ObserveMotor("right_hip_coronal", signal.RightHipCoronalDrive, dt);
        ObserveMotor("left_ankle_sagittal", signal.LeftAnkleSagittalDrive, dt);
        ObserveMotor("right_ankle_sagittal", signal.RightAnkleSagittalDrive, dt);
        ObserveMotor("left_ankle_coronal", signal.LeftAnkleCoronalDrive, dt);
        ObserveMotor("right_ankle_coronal", signal.RightAnkleCoronalDrive, dt);
        ObserveMotor("trunk_yaw", signal.TrunkYawDrive, dt);
    }

    private void ObserveMotor(string channel, double drive, double dt)
    {
        if (!motors.TryGetValue(channel, out var statistics))
        {
            statistics = new MutableMotorStatistics(channel);
            motors.Add(channel, statistics);
        }

        statistics.Observe(dt, double.IsFinite(drive) ? drive : 0.0, ActiveMotorThreshold);
    }

    private sealed class MutableContactStatistics(string source, string region)
    {
        public string Source { get; } = source;
        public string Region { get; } = region;
        public long Samples { get; private set; }
        public double TotalObservedSeconds { get; private set; }
        public double CurrentContinuousSeconds { get; private set; }
        public double MaximumContinuousSeconds { get; private set; }
        public double PeakForceNewtons { get; private set; }
        public double PeakImpulseNewtonSeconds { get; private set; }
        public double PeakVerticalSupportNewtons { get; private set; }

        public void Observe(
            double dt,
            double forceNewtons,
            double impulseNewtonSeconds,
            double verticalSupportNewtons)
        {
            Samples++;
            TotalObservedSeconds += dt;
            CurrentContinuousSeconds += dt;
            MaximumContinuousSeconds = Math.Max(MaximumContinuousSeconds, CurrentContinuousSeconds);
            PeakForceNewtons = Math.Max(PeakForceNewtons, Math.Max(0.0, forceNewtons));
            PeakImpulseNewtonSeconds = Math.Max(PeakImpulseNewtonSeconds, Math.Max(0.0, impulseNewtonSeconds));
            PeakVerticalSupportNewtons = Math.Max(
                PeakVerticalSupportNewtons,
                Math.Max(0.0, verticalSupportNewtons));
        }

        public void EndContinuousContact() => CurrentContinuousSeconds = 0.0;

        public WorldContactRunStatistics Capture() => new(
            Source,
            Region,
            Samples,
            TotalObservedSeconds,
            MaximumContinuousSeconds,
            PeakForceNewtons,
            PeakImpulseNewtonSeconds,
            PeakVerticalSupportNewtons);
    }

    private sealed class MutableMotorStatistics(string channel)
    {
        public string Channel { get; } = channel;
        public long Samples { get; private set; }
        public double ActiveSeconds { get; private set; }
        public double PositiveDriveSeconds { get; private set; }
        public double NegativeDriveSeconds { get; private set; }
        public double IntegralAbsoluteDriveSeconds { get; private set; }
        public double PeakAbsoluteDrive { get; private set; }

        public void Observe(double dt, double drive, double activeThreshold)
        {
            Samples++;
            var magnitude = Math.Abs(drive);
            IntegralAbsoluteDriveSeconds += magnitude * dt;
            PeakAbsoluteDrive = Math.Max(PeakAbsoluteDrive, magnitude);
            if (magnitude < activeThreshold)
            {
                return;
            }

            ActiveSeconds += dt;
            if (drive > 0.0)
            {
                PositiveDriveSeconds += dt;
            }
            else
            {
                NegativeDriveSeconds += dt;
            }
        }

        public WorldMotorChannelRunStatistics Capture() => new(
            Channel,
            Samples,
            ActiveSeconds,
            PositiveDriveSeconds,
            NegativeDriveSeconds,
            IntegralAbsoluteDriveSeconds,
            PeakAbsoluteDrive);
    }

    private sealed class MutableWithdrawalSourceStatistics(SpinalWithdrawalSourceActivity source)
    {
        public string SourceKey { get; } = source.SourceKey;
        public string BodySide { get; } = source.BodySide;
        public string Region { get; } = source.Region;
        public string ContactNormalSector { get; } = source.ContactNormalSector;
        public int ChannelIndex { get; } = source.ChannelIndex;
        public string MotorProjection { get; } = source.MotorProjection;
        public long Samples { get; private set; }
        public long EpisodeCount { get; private set; }
        public double ActiveSeconds { get; private set; }
        public double CurrentContinuousSeconds { get; private set; }
        public double MaximumContinuousSeconds { get; private set; }
        public double IntegralAfferentDriveSeconds { get; private set; }
        public double IntegralReflexDriveSeconds { get; private set; }
        public double PeakAfferentDrive { get; private set; }
        public double PeakReflexDrive { get; private set; }
        public double PeakRecurrentInhibition { get; private set; }
        public double MaximumAfferentAgeMilliseconds { get; private set; }
        private bool active;

        public void Observe(
            double dt,
            SpinalWithdrawalSourceActivity observation,
            double activeThreshold)
        {
            Samples++;
            var afferentDrive = Math.Clamp(observation.AfferentDrive, 0f, 1f);
            var reflexDrive = Math.Clamp(observation.ReflexDrive, 0f, 1f);
            var inhibition = Math.Clamp(observation.RecurrentInhibition, 0f, 1f);
            IntegralAfferentDriveSeconds += afferentDrive * dt;
            IntegralReflexDriveSeconds += reflexDrive * dt;
            PeakAfferentDrive = Math.Max(PeakAfferentDrive, afferentDrive);
            PeakReflexDrive = Math.Max(PeakReflexDrive, reflexDrive);
            PeakRecurrentInhibition = Math.Max(PeakRecurrentInhibition, inhibition);
            MaximumAfferentAgeMilliseconds = Math.Max(
                MaximumAfferentAgeMilliseconds,
                Math.Max(0f, observation.AfferentAgeMilliseconds));

            if (reflexDrive < activeThreshold)
            {
                EndEpisode();
                return;
            }

            if (!active)
            {
                active = true;
                EpisodeCount++;
            }

            ActiveSeconds += dt;
            CurrentContinuousSeconds += dt;
            MaximumContinuousSeconds = Math.Max(MaximumContinuousSeconds, CurrentContinuousSeconds);
        }

        public void EndEpisode()
        {
            active = false;
            CurrentContinuousSeconds = 0.0;
        }

        public WorldWithdrawalSourceRunStatistics Capture() => new(
            SourceKey,
            BodySide,
            Region,
            ContactNormalSector,
            ChannelIndex,
            MotorProjection,
            Samples,
            EpisodeCount,
            ActiveSeconds,
            MaximumContinuousSeconds,
            IntegralAfferentDriveSeconds,
            IntegralReflexDriveSeconds,
            PeakAfferentDrive,
            PeakReflexDrive,
            PeakRecurrentInhibition,
            MaximumAfferentAgeMilliseconds);
    }

    private sealed class MutableGaitStatistics
    {
        private bool initialized;
        private bool previousLeftStance;
        private bool previousRightStance;
        private string? lastSwingSide;
        private bool leftSwingCleared;
        private bool rightSwingCleared;
        private double currentLeftSwingSeconds;
        private double currentRightSwingSeconds;
        private double eligibleSeconds;
        private double leftStanceSeconds;
        private double rightStanceSeconds;
        private double leftSwingSeconds;
        private double rightSwingSeconds;
        private double doubleSupportSeconds;
        private double unsupportedSeconds;
        private long leftStanceEntries;
        private long rightStanceEntries;
        private long leftSwingEntries;
        private long rightSwingEntries;
        private long alternatingSwingTransitions;
        private long repeatedSameSideSwingTransitions;
        private long leftClearedSwingEntries;
        private long rightClearedSwingEntries;
        private double maximumLeftSwingSeconds;
        private double maximumRightSwingSeconds;
        private double peakLeftSwingClearanceMeters;
        private double peakRightSwingClearanceMeters;

        public void Reset()
        {
            initialized = false;
            previousLeftStance = false;
            previousRightStance = false;
            lastSwingSide = null;
            leftSwingCleared = false;
            rightSwingCleared = false;
            currentLeftSwingSeconds = 0.0;
            currentRightSwingSeconds = 0.0;
            eligibleSeconds = 0.0;
            leftStanceSeconds = 0.0;
            rightStanceSeconds = 0.0;
            leftSwingSeconds = 0.0;
            rightSwingSeconds = 0.0;
            doubleSupportSeconds = 0.0;
            unsupportedSeconds = 0.0;
            leftStanceEntries = 0;
            rightStanceEntries = 0;
            leftSwingEntries = 0;
            rightSwingEntries = 0;
            alternatingSwingTransitions = 0;
            repeatedSameSideSwingTransitions = 0;
            leftClearedSwingEntries = 0;
            rightClearedSwingEntries = 0;
            maximumLeftSwingSeconds = 0.0;
            maximumRightSwingSeconds = 0.0;
            peakLeftSwingClearanceMeters = 0.0;
            peakRightSwingClearanceMeters = 0.0;
        }

        public void Observe(
            double dt,
            bool eligible,
            bool leftStance,
            bool rightStance,
            double leftClearanceMeters,
            double rightClearanceMeters,
            double clearedSwingThresholdMeters)
        {
            if (!eligible)
            {
                initialized = false;
                currentLeftSwingSeconds = 0.0;
                currentRightSwingSeconds = 0.0;
                leftSwingCleared = false;
                rightSwingCleared = false;
                return;
            }

            eligibleSeconds += dt;
            if (!initialized)
            {
                initialized = true;
                previousLeftStance = leftStance;
                previousRightStance = rightStance;
                if (leftStance) leftStanceEntries++; else leftSwingEntries++;
                if (rightStance) rightStanceEntries++; else rightSwingEntries++;
            }
            else
            {
                ObserveTransition("left", previousLeftStance, leftStance);
                ObserveTransition("right", previousRightStance, rightStance);
                previousLeftStance = leftStance;
                previousRightStance = rightStance;
            }

            if (leftStance)
            {
                leftStanceSeconds += dt;
                currentLeftSwingSeconds = 0.0;
                leftSwingCleared = false;
            }
            else
            {
                leftSwingSeconds += dt;
                currentLeftSwingSeconds += dt;
                maximumLeftSwingSeconds = Math.Max(maximumLeftSwingSeconds, currentLeftSwingSeconds);
                peakLeftSwingClearanceMeters = Math.Max(peakLeftSwingClearanceMeters, leftClearanceMeters);
                if (!leftSwingCleared && leftClearanceMeters >= clearedSwingThresholdMeters)
                {
                    leftSwingCleared = true;
                    leftClearedSwingEntries++;
                }
            }

            if (rightStance)
            {
                rightStanceSeconds += dt;
                currentRightSwingSeconds = 0.0;
                rightSwingCleared = false;
            }
            else
            {
                rightSwingSeconds += dt;
                currentRightSwingSeconds += dt;
                maximumRightSwingSeconds = Math.Max(maximumRightSwingSeconds, currentRightSwingSeconds);
                peakRightSwingClearanceMeters = Math.Max(peakRightSwingClearanceMeters, rightClearanceMeters);
                if (!rightSwingCleared && rightClearanceMeters >= clearedSwingThresholdMeters)
                {
                    rightSwingCleared = true;
                    rightClearedSwingEntries++;
                }
            }

            if (leftStance && rightStance)
            {
                doubleSupportSeconds += dt;
            }
            else if (!leftStance && !rightStance)
            {
                unsupportedSeconds += dt;
            }
        }

        private void ObserveTransition(string side, bool previousStance, bool currentStance)
        {
            if (previousStance == currentStance)
            {
                return;
            }

            if (currentStance)
            {
                if (side == "left") leftStanceEntries++; else rightStanceEntries++;
                return;
            }

            if (side == "left") leftSwingEntries++; else rightSwingEntries++;
            if (lastSwingSide is not null)
            {
                if (lastSwingSide == side)
                {
                    repeatedSameSideSwingTransitions++;
                }
                else
                {
                    alternatingSwingTransitions++;
                }
            }
            lastSwingSide = side;
        }

        public WorldGaitRunStatistics Capture() => new(
            eligibleSeconds,
            leftStanceSeconds,
            rightStanceSeconds,
            leftSwingSeconds,
            rightSwingSeconds,
            doubleSupportSeconds,
            unsupportedSeconds,
            leftStanceEntries,
            rightStanceEntries,
            leftSwingEntries,
            rightSwingEntries,
            alternatingSwingTransitions,
            repeatedSameSideSwingTransitions,
            leftClearedSwingEntries,
            rightClearedSwingEntries,
            maximumLeftSwingSeconds,
            maximumRightSwingSeconds,
            peakLeftSwingClearanceMeters,
            peakRightSwingClearanceMeters);
    }
}

internal readonly record struct WorldRunContactObservation(
    string Source,
    string Region,
    double ForceNewtons,
    double ImpulseNewtonSeconds,
    double VerticalSupportNewtons = 0.0);

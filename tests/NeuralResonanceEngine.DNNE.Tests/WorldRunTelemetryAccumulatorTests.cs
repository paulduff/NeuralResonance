using NeuralResonanceEngine.Shared.Contracts;
using NRE.SimAvatar;
using NRE.WorldSim;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class WorldRunTelemetryAccumulatorTests
{
    [Fact]
    public void ContactAndWithdrawalEvidenceArePersistedExplicitly()
    {
        var telemetry = new WorldRunTelemetryAccumulator();
        telemetry.Observe(
            elapsedSeconds: 0.025,
            PhysicalBalanceStateFrame.Neutral,
            PhysicalArticulationFrame.Neutral,
            new AvatarNervousSystemSignal(0.0, 0.0, 0.0, 0, 0, 0),
            [new WorldRunContactObservation(
                "avatar_world_left_hand_load",
                "left_hand",
                ForceNewtons: 220.0,
                ImpulseNewtonSeconds: 5.5,
                VerticalSupportNewtons: 0.0)],
            spinalWithdrawalDrive: 0.72,
            spinalWithdrawalSources:
            [
                new SpinalWithdrawalSourceActivity(
                    SourceKey: "left:hand:normal_z_neg:channel_5",
                    BodySide: "left",
                    Region: "hand",
                    ContactNormalSector: "normal_z_neg",
                    ChannelIndex: 5,
                    MotorProjection: "left_shoulder_extension",
                    AfferentDrive: 0.80f,
                    ReflexDrive: 0.72f,
                    RecurrentInhibition: 0.45f)
            ]);

        var report = telemetry.Capture();
        var contact = Assert.Single(report.Contacts);
        Assert.Equal("avatar_world_left_hand_load", contact.Source);
        Assert.Equal(5.5, contact.PeakImpulseNewtonSeconds, 6);
        Assert.Equal(0.0, contact.PeakVerticalSupportNewtons);
        Assert.Equal(1, report.SpinalWithdrawalSamples);
        Assert.Equal(0.025, report.SpinalWithdrawalActiveSeconds, 6);
        Assert.Equal(0.72, report.PeakSpinalWithdrawalDrive, 6);
        var source = Assert.Single(report.WithdrawalSources);
        Assert.Equal("left:hand:normal_z_neg:channel_5", source.SourceKey);
        Assert.Equal(1, source.EpisodeCount);
        Assert.Equal(0.025, source.ActiveSeconds, 6);
        Assert.Equal(0.025, source.MaximumContinuousSeconds, 6);
        Assert.Equal(0.80, source.PeakAfferentDrive, 6);
        Assert.Equal(0.72, source.PeakReflexDrive, 6);
        Assert.Equal(0.45, source.PeakRecurrentInhibition, 6);
        Assert.Equal(0.0, source.MaximumAfferentAgeMilliseconds, 6);
    }

    [Fact]
    public void DeathEvidencePreservesPrimaryCauseAndContributionBreakdown()
    {
        var telemetry = new WorldRunTelemetryAccumulator();
        telemetry.ObserveDeath(new WorldDeathRunEvent(
            WorldTick: 4_200,
            ElapsedSeconds: 105.0,
            PrimaryCause: "energy_depletion",
            PrimaryCauseDamageFraction: 0.82,
            StoredEnergyJoules: 0.0,
            HydrationFraction: 0.42,
            TissueIntegrityFraction: 0.0,
            LastInteractionOutcome: "none",
            TissueDamageByCause: new Dictionary<string, double>
            {
                ["energy_depletion"] = 0.82,
                ["contact_impact:left_knee"] = 0.18
            }));

        var death = Assert.Single(telemetry.Capture().Deaths);
        Assert.Equal(4_200, death.WorldTick);
        Assert.Equal("energy_depletion", death.PrimaryCause);
        Assert.Equal(0.82, death.PrimaryCauseDamageFraction, 6);
        Assert.Equal(0.18, death.TissueDamageByCause["contact_impact:left_knee"], 6);

        telemetry.Reset();
        Assert.Empty(telemetry.Capture().Deaths);
    }

    [Fact]
    public void AlternatingLoadedFeetAndColliderClearanceProduceObjectiveGaitPhases()
    {
        var telemetry = new WorldRunTelemetryAccumulator();
        var doubleSupport = PhysicalArticulationFrame.Neutral with
        {
            LeftFootLoadNewtons = 360f,
            RightFootLoadNewtons = 360f
        };
        var leftSwing = doubleSupport with
        {
            LeftFootLoadNewtons = 0f,
            LeftHipAbductionRadians = 0.60f
        };
        var rightSwing = doubleSupport with
        {
            RightFootLoadNewtons = 0f,
            RightHipAbductionRadians = 0.60f
        };
        var signal = new AvatarNervousSystemSignal(0.0, 0.0, 0.0, 0, 0, 0);

        telemetry.Observe(0.10, PhysicalBalanceStateFrame.Neutral, doubleSupport, signal, []);
        telemetry.Observe(0.10, PhysicalBalanceStateFrame.Neutral, leftSwing, signal, []);
        telemetry.Observe(0.10, PhysicalBalanceStateFrame.Neutral, doubleSupport, signal, []);
        telemetry.Observe(0.10, PhysicalBalanceStateFrame.Neutral, rightSwing, signal, []);

        var gait = telemetry.Capture().Gait;
        Assert.Equal(0.40, gait.EligibleSeconds, 6);
        Assert.Equal(0.30, gait.LeftStanceSeconds, 6);
        Assert.Equal(0.30, gait.RightStanceSeconds, 6);
        Assert.Equal(0.10, gait.LeftSwingSeconds, 6);
        Assert.Equal(0.10, gait.RightSwingSeconds, 6);
        Assert.Equal(0.20, gait.DoubleSupportSeconds, 6);
        Assert.Equal(0.0, gait.UnsupportedSeconds, 6);
        Assert.Equal(1, gait.AlternatingSwingTransitions);
        Assert.Equal(0, gait.RepeatedSameSideSwingTransitions);
        Assert.Equal(1, gait.LeftClearedSwingEntries);
        Assert.Equal(1, gait.RightClearedSwingEntries);
        Assert.True(gait.PeakLeftSwingClearanceMeters >= 0.015);
        Assert.True(gait.PeakRightSwingClearanceMeters >= 0.015);
    }

    [Fact]
    public void PostureConflictAndCalibratedLocomotorRecruitmentAreMeasured()
    {
        var telemetry = new WorldRunTelemetryAccumulator();
        telemetry.Observe(
            elapsedSeconds: 0.05,
            PhysicalBalanceStateFrame.Neutral,
            PhysicalArticulationFrame.Neutral,
            new AvatarNervousSystemSignal(
                LeftMotorDrive: 24.0,
                RightMotorDrive: 24.0,
                ManipulatorDrive: 0.0,
                LeftShoulderSagittalDrive: 0.0,
                RightShoulderSagittalDrive: 0.0,
                LeftShoulderCoronalDrive: 0.0,
                RightShoulderCoronalDrive: 0.0,
                LeftElbowDrive: 0.0,
                RightElbowDrive: 0.0,
                HeadYawDrive: 0.0,
                HeadPitchDrive: 0.0,
                StandDrive: 0.8,
                CrouchDrive: 0.0,
                SitDrive: 0.4,
                LieDrive: 0.0,
                MotorEvents: 1,
                ManipulatorEvents: 0,
                OrientingEvents: 0,
                PostureEvents: 1,
                TicksWithoutMotorDispatch: 0,
                LeftHipCoronalDrive: 0.0,
                RightHipCoronalDrive: 0.0,
                LeftAnkleSagittalDrive: 0.0,
                RightAnkleSagittalDrive: 0.0,
                LeftAnkleCoronalDrive: 0.0,
                RightAnkleCoronalDrive: 0.0,
                TrunkYawDrive: 0.0),
            []);

        var report = telemetry.Capture();

        Assert.Equal(1, report.PostureConflictSamples);
        Assert.Equal(0.05, report.PostureConflictSeconds, 6);
        Assert.Equal(2, report.PeakConcurrentPostureDrives);
        Assert.Equal(0.05, report.LocomotorRecruitmentActiveSeconds, 6);
        Assert.Equal(0.025, report.IntegralLocomotorRecruitmentSeconds, 6);
        Assert.Equal(0.5, report.PeakLocomotorRecruitment, 6);
    }

    [Fact]
    public void LatestCumulativeBrainAuthorityIsPreservedInRunReport()
    {
        var telemetry = new WorldRunTelemetryAccumulator();
        telemetry.ObserveActionAuthority(new ActionAuthorityCumulativeTelemetry(
            Samples: 120,
            CircuitObservedTicks: 118,
            AuthorityGrantedTicks: 9,
            AuthorityGrantEpisodes: 2,
            FirstAuthorityGrantTick: 31,
            LastAuthorityGrantTick: 104,
            Channels:
            [
                new ActionAuthorityChannelCumulativeTelemetry(
                    ChannelIndex: 3,
                    Samples: 118,
                    SelectedTicks: 12,
                    AuthorityGrantedTicks: 9,
                    PeakProposalDrive: 0.4f,
                    PeakDirectPathwayActivation: 0.5f,
                    PeakIndirectPathwayActivation: 0.2f,
                    PeakHyperdirectSuppression: 0.1f,
                    MinimumOutputNucleusInhibition: 0.15f,
                    PeakThalamicRelayActivation: 0.45f,
                    PeakSelectionScore: 0.33f,
                    PeakDirectActiveNeurons: 8,
                    PeakIndirectActiveNeurons: 5,
                    PeakDirectMeanUpState: 0.7f,
                    PeakIndirectMeanUpState: 0.4f)
            ]));

        var authority = Assert.IsType<ActionAuthorityCumulativeTelemetry>(
            telemetry.Capture().BrainActionAuthority);
        Assert.Equal(9, authority.AuthorityGrantedTicks);
        Assert.Equal(2, authority.AuthorityGrantEpisodes);
        Assert.Equal(0.5f, Assert.Single(authority.Channels).PeakDirectPathwayActivation);

        telemetry.Reset();
        Assert.Null(telemetry.Capture().BrainActionAuthority);
    }
}

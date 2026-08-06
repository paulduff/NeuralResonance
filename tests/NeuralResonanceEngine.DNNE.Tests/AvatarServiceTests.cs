using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class AvatarServiceTests
{
    [Fact]
    public void ServiceProcessesBrainSignalsOnWorker()
    {
        using var service = CreateService();
        var dispatches = new[]
        {
            new AvatarDispatchSpike("M1", "L", 100, "population:l:excitatory:1:0"),
            new AvatarDispatchSpike("M1", "R", 101, "population:r:excitatory:1:0")
        };

        service.PostBrainSignals(dispatches, AwakeBody);

        var signal = WaitForSignal(service, static s => s.MotorEvents == 2);

        Assert.True(signal.LeftMotorDrive > 0.0);
        Assert.True(signal.RightMotorDrive > 0.0);
        Assert.True(service.ProcessedCommands > 0);
    }

    [Fact]
    public void ServiceAppliesDecayAndComputesMotorOutputFromLatestSnapshot()
    {
        using var service = CreateService();
        service.PostAddMotorDrive(80.0, 40.0);
        WaitForSignal(service, static s => s.LeftMotorDrive >= 80.0 && s.RightMotorDrive >= 40.0);

        service.PostApplyDriveDecay(0.5);
        var decayed = WaitForSignal(service, static s => s.LeftMotorDrive < 80.0 && s.RightMotorDrive < 40.0);
        var output = service.ComputeMotorOutput();

        Assert.Equal(decayed.LeftMotorDrive, service.LatestSignal.LeftMotorDrive);
        Assert.NotEqual(0.0, output.ForwardSpeed);
        Assert.NotEqual(0.0, output.TurnRateDeg);
    }

    [Fact]
    public void ServiceClockAppliesDriveDecay()
    {
        using var service = CreateService(new AvatarServiceClockOptions(
            Enabled: true,
            TickIntervalMs: 20,
            DriveDecayOverride: 0.5));
        service.PostAddMotorDrive(100.0, 100.0);
        WaitForSignal(service, static s => s.LeftMotorDrive >= 100.0 && s.RightMotorDrive >= 100.0);

        var decayed = WaitForSignal(service, static s => s.LeftMotorDrive < 100.0 && s.RightMotorDrive < 100.0);

        Assert.True(decayed.LeftMotorDrive <= 50.0);
        Assert.True(decayed.RightMotorDrive <= 50.0);
        Assert.True(service.ClockTicks > 0);
    }

    [Fact]
    public void ServicePublishesAvatarActionOutput()
    {
        using var service = CreateService();
        service.PostAddMotorDrive(80.0, 40.0);
        WaitForSignal(service, static s => s.LeftMotorDrive >= 80.0 && s.RightMotorDrive >= 40.0);

        var output = service.PublishActionOutput(forwardGain: 1.5, turnGain: 0.75);
        var queued = WaitForActionOutput(service, item => item.EmittedUnixMs == output.EmittedUnixMs);

        Assert.Equal(output, queued);
        Assert.True(queued.Movement.ForwardSpeed > 0.0);
        Assert.NotEqual(0.0, queued.Movement.TurnRateDeg);
        Assert.Equal(AvatarToolAction.None, queued.Tool.Action);
        Assert.Equal("rest", queued.Attention.Mode);
        Assert.Equal("avatar_action", queued.OutputSource);
        Assert.Contains(service.RecentBodyEvents, static item => item.Kind == "effort" && item.Source == "avatar_action");
    }

    [Fact]
    public void ServicePublishesAttentionOutputFromRecentSensationMemory()
    {
        using var service = CreateService();
        service.PostObjectCandidates(
            [new AvatarObjectObservation("berry.patch", "food", 0.82, 0.74, 1.4, 18, 2.0, "L")],
            maxObservations: 1);

        var attention = WaitForAttentionOutput(service, static item =>
            item.Mode == "look" &&
            item.Target == "food:berry.patch");

        Assert.Equal("L", attention.Hemisphere);
        Assert.Equal(0.74, attention.Confidence);
        Assert.Equal(0.82, attention.Salience);
        Assert.Equal("avatar_attention", attention.OutputSource);
        Assert.Equal(attention, service.LatestAttentionOutput);
    }

    [Fact]
    public void ServicePublishesPeripheralActionOutputs()
    {
        using var service = CreateService();
        var profile = new AvatarBodyStateProfile(
            MaxForwardSpeed: 3.2,
            MaxTurnRateDeg: 220.0,
            BaseIntensity: 0.2,
            MotionIntensityWeight: 0.5,
            TurnIntensityWeight: 0.1,
            ContactIntensityWeight: 0.4,
            BaseBurstCount: 6.0,
            MotionBurstWeight: 8.0,
            TurnBurstWeight: 3.0,
            ContactBurstWeight: 6.0);
        var body = new AvatarBodyTelemetry(
            ForwardVelocity: 1.6,
            TurnRateDeg: 0.0,
            ContactLevel: 0.0,
            LeftMotorDrive: 18.0,
            RightMotorDrive: 18.0,
            Anxiety: 0.72,
            PredatorThreat: 0.66,
            Hunger: 0.30,
            Health: 0.82,
            Urgency: 0.70);

        service.PostBodyInput(body, profile);
        service.PostAudioOutput(new AvatarAudioOutput("AvatarSpeech", 0.8f, "I am alert."));

        var action = WaitForActionOutput(service, static item =>
            item.Voice?.Text == "I am alert." &&
            item.Gesture.Name == "brace" &&
            item.Arousal.Mode == "alarm" &&
            item.BodySound.Pattern == "footstep");

        Assert.Equal("AvatarSpeech", action.Voice?.Pattern);
        Assert.True(action.Arousal.Level >= 0.70);
        Assert.Equal("avatar_gesture", action.Gesture.OutputSource);
        Assert.Equal("avatar_arousal", action.Arousal.OutputSource);
        Assert.Equal("avatar_body_sound", action.BodySound.OutputSource);
        Assert.Equal(action.Voice, service.LatestVoiceOutput);
        Assert.Equal(action.Gesture, service.LatestGestureOutput);
        Assert.Equal(action.Arousal, service.LatestArousalOutput);
        Assert.Equal(action.BodySound, service.LatestBodySoundOutput);
    }

    [Fact]
    public void ServiceOwnsAuditoryInputSelection()
    {
        using var service = CreateService();
        var cues = new[]
        {
            new AvatarAuditoryCue("QuietFootstep", 0.3f, 8),
            new AvatarAuditoryCue("ThreatGrowl", 1.4f, 24, Hemisphere: "R"),
            new AvatarAuditoryCue("WaterLapping", 0.5f, 10)
        };

        service.PostAuditoryInputCandidates(cues, maxCues: 1);

        var heard = WaitForAuditoryInput(service);

        Assert.Equal("ThreatGrowl", heard.Pattern);
        Assert.Equal("R", heard.Hemisphere);
    }

    [Fact]
    public void ServiceOwnsAvatarAudioOutput()
    {
        using var service = CreateService();
        service.PostAudioOutput(new AvatarAudioOutput("AvatarSpeech", 0.8f, "I hear something."));

        var output = WaitForAudioOutput(service);

        Assert.Equal("AvatarSpeech", output.Pattern);
        Assert.Equal("I hear something.", output.Text);
    }

    [Fact]
    public void ServiceOwnsBodyStateAndOutcomeInputs()
    {
        using var service = CreateService();
        var body = new AvatarBodyTelemetry(
            ForwardVelocity: 1.2,
            TurnRateDeg: 0.4,
            ContactLevel: 0.2,
            LeftMotorDrive: 12.0,
            RightMotorDrive: 14.0);
        var profile = new AvatarBodyStateProfile(
            MaxForwardSpeed: 3.2,
            MaxTurnRateDeg: 220.0,
            BaseIntensity: 0.2,
            MotionIntensityWeight: 0.5,
            TurnIntensityWeight: 0.1,
            ContactIntensityWeight: 0.4,
            BaseBurstCount: 6.0,
            MotionBurstWeight: 8.0,
            TurnBurstWeight: 3.0,
            ContactBurstWeight: 6.0);
        var outcome = new AvatarOutcomeTelemetry(Progress: 0.6, EffortCost: 0.1);

        service.PostBodyInput(body, profile);
        service.PostOutcome(outcome);

        var bodyInput = WaitForBodyInput(service);
        var publishedOutcome = WaitForOutcome(service);

        Assert.Equal(1.2, bodyInput.Telemetry.ForwardVelocity);
        Assert.Equal(profile, bodyInput.Profile);
        Assert.Equal(0.6, publishedOutcome.Progress);
        Assert.Equal(0.1, publishedOutcome.EffortCost);
    }

    [Fact]
    public void ServiceOwnsObjectObservationSelection()
    {
        using var service = CreateService();
        var observations = new[]
        {
            new AvatarObjectObservation("food.near", "food", 0.55, 0.70, 1.1, 16, 4.0, "L"),
            new AvatarObjectObservation("danger.bear", "danger_predator", 0.95, 0.80, 2.3, 40, 2.0, "R"),
            new AvatarObjectObservation("water.far", "water", 0.35, 0.60, 0.8, 10, 9.0, null)
        };

        service.PostObjectCandidates(observations, maxObservations: 1);

        var selected = WaitForObjectObservation(service);

        Assert.Equal("danger.bear", selected.ObjectId);
        Assert.Equal("danger_predator", selected.Label);
        Assert.Equal("R", selected.Hemisphere);
    }

    [Fact]
    public void ServiceOwnsSightPreviewOutput()
    {
        using var service = CreateService();
        byte[] pixels = [11, 22, 33, 44];
        var frame = new AvatarSightFrame(
            Generation: 12,
            CaptureTimestampMs: 3456,
            Width: 1,
            Height: 1,
            Stride: 4,
            Pixels: pixels,
            PreviewHeadingDeg: 15.0);

        service.PostSightInputFrame(frame);

        var output = WaitForSightOutput(service);

        Assert.Equal(12, output.Generation);
        Assert.Same(pixels, output.Pixels);
        Assert.Equal(15.0, output.PreviewHeadingDeg);
        Assert.Equal(output, service.LatestSightOutput);
    }

    [Fact]
    public void ServiceCoalescesSightInputAndBoundsPublishedOutput()
    {
        using var service = CreateService();
        for (var generation = 1; generation <= 160; generation++)
        {
            service.PostSightInputFrame(new AvatarSightFrame(
                Generation: generation,
                CaptureTimestampMs: generation,
                Width: 1,
                Height: 1,
                Stride: 4,
                Pixels: [1, 2, 3, 255],
                PreviewHeadingDeg: 0.0));
        }

        var latest = WaitForSightOutput(service, frame => frame.Generation == 160);

        Assert.Equal(160, latest.Generation);
        Assert.InRange(service.PendingCommandCount, 0, 64);
        Assert.InRange(service.PublishedSightOutputCount, 0, 3);
    }

    [Fact]
    public void ServiceRejectsMalformedSightFramesBeforeTheyReachTheWorker()
    {
        using var service = CreateService();
        var malformed = new AvatarSightFrame(
            Generation: 1,
            CaptureTimestampMs: 1,
            Width: 1,
            Height: 1,
            Stride: 3,
            Pixels: [1, 2, 3],
            PreviewHeadingDeg: 0.0);

        Assert.Throws<ArgumentException>(() => service.PostSightInputFrame(malformed));
    }

    [Fact]
    public void ServiceOwnsPersistentPlaceMemory()
    {
        using var service = CreateService();
        var observations = new[]
        {
            new AvatarPlaceObservation(
                PlaceId: "shelter.den",
                Label: "shelter",
                X: 12.0,
                Y: 4.0,
                Z: 0.0,
                Safety: 0.90,
                Danger: 0.05,
                Food: 0.0,
                Blockage: 0.0,
                Interest: 0.38,
                Confidence: 0.86,
                Source: "world"),
            new AvatarPlaceObservation(
                PlaceId: "ridge.hazard",
                Label: "broken ledge",
                X: -2.0,
                Y: 9.0,
                Z: 0.0,
                Safety: 0.05,
                Danger: 0.82,
                Food: 0.0,
                Blockage: 0.35,
                Interest: 0.20,
                Confidence: 0.74,
                Source: "maze")
        };

        service.PostPlaceObservations(observations);
        service.PostPlaceObservations([
            new AvatarPlaceObservation(
                PlaceId: "shelter.den",
                Label: "warm shelter",
                X: 12.6,
                Y: 4.4,
                Z: 0.0,
                Safety: 0.76,
                Danger: 0.02,
                Food: 0.18,
                Blockage: 0.0,
                Interest: 0.56,
                Confidence: 0.92,
                Source: "world")
        ]);

        var memories = WaitForPlaceMemories(service, static memories =>
            memories.Any(static item => item.PlaceId == "shelter.den" && item.ObservationCount >= 2) &&
            memories.Any(static item => item.PlaceId == "ridge.hazard"));
        var shelter = memories.Single(static item => item.PlaceId == "shelter.den");
        var hazard = memories.Single(static item => item.PlaceId == "ridge.hazard");

        Assert.Equal("safe", shelter.DominantKind);
        Assert.Equal("warm shelter", shelter.Label);
        Assert.True(shelter.Safety > shelter.Danger);
        Assert.True(shelter.LastSeenUnixMs >= shelter.FirstSeenUnixMs);
        Assert.Equal("danger", hazard.DominantKind);
        Assert.True(hazard.Danger > hazard.Safety);
    }

    [Fact]
    public void ServicePublishesNonTechnicalSelfDiagnostics()
    {
        using var service = CreateService();
        service.PostBodyInput(
            new AvatarBodyTelemetry(
                ForwardVelocity: 0.4,
                TurnRateDeg: 8.0,
                ContactLevel: 0.0,
                LeftMotorDrive: 8.0,
                RightMotorDrive: 9.0,
                Hunger: 0.74),
            new AvatarBodyStateProfile(
                MaxForwardSpeed: 3.2,
                MaxTurnRateDeg: 220.0,
                BaseIntensity: 0.2,
                MotionIntensityWeight: 0.5,
                TurnIntensityWeight: 0.1,
                ContactIntensityWeight: 0.4,
                BaseBurstCount: 6.0,
                MotionBurstWeight: 8.0,
                TurnBurstWeight: 3.0,
                ContactBurstWeight: 6.0));
        service.PostObjectCandidates(
            [new AvatarObjectObservation("berry.patch", "food", 0.8, 0.7, 1.1, 20, 3.0, "L")],
            maxObservations: 1);
        service.PostAddMotorDrive(80.0, 80.0);

        WaitForActionOutput(service, static item => Math.Abs(item.Movement.ForwardSpeed) > 0.0);
        var diagnostics = service.CurrentSelfDiagnostics;

        Assert.Equal("hungry", diagnostics.BodyMood);
        Assert.Equal("food:berry.patch", diagnostics.AttentionTarget);
        Assert.Contains("moving", diagnostics.CurrentAction);
        Assert.Equal("saw food", diagnostics.LastSensation);
        Assert.False(string.IsNullOrWhiteSpace(diagnostics.CurrentNeed));
        Assert.True(diagnostics.UpdatedUnixMs > 0);
    }

    [Fact]
    public void ServiceMaintainsRecentSensationMemory()
    {
        using var service = CreateService();
        var body = new AvatarBodyTelemetry(
            ForwardVelocity: 0.0,
            TurnRateDeg: 0.0,
            ContactLevel: 0.0,
            LeftMotorDrive: 0.0,
            RightMotorDrive: 0.0,
            Hunger: 0.72);
        var profile = new AvatarBodyStateProfile(
            MaxForwardSpeed: 3.2,
            MaxTurnRateDeg: 220.0,
            BaseIntensity: 0.2,
            MotionIntensityWeight: 0.5,
            TurnIntensityWeight: 0.1,
            ContactIntensityWeight: 0.4,
            BaseBurstCount: 6.0,
            MotionBurstWeight: 8.0,
            TurnBurstWeight: 3.0,
            ContactBurstWeight: 6.0);
        var observations = new[]
        {
            new AvatarObjectObservation("berry.patch", "food", 0.80, 0.72, 1.2, 18, 3.0, "L")
        };

        service.PostBodyInput(body, profile);
        service.PostAuditoryInputCandidates([new AvatarAuditoryCue("WaterNearby", 0.7f, 12)], maxCues: 1);
        service.PostObjectCandidates(observations, maxObservations: 1);

        var memory = WaitForMemory(service, static memory =>
            memory.LastBodyState is not null &&
            memory.LastHeardSound?.Pattern == "WaterNearby" &&
            memory.LastSeenObject?.ObjectId == "berry.patch");

        Assert.Equal("hungry", memory.BodyMood);
        Assert.Equal("food:berry.patch", memory.AttentionTarget);
        Assert.True(memory.Revision >= 3);
        Assert.True(memory.UpdatedUnixMs > 0);
    }

    [Fact]
    public void ServiceMaintainsAvatarOwnedBodyEventLedger()
    {
        using var service = CreateService();
        var profile = new AvatarBodyStateProfile(
            MaxForwardSpeed: 3.2,
            MaxTurnRateDeg: 220.0,
            BaseIntensity: 0.2,
            MotionIntensityWeight: 0.5,
            TurnIntensityWeight: 0.1,
            ContactIntensityWeight: 0.4,
            BaseBurstCount: 6.0,
            MotionBurstWeight: 8.0,
            TurnBurstWeight: 3.0,
            ContactBurstWeight: 6.0);
        var body = new AvatarBodyTelemetry(
            ForwardVelocity: 1.4,
            TurnRateDeg: 12.0,
            ContactLevel: 0.42,
            LeftMotorDrive: 18.0,
            RightMotorDrive: 18.0,
            Anxiety: 0.64,
            Hunger: 0.73,
            Health: 0.92);
        var outcome = new AvatarOutcomeTelemetry(
            SafetyRelief: 0.44,
            Progress: 0.35,
            EffortCost: 0.25);

        service.PostBodyInput(body, profile);
        service.PostOutcome(outcome);

        var events = WaitForBodyEvents(service, static events =>
            events.Any(static item => item.Kind == "movement") &&
            events.Any(static item => item.Kind == "impact") &&
            events.Any(static item => item.Kind == "hunger") &&
            events.Any(static item => item.Kind == "fear") &&
            events.Any(static item => item.Kind == "relief") &&
            events.Any(static item => item.Kind == "progress") &&
            events.Any(static item => item.Kind == "fatigue"));

        Assert.True(events.All(static item => item.ObservedUnixMs > 0));
        Assert.Contains(events, static item => item.Source == AvatarRuntimeDefaults.OutcomeInputSource);
    }

    [Fact]
    public void ServicePublishesNeedsAndRhythmsFromBodyLedger()
    {
        using var service = CreateService();
        var profile = new AvatarBodyStateProfile(
            MaxForwardSpeed: 3.2,
            MaxTurnRateDeg: 220.0,
            BaseIntensity: 0.2,
            MotionIntensityWeight: 0.5,
            TurnIntensityWeight: 0.1,
            ContactIntensityWeight: 0.4,
            BaseBurstCount: 6.0,
            MotionBurstWeight: 8.0,
            TurnBurstWeight: 3.0,
            ContactBurstWeight: 6.0);
        var body = new AvatarBodyTelemetry(
            ForwardVelocity: 2.1,
            TurnRateDeg: 10.0,
            ContactLevel: 0.20,
            LeftMotorDrive: 24.0,
            RightMotorDrive: 24.0,
            EnvironmentalDarkness: 0.70,
            Anxiety: 0.68,
            Hunger: 0.76,
            Health: 0.80,
            Urgency: 0.72);
        var outcome = new AvatarOutcomeTelemetry(
            EffortCost: 0.64,
            Novelty: 0.58);

        service.PostBodyInput(body, profile);
        service.PostOutcome(outcome);

        var needs = WaitForNeedsRhythm(service, static state =>
            state.Hunger >= 0.70 &&
            state.Stress >= 0.60 &&
            state.Fatigue >= 0.35 &&
            state.SleepPressure >= 0.45 &&
            state.Curiosity >= 0.10 &&
            state.RestNeed >= 0.35);
        var action = service.PublishActionOutput();

        Assert.Equal(needs, service.LatestNeedsRhythmState);
        Assert.Equal(needs, action.Needs);
        Assert.Contains(needs.DominantNeed, new[] { "hunger", "stress", "rest", "curiosity", "sleep" });
        Assert.True(needs.UpdatedUnixMs > 0);
    }

    [Fact]
    public void ServicePublishesPeripheralReflexes()
    {
        using var service = CreateService();
        var profile = new AvatarBodyStateProfile(
            MaxForwardSpeed: 3.2,
            MaxTurnRateDeg: 220.0,
            BaseIntensity: 0.2,
            MotionIntensityWeight: 0.5,
            TurnIntensityWeight: 0.1,
            ContactIntensityWeight: 0.4,
            BaseBurstCount: 6.0,
            MotionBurstWeight: 8.0,
            TurnBurstWeight: 3.0,
            ContactBurstWeight: 6.0);
        var body = new AvatarBodyTelemetry(
            ForwardVelocity: 0.8,
            TurnRateDeg: 0.0,
            ContactLevel: 0.0,
            LeftMotorDrive: 0.0,
            RightMotorDrive: 0.0,
            PainLevel: 0.62,
            Health: 0.72);

        service.PostAddMotorDrive(100.0, 100.0);
        WaitForSignal(service, static signal => signal.LeftMotorDrive >= 100.0);
        service.PostBodyInput(body, profile);

        var action = WaitForActionOutput(service, static item => item.Reflex.Name == "flinch");
        var reflex = WaitForReflexOutput(service, static item => item.Name == "flinch");

        Assert.Equal(reflex, action.Reflex);
        Assert.Equal("pain", reflex.Target);
        Assert.True(reflex.Intensity >= 0.60);
        Assert.True(reflex.ForwardScale < 1.0);
        Assert.True(action.Movement.ForwardSpeed < service.ComputeMotorOutput().ForwardSpeed);
        Assert.Equal(reflex, service.LatestReflexOutput);
    }

    [Fact]
    public void ServicePrioritizesDirectionalWithdrawalForPainfulWallContact()
    {
        using var service = CreateService();
        var profile = CreateBodyStateProfile();
        service.PostAddMotorDrive(100.0, 100.0);
        WaitForSignal(service, static signal => signal.LeftMotorDrive >= 100.0);
        service.PostBodyInput(
            new AvatarBodyTelemetry(
                ForwardVelocity: 0.8,
                TurnRateDeg: 0.0,
                ContactLevel: 0.95,
                LeftMotorDrive: 100.0,
                RightMotorDrive: 100.0,
                TactileFront: 0.72,
                TactileLeft: 0.94,
                TactileRight: 0.08,
                PainLevel: 0.62,
                Health: 0.72),
            profile);

        var action = WaitForActionOutput(service, static item => item.Reflex.Name == "withdraw_contact");

        Assert.Equal("contact", action.Reflex.Target);
        Assert.True(action.Reflex.TurnBiasDeg > 50.0);
        Assert.True(action.Reflex.ForwardScale < 0.20);
        Assert.True(action.Movement.TurnRateDeg > 0.0);
        Assert.True(action.Movement.ForwardSpeed < service.ComputeMotorOutput().ForwardSpeed);
    }

    [Fact]
    public void ServiceContinuesExistingTurnForSymmetricHeadOnContact()
    {
        using var service = CreateService();
        service.PostBodyInput(
            new AvatarBodyTelemetry(
                ForwardVelocity: 0.4,
                TurnRateDeg: -18.0,
                ContactLevel: 0.90,
                LeftMotorDrive: 40.0,
                RightMotorDrive: 40.0,
                TactileFront: 0.96,
                TactileLeft: 0.48,
                TactileRight: 0.48,
                PainLevel: 0.42),
            CreateBodyStateProfile());

        var reflex = WaitForReflexOutput(service, static item => item.Name == "withdraw_contact");

        Assert.True(reflex.TurnBiasDeg < 0.0);
    }

    [Fact]
    public void ServiceDoesNotWithdrawFromNearbyWallWithoutContact()
    {
        using var service = CreateService(new AvatarServiceClockOptions(Enabled: false));
        service.PostBodyInput(
            new AvatarBodyTelemetry(
                ForwardVelocity: 0.4,
                TurnRateDeg: 0.0,
                ContactLevel: 0.02,
                LeftMotorDrive: 40.0,
                RightMotorDrive: 40.0,
                TactileFront: 0.82,
                TactileLeft: 0.72,
                TactileRight: 0.04,
                PainLevel: 0.0),
            CreateBodyStateProfile());
        WaitForBodyInput(service);

        var action = service.PublishActionOutput();

        Assert.Equal("none", action.Reflex.Name);
    }

    [Fact]
    public void ServicePublishesAffectiveWeather()
    {
        using var service = CreateService();
        var profile = new AvatarBodyStateProfile(
            MaxForwardSpeed: 3.2,
            MaxTurnRateDeg: 220.0,
            BaseIntensity: 0.2,
            MotionIntensityWeight: 0.5,
            TurnIntensityWeight: 0.1,
            ContactIntensityWeight: 0.4,
            BaseBurstCount: 6.0,
            MotionBurstWeight: 8.0,
            TurnBurstWeight: 3.0,
            ContactBurstWeight: 6.0);
        var body = new AvatarBodyTelemetry(
            ForwardVelocity: 0.4,
            TurnRateDeg: 0.0,
            ContactLevel: 0.0,
            LeftMotorDrive: 0.0,
            RightMotorDrive: 0.0,
            Anxiety: 0.74,
            PredatorThreat: 0.70,
            Hunger: 0.20,
            Health: 0.90);

        service.PostBodyInput(body, profile);

        var weather = WaitForAffectiveWeather(service, static item => item.State == "tense");
        var action = WaitForActionOutput(service, static item => item.Weather.State == "tense");

        Assert.True(weather.Valence < 0.0);
        Assert.True(weather.Arousal > 0.0);
        Assert.Equal("avatar_affective_weather", weather.OutputSource);
        Assert.Equal(weather, service.LatestAffectiveWeather);
        Assert.Equal(weather, action.Weather);
        Assert.Equal("brace", action.Gesture.Name);
    }

    [Fact]
    public void ServiceOrientsReflexTowardSuddenSound()
    {
        using var service = CreateService();
        service.PostAuditoryInputCandidates(
            [new AvatarAuditoryCue("sharp_sound", 0.80f, 18, Hemisphere: "R")],
            maxCues: 1);

        var reflex = WaitForReflexOutput(service, static item => item.Name == "orient_to_sound");

        Assert.Equal("sharp_sound", reflex.Target);
        Assert.True(reflex.TurnBiasDeg > 0.0);
        Assert.True(reflex.ForwardScale < 1.0);
    }

    private static AvatarService CreateService(AvatarServiceClockOptions? clockOptions = null)
        => new(new AvatarNervousSystemOptions(
            new AvatarKinematicsOptions(
                MaxMotorDrive: 240.0,
                ForwardSpeedCoefficient: 0.0125,
                TurnSpeedCoefficient: 3.2,
                MinForwardSpeed: 0.0,
                MaxForwardSpeed: 3.2,
                MaxTurnRateDeg: 220.0),
            IdleMotorFallbackTicks: int.MaxValue),
            name: "NRE.Tests.AvatarService",
            clockOptions: clockOptions);

    private static AvatarBodyStateProfile CreateBodyStateProfile()
        => new(
            MaxForwardSpeed: 3.2,
            MaxTurnRateDeg: 220.0,
            BaseIntensity: 0.2,
            MotionIntensityWeight: 0.5,
            TurnIntensityWeight: 0.1,
            ContactIntensityWeight: 0.4,
            BaseBurstCount: 6.0,
            MotionBurstWeight: 8.0,
            TurnBurstWeight: 3.0,
            ContactBurstWeight: 6.0);

    private static AvatarNervousSystemBodyState AwakeBody { get; } = new(
        Hunger: 0.2,
        Threat: 0.1,
        Health: 1.0,
        SecondsSinceProgress: 0.0,
        NoProgressTimeoutSeconds: 4.0);

    private static AvatarNervousSystemSignal WaitForSignal(
        AvatarService service,
        Func<AvatarNervousSystemSignal, bool> predicate)
    {
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (DateTime.UtcNow < deadline)
        {
            while (service.TryDequeueSignal(out var queued))
            {
                if (predicate(queued))
                {
                    return queued;
                }
            }

            var latest = service.LatestSignal;
            if (predicate(latest))
            {
                return latest;
            }

            Thread.Sleep(10);
        }

        throw new TimeoutException("Avatar service did not publish the expected signal.");
    }

    private static AvatarAuditoryCue WaitForAuditoryInput(AvatarService service)
    {
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (DateTime.UtcNow < deadline)
        {
            if (service.TryDequeueAuditoryInput(out var cue))
            {
                return cue;
            }

            Thread.Sleep(10);
        }

        throw new TimeoutException("Avatar service did not publish an auditory input.");
    }

    private static AvatarAudioOutput WaitForAudioOutput(AvatarService service)
    {
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (DateTime.UtcNow < deadline)
        {
            if (service.TryDequeueAudioOutput(out var output))
            {
                return output;
            }

            Thread.Sleep(10);
        }

        throw new TimeoutException("Avatar service did not publish an audio output.");
    }

    private static AvatarBodyStateInput WaitForBodyInput(AvatarService service)
    {
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (DateTime.UtcNow < deadline)
        {
            if (service.TryDequeueBodyInput(out var input))
            {
                return input;
            }

            Thread.Sleep(10);
        }

        throw new TimeoutException("Avatar service did not publish a body input.");
    }

    private static AvatarOutcomeTelemetry WaitForOutcome(AvatarService service)
    {
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (DateTime.UtcNow < deadline)
        {
            if (service.TryDequeueOutcome(out var outcome))
            {
                return outcome;
            }

            Thread.Sleep(10);
        }

        throw new TimeoutException("Avatar service did not publish an outcome.");
    }

    private static AvatarObjectObservation WaitForObjectObservation(AvatarService service)
    {
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (DateTime.UtcNow < deadline)
        {
            if (service.TryDequeueObjectObservation(out var observation))
            {
                return observation;
            }

            Thread.Sleep(10);
        }

        throw new TimeoutException("Avatar service did not publish an object observation.");
    }

    private static AvatarSightFrame WaitForSightOutput(AvatarService service, Func<AvatarSightFrame, bool>? predicate = null)
    {
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (DateTime.UtcNow < deadline)
        {
            while (service.TryDequeueSightOutput(out var frame))
            {
                if (predicate is null || predicate(frame))
                {
                    return frame;
                }
            }

            Thread.Sleep(10);
        }

        throw new TimeoutException("Avatar service did not publish the expected sight output.");
    }

    private static AvatarActionOutput WaitForActionOutput(
        AvatarService service,
        Func<AvatarActionOutput, bool> predicate)
    {
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (DateTime.UtcNow < deadline)
        {
            while (service.TryDequeueActionOutput(out var output))
            {
                if (predicate(output))
                {
                    return output;
                }
            }

            Thread.Sleep(10);
        }

        throw new TimeoutException("Avatar service did not publish an action output.");
    }

    private static AvatarAttentionOutput WaitForAttentionOutput(
        AvatarService service,
        Func<AvatarAttentionOutput, bool> predicate)
    {
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (DateTime.UtcNow < deadline)
        {
            while (service.TryDequeueAttentionOutput(out var output))
            {
                if (predicate(output))
                {
                    return output;
                }
            }

            Thread.Sleep(10);
        }

        throw new TimeoutException("Avatar service did not publish an attention output.");
    }

    private static AvatarSensationMemory WaitForMemory(
        AvatarService service,
        Func<AvatarSensationMemory, bool> predicate)
    {
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (DateTime.UtcNow < deadline)
        {
            var memory = service.RecentSensationMemory;
            if (predicate(memory))
            {
                return memory;
            }

            Thread.Sleep(10);
        }

        throw new TimeoutException("Avatar service did not update recent sensation memory.");
    }

    private static IReadOnlyList<AvatarBodyEvent> WaitForBodyEvents(
        AvatarService service,
        Func<IReadOnlyList<AvatarBodyEvent>, bool> predicate)
    {
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (DateTime.UtcNow < deadline)
        {
            var events = service.RecentBodyEvents;
            if (predicate(events))
            {
                return events;
            }

            Thread.Sleep(10);
        }

        throw new TimeoutException("Avatar service did not update body event ledger.");
    }

    private static IReadOnlyList<AvatarPlaceMemory> WaitForPlaceMemories(
        AvatarService service,
        Func<IReadOnlyList<AvatarPlaceMemory>, bool> predicate)
    {
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (DateTime.UtcNow < deadline)
        {
            var memories = service.PlaceMemories;
            if (predicate(memories))
            {
                return memories;
            }

            Thread.Sleep(10);
        }

        throw new TimeoutException("Avatar service did not update place memory.");
    }

    private static AvatarNeedsRhythmState WaitForNeedsRhythm(
        AvatarService service,
        Func<AvatarNeedsRhythmState, bool> predicate)
    {
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (DateTime.UtcNow < deadline)
        {
            while (service.TryDequeueNeedsRhythmState(out var state))
            {
                if (predicate(state))
                {
                    return state;
                }
            }

            var latest = service.LatestNeedsRhythmState;
            if (predicate(latest))
            {
                return latest;
            }

            Thread.Sleep(10);
        }

        throw new TimeoutException("Avatar service did not publish needs rhythm state.");
    }

    private static AvatarReflexOutput WaitForReflexOutput(
        AvatarService service,
        Func<AvatarReflexOutput, bool> predicate)
    {
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (DateTime.UtcNow < deadline)
        {
            while (service.TryDequeueReflexOutput(out var output))
            {
                if (predicate(output))
                {
                    return output;
                }
            }

            var latest = service.LatestReflexOutput;
            if (predicate(latest))
            {
                return latest;
            }

            Thread.Sleep(10);
        }

        throw new TimeoutException("Avatar service did not publish reflex output.");
    }

    private static AvatarAffectiveWeather WaitForAffectiveWeather(
        AvatarService service,
        Func<AvatarAffectiveWeather, bool> predicate)
    {
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (DateTime.UtcNow < deadline)
        {
            while (service.TryDequeueAffectiveWeather(out var weather))
            {
                if (predicate(weather))
                {
                    return weather;
                }
            }

            var latest = service.LatestAffectiveWeather;
            if (predicate(latest))
            {
                return latest;
            }

            Thread.Sleep(10);
        }

        throw new TimeoutException("Avatar service did not publish affective weather.");
    }
}

using NeuralResonanceEngine.Shared.Contracts;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class VestibularLabyrinthTransducerTests
{
    [Fact]
    public void RestingLabyrinthHasSymmetricCanalToneAndGravityBearingSaccule()
    {
        var transducer = new VestibularLabyrinthTransducer();
        var frame = Descriptor(sequence: 1, timestampMs: 1_000, source: "resting_labyrinth");

        var output = transducer.Transduce(frame, previous: null, deltaSeconds: 0f);

        Assert.Equal(
            Activation(output.Left, "horizontal_canal"),
            Activation(output.Right, "horizontal_canal"),
            precision: 5);
        Assert.Equal(
            Activation(output.Left, "anterior_canal"),
            Activation(output.Right, "anterior_canal"),
            precision: 5);
        Assert.True(Activation(output.Left, "saccule_down") > 0.95f);
        Assert.Equal(0f, Activation(output.Left, "saccule_up"));
    }

    [Fact]
    public void YawRotationProducesBilateralPushPullHorizontalCanalActivity()
    {
        var positive = new VestibularLabyrinthTransducer();
        var positiveRest = Descriptor(1, 1_000, "positive_yaw");
        positive.Transduce(positiveRest, null, 0f);
        var positiveTurn = Descriptor(2, 1_100, "positive_yaw", angularY: 1.4f);
        var positiveOutput = positive.Transduce(positiveTurn, positiveRest, 0.1f);

        Assert.True(
            Activation(positiveOutput.Left, "horizontal_canal") >
            Activation(positiveOutput.Right, "horizontal_canal"));

        var negative = new VestibularLabyrinthTransducer();
        var negativeRest = Descriptor(1, 1_000, "negative_yaw");
        negative.Transduce(negativeRest, null, 0f);
        var negativeTurn = Descriptor(2, 1_100, "negative_yaw", angularY: -1.4f);
        var negativeOutput = negative.Transduce(negativeTurn, negativeRest, 0.1f);

        Assert.True(
            Activation(negativeOutput.Right, "horizontal_canal") >
            Activation(negativeOutput.Left, "horizontal_canal"));
    }

    [Fact]
    public void SustainedYawAdaptsAndStoppingProducesOppositeAfterResponse()
    {
        var transducer = new VestibularLabyrinthTransducer();
        var previous = Descriptor(1, 1_000, "canal_adaptation");
        transducer.Transduce(previous, null, 0f);

        var firstTurn = Descriptor(2, 1_100, "canal_adaptation", angularY: 1.2f);
        var firstOutput = transducer.Transduce(firstTurn, previous, 0.1f);
        var initialExcitation = Activation(firstOutput.Left, "horizontal_canal");
        previous = firstTurn;

        VestibularLabyrinthActivations sustainedOutput = firstOutput;
        for (var index = 0; index < 60; index++)
        {
            var current = Descriptor(
                sequence: 3 + index,
                timestampMs: 1_200 + (index * 100),
                source: "canal_adaptation",
                angularY: 1.2f);
            sustainedOutput = transducer.Transduce(current, previous, 0.1f);
            previous = current;
        }

        Assert.True(Activation(sustainedOutput.Left, "horizontal_canal") < initialExcitation);

        var stopped = Descriptor(63, 7_200, "canal_adaptation");
        var stopOutput = transducer.Transduce(stopped, previous, 0.1f);
        Assert.True(
            Activation(stopOutput.Right, "horizontal_canal") >
            Activation(stopOutput.Left, "horizontal_canal"));
    }

    [Fact]
    public void NeckPitchRecruitsAnteriorPosteriorCanalPairs()
    {
        var transducer = new VestibularLabyrinthTransducer();
        var rest = Descriptor(1, 1_000, "neck_pitch");
        transducer.Transduce(rest, null, 0f);
        var pitched = Descriptor(
            2,
            1_100,
            "neck_pitch",
            articulation: PhysicalArticulationFrame.Neutral with { NeckPitchRadians = 0.20f });

        var output = transducer.Transduce(pitched, rest, 0.1f);

        Assert.True(
            Activation(output.Left, "anterior_canal") >
            Activation(output.Left, "posterior_canal"));
        Assert.True(
            Activation(output.Right, "anterior_canal") >
            Activation(output.Right, "posterior_canal"));
    }

    [Fact]
    public void StaticRollTiltsOtolithsWithoutCreatingCanalRotation()
    {
        var balance = PhysicalBalanceStateFrame.Neutral with { FallRollRadians = 0.32f };
        var articulation = PhysicalArticulationFrame.Neutral with
        {
            Musculoskeletal = MusculoskeletalStateFrame.Neutral with { Balance = balance }
        };
        var transducer = new VestibularLabyrinthTransducer();
        var frame = Descriptor(1, 1_000, "static_roll", articulation: articulation);

        var output = transducer.Transduce(frame, null, 0f);

        Assert.True(Activation(output.Left, "otolith_roll_right") > 0.30f);
        Assert.Equal(0f, Activation(output.Left, "otolith_roll_left"));
        Assert.Equal(
            Activation(output.Left, "horizontal_canal"),
            Activation(output.Right, "horizontal_canal"),
            precision: 5);
        Assert.Equal(0.20f, Activation(output.Left, "horizontal_canal"), precision: 5);
    }

    [Fact]
    public void LabyrinthDoesNotReceiveHostComputedSupportMargin()
    {
        var balance = PhysicalBalanceStateFrame.Neutral with
        {
            SupportMarginMeters = -0.22f,
            Phase = "falling"
        };
        var articulation = PhysicalArticulationFrame.Neutral with
        {
            Musculoskeletal = MusculoskeletalStateFrame.Neutral with { Balance = balance }
        };
        var transducer = new VestibularLabyrinthTransducer();

        var output = transducer.Transduce(
            Descriptor(1, 1_000, "margin_boundary", articulation: articulation),
            null,
            0f);

        Assert.DoesNotContain(output.Left, population =>
            population.Receptor.Contains("margin", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(output.Right, population =>
            population.Receptor.Contains("margin", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SequenceResetCannotCreateFalseHeadAccelerationBurst()
    {
        var transducer = new VestibularLabyrinthTransducer();
        var oldBody = Descriptor(
            80,
            8_000,
            "reset_labyrinth",
            angularY: 1.8f,
            articulation: PhysicalArticulationFrame.Neutral with
            {
                NeckPitchRadians = 0.6f,
                NeckYawRadians = 0.7f
            });
        transducer.Transduce(oldBody, null, 0f);
        var resetBody = Descriptor(1, 100, "reset_labyrinth");

        var output = transducer.Transduce(resetBody, oldBody, 0.1f);

        Assert.Equal(
            Activation(output.Left, "horizontal_canal"),
            Activation(output.Right, "horizontal_canal"),
            precision: 5);
        Assert.Equal(
            Activation(output.Left, "anterior_canal"),
            Activation(output.Left, "posterior_canal"),
            precision: 5);
        Assert.Equal(0f, Activation(output.Left, "utricle_left"));
        Assert.Equal(0f, Activation(output.Left, "utricle_right"));
    }

    private static float Activation(
        IReadOnlyList<(string Receptor, float Activation)> populations,
        string receptor) =>
        Assert.Single(populations, population => population.Receptor == receptor).Activation;

    private static PhysicalBodyFrameDescriptor Descriptor(
        long sequence,
        long timestampMs,
        string source,
        float angularY = 0f,
        PhysicalArticulationFrame? articulation = null) =>
        new(
            sequence,
            timestampMs,
            LinearVelocityX: 0f,
            LinearVelocityY: 0f,
            LinearVelocityZ: 0f,
            AngularVelocityX: 0f,
            AngularVelocityY: angularY,
            AngularVelocityZ: 0f,
            StoredEnergyJoules: 8_000_000f,
            TissueIntegrityFraction: 1f,
            CoreTemperatureCelsius: 37f,
            BloodOxygenSaturationFraction: 0.98f,
            HydrationFraction: 0.8f,
            Articulation: articulation ?? PhysicalArticulationFrame.Neutral,
            InputSource: source,
            MotorTrainingMode: false,
            SaturatedMuscleVelocityCount: 0);
}

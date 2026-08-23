using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class SensorimotorTickSchedulerTests
{
    [Fact]
    public void StableLaptopBudgetPrioritizesSensorimotorLaneWithoutStarvingGeneralLane()
    {
        var services = Enumerable.Range(0, 40)
            .Select(index => Instance(StructureId.ProprioceptiveAfferents, $"fast-{index}"))
            .Concat(Enumerable.Range(0, 30)
                .Select(index => Instance(StructureId.CA1, $"general-{index}")))
            .ToArray();
        var fastCursor = 0;
        var generalCursor = 0;

        var first = SensorimotorTickScheduler.Select(
            services,
            ref fastCursor,
            ref generalCursor,
            maxTickRequestConcurrency: 10,
            adaptivePressure: 0.0,
            startupWarmup: false);
        var second = SensorimotorTickScheduler.Select(
            services,
            ref fastCursor,
            ref generalCursor,
            maxTickRequestConcurrency: 10,
            adaptivePressure: 0.0,
            startupWarmup: false);

        Assert.Equal(30, first.Participants.Count);
        Assert.Equal(25, first.FastLaneSelected);
        Assert.Equal(5, first.GeneralLaneSelected);
        Assert.Equal(5, second.GeneralLaneSelected);
        Assert.NotEqual(
            first.Participants.First().InstanceKey,
            second.Participants.First().InstanceKey);
        Assert.DoesNotContain(
            first.Participants.Where(static service => service.StructureId == StructureId.CA1)
                .Select(static service => service.InstanceKey),
            second.Participants.Where(static service => service.StructureId == StructureId.CA1)
                .Select(static service => service.InstanceKey).Contains);
    }

    [Fact]
    public void PressureReducesBudgetButRetainsBothLanes()
    {
        var services = Enumerable.Range(0, 20)
            .Select(index => Instance(StructureId.SpinalCordMotor, $"fast-{index}"))
            .Concat(Enumerable.Range(0, 20)
                .Select(index => Instance(StructureId.CA3, $"general-{index}")))
            .ToArray();
        var fastCursor = 0;
        var generalCursor = 0;

        var selected = SensorimotorTickScheduler.Select(
            services,
            ref fastCursor,
            ref generalCursor,
            maxTickRequestConcurrency: 10,
            adaptivePressure: 1.0,
            startupWarmup: false);

        Assert.Equal(10, selected.Participants.Count);
        Assert.Equal(8, selected.FastLaneSelected);
        Assert.Equal(2, selected.GeneralLaneSelected);
    }

    [Fact]
    public void CadenceTrackerReportsPerInstancePhysicalIntervalsAndAge()
    {
        var services = new[]
        {
            Instance(StructureId.ProprioceptiveAfferents, "left-proprio"),
            Instance(StructureId.SpinalCordMotor, "left-spinal")
        };
        var fastCursor = 0;
        var generalCursor = 0;
        var tracker = new SensorimotorCadenceTracker();

        var firstSelection = SensorimotorTickScheduler.Select(
            services,
            ref fastCursor,
            ref generalCursor,
            maxTickRequestConcurrency: 2,
            adaptivePressure: 0.0,
            startupWarmup: false);
        tracker.Observe(1, 100, physicalBodyInputAgeMilliseconds: 8, firstSelection);
        var secondSelection = SensorimotorTickScheduler.Select(
            services,
            ref fastCursor,
            ref generalCursor,
            maxTickRequestConcurrency: 2,
            adaptivePressure: 0.0,
            startupWarmup: false);
        var timing = tracker.Observe(2, 125, physicalBodyInputAgeMilliseconds: 4, secondSelection);

        Assert.Equal(4, timing.PhysicalBodyInputAgeMilliseconds);
        Assert.Equal(25.0, timing.FastLaneCadenceMeanMilliseconds, precision: 6);
        Assert.Equal(25.0, timing.FastLaneCadenceMaxMilliseconds, precision: 6);
        Assert.Equal(0, timing.FastLaneOldestAgeMilliseconds);
        Assert.All(timing.Instances, instance =>
        {
            Assert.Equal(2, instance.SelectionCount);
            Assert.Equal(25.0, instance.CadenceEmaMilliseconds, precision: 6);
            Assert.Equal(0, instance.AgeMilliseconds);
        });
    }

    [Fact]
    public void IntegrationCadenceCarriesSkippedBiologicalTimeForward()
    {
        var cadence = new StructureIntegrationCadence();
        var instance = Instance(StructureId.Striatum, "left-striatum");

        var first = cadence.CreateSignal(instance, Tick(1, 1.0));
        Assert.Equal(1.0, first.TickDurationMs, precision: 6);
        cadence.MarkSuccessful(instance, first);

        var afterTwoSkippedTicks = cadence.CreateSignal(instance, Tick(4, 4.0));
        Assert.Equal(3.0, afterTwoSkippedTicks.TickDurationMs, precision: 6);
        cadence.MarkSuccessful(instance, afterTwoSkippedTicks);

        var next = cadence.CreateSignal(instance, Tick(5, 5.0));
        Assert.Equal(1.0, next.TickDurationMs, precision: 6);
    }

    [Fact]
    public void FailedStepDoesNotConsumeElapsedBiologicalTimeAndRecoveryIsBounded()
    {
        var cadence = new StructureIntegrationCadence();
        var instance = Instance(StructureId.RapheNuclei, "left-raphe");
        var first = cadence.CreateSignal(instance, Tick(1, 1.0));
        cadence.MarkSuccessful(instance, first);

        var failedAttempt = cadence.CreateSignal(instance, Tick(20, 20.0));
        Assert.Equal(19.0, failedAttempt.TickDurationMs, precision: 6);

        var retry = cadence.CreateSignal(instance, Tick(21, 21.0));
        Assert.Equal(20.0, retry.TickDurationMs, precision: 6);

        var boundedRecovery = cadence.CreateSignal(instance, Tick(500, 500.0));
        Assert.Equal(StructureIntegrationCadence.MaximumCatchUpMilliseconds, boundedRecovery.TickDurationMs, precision: 6);
    }

    private static TickSignal Tick(long tick, double timestampMs)
        => new(
            tick,
            timestampMs,
            1.0,
            new NeuromodState(),
            new Dictionary<BrainRhythm, double>(),
            0f);

    private static ServiceInstance Instance(StructureId structureId, string key)
        => new(structureId, key, "L", new Uri($"http://127.0.0.1:{10000 + Math.Abs(key.GetHashCode()) % 40000}"));
}

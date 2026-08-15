using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;
using NRE.WorldSim;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class ClosedNeuronalLoopTests
{
    [Theory]
    [InlineData(StructureId.PurkinjeCellLayer, "HH")]
    [InlineData(StructureId.InferiorOlive, "HH")]
    [InlineData(StructureId.CerebellarGranule, "LIF")]
    [InlineData(StructureId.CerebellarVermis, "HH")]
    [InlineData(StructureId.CerebellarLobules, "LIF")]
    [InlineData(StructureId.Striatum, "LIF")]
    [InlineData(StructureId.GPe, "LIF")]
    [InlineData(StructureId.GPi, "LIF")]
    [InlineData(StructureId.Snr, "LIF")]
    [InlineData(StructureId.Snc, "Izhikevich")]
    [InlineData(StructureId.Vta, "Izhikevich")]
    [InlineData(StructureId.Habenula, "Izhikevich")]
    public void PreviouslySilentCircuitPopulationsGenerateIntrinsicSpikes(
        StructureId structure,
        string neuronModel)
    {
        var profile = StructureCircuitProfile.For(structure);
        var neurons = Enumerable.Range(0, 16)
            .Select(index => new ModelNeuron(index, neuronModel, profile))
            .ToArray();
        var spikeCount = 0;

        for (var tick = 0L; tick < 6_000; tick++)
        {
            foreach (var neuron in neurons)
            {
                IntrinsicCircuitDriveTopology.Resolve(
                    structure,
                    neuron.Index,
                    tick,
                    out var excitation,
                    out var inhibition);
                neuron.IntegratePacemakerDrive(excitation, inhibition);
                if (neuron.Step(1.0))
                {
                    spikeCount++;
                }
            }
        }

        Assert.True(spikeCount > 0, $"{structure} remained electrically silent for six simulated seconds.");
        Assert.True(
            spikeCount <= 19_200,
            $"{structure} fired above 200 Hz per neuron for six simulated seconds ({spikeCount} spikes).");
    }

    [Fact]
    public async Task QuiescenceWaitsForCurrentTickAndBlocksTheNextTickUntilResume()
    {
        var state = new SimulationQuiescenceState();
        var firstTick = await state.EnterTickAsync(CancellationToken.None);
        var quiesce = state.QuiesceAsync(CancellationToken.None);

        Assert.False(quiesce.IsCompleted);
        firstTick.Dispose();
        var frozen = await quiesce;
        Assert.True(frozen.IsQuiesced);
        Assert.Equal(0, frozen.ActiveTicks);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var blockedTick = state.EnterTickAsync(timeout.Token).AsTask();
        await Task.Delay(30, timeout.Token);
        Assert.False(blockedTick.IsCompleted);

        state.Resume();
        using var secondTick = await blockedTick;
        Assert.False(state.GetSnapshot().PauseRequested);
    }

    [Fact]
    public void EmbodiedFramesPopulateTheActiveCurriculumStage()
    {
        var state = new SimulationState();
        state.Configure(1.0, [], []);
        var transducer = new PhysicalBodyTransducerRuntime();
        Assert.True(PhysicalBodyFrameDescriptor.TryCreate(
            new PhysicalBodyFrameRequest(
                1, 1_000, 0.4f, 0f, 1.2f, 0f, 0.3f, 0f,
                5_000_000f, 0.8f, 37f, 0.96f, 0.7f, "curriculum_body"),
            out var descriptor,
            out var error), error);

        var transduction = transducer.Transduce(descriptor!, 1, 1);
        state.ObserveEmbodiedCurriculum(transduction);

        Assert.Equal(0, state.Curriculum.StageIndex);
        Assert.All(
            state.Curriculum.Tasks.Where(task => task.StageIndex == 0),
            task => Assert.Equal(1, task.Samples));
    }

    [Fact]
    public async Task PausingWorldWritesAnAtomicPersistentRunReport()
    {
        var directory = Path.Combine(Path.GetTempPath(), "dnne-world-report-tests", Guid.NewGuid().ToString("N"));
        try
        {
            await using var runtime = new HeadlessWorldRuntime(new HeadlessWorldOptions(
                new Uri("http://127.0.0.1:1"),
                ReportDirectory: directory));

            runtime.Pause();

            Assert.NotNull(runtime.LastRunReportPath);
            Assert.Null(runtime.LastRunReportError);
            Assert.True(File.Exists(runtime.LastRunReportPath));
            Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
            var json = await File.ReadAllTextAsync(runtime.LastRunReportPath!);
            Assert.Contains("dnne.world-run.v1", json, StringComparison.Ordinal);
            Assert.Contains("paused", json, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}

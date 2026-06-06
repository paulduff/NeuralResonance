using System;
using System.Threading.Tasks;
using NRE.Core.Engine;
using Xunit;

namespace NRE.Tests;

public sealed class NeuralVolumeSignalRingTests
{
    [Fact]
    public void Signals_Arrive_On_Expected_Ticks()
    {
        var vol = new NeuralVolume(w:4, h:1, d:1, baseHz:1f, oscGain:0f, baseThreshold:1f, energyMax:1f, signalRingSize:8);

        // Clear baseline
        for (int i = 0; i < vol.Total; i++) vol.Vm[i] = 0f;

        // tick 0: enqueue delay 0 to fi 1, delay 2 to fi 2
        vol.EnqueueSignalByIndex(1, new NeuralVolume.DelayedSignal(0, 0.5f));
        vol.EnqueueSignalByIndex(2, new NeuralVolume.DelayedSignal(2, 1.0f));

        // tick 0 drain
        vol.DrainDueSignals();
        Assert.Equal(0.5f, vol.Vm[1], 4);
        Assert.Equal(0f, vol.Vm[2], 4);

        // tick 1 drain
        vol.DrainDueSignals();
        Assert.Equal(0f, vol.Vm[2], 4);

        // tick 2 drain
        vol.DrainDueSignals();
        Assert.Equal(1.0f, vol.Vm[2], 4);
    }

    [Fact]
    public void Inactive_Voxels_Do_Not_Receive_Signals()
    {
        var vol = new NeuralVolume(w:2, h:1, d:1, baseHz:1f, oscGain:0f, baseThreshold:1f, energyMax:1f, signalRingSize:4);
        vol.Active[1] = false;
        vol.EnqueueSignalByIndex(1, new NeuralVolume.DelayedSignal(0, 2f));
        vol.DrainDueSignals();
        Assert.Equal(0f, vol.Vm[1], 4);
    }

    [Fact]
    public async Task Concurrent_Enqueues_Do_Not_Throw_And_Are_Applied()
    {
        var vol = new NeuralVolume(w:64, h:1, d:1, baseHz:1f, oscGain:0f, baseThreshold:1f, energyMax:1f, signalRingSize:8);
        for (int i = 0; i < vol.Total; i++) vol.Vm[i] = 0f;

        int writers = 8;
        int perWriter = 2000;

        var tasks = new Task[writers];
        for (int w = 0; w < writers; w++)
        {
            tasks[w] = Task.Run(() =>
            {
                // Delay 0: target the due bucket to stress locking
                for (int i = 0; i < perWriter; i++)
                {
                    int fi = i % vol.Total;
                    vol.EnqueueSignalByIndex(fi, new NeuralVolume.DelayedSignal(0, 0.001f));
                }
            });
        }

        await Task.WhenAll(tasks);

        vol.DrainDueSignals();

        // Total applied energy should match writers * perWriter * 0.001
        float sum = 0f;
        for (int i = 0; i < vol.Total; i++) sum += vol.Vm[i];

        Assert.InRange(sum, (writers * perWriter * 0.001f) * 0.99f, (writers * perWriter * 0.001f) * 1.01f);
    }
}

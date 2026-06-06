using NRE.Blazor.Services;
using NRE.Blazor.Shared.OperatorConsole;
using NRE.Contracts.Voice;
using Xunit;

namespace NRE.Tests;

public sealed class ConsoleRefreshCoordinatorTests
{
    [Fact]
    public async Task RunVoiceLoopAsync_Speaks_Reafferents_And_Forwards_In_Order()
    {
        using var cts = new CancellationTokenSource();
        var api = new FakeApiClient
        {
            VoiceMessagesFactory = _ => Task.FromResult<VoiceUtteranceDto[]?>(new[]
            {
                new VoiceUtteranceDto(1, "hello world", 1.1f, 0.9f, 0.8f)
            })
        };
        var renderer = new FakeRenderer();
        var sut = new ConsoleRefreshCoordinator(api, renderer) { VoiceLoopDelayMs = 1 };
        var events = new List<string>();

        var ex = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            sut.RunVoiceLoopAsync(msg =>
            {
                events.Add($"callback:{msg.Text}");
                cts.Cancel();
                return Task.CompletedTask;
            }, cts.Token));

        Assert.NotNull(ex);
        Assert.Equal(new[]
        {
            "speak:hello world",
            "reafferent:hello world",
            "callback:hello world"
        }, renderer.Events.Concat(api.Events).Concat(events).ToArray());
        Assert.Single(api.ReafferentRequests);
        Assert.Equal("hello world", api.ReafferentRequests[0].Text);
    }

    [Fact]
    public async Task RunFrameLoopAsync_Parses_Frame_And_Forwards_It()
    {
        using var cts = new CancellationTokenSource();
        var api = new FakeApiClient
        {
            FastFrameBytesFactory = _ => Task.FromResult(FastFrameBytes.Build(7, 0.4f, 2, true, 1, new byte[] { 1, 2 }, 1, new byte[] { 3, 4 }, new[] { 5f }))
        };
        var sut = new ConsoleRefreshCoordinator(api, new FakeRenderer()) { FrameLoopDelayMs = 1 };
        RenderFrameFastDto? seen = null;

        var ex = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            sut.RunFrameLoopAsync(frame =>
            {
                seen = frame;
                cts.Cancel();
                return Task.CompletedTask;
            }, cts.Token));

        Assert.NotNull(ex);
        Assert.NotNull(seen);
        Assert.Equal(7, seen!.StepIndex);
        Assert.Equal(0.4f, seen.CallosalTraffic01);
        Assert.Equal("N2", seen.SleepPhase);
        Assert.True(seen.ThalamicPulseActive);
    }

    [Fact]
    public async Task RunStatusLoopAsync_Ticks_Telemetry_Every_Twelfth_Monitor_Cycle()
    {
        using var cts = new CancellationTokenSource();
        var api = new FakeApiClient
        {
            StatusFactory = _ => Task.FromResult<EngineStatusDto?>(new EngineStatusDto(false, 123, 0.016f, 1, 2, 3, new NeuromodulatorDto(0.1f, 0.2f, 0.3f), new PonsDto(0.4f, 0.5f, 0.6f, 7f)))
        };
        var sut = new ConsoleRefreshCoordinator(api, new FakeRenderer()) { StatusLoopDelayMs = 1 };
        var statuses = 0;
        var invalidations = 0;
        var telemetryTicks = new List<int>();

        var ex = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            sut.RunStatusLoopAsync(
                status =>
                {
                    statuses++;
                    if (statuses >= 12) cts.Cancel();
                    return Task.CompletedTask;
                },
                tick =>
                {
                    telemetryTicks.Add(tick);
                    return Task.CompletedTask;
                },
                () =>
                {
                    invalidations++;
                    return Task.CompletedTask;
                },
                () => "Monitor",
                cts.Token));

        Assert.NotNull(ex);
        Assert.Equal(12, statuses);
        Assert.Equal(12, invalidations);
        Assert.Single(telemetryTicks);
        Assert.Equal(0, telemetryTicks[0]);
    }

    [Fact]
    public async Task RunStatusLoopAsync_Does_Not_Tick_Telemetry_Off_Monitor_Tab()
    {
        using var cts = new CancellationTokenSource();
        var api = new FakeApiClient
        {
            StatusFactory = _ => Task.FromResult<EngineStatusDto?>(new EngineStatusDto(true, 9, 0.016f, 1, 1, 1, new NeuromodulatorDto(0, 0, 0), new PonsDto(0, 0, 0, 0)))
        };
        var sut = new ConsoleRefreshCoordinator(api, new FakeRenderer()) { StatusLoopDelayMs = 1 };
        var statuses = 0;
        var telemetryTicks = 0;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            sut.RunStatusLoopAsync(
                _ =>
                {
                    statuses++;
                    if (statuses >= 12) cts.Cancel();
                    return Task.CompletedTask;
                },
                _ =>
                {
                    telemetryTicks++;
                    return Task.CompletedTask;
                },
                () => Task.CompletedTask,
                () => "Stimulus",
                cts.Token));

        Assert.Equal(0, telemetryTicks);
    }

    private sealed class FakeApiClient : IEngineApiClient
    {
        public Func<CancellationToken, Task<EngineStatusDto?>>? StatusFactory { get; set; }
        public Func<CancellationToken, Task<byte[]>>? FastFrameBytesFactory { get; set; }
        public Func<CancellationToken, Task<VoiceUtteranceDto[]?>>? VoiceMessagesFactory { get; set; }
        public List<VoiceReafferenceRequest> ReafferentRequests { get; } = new();
        public List<string> Events { get; } = new();

        public Task<EngineStatusDto?> GetStatusAsync(CancellationToken ct = default)
            => StatusFactory?.Invoke(ct) ?? Task.FromResult<EngineStatusDto?>(null);

        public Task<byte[]> GetFastFrameBinaryAsync(CancellationToken ct = default)
            => FastFrameBytesFactory?.Invoke(ct) ?? Task.FromResult(Array.Empty<byte>());

        public Task<VoiceUtteranceDto[]?> GetVoiceMessagesAsync(int max = 6, CancellationToken ct = default)
            => VoiceMessagesFactory?.Invoke(ct) ?? Task.FromResult<VoiceUtteranceDto[]?>(Array.Empty<VoiceUtteranceDto>());

        public Task PostVoiceReafferentAsync(VoiceReafferenceRequest request, CancellationToken ct = default)
        {
            ReafferentRequests.Add(request);
            Events.Add($"reafferent:{request.Text}");
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRenderer : IRendererInteropService
    {
        public List<string> Events { get; } = new();

        public ValueTask SpeakAsync(VoiceUtteranceDto utterance)
        {
            Events.Add($"speak:{utterance.Text}");
            return ValueTask.CompletedTask;
        }
    }
}

internal static class FastFrameBytes
{
    public static byte[] Build(long step, float callosalTraffic, byte sleepId, bool thalPulse, int spikesCount, byte[] spikesData, int trafficCount, byte[] trafficData, float[]? body)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        bw.Write(step);
        bw.Write(callosalTraffic);
        bw.Write(sleepId);
        bw.Write((byte)(thalPulse ? 1 : 0));
        bw.Write(spikesCount);
        bw.Write(spikesData.Length);
        bw.Write(spikesData);
        bw.Write(trafficCount);
        bw.Write(trafficData.Length);
        bw.Write(trafficData);
        bw.Write(body?.Length ?? 0);
        if (body is not null)
        {
            foreach (var value in body)
                bw.Write(value);
        }

        bw.Flush();
        return ms.ToArray();
    }
}

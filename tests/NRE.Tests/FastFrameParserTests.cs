using System.Buffers.Binary;
using NRE.Blazor.Services;
using Xunit;

namespace NRE.Tests;

public sealed class FastFrameParserTests
{
    [Fact]
    public void Parse_Returns_Frame_For_Valid_Payload()
    {
        var bytes = BuildFrameBytes(
            step: 42,
            callosalTraffic: 0.75f,
            sleepId: 4,
            thalPulse: true,
            spikesCount: 3,
            spikesData: new byte[] { 1, 2, 3, 4, 5, 6 },
            trafficCount: 2,
            trafficData: new byte[] { 7, 8, 9, 10 },
            body: new[] { 1.5f, 2.5f, 3.5f });

        var frame = FastFrameParser.Parse(bytes);

        Assert.NotNull(frame);
        Assert.Equal(42, frame!.StepIndex);
        Assert.Equal(0.75f, frame.CallosalTraffic01);
        Assert.Equal("REM", frame.SleepPhase);
        Assert.True(frame.ThalamicPulseActive);
        Assert.Equal(3, frame.Spikes.Count);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6 }, frame.Spikes.Data);
        Assert.Equal(2, frame.CrossModuleTraffic.Count);
        Assert.Equal(new byte[] { 7, 8, 9, 10 }, frame.CrossModuleTraffic.Data);
        Assert.Equal(new[] { 1.5f, 2.5f, 3.5f }, frame.Body);
    }

    [Fact]
    public void Parse_Returns_Null_For_Truncated_Body()
    {
        var bytes = BuildFrameBytes(
            step: 1,
            callosalTraffic: 0.1f,
            sleepId: 0,
            thalPulse: false,
            spikesCount: 1,
            spikesData: new byte[] { 1, 2 },
            trafficCount: 1,
            trafficData: new byte[] { 3, 4 },
            body: new[] { 1f, 2f });

        Array.Resize(ref bytes, bytes.Length - 2);

        Assert.Null(FastFrameParser.Parse(bytes));
    }

    [Fact]
    public void Parse_Returns_Null_For_Invalid_Spike_Length()
    {
        var bytes = BuildFrameBytes(
            step: 1,
            callosalTraffic: 0.2f,
            sleepId: 2,
            thalPulse: false,
            spikesCount: 1,
            spikesData: new byte[] { 1, 2 },
            trafficCount: 0,
            trafficData: Array.Empty<byte>(),
            body: null);

        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(18, 4), 99_999);

        Assert.Null(FastFrameParser.Parse(bytes));
    }

    private static byte[] BuildFrameBytes(long step, float callosalTraffic, byte sleepId, bool thalPulse, int spikesCount, byte[] spikesData, int trafficCount, byte[] trafficData, float[]? body)
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

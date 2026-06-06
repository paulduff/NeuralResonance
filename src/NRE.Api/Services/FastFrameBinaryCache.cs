using System.Buffers.Binary;
using NRE.Core.Engine;

namespace NRE.Api.Services;

/// <summary>
/// Caches a compact binary representation of the most recent FAST frame.
/// This avoids JSON serialize/deserialize costs and reduces payload size for the renderer hot-path.
/// </summary>
public sealed class FastFrameBinaryCache
{
    private readonly NreEngine _engine;

    private readonly object _gate = new();
    private long _lastStep = -1;
    private byte[] _lastBytes = Array.Empty<byte>();

    public FastFrameBinaryCache(NreEngine engine)
    {
        _engine = engine;
    }

    // Format (little endian):
    //  i64 step
    //  f32 callosalTraffic01
    //  u8  sleepPhaseId (0 Awake, 1 N1, 2 N2, 3 N3, 4 REM)
    //  u8  thalamicPulse (0/1)
    //  i32 spikesCount
    //  i32 spikesBytesLen
    //  bytes spikes
    //  i32 trafficCount
    //  i32 trafficBytesLen
    //  bytes traffic
    //  i32 bodyLen (0 if none)
    //  f32[bodyLen] body
    public byte[] GetFastFrameBytes()
    {
        var snap = _engine.GetPublishedFastFrame();
        if (snap.StepIndex == _lastStep)
            return _lastBytes;

        lock (_gate)
        {
            snap = _engine.GetPublishedFastFrame();
            if (snap.StepIndex == _lastStep)
                return _lastBytes;
            var spikes = snap.Spikes;
            var traffic = snap.CrossModuleTraffic;
            var spikesData = spikes.Data ?? Array.Empty<byte>();
            var trafficData = traffic.Data ?? Array.Empty<byte>();
            var body = snap.Body;

            int bodyLen = body?.Length ?? 0;

            int size =
                8 + 4 + 1 + 1 +
                4 + 4 + spikesData.Length +
                4 + 4 + trafficData.Length +
                4 + (bodyLen * 4);

            var buf = new byte[size];
            var span = buf.AsSpan();
            int o = 0;

            BinaryPrimitives.WriteInt64LittleEndian(span.Slice(o, 8), snap.StepIndex); o += 8;
            BinaryPrimitives.WriteSingleLittleEndian(span.Slice(o, 4), snap.CallosalTraffic01); o += 4;

            span[o++] = SleepPhaseToId(snap.SleepPhase);
            span[o++] = (byte)((snap.ThalamicPulseActive ?? false) ? 1 : 0);

            BinaryPrimitives.WriteInt32LittleEndian(span.Slice(o, 4), spikes.Count); o += 4;
            BinaryPrimitives.WriteInt32LittleEndian(span.Slice(o, 4), spikesData.Length); o += 4;
            spikesData.AsSpan().CopyTo(span.Slice(o, spikesData.Length)); o += spikesData.Length;

            BinaryPrimitives.WriteInt32LittleEndian(span.Slice(o, 4), traffic.Count); o += 4;
            BinaryPrimitives.WriteInt32LittleEndian(span.Slice(o, 4), trafficData.Length); o += 4;
            trafficData.AsSpan().CopyTo(span.Slice(o, trafficData.Length)); o += trafficData.Length;

            BinaryPrimitives.WriteInt32LittleEndian(span.Slice(o, 4), bodyLen); o += 4;
            if (bodyLen > 0 && body is not null)
            {
                // Write floats directly
                for (int i = 0; i < bodyLen; i++)
                {
                    BinaryPrimitives.WriteSingleLittleEndian(span.Slice(o, 4), body[i]);
                    o += 4;
                }
            }

            _lastBytes = buf;
            _lastStep = snap.StepIndex;
            return _lastBytes;
        }
    }

    private static byte SleepPhaseToId(string? phase)
    {
        if (string.IsNullOrWhiteSpace(phase)) return 0;
        return phase switch
        {
            "Awake" => 0,
            "N1" => 1,
            "N2" => 2,
            "N3" => 3,
            "REM" => 4,
            _ => 0
        };
    }
}

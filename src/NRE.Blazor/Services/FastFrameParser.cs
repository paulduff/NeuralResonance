using System.Buffers.Binary;
using NRE.Blazor.Shared.OperatorConsole;

namespace NRE.Blazor.Services;

public static class FastFrameParser
{
    public static RenderFrameFastDto? Parse(byte[] bytes)
    {
        if (bytes is null || bytes.Length < (8 + 4 + 1 + 1 + 4 + 4 + 4 + 4 + 4))
            return null;

        var span = bytes.AsSpan();
        var offset = 0;

        var step = BinaryPrimitives.ReadInt64LittleEndian(span.Slice(offset, 8)); offset += 8;
        var callosal = BinaryPrimitives.ReadSingleLittleEndian(span.Slice(offset, 4)); offset += 4;
        var sleepId = span[offset++];
        var thalPulse = span[offset++] != 0;

        var spikesCount = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(offset, 4)); offset += 4;
        var spikesLen = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(offset, 4)); offset += 4;
        if (spikesLen < 0 || offset + spikesLen > span.Length) return null;
        var spikesData = span.Slice(offset, spikesLen).ToArray(); offset += spikesLen;

        var trafficCount = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(offset, 4)); offset += 4;
        var trafficLen = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(offset, 4)); offset += 4;
        if (trafficLen < 0 || offset + trafficLen > span.Length) return null;
        var trafficData = span.Slice(offset, trafficLen).ToArray(); offset += trafficLen;

        var bodyLen = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(offset, 4)); offset += 4;
        float[]? body = null;
        if (bodyLen > 0)
        {
            var bytesNeeded = bodyLen * 4;
            if (offset + bytesNeeded > span.Length) return null;
            body = new float[bodyLen];
            for (var i = 0; i < bodyLen; i++)
            {
                body[i] = BinaryPrimitives.ReadSingleLittleEndian(span.Slice(offset, 4));
                offset += 4;
            }
        }

        var sleepPhase = sleepId switch
        {
            0 => "Awake",
            1 => "N1",
            2 => "N2",
            3 => "N3",
            4 => "REM",
            _ => "Awake"
        };

        return new RenderFrameFastDto(
            step,
            new PackedPoints(spikesCount, spikesData),
            new PackedTrafficEvents(trafficCount, trafficData),
            callosal,
            sleepPhase,
            thalPulse,
            body);
    }
}

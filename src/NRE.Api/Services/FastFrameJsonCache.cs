using System.Text.Json;
using NRE.Api.Serialization;
using NRE.Core.Engine;

namespace NRE.Api.Services;

/// <summary>
/// Caches the serialized JSON for the most recent FAST frame.
///
/// Why: UI polling can be higher frequency than FAST publishing. If we serialize on every request,
/// we waste CPU and allocate heavily. Instead, serialize once per publish and serve the same bytes
/// until the engine publishes a new frame.
///
/// Safety: we always allocate a new byte[] when the frame changes. We never mutate published buffers.
/// Outstanding HTTP responses can safely continue using older arrays.
/// </summary>
public sealed class FastFrameJsonCache
{
    private readonly NreEngine _engine;

    private readonly object _gate = new();
    private long _lastStep = -1;
    private byte[] _lastJson = Array.Empty<byte>();

    public FastFrameJsonCache(NreEngine engine)
    {
        _engine = engine;
    }

    public byte[] GetFastFrameJson()
    {
        // Fast path: if the step hasn't changed, serve cached bytes.
        var snap = _engine.GetPublishedFastFrame();
        if (snap.StepIndex == _lastStep)
            return _lastJson;

        lock (_gate)
        {
            snap = _engine.GetPublishedFastFrame();
            if (snap.StepIndex == _lastStep)
                return _lastJson;

            // Serialize once per published frame using source-generated metadata.
            _lastJson = JsonSerializer.SerializeToUtf8Bytes(snap, NreJsonContext.Default.RenderFrameFastDto);
            _lastStep = snap.StepIndex;
            return _lastJson;
        }
    }
}

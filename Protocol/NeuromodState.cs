using ProtoBuf;

namespace NeuralResonanceEngine.Protocol;

[ProtoContract]
public sealed class NeuromodState
{
    [ProtoMember(1)] public float DopamineLevel { get; set; }
    [ProtoMember(2)] public float SerotoninLevel { get; set; }
    [ProtoMember(3)] public float AcetylcholineLevel { get; set; }
    [ProtoMember(4)] public float NorepinephrineLevel { get; set; }

    public static NeuromodState Clamp(NeuromodState state)
    {
        return new NeuromodState
        {
            DopamineLevel = Math.Clamp(state.DopamineLevel, 0f, 1f),
            SerotoninLevel = Math.Clamp(state.SerotoninLevel, 0f, 1f),
            AcetylcholineLevel = Math.Clamp(state.AcetylcholineLevel, 0f, 1f),
            NorepinephrineLevel = Math.Clamp(state.NorepinephrineLevel, 0f, 1f)
        };
    }

    /// <summary>
    /// In-place clamp for hot paths (e.g. per-spike validation) that want to keep the
    /// existing instance instead of allocating a fresh one. Other callers should keep
    /// using <see cref="Clamp"/> for non-aliasing semantics.
    /// </summary>
    public static void ClampInPlace(NeuromodState state)
    {
        state.DopamineLevel = Math.Clamp(state.DopamineLevel, 0f, 1f);
        state.SerotoninLevel = Math.Clamp(state.SerotoninLevel, 0f, 1f);
        state.AcetylcholineLevel = Math.Clamp(state.AcetylcholineLevel, 0f, 1f);
        state.NorepinephrineLevel = Math.Clamp(state.NorepinephrineLevel, 0f, 1f);
    }
}

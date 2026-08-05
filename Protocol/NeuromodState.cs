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
            DopamineLevel = ClampFiniteUnit(state.DopamineLevel),
            SerotoninLevel = ClampFiniteUnit(state.SerotoninLevel),
            AcetylcholineLevel = ClampFiniteUnit(state.AcetylcholineLevel),
            NorepinephrineLevel = ClampFiniteUnit(state.NorepinephrineLevel)
        };
    }

    /// <summary>
    /// In-place clamp for hot paths (e.g. per-spike validation) that want to keep the
    /// existing instance instead of allocating a fresh one. Other callers should keep
    /// using <see cref="Clamp"/> for non-aliasing semantics.
    /// </summary>
    public static void ClampInPlace(NeuromodState state)
    {
        state.DopamineLevel = ClampFiniteUnit(state.DopamineLevel);
        state.SerotoninLevel = ClampFiniteUnit(state.SerotoninLevel);
        state.AcetylcholineLevel = ClampFiniteUnit(state.AcetylcholineLevel);
        state.NorepinephrineLevel = ClampFiniteUnit(state.NorepinephrineLevel);
    }

    private static float ClampFiniteUnit(float value) =>
        float.IsFinite(value) ? Math.Clamp(value, 0f, 1f) : 0f;
}

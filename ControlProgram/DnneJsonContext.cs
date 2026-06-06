using System.Text.Json;
using System.Text.Json.Serialization;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(SpikeMessage))]
[JsonSerializable(typeof(List<SpikeMessage>))]
[JsonSerializable(typeof(FramePayload))]
[JsonSerializable(typeof(BrainSnapshot))]
internal partial class DnneJsonContext : JsonSerializerContext
{
}

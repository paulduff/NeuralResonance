using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using NeuralResonanceEngine.Protocol;

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(SpikeMessage))]
[JsonSerializable(typeof(List<SpikeMessage>))]
internal partial class StructureJsonContext : JsonSerializerContext
{
}

using System.Text.Json.Serialization;
using NRE.Core.Engine;
using NRE.Contracts.Voice;

namespace NRE.Api.Serialization;

/// <summary>
/// System.Text.Json source-generation for the hottest API payloads.
/// This avoids reflection and cuts allocations during high-frequency /framefast polling.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = false,
    GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(RenderFrameFastDto))]
[JsonSerializable(typeof(PackedPoints))]
[JsonSerializable(typeof(PackedTrafficEvents))]
[JsonSerializable(typeof(RenderHeatmapsDto))]
[JsonSerializable(typeof(PackedHeatmap))]
[JsonSerializable(typeof(PackedLines))]
[JsonSerializable(typeof(EngineStatusDto))]
[JsonSerializable(typeof(VoiceUtteranceDto[]))]
public sealed partial class NreJsonContext : JsonSerializerContext
{
}

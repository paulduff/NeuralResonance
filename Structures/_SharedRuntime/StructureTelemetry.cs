using System.Diagnostics;

/// <summary>
/// Shared <see cref="ActivitySource"/> for structure-side spike processing. Subscribe
/// from a parent process or test using <c>ActivityListener</c> or OpenTelemetry SDKs
/// to capture spans without per-service wiring.
/// </summary>
internal static class StructureTelemetry
{
	public const string SourceName = "NeuralResonanceEngine.Structures";
	public static readonly ActivitySource Source = new(SourceName);
}

using System.Net;

namespace NRE.SimAvatar;

public static class AvatarControlStatusText
{
    public static string Connecting() => "Status: Connecting to Control Program...";

    public static string Reconnecting() => "Status: Reconnecting to Control Program...";

    public static string InvalidEndpoint() => "Status: Invalid endpoint URI";

    public static string FramePollFailed(HttpStatusCode statusCode) => $"Status: Frame poll failed ({(int)statusCode})";

    public static string ConnectedWithMotorEvents(long tick, int motorEvents) => $"Status: Connected | tick {tick} | motor events {motorEvents}";

    public static string ConnectedWithPathways(long tick, long pathways) => $"Status: Connected | tick {tick} | pathways {pathways}";

    public static string PollError(string exceptionType) => $"Status: poll error ({exceptionType})";

    public static string TelemetryDelayed(string reason, double staleSeconds) => $"Status: Telemetry delayed ({reason}, {staleSeconds:0.0}s stale)";

    public static string TelemetryIssue(string reason) => $"Status: Telemetry connection issue ({reason})";

    public static string EndpointFallback(string endpoint) => $"Endpoint selection invalid, using last valid endpoint: {endpoint}";
}

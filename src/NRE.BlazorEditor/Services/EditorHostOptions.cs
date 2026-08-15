using NeuralResonanceEngine.Shared.Contracts;

namespace NRE.BlazorEditor.Services;

public sealed class EditorHostOptions
{
    private EditorHostOptions(
        Uri controlProgramBaseUri,
        string? accessKey,
        int port,
        bool listenAnyIp,
        bool trustForwardedHeaders)
    {
        ControlProgramBaseUri = controlProgramBaseUri;
        AccessKey = accessKey;
        Port = port;
        ListenAnyIp = listenAnyIp;
        TrustForwardedHeaders = trustForwardedHeaders;
    }

    public Uri ControlProgramBaseUri { get; }
    public int Port { get; }
    public bool ListenAnyIp { get; }
    public bool TrustForwardedHeaders { get; }
    public bool RequiresAuthentication => AccessKey is not null;
    internal string? ControlSharedSecret => NreControlPlaneSecurity.ResolveSharedSecret();

    private string? AccessKey { get; }

    public bool IsValidAccessKey(string? suppliedKey) =>
        AccessKey is not null && NreControlPlaneSecurity.IsAuthorized(suppliedKey, AccessKey);

    public static EditorHostOptions FromConfiguration(IConfiguration configuration)
    {
        var configuredControlUrl =
            Environment.GetEnvironmentVariable("NRE_EDITOR_CONTROL_BASE_URL") ??
            configuration["Editor:ControlProgramBaseUrl"] ??
            "http://127.0.0.1:5080";
        if (!Uri.TryCreate(configuredControlUrl, UriKind.Absolute, out var controlUri) ||
            !controlUri.IsLoopback)
        {
            throw new InvalidOperationException(
                "The Blazor Editor only accepts a loopback ControlProgram endpoint. " +
                "Run it on the Control machine and use localhost or 127.0.0.1.");
        }

        var listenAnyIp = ReadBoolean(
            Environment.GetEnvironmentVariable("NRE_EDITOR_LISTEN_ANY_IP") ??
            configuration["Editor:ListenAnyIp"]);
        var accessKey = NormalizeSecret(
            Environment.GetEnvironmentVariable("NRE_EDITOR_ACCESS_KEY") ??
            configuration["Editor:AccessKey"]);
        if (listenAnyIp && accessKey is null)
        {
            throw new InvalidOperationException(
                "NRE_EDITOR_LISTEN_ANY_IP=true requires NRE_EDITOR_ACCESS_KEY. " +
                "The Editor will not expose an unauthenticated control surface.");
        }

        var configuredPort =
            Environment.GetEnvironmentVariable("NRE_EDITOR_PORT") ??
            configuration["Editor:Port"];
        var port = int.TryParse(configuredPort, out var parsedPort)
            ? Math.Clamp(parsedPort, 1024, 65535)
            : 5090;

        var trustForwardedHeaders = ReadBoolean(
            Environment.GetEnvironmentVariable("NRE_EDITOR_TRUST_FORWARDED_HEADERS") ??
            configuration["Editor:TrustForwardedHeaders"]);
        return new EditorHostOptions(
            EnsureTrailingSlash(controlUri),
            accessKey,
            port,
            listenAnyIp,
            trustForwardedHeaders);
    }

    private static Uri EnsureTrailingSlash(Uri uri) =>
        uri.AbsoluteUri.EndsWith("/", StringComparison.Ordinal)
            ? uri
            : new Uri(uri.AbsoluteUri + "/", UriKind.Absolute);

    private static bool ReadBoolean(string? value) =>
        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || value == "1";

    private static string? NormalizeSecret(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

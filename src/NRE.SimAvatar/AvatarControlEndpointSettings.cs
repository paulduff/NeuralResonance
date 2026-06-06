namespace NRE.SimAvatar;

public static class AvatarControlEndpointSettings
{
    public const string DefaultEndpoint = "http://localhost:5080";

    public static string ResolveConfiguredEndpoint(string fallback = DefaultEndpoint)
    {
        var configured = Environment.GetEnvironmentVariable("NRE_CONTROL_ENDPOINTS")
            ?? Environment.GetEnvironmentVariable("CONTROLPROGRAM_BASE_URLS")
            ?? Environment.GetEnvironmentVariable("CONTROLPROGRAM_BASE_URL");
        if (string.IsNullOrWhiteSpace(configured))
        {
            return fallback;
        }

        foreach (var token in configured.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries))
        {
            if (AvatarEndpointResolver.TryNormalizeEndpoint(token, out var normalized))
            {
                return normalized;
            }
        }

        return fallback;
    }
}

namespace NRE.SimAvatar;

public static class AvatarEndpointResolver
{
    public static bool TryNormalizeEndpoint(string? endpoint, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return false;
        }

        if (!Uri.TryCreate(endpoint.Trim(), UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (!uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        normalized = uri.GetLeftPart(UriPartial.Authority);
        return true;
    }

    public static Uri? ResolveUri(string? endpoint)
    {
        return TryNormalizeEndpoint(endpoint, out var normalized)
            ? new Uri(normalized)
            : null;
    }
}

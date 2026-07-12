using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

namespace NeuralResonanceEngine.Shared.Contracts;

public static class NreControlPlaneSecurity
{
    public const string SharedSecretEnvironmentVariable = "NRE_CONTROL_SHARED_SECRET";
    public const string HeaderName = "X-NRE-Control-Auth";

    public static string? ResolveSharedSecret()
    {
        var value = Environment.GetEnvironmentVariable(SharedSecretEnvironmentVariable);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public static void ApplyClientAuthentication(HttpClient client)
    {
        ArgumentNullException.ThrowIfNull(client);

        var secret = ResolveSharedSecret();
        if (secret is null)
        {
            return;
        }

        client.DefaultRequestHeaders.Remove(HeaderName);
        client.DefaultRequestHeaders.TryAddWithoutValidation(HeaderName, secret);
    }

    public static void ApplyRequestAuthentication(HttpRequestMessage request, string? secret)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(secret))
        {
            return;
        }

        request.Headers.Remove(HeaderName);
        request.Headers.TryAddWithoutValidation(HeaderName, secret);
    }

    public static bool IsAuthorized(string? suppliedSecret, string configuredSecret)
    {
        if (string.IsNullOrEmpty(suppliedSecret))
        {
            return false;
        }

        var expected = Encoding.UTF8.GetBytes(configuredSecret);
        var supplied = Encoding.UTF8.GetBytes(suppliedSecret);
        return expected.Length == supplied.Length && CryptographicOperations.FixedTimeEquals(expected, supplied);
    }
}

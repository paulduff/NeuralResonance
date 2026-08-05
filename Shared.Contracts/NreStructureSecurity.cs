using System.Security.Cryptography;
using System.Text;

namespace NeuralResonanceEngine.Shared.Contracts;

public static class NreStructureSecurity
{
    public const string SharedSecretEnvironmentVariable = "NRE_STRUCTURE_SHARED_SECRET";
    public const string ListenAnyIpEnvironmentVariable = "NRE_STRUCTURE_LISTEN_ANY_IP";
    public const string HeaderName = "X-NRE-Auth";

    public static string? ResolveSharedSecret()
    {
        var value = Environment.GetEnvironmentVariable(SharedSecretEnvironmentVariable);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public static bool ResolveListenAnyIp() =>
        bool.TryParse(Environment.GetEnvironmentVariable(ListenAnyIpEnvironmentVariable), out var enabled) && enabled;

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

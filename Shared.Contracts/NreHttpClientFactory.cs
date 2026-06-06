using System.Net;
using System.Net.Http;

namespace NeuralResonanceEngine.Shared.Contracts;

/// <summary>
/// Factory for HttpClient instances configured for talking to the NRE API.
/// Centralises the SocketsHttpHandler tuning that was previously duplicated
/// across the WPF Editor, MazeSim and WorldSim with subtly different settings.
/// </summary>
public static class NreHttpClientFactory
{
    /// <summary>
    /// Default profile: snappy connect timeout, infinite request timeout, no proxy,
    /// long pooled-connection lifetimes. Suitable for the long-lived UI clients that
    /// poll the engine for telemetry and dispatch input deltas.
    /// </summary>
    public static HttpClient CreateDefault() => Create(NreHttpClientOptions.Default);

    /// <summary>
    /// Short-lived probe profile: short connect timeout, finite request timeout.
    /// Suitable for splash-screen / liveness probes.
    /// </summary>
    public static HttpClient CreateProbe() => Create(NreHttpClientOptions.Probe);

    /// <summary>
    /// Build a client with explicit options.
    /// The returned <see cref="HttpClient"/> owns the underlying handler and should be
    /// disposed when the consumer is shutting down.
    /// </summary>
    public static HttpClient Create(NreHttpClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var handler = new SocketsHttpHandler
        {
            UseProxy = options.UseProxy,
            ConnectTimeout = options.ConnectTimeout,
            PooledConnectionIdleTimeout = options.PooledConnectionIdleTimeout,
            PooledConnectionLifetime = options.PooledConnectionLifetime,
            // Negotiate response compression (brotli/gzip) with the server. The control
            // program enables Brotli + Gzip middleware for JSON/NDJSON responses; this
            // halves the bytes on the wire for the frame stream and large state payloads.
            AutomaticDecompression = DecompressionMethods.Brotli | DecompressionMethods.GZip
        };

        return new HttpClient(handler, disposeHandler: true)
        {
            Timeout = options.RequestTimeout
        };
    }
}

/// <summary>Tuning knobs for <see cref="NreHttpClientFactory"/>.</summary>
public sealed record NreHttpClientOptions(
    TimeSpan ConnectTimeout,
    TimeSpan RequestTimeout,
    TimeSpan PooledConnectionIdleTimeout,
    TimeSpan PooledConnectionLifetime,
    bool UseProxy = false)
{
    public static NreHttpClientOptions Default { get; } = new(
        ConnectTimeout: TimeSpan.FromMilliseconds(1200),
        RequestTimeout: Timeout.InfiniteTimeSpan,
        PooledConnectionIdleTimeout: TimeSpan.FromMinutes(2),
        PooledConnectionLifetime: TimeSpan.FromMinutes(10));

    public static NreHttpClientOptions Probe { get; } = new(
        ConnectTimeout: TimeSpan.FromMilliseconds(900),
        RequestTimeout: TimeSpan.FromSeconds(2),
        PooledConnectionIdleTimeout: TimeSpan.FromMinutes(1),
        PooledConnectionLifetime: TimeSpan.FromMinutes(5));
}

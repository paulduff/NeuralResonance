using NeuralResonanceEngine.Shared.Contracts;

namespace NRE.BlazorEditor.Services;

public sealed class ControlTelemetryClient(
    HttpClient client,
    EditorHostOptions options,
    ILogger<ControlTelemetryClient> logger)
{
    public async Task CopyGetAsync(HttpContext context, string relativePath)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, relativePath);
            NreControlPlaneSecurity.ApplyRequestAuthentication(request, options.ControlSharedSecret);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
            timeout.CancelAfter(TimeSpan.FromSeconds(12));
            using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeout.Token);

            context.Response.StatusCode = (int)response.StatusCode;
            context.Response.Headers.CacheControl = "no-store, max-age=0";
            if (response.Content.Headers.ContentType is { } contentType)
            {
                context.Response.ContentType = contentType.ToString();
            }

            await response.Content.CopyToAsync(context.Response.Body, timeout.Token);
        }
        catch (OperationCanceledException) when (!context.RequestAborted.IsCancellationRequested)
        {
            context.Response.StatusCode = StatusCodes.Status504GatewayTimeout;
            await context.Response.WriteAsJsonAsync(
                new { error = "ControlProgram did not respond before the editor telemetry timeout." },
                context.RequestAborted);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "ControlProgram telemetry request {Path} failed", relativePath);
            context.Response.StatusCode = StatusCodes.Status502BadGateway;
            await context.Response.WriteAsJsonAsync(
                new { error = "ControlProgram is unavailable on the configured loopback endpoint." },
                context.RequestAborted);
        }
    }
}

using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using NeuralResonanceEngine.Shared.Contracts;

namespace NRE.WpfEditor;

public partial class App : Application
{
    private static readonly string CrashLogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NRE.WpfEditor",
        "crash.log");
    private static readonly Uri[] ControlBaseUris = BuildControlBaseUris();
    private static readonly HttpClient SplashStatusClient = NreHttpClientFactory.Create(
        NreHttpClientOptions.Default with
        {
            ConnectTimeout = TimeSpan.FromMilliseconds(900),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(1),
            PooledConnectionLifetime = TimeSpan.FromMinutes(5)
        });

    private async void Application_OnStartup(object sender, StartupEventArgs e)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(CrashLogPath)!);

        DispatcherUnhandledException += (_, args) =>
        {
            WriteCrashLog("DispatcherUnhandledException", args.Exception);
            MessageBox.Show($"Unexpected error.\n\n{args.Exception.Message}\n\nLog: {CrashLogPath}", "NRE.WpfEditor", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
            Shutdown(-1);
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                WriteCrashLog("AppDomainUnhandledException", ex);
            }
        };

        StartupSplashWindow? splash = null;
        CancellationTokenSource? startupCts = null;
        Task? splashLoopTask = null;
        try
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            splash = new StartupSplashWindow();
            splash.Show();
            splash.UpdateStatus(
                "Starting DNNE Editor",
                "Bootstrapping UI and probing Control Program startup state...");

            startupCts = new CancellationTokenSource();
            splashLoopTask = RunSplashStatusLoopAsync(splash, startupCts.Token);
            await Dispatcher.Yield(DispatcherPriority.Background);

            var window = new MainWindow();
            var renderedTcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            window.ContentRendered += (_, _) => renderedTcs.TrySetResult(null);
            window.Show();
            await renderedTcs.Task.WaitAsync(TimeSpan.FromSeconds(20));

            startupCts.Cancel();
            await AwaitSplashLoopQuietlyAsync(splashLoopTask);
            if (splash.IsVisible)
            {
                splash.Close();
            }

            MainWindow = window;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
        }
        catch (Exception ex)
        {
            if (startupCts is not null)
            {
                startupCts.Cancel();
            }

            if (splashLoopTask is not null)
            {
                await AwaitSplashLoopQuietlyAsync(splashLoopTask);
            }

            if (splash is not null && splash.IsVisible)
            {
                splash.Close();
            }

            WriteCrashLog("StartupException", ex);
            MessageBox.Show($"Startup failed.\n\n{ex.Message}\n\nLog: {CrashLogPath}", "NRE.WpfEditor", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(-1);
        }
        finally
        {
            startupCts?.Dispose();
        }
    }

    private static async Task AwaitSplashLoopQuietlyAsync(Task splashLoopTask)
    {
        try
        {
            await splashLoopTask;
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown path.
        }
        catch
        {
            // Splash telemetry is best effort only.
        }
    }

    private static async Task RunSplashStatusLoopAsync(StartupSplashWindow splash, CancellationToken token)
    {
        var lastSummary = string.Empty;
        var lastDetail = string.Empty;

        while (!token.IsCancellationRequested)
        {
            var status = await ProbeStartupStatusAsync(token);
            if (status is null)
            {
                await Task.Delay(450, token);
                continue;
            }

            if (!string.Equals(lastSummary, status.Summary, StringComparison.Ordinal) ||
                !string.Equals(lastDetail, status.Detail, StringComparison.Ordinal))
            {
                lastSummary = status.Summary;
                lastDetail = status.Detail;
                await splash.Dispatcher.InvokeAsync(
                    () => splash.UpdateStatus(status.Summary, status.Detail),
                    DispatcherPriority.Background,
                    token);
            }

            await Task.Delay(status.Connected ? 320 : 520, token);
        }
    }

    private static async Task<SplashStartupStatus?> ProbeStartupStatusAsync(CancellationToken token)
    {
        string? lastIssue = null;
        foreach (var baseUri in ControlBaseUris)
        {
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                timeout.CancelAfter(TimeSpan.FromMilliseconds(1200));
                using var response = await SplashStatusClient.GetAsync(
                    new Uri(baseUri, "/api/v1/startup-health"),
                    HttpCompletionOption.ResponseHeadersRead,
                    timeout.Token);
                if (!response.IsSuccessStatusCode)
                {
                    using var stateResponse = await SplashStatusClient.GetAsync(
                        new Uri(baseUri, "/api/v1/state"),
                        HttpCompletionOption.ResponseHeadersRead,
                        timeout.Token);
                    if (!stateResponse.IsSuccessStatusCode)
                    {
                        lastIssue = $"Engine endpoint {baseUri} responded HTTP {(int)stateResponse.StatusCode}.";
                        continue;
                    }

                    await using var stateStream = await stateResponse.Content.ReadAsStreamAsync(timeout.Token);
                    using var stateDoc = await JsonDocument.ParseAsync(stateStream, cancellationToken: timeout.Token);
                    if (stateDoc.RootElement.ValueKind != JsonValueKind.Object)
                    {
                        lastIssue = $"Engine endpoint {baseUri} returned malformed state payload.";
                        continue;
                    }

                    var stateRoot = stateDoc.RootElement;
                    var tickState = GetLong(stateRoot, "tick");
                    var simMsState = GetDouble(stateRoot, "simulationClockMs");
                    var serviceCountState = GetInt(stateRoot, "serviceCount");
                    var nonOkState = CountNonOkServices(stateRoot);
                    var lastSnapshotTickState = GetLong(stateRoot, "lastSnapshotTick");
                    var endpointStateLabel = $"{baseUri.Scheme}://{baseUri.Host}:{baseUri.Port}";
                    var snapshotStateText = lastSnapshotTickState <= 0 ? "awaiting first snapshot" : $"last snapshot tick {lastSnapshotTickState}";
                    var detailState = $"Tick {tickState} @ {simMsState:0.0} ms | services {serviceCountState} | non-OK {nonOkState} | {snapshotStateText}";
                    return new SplashStartupStatus(true, $"Engine online ({endpointStateLabel})", detailState);
                }

                await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: timeout.Token);
                if (doc.RootElement.ValueKind != JsonValueKind.Object)
                {
                    lastIssue = $"Engine endpoint {baseUri} returned malformed state payload.";
                    continue;
                }

                var root = doc.RootElement;
                var tick = GetLong(root, "tick");
                var simMs = GetDouble(root, "simulationClockMs");
                var serviceCount = GetInt(root, "serviceCount");
                var nonOk = CountNonOkServices(root);
                var lastSnapshotTick = GetLong(root, "lastSnapshotTick");
                var endpointLabel = $"{baseUri.Scheme}://{baseUri.Host}:{baseUri.Port}";
                var snapshotText = lastSnapshotTick <= 0 ? "awaiting first snapshot" : $"last snapshot tick {lastSnapshotTick}";
                var detail = simMs > 0
                    ? $"Tick {tick} @ {simMs:0.0} ms | services {serviceCount} | non-OK {nonOk} | {snapshotText}"
                    : $"Tick {tick} | services {serviceCount} | non-OK {nonOk} | {snapshotText}";
                return new SplashStartupStatus(true, $"Engine online ({endpointLabel})", detail);
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
                lastIssue = $"Engine probe timeout at {baseUri}.";
            }
            catch (Exception ex)
            {
                lastIssue = $"Engine probe failed at {baseUri}: {ex.Message}";
            }
        }

        return new SplashStartupStatus(false, "Waiting for engine startup...", lastIssue ?? "Control Program endpoint unavailable.");
    }

    private static Uri[] BuildControlBaseUris()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var uris = new List<Uri>(10);

        void AddIfValid(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return;
            }

            if (!Uri.TryCreate(raw.Trim(), UriKind.Absolute, out var uri))
            {
                return;
            }

            if (!uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
                !uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var canonical = $"{uri.Scheme}://{uri.Host}:{uri.Port}";
            if (seen.Add(canonical))
            {
                uris.Add(new Uri(canonical));
            }
        }

        var configured = Environment.GetEnvironmentVariable("NRE_CONTROL_ENDPOINTS")
            ?? Environment.GetEnvironmentVariable("CONTROLPROGRAM_BASE_URLS")
            ?? Environment.GetEnvironmentVariable("CONTROLPROGRAM_BASE_URL");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            foreach (var token in configured.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries))
            {
                AddIfValid(token);
            }
        }

        AddIfValid("http://localhost:5080");
        AddIfValid("http://localhost:5000");
        AddIfValid("http://127.0.0.1:5080");
        AddIfValid("http://127.0.0.1:5000");
        AddIfValid("https://localhost:5081");
        AddIfValid("https://localhost:5001");
        AddIfValid("https://127.0.0.1:5081");
        AddIfValid("https://127.0.0.1:5001");

        return uris.ToArray();
    }

    private static int CountNonOkServices(JsonElement root)
    {
        var summarized = GetInt(root, "nonOkCount");
        if (summarized > 0)
        {
            return summarized;
        }

        if (!TryGetProperty(root, "serviceTelemetry", out var telemetry) || telemetry.ValueKind != JsonValueKind.Object)
        {
            return 0;
        }

        var nonOk = 0;
        foreach (var entry in telemetry.EnumerateObject())
        {
            if (!TryGetProperty(entry.Value, "lastStatus", out var statusProp) || statusProp.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var status = statusProp.GetString() ?? string.Empty;
            if (!string.Equals(status, "OK", StringComparison.OrdinalIgnoreCase))
            {
                nonOk++;
            }
        }

        return nonOk;
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            value = default;
            return false;
        }

        var target = Normalize(propertyName);
        foreach (var prop in element.EnumerateObject())
        {
            if (Normalize(prop.Name) == target)
            {
                value = prop.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static int GetInt(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var prop))
        {
            return 0;
        }

        if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var value))
        {
            return value;
        }

        if (prop.ValueKind == JsonValueKind.String && int.TryParse(prop.GetString(), out value))
        {
            return value;
        }

        return 0;
    }

    private static long GetLong(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var prop))
        {
            return 0;
        }

        if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt64(out var value))
        {
            return value;
        }

        if (prop.ValueKind == JsonValueKind.String && long.TryParse(prop.GetString(), out value))
        {
            return value;
        }

        return 0;
    }

    private static double GetDouble(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var prop))
        {
            return 0.0;
        }

        if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDouble(out var value))
        {
            return value;
        }

        if (prop.ValueKind == JsonValueKind.String && double.TryParse(prop.GetString(), out value))
        {
            return value;
        }

        return 0.0;
    }

    private static string Normalize(string name) => name.Replace("_", string.Empty, StringComparison.Ordinal).ToLowerInvariant();

    private static readonly object _crashLogGate = new();

    private static void WriteCrashLog(string source, Exception ex)
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {source}");
            sb.AppendLine(ex.ToString());
            sb.AppendLine(new string('-', 80));
            lock (_crashLogGate)
            {
                File.AppendAllText(CrashLogPath, sb.ToString());
            }
        }
        catch
        {
            // Never let the crash logger itself terminate the process.
        }
    }

    private sealed record SplashStartupStatus(bool Connected, string Summary, string Detail);
}

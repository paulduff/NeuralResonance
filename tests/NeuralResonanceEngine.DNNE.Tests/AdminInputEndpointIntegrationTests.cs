using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using NeuralResonanceEngine.Shared.Contracts;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class AdminInputEndpointIntegrationTests : IClassFixture<ControlProgramProcessFixture>
{
    private readonly ControlProgramProcessFixture _fixture;

    public AdminInputEndpointIntegrationTests(ControlProgramProcessFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task InputGates_Post_Then_Get_Reflects_Runtime_State()
    {
        var client = _fixture.Client;

        var update = await client.PostAsJsonAsync(
            "/api/v1/admin/input-gates",
            new InputGateControlRequest(AvatarVisionEnabled: false, SpontaneousSpikingEnabled: false));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        using var updateDoc = await ReadJsonAsync(update);
        Assert.True(GetBool(updateDoc.RootElement, "applied"));

        var snapshot = await client.GetAsync("/api/v1/admin/input-gates");
        Assert.Equal(HttpStatusCode.OK, snapshot.StatusCode);

        using var snapshotDoc = await ReadJsonAsync(snapshot);
        Assert.False(GetBool(snapshotDoc.RootElement, "avatarVisionEnabled"));
        Assert.False(GetBool(snapshotDoc.RootElement, "spontaneousSpikingEnabled"));
    }

    [Fact]
    public async Task InputGates_Post_Without_Settings_Returns_BadRequest()
    {
        var client = _fixture.Client;

        var response = await client.PostAsJsonAsync("/api/v1/admin/input-gates", new { });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var doc = await ReadJsonAsync(response);
        Assert.Contains("at least one setting", GetString(doc.RootElement, "error"), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task VisualInput_AvatarSource_Is_Blocked_When_AvatarVision_Gate_Is_Disabled()
    {
        var client = _fixture.Client;
        await SetAvatarVisionGateAsync(client, enabled: false);

        var response = await client.PostAsJsonAsync(
            "/api/v1/admin/input/visual",
            new VisualInputRequest(
                Pattern: "VideoFrame",
                Intensity: 0.8f,
                BurstCount: 16,
                TargetStructure: "V1",
                SourceStructure: "Retina",
                Hemisphere: null,
                LeftFieldSaliency: 0.4f,
                RightFieldSaliency: 0.6f,
                UseAttentionRouting: true,
                InputSource: "avatar_vision"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = await ReadJsonAsync(response);
        Assert.True(GetBool(doc.RootElement, "blockedByInputGate"));
        Assert.Equal(0, GetInt(doc.RootElement, "deliveredSpikes"));
        Assert.Equal(0, GetInt(doc.RootElement, "generatedSpikes"));
    }

    [Fact]
    public async Task VisualInput_NonAvatarSource_Uses_Normal_Path_Instead_Of_Gate_Blocking()
    {
        var client = _fixture.Client;
        await SetAvatarVisionGateAsync(client, enabled: false);

        var response = await client.PostAsJsonAsync(
            "/api/v1/admin/input/visual",
            new VisualInputRequest(
                Pattern: "TerrainPaint",
                Intensity: 0.7f,
                BurstCount: 12,
                TargetStructure: "V1",
                SourceStructure: "Retina",
                Hemisphere: null,
                LeftFieldSaliency: 0.3f,
                RightFieldSaliency: 0.7f,
                UseAttentionRouting: true,
                InputSource: "world_map_editor"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = await ReadJsonAsync(response);
        Assert.False(GetBool(doc.RootElement, "blockedByInputGate"));
    }

    [Fact]
    public async Task ObjectInput_AvatarSource_Is_Blocked_When_AvatarVision_Gate_Is_Disabled()
    {
        var client = _fixture.Client;
        await SetAvatarVisionGateAsync(client, enabled: false);

        var response = await client.PostAsJsonAsync(
            "/api/v1/admin/input/object",
            new ObjectInputRequest(
                ObjectId: "obj-1",
                Label: "rock",
                Salience: 0.8f,
                Confidence: 0.7f,
                Intensity: 1.0f,
                BurstCount: 12,
                Hemisphere: null,
                EncodeMemory: true,
                InputSource: "avatar_object"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = await ReadJsonAsync(response);
        Assert.True(GetBool(doc.RootElement, "blockedByInputGate"));
        Assert.Equal(0, GetInt(doc.RootElement, "deliveredSpikes"));
        Assert.Equal(0, GetInt(doc.RootElement, "generatedSpikes"));

        var ingressResponse = await client.GetAsync("/api/v1/admin/input/ingress");
        Assert.Equal(HttpStatusCode.OK, ingressResponse.StatusCode);
        using var ingressDoc = await ReadJsonAsync(ingressResponse);
        Assert.True(TryGetProperty(ingressDoc.RootElement, "object", out var objectIngress));
        Assert.True(GetInt(objectIngress, "accepted") >= 1);
    }

    [Fact]
    public async Task ObjectInput_NonAvatarSource_Processes_Route_And_Returns_Errors_When_No_Instances()
    {
        var client = _fixture.Client;
        await SetAvatarVisionGateAsync(client, enabled: false);

        var response = await client.PostAsJsonAsync(
            "/api/v1/admin/input/object",
            new ObjectInputRequest(
                ObjectId: "obj-2",
                Label: "tree",
                Salience: 0.75f,
                Confidence: 0.68f,
                Intensity: 1.0f,
                BurstCount: 10,
                Hemisphere: "L",
                EncodeMemory: true,
                InputSource: "world_map_editor"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = await ReadJsonAsync(response);
        Assert.False(GetBool(doc.RootElement, "blockedByInputGate"));
        var deliveredSpikes = GetInt(doc.RootElement, "deliveredSpikes");
        var errorCount = GetArrayLength(doc.RootElement, "errors");
        Assert.True(
            deliveredSpikes > 0 || errorCount >= 1,
            $"Expected either delivered spikes (>0) or at least one routing error. delivered={deliveredSpikes}, errors={errorCount}");
    }

    [Fact]
    public async Task VisualAttention_Endpoint_Validates_And_Accepts_Signals()
    {
        var client = _fixture.Client;

        var bad = await client.PostAsJsonAsync("/api/v1/admin/input/visual-attention", new { });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);

        var good = await client.PostAsJsonAsync(
            "/api/v1/admin/input/visual-attention",
            new VisualAttentionInputRequest(LeftFieldSaliency: 0.85f, RightFieldSaliency: 0.10f));
        Assert.Equal(HttpStatusCode.OK, good.StatusCode);

        using var goodDoc = await ReadJsonAsync(good);
        Assert.NotEqual(string.Empty, GetString(goodDoc.RootElement, "focusedField"));
    }

    [Fact]
    public async Task Reasoning_And_Telemetry_Routes_Are_Reachable()
    {
        var client = _fixture.Client;

        var schemas = await client.GetAsync("/api/v1/admin/reasoning/schemas?limit=4");
        Assert.Equal(HttpStatusCode.OK, schemas.StatusCode);

        var startup = await client.GetAsync("/api/v1/admin/startup-health?maxNonOkDetails=4");
        Assert.Equal(HttpStatusCode.OK, startup.StatusCode);

        var transport = await client.GetAsync("/api/v1/transport/stats");
        Assert.Equal(HttpStatusCode.OK, transport.StatusCode);
        using var transportDoc = await ReadJsonAsync(transport);
        Assert.True(TryGetProperty(transportDoc.RootElement, "inputIngress", out var inputIngress));
        Assert.True(TryGetProperty(inputIngress, "object", out _));
    }

    [Fact]
    public async Task Frame_Endpoint_Serializes_FramePayload_Without_InternalServerError()
    {
        var client = _fixture.Client;
        using var response = await client.GetAsync("/api/v1/frame?include_connectome=0&max_output_log=8&max_spike_log=8&max_dispatch_spikes=16");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = await ReadJsonAsync(response);
        Assert.True(TryGetProperty(doc.RootElement, "state", out var state));
        Assert.Equal(JsonValueKind.Object, state.ValueKind);
    }

    [Fact]
    public async Task Frame_Stream_Endpoint_Is_Retired_For_OnDemand_Frames()
    {
        var client = _fixture.Client;

        using var response = await client.GetAsync("/api/v1/frame/stream");

        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
    }

    private static async Task SetAvatarVisionGateAsync(HttpClient client, bool enabled)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/admin/input-gates",
            new InputGateControlRequest(AvatarVisionEnabled: enabled, SpontaneousSpikingEnabled: true));
        response.EnsureSuccessStatusCode();
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(payload);
    }

    private static bool GetBool(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out var value))
        {
            return false;
        }

        if (value.ValueKind == JsonValueKind.True)
        {
            return true;
        }

        if (value.ValueKind == JsonValueKind.False)
        {
            return false;
        }

        if (value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out var parsed))
        {
            return parsed;
        }

        return false;
    }

    private static string GetString(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out var value))
        {
            return string.Empty;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;
    }

    private static int GetInt(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out var value))
        {
            return 0;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }

        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var parsed))
        {
            return parsed;
        }

        return 0;
    }

    private static int GetArrayLength(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }

        return value.GetArrayLength();
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.TryGetProperty(name, out value))
        {
            return true;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}

public sealed class ControlProgramProcessFixture : IAsyncLifetime
{
    private Process? _process;
    private readonly List<string> _logs = [];

    public HttpClient Client { get; private set; } = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    public async Task InitializeAsync()
    {
        var repoRoot = ResolveRepoRoot();
        var dllPath = Path.Combine(repoRoot, "ControlProgram", "bin", "Release", "net8.0", "NeuralResonanceEngine.ControlProgram.dll");
        if (!File.Exists(dllPath))
        {
            throw new FileNotFoundException($"ControlProgram assembly not found: {dllPath}");
        }

        var port = ReservePort();
        var start = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments =
                $"\"{dllPath}\" --urls http://127.0.0.1:{port}" +
                " --AdminInputRecovery:Enabled=false" +
                " --ServiceInstances:Exclusive=true" +
                " --ServiceInstances:0:StructureId=V1" +
                " --ServiceInstances:0:Endpoint=http://127.0.0.1:1" +
                " --ServiceInstances:0:Hemisphere=M",
            WorkingDirectory = repoRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        _process = Process.Start(start) ?? throw new InvalidOperationException("Failed to start ControlProgram process.");
        _ = PumpOutputAsync(_process.StandardOutput);
        _ = PumpOutputAsync(_process.StandardError);

        Client.Dispose();
        Client = new HttpClient
        {
            BaseAddress = new Uri($"http://127.0.0.1:{port}"),
            Timeout = TimeSpan.FromSeconds(10)
        };

        await WaitUntilReadyAsync();
    }

    public Task DisposeAsync()
    {
        try
        {
            Client.Dispose();
        }
        catch
        {
            // best effort cleanup
        }

        if (_process is not null)
        {
            try
            {
                if (!_process.HasExited)
                {
                    _process.Kill(entireProcessTree: true);
                    _process.WaitForExit(3000);
                }
            }
            catch
            {
                // best effort cleanup
            }
        }

        return Task.CompletedTask;
    }

    private async Task WaitUntilReadyAsync()
    {
        var deadline = DateTime.UtcNow.AddSeconds(25);
        while (DateTime.UtcNow < deadline)
        {
            if (_process is null)
            {
                throw new InvalidOperationException("ControlProgram process was not initialized.");
            }

            if (_process.HasExited)
            {
                throw new InvalidOperationException($"ControlProgram exited during startup. Logs:{Environment.NewLine}{string.Join(Environment.NewLine, _logs.TakeLast(120))}");
            }

            try
            {
                using var response = await Client.GetAsync("/api/v1/state");
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch
            {
                // Service may still be starting.
            }

            await Task.Delay(200);
        }

        throw new TimeoutException($"Timed out waiting for ControlProgram startup. Logs:{Environment.NewLine}{string.Join(Environment.NewLine, _logs.TakeLast(120))}");
    }

    private async Task PumpOutputAsync(StreamReader reader)
    {
        while (true)
        {
            string? line;
            try
            {
                line = await reader.ReadLineAsync();
            }
            catch
            {
                break;
            }

            if (line is null)
            {
                break;
            }

            lock (_logs)
            {
                _logs.Add(line);
                if (_logs.Count > 2000)
                {
                    _logs.RemoveRange(0, _logs.Count - 2000);
                }
            }
        }
    }

    private static int ReservePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static string ResolveRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "ControlProgram", "NeuralResonanceEngine.ControlProgram.csproj");
            if (File.Exists(candidate))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not resolve repository root from test base directory.");
    }
}

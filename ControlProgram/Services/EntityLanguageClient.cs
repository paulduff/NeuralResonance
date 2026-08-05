using System.Net.Http.Json;
using NeuralResonanceEngine.Shared.Contracts;

internal interface IEntityLanguageClient
{
    Task<EntityLanguageCandidateResult> GenerateAsync(DyadEntityPromptSnapshot prompt, CancellationToken cancellationToken);
}

internal sealed record EntityLanguageBridgeOptions(
    bool Enabled,
    Uri ApiBaseUri,
    string CheckpointPath,
    string? ChatExamplesPath,
    string? IdentityProfilePath,
    string? HistoryPath,
    string? KnowledgePath,
    int Tokens,
    float Temperature,
    int TopK,
    int Seed,
    TimeSpan Timeout)
{
    public string? ApiKey { get; init; }

    public bool CanGenerate => Enabled && !string.IsNullOrWhiteSpace(CheckpointPath);

    public static EntityLanguageBridgeOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var enabled = bool.TryParse(configuration["NRE_ENTITY_ENABLED"], out var parsedEnabled) && parsedEnabled;
        var baseUrl = configuration["NRE_ENTITY_API_URL"]?.Trim();
        if (!Uri.TryCreate(
                string.IsNullOrWhiteSpace(baseUrl) ? "http://127.0.0.1:5165" : baseUrl,
                UriKind.Absolute,
                out var apiBaseUri) ||
            (apiBaseUri.Scheme != Uri.UriSchemeHttp && apiBaseUri.Scheme != Uri.UriSchemeHttps))
        {
            apiBaseUri = new Uri("http://127.0.0.1:5165");
        }

        var timeoutMs = ReadInt(configuration, "NRE_ENTITY_TIMEOUT_MS", 60_000, 1_000, 300_000);
        return new EntityLanguageBridgeOptions(
            enabled,
            apiBaseUri,
            configuration["NRE_ENTITY_CHECKPOINT_PATH"]?.Trim() ?? string.Empty,
            NormalizeOptional(configuration["NRE_ENTITY_CHAT_EXAMPLES_PATH"]),
            NormalizeOptional(configuration["NRE_ENTITY_IDENTITY_PROFILE_PATH"]),
            NormalizeOptional(configuration["NRE_ENTITY_HISTORY_PATH"]),
            NormalizeOptional(configuration["NRE_ENTITY_KNOWLEDGE_PATH"]),
            ReadInt(configuration, "NRE_ENTITY_TOKENS", 80, 16, 240),
            ReadFloat(configuration, "NRE_ENTITY_TEMPERATURE", 0.20f, 0.05f, 1.25f),
            ReadInt(configuration, "NRE_ENTITY_TOP_K", 8, 1, 80),
            ReadInt(configuration, "NRE_ENTITY_SEED", 1337, int.MinValue, int.MaxValue),
            TimeSpan.FromMilliseconds(timeoutMs))
        {
            ApiKey = NormalizeOptional(configuration["NRE_ENTITY_API_KEY"])
        };
    }

    private static int ReadInt(IConfiguration configuration, string key, int fallback, int minimum, int maximum)
        => int.TryParse(configuration[key], out var value) ? Math.Clamp(value, minimum, maximum) : fallback;

    private static float ReadFloat(IConfiguration configuration, string key, float fallback, float minimum, float maximum)
        => float.TryParse(configuration[key], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? Math.Clamp(value, minimum, maximum)
            : fallback;

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

internal sealed record EntityLanguageCandidateResult(
    bool IsAvailable,
    string Detail,
    string CandidateText,
    string EntityVersion,
    string EntityConfiguration,
    IReadOnlyList<string> SourceReferences)
{
    public static EntityLanguageCandidateResult Unavailable(string detail)
        => new(false, detail, string.Empty, string.Empty, string.Empty, Array.Empty<string>());
}

internal sealed class EntityLanguageClient(HttpClient httpClient, EntityLanguageBridgeOptions options) : IEntityLanguageClient
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly EntityLanguageBridgeOptions _options = options;

    public async Task<EntityLanguageCandidateResult> GenerateAsync(DyadEntityPromptSnapshot prompt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        if (!_options.CanGenerate)
        {
            return EntityLanguageCandidateResult.Unavailable(
                "Entity bridge is disabled or NRE_ENTITY_CHECKPOINT_PATH is not configured.");
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/chat")
            {
                Content = JsonContent.Create(new EntityChatApiRequest(
                    _options.CheckpointPath,
                    prompt.PromptText,
                    Tokens: _options.Tokens,
                    Temperature: _options.Temperature,
                    TopK: _options.TopK,
                    Seed: _options.Seed,
                    ChatExamplesPath: _options.ChatExamplesPath,
                    IdentityProfilePath: _options.IdentityProfilePath,
                    HistoryPath: _options.HistoryPath,
                    KnowledgePath: _options.KnowledgePath))
            };
            if (!string.IsNullOrWhiteSpace(_options.ApiKey))
            {
                request.Headers.TryAddWithoutValidation("X-Entity-Api-Key", _options.ApiKey);
            }
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return EntityLanguageCandidateResult.Unavailable($"Entity API returned HTTP {(int)response.StatusCode}.");
            }

            var payload = await response.Content.ReadFromJsonAsync<EntityChatApiResponse>(cancellationToken: cancellationToken);
            if (payload is null || string.IsNullOrWhiteSpace(payload.Response))
            {
                return EntityLanguageCandidateResult.Unavailable("Entity API returned no candidate text.");
            }

            var historicalSources = payload.HistoricalSources ?? Array.Empty<EntityChatSource>();
            var knowledgeSources = payload.KnowledgeSources ?? Array.Empty<EntityChatSource>();
            var references = historicalSources
                .Concat(knowledgeSources)
                .Select(source => NormalizeReference(source.StableUrl, source.SourceId, source.Title))
                .Where(reference => !string.IsNullOrWhiteSpace(reference))
                .Distinct(StringComparer.Ordinal)
                .Take(DyadLanguageContract.MaxSourceReferences)
                .ToArray();
            var checkpointName = Path.GetFileName(_options.CheckpointPath);
            var version = $"{checkpointName}; architecture={NormalizeValue(payload.Architecture, "unknown")}; tokenizer={NormalizeValue(payload.Tokenizer, "unknown")}";
            var configuration = $"tokens={_options.Tokens};temperature={_options.Temperature:0.00};topK={_options.TopK};seed={_options.Seed}";
            return new EntityLanguageCandidateResult(
                true,
                "Entity candidate generated through the hosted chat API.",
                payload.Response.Trim(),
                TrimTo(version, 128),
                TrimTo(configuration, 256),
                references);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return EntityLanguageCandidateResult.Unavailable($"Entity API is unavailable: {exception.GetType().Name}.");
        }
    }

    private static string NormalizeReference(string? stableUrl, string? sourceId, string? title)
    {
        var value = !string.IsNullOrWhiteSpace(stableUrl)
            ? stableUrl
            : !string.IsNullOrWhiteSpace(sourceId)
                ? sourceId
                : title;
        return string.IsNullOrWhiteSpace(value) ? string.Empty : TrimTo(value.Trim(), 256);
    }

    private static string NormalizeValue(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string TrimTo(string value, int maximumLength)
        => value.Length <= maximumLength ? value : value[..maximumLength];

    private sealed record EntityChatApiRequest(
        string CheckpointPath,
        string Message,
        int Tokens,
        float Temperature,
        int TopK,
        int Seed,
        string? ChatExamplesPath,
        string? IdentityProfilePath,
        string? HistoryPath,
        string? KnowledgePath,
        IReadOnlyList<EntityChatTurn>? History = null,
        int ShortMemoryCharacters = 0,
        string? MemoryPath = null,
        int Recall = 0);

    private sealed record EntityChatTurn(string Role, string Text);

    private sealed record EntityChatApiResponse(
        string? Response,
        string? Architecture,
        string? Tokenizer,
        IReadOnlyList<EntityChatSource>? HistoricalSources,
        IReadOnlyList<EntityChatSource>? KnowledgeSources);

    private sealed record EntityChatSource(string? SourceId, string? Title, string? StableUrl);
}

using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Forms;
using NRE.Blazor.Services;
using Xunit;

namespace NRE.Tests;

public sealed class EngineApiClientTests
{
    [Fact]
    public async Task SetPeerNameAsync_Encodes_Name_In_Query()
    {
        var handler = new RecordingHandler();
        var client = CreateClient(handler);

        await client.SetPeerNameAsync("Paul & Beverly");

        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        string uri = handler.LastRequest.RequestUri!.AbsoluteUri;
        Assert.Contains("http://localhost:5005/api/engine/peer/name?name=Paul", uri);
        Assert.Contains("%26", uri);
    }

    [Fact]
    public async Task ApplyVisualAsync_Posts_Expected_Json_Payload()
    {
        var handler = new RecordingHandler();
        var client = CreateClient(handler);

        await client.ApplyVisualAsync(0.4f, 6.5f, 1.25f);

        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("http://localhost:5005/api/engine/visual", handler.LastRequest.RequestUri!.ToString());
        var payload = await handler.LastRequest.Content!.ReadFromJsonAsync<VisualPayload>();
        Assert.NotNull(payload);
        Assert.Equal(0.4f, payload!.Intensity01);
        Assert.Equal(6.5f, payload.SpeedHz);
        Assert.Equal(1.25f, payload.SpatialFreq);
        Assert.True(payload.Enabled);
    }

    [Fact]
    public async Task LoadBrainAsync_Uses_Octet_Stream_Content_Type()
    {
        var handler = new RecordingHandler();
        var client = CreateClient(handler);

        using var response = await client.LoadBrainAsync(new byte[] { 1, 2, 3, 4 });

        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("application/octet-stream", handler.LastRequest.Content!.Headers.ContentType!.MediaType);
        Assert.Equal("http://localhost:5005/api/engine/load", handler.LastRequest.RequestUri!.ToString());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static EngineApiClient CreateClient(RecordingHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:5005/")
        };

        return new EngineApiClient(new StubHttpClientFactory(httpClient));
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;

        public StubHttpClientFactory(HttpClient client)
        {
            _client = client;
        }

        public HttpClient CreateClient(string name) => _client;
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = await CloneAsync(request, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { ok = true })
            };
        }

        private static async Task<HttpRequestMessage> CloneAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var clone = new HttpRequestMessage(request.Method, request.RequestUri);
            foreach (var header in request.Headers)
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

            if (request.Content is not null)
            {
                var bytes = await request.Content.ReadAsByteArrayAsync(cancellationToken);
                clone.Content = new ByteArrayContent(bytes);
                foreach (var header in request.Content.Headers)
                    clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            return clone;
        }
    }

    private sealed class VisualPayload
    {
        public float Intensity01 { get; set; }
        public float SpeedHz { get; set; }
        public float SpatialFreq { get; set; }
        public bool Enabled { get; set; }
    }
}

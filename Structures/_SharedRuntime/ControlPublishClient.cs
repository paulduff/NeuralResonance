using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

internal sealed class ControlPublishClient
{
	private static readonly HttpClient PublishClient = new HttpClient(new SocketsHttpHandler
	{
		UseProxy = false,
		ConnectTimeout = TimeSpan.FromMilliseconds(300.0),
		PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2.0),
		PooledConnectionLifetime = TimeSpan.FromMinutes(10.0),
		MaxConnectionsPerServer = 64,
		AutomaticDecompression = DecompressionMethods.None
	})
	{
		Timeout = TimeSpan.FromMilliseconds(1000.0)
	};

	private readonly Uri? _publishUri;

	private readonly string _instanceKey;

	private readonly string _hemisphere;

	private int _publishInFlight;
	private readonly string? _controlSharedSecret;

	public ControlPublishClient()
	{
		string environmentVariable = Environment.GetEnvironmentVariable("CONTROL_PUBLISH_URL");
		_publishUri = (Uri.TryCreate(environmentVariable, UriKind.Absolute, out Uri result) ? result : null);
		_instanceKey = Environment.GetEnvironmentVariable("SERVICE_INSTANCE") ?? "M_unknown";
		_hemisphere = Environment.GetEnvironmentVariable("HEMISPHERE") ?? "M";
		_controlSharedSecret = NreControlPlaneSecurity.ResolveSharedSecret();
	}

	public async ValueTask PublishAsync(StructureId structureId, StructureStepResult step, CancellationToken cancellationToken)
	{
		if ((object)_publishUri == null || Interlocked.Exchange(ref _publishInFlight, 1) == 1)
		{
			return;
		}

		try
		{
			PublishedStepMessage payload = new PublishedStepMessage(_instanceKey, _hemisphere, structureId, step);
			using var request = new HttpRequestMessage(HttpMethod.Post, _publishUri)
			{
				Content = JsonContent.Create(payload)
			};
			NreControlPlaneSecurity.ApplyRequestAuthentication(request, _controlSharedSecret);
			using HttpResponseMessage response = await PublishClient.SendAsync(request, cancellationToken);
			_ = response.IsSuccessStatusCode;
		}
		catch
		{
		}
		finally
		{
			Interlocked.Exchange(ref _publishInFlight, 0);
		}
	}
}



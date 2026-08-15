using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;
using ProtoBuf;
using ProtoBuf.Grpc.Server;

public static class StructureHostApplication
{
	public static void Run(string[] args, StructureProfile profile)
	{
		var sharedSecret = NreStructureSecurity.ResolveSharedSecret();
		var listenAnyIp = NreStructureSecurity.ResolveListenAnyIp();
		if (listenAnyIp && sharedSecret is null)
		{
			throw new InvalidOperationException(
				$"{NreStructureSecurity.ListenAnyIpEnvironmentVariable}=true requires {NreStructureSecurity.SharedSecretEnvironmentVariable}.");
		}

		WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
		var verboseFrameworkLogs = string.Equals(
			Environment.GetEnvironmentVariable("NRE_VERBOSE_FRAMEWORK_LOGS"),
			"true",
			StringComparison.OrdinalIgnoreCase);
		if (!verboseFrameworkLogs)
		{
			builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);
			builder.Logging.AddFilter("Microsoft.AspNetCore.Routing.EndpointMiddleware", LogLevel.Warning);
		}
		builder.WebHost.ConfigureKestrel(options =>
		{
			options.Limits.MaxRequestBodySize = StructureTransportLimits.MaxSpikeBatchBytes;
			if (int.TryParse(Environment.GetEnvironmentVariable("PORT"), out var port))
			{
				if (listenAnyIp)
				{
					options.ListenAnyIP(port, listen => listen.Protocols = HttpProtocols.Http1AndHttp2);
				}
				else
				{
					options.ListenLocalhost(port, listen => listen.Protocols = HttpProtocols.Http1AndHttp2);
				}
			}
		});

		builder.Services.AddSingleton(profile);
		builder.Services.AddSingleton<StructureEngine>();
		builder.Services.AddSingleton<ControlPublishClient>();
		builder.Services.AddCodeFirstGrpc();

		WebApplication app = builder.Build();

		// Optional shared-secret authentication. When NRE_STRUCTURE_SHARED_SECRET is
		// set in the environment, every request must carry the same value in the
		// `X-NRE-Auth` header or it is rejected with 401. Off by default to preserve
		// the existing localhost-only contract.
		if (sharedSecret is not null)
		{
			app.Use(async (context, next) =>
			{
				if (context.Request.Path.StartsWithSegments("/health"))
				{
					await next();
					return;
				}

				if (!context.Request.Headers.TryGetValue(NreStructureSecurity.HeaderName, out var header)
					|| !NreStructureSecurity.IsAuthorized(header.ToString(), sharedSecret))
				{
					context.Response.StatusCode = StatusCodes.Status401Unauthorized;
					return;
				}

				await next();
			});
		}

		app.MapGrpcService<StructureSpikeGrpcService>();
		MapHttpEndpoints(app);
		app.Run();
	}

	private static void MapHttpEndpoints(WebApplication app)
	{
		app.MapPost("/api/v1/structure/spike", async (HttpRequest request, StructureEngine engine, StructureProfile profile, CancellationToken ct) =>
		{
			try
			{
				if (RequestIsTooLarge(request))
				{
					return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
				}

				using var activity = StructureTelemetry.Source.StartActivity("spike.receive");
				activity?.SetTag("structure.id", profile.StructureId.ToString());
				SpikeMessage? spike;
				if (request.HasJsonContentType())
				{
					spike = await request.ReadFromJsonAsync(
						StructureJsonContext.Default.SpikeMessage,
						ct);
				}
				else
				{
					spike = await SpikeProtocol.receive_spike(request.Body, ct);
				}

				if (spike == null)
				{
					activity?.SetStatus(ActivityStatusCode.Error, "Spike payload missing");
					return Results.BadRequest("Spike payload missing");
				}

				await engine.EnqueueSpikeAsync(spike, ct);
				return Results.Accepted();
			}
			catch (StructureIngressOverloadException ex)
			{
				return Results.Problem(ex.Message, statusCode: StatusCodes.Status429TooManyRequests);
			}
			catch (Exception ex) when (IsInvalidSpikePayload(ex))
			{
				return Results.BadRequest($"Invalid spike payload: {ex.Message}");
			}
		}).Accepts<byte[]>("application/octet-stream", "application/json");

		app.MapPost("/api/v1/structure/spike-batch", async (HttpRequest request, StructureEngine engine, CancellationToken ct) =>
		{
			if (RequestIsTooLarge(request))
			{
				return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
			}

			try
			{
				List<SpikeMessage>? spikes;
				if (request.HasJsonContentType())
				{
					spikes = await request.ReadFromJsonAsync(
						StructureJsonContext.Default.ListSpikeMessage,
						ct);
				}
				else
				{
					await using MemoryStream buffered = await ReadBoundedBodyAsync(request.Body, ct);
					spikes = new List<SpikeMessage>();
					while (buffered.Position < buffered.Length)
					{
						SpikeMessage spike = Serializer.DeserializeWithLengthPrefix<SpikeMessage>(buffered, PrefixStyle.Base128);
						if (spike == null)
						{
							break;
						}

						spikes.Add(spike);
						if (spikes.Count > StructureTransportLimits.MaxSpikeBatchCount)
						{
							return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
						}
					}
				}

				if (spikes == null || spikes.Count == 0)
				{
					return Results.BadRequest("Spike batch payload missing");
				}
				if (spikes.Count > StructureTransportLimits.MaxSpikeBatchCount)
				{
					return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
				}

				int accepted = await engine.EnqueueSpikeBatchAsync(spikes, ct);
				return Results.Ok(new { accepted });
			}
			catch (StructureIngressOverloadException ex)
			{
				return Results.Problem(ex.Message, statusCode: StatusCodes.Status429TooManyRequests);
			}
			catch (Exception ex) when (IsInvalidSpikePayload(ex))
			{
				return Results.BadRequest($"Invalid spike batch payload: {ex.Message}");
			}
		}).Accepts<byte[]>("application/octet-stream", "application/json");

		// Acknowledge-only ticks depended on a separate fire-and-forget publish path.
		// They could report success while losing the corresponding outbound activity.
		// Callers must use /step, which returns one coherent result for each tick.
		app.MapPost("/api/v1/structure/tick", () => Results.Problem(
			title: "Acknowledge-only tick transport retired.",
			detail: "Use POST /api/v1/structure/step to receive the complete StructureStepResult.",
			statusCode: StatusCodes.Status410Gone));

		app.MapPost("/api/v1/structure/step", async (StructureStepRequest request, StructureEngine engine, CancellationToken ct) =>
			Results.Ok(await engine.ProcessStepAsync(request.TickSignal, request.IncludeTop ? request.TopK : 0, ct)));

		app.MapPost("/api/v1/structure/drain", () => Results.Problem(
			title: "Independent drain transport retired.",
			detail: "Use POST /api/v1/structure/step so a concurrent caller cannot steal outbound spikes.",
			statusCode: StatusCodes.Status410Gone));

		app.MapPost("/api/v1/structure/shutdown", (IHostApplicationLifetime lifetime) =>
		{
			_ = Task.Run(async () =>
			{
				await Task.Delay(100).ConfigureAwait(false);
				lifetime.StopApplication();
			});
			return Results.Accepted();
		});

		app.MapGet("/api/v1/structure/top", async (int count, StructureEngine engine, CancellationToken ct) =>
			Results.Ok(await engine.GetTopActiveNeuronsAsync(Math.Clamp(count, 1, 100), ct)));

		app.MapGet("/health", (StructureProfile profile) => Results.Ok(new
		{
			structure = profile.StructureId,
			profile.NeuronModel,
			profile.PlasticityRule,
			microtubuleMode = IntracellularMicrotubuleState.NormalizeMode(Environment.GetEnvironmentVariable("NRE_MICROTUBULE_MODE")),
			microtubuleLabel = "Experimental: intracellular microtubule approximation"
		}));
	}

	private static bool RequestIsTooLarge(HttpRequest request) =>
		request.ContentLength is > StructureTransportLimits.MaxSpikeBatchBytes;

	private static bool IsInvalidSpikePayload(Exception exception) => exception is
		ArgumentException or
		InvalidOperationException or
		InvalidDataException or
		EndOfStreamException or
		System.Text.Json.JsonException or
		ProtoException;

	private static async Task<MemoryStream> ReadBoundedBodyAsync(Stream body, CancellationToken cancellationToken)
	{
		var output = new MemoryStream();
		byte[] buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
		try
		{
			while (true)
			{
				int read = await body.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
				if (read == 0)
				{
					output.Position = 0;
					return output;
				}
				if (output.Length + read > StructureTransportLimits.MaxSpikeBatchBytes)
				{
					output.Dispose();
					throw new Microsoft.AspNetCore.Http.BadHttpRequestException(
						"Spike batch body exceeds the configured limit.",
						StatusCodes.Status413PayloadTooLarge);
				}

				await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
			}
		}
		finally
		{
			ArrayPool<byte>.Shared.Return(buffer);
		}
	}

}

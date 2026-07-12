using System;
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
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;
using ProtoBuf;
using ProtoBuf.Grpc.Server;

public static class StructureHostApplication
{
	public static void Run(string[] args, StructureProfile profile)
	{
		WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
		builder.WebHost.ConfigureKestrel(options =>
		{
			if (int.TryParse(Environment.GetEnvironmentVariable("PORT"), out var port))
			{
				options.ListenAnyIP(port, listen => listen.Protocols = HttpProtocols.Http1AndHttp2);
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
		var sharedSecret = Environment.GetEnvironmentVariable("NRE_STRUCTURE_SHARED_SECRET");
		if (!string.IsNullOrWhiteSpace(sharedSecret))
		{
			app.Use(async (context, next) =>
			{
				if (context.Request.Path.StartsWithSegments("/health"))
				{
					await next();
					return;
				}

				if (!context.Request.Headers.TryGetValue("X-NRE-Auth", out var header)
					|| !string.Equals(header.ToString(), sharedSecret, StringComparison.Ordinal))
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
			using var activity = StructureTelemetry.Source.StartActivity("spike.receive");
			activity?.SetTag("structure.id", profile.StructureId.ToString());
			SpikeMessage spike = await SpikeProtocol.receive_spike(request.Body, ct);
			if (spike == null)
			{
				activity?.SetStatus(ActivityStatusCode.Error, "Spike payload missing");
				return Results.BadRequest("Spike payload missing");
			}

			await engine.EnqueueSpikeAsync(spike, ct);
			return Results.Accepted();
		}).Accepts<byte[]>("application/octet-stream");

		app.MapPost("/api/v1/structure/spike-batch", async (HttpRequest request, StructureEngine engine, CancellationToken ct) =>
		{
			if (request.HasJsonContentType())
			{
				List<SpikeMessage>? spikes = await request.ReadFromJsonAsync(
					StructureJsonContext.Default.ListSpikeMessage,
					ct);
				if (spikes == null || spikes.Count == 0)
				{
					return Results.BadRequest("Spike batch payload missing");
				}

				int acceptedJson = 0;
				foreach (SpikeMessage spike in spikes)
				{
					if (spike != null)
					{
						await engine.EnqueueSpikeAsync(spike, ct);
						acceptedJson++;
					}
				}

				return Results.Ok(new { accepted = acceptedJson });
			}

			await using MemoryStream buffered = new MemoryStream();
			await request.Body.CopyToAsync(buffered, ct);
			if (buffered.Length == 0)
			{
				return Results.BadRequest("Spike batch payload missing");
			}

			buffered.Position = 0L;
			int accepted = 0;
			while (buffered.Position < buffered.Length)
			{
				SpikeMessage spike = Serializer.DeserializeWithLengthPrefix<SpikeMessage>(buffered, PrefixStyle.Base128);
				if (spike == null)
				{
					break;
				}

				await engine.EnqueueSpikeAsync(spike, ct);
				accepted++;
			}

			return Results.Ok(new { accepted });
		}).Accepts<byte[]>("application/octet-stream");

		// Acknowledge-only ticks depended on a separate fire-and-forget publish path.
		// They could report success while losing the corresponding outbound activity.
		// Callers must use /step, which returns one coherent result for each tick.
		app.MapPost("/api/v1/structure/tick", () => Results.Problem(
			title: "Acknowledge-only tick transport retired.",
			detail: "Use POST /api/v1/structure/step to receive the complete StructureStepResult.",
			statusCode: StatusCodes.Status410Gone));

		app.MapPost("/api/v1/structure/step", async (StructureStepRequest request, StructureEngine engine, CancellationToken ct) =>
			Results.Ok(await engine.ProcessStepAsync(request.TickSignal, request.IncludeTop ? request.TopK : 0, ct)));

		app.MapPost("/api/v1/structure/drain", async (StructureEngine engine, CancellationToken ct) =>
			Results.Ok(await engine.DrainOutboundSpikesAsync(ct)));

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

}

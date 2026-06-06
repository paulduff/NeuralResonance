using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;
using ProtoBuf.Grpc;

internal sealed class StructureSpikeGrpcService(StructureEngine engine) : IStructureSpikeTransport
{
	public async ValueTask<SpikeBatchAck> PushSpikeBatchAsync(SpikeBatchEnvelope request, CallContext context = default(CallContext))
	{
		if (request?.Spikes == null || request.Spikes.Count == 0)
		{
			return new SpikeBatchAck
			{
				Accepted = 0,
				Error = "empty_batch"
			};
		}
		int accepted = 0;
		foreach (SpikeMessage spike in request.Spikes)
		{
			if (spike != null)
			{
				await engine.EnqueueSpikeAsync(spike, context.CancellationToken);
				accepted++;
			}
		}
		return new SpikeBatchAck
		{
			Accepted = accepted
		};
	}

	public async IAsyncEnumerable<SpikeBatchAck> StreamSpikeBatchesAsync(
		IAsyncEnumerable<SpikeBatchEnvelope> requests,
		CallContext context = default(CallContext))
	{
		await foreach (var batch in requests.WithCancellation(context.CancellationToken).ConfigureAwait(false))
		{
			if (batch?.Spikes == null || batch.Spikes.Count == 0)
			{
				yield return new SpikeBatchAck { Accepted = 0, Error = "empty_batch" };
				continue;
			}

			int accepted = 0;
			foreach (var spike in batch.Spikes)
			{
				if (spike != null)
				{
					await engine.EnqueueSpikeAsync(spike, context.CancellationToken).ConfigureAwait(false);
					accepted++;
				}
			}
			yield return new SpikeBatchAck { Accepted = accepted };
		}
	}
}

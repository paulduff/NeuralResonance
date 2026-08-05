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
				Error = "empty_batch",
				BatchId = request?.BatchId ?? string.Empty
			};
		}
		if (request.Spikes.Count > StructureTransportLimits.MaxSpikeBatchCount)
		{
			return new SpikeBatchAck { Accepted = 0, Error = "batch_too_large", BatchId = request.BatchId };
		}

		try
		{
			int accepted = await engine.EnqueueSpikeBatchAsync(request.Spikes, context.CancellationToken).ConfigureAwait(false);
			return new SpikeBatchAck { Accepted = accepted, BatchId = request.BatchId };
		}
		catch (StructureIngressOverloadException ex)
		{
			return new SpikeBatchAck { Accepted = 0, Error = ex.Message, BatchId = request.BatchId };
		}
		catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
		{
			return new SpikeBatchAck { Accepted = 0, Error = $"invalid_batch:{ex.Message}", BatchId = request.BatchId };
		}
	}

	public async IAsyncEnumerable<SpikeBatchAck> StreamSpikeBatchesAsync(
		IAsyncEnumerable<SpikeBatchEnvelope> requests,
		CallContext context = default(CallContext))
	{
		await foreach (var batch in requests.WithCancellation(context.CancellationToken).ConfigureAwait(false))
		{
			if (batch?.Spikes == null || batch.Spikes.Count == 0)
			{
				yield return new SpikeBatchAck { Accepted = 0, Error = "empty_batch", BatchId = batch?.BatchId ?? string.Empty };
				continue;
			}

			if (batch.Spikes.Count > StructureTransportLimits.MaxSpikeBatchCount)
			{
				yield return new SpikeBatchAck { Accepted = 0, Error = "batch_too_large", BatchId = batch.BatchId };
				continue;
			}

			SpikeBatchAck ack;
			try
			{
				int accepted = await engine.EnqueueSpikeBatchAsync(batch.Spikes, context.CancellationToken).ConfigureAwait(false);
				ack = new SpikeBatchAck { Accepted = accepted, BatchId = batch.BatchId };
			}
			catch (StructureIngressOverloadException ex)
			{
				ack = new SpikeBatchAck { Accepted = 0, Error = ex.Message, BatchId = batch.BatchId };
			}
			catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
			{
				ack = new SpikeBatchAck { Accepted = 0, Error = $"invalid_batch:{ex.Message}", BatchId = batch.BatchId };
			}
			yield return ack;
		}
	}
}

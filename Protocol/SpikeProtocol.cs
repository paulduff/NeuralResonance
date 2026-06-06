using ProtoBuf;

namespace NeuralResonanceEngine.Protocol;

public static class SpikeProtocol
{
    public static Task send_spike(SpikeMessage message, Stream destination, CancellationToken cancellationToken = default)
    {
        if (!validate_spike(message, out var validationError))
        {
            throw new InvalidDataException(validationError);
        }

        cancellationToken.ThrowIfCancellationRequested();
        Serializer.SerializeWithLengthPrefix(destination, message, PrefixStyle.Base128);
        return Task.CompletedTask;
    }

    public static async Task<SpikeMessage?> receive_spike(Stream source, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Pool the intermediate buffer so the receive path does not allocate a fresh
        // MemoryStream-backed byte[] per spike. Wrapping the pooled array in a
        // MemoryStream with the (buffer, index, count, writable) constructor avoids
        // any further array allocation.
        var pool = System.Buffers.ArrayPool<byte>.Shared;
        var buffer = pool.Rent(4096);
        try
        {
            int totalRead = 0;
            while (true)
            {
                if (totalRead == buffer.Length)
                {
                    var bigger = pool.Rent(buffer.Length * 2);
                    Buffer.BlockCopy(buffer, 0, bigger, 0, totalRead);
                    pool.Return(buffer);
                    buffer = bigger;
                }

                int read = await source.ReadAsync(buffer.AsMemory(totalRead), cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }
                totalRead += read;
            }

            if (totalRead == 0)
            {
                return null;
            }

            using var ms = new MemoryStream(buffer, 0, totalRead, writable: false);
            return Serializer.DeserializeWithLengthPrefix<SpikeMessage>(ms, PrefixStyle.Base128);
        }
        finally
        {
            pool.Return(buffer);
        }
    }

    public static bool validate_spike(SpikeMessage message, out string error)
    {
        if (message.MessageId == Guid.Empty)
        {
            error = "message_id must be set";
            return false;
        }

        if (message.SynapseId == Guid.Empty)
        {
            error = "synapse_id must be set";
            return false;
        }

        if (message.TimestampMs < 0)
        {
            error = "timestamp_ms must be >= 0";
            return false;
        }

        if (string.IsNullOrWhiteSpace(message.SourceNeuronId) || string.IsNullOrWhiteSpace(message.TargetNeuronId))
        {
            error = "source_neuron_id and target_neuron_id must be set";
            return false;
        }

        if (message.VesicleQuanta <= 0)
        {
            error = "vesicle_quanta must be > 0";
            return false;
        }

        if (message.ReuptakeRate <= 0)
        {
            error = "reuptake_rate must be > 0";
            return false;
        }

        // Side-effect: per-spike normalization is part of validation so the engine
        // never has to defend against out-of-range neuromod values. The in-place
        // variant avoids the per-spike allocation the original Clamp would do.
        // ModulationContext is now nullable; null means "no modulation" and is left
        // as-is so we do not re-introduce the allocation we are trying to avoid.
        if (message.ModulationContext is not null)
        {
            NeuromodState.ClampInPlace(message.ModulationContext);
        }
        error = string.Empty;
        return true;
    }
}

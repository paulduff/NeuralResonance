using System.Collections.Concurrent;
using System.IO;
using System.Text;

namespace NRE.WpfWorldSim;

internal sealed class AsyncRuntimeLogWriter : IDisposable
{
    private const int QueueCapacity = 4096;
    private const int MaxBatchLines = 128;

    private readonly BlockingCollection<string> _queue = new(new ConcurrentQueue<string>(), QueueCapacity);
    private readonly Thread _worker;
    private readonly string _path;
    private bool _disposed;

    public AsyncRuntimeLogWriter(string path)
    {
        _path = path;
        _worker = new Thread(WorkerLoop)
        {
            IsBackground = true,
            Name = "NRE.WorldSim.RuntimeLogWriter"
        };
        _worker.Start();
    }

    public void Enqueue(string line)
    {
        if (_disposed || _queue.IsAddingCompleted)
        {
            return;
        }

        if (_queue.TryAdd(line))
        {
            return;
        }

        // Prefer newest diagnostics over blocking the simulation loop.
        _queue.TryTake(out _);
        _queue.TryAdd(line);
    }

    private void WorkerLoop()
    {
        var buffer = new List<string>(MaxBatchLines);
        while (!_queue.IsCompleted)
        {
            try
            {
                var line = _queue.Take();
                buffer.Add(line);

                while (buffer.Count < MaxBatchLines && _queue.TryTake(out var next))
                {
                    buffer.Add(next);
                }

                Flush(buffer);
                buffer.Clear();
            }
            catch (InvalidOperationException)
            {
                break;
            }
            catch
            {
                buffer.Clear();
            }
        }

        while (_queue.TryTake(out var remaining))
        {
            buffer.Add(remaining);
            if (buffer.Count >= MaxBatchLines)
            {
                Flush(buffer);
                buffer.Clear();
            }
        }

        if (buffer.Count > 0)
        {
            Flush(buffer);
        }
    }

    private void Flush(List<string> lines)
    {
        if (lines.Count == 0)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path) ?? ".");
            var builder = new StringBuilder(lines.Count * 96);
            foreach (var line in lines)
            {
                builder.AppendLine(line);
            }

            File.AppendAllText(_path, builder.ToString(), Encoding.UTF8);
        }
        catch
        {
            // Runtime logging must never affect simulation responsiveness.
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _queue.CompleteAdding();
        try
        {
            if (_worker.IsAlive)
            {
                _worker.Join(TimeSpan.FromSeconds(1));
            }
        }
        catch
        {
            // Best-effort shutdown.
        }

        _queue.Dispose();
    }
}

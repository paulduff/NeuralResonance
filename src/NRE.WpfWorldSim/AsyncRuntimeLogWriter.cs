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
    private int _disposed;

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
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        try
        {
            if (_queue.IsAddingCompleted || _queue.TryAdd(line))
            {
                return;
            }

            // Prefer newest diagnostics over blocking the simulation loop.
            _queue.TryTake(out _);
            _queue.TryAdd(line);
        }
        catch (ObjectDisposedException)
        {
            // The worker stopped and released the queue.
        }
        catch (InvalidOperationException)
        {
            // Shutdown raced with a log write.
        }
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

        try
        {
            while (_queue.TryTake(out var remaining))
            {
                buffer.Add(remaining);
                if (buffer.Count >= MaxBatchLines)
                {
                    Flush(buffer);
                    buffer.Clear();
                }
            }
        }
        catch (ObjectDisposedException)
        {
            return;
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
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        try
        {
            _queue.CompleteAdding();
        }
        catch (InvalidOperationException)
        {
            return;
        }

        try
        {
            if (_worker.IsAlive)
            {
                _worker.Join(TimeSpan.FromSeconds(2));
            }
        }
        catch
        {
            // Best-effort shutdown.
        }

        if (!_worker.IsAlive)
        {
            _queue.Dispose();
        }
    }
}

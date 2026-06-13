using System.Text;

internal static class ControlHealthLog
{
    private const long MaxLogBytes = 6L * 1024 * 1024;
    private static readonly object Gate = new();
    private static readonly string DirectoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NRE.ControlProgram");

    private static readonly string LogPath = Path.Combine(DirectoryPath, "control-health.log");

    public static void Append(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(DirectoryPath);
            lock (Gate)
            {
                RotateIfNeeded();
                File.AppendAllText(
                    LogPath,
                    $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz}] {message}{Environment.NewLine}",
                    Encoding.UTF8);
            }
        }
        catch
        {
            // Runtime health logging must never destabilize the brain loop.
        }
    }

    private static void RotateIfNeeded()
    {
        var file = new FileInfo(LogPath);
        if (!file.Exists || file.Length <= MaxLogBytes)
        {
            return;
        }

        var rotatedPath = Path.Combine(DirectoryPath, "control-health.previous.log");
        if (File.Exists(rotatedPath))
        {
            File.Delete(rotatedPath);
        }

        File.Move(LogPath, rotatedPath);
    }
}

using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

// Read-only dependency capture. No cell, body, simulator or test is created.
internal static class RuntimeFingerprint
{
    internal static void Write(string output)
    {
        if (!Path.IsPathFullyQualified(output) || File.Exists(output) || Directory.Exists(output))
            throw new ArgumentException("Require a new absolute metadata file.");
        string app = Path.GetFullPath(AppContext.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string trusted = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string
            ?? throw new InvalidOperationException("Runtime did not declare its platform assemblies.");
        var folders = trusted.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(Path.GetFullPath).Where(p => !p.StartsWith(app, StringComparison.OrdinalIgnoreCase))
            .Select(p => Path.GetDirectoryName(p)!).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray();
        if (folders.Length == 0) throw new InvalidOperationException("No external runtime dependency directories found.");
        string Hash(string path)
        {
            using var stream = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(stream));
        }
        string host = Environment.ProcessPath ?? throw new InvalidOperationException("Unknown execution host.");
        string[] switches = ["DOTNET_TieredCompilation", "DOTNET_TieredPGO", "DOTNET_ReadyToRun",
            "DOTNET_EnableHWIntrinsic", "COMPlus_TieredCompilation", "COMPlus_TieredPGO", "COMPlus_ReadyToRun",
            "COMPlus_EnableHWIntrinsic", "COMPlus_EnableAVX", "COMPlus_EnableAVX2", "COMPlus_EnableFMA"];
        var value = new
        {
            version = "motor-probe-runtime-dependencies-v1", framework = RuntimeInformation.FrameworkDescription,
            runtimeVersion = Environment.Version.ToString(), processArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
            osArchitecture = RuntimeInformation.OSArchitecture.ToString(), host = Path.GetFullPath(host), hostSha256 = Hash(host),
            runtimeSwitches = switches.ToDictionary(k => k, Environment.GetEnvironmentVariable),
            directories = folders.Select(folder => new { path = folder,
                files = Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                    .Select(path => new { path = Path.GetFullPath(path), sha256 = Hash(path) }).ToArray() }).ToArray(),
            appLocalDependenciesExcluded = true, appLocalFilesRequireSeparateBinaryManifest = true,
            modelsExecuted = false, testsExecuted = false
        };
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        File.WriteAllText(output, JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("Recorded managed runtime dependencies; no model or test executed.");
    }
}

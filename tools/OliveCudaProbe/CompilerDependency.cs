using System.IO.Compression;
using System.Security.Cryptography;

internal static class CompilerDependency
{
    internal const string ArchiveSha256 = "8C50A52467826167E0DBE99936140C52D62272BFC5849FE2D6587D050C8C5D29";
    internal static (string Compiler,string Builtins) Verify(string directory)
    {
        string archivePath = Path.Combine(directory,"nvrtc-13.0.88.zip");
        using (var file = File.OpenRead(archivePath))
            if (file.Length != 318076161 || Convert.ToHexString(SHA256.HashData(file)) != ArchiveSha256)
                throw new InvalidOperationException("Unverified pinned NVRTC archive.");
        using var archive = ZipFile.OpenRead(archivePath);
        var paths = new List<string>();
        foreach (string name in new[] {"nvrtc64_130_0.dll","nvrtc-builtins64_130.dll"})
        {
            var entry = archive.Entries.Single(e => Path.GetFileName(e.FullName) == name);
            string path = Path.GetFullPath(Path.Combine(directory,"unpacked",entry.FullName));
            if (!path.StartsWith(Path.GetFullPath(directory)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Compiler path escapes its package.");
            using var original = entry.Open(); using var local = File.OpenRead(path);
            if (!SHA256.HashData(original).AsSpan().SequenceEqual(SHA256.HashData(local)))
                throw new InvalidOperationException("Compiler differs from pinned archive.");
            paths.Add(path);
        }
        return (paths[0],paths[1]);
    }
}

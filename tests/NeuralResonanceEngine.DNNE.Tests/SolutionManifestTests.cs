using System.Xml.Linq;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class SolutionManifestTests
{
    [Fact]
    public void Solution_Manifest_Includes_Every_Project_In_The_Repository()
    {
        var root = FindRepositoryRoot();
        var solution = XDocument.Load(Path.Combine(root.FullName, "NeuralResonanceEngine.DNNE.slnx"));
        var listed = solution
            .Descendants("Project")
            .Select(project => project.Attribute("Path")?.Value)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => Path.GetFullPath(Path.Combine(root.FullName, path!)))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var projects = Directory
            .EnumerateFiles(root.FullName, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var missing = projects.Where(path => !listed.Contains(path)).ToArray();
        Assert.True(missing.Length == 0, $"Projects missing from solution: {string.Join(", ", missing)}");
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "NeuralResonanceEngine.DNNE.slnx")))
            {
                return current;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the DNNE repository root.");
    }
}

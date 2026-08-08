using System.Text.Json;
using NeuralResonanceEngine.Shared.Contracts;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class DyadWireContractCompatibilityTests
{
    [Fact]
    public void ManifestMatchesDnneWireTypes()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(FindManifest()));
        var root = document.RootElement;

        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("dyad.wire-contract.v1", root.GetProperty("contractId").GetString());
        Assert.Equal(DyadLanguageContract.ProtocolVersion, root.GetProperty("languageProtocol").GetString());
        Assert.Equal(DyadPopulationLanguageTrainingContract.ProtocolVersion, root.GetProperty("adapterTrainingProtocol").GetString());
        Assert.Equal(DyadLanguageContract.MaxCandidateLength, root.GetProperty("maximumCandidateTextLength").GetInt32());
        AssertProperties<DyadAdapterTrainingGrounding>(root, "numericGroundingProperties");
        AssertProperties<DyadAdapterTrainingSource>(root, "numericSourceProperties");
        AssertProperties<DyadAdapterTrainingRecord>(root, "adapterTrainingRecordProperties");
        AssertProperties<DyadEntityGenerationResponse>(root, "dnneGenerationResponseProperties");
        AssertProperties<DyadLanguageCandidateResponse>(root, "dnneReviewProperties");
    }

    private static void AssertProperties<T>(JsonElement root, string manifestProperty)
    {
        var expected = root.GetProperty(manifestProperty)
            .EnumerateArray()
            .Select(property => property.GetString())
            .ToArray();
        var actual = typeof(T).GetProperties().Select(property => property.Name).ToArray();

        Assert.Equal(expected, actual);
    }

    private static string FindManifest()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName, "contracts", "dyad-wire-contract.v1.json");
            if (File.Exists(path))
            {
                return path;
            }
        }

        throw new FileNotFoundException("Unable to locate contracts/dyad-wire-contract.v1.json.");
    }
}

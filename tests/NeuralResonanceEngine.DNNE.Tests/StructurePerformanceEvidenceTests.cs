using NeuralResonanceEngine.Protocol;

namespace NeuralResonanceEngine.DNNE.Tests;

[Collection(EnvironmentVariableTestCollection.Name)]
public sealed class StructurePerformanceEvidenceTests
{
    [Fact]
    public void TimingRequiresFreshIsolatedStateWithoutTouchingExistingFiles()
    {
        var previousDirectory = Environment.GetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR");
        var previousInstance = Environment.GetEnvironmentVariable("SERVICE_INSTANCE");
        var path = Path.Combine(Path.GetTempPath(), "nre-performance-guards", Guid.NewGuid().ToString("N"));
        var profile = new StructureProfile(StructureId.TemporalAssociation, "Izhikevich", "STDP", "guard", new(0,0));
        try
        {
            Environment.SetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR", path);
            Environment.SetEnvironmentVariable("SERVICE_INSTANCE", "live-cortex");
            Assert.Throws<InvalidOperationException>(() => new StructureEngine(profile, null, diagnosticPerformance: true));
            Assert.False(Directory.Exists(path));
            Environment.SetEnvironmentVariable("SERVICE_INSTANCE", "isolated-performance-guard");
            Directory.CreateDirectory(path); File.WriteAllText(Path.Combine(path, "preserved.txt"), "preserve this evidence");
            Assert.Throws<InvalidOperationException>(() => new StructureEngine(profile, null, diagnosticPerformance: true));
            Assert.Equal("preserve this evidence", File.ReadAllText(Path.Combine(path, "preserved.txt")));
            Assert.Single(Directory.GetFiles(path));
        }
        finally
        {
            Environment.SetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR", previousDirectory);
            Environment.SetEnvironmentVariable("SERVICE_INSTANCE", previousInstance);
        }
    }

    [Fact]
    public void PublicHostConstructorDoesNotEnableTiming()
    {
        var previousDirectory = Environment.GetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR");
        var previousInstance = Environment.GetEnvironmentVariable("SERVICE_INSTANCE");
        try
        {
            Environment.SetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR", Path.Combine(Path.GetTempPath(), "nre-performance-guards", Guid.NewGuid().ToString("N")));
            Environment.SetEnvironmentVariable("SERVICE_INSTANCE", "isolated-performance-default");
            using var engine = new StructureEngine(new(StructureId.TemporalAssociation, "Izhikevich", "STDP", "guard", new(0,0)));
            Assert.Null(engine.DiagnosticPerformance);
        }
        finally
        {
            Environment.SetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR", previousDirectory);
            Environment.SetEnvironmentVariable("SERVICE_INSTANCE", previousInstance);
        }
    }
}

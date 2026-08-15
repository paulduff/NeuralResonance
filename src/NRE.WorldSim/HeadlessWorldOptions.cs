namespace NRE.WorldSim;

public sealed record HeadlessWorldOptions(
    Uri ControlEndpoint,
    int Seed = 317,
    TimeSpan? SimulationInterval = null,
    TimeSpan? FramePollInterval = null,
    TimeSpan? BodyFrameInterval = null,
    TimeSpan? VisionFrameInterval = null,
    TimeSpan? AudioFrameInterval = null,
    string? ReportDirectory = null,
    bool PredatorsEnabled = false)
{
    public TimeSpan EffectiveSimulationInterval => SimulationInterval ?? TimeSpan.FromMilliseconds(33);
    public TimeSpan EffectiveFramePollInterval => FramePollInterval ?? TimeSpan.FromMilliseconds(90);
    public TimeSpan EffectiveBodyFrameInterval => BodyFrameInterval ?? TimeSpan.FromMilliseconds(50);
    public TimeSpan EffectiveVisionFrameInterval => VisionFrameInterval ?? TimeSpan.FromMilliseconds(125);
    public TimeSpan EffectiveAudioFrameInterval => AudioFrameInterval ?? TimeSpan.FromMilliseconds(500);
    public string EffectiveReportDirectory => string.IsNullOrWhiteSpace(ReportDirectory)
        ? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NeuralResonanceEngine",
            "world-runs")
        : Path.GetFullPath(ReportDirectory);

    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(ControlEndpoint);
        if (!ControlEndpoint.IsAbsoluteUri || !ControlEndpoint.IsLoopback)
        {
            throw new ArgumentException("The headless world requires a loopback ControlProgram endpoint.", nameof(ControlEndpoint));
        }

        ValidateInterval(EffectiveSimulationInterval, nameof(SimulationInterval), 10, 1000);
        ValidateInterval(EffectiveFramePollInterval, nameof(FramePollInterval), 20, 5000);
        ValidateInterval(EffectiveBodyFrameInterval, nameof(BodyFrameInterval), 20, 10000);
        ValidateInterval(EffectiveVisionFrameInterval, nameof(VisionFrameInterval), 100, 10000);
        ValidateInterval(EffectiveAudioFrameInterval, nameof(AudioFrameInterval), 100, 10000);
    }

    private static void ValidateInterval(TimeSpan value, string name, int minimumMs, int maximumMs)
    {
        if (value < TimeSpan.FromMilliseconds(minimumMs) || value > TimeSpan.FromMilliseconds(maximumMs))
        {
            throw new ArgumentOutOfRangeException(name);
        }
    }
}

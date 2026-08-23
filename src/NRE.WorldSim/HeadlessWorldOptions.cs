namespace NRE.WorldSim;

public enum WorldDevelopmentStage
{
    HandSpace,
    NearFlat,
    NormalDistance,
    Terrain,
    Ecology
}

public sealed record HeadlessWorldOptions(
    Uri ControlEndpoint,
    int Seed = 317,
    TimeSpan? SimulationInterval = null,
    TimeSpan? FramePollInterval = null,
    TimeSpan? BodyFrameInterval = null,
    TimeSpan? VisionFrameInterval = null,
    TimeSpan? AudioFrameInterval = null,
    string? ReportDirectory = null,
    bool PredatorsEnabled = false,
    bool MotorTrainingMode = false,
    WorldDevelopmentStage DevelopmentStage = WorldDevelopmentStage.Terrain,
    TimeSpan? RollingReportInterval = null,
    TimeSpan? BrainFrameOverloadThreshold = null,
    int ConsecutiveBrainFrameOverloadLimit = 3)
{
    public TimeSpan EffectiveSimulationInterval => SimulationInterval ?? TimeSpan.FromMilliseconds(33);
    public TimeSpan EffectiveFramePollInterval => FramePollInterval ?? TimeSpan.FromMilliseconds(90);
    public TimeSpan EffectiveBodyFrameInterval => BodyFrameInterval ?? TimeSpan.FromMilliseconds(50);
    public TimeSpan EffectiveVisionFrameInterval => VisionFrameInterval ?? TimeSpan.FromMilliseconds(125);
    public TimeSpan EffectiveAudioFrameInterval => AudioFrameInterval ?? TimeSpan.FromMilliseconds(500);
    public TimeSpan EffectiveRollingReportInterval => RollingReportInterval ?? TimeSpan.FromSeconds(30);
    public TimeSpan EffectiveBrainFrameOverloadThreshold => BrainFrameOverloadThreshold ?? TimeSpan.FromSeconds(4);
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
        ValidateInterval(EffectiveRollingReportInterval, nameof(RollingReportInterval), 100, 300000);
        ValidateInterval(EffectiveBrainFrameOverloadThreshold, nameof(BrainFrameOverloadThreshold), 100, 30000);
        if (ConsecutiveBrainFrameOverloadLimit is < 2 or > 20)
        {
            throw new ArgumentOutOfRangeException(nameof(ConsecutiveBrainFrameOverloadLimit));
        }
        if (!Enum.IsDefined(DevelopmentStage))
        {
            throw new ArgumentOutOfRangeException(nameof(DevelopmentStage));
        }
    }

    private static void ValidateInterval(TimeSpan value, string name, int minimumMs, int maximumMs)
    {
        if (value < TimeSpan.FromMilliseconds(minimumMs) || value > TimeSpan.FromMilliseconds(maximumMs))
        {
            throw new ArgumentOutOfRangeException(name);
        }
    }
}

namespace NRE.SimAvatar;

public sealed record AvatarCochlearFrameDispatchResult(
    bool Accepted,
    bool DispatchDeferred,
    int GeneratedSpikes,
    int DeliveredSpikes,
    int TargetInstances,
    int FrequencyBands,
    int ActiveLeftBands,
    int ActiveRightBands,
    float RootMeanSquare,
    float PeakAmplitude,
    float MeanBandAmplitude,
    float MeanOnset);

namespace NRE.SimAvatar;

public sealed record AvatarRetinalFrameDispatchResult(
    bool Accepted,
    bool DispatchDeferred,
    bool BlockedByInputGate,
    int GeneratedSpikes,
    int DeliveredSpikes,
    int TargetInstances,
    int SampleColumns,
    int SampleRows,
    int OnChannelSpikes,
    int OffChannelSpikes,
    float MeanLuminance,
    float MeanTemporalChange);

namespace NRE.SimAvatar;

public sealed record AvatarPhysicalBodyDispatchResult(
    bool Accepted,
    bool DispatchDeferred,
    int GeneratedSpikes,
    int DeliveredSpikes,
    int TargetInstances,
    float LinearAccelerationMagnitude,
    float AngularSpeedMagnitude,
    float StoredEnergyReserve,
    float TissueIntegrity,
    float HomeostaticDeviation,
    int ActiveProprioceptivePopulations,
    int ActiveVestibularPopulations,
    int ActiveVisceralPopulations);

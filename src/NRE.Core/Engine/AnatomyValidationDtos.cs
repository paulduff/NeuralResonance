using System.Numerics;

namespace NRE.Core.Engine;

public sealed record AnatomyValidationReportDto(
    int Width,
    int Height,
    int Depth,
    bool IsValid,
    int PassedCount,
    int FailedCount,
    AnatomyInvariantResultDto[] Invariants,
    AnatomyRegionSummaryDto[] Regions);

public sealed record AnatomyInvariantResultDto(
    string Id,
    string Name,
    bool Passed,
    string Details);

public sealed record AnatomyRegionSummaryDto(
    string Hemisphere,
    byte RegionId,
    string Name,
    int VoxelCount,
    Vector3 CentroidNorm,
    Vector3 BoundsMinNorm,
    Vector3 BoundsMaxNorm);

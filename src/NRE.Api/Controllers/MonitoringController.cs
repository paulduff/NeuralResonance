using Microsoft.AspNetCore.Mvc;
using NRE.Core.Engine;

namespace NRE.Api.Controllers;

[ApiController]
[Route("api/monitor")]
public sealed class MonitoringController : ControllerBase
{
    private readonly NreEngine _engine;

    public MonitoringController(NreEngine engine)
    {
        _engine = engine;
    }

    [HttpGet("resonant-clusters")]
    public ActionResult<ResonantClustersDto> ResonantClusters()
        => _engine.GetResonantClusters();

    [HttpGet("thought-clusters")]
    public ActionResult<ThoughtClustersDto> ThoughtClusters()
        => _engine.GetThoughtClusters();

    [HttpGet("telemetry")]
    public ActionResult<TelemetrySnapshotDto> Telemetry()
        => _engine.GetTelemetry();

    [HttpGet("body")]
    public ActionResult<BodyStateDto> Body()
        => _engine.GetBodyState();
}

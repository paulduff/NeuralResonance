using Microsoft.AspNetCore.Mvc;
using NRE.Contracts.Voice;
using NRE.Core.Engine;
using NRE.Api.Services;

namespace NRE.Api.Controllers;

[ApiController]
[Route("api/engine")]
public sealed class EngineController : ControllerBase
{
    private readonly NreEngine _engine;
    private readonly FastFrameJsonCache _fastFrameJson;
    private readonly FastFrameBinaryCache _fastFrameBin;

    private const float DtSeconds = 0.016f;

    public EngineController(NreEngine engine, FastFrameJsonCache fastFrameJson, FastFrameBinaryCache fastFrameBin)
    {
        _engine = engine;
        _fastFrameJson = fastFrameJson;
        _fastFrameBin = fastFrameBin;
    }

    [HttpGet("status")]
    public ActionResult<EngineStatusDto> Status()
        => _engine.GetStatus(DtSeconds);

    [HttpPost("start")]
    public IActionResult Start()
    {
        _engine.Start();
        return Ok();
    }

    [HttpPost("stop")]
    public IActionResult Stop()
    {
        _engine.Stop();
        return Ok();
    }

    [HttpPost("neuromodulator")]
    public IActionResult SetNeuromodulator([FromQuery] string type, [FromQuery] float value)
    {
        _engine.SetNeuromodulator(type, value);
        return Ok();
    }

    [HttpPost("pons")]
    public IActionResult SetPons([FromQuery] float arousal01, [FromQuery] float stability01, [FromQuery] float resetPressure01, [FromQuery] float thetaHz)
    {
        _engine.SetPons(arousal01, stability01, resetPressure01, thetaHz);
        return Ok();
    }

    [HttpPost("inject")]
    public IActionResult Inject([FromBody] InjectRequestDto req)
    {
        _engine.Inject(req.Hemisphere, req.X, req.Y, req.Z, req.Intensity, req.DelayTicks);
        return Ok();
    }

    // === SUBSYSTEM ENDPOINTS ===
    
    [HttpPost("thalamus")]
    public IActionResult SetThalamus([FromQuery] float frequencyHz = 40f, [FromQuery] float bindingWindow = 0.35f, [FromQuery] float speedBoost = 2.0f)
    {
        _engine.Thalamus.Configure(frequencyHz, bindingWindow, speedBoost);
        return Ok();
    }
    
    [HttpPost("sleep/force")]
    public IActionResult ForceSleep([FromQuery] string phase)
    {
        if (Enum.TryParse<SleepPhase>(phase, ignoreCase: true, out var p))
        {
            _engine.Sleep.ForcePhase(p);
            return Ok();
        }
        return BadRequest($"Invalid phase: {phase}. Use Awake, Nrem, or Rem.");
    }
    
    [HttpPost("sleep/buildpressure")]
    public IActionResult BuildSleepPressure([FromQuery] float amount = 0.3f)
    {
        _engine.Sleep.BuildPressure(amount);
        return Ok($"Sleep pressure increased by {amount:0.00}");
    }
    
    [HttpPost("amygdala/salience")]
    public IActionResult SetRegionSalience([FromQuery] byte regionId, [FromQuery] float salience)
    {
        _engine.Amygdala.SetRegionSalience(regionId, salience);
        return Ok();
    }
    
    [HttpGet("hippocampus/episodes")]
    public ActionResult<EpisodeSummary[]> GetEpisodes()
        => _engine.Hippocampus.GetEpisodeSummaries();
    
    [HttpPost("hippocampus/capture")]
    public IActionResult CaptureEpisode([FromQuery] float salience = 1.0f)
    {
        // Manual episode capture would require access to current spikes
        // For now, just return OK as auto-capture handles this
        return Ok("Episodes are auto-captured when salience is high.");
    }
    
    [HttpPost("cerebellum/reset")]
    public IActionResult ResetCerebellum()
    {
        _engine.Cerebellum.Reset();
        return Ok();
    }

    [HttpGet("save")]
    public IActionResult SaveBrain()
    {
        var data = _engine.SaveState();
        return File(data, "application/octet-stream", $"brain_{DateTime.UtcNow:yyyyMMdd_HHmmss}.nre");
    }

    [HttpPost("load")]
    [RequestSizeLimit(50_000_000)] // 50MB max
    public async Task<IActionResult> LoadBrain()
    {
        using var ms = new MemoryStream();
        await Request.Body.CopyToAsync(ms);
        try
        {
            _engine.LoadState(ms.ToArray());
            return Ok("Brain state loaded successfully.");
        }
        catch (Exception ex)
        {
            return BadRequest($"Failed to load brain: {ex.Message}");
        }
    }
    
public sealed record VisualStimulusRequest(float Intensity01, float SpeedHz, float SpatialFreq, bool Enabled = true);

[HttpPost("visual")]
public IActionResult Visual([FromBody] VisualStimulusRequest req)
{
    _engine.SetVisualStimulus(req.Intensity01, req.SpeedHz, req.SpatialFreq, enabled: req.Enabled);
    return Ok();
}

/// <summary>
/// Pixel-level visual input: accepts raw 8-bit grayscale bytes in the request
/// body (row-major, length = w*h) and pushes them through the brain's V1 Gabor
/// hierarchy + retinotopic occipital injection via <see cref="NreEngine.SetVisualFrame"/>.
///
/// This is the entry point external imagers (world-sim avatar camera, real
/// webcam) use to drive the brain with actual pixels rather than pre-categorised
/// symbolic stimuli. Request body is bounded to 8 MB so a runaway frame size
/// can't exhaust memory.
/// </summary>
[HttpPost("visual-frame")]
public async Task<IActionResult> VisualFrame([FromQuery] int w, [FromQuery] int h, CancellationToken ct)
{
    if (w <= 0 || h <= 0)
    {
        return BadRequest("width and height must be positive");
    }
    long needed = (long)w * h;
    if (needed > 8 * 1024 * 1024)
    {
        return BadRequest("frame too large (max 8 MiB)");
    }
    if (Request.ContentLength is long declared && declared < needed)
    {
        return BadRequest($"Content-Length {declared} smaller than declared dimensions {w}x{h}={needed}");
    }

    // Read the raw bytes from the body.
    var buffer = new byte[needed];
    int read = 0;
    while (read < buffer.Length)
    {
        int n = await Request.Body.ReadAsync(buffer.AsMemory(read, buffer.Length - read), ct);
        if (n == 0) break;
        read += n;
    }
    if (read < buffer.Length)
    {
        return BadRequest($"body had {read} bytes; expected {buffer.Length}");
    }

    try
    {
        _engine.SetVisualFrame(buffer.AsSpan(0, (int)needed), w, h);
        return Ok();
    }
    catch (ArgumentException ex)
    {
        return BadRequest(ex.Message);
    }
}

/// <summary>
/// Stop driving the brain from an external frame buffer. Reverts the visual
/// path to the synthetic-grating stimulus (or no input if that is disabled).
/// </summary>
[HttpPost("visual-frame/clear")]
public IActionResult VisualFrameClear()
{
    _engine.ClearVisualFrame();
    return Ok();
}

public sealed record AuditoryStimulusRequest(float Intensity01, float ToneHz, bool Enabled = true);

[HttpPost("auditory")]
public IActionResult Auditory([FromBody] AuditoryStimulusRequest req)
{
    _engine.SetAuditoryStimulus(req.Intensity01, req.ToneHz, enabled: req.Enabled);
    return Ok();
}

public sealed record SensoryStimulusRequest(VisualStimulusRequest? Visual, AuditoryStimulusRequest? Auditory);

/// <summary>
/// Combined sensory endpoint (preferred): reduces HTTP churn by sending webcam+mic features together.
/// </summary>
[HttpPost("sensory")]
public IActionResult Sensory([FromBody] SensoryStimulusRequest req)
{
    if (req.Visual is not null)
        _engine.SetVisualStimulus(req.Visual.Intensity01, req.Visual.SpeedHz, req.Visual.SpatialFreq, enabled: req.Visual.Enabled);

    if (req.Auditory is not null)
        _engine.SetAuditoryStimulus(req.Auditory.Intensity01, req.Auditory.ToneHz, enabled: req.Auditory.Enabled);

    return Ok();
}


[HttpGet("anatomy/validate")]
public ActionResult<AnatomyValidationReportDto> ValidateAnatomy()
    => _engine.GetAnatomyValidationReport();

[HttpGet("layout")]
public ActionResult<PackedPoints> Layout()
    => _engine.GetLayoutPoints();

[HttpGet("connections")]
public ActionResult<PackedLines> Connections([FromQuery] int maxEdges = 12000)
    => _engine.GetConnectionLines(maxEdges);


    [HttpGet("framefast")]
    public IActionResult FrameFast()
    {
        // PERF: serve cached serialized JSON (serialize once per published frame).
        var json = _fastFrameJson.GetFastFrameJson();
        return File(json, "application/json");
    }


    [HttpGet("framefast.bin")]
    public IActionResult FrameFastBinary()
    {
        // PERF: serve cached binary bytes (pack once per published frame).
        var bytes = _fastFrameBin.GetFastFrameBytes();
        return File(bytes, "application/octet-stream");
    }


    // === Voice (browser TTS via Web Speech API) ===
    /// <summary>
    /// Dequeues any pending utterances produced by the engine.
    /// Intended for client-side speech synthesis.
    /// </summary>
    [HttpGet("voice")]
    public ActionResult<VoiceUtteranceDto[]> Voice([FromQuery] int max = 8)
    {
        var items = _engine.DequeueVoice(max);
        var dto = new VoiceUtteranceDto[items.Length];
        for (int i = 0; i < items.Length; i++)
        {
            var v = items[i];
            dto[i] = new VoiceUtteranceDto(v.StepIndex, v.Text, v.Rate, v.Pitch, v.Volume, v.Gloss, v.Phonemes);
}
        return dto;
    }

    /// <summary>
    /// Manual say endpoint (useful for testing; UI controls can be added later).
    /// </summary>
    [HttpPost("voice/say")]
    public IActionResult Say([FromQuery] string text, [FromQuery] float urgency01 = 0.35f)
    {
        _engine.EnqueueVoice(text, urgency01);
        return Ok();
    }
    /// <summary>
    /// Close the loop: when the browser speaks an utterance, it POSTs it back here so A1 receives a reafferent drive.
    /// This is a lightweight "hears itself" mechanism (no audio stream required).
    /// </summary>
    [HttpPost("voice/reafferent")]
    public IActionResult VoiceReafferent([FromBody] VoiceReafferenceRequest req)
    {
        var text = req.Text ?? string.Empty;

        // Map speech synthesis parameters into an approximate auditory drive.
        float vol = Math.Clamp(req.Volume, 0f, 1f);
        float intensity01 = Math.Clamp(0.10f + 0.55f * vol, 0f, 1f);

        float pitch = Math.Clamp(req.Pitch, 0f, 2f);
        float baseHz = 220f * MathF.Pow(2f, pitch - 1f);

        float jitter = 1f + TextToneJitter01(text);
        float toneHz = Math.Clamp(baseHz * jitter, 80f, 800f);

        _engine.RegisterSelfVoice(text, intensity01, toneHz, req.HoldSeconds, req.Phonemes, req.Gloss);
        return Ok();
    }

    /// <summary>Adjust vocal motor thresholds at runtime.</summary>
    [HttpPost("voice/settings")]
    public IActionResult VoiceSettings([FromQuery] float? bgConfidence = null, [FromQuery] int? cooldownSteps = null)
    {
        if (bgConfidence.HasValue)
            _engine.Vocal.BgConfidenceThreshold = Math.Clamp(bgConfidence.Value, 0.05f, 0.95f);
        if (cooldownSteps.HasValue)
            _engine.Vocal.CooldownSteps = Math.Clamp(cooldownSteps.Value, 10, 600);
        return Ok();
    }

    /// <summary>Adjust motor output gain.</summary>
    [HttpPost("motor/gain")]
    public IActionResult MotorGain([FromQuery] float gain)
    {
        _engine.MotorGain = Math.Clamp(gain, 0.5f, 10f);
        return Ok();
    }

    // Stable (non-randomized) text hash -> small +/- jitter for tone so different utterances are distinguishable.
    private static float TextToneJitter01(string s)
    {
        unchecked
        {
            // FNV-1a 32-bit
            uint hash = 2166136261u;
            for (int i = 0; i < s.Length; i++)
            {
                hash ^= s[i];
                hash *= 16777619u;
            }

            // Map to [-0.12 .. +0.12]
            float u = (hash % 2001) / 1000f; // 0..2.001
            float signed = u - 1f;           // -1..+1
            return signed * 0.12f;
        }
    }

// Back-compat endpoint: returns a combined payload from cached fast frame.
    [HttpGet("frame")]
    public ActionResult<RenderFrameDto> Frame()
    {
        var fast = _engine.GetPublishedFastFrame();
        var empty = new PackedHeatmap(0, 0, 0, Array.Empty<byte>());

        return new RenderFrameDto(
            fast.StepIndex,
            fast.Spikes,
            empty,
            empty,
            empty,
            empty,
            fast.CrossModuleTraffic,
            fast.CallosalTraffic01,
            fast.SleepPhase,
            fast.ThalamicPulseActive);
    }

    // === Peer Bridge (inter-instance communication) ===

    /// <summary>Get this instance's peer bridge info.</summary>
    [HttpGet("peer/info")]
    public ActionResult<PeerBridgeStatusDto> PeerInfo()
    {
        return Ok(_engine.CreatePeerBridgeStatus());
    }

    /// <summary>Set this instance's name.</summary>
    [HttpPost("peer/name")]
    public IActionResult PeerName([FromQuery] string name)
    {
        _engine.Peer.InstanceName = name;
        return Ok();
    }

    /// <summary>Connect to another instance by ID.</summary>
    [HttpPost("peer/connect")]
    public IActionResult PeerConnect([FromQuery] string peerId)
    {
        bool ok = _engine.Peer.ConnectTo(peerId);
        return ok ? Ok() : NotFound($"Peer '{peerId}' not found in hub");
    }

    /// <summary>Connect to all available instances.</summary>
    [HttpPost("peer/connect-all")]
    public ActionResult<int> PeerConnectAll()
    {
        int count = _engine.Peer.ConnectToAll();
        return Ok(count);
    }

    /// <summary>Disconnect from a peer.</summary>
    [HttpPost("peer/disconnect")]
    public IActionResult PeerDisconnect([FromQuery] string peerId)
    {
        _engine.Peer.Disconnect(peerId);
        return Ok();
    }

    /// <summary>List all instances in the hub (for discovery).</summary>
    [HttpGet("peer/hub")]
    public ActionResult<string[]> PeerHub()
    {
        return Ok(PeerBridge.GetAllInstances());
    }

    /// <summary>Send a message to connected peers (text -> vocal tract -> peer bridge).</summary>
    [HttpPost("peer/say")]
    public IActionResult PeerSay([FromQuery] string text, [FromQuery] float rate = 1.0f)
    {
        if (string.IsNullOrWhiteSpace(text)) return BadRequest("Empty text");
        _engine.SendSpeechToPeers(text, rate);
        return Ok();
    }

    // === Vocal Tract (articulatory synthesis) ===

    /// <summary>Get current vocal tract articulatory state.</summary>
    [HttpGet("vocaltract/state")]
    public ActionResult<VocalTractStatusDto> VocalTractState()
    {
        return Ok(_engine.CreateVocalTractStatus());
    }

    /// <summary>Set base pitch for the vocal tract.</summary>
    [HttpPost("vocaltract/pitch")]
    public IActionResult VocalTractPitch([FromQuery] float hz)
    {
        _engine.VocalTract.BasePitchHz = Math.Clamp(hz, 50f, 400f);
        return Ok();
    }

    /// <summary>Speak a word through the vocal tract (for testing).</summary>
    [HttpPost("vocaltract/speak")]
    public ActionResult<string[]> VocalTractSpeak([FromQuery] string word)
    {
        var phonemes = DiphthongVocalTract.GraphemeToPhoneme(word);
        _engine.VocalTract.EnqueueWord(word);
        return Ok(phonemes.ToArray());
    }
    
    [HttpGet("timing")]
    public IActionResult GetTiming() => Ok(new { avgStepMs = _engine.AvgStepMs, breakdown = _engine.StepTimingBreakdown, hemi = _engine.HemiTimingDetail });

}


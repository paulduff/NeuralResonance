namespace NRE.Core.Engine;

/// <summary>
/// Canonical subcortical modulation output from Pons.
/// This replaces "IdleDrive" by biasing excitability + rhythm + reset pressure.
/// </summary>
public readonly record struct ModulationPacket(
    float Arousal01,
    float Stability01,
    float ResetPressure01,
    float ThetaHz);

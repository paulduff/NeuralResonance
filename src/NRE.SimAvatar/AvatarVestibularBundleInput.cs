using System.Text.Json;
using NeuralResonanceEngine.Shared.Contracts;

namespace NRE.SimAvatar;

public enum AvatarVestibularBundleReceptor
{
    LeftHorizontal, RightHorizontal, LeftAnterior, RightAnterior, LeftPosterior, RightPosterior, Otolith
}

// A declared linear mechanical hypothesis, NOT a calibrated organ. There are
// deliberately no default gains or conversion from normalized activation.
// ParameterSource identifies the supplied evidence/diagnostic fixture; a string
// or fingerprint does not establish the evidence's physiological validity.
public sealed record AvatarVestibularBundleProjection(Guid HairCellId, string ParameterSource,
    AvatarVestibularBundleReceptor Receptor, double CanalNanometersPerRadianPerSecond,
    AvatarVestibularVector OtolithNanometersPerMeterPerSecondSquared, double OffsetNanometers)
{
    public const string Version = "declared-linear-vestibular-bundle-projection-v1";

    public void Validate()
    {
        if (HairCellId == Guid.Empty || string.IsNullOrWhiteSpace(ParameterSource) || ParameterSource != ParameterSource.Trim() ||
            !Enum.IsDefined(Receptor) || !double.IsFinite(CanalNanometersPerRadianPerSecond) ||
            !double.IsFinite(OffsetNanometers) || OtolithNanometersPerMeterPerSecondSquared is not { } v ||
            !double.IsFinite(v.Right) || !double.IsFinite(v.Down) || !double.IsFinite(v.Forward))
            throw new ArgumentException("Declare the hair cell, parameter provenance, receptor and finite dimensional coefficients.");
        if (Receptor == AvatarVestibularBundleReceptor.Otolith ? CanalNanometersPerRadianPerSecond != 0 :
            v.Right != 0 || v.Down != 0 || v.Forward != 0)
            throw new ArgumentException("Do not mix canal velocity and otolith acceleration coefficients in one receptor.");
    }

    public string Fingerprint
    {
        get
        {
            Validate();
            return PhysicalSampleProtocol.Digest(JsonSerializer.Serialize(new
            {
                Version, HairCellId, ParameterSource, Receptor, CanalNanometersPerRadianPerSecond,
                OtolithNanometersPerMeterPerSecondSquared, OffsetNanometers
            }));
        }
    }

    internal double Project(AvatarVestibularMechanicalState state)
    {
        var c = state.Cupula;
        double value = Receptor switch
        {
            AvatarVestibularBundleReceptor.LeftHorizontal => c.LeftHorizontal,
            AvatarVestibularBundleReceptor.RightHorizontal => c.RightHorizontal,
            AvatarVestibularBundleReceptor.LeftAnterior => c.LeftAnterior,
            AvatarVestibularBundleReceptor.RightAnterior => c.RightAnterior,
            AvatarVestibularBundleReceptor.LeftPosterior => c.LeftPosterior,
            AvatarVestibularBundleReceptor.RightPosterior => c.RightPosterior,
            AvatarVestibularBundleReceptor.Otolith => 0,
            _ => throw new ArgumentOutOfRangeException(nameof(Receptor))
        };
        var a = state.Otolith; var g = OtolithNanometersPerMeterPerSecondSquared;
        double displacement = Receptor == AvatarVestibularBundleReceptor.Otolith
            ? OffsetNanometers + a.Right * g.Right + a.Down * g.Down + a.Forward * g.Forward
            : OffsetNanometers + value * CanalNanometersPerRadianPerSecond;
        if (!double.IsFinite(displacement))
            throw new ArithmeticException("Nonfinite bundle projection; retain the accepted mechanics without clipping or rescaling.");
        return displacement;
    }
}

public sealed record AvatarVestibularBundleSample(AvatarVestibularKinematics Input,
    AvatarVestibularMechanicalReceipt Mechanics, double DisplacementNanometers);

// Both original endpoints are retained. Only StartSample's displacement drives
// this completed interval. EndSample becomes the following interval's input.
// This second zero-order hold is a declared approximation, not continuous motion.
public sealed record AvatarVestibularBundleInterval(string OwnerFingerprint,
    AvatarVestibularBundleSample StartSample, AvatarVestibularBundleSample EndSample, string Fingerprint)
{
    public double StartNeuralMs => StartSample.Mechanics.State.NeuralTimeMs!.Value;
    public double EndNeuralMs => EndSample.Mechanics.State.NeuralTimeMs!.Value;
    public double HeldDisplacementNanometers => StartSample.DisplacementNanometers;

    public double HeldInputFor(double startNeuralMs, double endNeuralMs)
    {
        if (!double.IsFinite(startNeuralMs) || !double.IsFinite(endNeuralMs) ||
            endNeuralMs <= startNeuralMs || startNeuralMs < StartNeuralMs || endNeuralMs > EndNeuralMs)
            throw new ArgumentException("A sensory step must fit completely inside the released physical interval; no future or missing sample fill.");
        return HeldDisplacementNanometers;
    }
}

public sealed record AvatarVestibularBundleReceipt(AvatarVestibularBundleSample Sample,
    AvatarVestibularBundleInterval? CompletedInterval);
public sealed record AvatarVestibularBundleFailure(AvatarVestibularKinematics AttemptedInput, string Reason);
public sealed record AvatarVestibularBundleState(string Fingerprint, AvatarVestibularMechanicalState Mechanics,
    AvatarVestibularBundleReceipt? LastReceipt, AvatarVestibularBundleInterval? PendingInterval,
    AvatarVestibularBundleFailure? Failure);

// Isolated body-to-receptor boundary. Owns the existing mechanics, but changes
// none of its equations. No neuronal advancement, live selector or spike coding.
// One interval of backpressure prevents fast body delivery from erasing input.
public sealed class AvatarVestibularBundleInput
{
    public const string Version = "isolated-completed-vestibular-bundle-input-v1";
    private readonly int _owner = Environment.CurrentManagedThreadId;
    private readonly AvatarClockedVestibularMechanics _mechanics;
    private readonly AvatarVestibularBundleProjection _projection;
    private AvatarVestibularBundleReceipt? _receipt;
    private AvatarVestibularBundleInterval? _pending;
    private AvatarVestibularBundleFailure? _failure;
    private string? _acknowledged;
    public string Fingerprint { get; }

    public AvatarVestibularBundleInput(PhysicalSampleClock clock, AvatarVestibularBundleProjection projection,
        long? firstObservationStep = null)
    {
        ArgumentNullException.ThrowIfNull(projection); projection.Validate();
        _mechanics = new(clock, firstObservationStep); _projection = projection;
        Fingerprint = PhysicalSampleProtocol.Digest(JsonSerializer.Serialize(new
        {
            Version, Mechanics = _mechanics.Fingerprint, Projection = projection.Fingerprint,
            Input = "completed physical intervals; previous projected endpoint held; explicit initial state; no extrapolation"
        }));
    }

    public AvatarVestibularBundleState Capture()
    {
        RequireOwner(); return new(Fingerprint, _mechanics.Capture(), _receipt, _pending, _failure);
    }

    public AvatarVestibularBundleReceipt Consume(AvatarVestibularKinematics input)
    {
        RequireOwner(); ArgumentNullException.ThrowIfNull(input);
        if (_failure is not null) throw new InvalidOperationException("Faulted bundle epoch cannot resume or reset.");
        if (_receipt is not null && input.PhysicsStep == _receipt.Sample.Input.PhysicsStep)
        {
            if (PhysicalSampleProtocol.Digest(JsonSerializer.Serialize(input)) != _receipt.Sample.Mechanics.InputSha256)
                throw new ArgumentException("Conflicting physical input replay.");
            return _receipt;
        }
        if (_pending is not null)
            throw new InvalidOperationException("Consume and acknowledge the pending physical interval before accepting another endpoint.");
        var before = _mechanics.Capture();
        try
        {
            var mechanical = _mechanics.Consume(input);
            var sample = new AvatarVestibularBundleSample(input, mechanical, _projection.Project(mechanical.State));
            AvatarVestibularBundleInterval? interval = null;
            if (_receipt is not null)
            {
                var start = _receipt.Sample;
                string digest = PhysicalSampleProtocol.Digest(JsonSerializer.Serialize(new { Version, Fingerprint, Start = start, End = sample }));
                interval = new(Fingerprint, start, sample, digest);
            }
            // Initial observation has no elapsed interval and releases no input
            // before its actual availability. No reconstructed sensory history.
            _pending = interval;
            return _receipt = new(sample, interval);
        }
        catch (Exception error)
        {
            var after = _mechanics.Capture();
            if (after.Faulted || after.CompletedThroughStep != before.CompletedThroughStep)
                _failure = new(input, error.ToString());
            // Validation failures do not fault the owner. A failure after an
            // accepted mechanical step retains that physical prefix and faults.
            throw;
        }
    }

    public void AcknowledgeInterval(string fingerprint)
    {
        RequireOwner();
        if (_pending is null)
        {
            if (_acknowledged is not null && fingerprint == _acknowledged) return;
            throw new ArgumentException("No matching pending physical interval.");
        }
        if (fingerprint != _pending.Fingerprint) throw new ArgumentException("Acknowledgement belongs to another physical interval.");
        _acknowledged = fingerprint; _pending = null;
    }

    private void RequireOwner()
    {
        if (Environment.CurrentManagedThreadId != _owner)
            throw new InvalidOperationException("One physical owner publishes each bundle epoch.");
    }
}

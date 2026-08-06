namespace NRE.SimAvatar;

/// <summary>
/// Avatar-side nervous system: interprets brain dispatch spikes into body signals.
/// The DNN remains the brain; this object is the spinal/peripheral layer that keeps
/// motor drive, idle arousal, and tool/body intent inside the avatar.
/// </summary>
public sealed class AvatarNervousSystem
{
    private readonly AvatarNervousSystemOptions _options;

    public AvatarNervousSystem(AvatarNervousSystemOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public double LeftMotorDrive { get; private set; }

    public double RightMotorDrive { get; private set; }

    public int LastMotorDispatchCount { get; private set; }

    public int TicksWithoutMotorDispatch { get; private set; }

    public void ResetMotor()
    {
        LeftMotorDrive = 0.0;
        RightMotorDrive = 0.0;
        LastMotorDispatchCount = 0;
        TicksWithoutMotorDispatch = 0;
    }

    public void SetMotorDrive(double left, double right)
    {
        var min = _options.Kinematics.AllowSignedMotorDrive ? -_options.Kinematics.MaxMotorDrive : 0.0;
        LeftMotorDrive = Math.Clamp(left, min, _options.Kinematics.MaxMotorDrive);
        RightMotorDrive = Math.Clamp(right, min, _options.Kinematics.MaxMotorDrive);
    }

    public void AddMotorDrive(double leftDelta, double rightDelta)
        => SetMotorDrive(LeftMotorDrive + leftDelta, RightMotorDrive + rightDelta);

    public void ApplyDriveDecay(double? smoothingOverride = null)
    {
        var left = LeftMotorDrive;
        var right = RightMotorDrive;
        AvatarKinematics.ApplyDriveDecay(ref left, ref right, smoothingOverride ?? _options.DriveDecay);
        SetMotorDrive(left, right);
    }

    public (double ForwardSpeed, double TurnRateDeg) ComputeMotorOutput(
        double forwardGain = 1.0,
        double turnGain = 1.0,
        double forwardScale = 1.0)
        => AvatarKinematics.ComputeBrainMotorOutput(
            LeftMotorDrive,
            RightMotorDrive,
            _options.Kinematics,
            forwardGain: forwardGain,
            turnGain: turnGain,
            forwardScale: forwardScale);

    public AvatarNervousSystemSignal InterpretBrainSignals(
        IReadOnlyList<AvatarDispatchSpike> dispatches,
        AvatarNervousSystemBodyState body)
    {
        ArgumentNullException.ThrowIfNull(dispatches);

        var tool = InterpretToolSignal(dispatches);
        if (body.IsSleeping)
        {
            ResetMotor();
            return CurrentSignal(tool);
        }

        var left = LeftMotorDrive;
        var right = RightMotorDrive;
        var motorSummary = AvatarKinematics.IntegrateMotorSpikes(dispatches, ref left, ref right, _options.Kinematics);
        SetMotorDrive(left, right);

        if (motorSummary.MotorEvents > 0)
        {
            TicksWithoutMotorDispatch = 0;
        }
        else
        {
            TicksWithoutMotorDispatch++;
            ApplyIdleFallback(body);
        }

        LastMotorDispatchCount = motorSummary.MotorEvents;
        return CurrentSignal(tool);
    }

    private AvatarNervousSystemSignal CurrentSignal(AvatarToolSignal tool)
        => new(LeftMotorDrive, RightMotorDrive, LastMotorDispatchCount, TicksWithoutMotorDispatch, tool);

    private void ApplyIdleFallback(AvatarNervousSystemBodyState body)
    {
        // Brain-drive only: idle body state must not synthesize locomotion.
        // Movement is produced only by motor pathway dispatches.
    }

    public AvatarToolSignal InterpretToolSignal(IReadOnlyList<AvatarDispatchSpike> dispatches)
    {
        ArgumentNullException.ThrowIfNull(dispatches);

        if (dispatches.Count == 0)
        {
            return AvatarToolSignal.None;
        }

        var digStrength = 0.0;
        var buildStrength = 0.0;
        var digDirectionStrength = new double[5];
        var buildDirectionStrength = new double[5];

        for (var i = 0; i < dispatches.Count; i++)
        {
            var dispatch = dispatches[i];
            if (!AvatarMotorCatalog.IsMotorStructure(dispatch.SourceStructure))
            {
                continue;
            }

            var action = ResolveToolActionType(dispatch);
            if (action == AvatarToolAction.None)
            {
                continue;
            }

            var direction = ResolveToolDirection(dispatch);
            var directionIndex = (int)direction;
            var weight = ResolveToolActionWeight(dispatch.SourceStructure);
            if (action == AvatarToolAction.Dig)
            {
                digStrength += weight;
                digDirectionStrength[directionIndex] += weight;
            }
            else
            {
                buildStrength += weight;
                buildDirectionStrength[directionIndex] += weight;
            }
        }

        var strongestAction = digStrength >= buildStrength ? AvatarToolAction.Dig : AvatarToolAction.Build;
        var strongestStrength = Math.Max(digStrength, buildStrength);
        if (strongestStrength < _options.ToolActionThreshold)
        {
            return AvatarToolSignal.None;
        }

        var directionArray = strongestAction == AvatarToolAction.Dig ? digDirectionStrength : buildDirectionStrength;
        var strongestDirection = AvatarToolDirection.Forward;
        var strongestDirectionWeight = double.MinValue;
        for (var i = 0; i < directionArray.Length; i++)
        {
            if (directionArray[i] <= strongestDirectionWeight)
            {
                continue;
            }

            strongestDirectionWeight = directionArray[i];
            strongestDirection = (AvatarToolDirection)i;
        }

        return new AvatarToolSignal(strongestAction, strongestDirection, strongestStrength);
    }

    private static AvatarToolAction ResolveToolActionType(AvatarDispatchSpike dispatch)
    {
        if (string.IsNullOrWhiteSpace(dispatch.SourceNeuronId))
        {
            return AvatarToolAction.None;
        }

        var directive = dispatch.SourceNeuronId.Trim().ToLowerInvariant();
        if (directive.Contains("dig", StringComparison.Ordinal) ||
            directive.Contains("mine", StringComparison.Ordinal) ||
            directive.Contains("excavate", StringComparison.Ordinal))
        {
            return AvatarToolAction.Dig;
        }

        if (directive.Contains("build", StringComparison.Ordinal) ||
            directive.Contains("place", StringComparison.Ordinal) ||
            directive.Contains("construct", StringComparison.Ordinal))
        {
            return AvatarToolAction.Build;
        }

        // Generic locomotion spikes from SMA, premotor cortex, and M1 must not
        // become destructive tool actions merely because they share motor areas.
        if (!directive.Contains("tool", StringComparison.Ordinal))
        {
            return AvatarToolAction.None;
        }

        if (dispatch.SourceStructure.Equals("Sma", StringComparison.OrdinalIgnoreCase))
        {
            return AvatarToolAction.Build;
        }

        if (dispatch.SourceStructure.Equals("PremotorCortex", StringComparison.OrdinalIgnoreCase))
        {
            return AvatarToolAction.Dig;
        }

        if (dispatch.SourceStructure.Equals("MotorThalamus", StringComparison.OrdinalIgnoreCase) ||
            dispatch.SourceStructure.Equals("SpinalCordMotor", StringComparison.OrdinalIgnoreCase) ||
            dispatch.SourceStructure.Equals("ReticularFormation", StringComparison.OrdinalIgnoreCase))
        {
            return AvatarToolAction.None;
        }

        var hash = StableHash(dispatch.SourceNeuronId);
        return (hash & 1) == 0 ? AvatarToolAction.Dig : AvatarToolAction.Build;
    }

    private static AvatarToolDirection ResolveToolDirection(AvatarDispatchSpike dispatch)
    {
        if (!string.IsNullOrWhiteSpace(dispatch.SourceNeuronId))
        {
            return (AvatarToolDirection)(Math.Abs(StableHash(dispatch.SourceNeuronId)) % 5);
        }

        return dispatch.SourceHemisphere switch
        {
            "L" => AvatarToolDirection.Left,
            "R" => AvatarToolDirection.Right,
            _ => AvatarToolDirection.Forward
        };
    }

    private static double ResolveToolActionWeight(string structure)
    {
        if (structure.Equals("Sma", StringComparison.OrdinalIgnoreCase) ||
            structure.Equals("PremotorCortex", StringComparison.OrdinalIgnoreCase))
        {
            return 1.25;
        }

        if (structure.Equals("M1", StringComparison.OrdinalIgnoreCase))
        {
            return 1.05;
        }

        return 0.6;
    }

    private static int StableHash(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return 0;
        }

        unchecked
        {
            var hash = 23;
            for (var i = 0; i < value.Length; i++)
            {
                hash = (hash * 31) + value[i];
            }

            return hash;
        }
    }
}

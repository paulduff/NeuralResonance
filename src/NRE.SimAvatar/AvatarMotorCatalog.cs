namespace NRE.SimAvatar;

public static class AvatarMotorCatalog
{
    public static bool IsLocomotorPopulationEvent(AvatarDispatchSpike dispatch)
        => IsMotorStructure(dispatch.SourceStructure) &&
           !string.IsNullOrWhiteSpace(dispatch.SourceNeuronId) &&
           dispatch.SourceNeuronId.StartsWith("population:", StringComparison.OrdinalIgnoreCase);

    public static bool IsMotorStructure(string structure)
    {
        return structure.Equals("M1", StringComparison.OrdinalIgnoreCase)
               || structure.Equals("Sma", StringComparison.OrdinalIgnoreCase)
               || structure.Equals("PremotorCortex", StringComparison.OrdinalIgnoreCase)
               || structure.Equals("MotorThalamus", StringComparison.OrdinalIgnoreCase)
               || structure.Equals("SpinalCordMotor", StringComparison.OrdinalIgnoreCase)
               || structure.Equals("ReticularFormation", StringComparison.OrdinalIgnoreCase);
    }

    public static double ResolveMotorWeight(string structure)
    {
        if (structure.Equals("M1", StringComparison.OrdinalIgnoreCase))
        {
            return 1.8;
        }

        if (structure.Equals("Sma", StringComparison.OrdinalIgnoreCase))
        {
            return 1.3;
        }

        if (structure.Equals("PremotorCortex", StringComparison.OrdinalIgnoreCase))
        {
            return 1.0;
        }

        if (structure.Equals("MotorThalamus", StringComparison.OrdinalIgnoreCase))
        {
            return 0.8;
        }

        if (structure.Equals("SpinalCordMotor", StringComparison.OrdinalIgnoreCase))
        {
            return 2.2;
        }

        if (structure.Equals("ReticularFormation", StringComparison.OrdinalIgnoreCase))
        {
            return 0.9;
        }

        return 0.6;
    }

    public static AvatarMotorDriveSummary SummarizeMotorDrive(IReadOnlyList<AvatarDispatchSpike> dispatches)
    {
        double leftInput = 0.0;
        double rightInput = 0.0;
        var motorEvents = 0;

        for (var i = 0; i < dispatches.Count; i++)
        {
            var dispatch = dispatches[i];
            if (!IsMotorStructure(dispatch.SourceStructure))
            {
                continue;
            }

            var weight = ResolveMotorWeight(dispatch.SourceStructure);
            if (!TryApplyPopulationCode(dispatch, weight, ref leftInput, ref rightInput))
            {
                continue;
            }

            motorEvents++;
        }

        return new AvatarMotorDriveSummary(leftInput, rightInput, motorEvents, InPlaceTurnEvents: 0);
    }

    private static bool TryApplyPopulationCode(
        AvatarDispatchSpike dispatch,
        double weight,
        ref double leftInput,
        ref double rightInput)
    {
        if (!IsLocomotorPopulationEvent(dispatch))
        {
            return false;
        }

        var sign = dispatch.SourceNeuronId.Contains(":inhibitory:", StringComparison.OrdinalIgnoreCase)
            ? -1.0
            : 1.0;
        var contribution = weight * sign;
        switch (dispatch.SourceHemisphere)
        {
            case "L":
                leftInput += contribution;
                break;
            case "R":
                rightInput += contribution;
                break;
            default:
                leftInput += contribution * 0.5;
                rightInput += contribution * 0.5;
                break;
        }

        return true;
    }

}

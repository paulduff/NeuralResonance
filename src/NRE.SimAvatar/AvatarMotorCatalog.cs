namespace NRE.SimAvatar;

public static class AvatarMotorCatalog
{
    public static bool IsMotorStructure(string structure)
    {
        return structure.Equals("M1", StringComparison.OrdinalIgnoreCase)
               || structure.Equals("Sma", StringComparison.OrdinalIgnoreCase)
               || structure.Equals("PremotorCortex", StringComparison.OrdinalIgnoreCase)
               || structure.Equals("MotorThalamus", StringComparison.OrdinalIgnoreCase);
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

        return 0.6;
    }

    public static AvatarMotorDriveSummary SummarizeMotorDrive(IReadOnlyList<AvatarDispatchSpike> dispatches)
    {
        double leftInput = 0.0;
        double rightInput = 0.0;
        var motorEvents = 0;
        var inPlaceTurnEvents = 0;

        for (var i = 0; i < dispatches.Count; i++)
        {
            var dispatch = dispatches[i];
            if (!IsMotorStructure(dispatch.SourceStructure))
            {
                continue;
            }

            motorEvents++;
            var weight = ResolveMotorWeight(dispatch.SourceStructure);
            if (TryApplyExplicitMotorDirective(dispatch.SourceNeuronId, weight, ref leftInput, ref rightInput, ref inPlaceTurnEvents))
            {
                continue;
            }

            switch (dispatch.SourceHemisphere)
            {
                case "L":
                    leftInput += weight;
                    break;
                case "R":
                    rightInput += weight;
                    break;
                default:
                    leftInput += weight * 0.5;
                    rightInput += weight * 0.5;
                    break;
            }
        }

        return new AvatarMotorDriveSummary(leftInput, rightInput, motorEvents, inPlaceTurnEvents);
    }

    private static bool TryApplyExplicitMotorDirective(
        string sourceNeuronId,
        double weight,
        ref double leftInput,
        ref double rightInput,
        ref int inPlaceTurnEvents)
    {
        if (string.IsNullOrWhiteSpace(sourceNeuronId))
        {
            return false;
        }

        var normalized = sourceNeuronId.Trim().ToLowerInvariant();
        if (normalized.Contains("motor_stop", StringComparison.Ordinal))
        {
            leftInput -= weight * 10.5;
            rightInput -= weight * 10.5;
            return true;
        }

        if (normalized.Contains("motor_forward", StringComparison.Ordinal) ||
            normalized.Contains("motor_approach", StringComparison.Ordinal))
        {
            leftInput += weight * 1.35;
            rightInput += weight * 1.35;
            return true;
        }

        if (normalized.Contains("motor_seek", StringComparison.Ordinal))
        {
            leftInput += weight * 1.12;
            rightInput += weight * 1.12;
            return true;
        }

        if (normalized.Contains("motor_avoid_threat", StringComparison.Ordinal) ||
            normalized.Contains("motor_escape", StringComparison.Ordinal) ||
            normalized.Contains("motor_back", StringComparison.Ordinal) ||
            normalized.Contains("motor_retreat", StringComparison.Ordinal))
        {
            leftInput -= weight * 0.92;
            rightInput -= weight * 0.92;
            return true;
        }

        if (normalized.Contains("motor_about_face_left", StringComparison.Ordinal) ||
            normalized.Contains("motor_turn_around_left", StringComparison.Ordinal))
        {
            leftInput -= weight * 1.75;
            rightInput += weight * 1.75;
            inPlaceTurnEvents++;
            return true;
        }

        if (normalized.Contains("motor_about_face_right", StringComparison.Ordinal) ||
            normalized.Contains("motor_turn_around_right", StringComparison.Ordinal) ||
            normalized.Contains("motor_about_face", StringComparison.Ordinal) ||
            normalized.Contains("motor_turn_around", StringComparison.Ordinal))
        {
            leftInput += weight * 1.75;
            rightInput -= weight * 1.75;
            inPlaceTurnEvents++;
            return true;
        }

        if (normalized.Contains("motor_bear_left", StringComparison.Ordinal) ||
            normalized.Contains("motor_arc_left", StringComparison.Ordinal))
        {
            leftInput += weight * 0.16;
            rightInput += weight * 1.72;
            return true;
        }

        if (normalized.Contains("motor_bear_right", StringComparison.Ordinal) ||
            normalized.Contains("motor_arc_right", StringComparison.Ordinal))
        {
            leftInput += weight * 1.72;
            rightInput += weight * 0.16;
            return true;
        }

        if (normalized.Contains("motor_turn_left", StringComparison.Ordinal) ||
            normalized.Contains("motor_pivot_left", StringComparison.Ordinal) ||
            normalized.Contains("motor_left", StringComparison.Ordinal))
        {
            leftInput -= weight * 1.35;
            rightInput += weight * 1.35;
            inPlaceTurnEvents++;
            return true;
        }

        if (normalized.Contains("motor_turn_right", StringComparison.Ordinal) ||
            normalized.Contains("motor_pivot_right", StringComparison.Ordinal) ||
            normalized.Contains("motor_right", StringComparison.Ordinal))
        {
            leftInput += weight * 1.35;
            rightInput -= weight * 1.35;
            inPlaceTurnEvents++;
            return true;
        }

        return false;
    }
}

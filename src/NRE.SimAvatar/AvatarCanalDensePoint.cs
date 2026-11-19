namespace NRE.SimAvatar;

// Integral of the linear derivative interpolant through Radau nodes (1/3,1).
// This is an explicit quadratic dense approximation, not another integration
// or proof of accuracy between the previously qualified accepted endpoints.
public static class AvatarCanalDensePoint
{
    public const string Version="canal-radau-iia2-quadratic-dense-v1";
    public static AvatarCanalMechanicalPoint At(AvatarCanalMechanicalTransition transition,double fraction)
    {
        ArgumentNullException.ThrowIfNull(transition);
        if(!double.IsFinite(fraction)||fraction<0||fraction>1)throw new ArgumentOutOfRangeException(nameof(fraction));
        if(transition.Version!=AvatarCoupledCanalDynamics.Version)throw new ArgumentException("Unknown mechanical stage convention.");
        CleftNumbers.Positive(transition.StepSeconds);
        static AvatarCanalMechanicalPoint Valid(AvatarCanalMechanicalPoint point)
        { ArgumentNullException.ThrowIfNull(point);point.VolumeMeters3.Validate();point.FlowMeters3PerSecond.Validate();return point; }
        if(fraction==0)return Valid(transition.Before);
        if(fraction==1)return Valid(transition.After);
        if(fraction==1d/3)return Valid(transition.FirstStage.Point);
        double first=fraction*(1.5-.75*fraction),last=fraction*(.75*fraction-.5);
        AvatarCanalVector Evaluate(AvatarCanalVector start,AvatarCanalVector a,AvatarCanalVector b)
        {
            var x=start.ToArray();var y=a.ToArray();var z=b.ToArray();
            for(int i=0;i<6;i++){x[i]+=transition.StepSeconds*(first*y[i]+last*z[i]);CleftNumbers.Finite(x[i]);}
            return AvatarCanalVector.From(x);
        }
        return new(Evaluate(transition.Before.VolumeMeters3,transition.FirstStage.Point.FlowMeters3PerSecond,transition.LastStage.Point.FlowMeters3PerSecond),
            Evaluate(transition.Before.FlowMeters3PerSecond,transition.FirstStage.AccelerationMeters3PerSecond2,transition.LastStage.AccelerationMeters3PerSecond2));
    }
}

namespace NRE.SimAvatar;

public readonly record struct AvatarAfferentVoltageInterval(double StartMs,double EndMs,
    double StartMv,double EndMv,double StartSlopeMvPerMs,double EndSlopeMvPerMs);
public readonly record struct AvatarAfferentCrossingEstimate(double TimeMs,double Fraction,double ResidualMv,double MinimumScaledSlope);

// Observes completed membrane endpoints; never advances or resets a cell.
// An observed crossing alone does not qualify a physiological action potential.
public static class AvatarAfferentCrossing
{
    public const string Version="completed-afferent-voltage-cubic-v1";
    public const double ResidualBoundMv=1e-8;

    public static AvatarAfferentCrossingEstimate? Observe(AvatarAfferentVoltageInterval interval,double thresholdMv)
    {
        var p=interval;
        if(!double.IsFinite(p.StartMs)||!double.IsFinite(p.EndMs)||p.StartMs<0||p.EndMs<=p.StartMs||
            !double.IsFinite(p.StartMv)||!double.IsFinite(p.EndMv)||!double.IsFinite(p.StartSlopeMvPerMs)||
            !double.IsFinite(p.EndSlopeMvPerMs)||!double.IsFinite(thresholdMv))
            throw new ArgumentException("Require two completed finite endpoints and a declared threshold.");
        double h=p.EndMs-p.StartMs;
        double a=2*p.StartMv-2*p.EndMv+h*(p.StartSlopeMvPerMs+p.EndSlopeMvPerMs);
        double b=-3*p.StartMv+3*p.EndMv-h*(2*p.StartSlopeMvPerMs+p.EndSlopeMvPerMs);
        double c=h*p.StartSlopeMvPerMs,d=p.StartMv-thresholdMv;
        if(!double.IsFinite(a)||!double.IsFinite(b)||!double.IsFinite(c)||!double.IsFinite(d)||!double.IsFinite(p.EndMv-thresholdMv))
            throw new InvalidOperationException("Nonfinite observation polynomial; no event selected.");
        double Value(double s)=>((a*s+b)*s+c)*s+d;
        double Slope(double s)=>3*a*s*s+2*b*s+c;
        bool bracket=p.StartMv<thresholdMv && p.EndMv>=thresholdMv;
        if(!bracket)
        {
            // A same-side or downward pair can conceal an upward crossing.
            // Inspect all cubic monotonic pieces and refuse to synthesize one
            // without an actual upward endpoint bracket.
            var points=new List<double> { 0,1 };double aa=3*a,bb=2*b;
            if(!double.IsFinite(aa)||!double.IsFinite(bb))throw new InvalidOperationException("Unrepresentable observation derivative.");
            if(aa==0)
            {
                if(bb!=0)Add(-c/bb);
            }
            else
            {
                double scale=Math.Max(Math.Abs(aa),Math.Max(Math.Abs(bb),Math.Abs(c)));
                double x=aa/scale,y=bb/scale,z=c/scale,discriminant=y*y-4*x*z;
                if(discriminant==0)Add(-y/(2*x));
                else if(discriminant>0)
                {
                    double q=-.5*(y+Math.CopySign(Math.Sqrt(discriminant),y));
                    Add(q/x);if(q!=0)Add(z/q);
                }
            }
            points.Sort();
            for(int i=1;i<points.Count;i++)
            {
                double left=points[i-1]==0 ? p.StartMv-thresholdMv : Value(points[i-1]);
                double right=points[i]==1 ? p.EndMv-thresholdMv : Value(points[i]);
                if(!double.IsFinite(left)||!double.IsFinite(right))throw new InvalidOperationException("Nonfinite completed curve.");
                if(left<0 && right>=0)throw new InvalidOperationException("An interior upward crossing is unresolved; refine the trajectory, do not invent an event.");
            }
            return null;
            void Add(double s){if(double.IsNaN(s))throw new InvalidOperationException("Undefined curve extremum.");if(s>0 && s<1)points.Add(s);}
        }
        double minimum=Math.Min(Slope(0),Slope(1));
        if(a!=0)
        {
            double vertex=-b/(3*a);if(vertex>0 && vertex<1)minimum=Math.Min(minimum,Slope(vertex));
        }
        if(!double.IsFinite(minimum)||minimum<=0)
            throw new InvalidOperationException("Completed crossing curve is not strictly increasing; refine before selecting an event.");
        double lower=0,upper=1;
        for(int iteration=0;iteration<64;iteration++)
        {
            double middle=(lower+upper)*.5;if(middle==lower||middle==upper)break;
            if(Value(middle)<0)lower=middle;else upper=middle;
        }
        double fraction=(lower+upper)*.5,residual=Value(fraction),time=p.StartMs+h*fraction;
        if(!double.IsFinite(residual)||Math.Abs(residual)>ResidualBoundMv||!double.IsFinite(time)||time<p.StartMs||time>p.EndMs)
            throw new InvalidOperationException("Observation exceeded its residual or completed-time bracket.");
        return new(time,fraction,residual,minimum);
    }
}

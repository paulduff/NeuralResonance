namespace NRE.SimAvatar;

public sealed record AvatarCanalMechanicalPoint(AvatarCanalVector VolumeMeters3,AvatarCanalVector FlowMeters3PerSecond);
public sealed record AvatarCanalDerivative(AvatarCanalVector FlowMeters3PerSecond,AvatarCanalVector AccelerationMeters3PerSecond2,
    AvatarCanalVector PressurePascal,double EnergyJoules,double InputPowerWatts,double DampingPowerWatts);
public sealed record AvatarCanalRadauStage(AvatarCanalMechanicalPoint Point,AvatarCanalVector AccelerationMeters3PerSecond2);
public sealed record AvatarCanalMechanicalTransition(string Version,string Fingerprint,double StepSeconds,AvatarCanalPrimeAcceleration Input,
    AvatarCanalMechanicalPoint Before,AvatarCanalMechanicalPoint After,AvatarCanalRadauStage FirstStage,AvatarCanalRadauStage LastStage,
    double EnergyBeforeJoules,double EnergyAfterJoules,double InputWorkJoules,double PhysicalDampingJoules,double NumericalDampingJoules,
    double EnergyIdentityResidualJoules,double MaximumStageBackwardError);

// The mechanical organ is non-neuronal: it produces dimensional displacement
// upstream of hair-cell transduction. No firing map, neural adaptation, bundle
// deformation preset or live Avatar selector is implemented here.
public sealed class AvatarCoupledCanalDynamics
{
    public const string Version="coupled-canal-radau-iia2-v1";
    private static readonly double[,] A={ {5d/12,-1d/12},{3d/4,1d/4} };
    private static readonly double[] Nodes=[1d/3,1];
    private readonly AvatarCoupledCanalParameters _p;
    private readonly CanalLinearAlgebra.Factor _factor;
    public double StepSeconds { get; }
    public string Fingerprint { get; }
    public AvatarCoupledCanalParameters Parameters=>_p;

    public AvatarCoupledCanalDynamics(AvatarCoupledCanalParameters parameters,double stepSeconds)
    {
        ArgumentNullException.ThrowIfNull(parameters);CleftNumbers.Positive(stepSeconds);_p=parameters;StepSeconds=stepSeconds;
        double h=stepSeconds;var matrix=new double[12,12];
        for(int a=0;a<2;a++)for(int b=0;b<2;b++)
        {
            double square=0;for(int k=0;k<2;k++)square+=A[a,k]*A[k,b];
            for(int i=0;i<6;i++)for(int j=0;j<6;j++)matrix[6*a+i,6*b+j]=(a==b?_p.M(i,j):0)+h*A[a,b]*_p.C(i,j)+h*h*square*_p.K(i,j);
        }
        _factor=new(matrix);
        Fingerprint=CleftNumbers.Fingerprint(new { Version,Parameters=parameters.Fingerprint,StepSeconds,
            Method="two-stage order-three L-stable Radau IIA; held source-prime acceleration",
            StateUnits="m^3 and m^3/s",LinearSolve="row-scaled partial-pivot LU; componentwise backward error <= 1e-12",
            Energy="physical damping and algebraic numerical damping recorded separately",LiveSelectionChanged=false });
    }

    public AvatarCanalDerivative Evaluate(AvatarCanalMechanicalPoint point,AvatarCanalPrimeAcceleration input)
    {
        Validate(point);var q=point.VolumeMeters3.ToArray();var v=point.FlowMeters3PerSecond.ToArray();var force=_p.Force(input);var rhs=(double[])force.Clone();
        for(int i=0;i<6;i++)for(int j=0;j<6;j++)rhs[i]-=_p.C(i,j)*v[j]+_p.K(i,j)*q[j];
        var acceleration=CanalLinearAlgebra.CholeskySolve(_p.MassFactor,rhs);
        return new(point.FlowMeters3PerSecond,AvatarCanalVector.From(acceleration),AvatarCanalVector.From(force),_p.Energy(q,v),Dot(force,v),_p.Dissipation(v));
    }

    // Stateless transaction. The caller owns accepted time/inputs and commits
    // this complete result; a failed solve cannot mutate its accepted state.
    public AvatarCanalMechanicalTransition AdvanceHeld(AvatarCanalMechanicalPoint before,AvatarCanalPrimeAcceleration input)
    {
        Validate(before);double h=StepSeconds;var q=before.VolumeMeters3.ToArray();var v=before.FlowMeters3PerSecond.ToArray();
        var force=_p.Force(input);var rhs=new double[12];
        for(int stage=0;stage<2;stage++)for(int i=0;i<6;i++)
        {
            double r=force[i];for(int j=0;j<6;j++)r-=_p.C(i,j)*v[j]+_p.K(i,j)*(q[j]+h*Nodes[stage]*v[j]);rhs[stage*6+i]=r;
        }
        var solution=_factor.Solve(rhs);var accelerations=new[] {solution.Values[..6],solution.Values[6..]};
        var velocities=new[] {new double[6],new double[6]};var positions=new[] {new double[6],new double[6]};
        for(int stage=0;stage<2;stage++)for(int i=0;i<6;i++)
            velocities[stage][i]=v[i]+h*(A[stage,0]*accelerations[0][i]+A[stage,1]*accelerations[1][i]);
        for(int stage=0;stage<2;stage++)for(int i=0;i<6;i++)
            positions[stage][i]=q[i]+h*(A[stage,0]*velocities[0][i]+A[stage,1]*velocities[1][i]);
        var stage1=new AvatarCanalRadauStage(new(AvatarCanalVector.From(positions[0]),AvatarCanalVector.From(velocities[0])),AvatarCanalVector.From(accelerations[0]));
        var stage2=new AvatarCanalRadauStage(new(AvatarCanalVector.From(positions[1]),AvatarCanalVector.From(velocities[1])),AvatarCanalVector.From(accelerations[1]));
        Validate(stage1.Point);Validate(stage2.Point);stage1.AccelerationMeters3PerSecond2.Validate();stage2.AccelerationMeters3PerSecond2.Validate();
        double initialEnergy=_p.Energy(q,v),finalEnergy=_p.Energy(positions[1],velocities[1]);
        double work=h*(.75*Dot(force,velocities[0])+.25*Dot(force,velocities[1]));
        double physical=h*(.75*_p.Dissipation(velocities[0])+.25*_p.Dissipation(velocities[1]));
        var deltaV=new double[6];var deltaA=new double[6];
        for(int i=0;i<6;i++) { deltaV[i]=velocities[0][i]-velocities[1][i];deltaA[i]=accelerations[0][i]-accelerations[1][i]; }
        // For this Radau tableau, b_i*a_ij+b_j*a_ji-b_i*b_j =
        // (1/16)*[[1,-1],[-1,1]]. Its quadratic-energy loss is h²/32
        // times the H-norm of f(Y1)-f(Y2), independently of physical C damping.
        double numerical=h*h/32*(_p.SpringNorm(deltaV)+_p.MassNorm(deltaA));
        CleftNumbers.Finite(work);CleftNumbers.Nonnegative(physical);CleftNumbers.Nonnegative(numerical);
        double residual=initialEnergy+work-physical-numerical-finalEnergy;CleftNumbers.Finite(residual);
        return new(Version,Fingerprint,h,input,before,stage2.Point,stage1,stage2,initialEnergy,finalEnergy,work,physical,numerical,residual,solution.MaximumBackwardError);
    }
    private static double Dot(double[] a,double[] b) { double sum=0;for(int i=0;i<6;i++)sum+=a[i]*b[i];CleftNumbers.Finite(sum);return sum; }
    private static void Validate(AvatarCanalMechanicalPoint point)
    { ArgumentNullException.ThrowIfNull(point);point.VolumeMeters3.Validate();point.FlowMeters3PerSecond.Validate(); }
}

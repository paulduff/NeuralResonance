using System.Globalization;
using System.Text.Json;

namespace NRE.SimAvatar;

// Six generalized volumes: endolymph HC/AC/PC, then cupula HC/AC/PC.
// The same immutable vector type carries quantities whose units are declared
// by each property/argument. It never represents normalized neural activity.
public readonly record struct AvatarCanalVector(double EndolymphHC,double EndolymphAC,double EndolymphPC,
    double CupulaHC,double CupulaAC,double CupulaPC)
{
    public double[] ToArray()=>[EndolymphHC,EndolymphAC,EndolymphPC,CupulaHC,CupulaAC,CupulaPC];
    internal static AvatarCanalVector From(double[] x)=>new(x[0],x[1],x[2],x[3],x[4],x[5]);
    internal void Validate() { foreach(double x in ToArray())CleftNumbers.Finite(x); }
}

// Eq. 26's declared source-prime acceleration components, in rad/s^2.
// No implicit mapping from Avatar's right/up/forward or normalization of the
// rounded prime vectors. The source-to-skull conversion is a separate gate.
public readonly record struct AvatarCanalPrimeAcceleration(double HC,double AC,double PC)
{
    internal double[] Values() { CleftNumbers.Finite(HC);CleftNumbers.Finite(AC);CleftNumbers.Finite(PC);return [HC,AC,PC]; }
}

public sealed class AvatarCoupledCanalParameters
{
    public const string Version="six-volume-passive-canal-parameters-v1";
    private readonly double[] _mass,_damping,_stiffness,_forcing;
    internal readonly double[,] MassFactor,DampingFactor;
    public string Source { get; }
    public string SourceSha256 { get; }
    public string Fingerprint { get; }
    public IReadOnlyList<double> MassKgPerMeter4 { get; }
    public IReadOnlyList<double> DampingPascalSecondsPerMeter3 { get; }
    public IReadOnlyList<double> StiffnessPascalPerMeter3 { get; }
    public IReadOnlyList<double> PressurePascalPerRadianPerSecondSquared { get; }

    public AvatarCoupledCanalParameters(string source,string sourceSha256,IEnumerable<double> mass,
        IEnumerable<double> damping,IEnumerable<double> stiffness,IEnumerable<double> forcing)
    {
        if(string.IsNullOrWhiteSpace(source)||source!=source.Trim()||sourceSha256 is null||sourceSha256.Length!=64||
            sourceSha256.Any(c=>!Uri.IsHexDigit(c)))throw new ArgumentException("Explicit source identity and SHA-256 required.");
        Source=source;SourceSha256=sourceSha256.ToUpperInvariant();
        _mass=Copy(mass,36);_damping=Copy(damping,36);_stiffness=Copy(stiffness,36);_forcing=Copy(forcing,18);
        MassFactor=CanalLinearAlgebra.Cholesky(_mass);DampingFactor=CanalLinearAlgebra.Cholesky(_damping);
        // This model has cupula stiffness and free endolymph volume coordinates,
        // exactly as Eq. 25. Reject another mechanical topology explicitly.
        for(int i=0;i<6;i++)for(int j=0;j<6;j++)
            if(i==j&&i>=3)CleftNumbers.Positive(_stiffness[6*i+j]);
            else if(_stiffness[6*i+j]!=0)throw new ArgumentException("Require the declared cupula-only diagonal stiffness topology.");
        MassKgPerMeter4=Array.AsReadOnly(_mass);DampingPascalSecondsPerMeter3=Array.AsReadOnly(_damping);
        StiffnessPascalPerMeter3=Array.AsReadOnly(_stiffness);PressurePascalPerRadianPerSecondSquared=Array.AsReadOnly(_forcing);
        Fingerprint=CleftNumbers.Fingerprint(new { Version,Source,SourceSha256,Mass=_mass,Damping=_damping,Stiffness=_stiffness,Forcing=_forcing,
            StateOrder="endolymph HC AC PC; cupula HC AC PC",Volumes="m^3",Time="seconds",Pressure="Pa",Input="declared source-prime rad/s^2",
            FrameConversionQualified=false,LocalBundleMapQualified=false,AvatarAnatomyCalibrated=false });
    }
    private static double[] Copy(IEnumerable<double> values,int length)
    {
        ArgumentNullException.ThrowIfNull(values);var x=values.ToArray();
        if(x.Length!=length)throw new ArgumentException("Incorrect matrix dimensions.");
        foreach(double v in x)CleftNumbers.Finite(v);return x;
    }
    public static AvatarCoupledCanalParameters PublishedRightLabyrinth1()
    {
        using var stream=typeof(AvatarCoupledCanalParameters).Assembly.GetManifestResourceStream("NRE.SimAvatar.Ifediba2007.json")
            ??throw new InvalidOperationException("Missing pinned canal source catalog.");
        using var document=JsonDocument.Parse(stream);var r=document.RootElement;
        if(r.GetProperty("version").GetString()!="ifediba-2007-published-matrix-transcription-v1"||
            r.GetProperty("doi").GetString()!="10.1007/s10439-007-9277-y")throw new InvalidOperationException("Unexpected canal catalog.");
        static double[] Matrix(JsonElement x)=>x.GetProperty("siDecimalStrings").EnumerateArray()
            .SelectMany(row=>row.EnumerateArray().Select(v=>double.Parse(v.GetString()!,NumberStyles.Float,CultureInfo.InvariantCulture))).ToArray();
        var matrices=r.GetProperty("matrices");
        return new("Ifediba 2007, appendix 23-26; right human labyrinth 1; printed precision",r.GetProperty("retainedHtmlSha256").GetString()!,
            Matrix(matrices.GetProperty("mass")),Matrix(matrices.GetProperty("damping")),Matrix(matrices.GetProperty("stiffness")),Matrix(r.GetProperty("forcing")));
    }
    internal double M(int i,int j)=>_mass[6*i+j];
    internal double C(int i,int j)=>_damping[6*i+j];
    internal double K(int i,int j)=>_stiffness[6*i+j];
    internal double[] Force(AvatarCanalPrimeAcceleration input)
    {
        var a=input.Values();var f=new double[6];
        for(int i=0;i<6;i++)for(int j=0;j<3;j++)f[i]+=_forcing[3*i+j]*a[j];
        foreach(double x in f)CleftNumbers.Finite(x);return f;
    }
    internal double MassNorm(double[] v)=>CanalLinearAlgebra.FactorNorm(MassFactor,v);
    internal double Dissipation(double[] v)=>CanalLinearAlgebra.FactorNorm(DampingFactor,v);
    internal double SpringNorm(double[] q)
    {
        double result=0;for(int i=3;i<6;i++)result+=_stiffness[6*i+i]*q[i]*q[i];CleftNumbers.Nonnegative(result);return result;
    }
    internal double Energy(double[] q,double[] v)
    { double energy=.5*(MassNorm(v)+SpringNorm(q));CleftNumbers.Nonnegative(energy);return energy; }
}

internal static class CanalLinearAlgebra
{
    internal static double[,] Cholesky(double[] a)
    {
        var l=new double[6,6];
        for(int i=0;i<6;i++)for(int j=0;j<6;j++)if(a[6*i+j]!=a[6*j+i])throw new ArgumentException("Mechanical matrix must be symmetric.");
        for(int i=0;i<6;i++)for(int j=0;j<=i;j++)
        {
            double v=a[6*i+j];for(int k=0;k<j;k++)v-=l[i,k]*l[j,k];
            if(i==j) { if(!double.IsFinite(v)||v<=0)throw new ArgumentException("Mechanical matrix must be positive definite.");l[i,j]=Math.Sqrt(v); }
            else { l[i,j]=v/l[j,j];CleftNumbers.Finite(l[i,j]); }
        }
        return l;
    }
    internal static double FactorNorm(double[,] l,double[] x)
    {
        double result=0;for(int j=0;j<6;j++) { double y=0;for(int i=j;i<6;i++)y+=l[i,j]*x[i];result+=y*y; }
        CleftNumbers.Nonnegative(result);return result;
    }
    internal static double[] CholeskySolve(double[,] l,double[] b)
    {
        var x=(double[])b.Clone();
        for(int i=0;i<6;i++) { for(int j=0;j<i;j++)x[i]-=l[i,j]*x[j];x[i]/=l[i,i]; }
        for(int i=5;i>=0;i--) { for(int j=i+1;j<6;j++)x[i]-=l[j,i]*x[j];x[i]/=l[i,i]; }
        foreach(double v in x)CleftNumbers.Finite(v);return x;
    }

    // Row-scaled partial-pivot LU. Used only by this new mechanical candidate;
    // no existing neuronal or receptor solver is changed.
    internal sealed class Factor
    {
        private readonly double[,] _a,_lu;
        private readonly double[] _scale;
        private readonly int[] _pivots;
        private readonly int _n;
        internal Factor(double[,] matrix)
        {
            _n=matrix.GetLength(0);if(_n!=matrix.GetLength(1))throw new ArgumentException("Square matrix required.");
            _a=(double[,])matrix.Clone();_lu=new double[_n,_n];_scale=new double[_n];_pivots=new int[_n];
            for(int i=0;i<_n;i++)
            {
                for(int j=0;j<_n;j++) { CleftNumbers.Finite(_a[i,j]);_scale[i]=Math.Max(_scale[i],Math.Abs(_a[i,j])); }
                CleftNumbers.Positive(_scale[i]);for(int j=0;j<_n;j++)_lu[i,j]=_a[i,j]/_scale[i];
            }
            for(int k=0;k<_n;k++)
            {
                int pivot=k;for(int i=k+1;i<_n;i++)if(Math.Abs(_lu[i,k])>Math.Abs(_lu[pivot,k]))pivot=i;
                if(!double.IsFinite(_lu[pivot,k])||_lu[pivot,k]==0)throw new ArithmeticException("Singular canal stage system.");
                _pivots[k]=pivot;
                if(pivot!=k)for(int j=0;j<_n;j++)(_lu[k,j],_lu[pivot,j])=(_lu[pivot,j],_lu[k,j]);
                for(int i=k+1;i<_n;i++) { _lu[i,k]/=_lu[k,k];for(int j=k+1;j<_n;j++)_lu[i,j]-=_lu[i,k]*_lu[k,j]; }
            }
        }
        internal (double[] Values,double MaximumBackwardError) Solve(double[] rhs)
        {
            if(rhs.Length!=_n)throw new ArgumentException("Incorrect stage right-hand side.");
            var x=new double[_n];for(int i=0;i<_n;i++) { CleftNumbers.Finite(rhs[i]);x[i]=rhs[i]/_scale[i]; }
            for(int k=0;k<_n;k++)if(_pivots[k]!=k)(x[k],x[_pivots[k]])=(x[_pivots[k]],x[k]);
            for(int i=0;i<_n;i++)for(int j=0;j<i;j++)x[i]-=_lu[i,j]*x[j];
            for(int i=_n-1;i>=0;i--) { for(int j=i+1;j<_n;j++)x[i]-=_lu[i,j]*x[j];x[i]/=_lu[i,i];CleftNumbers.Finite(x[i]); }
            double maximum=0;
            for(int i=0;i<_n;i++)
            {
                double sum=0,denominator=Math.Abs(rhs[i]);
                for(int j=0;j<_n;j++) { double product=_a[i,j]*x[j];sum+=product;denominator+=Math.Abs(product); }
                CleftNumbers.Finite(sum);CleftNumbers.Finite(denominator);
                double error=denominator==0?Math.Abs(sum-rhs[i]):Math.Abs(sum-rhs[i])/denominator;
                CleftNumbers.Finite(error);maximum=Math.Max(maximum,error);
            }
            if(maximum>1e-12)throw new ArithmeticException("Canal stage backward-error limit exceeded.");
            return (x,maximum);
        }
    }
}

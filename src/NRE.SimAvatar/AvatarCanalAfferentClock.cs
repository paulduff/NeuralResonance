using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

namespace NRE.SimAvatar;

public sealed record AvatarCanalAfferentTime(long SourceStep,long MechanicalTick,double PhysicalMs,
    double MechanicalNeuralMs,double SourceNeuralMs,double RepresentationDifferenceMs);

// Integer indices establish interval identity. The original mechanical and
// neuronal double timestamp expressions remain intact; their rounding difference
// is recorded, never converted into a delay, resampling offset or clock reset.
public sealed class AvatarCanalAfferentClock
{
    public const string Version="integer-canal-afferent-clock-binding-v1";
    public PhysicalSampleClock PhysicalClock { get; }
    public int MechanicalTicksPerInterval { get; }
    public int SourceStepsPerInterval { get; }
    public int MechanicalTicksPerSourceStep { get; }
    public long FirstAngularStep { get; }
    public double SourceEpochMs { get; }
    public double SourceStepMs { get; }
    public string Fingerprint { get; }
    private readonly double _mechanicalTickMs;

    public AvatarCanalAfferentClock(PhysicalSampleClock physicalClock,int mechanicalTicksPerInterval,int sourceStepsPerInterval)
    {
        ArgumentNullException.ThrowIfNull(physicalClock);physicalClock.Validate();
        static bool Dyadic(int n)=>n>0&&(n&(n-1))==0;
        if(!Dyadic(mechanicalTicksPerInterval)||mechanicalTicksPerInterval>AvatarCanalStepSchedule.MaximumTicksPerInterval||
            !Dyadic(sourceStepsPerInterval)||sourceStepsPerInterval>mechanicalTicksPerInterval)
            throw new ArgumentException("Declare a dyadic sensory subdivision addressable on the mechanical clock.");
        PhysicalClock=physicalClock;MechanicalTicksPerInterval=mechanicalTicksPerInterval;SourceStepsPerInterval=sourceStepsPerInterval;
        MechanicalTicksPerSourceStep=mechanicalTicksPerInterval/sourceStepsPerInterval;
        FirstAngularStep=checked(physicalClock.InitialPhysicsStep+1);SourceEpochMs=physicalClock.AtStep(FirstAngularStep).NeuralMs;
        SourceStepMs=physicalClock.PhysicsStepMs/sourceStepsPerInterval;_mechanicalTickMs=physicalClock.PhysicsStepMs/mechanicalTicksPerInterval;
        if(!double.IsNormal(SourceStepMs)||!double.IsNormal(_mechanicalTickMs)||SourceStepMs*sourceStepsPerInterval!=physicalClock.PhysicsStepMs||
            _mechanicalTickMs*MechanicalTicksPerSourceStep!=SourceStepMs)
            throw new ArgumentException("Subdivision must preserve the declared physical duration exactly.");
        _=At(0);_=At(1);
        Fingerprint=CleftNumbers.Fingerprint(new {Version,Clock=physicalClock.Fingerprint,MechanicalTicksPerInterval,SourceStepsPerInterval,
            MechanicalTicksPerSourceStep,FirstAngularStep,SourceEpochMs,SourceStepMs,
            Identity="integer source step maps to integer mechanical tick; no timestamp-based lookup",
            Times="original mechanical and source expressions retained separately; difference is representation, not physiological delay"});
    }

    public void ValidateSource(ObservedAfferentSource source)
    {
        ArgumentNullException.ThrowIfNull(source);source.Validate();
        if(source.SessionId!=PhysicalClock.SessionId||source.EpochMs!=SourceEpochMs||source.StepMs!=SourceStepMs)
            throw new ArgumentException("Sensory source must retain the same session and explicit first-angular epoch/rate.");
    }

    public AvatarCanalAfferentTime At(long sourceStep)
    {
        if(sourceStep<0||sourceStep>ObservedAfferentSource.MaximumExactStep)throw new ArgumentOutOfRangeException(nameof(sourceStep));
        long tick=checked(MechanicalTicksPerInterval+sourceStep*(long)MechanicalTicksPerSourceStep);
        if(tick>PhysicalSampleClock.MaximumExactStep)throw new ArgumentOutOfRangeException(nameof(sourceStep),"Mechanical tick identity is no longer exact.");
        long physicalStep=checked(FirstAngularStep+sourceStep/SourceStepsPerInterval);
        _=PhysicalClock.AtStep(physicalStep);
        double elapsed=tick*_mechanicalTickMs;
        double physical=PhysicalClock.PhysicalEpochMs+elapsed,mechanical=PhysicalClock.NeuralEpochMs+elapsed;
        double source=SourceEpochMs+sourceStep*SourceStepMs;
        if(!double.IsFinite(physical)||!double.IsFinite(mechanical)||!double.IsFinite(source)||
            physical<=PhysicalClock.PhysicalEpochMs+(tick-1)*_mechanicalTickMs||
            mechanical<=PhysicalClock.NeuralEpochMs+(tick-1)*_mechanicalTickMs||
            (sourceStep>0&&source<=SourceEpochMs+(sourceStep-1)*SourceStepMs))
            throw new ArithmeticException("Adjacent clock coordinates are no longer distinguishable; no silent merging.");
        return new(sourceStep,tick,physical,mechanical,source,source-mechanical);
    }
}

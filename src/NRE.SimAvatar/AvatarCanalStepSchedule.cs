namespace NRE.SimAvatar;

// Numerical partition only. It changes no mechanical coefficient or input.
// Integer dyadic ticks retain the original physical endpoints exactly.
public sealed class AvatarCanalStepSchedule
{
    public const string Version="declared-dyadic-canal-substeps-v1";
    public const int MaximumTicksPerInterval=1<<24;
    public const int MaximumSubstepsPerInterval=65536;
    public string Name { get; }
    public int TicksPerInterval { get; }
    public IReadOnlyList<int> StepWidthsTicks { get; }
    public int Count=>StepWidthsTicks.Count;
    public string Fingerprint { get; }

    public AvatarCanalStepSchedule(string name,int ticksPerInterval,IEnumerable<int> stepWidthsTicks)
    {
        if(string.IsNullOrWhiteSpace(name)||name!=name.Trim())throw new ArgumentException("Name the numerical partition.");
        ArgumentNullException.ThrowIfNull(stepWidthsTicks);
        if(ticksPerInterval<1||ticksPerInterval>MaximumTicksPerInterval||(ticksPerInterval&(ticksPerInterval-1))!=0)
            throw new ArgumentException("A bounded power-of-two tick grid is required.");
        var widths=stepWidthsTicks.Take(MaximumSubstepsPerInterval+1).ToArray();
        if(widths.Length==0||widths.Length>MaximumSubstepsPerInterval||widths.Any(w=>w<1||w>ticksPerInterval||(w&(w-1))!=0)||
            widths.Sum(w=>(long)w)!=ticksPerInterval)
            throw new ArgumentException("Positive dyadic substeps must cover exactly one physical interval.");
        Name=name;TicksPerInterval=ticksPerInterval;StepWidthsTicks=Array.AsReadOnly(widths);
        Fingerprint=CleftNumbers.Fingerprint(new {Version,Name,TicksPerInterval,StepWidthsTicks,
            Meaning="prospective numerical partition; no changed biology, skipped elapsed time or input smoothing"});
    }

    public static AvatarCanalStepSchedule BoundaryRefined(int refinement)
    {
        if(refinement<1||refinement>128||(refinement&(refinement-1))!=0)throw new ArgumentException("Bounded dyadic refinement required.");
        var widths=new List<int>();
        for(int width=1;width<=1024;width*=2)
            for(int i=0;i<32*refinement;i++)widths.Add(width);
        for(int i=0;i<refinement;i++)widths.Add(32);
        return new("32 substeps per dyadic band; explicit closing remainder",65536*refinement,widths);
    }
}

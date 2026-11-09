using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

// Reproduce only the scheduler arithmetic. No cell, channel RHS, learning rule,
// body, GPU or saved brain is constructed or advanced by this diagnostic.
internal static class ClockAudit
{
    private const int ScalePower=80;
    private static readonly double[] Boundaries=[1000,1002,1800,2200,3000,6000];

    private static Int128 Units(double value)
    {
        if(!double.IsFinite(value) || value<0 || value>6000)throw new ArgumentOutOfRangeException(nameof(value));
        ulong bits=(ulong)BitConverter.DoubleToInt64Bits(value);
        int exponent=(int)((bits>>52)&2047);ulong mantissa=bits&0x000fffffffffffffUL;
        if(exponent==0 && mantissa==0)return 0;
        if(exponent==0)throw new InvalidOperationException("Subnormal duration outside declared exact-unit domain.");
        mantissa|=1UL<<52;int shift=exponent-1023-52+ScalePower;
        if(shift>=0)return (Int128)mantissa<<shift;
        if(-shift>=64 || (mantissa&((1UL<<(-shift))-1))!=0)
            throw new InvalidOperationException("Duration not exactly representable in declared audit units.");
        return (Int128)(mantissa>>(-shift));
    }

    private static double BoundaryDuration(double from,double until,double maximum)
    {
        double end=Math.Min(until,from+maximum);
        // Rounding the absolute endpoint upward can otherwise exceed the
        // declared maximum. Retain the adjacent lower endpoint explicitly.
        if(end-from>maximum)end=Math.BitDecrement(end);
        double duration=end-from;
        if(!(end>from) || duration<=0 || duration>maximum || from+duration!=end)
            throw new InvalidOperationException("Boundary cannot be represented at this clock resolution.");
        return duration;
    }

    internal static void Run(string output)
    {
        if(!Path.IsPathFullyQualified(output) || Directory.Exists(output))throw new ArgumentException("New absolute output directory required.");
        CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;Directory.CreateDirectory(output);
        var json=new JsonSerializerOptions(JsonSerializerDefaults.Web){WriteIndented=true};
        void Save(string name,object value)=>File.WriteAllText(Path.Combine(output,name),JsonSerializer.Serialize(value,json));
        var cases=new List<object>();
        Save("protocol.json",new {version="motor-clock-arithmetic-v1",scalePower=ScalePower,durationMs=6000,sampleMs=.1,
            stepsMs=new[]{.0125,.00625,.003125},modes=new[]{"original","endpoint-difference"},boundariesMs=Boundaries,
            modelExecuted=false,causalCalciumExplanationEstablished=false,originalBoundsChanged=false});
        try
        {
            foreach(double maximum in new[]{.0125,.00625,.003125})foreach(string mode in new[]{"original","endpoint-difference"})
            {
                string key=mode+"-h"+maximum.ToString("R");Console.WriteLine(key);
                string path=Path.Combine(output,key+".csv");
                long calls=0,roundedClockAdditions=0;int segment=0;double time=0;
                Int128 supplied=0,maxSampleError=0,maxClockRounding=0;double worstSample=0;
                using(var writer=new StreamWriter(path))
                {
                    writer.WriteLine("timeMs,suppliedUnits,clockUnits,errorUnits,calls,roundedAdditions");
                    writer.WriteLine("0,0,0,0,0,0");
                    for(int sample=1;sample<=60000;++sample)
                    {
                        double until=sample*.1;
                        while(time<until)
                        {
                            while(segment+1<Boundaries.Length && time>=Boundaries[segment])++segment;
                            double target=Math.Min(until,Boundaries[segment]);
                            double dt=mode=="original"?Math.Min(maximum,target-time):BoundaryDuration(time,target,maximum);
                            if(!double.IsFinite(dt) || dt<=0 || dt>maximum || time+dt<=time)
                                throw new InvalidOperationException("Invalid reconstructed interval.");
                            double next=time+dt;
                            Int128 increment=Units(dt),represented=Units(next)-Units(time),rounding=represented-increment;
                            if(rounding!=0)++roundedClockAdditions;
                            maxClockRounding=Int128.Max(maxClockRounding,Int128.Abs(rounding));
                            supplied=checked(supplied+increment);time=next;++calls;
                            if(mode=="endpoint-difference" && rounding!=0)throw new InvalidOperationException("Endpoint difference lost elapsed time.");
                        }
                        if(time!=until)throw new InvalidOperationException("Sample endpoint changed.");
                        Int128 clock=Units(time),error=supplied-clock;
                        if(Int128.Abs(error)>maxSampleError){maxSampleError=Int128.Abs(error);worstSample=time;}
                        writer.WriteLine($"{time:R},{supplied},{clock},{error},{calls},{roundedClockAdditions}");
                    }
                }
                string hash;using(var stream=File.OpenRead(path))hash=Convert.ToHexString(SHA256.HashData(stream));
                cases.Add(new {key,mode,maximumStepMs=maximum,calls,roundedClockAdditions,finalTimeMs=time,
                    finalSuppliedUnits=supplied.ToString(),finalErrorUnits=(supplied-Units(time)).ToString(),
                    maximumSampleErrorUnits=maxSampleError.ToString(),worstSampleMs=worstSample,
                    maximumSingleAdditionRoundingUnits=maxClockRounding.ToString(),sha256=hash});
                Save("progress.json",cases);
            }
            Save("results.json",new {completed=true,cases,modelExecuted=false,solverRepairQualified=false,
                causalCalciumExplanationEstablished=false,physiologyQualified=false,avatarRecoveryAssessed=false});
            Console.WriteLine("Clock arithmetic captured. Independent exact-binary audit determines the elapsed-time discrepancy; no neuron was simulated.");
        }
        catch(Exception ex){Save("FAILED.json",new {error=ex.ToString(),completedCases=cases.Count});throw;}
    }
}

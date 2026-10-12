using System.Text.Json;
using NeuralResonanceEngine.Shared.Contracts;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class UnitySensoryWireTests
{
    [Fact]
    public void MeasuredUnityFallIsAcceptedWithoutLosingFallenOrientation()
    {
        var request = Read<PhysicalBodyFrameRequest>("body-frame-example.json");
        Assert.True(PhysicalBodyFrameDescriptor.TryCreate(request, out var descriptor, out var error), error);
        Assert.NotNull(descriptor);
        Assert.InRange(request.StoredEnergyJoules, 5966399f, 5966401f);
        Assert.InRange(request.HydrationFraction, .7477f, .7479f);
        Assert.InRange(request.TissueIntegrityFraction, .9999f, 1f);
        var articulation = descriptor.Articulation;
        Assert.InRange(articulation.TrunkPitchRadians, -2f, 2f);
        Assert.InRange(articulation.TrunkRollRadians, -2f, 2f);
        var measurements = articulation.Musculoskeletal!;
        var balance = measurements.Balance!;
        Assert.Equal("fallen", balance.Phase);
        Assert.True(Math.Abs(balance.FallRollRadians) > 2f);
        Assert.InRange(balance.CenterOfMassYMeters, 0.05f, 0.5f);
        Assert.Equal(0f, measurements.UprightFraction);
        Assert.False(descriptor.MotorTrainingMode);
    }

    [Fact]
    public void MeasuredUnityFloorContactIsAcceptedBySomaticReceptor()
    {
        var request = Read<SomaticContactFrameRequest>("contact-frame-example.json");
        Assert.True(SomaticContactDescriptor.TryCreate(request, out var descriptor, out var error), error);
        Assert.NotNull(descriptor);
        Assert.True(descriptor.ForceNewtons > 50f);
        Assert.InRange(descriptor.PenetrationMillimeters, 0f, 3f);
        Assert.Equal("avatar_unity_left_foot_contact", descriptor.InputSource);
    }

    [Theory]
    [InlineData("muscles-stand-0-body-frame.json")]
    [InlineData("muscles-stand-1-body-frame.json")]
    public void CapturedMuscleFramesAreAcceptedWithThirtyEightIndependentMeasurements(string filename)
    {
        var request=Read<PhysicalBodyFrameRequest>(filename);
        Assert.True(PhysicalBodyFrameDescriptor.TryCreate(request,out var descriptor,out var error),error);
        var measurements=descriptor!.Articulation.Musculoskeletal!;
        Assert.Equal(38,measurements.Muscles.Count);
        Assert.Equal(38,measurements.Muscles.Select(m=>(m.Name,m.Side)).Distinct().Count());
        Assert.Contains(measurements.Muscles,m=>m.Name=="GastrocnemiusSoleus"&&m.Side=="L");
        Assert.Contains(measurements.Muscles,m=>m.Name=="GastrocnemiusSoleus"&&m.Side=="R");
        Assert.False(descriptor.MotorTrainingMode);
        var response=new PhysicalBodyTransducerRuntime().Transduce(descriptor,1,request.TimestampMs);
        var withoutMuscles=descriptor with {Articulation=descriptor.Articulation with {
            Musculoskeletal=measurements with {Muscles=Array.Empty<PhysicalMuscleMeasurement>()}}};
        var baseline=new PhysicalBodyTransducerRuntime().Transduce(withoutMuscles,1,request.TimestampMs);
        Assert.True(response.ActiveProprioceptivePopulations>baseline.ActiveProprioceptivePopulations);
    }

    [Theory]
    [InlineData("corrected-body-standing-example.json",true)]
    [InlineData("corrected-body-fallen-example.json",false)]
    public void CorrectedPhysicalFramesPreserveRootTiltAndMeasuredPlantarFeedback(string filename,bool upright)
    {
        var request=Read<PhysicalBodyFrameRequest>(filename);
        Assert.True(PhysicalBodyFrameDescriptor.TryCreate(request,out var descriptor,out var error),error);
        var a=descriptor!.Articulation;
        Assert.InRange(Math.Abs(a.TrunkPitchRadians),0,.001f);
        Assert.InRange(Math.Abs(a.TrunkRollRadians),0,.001f);
        Assert.Equal(38,a.Musculoskeletal!.Muscles.Count);
        if(upright)Assert.True(a.Musculoskeletal.UprightFraction>.99);
        else Assert.True(Math.Abs(a.Musculoskeletal.Balance!.FallPitchRadians)>1);
        foreach(var foot in new[]{(a.LeftFootPressure,a.LeftFootLoadNewtons),(a.RightFootPressure,a.RightFootLoadNewtons)}) {
            Assert.NotNull(foot.Item1);var p=foot.Item1!;
            Assert.Equal(foot.Item2,p.HeelMedialLoadNewtons+p.HeelLateralLoadNewtons+p.ForefootMedialLoadNewtons+p.ForefootLateralLoadNewtons,2);
        }
        var response=new PhysicalBodyTransducerRuntime().Transduce(descriptor,1,request.TimestampMs);
        Assert.True(response.ActiveVestibularPopulations>0);
        Assert.Contains(response.ProprioceptiveLeft.Concat(response.ProprioceptiveRight),s=>s.SourceNeuronId.Contains("plantar_pressure"));
    }

    [Theory]
    [InlineData("pre-normalization-margin-example.json")]
    [InlineData("pre-saturation-hand-example.json")]
    public void ObservedFallRejectionsAreResolvedBySensorEncodingWithoutChangingRawPhysics(string filename)
    {
        var measured=Read<PhysicalBodyFrameRequest>(filename);
        Assert.False(PhysicalBodyFrameDescriptor.TryCreate(measured,out _,out _));
        var a=measured.Articulation!;var physicalBalance=a.Musculoskeletal!.Balance!;
        var encoded=measured with {Articulation=a with {
            LeftHandLoadNewtons=Nre.UnityWorld.BodySensorMath.HandReceptorLoad(a.LeftHandLoadNewtons),
            RightHandLoadNewtons=Nre.UnityWorld.BodySensorMath.HandReceptorLoad(a.RightHandLoadNewtons),
            Musculoskeletal=a.Musculoskeletal with {BalanceError=Nre.UnityWorld.BodySensorMath.NormalizedSupportError(physicalBalance.SupportMarginMeters)}}};
        Assert.True(PhysicalBodyFrameDescriptor.TryCreate(encoded,out _,out var error),error);
        Assert.Same(physicalBalance,encoded.Articulation!.Musculoskeletal!.Balance);
    }

    [Fact]
    public void ActualFloorSupportedFallKeepsItsBalanceErrorWithoutInventingRecoveryTeaching()
    {
        var measured=Read<PhysicalBodyFrameRequest>("live-fallen-zero-error-example.json");
        var a=measured.Articulation!;
        var muscles=a.Musculoskeletal!;
        var balance=muscles.Balance!;
        Assert.Equal("fallen",balance.Phase);
        Assert.True(muscles.UprightFraction<.3f);
        Assert.True(balance.SupportMarginMeters>0);
        Assert.Equal(0,muscles.BalanceError);
        Assert.True(PhysicalBodyFrameDescriptor.TryCreate(measured,out var oldDescriptor,out var oldError),oldError);

        var corrected=measured with {Articulation=a with {Musculoskeletal=muscles with {
            BalanceError=Nre.UnityWorld.BodySensorMath.PhysicalBalanceError(
                balance.SupportMarginMeters,balance.FallPitchRadians,balance.FallRollRadians,balance.Phase)}}};
        Assert.Equal(1,corrected.Articulation!.Musculoskeletal!.BalanceError);
        Assert.Same(balance,corrected.Articulation.Musculoskeletal.Balance);
        Assert.Same(muscles.Muscles,corrected.Articulation.Musculoskeletal.Muscles);
        Assert.True(PhysicalBodyFrameDescriptor.TryCreate(corrected,out var fixedDescriptor,out var fixedError),fixedError);

        // Both frames describe a fallen body. A changing floor support hull must
        // not turn its balance-error channel into a positive recovery delta.
        var previous=fixedDescriptor! with {Sequence=measured.Sequence-1,TimestampMs=measured.TimestampMs-100};
        var legacy=new PhysicalBodyTransducerRuntime();
        legacy.Transduce(previous,1,previous.TimestampMs);
        Assert.Equal(1,legacy.Transduce(oldDescriptor!,2,measured.TimestampMs).BalanceImprovement);
        var repaired=new PhysicalBodyTransducerRuntime();
        repaired.Transduce(previous,1,previous.TimestampMs);
        Assert.Equal(0,repaired.Transduce(fixedDescriptor!,2,measured.TimestampMs).BalanceImprovement);
    }

    private static T Read<T>(string filename)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "NeuralResonanceEngine.DNNE.slnx")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        var path = Path.Combine(directory.FullName, "UnityWorld", "Qualification", filename);
        return JsonSerializer.Deserialize<T>(File.ReadAllText(path),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }
}

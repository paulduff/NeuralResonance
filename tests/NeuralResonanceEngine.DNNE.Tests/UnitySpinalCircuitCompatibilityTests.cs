namespace NeuralResonanceEngine.DNNE.Tests;
public sealed class UnitySpinalCircuitCompatibilityTests
{
    [Fact]
    public void UnityPeripheralCircuitMatchesCanonicalCircuitThroughLoadingAndStopping()
    {
        var canonical=new NRE.SimAvatar.AvatarSpinalLocomotorCircuit();
        var portable=new Nre.UnityWorld.AvatarSpinalLocomotorCircuit();
        for (var i=0;i<600;i++)
        {
            var drive=i<50||i>450?0:i<250?.35:.7;
            var left=i%80<40?.8:0;
            var right=i%80<40?0:.8;
            var a=canonical.Advance(.02,drive,left,right);
            var b=portable.Advance(.02,drive,left,right);
            Assert.Equal(a.LeftFlexorRecruitment,b.LeftFlexorRecruitment,12);
            Assert.Equal(a.LeftExtensorRecruitment,b.LeftExtensorRecruitment,12);
            Assert.Equal(a.RightFlexorRecruitment,b.RightFlexorRecruitment,12);
            Assert.Equal(a.RightExtensorRecruitment,b.RightExtensorRecruitment,12);
            Assert.Equal(a.PhaseHandoffs,b.PhaseHandoffs);
        }
        canonical.Reset();portable.Reset();
        Assert.Equal(canonical.State.LeftCycle,portable.State.LeftCycle);
    }
}

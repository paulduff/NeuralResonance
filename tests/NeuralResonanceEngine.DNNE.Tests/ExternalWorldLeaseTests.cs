using NRE.WorldSim;
namespace NeuralResonanceEngine.DNNE.Tests;
public sealed class ExternalWorldLeaseTests
{
    [Fact]
    public void OnlyOwnerCanRenewOrReleaseAndExpiryRequiresNewAcquisition()
    {
        var clock = new Clock();
        var lease = new ExternalWorldLease(clock);
        Assert.False(lease.TryAcquire("", out _));
        Assert.True(lease.TryAcquire("unity-1", out var token));
        Assert.False(lease.TryAcquire("unity-2", out _));
        Assert.False(lease.Renew("wrong"));
        Assert.False(lease.Release("wrong"));
        clock.Now = clock.Now.AddSeconds(6);
        Assert.True(lease.Renew(token!));
        clock.Now = clock.Now.AddSeconds(11);
        Assert.False(lease.IsHeld);
        Assert.False(lease.Renew(token!));
        Assert.True(lease.TryAcquire("unity-2", out var next));
        Assert.NotEqual(token, next);
        Assert.False(lease.Release(token!));
        Assert.True(lease.Release(next!));
    }
    [Fact]
    public async Task ExternalOwnershipPausesWorldAndBlocksResumeAndReset()
    {
        await using var world = new HeadlessWorldRuntime(new HeadlessWorldOptions(new Uri("http://127.0.0.1:5080"), StartPaused:true,
            ReportDirectory:Path.Combine(Path.GetTempPath(),"DNNE-UnityLease",Guid.NewGuid().ToString("N"))));
        world.Resume();
        var token=world.AcquireExternalWorld("unity");
        Assert.NotNull(token);
        Assert.False(world.GetSnapshot().Running);
        Assert.Throws<InvalidOperationException>(()=>world.Resume());
        Assert.Throws<InvalidOperationException>(()=>world.Reset());
        Assert.True(world.ReleaseExternalWorld(token!));
        Assert.False(world.GetSnapshot().Running);
        world.Resume();
        Assert.True(world.GetSnapshot().Running);
    }
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = DateTimeOffset.Parse("2026-10-12T10:00:00Z");
        public override DateTimeOffset GetUtcNow()=>Now;
    }
}

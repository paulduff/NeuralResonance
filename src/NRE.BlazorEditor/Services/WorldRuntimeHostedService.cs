using NRE.WorldSim;

namespace NRE.BlazorEditor.Services;

public sealed class WorldRuntimeHostedService(HeadlessWorldRuntime runtime) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        runtime.Start(cancellationToken);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => runtime.StopAsync();
}

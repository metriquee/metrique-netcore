using Microsoft.Extensions.Hosting;

namespace Metriquee.NetCore.Internal;

internal sealed class NoOpHostedService : IHostedService
{
    public static readonly NoOpHostedService Instance = new();

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
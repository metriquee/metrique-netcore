using Metriquee.NetCore.Abstractions;
using Metriquee.NetCore.Models;
using Metriquee.NetCore.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Metriquee.NetCore.Services;

internal sealed class HealthPublisherHostedService : BackgroundService
{
    private readonly ILogger<HealthPublisherHostedService> _logger;
    private readonly LogCollectorOptions _options;
    private readonly ICollectorSink _sink;

    public HealthPublisherHostedService(
        ICollectorSink sink,
        IOptions<LogCollectorOptions> options,
        ILogger<HealthPublisherHostedService> logger)
    {
        _sink = sink;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Health.IsEnabled) return;

        var interval = TimeSpan.FromSeconds(_options.Health.IntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
            try
            {
                await Task.Delay(interval, stoppingToken);
                await PublishHealthAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Graceful shutdown
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing health");
            }
    }

    private async Task PublishHealthAsync(CancellationToken ct)
    {
        var evt = new HealthEvent
        {
            Timestamp = DateTimeOffset.UtcNow,
            Category = "self",
            Status = "Healthy", // Hardcoded for now as we don't have deeper health checks
            Entries = new Dictionary<string, HealthEntry>()
        };

        await _sink.TrackHealthAsync(evt, ct);
    }
}
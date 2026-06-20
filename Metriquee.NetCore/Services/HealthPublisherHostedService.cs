using System.Collections.Concurrent;
using System.Diagnostics;
using Metriquee.NetCore.Abstractions;
using Metriquee.NetCore.Checks;
using Metriquee.NetCore.Models;
using Metriquee.NetCore.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Metriquee.NetCore.Services;

// Runs the registered custom health checks. Each checker runs on its own interval inside a DI
// scope; the latest result per check is kept in memory and published together as one HealthEvent
// on Health.IntervalSeconds. When no checkers are registered it falls back to publishing a single
// "Healthy" self status, preserving the previous behaviour.
internal sealed class HealthPublisherHostedService : BackgroundService
{
    private readonly ILogger<HealthPublisherHostedService> _logger;
    private readonly MetriqueeOptions _options;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ICollectorSink _sink;

    public HealthPublisherHostedService(
        ICollectorSink sink,
        IServiceScopeFactory scopeFactory,
        IOptions<MetriqueeOptions> options,
        ILogger<HealthPublisherHostedService> logger)
    {
        _sink = sink;
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Health.IsEnabled) return;

        var checkers = _options.Health.Checkers.Where(c => c.IsEnabled).ToList();
        if (checkers.Count == 0)
        {
            await RunSelfOnlyLoopAsync(stoppingToken);
            return;
        }

        var slots = new ConcurrentDictionary<string, CheckSlot>();
        var loops = new List<Task>(checkers.Count + 1);
        foreach (var checker in checkers)
            loops.Add(RunCheckerLoopAsync(checker, slots, stoppingToken));
        loops.Add(PublishLoopAsync(slots, stoppingToken));

        await Task.WhenAll(loops);
    }

    // No custom checks registered: keep publishing a single Healthy self status.
    private async Task RunSelfOnlyLoopAsync(CancellationToken ct)
    {
        var interval = TimeSpan.FromSeconds(_options.Health.IntervalSeconds);
        while (!ct.IsCancellationRequested)
            try
            {
                await Task.Delay(interval, ct);
                await _sink.TrackHealthAsync(new HealthEvent
                {
                    Timestamp = DateTimeOffset.UtcNow,
                    Category = "self",
                    Status = "Healthy",
                    Entries = new Dictionary<string, HealthEntry>()
                }, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing health");
            }
    }

    // One loop per checker: run immediately, then every IntervalSeconds.
    private async Task RunCheckerLoopAsync(
        CheckerRegistration reg, ConcurrentDictionary<string, CheckSlot> slots, CancellationToken ct)
    {
        var interval = TimeSpan.FromSeconds(reg.IntervalSeconds);
        while (!ct.IsCancellationRequested)
        {
            await RunCheckerOnceAsync(reg, slots, ct);
            try
            {
                await Task.Delay(interval, ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task RunCheckerOnceAsync(
        CheckerRegistration reg, ConcurrentDictionary<string, CheckSlot> slots, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var checker = (IMetriqueeChecker)scope.ServiceProvider.GetRequiredService(reg.CheckerType);
            var result = await checker.CheckHealthAsync(ct);
            sw.Stop();

            slots[reg.Name] = new CheckSlot(
                StatusToString(result.Status), result.Description, result.ExceptionMessage,
                result.Data, sw.Elapsed, DateTimeOffset.UtcNow, reg.IntervalSeconds);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Graceful shutdown
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Health check '{Name}' threw", reg.Name);
            slots[reg.Name] = new CheckSlot(
                "Unhealthy", null, ex.Message, null, sw.Elapsed, DateTimeOffset.UtcNow, reg.IntervalSeconds);
        }
    }

    // Publishes a combined snapshot of all current check results on the publish interval.
    private async Task PublishLoopAsync(ConcurrentDictionary<string, CheckSlot> slots, CancellationToken ct)
    {
        var interval = TimeSpan.FromSeconds(_options.Health.IntervalSeconds);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            try
            {
                await PublishSnapshotAsync(slots, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing health");
            }
        }
    }

    private async Task PublishSnapshotAsync(ConcurrentDictionary<string, CheckSlot> slots, CancellationToken ct)
    {
        if (slots.IsEmpty) return;

        var entries = slots.ToDictionary(
            kv => kv.Key,
            kv => new HealthEntry
            {
                Status = kv.Value.Status,
                Description = kv.Value.Description,
                ExceptionMessage = kv.Value.ExceptionMessage,
                Data = kv.Value.Data,
                Duration = kv.Value.Duration,
                IntervalSeconds = kv.Value.IntervalSeconds,
                LastCheckedAt = kv.Value.LastCheckedAt
            });

        var evt = new HealthEvent
        {
            Timestamp = DateTimeOffset.UtcNow,
            Category = "custom",
            Status = WorstStatus(entries.Values),
            Entries = entries
        };

        await _sink.TrackHealthAsync(evt, ct);
    }

    private static string StatusToString(MetriqueeHealthStatus status) => status switch
    {
        MetriqueeHealthStatus.Healthy => "Healthy",
        MetriqueeHealthStatus.Degraded => "Degraded",
        MetriqueeHealthStatus.Unhealthy => "Unhealthy",
        _ => "Unhealthy"
    };

    // Overall status is the worst of all checks (Unhealthy > Degraded > Healthy).
    private static string WorstStatus(IEnumerable<HealthEntry> entries)
    {
        var worst = MetriqueeHealthStatus.Healthy;
        foreach (var e in entries)
        {
            var s = e.Status switch
            {
                "Unhealthy" => MetriqueeHealthStatus.Unhealthy,
                "Degraded" => MetriqueeHealthStatus.Degraded,
                _ => MetriqueeHealthStatus.Healthy
            };
            if (s > worst) worst = s;
        }

        return StatusToString(worst);
    }

    private sealed record CheckSlot(
        string Status,
        string? Description,
        string? ExceptionMessage,
        IReadOnlyDictionary<string, object?>? Data,
        TimeSpan Duration,
        DateTimeOffset LastCheckedAt,
        int IntervalSeconds);
}

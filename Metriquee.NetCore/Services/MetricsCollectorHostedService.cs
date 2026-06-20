using System.Diagnostics;
using Metriquee.NetCore.Abstractions;
using Metriquee.NetCore.Internal;
using Metriquee.NetCore.Models;
using Metriquee.NetCore.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Metriquee.NetCore.Services;

internal sealed class MetricsCollectorHostedService : BackgroundService
{
    private readonly RequestCounters _counters;
    private readonly ILogger<MetricsCollectorHostedService> _logger;
    private readonly LogCollectorOptions _options;
    private readonly ICollectorSink _sink;
    private DateTimeOffset _lastCheck;
    private TimeSpan _lastCpuTime;

    public MetricsCollectorHostedService(
        ICollectorSink sink,
        RequestCounters counters,
        IOptions<LogCollectorOptions> options,
        ILogger<MetricsCollectorHostedService> logger)
    {
        _sink = sink;
        _counters = counters;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Metrics.IsEnabled) return;

        var interval = TimeSpan.FromSeconds(_options.Metrics.IntervalSeconds);
        using (var process = Process.GetCurrentProcess())
        {
            _lastCpuTime = process.TotalProcessorTime;
        }

        _lastCheck = DateTimeOffset.UtcNow;

        while (!stoppingToken.IsCancellationRequested)
            try
            {
                await Task.Delay(interval, stoppingToken);
                await CollectAndPublishMetricsAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Graceful shutdown
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting metrics");
            }
    }

    private async Task CollectAndPublishMetricsAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        using var process = Process.GetCurrentProcess();

        var workingSet = process.WorkingSet64;
        var managedHeap = GC.GetTotalMemory(false);

        var gen0 = GC.CollectionCount(0);
        var gen1 = GC.CollectionCount(1);
        var gen2 = GC.CollectionCount(2);

        ThreadPool.GetAvailableThreads(out var workerThreads, out var ioThreads);
        ThreadPool.GetMaxThreads(out var maxWorkerThreads, out var maxIoThreads);

        var requestCount = _counters.SnapshotAndReset();

        var elapsed = now - _lastCheck;
        double rps = 0;
        if (elapsed.TotalSeconds > 0) rps = requestCount / elapsed.TotalSeconds;

        double? cpuPercent = null;
        try
        {
            var currentCpuTime = process.TotalProcessorTime;
            var cpuUsed = currentCpuTime - _lastCpuTime;
            if (elapsed.TotalMilliseconds > 0)
                // Create a rough percentage: (CPU time used / Wall time elapsed) / Number of Processors?
                // Or typically just (TotalCpuTimeUsed / ElapsedTime) * 100 / Environment.ProcessorCount
                cpuPercent = cpuUsed.TotalMilliseconds / elapsed.TotalMilliseconds * 100 / Environment.ProcessorCount;
            _lastCpuTime = currentCpuTime;
        }
        catch
        {
            // Access denied or other issue
        }

        _lastCheck = now;

        var evt = new MetricsEvent
        {
            Timestamp = now,
            WorkingSetBytes = workingSet,
            ManagedHeapBytes = managedHeap,
            Gen0Collections = gen0,
            Gen1Collections = gen1,
            Gen2Collections = gen2,
            ThreadPoolAvailableWorkerThreads = workerThreads,
            ThreadPoolAvailableIoThreads = ioThreads,
            ThreadPoolMaxWorkerThreads = maxWorkerThreads,
            ThreadPoolMaxIoThreads = maxIoThreads,
            RequestsPerSecond = rps,
            CpuProcessPercent = cpuPercent
        };

        await _sink.TrackMetricsAsync(evt, ct);
    }
}
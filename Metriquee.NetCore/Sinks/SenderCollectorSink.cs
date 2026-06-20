using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Metriquee.NetCore.Abstractions;
using Metriquee.NetCore.Models;
using Metriquee.NetCore.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Metriquee.NetCore.Sinks;

internal sealed class SenderCollectorSink(
    IHttpClientFactory httpClientFactory,
    IOptions<MetriqueeOptions> options,
    ILogger<SenderCollectorSink> logger) : ICollectorSink, IHostedService, IAsyncDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private readonly ConcurrentQueue<ExceptionEvent> _exceptionEvents = new();
    private readonly ConcurrentQueue<HealthEvent> _healthEvents = new();
    private readonly ConcurrentQueue<HttpEvent> _httpEvents = new();
    private readonly ConcurrentQueue<MetricsEvent> _metricsEvents = new();
    private volatile bool _disposed;

    private int _eventCount;
    private PeriodicTimer? _timer;
    private Task? _timerTask;

    private MetriqueeOptions Options => options.Value;

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        await ShutdownAsync(CancellationToken.None);
        _cts.Dispose();
    }

    public ValueTask TrackHttpAsync(HttpEvent evt, CancellationToken ct = default)
    {
        _httpEvents.Enqueue(evt);
        if (Interlocked.Increment(ref _eventCount) >= Options.Batch.MaxBatchSize)
            _ = FlushAsync(CancellationToken.None);
        return ValueTask.CompletedTask;
    }

    public ValueTask TrackExceptionAsync(ExceptionEvent evt, CancellationToken ct = default)
    {
        _exceptionEvents.Enqueue(evt);
        if (Interlocked.Increment(ref _eventCount) >= Options.Batch.MaxBatchSize)
            _ = FlushAsync(CancellationToken.None);
        return ValueTask.CompletedTask;
    }

    public ValueTask TrackMetricsAsync(MetricsEvent evt, CancellationToken ct = default)
    {
        _metricsEvents.Enqueue(evt);
        if (Interlocked.Increment(ref _eventCount) >= Options.Batch.MaxBatchSize)
            _ = FlushAsync(CancellationToken.None);
        return ValueTask.CompletedTask;
    }

    public ValueTask TrackHealthAsync(HealthEvent evt, CancellationToken ct = default)
    {
        _healthEvents.Enqueue(evt);
        if (Interlocked.Increment(ref _eventCount) >= Options.Batch.MaxBatchSize)
            _ = FlushAsync(CancellationToken.None);
        return ValueTask.CompletedTask;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _timer = new PeriodicTimer(TimeSpan.FromSeconds(Options.Batch.FlushIntervalSeconds));
        _timerTask = RunTimerAsync(_cts.Token);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return ShutdownAsync(cancellationToken);
    }

    // Shared by StopAsync and DisposeAsync: cancel the timer loop, wait for it to finish,
    // and flush any remaining buffered events. Safe to call more than once.
    private async Task ShutdownAsync(CancellationToken cancellationToken)
    {
        if (!_cts.IsCancellationRequested)
            await _cts.CancelAsync();
        _timer?.Dispose();

        if (_timerTask is not null)
            try
            {
                await _timerTask;
            }
            catch (OperationCanceledException)
            {
            }

        await FlushAsync(cancellationToken);
    }

    private async Task RunTimerAsync(CancellationToken ct)
    {
        while (await _timer!.WaitForNextTickAsync(ct)) await FlushAsync(ct);
    }

    private async Task FlushAsync(CancellationToken ct)
    {
        var drainedCount = Interlocked.Exchange(ref _eventCount, 0);
        if (drainedCount == 0)
            return;

        var httpEvents = DrainQueue(_httpEvents);
        var exceptionEvents = DrainQueue(_exceptionEvents);
        var metricsEvents = DrainQueue(_metricsEvents);
        var healthEvents = DrainQueue(_healthEvents);

        var batches = BuildBatches(httpEvents, exceptionEvents, metricsEvents, healthEvents);

        foreach (var batch in batches) await SendBatchAsync(batch, ct);
    }

    private List<BatchPayload> BuildBatches(
        List<HttpEvent> httpEvents,
        List<ExceptionEvent> exceptionEvents,
        List<MetricsEvent> metricsEvents,
        List<HealthEvent> healthEvents)
    {
        var maxSize = Options.Batch.MaxPayloadSizeMb * 1024L * 1024L;
        var payload = new BatchPayload
        {
            ApiKey = Options.Sender.ApiKey,
            FlushedAt = DateTimeOffset.UtcNow,
            HttpEvents = httpEvents,
            ExceptionEvents = exceptionEvents,
            MetricsEvents = metricsEvents,
            HealthEvents = healthEvents
        };

        var totalEvents = httpEvents.Count + exceptionEvents.Count + metricsEvents.Count + healthEvents.Count;

        // Fits within the limit, or is a single event that cannot be split further — send as one.
        if (totalEvents <= 1 ||
            Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(payload)) <= maxSize)
            return [payload];

        // Halve every event list and recurse on each side until each batch fits.
        var (httpA, httpB) = SplitHalf(httpEvents);
        var (exA, exB) = SplitHalf(exceptionEvents);
        var (metA, metB) = SplitHalf(metricsEvents);
        var (healthA, healthB) = SplitHalf(healthEvents);

        var batches = BuildBatches(httpA, exA, metA, healthA);
        batches.AddRange(BuildBatches(httpB, exB, metB, healthB));
        return batches;
    }

    private static (List<T> First, List<T> Second) SplitHalf<T>(List<T> source)
    {
        var mid = source.Count / 2;
        return (source.GetRange(0, mid), source.GetRange(mid, source.Count - mid));
    }

    private async Task SendBatchAsync(BatchPayload batch, CancellationToken ct)
    {
        try
        {
            var client = httpClientFactory.CreateClient("Metriquee");
            var baseUrl = Options.Sender.BaseUrl.TrimEnd('/');

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/api/logs");
            request.Headers.Add("X-Api-Key", Options.Sender.ApiKey);
            request.Content = JsonContent.Create(batch);

            using var response = await client.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to send batch to {BaseUrl}", Options.Sender.BaseUrl);
        }
    }

    private static List<T> DrainQueue<T>(ConcurrentQueue<T> queue)
    {
        var items = new List<T>();
        while (queue.TryDequeue(out var item))
            items.Add(item);
        return items;
    }
}
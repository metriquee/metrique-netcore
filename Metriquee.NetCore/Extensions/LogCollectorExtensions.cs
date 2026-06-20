using Metriquee.NetCore.Abstractions;
using Metriquee.NetCore.Internal;
using Metriquee.NetCore.Middleware;
using Metriquee.NetCore.Options;
using Metriquee.NetCore.Services;
using Metriquee.NetCore.Sinks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Metriquee.NetCore.Extensions;

public static class LogCollectorExtensions
{
    public static IServiceCollection AddLogCollector(
        this IServiceCollection services,
        Action<LogCollectorOptions>? configure = null)
    {
        services.AddOptions<LogCollectorOptions>()
            .Validate(o => !o.Sender.EnableSenderSink ||
                           SenderConnectionString.TryParse(o.Sender.ConnectionString, out _, out _),
                "Sender sink is enabled but Sender.ConnectionString is missing or invalid " +
                "(expected scheme://<apiKey>@host).")
            .ValidateOnStart();
        if (configure is not null) services.Configure(configure);

        services.TryAddSingleton<RequestCounters>();
        services.AddHttpClient("LogCollector");

        services.TryAddSingleton<ICollectorSink>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<LogCollectorOptions>>().Value;

            var loggerSink = ActivatorUtilities.CreateInstance<LoggerCollectorSink>(sp);
            var senderSink = ActivatorUtilities.CreateInstance<SenderCollectorSink>(sp);

            return options.Sender switch
            {
                { EnableLoggerSink: true, EnableSenderSink: false } => loggerSink,
                { EnableLoggerSink: false, EnableSenderSink: true } => senderSink,
                { EnableLoggerSink: true, EnableSenderSink: true } =>
                    new CompositeCollectorSink([
                        loggerSink, senderSink
                    ]),
                _ => new CompositeCollectorSink([])
            };
        });

        services.AddSingleton<IHostedService>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<LogCollectorOptions>>().Value;
            if (!opts.Sender.EnableSenderSink)
                return NoOpHostedService.Instance;

            var sink = sp.GetRequiredService<ICollectorSink>();

            // Extract HttpLogCollectorSink from composite or direct
            if (sink is SenderCollectorSink httpSink)
                return httpSink;

            if (sink is CompositeCollectorSink composite)
            {
                var inner = composite.GetSink<SenderCollectorSink>();
                if (inner is not null)
                    return inner;
            }

            return NoOpHostedService.Instance;
        });

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, MetricsCollectorHostedService>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, HealthPublisherHostedService>());

        return services;
    }

    public static IApplicationBuilder UseLogCollector(this IApplicationBuilder app)
    {
        app.UseMiddleware<HttpLoggingMiddleware>();
        app.UseMiddleware<ExceptionLoggingMiddleware>();
        return app;
    }
}
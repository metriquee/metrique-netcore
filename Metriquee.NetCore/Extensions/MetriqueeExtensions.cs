using System.Reflection;
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

public static class MetriqueeExtensions
{
    public static IServiceCollection AddMetriquee(
        this IServiceCollection services,
        Action<MetriqueeOptions>? configure = null)
    {
        services.AddOptions<MetriqueeOptions>()
            .Validate(o => !o.Sender.EnableSenderSink ||
                           SenderConnectionString.TryParse(o.Sender.ConnectionString, out _, out _),
                "Sender sink is enabled but Sender.ConnectionString is missing or invalid " +
                "(expected scheme://<apiKey>@host).")
            // Auto-fill resource tags left blank after the user's configure runs. Only fills empties,
            // so an explicit opts.Resource.* always wins.
            .PostConfigure<IHostEnvironment>((o, hostEnv) =>
            {
                if (string.IsNullOrWhiteSpace(o.Resource.Environment))
                    o.Resource.Environment = hostEnv.EnvironmentName;
                if (string.IsNullOrWhiteSpace(o.Resource.Host))
                    o.Resource.Host = System.Environment.MachineName;
                if (string.IsNullOrWhiteSpace(o.Resource.Release))
                    o.Resource.Release = DefaultRelease();
            })
            .ValidateOnStart();
        if (configure is not null)
        {
            services.Configure(configure);

            // Run the lambda once on a throwaway options instance to discover the registered
            // checker types, then register each enabled one in DI as scoped (so a check can
            // depend on scoped services such as a DbContext). The hosted service reads the real
            // names/intervals later from IOptions<MetriqueeOptions>.
            var probe = new MetriqueeOptions();
            configure(probe);
            foreach (var checker in probe.Health.Checkers.Where(c => c.IsEnabled))
                services.TryAddScoped(checker.CheckerType);
        }

        services.TryAddSingleton<RequestCounters>();
        services.AddHttpClient("Metriquee");

        services.TryAddSingleton<ICollectorSink>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<MetriqueeOptions>>().Value;

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
            var opts = sp.GetRequiredService<IOptions<MetriqueeOptions>>().Value;
            if (!opts.Sender.EnableSenderSink)
                return NoOpHostedService.Instance;

            var sink = sp.GetRequiredService<ICollectorSink>();

            // Extract the SenderCollectorSink from composite or direct
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

    public static IApplicationBuilder UseMetriquee(this IApplicationBuilder app)
    {
        app.UseMiddleware<HttpLoggingMiddleware>();
        app.UseMiddleware<ExceptionLoggingMiddleware>();
        return app;
    }

    // Best-effort release/version of the host app: prefer the informational version (e.g. SemVer with
    // git hash), fall back to the assembly version, then empty.
    private static string DefaultRelease()
    {
        var asm = Assembly.GetEntryAssembly();
        return asm?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
               ?? asm?.GetName().Version?.ToString()
               ?? string.Empty;
    }
}
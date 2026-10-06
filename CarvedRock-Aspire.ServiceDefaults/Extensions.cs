using Microsoft.AspNetCore.Builder;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.Hosting;

public static class Extensions
{
    private static readonly string[] _excludeFromTraces =
    [
        "aspnetcore-browser-refresh.js",
        "browserLink",
        "health"
    ];

    public static IHostApplicationBuilder AddServiceDefaults(this IHostApplicationBuilder builder)
    {
        builder.ConfigureOpenTelemetry();
        builder.AddDefaultHealthChecks(); 

        builder.Services.AddServiceDiscovery();

        return builder;
    }

    public static IHostApplicationBuilder ConfigureOpenTelemetry(this IHostApplicationBuilder builder)
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();
            })
            .WithTracing(tracing =>
            {
                tracing.AddAspNetCoreInstrumentation(options =>
                {
                    options.Filter = (httpContext) =>
                    {
                        var excludeFromTraces = _excludeFromTraces;
                        if (excludeFromTraces.Any(ex => httpContext.Request.Path.Value?.Contains(ex) ?? false))
                        {
                            return false;
                        }
                        return true;
                    };
                })
                .AddHttpClientInstrumentation(options =>
                {
                    options.FilterHttpRequestMessage = (request) =>
                    {
                        return !(request.RequestUri?.ToString().Contains(
                            "getScriptTag", StringComparison.InvariantCultureIgnoreCase) ?? false);
                    };
                });
            });

        builder.AddOpenTelemetryExporters(); 

        return builder;
    }


    private static IHostApplicationBuilder AddOpenTelemetryExporters(this IHostApplicationBuilder builder)
    {
        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter)
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }        
        return builder;
    }


    public static IHostApplicationBuilder AddDefaultHealthChecks(this IHostApplicationBuilder builder)
    {
        builder.Services.AddRequestTimeouts(); 
        builder.Services.AddOutputCache(); 


        builder.Services.AddRequestTimeouts(
            configure: static timeouts => 
                timeouts.AddPolicy("HealthChecks", TimeSpan.FromSeconds(5))); 

        builder.Services.AddOutputCache(
            configureOptions: static caching => 
                caching.AddPolicy("HealthChecks", 
                build: static policy => policy.Expire(TimeSpan.FromSeconds(10)))); 
        
        builder.Services.AddHealthChecks()
            // Add a default liveness check to ensure app is responsive
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);
        
        return builder; 
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        var healthChecks = app.MapGroup("");

        healthChecks
            .CacheOutput("HealthChecks")
            .WithRequestTimeout("HealthChecks");

        healthChecks.MapHealthChecks("/health");
        healthChecks.MapHealthChecks("/alive", new()
        {
            Predicate = static registration => registration.Tags.Contains("live")
        });

        app.UseRequestTimeouts();
        app.UseOutputCache();

        return app;
    }
}

using EcommerceSupport.Agents.Factory;
using EcommerceSupport.Agents.Memory;
using EcommerceSupport.Agents.Middleware;
using EcommerceSupport.Agents.Options;
using EcommerceSupport.Api.Services;
using EcommerceSupport.Core.Interfaces;
using EcommerceSupport.Infrastructure.Repositories;
using EcommerceSupport.Infrastructure.Tools;
using EcommerceSupport.Workflows;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace EcommerceSupport.Api.Extensions;

public static class ServiceExtensions
{
    // ─── Repositories ─────────────────────────────────────────────────────────

    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        // In-memory implementations — swap for EF Core / Cosmos DB / Redis in production
        services.AddSingleton<IOrderRepository, InMemoryOrderRepository>();
        services.AddSingleton<ICustomerRepository, InMemoryCustomerRepository>();
        services.AddSingleton<ITicketRepository, InMemoryTicketRepository>();
        return services;
    }

    // ─── Tools ────────────────────────────────────────────────────────────────

    public static IServiceCollection AddTools(this IServiceCollection services)
    {
        services.AddScoped<OrderTools>();
        services.AddScoped<BillingTools>();
        services.AddScoped<ShippingTools>();
        services.AddScoped<TechnicalTools>();
        return services;
    }

    // ─── Agents ───────────────────────────────────────────────────────────────

    public static IServiceCollection AddAgents(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AgentOptions>(
            configuration.GetSection(AgentOptions.SectionName));

        // Memory provider: singleton because it manages per-session state internally
        services.AddSingleton<CustomerContextProvider>();

        // Middleware: singleton for shared in-memory state (rate limiter counters)
        services.AddSingleton<AuditLoggingMiddleware>();
        services.AddSingleton<RateLimitingMiddleware>();

        // AgentFactory: scoped so tools are injected at request scope
        services.AddScoped<AgentFactory>();

        return services;
    }

    // ─── Workflow ─────────────────────────────────────────────────────────────

    public static IServiceCollection AddSupportWorkflow(this IServiceCollection services)
    {
        services.AddScoped<ISupportWorkflow, SupportWorkflow>();
        return services;
    }

    // ─── Sessions ─────────────────────────────────────────────────────────────

    public static IServiceCollection AddSessionManagement(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddScoped<ISessionManager, InMemorySessionManager>();
        return services;
    }

    // ─── Observability ────────────────────────────────────────────────────────

    public static IServiceCollection AddObservability(
        this IServiceCollection services, IConfiguration configuration)
    {
        var otlpEndpoint = configuration["Observability:OtlpEndpoint"];

        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService("EcommerceSupport.Api",
                serviceVersion: "1.0.0"))
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddSource("Microsoft.Agents.*");   // capture all AF spans

                if (!string.IsNullOrEmpty(otlpEndpoint))
                    tracing.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
            });

        return services;
    }
}

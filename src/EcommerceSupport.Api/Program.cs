using EcommerceSupport.Api.Extensions;
using Serilog;
using Serilog.Events;
using Scalar.AspNetCore;

// ─── Bootstrap Serilog ────────────────────────────────────────────────────────

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.Agents", LogEventLevel.Information)
    .MinimumLevel.Debug()
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/ecommerce-support-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7)
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // ─── Logging ──────────────────────────────────────────────────────────────
    builder.Host.UseSerilog();

    // ─── Services ─────────────────────────────────────────────────────────────
    var services = builder.Services;
    var config = builder.Configuration;

    services
        .AddRepositories()
        .AddTools()
        .AddAgents(config)
        .AddSupportWorkflow()
        .AddSessionManagement()
        .AddObservability(config);

    services.AddControllers();
    services.AddOpenApi();
    services.AddEndpointsApiExplorer();

    // Health checks
    services.AddHealthChecks();

    // ─── Build App ────────────────────────────────────────────────────────────
    var app = builder.Build();

    app.UseSerilogRequestLogging(opts =>
    {
        opts.MessageTemplate =
            "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.00}ms";
    });

    app.UseRouting();
    app.MapControllers();
    app.MapHealthChecks("/health");

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();   // serves /openapi/v1.json
        app.MapScalarApiReference();

        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/openapi/v1.json", "EcommerceSupport API v1");
        });
    }

    Log.Information("Starting E-Commerce Support API...");
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly.");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

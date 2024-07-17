using Serilog;
using Serilog.Events;

try
{
    Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Override("Microsoft.AspNetCore.Hosting", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.AspNetCore.Mvc", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.AspNetCore.Routing", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .CreateLogger();

    WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

    builder.Services.AddSerilog();

    builder.AddDefinitions(typeof(Program));

    WebApplication app = builder.Build();

    app.UseDefinitions();

    app.UseSerilogRequestLogging();

    app.Run();

    return 0;
}
catch (Exception ex)
{
    string type = ex.GetType().Name;

    if (type.Equals("HostAbortedException", StringComparison.Ordinal))
    {
        throw;
    }

    Log.Fatal(ex, "Unhandled exception");

    return 0;
}
finally
{
    Log.CloseAndFlush();
}
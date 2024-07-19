using Serilog;
using Serilog.Events;

try
{
    Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Debug()
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
catch (Exception exception)
{
    Type type = exception.GetType();

    if (type == typeof(HostAbortedException))
    {
        throw;
    }

    Log.Fatal(exception, "Unhandled exception");

    return 0;
}
finally
{
    Log.CloseAndFlush();
}
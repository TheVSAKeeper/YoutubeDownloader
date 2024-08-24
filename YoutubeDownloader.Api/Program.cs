using NLog;
using NLog.Web;

Logger? logger = LogManager.Setup()
    .LoadConfigurationFromAppSettings()
    .GetCurrentClassLogger();

logger.Debug("init main");

try
{
    WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

    builder.Logging.ClearProviders();
    builder.Host.UseNLog();

    builder.AddDefinitions(typeof(Program));

    WebApplication app = builder.Build();

    app.UseDefinitions();

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

    logger.Fatal(exception, "Unhandled exception");

    return 0;
}
finally
{
    LogManager.Shutdown();
}

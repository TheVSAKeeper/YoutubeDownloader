using System.Text.Json.Serialization;

namespace YoutubeDownloader.Api.Definitions.Common;

public class CommonDefinition : AppDefinition
{
    public override void ConfigureServices(WebApplicationBuilder builder)
    {
        builder.Services.AddLocalization();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddResponseCaching();
        builder.Services.AddMemoryCache();
        builder.Services.AddEndpointsApiExplorer();

        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        });
    }

    public override void ConfigureApplication(WebApplication app)
    {
        app.UseHttpsRedirection();
    }
}
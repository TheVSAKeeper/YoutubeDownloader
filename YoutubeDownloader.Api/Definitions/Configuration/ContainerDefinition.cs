using YoutubeDownloader.Api.Configurations;

namespace YoutubeDownloader.Api.Definitions.Configuration;

public class ConfigurationDefinition : AppDefinition
{
    public override void ConfigureServices(WebApplicationBuilder builder)
    {
        builder.Services.Configure<DownloadOptions>(builder.Configuration.GetSection(nameof(DownloadOptions)));
        builder.Services.Configure<TelegramBotOptions>(builder.Configuration.GetSection(nameof(TelegramBotOptions)));
    }
}
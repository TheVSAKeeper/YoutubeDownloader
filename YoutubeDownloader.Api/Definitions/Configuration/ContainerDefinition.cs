using YoutubeDownloader.Api.Application;
using YoutubeDownloader.Api.Configurations;

namespace YoutubeDownloader.Api.Definitions.Configuration;

public class ConfigurationDefinition : AppDefinition
{
    public override void ConfigureServices(WebApplicationBuilder builder)
    {
        builder.AddConfiguration<DownloadOptions>();
        builder.AddConfiguration<TelegramBotOptions>();
        builder.AddConfiguration<FFmpegOptions>();
    }
}
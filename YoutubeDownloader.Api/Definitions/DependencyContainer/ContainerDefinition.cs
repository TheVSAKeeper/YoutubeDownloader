using YoutubeDownloader.Api.Services;
using YoutubeExplode;

namespace YoutubeDownloader.Api.Definitions.DependencyContainer;

public class ContainerDefinition : AppDefinition
{
    public override void ConfigureServices(WebApplicationBuilder builder)
    {
        builder.Services.AddHostedService<VideoDownloaderBackgroundService>();
        //builder.Services.AddHostedService<TelegramBotService>();
        builder.Services.AddSingleton<DownloadService>();
        builder.Services.AddTransient<YoutubeClient>();
        builder.Services.AddSingleton<YoutubeDownloadService>();
    }
}
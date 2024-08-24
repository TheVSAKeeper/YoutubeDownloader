using YoutubeDownloader.Api.Services;
using YoutubeExplode;
using FFmpeg = YoutubeDownloader.Api.Services.FFmpeg;

namespace YoutubeDownloader.Api.Definitions.DependencyContainer;

public class ContainerDefinition : AppDefinition
{
    public override void ConfigureServices(WebApplicationBuilder builder)
    {
        builder.Services.AddHostedService<VideoDownloaderBackgroundService>();
        //builder.Services.AddHostedService<TelegramBotService>();
        builder.Services.AddSingleton<DownloadService>();
        builder.Services.AddTransient<YoutubeClient>();
        builder.Services.AddTransient<FFmpeg>();
        builder.Services.AddTransient<FFmpegConverter>();
        builder.Services.AddSingleton<YoutubeDownloadService>();
    }
}

namespace YoutubeDownloader.Api.Services;

public class VideoDownloaderBackgroundService(ILogger<VideoDownloaderBackgroundService> logger, DownloadService downloadService) : BackgroundService
{
    private readonly PeriodicTimer _timer = new(TimeSpan.FromSeconds(5));

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        _timer.Dispose();
        await base.StopAsync(stoppingToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("{Name} is starting", nameof(VideoDownloaderBackgroundService));

        stoppingToken.Register(() => logger.LogInformation("{Name} is stopping", nameof(VideoDownloaderBackgroundService)));

        while (await _timer.WaitForNextTickAsync(stoppingToken))
        {
            if (downloadService.IsNeedDownloadAny)
            {
                await downloadService.DownloadFromQueue();
            }
        }
    }
}

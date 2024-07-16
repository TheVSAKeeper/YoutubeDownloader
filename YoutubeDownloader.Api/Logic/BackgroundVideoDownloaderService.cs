namespace YoutubeDownloader.Api.Logic;

public class BackgroundVideoDownloaderService : IHostedService, IDisposable
{
    private readonly DownloadManager _downloadManager;
    private readonly ILogger<BackgroundVideoDownloaderService> _logger;
    private bool _isInProcess;
    private int executionCount;
    private Timer? _timer;

    public BackgroundVideoDownloaderService(ILogger<BackgroundVideoDownloaderService> logger, DownloadManager downloadManager)
    {
        _logger = logger;
        _downloadManager = downloadManager;
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }

    public async Task StartAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BackgroundVideoDownloaderService running.");

        _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
    }

    public async Task StopAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BackgroundVideoDownloaderService is stopping.");

        _timer?.Change(Timeout.Infinite, 0);
    }

    private void DoWork(object? state)
    {
        if (_isInProcess)
        {
            return;
        }

        _isInProcess = true;

        int count = Interlocked.Increment(ref executionCount);
        _downloadManager.DownloadFromQueue().GetAwaiter().GetResult();

        _isInProcess = false;
    }
}
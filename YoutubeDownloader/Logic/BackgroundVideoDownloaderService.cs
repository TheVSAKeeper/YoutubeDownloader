namespace YoutubeDownloader.Logic
{
    public class BackgroundVideoDownloaderService : IHostedService, IDisposable
    {
        private int executionCount = 0;
        private readonly ILogger<BackgroundVideoDownloaderService> _logger;
        private readonly DownloadManager _downloadManager;
        private Timer? _timer = null;
        private bool _isInProcess = false;

        public BackgroundVideoDownloaderService(ILogger<BackgroundVideoDownloaderService> logger, DownloadManager downloadManager)
        {
            _logger = logger;
            _downloadManager = downloadManager;
        }

        public async Task StartAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("BackgroundVideoDownloaderService running.");

            _timer = new Timer(DoWork, null, TimeSpan.Zero,
                TimeSpan.FromSeconds(5));
        }

        private void DoWork(object? state)
        {
            if (_isInProcess)
            {
                return;
            }
            _isInProcess = true;

            var count = Interlocked.Increment(ref executionCount);
            _downloadManager.DownloadFromQueue().GetAwaiter().GetResult();

            _isInProcess = false;
        }

        public async Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("BackgroundVideoDownloaderService is stopping.");

            _timer?.Change(Timeout.Infinite, 0);
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}

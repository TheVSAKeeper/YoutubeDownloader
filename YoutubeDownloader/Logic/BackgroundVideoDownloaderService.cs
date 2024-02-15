namespace YoutubeDownloader.Logic
{
    public class BackgroundVideoDownloaderService : IHostedService, IDisposable
    {
        private int executionCount = 0;
        private readonly ILogger<BackgroundVideoDownloaderService> _logger;
        private Timer? _timer = null;
        private bool _isInProcess = false;

        public BackgroundVideoDownloaderService(ILogger<BackgroundVideoDownloaderService> logger)
        {
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Timed Hosted Service running.");

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
            var error = Globals.DownloadManager.DownloadFromQueue().GetAwaiter().GetResult();
            if (error != null)
            {
                _logger.LogInformation("Download exception: " + error);
            }

            _isInProcess = false;
        }

        public async Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Timed Hosted Service is stopping.");

            _timer?.Change(Timeout.Infinite, 0);
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}

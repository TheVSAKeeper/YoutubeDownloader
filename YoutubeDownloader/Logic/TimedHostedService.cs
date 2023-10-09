using YoutubeDownloader.Controllers;

namespace YoutubeDownloader.Logic
{
    public class TimedHostedService : IHostedService, IDisposable
    {
        private int executionCount = 0;
        private readonly ILogger<TimedHostedService> _logger;
        private Timer? _timer = null;
        private bool _isInProcess = false;

        public TimedHostedService(ILogger<TimedHostedService> logger)
        {
            _logger = logger;
        }

        public Task StartAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Timed Hosted Service running.");

            _timer = new Timer(DoWork, null, TimeSpan.Zero,
                TimeSpan.FromSeconds(5));

            return Task.CompletedTask;
        }

        private void DoWork(object? state)
        {
            if (_isInProcess)
            {
                return;
            }
            _isInProcess = true;

            var count = Interlocked.Increment(ref executionCount);
            Globals.DownloadManager.DownloadFromQueue().GetAwaiter().GetResult();
            _logger.LogInformation(
                "Timed Hosted Service is working. Count: {Count}", count);

            _isInProcess = false;
        }

        public Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Timed Hosted Service is stopping.");

            _timer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}

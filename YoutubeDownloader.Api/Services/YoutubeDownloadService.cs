using System.Diagnostics;
using YoutubeExplode;

namespace YoutubeDownloader.Api.Services;

public class YoutubeDownloadService(YoutubeClient youtubeClient, ILogger<YoutubeDownloadService> logger)
{
    public ValueTask DownloadAsync(IStreamInfo stream, string path, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        return youtubeClient.Videos.Streams.DownloadAsync(stream, path, progress, cancellationToken);
    }

    public ValueTask DownloadWithProgressAsync(IStreamInfo streamInfo, string path, string streamTitle, string videoTitle, CancellationToken cancellationToken)
    {
        double oldPercent = -1;
        long totalBytesDownloaded = 0;
        Stopwatch stopwatch = Stopwatch.StartNew();

        string streamType = streamInfo switch
        {
            AudioOnlyStreamInfo => "Audio",
            VideoOnlyStreamInfo => "Video",
            MuxedStreamInfo => "Muxed",
            var _ => "Unknown"
        };

        Progress<double> progress = new(percent =>
        {
            if (percent - oldPercent < 0.02)
            {
                return;
            }

            logger.LogDebug("{StreamType}: {Percent:P2}\t{StreamTitle}\t{VideoTitle}", streamType, percent, streamTitle, videoTitle);
            oldPercent = percent;

            long bytesDownloaded = (long)(streamInfo.Size.MegaBytes * percent);
            long bytesThisUpdate = bytesDownloaded - totalBytesDownloaded;
            totalBytesDownloaded = bytesDownloaded;

            double elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
            double speedInBytesPerSecond = elapsedSeconds > 0 ? bytesThisUpdate / elapsedSeconds : 0;

            logger.LogInformation("{StreamType}: Speed: {Speed:F3} MegaBytes/sec\t{StreamTitle}\t{VideoTitle}", streamType, speedInBytesPerSecond, streamTitle, videoTitle);
        });

        return DownloadAsync(streamInfo, path, progress, cancellationToken);
    }

    public ValueTask DownloadWithProgressAsync(DownloadItemStream downloadStream, CancellationToken token)
    {
        return downloadStream.Stream != null
            ? DownloadWithProgressAsync(downloadStream.Stream, downloadStream.TempPath, downloadStream.Title, downloadStream.FileName, token)
            : ValueTask.CompletedTask;
    }

    public ValueTask<StreamManifest> GetStreamManifestAsync(string url)
    {
        return youtubeClient.Videos.Streams.GetManifestAsync(url);
    }

    public ValueTask<Video> GetVideoAsync(string url)
    {
        return youtubeClient.Videos.GetAsync(url);
    }
}

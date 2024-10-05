using Microsoft.Extensions.Logging;
using System.Diagnostics;
using YoutubeChannelDownloader.Models;
using YoutubeExplode;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;

namespace YoutubeChannelDownloader;

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
            var _ => "Unknown",
        };

        Progress<double> progress = new(percent =>
        {
            if (percent - oldPercent < 0.1)
            {
                return;
            }

            long bytesDownloaded = (long)(streamInfo.Size.MegaBytes * percent);
            long bytesThisUpdate = bytesDownloaded - totalBytesDownloaded;
            totalBytesDownloaded = bytesDownloaded;

            double elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
            double speedInBytesPerSecond = elapsedSeconds > 0 ? bytesThisUpdate / elapsedSeconds : 0;

            logger.LogDebug("{StreamType}: {Percent:P2}\t{Speed:F3} MB/s\t{StreamTitle}\t{VideoTitle}", streamType, percent, speedInBytesPerSecond, streamTitle, videoTitle);
            oldPercent = percent;
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

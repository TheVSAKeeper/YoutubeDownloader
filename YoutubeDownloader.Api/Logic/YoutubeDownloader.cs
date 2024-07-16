using YoutubeExplode;
using YoutubeExplode.Videos.Streams;

namespace YoutubeDownloader.Api.Logic;

public class YoutubeDownloader
{
    public static async Task Download(IStreamInfo stream, string path, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        YoutubeClient youtube = new();
        await youtube.Videos.Streams.DownloadAsync(stream, path, progress, cancellationToken);
    }
}
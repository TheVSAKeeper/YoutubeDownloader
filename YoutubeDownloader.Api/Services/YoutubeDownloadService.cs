using YoutubeExplode;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;

namespace YoutubeDownloader.Api.Services;

public class YoutubeDownloadService(YoutubeClient youtubeClient)
{
    public async Task Download(IStreamInfo stream, string path, IProgress<double>? progress, CancellationToken cancellationToken) =>
        await youtubeClient.Videos.Streams.DownloadAsync(stream, path, progress, cancellationToken);

    public async Task<StreamManifest> GetStreamManifestAsync(string url) =>
        await youtubeClient.Videos.Streams.GetManifestAsync(url);

    public async Task<Video> GetVideoAsync(string url) =>
        await youtubeClient.Videos.GetAsync(url);
}
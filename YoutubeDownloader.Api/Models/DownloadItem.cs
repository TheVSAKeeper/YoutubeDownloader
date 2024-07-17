using YoutubeExplode.Videos;

namespace YoutubeDownloader.Api.Models;

public class DownloadItem
{
    private readonly List<DownloadItemSteam> _streams;

    public DownloadItem(Guid id, string url, IEnumerable<DownloadItemSteam> streams, Video video)
    {
        Id = id;
        Url = url;
        _streams = streams.ToList();
        Video = video;
    }

    public Guid Id { get; }
    public string Url { get; }

    public IEnumerable<DownloadItemSteam> Streams => _streams;

    public Video Video { get; }

    public bool IsNeedDownloadAnyStream => Streams.Any(steam => steam.IsNeedDownload);

    public Operation<DownloadItemSteam, string> GetStream(int id)
    {
        DownloadItemSteam? itemSteam = Streams.FirstOrDefault(downloadItem => downloadItem.Id == id);

        return itemSteam is not null ? Operation.Result(itemSteam) : Operation.Error<string>($"DownloadItemSteam c id {id} не найден");
    }
}
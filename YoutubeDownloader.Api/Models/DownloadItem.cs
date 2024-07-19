namespace YoutubeDownloader.Api.Models;

public class DownloadItem
{
    private readonly List<DownloadItemSteam> _streams;

    private DownloadItem(Guid id, string url, List<DownloadItemSteam> streams, Video video)
    {
        Id = id;
        Url = url;
        _streams = streams;
        Video = video;
    }

    public Guid Id { get; }
    public string Url { get; }

    public IEnumerable<DownloadItemSteam> Streams => _streams;

    public Video Video { get; }

    public bool IsNeedDownloadAnyStream => Streams.Any(steam => steam.IsNeedDownload);

    public static Operation<DownloadItem, string> Create(Guid id, string url, IEnumerable<DownloadItemSteam> streams, Video video)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return Operation.Error<string>("URL не может быть null или пустым.");
        }

        List<DownloadItemSteam> streamsList = streams.ToList();

        if (streamsList.Count == 0)
        {
            return Operation.Error<string>("Список потоков не может быть пустым.");
        }

        return new DownloadItem(id, url, streamsList, video);
    }

    public Operation<DownloadItemSteam, string> GetStream(int id)
    {
        DownloadItemSteam? itemSteam = Streams.FirstOrDefault(downloadItem => downloadItem.Id == id);

        return itemSteam is not null
            ? Operation.Result(itemSteam)
            : Operation.Error<string>($"DownloadItemSteam c id {id} не найден");
    }
}
namespace YoutubeDownloader.Api.Models;

public class DownloadItem
{
    private readonly List<DownloadItemStream> _streams;

    private DownloadItem(Guid id, string url, IEnumerable<DownloadItemStream> streams, Video video)
    {
        Id = id;
        Url = url;
        _streams = streams.ToList();
        Video = video;
    }

    public Guid Id { get; }
    public string Url { get; }

    public IEnumerable<DownloadItemStream> Streams => _streams;

    public Video Video { get; }

    public bool IsNeedDownloadAnyStream => Streams.Any(stream => stream.IsNeedDownload);

    public static Operation<DownloadItem, string> Create(Guid id, string url, IEnumerable<DownloadItemStream> streams, Video video)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return Operation.Error<string>("URL не может быть null или пустым.");
        }

        List<DownloadItemStream> streamsList = streams.ToList();

        if (streamsList.Count == 0)
        {
            return Operation.Error<string>("Список потоков не может быть пустым.");
        }

        return new DownloadItem(id, url, streamsList, video);
    }

    public Operation<DownloadItemStream, string> GetStream(int id)
    {
        DownloadItemStream? itemSteam = Streams.FirstOrDefault(downloadItem => downloadItem.Id == id);

        return itemSteam is not null
            ? Operation.Result(itemSteam)
            : Operation.Error<string>($"DownloadItemStream c id {id} не найден");
    }

    public IEnumerable<DownloadItemStream> GetWaitStreams() =>
        Streams.Where(stream => stream.State == DownloadItemState.Wait);
}
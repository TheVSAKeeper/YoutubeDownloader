namespace YoutubeChannelDownloader.Models;

public class StateModel
{
    private readonly List<StreamModel> _streams;

    private StateModel(string downloadId, string title, IEnumerable<StreamModel> streams)
    {
        DownloadId = downloadId;
        Title = title;
        _streams = streams.ToList();
    }

    public string DownloadId { get; }

    public string Title { get; }
    public IEnumerable<StreamModel> Streams => _streams;

    public static StateModel Create(DownloadItem item)
    {
        string title = item.Video.Title;
        IEnumerable<StreamModel> models = StreamModel.Create(item.Streams);
        return new StateModel(item.Id, title, models);
    }
}

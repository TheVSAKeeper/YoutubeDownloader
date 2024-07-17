namespace YoutubeDownloader.Api.Models;

public class StateModel
{
    private readonly List<StreamModel> _streams;

    private StateModel(Guid downloadId, string title, IEnumerable<StreamModel> streams)
    {
        DownloadId = downloadId;
        Title = title;
        _streams = streams.ToList();
    }

    public Guid DownloadId { get; }

    public string Title { get; }
    public IEnumerable<StreamModel> Streams => _streams;

    public static Operation<StateModel> Create(DownloadItem item)
    {
        string title = item.Video.Title;

        Operation<IEnumerable<StreamModel>> operation = StreamModel.Create(item.Streams);

        if (operation.Ok == false)
        {
            return Operation.Error();
        }

        StateModel model = new(item.Id, title, operation.Result);

        return model;
    }
}
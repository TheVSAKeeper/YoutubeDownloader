using CSharpFunctionalExtensions;

namespace YoutubeDownloader.Api.Models;

public class StateModel
{
    private StateModel(Guid downloadId, string title, IEnumerable<StreamModel> streams)
    {
        DownloadId = downloadId;
        Title = title;
        Streams = streams.ToArray();
    }

    public Guid DownloadId { get; }

    public string Title { get; }

    public StreamModel[] Streams { get; }

    public static Result<StateModel> Create(DownloadItem item)
    {
        string title = item.Video.Title;

        StateModel model = new(item.Id, title, StreamModel.Create(item.Streams).Value);

        return model;
    }
}
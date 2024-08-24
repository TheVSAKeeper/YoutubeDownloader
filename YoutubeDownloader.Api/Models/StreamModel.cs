namespace YoutubeDownloader.Api.Models;

public class StreamModel
{
    private StreamModel(int id, string state, string title)
    {
        Id = id;
        State = state;
        Title = title;
    }

    public int Id { get; }

    public string State { get; }

    public string Title { get; }

    public static Operation<StreamModel> Create(DownloadItemStream item)
    {
        return new StreamModel(item.Id, item.State.ToString(), item.Title);
    }

    public static Operation<IEnumerable<StreamModel>> Create(IEnumerable<DownloadItemStream> steams)
    {
        return Operation.Result(steams.Select(steam => Create(steam).Result));
    }
}

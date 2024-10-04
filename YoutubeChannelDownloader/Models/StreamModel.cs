namespace YoutubeChannelDownloader.Models;

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

    public static StreamModel Create(DownloadItemStream item)
    {
        return new StreamModel(item.Id, item.State.ToString(), item.Title);
    }

    public static IEnumerable<StreamModel> Create(IEnumerable<DownloadItemStream> steams)
    {
        return steams.Select(Create);
    }
}

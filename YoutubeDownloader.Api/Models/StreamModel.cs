using CSharpFunctionalExtensions;

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

    public static Result<StreamModel> Create(DownloadItemSteam item) =>
        new StreamModel(item.Id, item.State.ToString(), item.Title);

    public static Result<IEnumerable<StreamModel>> Create(IEnumerable<DownloadItemSteam> steams) =>
        Result.Success(steams.Select(steam => Create(steam).Value));
}
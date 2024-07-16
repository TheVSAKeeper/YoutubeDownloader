using CSharpFunctionalExtensions;
using YoutubeExplode.Videos;

namespace YoutubeDownloader.Api.Models;

public class DownloadItem
{
    public Guid Id { get; set; }
    public string Url { get; set; }
    public List<DownloadItemSteam> Streams { get; set; }
    public Video Video { get; set; }

    public Result<DownloadItemSteam> GetStream(int id)
    {
        DownloadItemSteam? item = Streams.FirstOrDefault(downloadItem => downloadItem.Id == id);

        return item ?? Result.Failure<DownloadItemSteam>($"DownloadItemSteam c id {id} не найден");
    }
}
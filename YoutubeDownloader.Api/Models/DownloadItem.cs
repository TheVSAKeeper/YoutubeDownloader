using Calabonga.OperationResults;
using YoutubeExplode.Videos;

namespace YoutubeDownloader.Api.Models;

public class DownloadItem
{
    public Guid Id { get; set; }
    public string Url { get; set; }
    public List<DownloadItemSteam> Streams { get; set; }
    public Video Video { get; set; }

    public string FileName => $"{Video.Title}.{Video}";

    public Operation<DownloadItemSteam, string> GetStream(int id)
    {
        DownloadItemSteam? itemSteam = Streams.FirstOrDefault(downloadItem => downloadItem.Id == id);

        return itemSteam is not null ? Operation.Result(itemSteam) : Operation.Error<string>($"DownloadItemSteam c id {id} не найден");
    }
}
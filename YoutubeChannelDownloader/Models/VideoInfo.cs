namespace YoutubeChannelDownloader.Models;

public class VideoInfo(string title, string fileName, VideoState state, string url, string? thumbnailUrl, string playlistId)
{
    public string Title { get; init; } = title;
    public string FileName { get; init; } = fileName;
    public VideoState State { get; set; } = state;
    public string Url { get; init; } = url;
    public string? ThumbnailUrl { get; init; } = thumbnailUrl;
    public string PlaylistId { get; init; } = playlistId;
}

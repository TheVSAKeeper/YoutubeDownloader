namespace YoutubeChannelDownloader.Models;

public enum VideoStatus
{
    /// <summary>
    ///     Видео еще не загружено
    /// </summary>
    NotDownloaded = 0,

    /// <summary>
    ///     Видео успешно загружено
    /// </summary>
    Downloaded = 1,

    /// <summary>
    ///     Ошибка при загрузке
    /// </summary>
    Error = 2,
}

public class VideoInfo(string title, string fileName, VideoStatus status, string url, string? thumbnailUrl, string playlistId)
{
    public string Title { get; init; } = title;
    public string FileName { get; init; } = fileName;
    public VideoStatus Status { get; set; } = status;
    public string Url { get; init; } = url;
    public string? ThumbnailUrl { get; init; } = thumbnailUrl;
    public string PlaylistId { get; init; } = playlistId;
}

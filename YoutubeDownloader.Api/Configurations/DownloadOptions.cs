namespace YoutubeDownloader.Api.Configurations;

public class DownloadOptions
{
    private string? _videoFolderPath;

    public required string VideoFolderPath { get; init; }

    public string FullVideoFolderPath => _videoFolderPath ??= IsRelativePath
        ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, VideoFolderPath)
        : VideoFolderPath;

    public required bool IsRelativePath { get; init; }
}
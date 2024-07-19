using YoutubeExplode.Videos.Streams;

namespace YoutubeDownloader.Api.Models;

public class DownloadItemSteam
{
    private double? _sizeMegaBytes;
    private string? _title;
    private string? _videoType;

    public required int Id { get; set; }
    public required string TempName { get; set; }
    public required string TempPath { get; set; }
    public required string FileName { get; set; }
    public required string FilePath { get; set; }
    public DownloadItemState State { get; set; } = DownloadItemState.Added;
    public IStreamInfo Stream { get; set; }

    public bool IsCombineAfterDownload => AudioStreamInfo is not null && VideoStreamInfo is not null;
    public IAudioStreamInfo? AudioStreamInfo { get; set; }
    public IVideoStreamInfo? VideoStreamInfo { get; set; }

    public bool IsNeedDownload => State == DownloadItemState.Wait;

    public string Title => _title ??= IsCombineAfterDownload
        ? $"Muxed (custom) ({VideoStreamInfo!.VideoQuality.MaxHeight} | {VideoStreamInfo.Container.Name}) ~{SizeMegaBytes}МБ"
        : $"{Stream} {SizeMegaBytes}МБ";

    public double SizeMegaBytes => _sizeMegaBytes ??= IsCombineAfterDownload
        ? Math.Round(AudioStreamInfo!.Size.MegaBytes + VideoStreamInfo!.Size.MegaBytes, 2)
        : Math.Round(Stream.Size.MegaBytes, 2);

    public string VideoType => _videoType ??= IsCombineAfterDownload
        ? VideoStreamInfo!.Container.Name
        : Stream.Container.Name;
}
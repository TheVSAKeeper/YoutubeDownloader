using YoutubeExplode.Videos.Streams;

namespace YoutubeDownloader.Api.Models;

public class DownloadItemSteam
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string FullPath { get; set; }
    public DownloadItemState State { get; set; }
    public IStreamInfo Stream { get; set; }

    public bool IsCombineAfterDownload { get; set; }
    public IStreamInfo CombineAfterDownloadStreamAudio { get; set; }
    public IStreamInfo CombineAfterDownloadStreamVideo { get; set; }

    public bool IsNeedDownload => State == DownloadItemState.Wait;

    public string Title
    {
        get
        {
            if (IsCombineAfterDownload)
            {
                VideoOnlyStreamInfo video = (VideoOnlyStreamInfo)CombineAfterDownloadStreamVideo;
                return $"Muxed ({video.VideoQuality.MaxHeight} | {video.Container.Name}) ~{SizeMb}МБ";
            }

            return $"{Stream} {SizeMb}МБ";
        }
    }

    public double SizeMb
    {
        get
        {
            if (IsCombineAfterDownload)
            {
                double size = CombineAfterDownloadStreamAudio.Size.MegaBytes + CombineAfterDownloadStreamVideo.Size.MegaBytes;
                return Math.Round(size, 2);
            }

            return Math.Round(Stream.Size.MegaBytes, 2);
        }
    }

    public string VideoType
    {
        get
        {
            if (IsCombineAfterDownload)
            {
                VideoOnlyStreamInfo video = (VideoOnlyStreamInfo)CombineAfterDownloadStreamVideo;
                return video.Container.Name;
            }

            return Stream.Container.Name;
        }
    }

    public Action? AfterDownloadAction { get; set; }
    public string FileNamePath { get; set; }
    public string FileName { get; set; }
}
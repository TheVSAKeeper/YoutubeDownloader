using System.Diagnostics;
using System.Text;
using YoutubeExplode;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;

namespace YoutubeDownloader.Api.Logic;

public class DownloadManager(ILogger<DownloadManager> logger)
{
    private readonly List<DownloadItem> _items = [];

    public IEnumerable<DownloadItem> Items => _items;

    public async Task<DownloadItem> AddToQueueAsync(string url)
    {
        logger.LogTrace("Try add to queue: " + url);
        YoutubeClient youtube = new();

        Video video = await youtube.Videos.GetAsync(url);

        StreamManifest streamManifest = await youtube.Videos.Streams.GetManifestAsync(url);
        IVideoStreamInfo streamInfo = streamManifest.GetMuxedStreams().GetWithHighestVideoQuality();

        Guid id = Guid.NewGuid();

        List<DownloadItemSteam> streams = streamManifest.Streams.Select((x, i) =>
                new DownloadItemSteam
                {
                    Id = i,
                    Name = $"{id}_{i}.mp4",
                    FullPath = Path.Combine(Globals.Settings.VideoFolderPath, $"{id}_{i}.mp4"),
                    Stream = streamManifest.Streams[i],
                    State = DownloadItemState.Base
                })
            .ToList();

        int streamId = streams.Count;

        List<string> types = ["webm", "mp4"];

        foreach (string type in types)
        {
            DownloadItemSteam? bestMuxedStream = streams
                .Where(x => x.Stream != null && x.Stream.Container.Name == type && x.Stream is MuxedStreamInfo)
                .MaxBy(x => ((MuxedStreamInfo)x.Stream).VideoQuality.MaxHeight);

            DownloadItemSteam? bestVideoStream = streams
                .Where(x => x.Stream != null && x.Stream.Container.Name == type && x.Stream is VideoOnlyStreamInfo)
                .MaxBy(x => ((VideoOnlyStreamInfo)x.Stream).VideoQuality.MaxHeight);

            DownloadItemSteam? bestAudioStream = streams
                .Where(x => x.Stream != null && x.Stream.Container.Name == type && x.Stream is AudioOnlyStreamInfo)
                .MaxBy(x => ((AudioOnlyStreamInfo)x.Stream).Size);

            int muxedMaxHeight = 0;

            if (bestMuxedStream != null)
            {
                muxedMaxHeight = ((MuxedStreamInfo)bestMuxedStream.Stream).VideoQuality.MaxHeight;
            }

            if (bestVideoStream == null || bestAudioStream == null)
            {
                continue;
            }

            if (muxedMaxHeight >= ((VideoOnlyStreamInfo)bestVideoStream.Stream).VideoQuality.MaxHeight)
            {
                continue;
            }

            streams.Insert(0, new DownloadItemSteam
            {
                Id = streamId,
                Name = $"{id}_{streamId}.{type}",
                FullPath = Path.Combine(Globals.Settings.VideoFolderPath, $"{id}_{streamId}.{type}"),
                FileName = Path.Combine(Globals.Settings.VideoFolderPath, $"{video.Title}.{type}"),
                State = DownloadItemState.Base,
                IsCombineAfterDownload = true,
                CombineAfterDownloadStreamAudio = bestAudioStream.Stream,
                CombineAfterDownloadStreamVideo = bestVideoStream.Stream
            });

            streamId++;
        }

        DownloadItem item = new()
        {
            Id = id,
            Url = url,
            Video = video,
            Streams = streams
        };

        _items.Add(item);
        logger.LogTrace("Add to queue: " + url + " " + id);
        return item;
    }

    public void SetStreamToDownload(Guid downloadId, int streamId, Action afterDownloadAction = null)
    {
        logger.LogTrace("Try set stream to download: " + downloadId + " " + streamId);
        DownloadItem downloadItem = _items.First(x => x.Id == downloadId);
        DownloadItemSteam stream = downloadItem.Streams.First(x => x.Id == streamId);
        stream.State = DownloadItemState.Wait;
        stream.AfterDownloadAction = afterDownloadAction;
        logger.LogTrace("Set stream to download: " + downloadId + " " + streamId);
    }

    public async Task DownloadFromQueue()
    {
        DownloadItem? downloadItem = _items.FirstOrDefault(x => x.Streams.Any(itemSteam => itemSteam.State == DownloadItemState.Wait));

        if (downloadItem == null)
        {
            return;
        }

        DownloadItemSteam downloadStream = downloadItem.Streams.First(x => x.State == DownloadItemState.Wait);
        downloadStream.State = DownloadItemState.InProcess;
        logger.LogTrace("Try download from queue: " + downloadItem.Id + " " + downloadStream.Id);

        try
        {
            CancellationTokenSource cancellationTokenSource = new();

            if (downloadStream.IsCombineAfterDownload)
            {
                string audioPath = $"{downloadStream.FullPath}_audio.{downloadStream.CombineAfterDownloadStreamVideo.Container.Name}";
                string videoPath = $"{downloadStream.FullPath}_video.{downloadStream.CombineAfterDownloadStreamVideo.Container.Name}";

                (double, double) old = (0d, 0d);

                Task task = YoutubeDownloader.Download(downloadStream.CombineAfterDownloadStreamAudio, audioPath, new Progress<double>(percent =>
                {
                    if (percent - old.Item1 < 0.02)
                    {
                        return;
                    }

                    logger.LogTrace("Audio: {Percent:P}\t{VideoTitle}/{StreamId}", percent, downloadItem.Video.Title, downloadStream.Id);
                    old.Item1 = percent;
                }), cancellationTokenSource.Token);

                Task task2 = YoutubeDownloader.Download(downloadStream.CombineAfterDownloadStreamVideo, videoPath, new Progress<double>(percent =>
                {
                    if (percent - old.Item2 < 0.02)
                    {
                        return;
                    }

                    logger.LogTrace("Video: {Percent:P}\t{VideoTitle}/{StreamId}", percent, downloadItem.Video.Title, downloadStream.Id);
                    old.Item2 = percent;
                }), cancellationTokenSource.Token);

                Task.WaitAll(task, task2);

                logger.LogTrace("Try merge video and audio: " + downloadItem.Id + " " + downloadStream.Id);

                string args = $"""
                               -i "{videoPath}" -i "{audioPath}" -c copy "{downloadStream.FileName}"
                               """;

                await RunAsync(args);
            }
            else
            {
                await YoutubeDownloader.Download(downloadStream.Stream, downloadStream.FullPath, null, cancellationTokenSource.Token);
            }

            downloadStream.State = DownloadItemState.Ready;

            if (downloadStream.AfterDownloadAction != null)
            {
                try
                {
                    // todo пока отправляем видева пользователю, следующий видос не качается
                    downloadStream.AfterDownloadAction();
                }
                catch
                {
                    // todo обработку ошибок доверим автору колбэка, а тут как бы на всякий влепим
                }
            }

            logger.LogTrace("Download from queue: " + downloadItem.Id + " " + downloadStream.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Download from queue: " + downloadItem.Id + " " + downloadStream.Id + " " + ex.Message);
            downloadStream.State = DownloadItemState.Error;
        }
    }

    public async Task RunAsync(string ffmpegCommand)
    {
        using Process process = new();

        ProcessStartInfo processStartInfo2 = process.StartInfo = new ProcessStartInfo
        {
            WindowStyle = ProcessWindowStyle.Hidden,
            FileName = @"C:\Programs\ffmpeg\ffmpeg.exe",
            RedirectStandardError = true,
            Arguments = ffmpegCommand
        };

        process.Start();

        string? lastLine = null;
        StringBuilder runMessage = new();

        while (!process.StandardError.EndOfStream)
        {
            string text = await process.StandardError.ReadLineAsync().ConfigureAwait(continueOnCapturedContext: false);
            runMessage.AppendLine(text);
            lastLine = text;
        }

        await process.WaitForExitAsync().ConfigureAwait(continueOnCapturedContext: false);

        if (process.ExitCode != 0)
        {
        }
    }

    public class DownloadItem
    {
        public Guid Id { get; set; }
        public string Url { get; set; }
        public List<DownloadItemSteam> Streams { get; set; }
        public Video Video { get; internal set; }
    }

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
        public string FileName { get; set; }
    }
}
using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Options;
using YoutubeDownloader.Api.Configurations;
using YoutubeDownloader.Api.Models;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;

namespace YoutubeDownloader.Api.Services;

public class DownloadService(ILogger<DownloadService> logger, YoutubeDownloadService youtubeDownloadService, IOptions<DownloadOptions> options)
{
    private readonly List<DownloadItem> _items = [];

    public IEnumerable<DownloadItem> Items => _items;

    public bool IsNeedDownloadAny => _items.Any(item => item.IsNeedDownloadAnyStream);

    public Operation<DownloadItem, string> FindItem(Guid id)
    {
        if (id == Guid.Empty)
        {
            return Operation.Error<string>("Id не может быть пустым");
        }

        DownloadItem? item = Items.FirstOrDefault(downloadItem => downloadItem.Id == id);

        return item is not null ? Operation.Result(item) : Operation.Error<string>($"DownloadItem c id {id} не найден");
    }

    public async Task<DownloadItem> AddToQueueAsync(string url)
    {
        logger.LogDebug("Try add to queue: {Url}", url);

        Video video = await youtubeDownloadService.GetVideoAsync(url);

        StreamManifest streamManifest = await youtubeDownloadService.GetStreamManifestAsync(url);
        IVideoStreamInfo streamInfo = streamManifest.GetMuxedStreams().GetWithHighestVideoQuality();

        Guid id = Guid.NewGuid();

        List<DownloadItemSteam> streams = streamManifest.Streams.Select((stream, i) =>
                new DownloadItemSteam
                {
                    Id = i,
                    Name = $"{id}_{i}.mp4",
                    FullPath = Path.Combine(options.Value.FullVideoFolderPath, $"{id}_{i}.mp4"),
                    Stream = stream,
                    State = DownloadItemState.Added
                })
            .ToList();

        int streamId = streams.Count;

        List<string> types = ["webm", "mp4"];

        foreach (string type in types)
        {
            DownloadItemSteam? bestMuxedStream = streams
                .Where(x => x.Stream.Container.Name == type && x.Stream is MuxedStreamInfo)
                .MaxBy(x => ((MuxedStreamInfo)x.Stream).VideoQuality.MaxHeight);

            DownloadItemSteam? bestVideoStream = streams
                .Where(x => x.Stream.Container.Name == type && x.Stream is VideoOnlyStreamInfo)
                .MaxBy(x => ((VideoOnlyStreamInfo)x.Stream).VideoQuality.MaxHeight);

            DownloadItemSteam? bestAudioStream = streams
                .Where(x => x.Stream.Container.Name == type && x.Stream is AudioOnlyStreamInfo)
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
                FullPath = Path.Combine(options.Value.FullVideoFolderPath, $"{id}_{streamId}.{type}"),
                FileNamePath = Path.Combine(options.Value.FullVideoFolderPath, $"{video.Title}.{type}"),
                FileName = $"{video.Title}.{type}",
                State = DownloadItemState.Added,
                IsCombineAfterDownload = true,
                CombineAfterDownloadStreamAudio = bestAudioStream.Stream,
                CombineAfterDownloadStreamVideo = bestVideoStream.Stream
            });

            streamId++;
        }

        DownloadItem item = new(id, url, streams, video);

        _items.Add(item);
        logger.LogDebug("Add to queue: " + url + " " + id);
        return item;
    }

    public void SetStreamToDownload(Guid downloadId, int streamId, Action afterDownloadAction = null)
    {
        logger.LogDebug("Try set stream to download: " + downloadId + " " + streamId);
        DownloadItem downloadItem = _items.First(x => x.Id == downloadId);
        DownloadItemSteam stream = downloadItem.Streams.First(x => x.Id == streamId);
        stream.State = DownloadItemState.Wait;
        stream.AfterDownloadAction = afterDownloadAction;
        logger.LogDebug("Set stream to download: " + downloadId + " " + streamId);
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
        logger.LogDebug("Try download from queue: " + downloadItem.Id + " " + downloadStream.Id);

        try
        {
            CancellationTokenSource cancellationTokenSource = new();

            if (downloadStream.IsCombineAfterDownload)
            {
                string audioPath = $"{downloadStream.FullPath}_audio.{downloadStream.CombineAfterDownloadStreamVideo.Container.Name}";
                string videoPath = $"{downloadStream.FullPath}_video.{downloadStream.CombineAfterDownloadStreamVideo.Container.Name}";

                (double, double) old = (0d, 0d);

                Task task = youtubeDownloadService.Download(downloadStream.CombineAfterDownloadStreamAudio, audioPath, new Progress<double>(percent =>
                {
                    if (percent - old.Item1 < 0.02)
                    {
                        return;
                    }

                    logger.LogDebug("Audio: {Percent:P}\t{VideoTitle}/{StreamId}", percent, downloadItem.Video.Title, downloadStream.Id);
                    old.Item1 = percent;
                }), cancellationTokenSource.Token);

                Task task2 = youtubeDownloadService.Download(downloadStream.CombineAfterDownloadStreamVideo, videoPath, new Progress<double>(percent =>
                {
                    if (percent - old.Item2 < 0.02)
                    {
                        return;
                    }

                    logger.LogDebug("Video: {Percent:P}\t{VideoTitle}/{StreamId}", percent, downloadItem.Video.Title, downloadStream.Id);
                    old.Item2 = percent;
                }), cancellationTokenSource.Token);

                Task.WaitAll(task, task2);

                logger.LogDebug("Try merge video and audio: " + downloadItem.Id + " " + downloadStream.Id);

                string args = $"""
                               -i "{videoPath}" -i "{audioPath}" -c copy "{downloadStream.FileNamePath}"
                               """;

                await RunAsync(args);
            }
            else
            {
                await youtubeDownloadService.Download(downloadStream.Stream, downloadStream.FullPath, null, cancellationTokenSource.Token);
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

            logger.LogDebug("Download from queue: " + downloadItem.Id + " " + downloadStream.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Download from queue: " + downloadItem.Id + " " + downloadStream.Id + " " + ex.Message);
            downloadStream.State = DownloadItemState.Error;
        }
    }

    private static async Task RunAsync(string ffmpegCommand)
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
            string? text = await process.StandardError.ReadLineAsync().ConfigureAwait(continueOnCapturedContext: false);
            runMessage.AppendLine(text);
            lastLine = text;
        }

        await process.WaitForExitAsync().ConfigureAwait(continueOnCapturedContext: false);

        if (process.ExitCode != 0)
        {
        }
    }
}
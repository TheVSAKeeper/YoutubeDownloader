using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using YoutubeDownloader.Api.Application;
using YoutubeDownloader.Api.Configurations;

namespace YoutubeDownloader.Api.Services;

public class DownloadService
{
    private readonly List<DownloadItem> _items = [];
    private readonly ILogger<DownloadService> _logger;
    private readonly DownloadOptions _options;
    private readonly YoutubeDownloadService _youtubeDownloadService;

    public DownloadService(ILogger<DownloadService> logger, YoutubeDownloadService youtubeDownloadService, IOptions<DownloadOptions> options)
    {
        _logger = logger;
        _youtubeDownloadService = youtubeDownloadService;
        _options = options.Value;

        RefreshDirectories();
    }

    public bool IsNeedDownloadAny => _items.Any(item => item.IsNeedDownloadAnyStream);

    public Operation<DownloadItem, string> FindItem(Guid id)
    {
        if (id == Guid.Empty)
        {
            return Operation.Error<string>("Id не может быть пустым");
        }

        DownloadItem? item = _items.FirstOrDefault(downloadItem => downloadItem.Id == id);

        return item is not null
            ? Operation.Result(item)
            : Operation.Error<string>($"DownloadItem c id {id} не найден");
    }

    public async Task<DownloadItem> AddToQueueAsync(string url)
    {
        _logger.LogDebug("Попытка добавить в очередь: {Url}", url);

        Video video = await _youtubeDownloadService.GetVideoAsync(url);
        Guid id = Guid.NewGuid();

        StreamManifest streamManifest = await _youtubeDownloadService.GetStreamManifestAsync(url);

        Regex illegalInFileName = new($"[{Regex.Escape(new string(Path.GetInvalidFileNameChars()))}]", RegexOptions.Compiled);
        string videoTitle = illegalInFileName.Replace(video.Title, "_");

        List<DownloadItemSteam> streams = streamManifest.Streams.Select((stream, i) => new DownloadItemSteam
            {
                Id = i,
                TempName = $"{id}_{i}.{stream.Container.Name}",
                TempPath = Path.Combine(_options.TempFolderPath, $"{id}_{i}.{stream.Container.Name}"),
                FileName = $"{videoTitle}.{stream.Container.Name}",
                FilePath = Path.Combine(_options.FullVideoFolderPath, $"{videoTitle}.{stream.Container.Name}"),
                Stream = stream
            })
            .ToList();

        int streamId = streams.Count;

        IAudioStreamInfo highestAudioStream = (IAudioStreamInfo)streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();

        string[] videoTypes = ["webm", "mp4"];

        foreach (string videoType in videoTypes)
        {
            IVideoStreamInfo? highestVideoStream = streamManifest.GetVideoOnlyStreams()
                .Where(info => string.Equals(info.Container.Name, videoType, StringComparison.InvariantCultureIgnoreCase))
                .TryGetWithHighestVideoQuality();

            if (highestVideoStream is null)
            {
                continue;
            }

            streams.Insert(0, new DownloadItemSteam
            {
                Id = streamId,
                TempName = $"{id}_{streamId}.{videoType}",
                TempPath = Path.Combine(_options.TempFolderPath, $"{id}_{streamId}.{videoType}"),
                FileName = $"{videoTitle}.{videoType}",
                FilePath = Path.Combine(_options.FullVideoFolderPath, $"{videoTitle}.{videoType}"),
                AudioStreamInfo = highestAudioStream,
                VideoStreamInfo = highestVideoStream
            });
        }

        DownloadItem item = DownloadItem.Create(id, url, streams, video).Result;

        _items.Add(item);
        _logger.LogDebug("Добавлено в очередь {Id}: {Url}", id, url);
        return item;
    }

    public void SetStreamToDownload(Guid downloadId, int streamId)
    {
        _logger.LogDebug("Try set stream to download: " + downloadId + " " + streamId);

        DownloadItem downloadItem = _items.First(x => x.Id == downloadId);
        DownloadItemSteam stream = downloadItem.Streams.First(x => x.Id == streamId);
        stream.State = DownloadItemState.Wait;

        _logger.LogDebug("Set stream to download: " + downloadId + " " + streamId);
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
        _logger.LogDebug("Попытка скачать из очереди: {Id} {StreamId}", downloadItem.Id, downloadStream.Id);

        CancellationTokenSource cancellationTokenSource = new();

        try
        {
            Task downloadTask = downloadStream.IsCombineAfterDownload
                ? DownloadCombinedStream(downloadStream, downloadItem, cancellationTokenSource)
                : DownloadMuxedStream(downloadStream, cancellationTokenSource);

            await downloadTask;
            downloadStream.State = DownloadItemState.Ready;
            _logger.LogDebug("Успешно скачан из очереди: {Id} {StreamId}", downloadItem.Id, downloadStream.Id);
        }
        catch (Exception exception)
        {
            await cancellationTokenSource.CancelAsync();

            downloadStream.State = DownloadItemState.Error;
            _logger.LogError(exception, "Не полупилось скачать из очереди: {Id} {StreamId}", downloadItem.Id, downloadStream.Id);
        }
    }

    private void RefreshDirectories()
    {
        Directory.CreateDirectory(_options.FullVideoFolderPath);

        if (Directory.Exists(_options.TempFolderPath))
        {
            Directory.Delete(_options.TempFolderPath, true);
        }

        Directory.CreateDirectory(_options.TempFolderPath);
    }

    private async Task DownloadCombinedStream(DownloadItemSteam downloadStream, DownloadItem downloadItem, CancellationTokenSource cancellationTokenSource)
    {
        string audioPath = downloadStream.TempPath.AddSuffixToFileName("audio");
        string videoPath = downloadStream.TempPath.AddSuffixToFileName("video");

        if (downloadStream.AudioStreamInfo == null || downloadStream.VideoStreamInfo == null)
        {
            _logger.LogError("Не удалось объединить ({MethodName}). Нет видео или аудио", nameof(DownloadCombinedStream));
            return;
        }

        ValueTask audioTask = _youtubeDownloadService.DownloadWithProgressAsync(downloadStream.AudioStreamInfo,
            audioPath,
            downloadStream.Title,
            downloadItem.Video.Title,
            cancellationTokenSource.Token);

        ValueTask videoTask = _youtubeDownloadService.DownloadWithProgressAsync(downloadStream.VideoStreamInfo,
            videoPath,
            downloadStream.Title,
            downloadItem.Video.Title,
            cancellationTokenSource.Token);

        await Task.WhenAll(audioTask.AsTask(), videoTask.AsTask());

        _logger.LogDebug("Попытка объединить видео и аудио: {Id} {StreamId}", downloadItem.Id, downloadStream.Id);

        // todo переделать нормально объединение

        string args = $"""
                       -i "{videoPath}" -i "{audioPath}" -c copy "{downloadStream.FilePath}"
                       """;

        await RunAsync(args);

        _logger.LogDebug("Успешно объединено видео и аудио: {Id} {StreamId}", downloadItem.Id, downloadStream.Id);
    }

    private async Task DownloadMuxedStream(DownloadItemSteam downloadStream, CancellationTokenSource cancellationTokenSource)
    {
        await _youtubeDownloadService.DownloadWithProgressAsync(downloadStream, cancellationTokenSource.Token);

        try
        {
            File.Move(downloadStream.TempPath, downloadStream.FilePath, true);
            _logger.LogDebug("Файл успешно перемещен \nиз {TempPath} \nв  {FilePath}", downloadStream.TempPath, downloadStream.FilePath);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Произошла ошибка при перемещении файла \nиз {TempPath} \nв  {FilePath}", downloadStream.TempPath, downloadStream.FilePath);
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